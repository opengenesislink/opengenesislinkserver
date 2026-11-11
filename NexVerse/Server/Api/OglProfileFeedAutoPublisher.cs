// SPDX-License-Identifier: MPL-2.0
// Resident-approved, idempotent and privacy-bound discovery activity posts.
// Unauthenticated event-bus payloads never authorize publication: every
// referenced entity is independently re-read from the authoritative store.
using System;
using System.Collections.Generic;
using log4net;
using NexVerse.Core.Audit;
using NexVerse.Core.Discovery;
using NexVerse.Core.Messaging;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class OglProfileFeedAutoPublisher : IDisposable
    {
        private static readonly ILog s_Log =
            LogManager.GetLogger(typeof(OglProfileFeedAutoPublisher));
        private readonly OglProfileFeedStore m_Feed;
        private readonly NexDiscoveryStore m_Discovery;
        private readonly IUserAccountService m_Accounts;
        private readonly IProfilesData m_Profiles;
        private readonly INexAuditSink m_Audit;
        private readonly IDisposable m_Events;
        private readonly IDisposable m_Classifieds;

        internal OglProfileFeedAutoPublisher(
            INexEventBus bus, OglProfileFeedStore feed,
            NexDiscoveryStore discovery, IUserAccountService accounts,
            IProfilesData profiles, INexAuditSink audit)
        {
            m_Feed = feed ?? throw new ArgumentNullException(nameof(feed));
            m_Discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            m_Audit = audit ?? NullNexAuditSink.Instance;
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            m_Events = bus.Subscribe("discovery.event.approved", OnActivity);
            m_Classifieds = bus.Subscribe("discovery.classified.approved", OnActivity);
        }

        private void OnActivity(NexEvent ev)
        {
            // The source label is an extra guard, NOT proof of authorization.
            // The durable source record and the resident's consent are checked
            // before any post can be committed.
            if (ev == null || ev.Source != "nexverse.world-api" ||
                !ev.Data.TryGetValue("entity_id", out string reference) ||
                !Guid.TryParse(reference, out Guid id) || id == Guid.Empty)
                return;
            string kind = ev.Name == "discovery.event.approved" ? "event" :
                ev.Name == "discovery.classified.approved" ? "classified" : "";
            if (kind.Length == 0) return;

            try
            {
                Guid ownerGuid;
                string title;
                if (kind == "event")
                {
                    NexEventRecord record = m_Discovery.GetEvent(id);
                    if (record == null ||
                        record.State != NexDiscoveryPublicationState.Approved)
                        return;
                    ownerGuid = record.OrganizerId;
                    title = record.Name;
                }
                else
                {
                    NexClassifiedRecord record = m_Discovery.GetClassified(id);
                    if (record == null ||
                        record.State != NexDiscoveryPublicationState.Approved)
                        return;
                    ownerGuid = record.OwnerId;
                    title = record.Name;
                }
                if (ownerGuid == Guid.Empty || string.IsNullOrWhiteSpace(title))
                    return;
                UUID owner = new UUID(ownerGuid);
                UserAccount account = m_Accounts.GetUserAccount(UUID.Zero,owner);
                if (account == null || !account.LocalToGrid || !account.LoginAllowed)
                    return;
                UserProfileProperties properties = new UserProfileProperties { UserId=owner };
                string err = "";
                if (!m_Profiles.GetAvatarProperties(ref properties,ref err) ||
                    !properties.PublishProfile)
                    return;

                // Never copy arbitrary text, private coordinates, visitor logs
                // or other event-bus payload fields into an auto post.
                string safeName = title.Trim();
                if (safeName.Length > 140) safeName = safeName.Substring(0,140);
                string body = kind == "event"
                    ? "Ich habe ein neues Event in Stadt NexVerse veroeffentlicht: " + safeName
                    : "Mein neuer Eintrag in Stadt NexVerse ist verfuegbar: " + safeName;
                if (m_Feed.TryCreateActivityPost(owner,kind,id,body))
                    m_Audit.Record(new NexAuditEvent(owner.ToString(),
                        "feed.activity.auto." + kind, "profile-feed:" + owner,
                        ev.CorrelationId, new Dictionary<string,string> {
                            ["entity_id"]=id.ToString("D")
                        }));
            }
            catch (Exception error)
            {
                // Publishing the source entity has already committed; never
                // undo its API response because optional feed posting failed.
                s_Log.Warn("[OGL-FEED]: Auto activity post failed: " +
                    error.GetType().Name);
            }
        }

        public void Dispose()
        {
            m_Events.Dispose();
            m_Classifieds.Dispose();
        }
    }
}
