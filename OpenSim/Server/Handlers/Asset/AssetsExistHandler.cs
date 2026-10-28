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

using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Asset
{
    public class AssetsExistHandler : BaseStreamHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_AssetService;
        // Zero preserves the legacy private AssetService behavior. Limits are
        // set only by the public HG connector to bound asset-ID enumeration.
        private int m_MaxIds;
        private long m_MaxXmlCharacters;

        public AssetsExistHandler(IAssetService service, int maxIds, long maxXmlCharacters) :
            this(service)
        {
            m_MaxIds = Math.Max(0, maxIds);
            m_MaxXmlCharacters = Math.Max(0, maxXmlCharacters);
        }

        public AssetsExistHandler(IAssetService service) :
            base("POST", "/get_assets_exist")
        {
            m_AssetService = service;
        }

        public AssetsExistHandler(IAssetService service, IServiceAuth auth) :
            base("POST", "/get_assets_exist", auth)
        {
            m_AssetService = service;
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            XmlSerializer xs;

            string[] ids;
            try
            {
                xs = new XmlSerializer(typeof(string[]));
                // Defend the public HG existence endpoint against unbounded
                // XML and entity payloads, without affecting authorized GET
                // downloads or ordinary foreign-grid inventory transfers.
                using (XmlReader xmlReader = XmlReader.Create(request, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = m_MaxXmlCharacters,
                    CloseInput = false
                }))
                    ids = (string[])xs.Deserialize(xmlReader);
            }
            catch (Exception)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return null;
            }

            if (ids == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return null;
            }

            if (m_MaxIds > 0 && ids.Length > m_MaxIds)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return null;
            }

            bool[] exist = m_AssetService.AssetsExist(ids);

            xs = new XmlSerializer(typeof(bool[]));
            return ServerUtils.SerializeResult(xs, exist);
        }
    }
}
