// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Immutable, content-addressed S3 payload provider. Asset UUID and all
    /// OpenSim metadata stay in the existing FSAssets SQL metadata provider;
    /// the existing SHA-256 mapping points to this payload when S3 is selected.
    /// No bucket creation, enumeration, background migration or object deletion.
    /// An unavailable S3 store never falls back to a different writer/reader.
    /// </summary>
    public sealed class OGLS3ObjectStore : IDisposable
    {
        private readonly IAmazonS3 client;
        private readonly OGLS3StorageOptions options;
        private readonly bool ownsClient;

        public OGLS3ObjectStore(OGLS3StorageOptions options)
        {
            if (options == null || !options.Enabled)
                throw new InvalidOperationException("The S3 payload provider may only be created after explicit opt-in.");
            this.options = options;

            var settings = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                Timeout = TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds),
                MaxErrorRetry = options.MaxRetryAttempts - 1
            };
            client = new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey), settings);
            ownsClient = true;
        }

        // Only for isolated tests: the caller supplies a stub or a separate
        // test-bucket client. Never injected through the public asset API.
        internal OGLS3ObjectStore(OGLS3StorageOptions options, IAmazonS3 client)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            if (!options.Enabled || client == null)
                throw new ArgumentException("An enabled provider and S3 client are required.");
            this.client = client;
            ownsClient = false;
        }

        private static string ValidateHash(string hash)
        {
            if (hash == null || hash.Length != 64)
                throw new ArgumentException("Invalid SHA-256 object identifier.", nameof(hash));
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("SHA-256 digest must be lowercase hex.", nameof(hash));
            return hash;
        }

        private static string Key(string hash)
        {
            ValidateHash(hash);
            return "assets/sha256/" + hash.Substring(0, 2) + "/" + hash.Substring(2, 2) + "/" + hash;
        }

        private CancellationTokenSource Timeout()
            => new CancellationTokenSource(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        private static bool IsNotFound(AmazonS3Exception exception)
            => exception.StatusCode == HttpStatusCode.NotFound;

        /// <summary>One-shot startup check. Health probes are not scheduled.</summary>
        public void CheckBucket()
        {
            using var timeout = Timeout();
            try
            {
                client.HeadBucketAsync(new HeadBucketRequest
                {
                    BucketName = options.Bucket
                }, timeout.Token).GetAwaiter().GetResult();
            }
            catch (AmazonS3Exception error) when (IsNotFound(error))
            {
                throw new InvalidOperationException(
                    "Configured OGL S3 bucket does not exist. Buckets are never created automatically.");
            }
            // Authentication failures, 5xx and network errors intentionally
            // propagate rather than silently selecting another backend.
        }

        /// <summary>HEAD only: no asset body download.</summary>
        public bool Exists(string hash)
        {
            using var timeout = Timeout();
            try
            {
                client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = options.Bucket,
                    Key = Key(hash)
                }, timeout.Token).GetAwaiter().GetResult();
                return true;
            }
            catch (AmazonS3Exception error) when (IsNotFound(error))
            {
                return false;
            }
        }

        /// <summary>
        /// Get an immutable object and verify its hash before returning a
        /// byte[]. An interrupted response cannot produce a successful read.
        /// </summary>
        public byte[] Get(string hash)
        {
            using var timeout = Timeout();
            try
            {
                using var response = client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = options.Bucket,
                    Key = Key(hash)
                }, timeout.Token).GetAwaiter().GetResult();

                using var output = new MemoryStream();
                response.ResponseStream.CopyToAsync(output, 81920, timeout.Token)
                    .GetAwaiter().GetResult();
                byte[] data = output.ToArray();
                string received = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                if (!String.Equals(received, hash, StringComparison.Ordinal))
                    throw new InvalidDataException("OGL S3 object SHA-256 integrity verification failed.");
                return data;
            }
            catch (AmazonS3Exception error) when (IsNotFound(error))
            {
                return null;
            }
        }

        /// <summary>
        /// Put content by SHA-256 without overwriting existing keys. An HTTP
        /// 412 is accepted only after retrieving and hashing the existing data.
        /// On success, HEAD must confirm the expected byte length.
        /// </summary>
        public string Put(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            string hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            using (var input = new MemoryStream(data, writable: false))
            using (var timeout = Timeout())
            {
                var request = new PutObjectRequest
                {
                    BucketName = options.Bucket,
                    Key = Key(hash),
                    InputStream = input,
                    IfNoneMatch = "*",
                    UseChunkEncoding = false
                };
                request.Metadata["sha256"] = hash;
                try
                {
                    using var response = client.PutObjectAsync(request, timeout.Token)
                        .GetAwaiter().GetResult();
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        throw new IOException("OGL S3 did not acknowledge the object upload.");
                }
                catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // Immutable key already exists: accept only exact same bytes.
                    byte[] existing = Get(hash);
                    if (existing == null || !existing.AsSpan().SequenceEqual(data))
                        throw new InvalidDataException("Existing OGL S3 object is missing or corrupt.");
                    return hash;
                }
            }

            using (var timeout = Timeout())
            {
                var metadata = client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = options.Bucket,
                    Key = Key(hash)
                }, timeout.Token).GetAwaiter().GetResult();
                if (metadata.ContentLength != data.LongLength)
                    throw new InvalidDataException("OGL S3 upload length verification failed.");
            }
            return hash;
        }

        public void Dispose()
        {
            if (ownsClient)
                client.Dispose();
        }
    }
}
