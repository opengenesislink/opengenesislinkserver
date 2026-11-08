/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

// A pool of jobs or workitems with same method (callback) but diferent argument (as object) to run in main threadpool
// can have up to m_concurrency number of execution threads
// it will hold each thread up to m_threadsHoldtime ms waiting for more work, before releasing it back to the pool.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// Bounded or legacy-unbounded worker queue. The callback is never invoked
    /// under the queue lock, and accepted-but-unprocessed items are discarded
    /// exactly once when the engine is stopped. Active callbacks may finish
    /// after Dispose; the queue/CTS live until their final worker exits.
    /// </summary>
    public class ObjectJobEngine : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly object m_mainLock = new object();
        private readonly string m_name;
        private readonly int m_threadsHoldtime;
        private readonly int m_concurrency;
        private readonly Action<object> m_onDiscard;

        private BlockingCollection<object> m_jobQueue;
        private CancellationTokenSource m_cancelSource;
        private WaitCallback m_callback;
        // Protected by m_mainLock. Include scheduled-but-not-yet-started workers.
        private int m_numberThreads;
        private volatile bool m_isRunning;
        private bool m_stopRequested;

        public ObjectJobEngine(WaitCallback callback, string name, int threadsHoldtime = 1000,
            int concurrency = 1, int maxQueuedJobs = 0, Action<object> onDiscard = null)
        {
            m_name = name;
            m_threadsHoldtime = threadsHoldtime;
            m_concurrency = Math.Max(1, concurrency);
            m_onDiscard = onDiscard;

            if (callback != null)
            {
                m_callback = callback;
                m_jobQueue = maxQueuedJobs > 0
                    ? new BlockingCollection<object>(Math.Clamp(maxQueuedJobs, 1, 65536))
                    : new BlockingCollection<object>();
                m_cancelSource = new CancellationTokenSource();
                m_isRunning = true;
            }
        }

        // Only managed resources: a finalizer would unnecessarily promote a
        // busy worker/queue into a later GC generation.
        public void Dispose()
        {
            Stop();
        }

        /// <summary>Number of pending jobs, excluding active callbacks.</summary>
        public int Count
        {
            get
            {
                lock (m_mainLock)
                    return m_jobQueue?.Count ?? 0;
            }
        }

        // Historically Cancel stopped the worker's CancellationTokenSource.
        // Make that stop explicit so future Enqueue cannot silently accept work
        // against an already cancelled token.
        public void Cancel()
        {
            Stop();
        }

        private void Stop()
        {
            List<object> discarded = new List<object>();
            lock (m_mainLock)
            {
                if (m_stopRequested)
                    return;

                m_stopRequested = true;
                m_isRunning = false;
                m_cancelSource?.Cancel();

                if (m_jobQueue != null)
                {
                    while (m_jobQueue.TryTake(out object job))
                        discarded.Add(job);
                }

                if (m_numberThreads == 0)
                    ReleaseResourcesLocked();
            }

            // User callbacks must never execute under m_mainLock.
            foreach (object job in discarded)
                Discard(job);
        }

        private void ReleaseResourcesLocked()
        {
            // Called only after every scheduled/active worker has exited.
            m_jobQueue?.Dispose();
            m_jobQueue = null;
            m_cancelSource?.Dispose();
            m_cancelSource = null;
            m_callback = null;
        }

        public bool Enqueue(object job)
        {
            lock (m_mainLock)
            {
                if (!m_isRunning || m_jobQueue == null || !m_jobQueue.TryAdd(job))
                    return false;

                if (m_numberThreads < m_concurrency && m_numberThreads < m_jobQueue.Count)
                {
                    // Reserve before queueing to the ThreadPool: an immediate
                    // callback must never decrement before the increment.
                    ++m_numberThreads;
                    try
                    {
                        Util.FireAndForget(ProcessRequests, null, m_name, false);
                    }
                    catch (Exception e)
                    {
                        --m_numberThreads;
                        m_log.ErrorFormat("[ObjectJob {0}]: Unable to start worker: {1}", m_name, e);
                        // Stop and discard safely outside this lock if the
                        // threadpool refuses scheduling rather than leak jobs.
                        ThreadPool.QueueUserWorkItem(_ => Stop());
                    }
                }
                return true;
            }
        }

        private void Discard(object job)
        {
            if (m_onDiscard == null)
                return;
            try
            {
                m_onDiscard(job);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ObjectJob {0}]: Discard callback failed: {1}", m_name, e);
            }
        }

        private void ProcessRequests(object state)
        {
            BlockingCollection<object> queue;
            CancellationToken token;
            WaitCallback callback;
            lock (m_mainLock)
            {
                queue = m_jobQueue;
                token = m_cancelSource.Token;
                callback = m_callback;
            }

            try
            {
                while (m_isRunning)
                {
                    object job;
                    try
                    {
                        if (!queue.TryTake(out job, m_threadsHoldtime, token))
                        {
                            // Keep the old worker idle timeout and honor jobs
                            // concurrently enqueued just before the timeout.
                            lock (m_mainLock)
                            {
                                if (m_isRunning && queue.Count > 0)
                                    continue;
                            }
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!m_isRunning)
                    {
                        Discard(job);
                        break;
                    }

                    try
                    {
                        callback(job);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[ObjectJob {0}]: Job failed, continuing. Exception {1}", m_name, e);
                    }
                }
            }
            finally
            {
                lock (m_mainLock)
                {
                    --m_numberThreads;
                    if (m_stopRequested && m_numberThreads == 0)
                        ReleaseResourcesLocked();
                }
            }
        }
    }
}
