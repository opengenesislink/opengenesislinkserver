# NexVerse Messenger v1 API contract (draft)

Base path: /api/v1. All user routes require an authenticated local avatar session.

POST /auth/login — avatar name + password, issue secure session cookie; errors do not reveal account existence.
POST /auth/refresh — rotate session token.
POST /auth/logout — revoke device session.
GET /auth/me — avatar UUID, display name, session metadata.
GET /friends — local and HG contacts with presence capability metadata.
GET /conversations — paginated inbox.
GET /conversations/{id}/messages?cursor= — paginated history.
POST /im — recipient identity, body, clientMessageId (UUID), returns durable message ID and delivery state.
GET /groups — memberships and effective rights.
GET /groups/{id}/messages — paginated group chat.
POST /groups/{id}/messages — authorized group message.
GET /groups/{id}/notices — group notices.
POST /groups/{id}/notices — authorized notice, distinct from chat.
POST /push/subscriptions — save Web Push subscription for current device.
DELETE /push/subscriptions/{id} — remove subscription.
GET /devices — active sessions.
DELETE /devices/{id} — revoke device.

SignalR /hubs/messenger: message.received, message.status, presence.changed, group.message, group.notice, conversation.updated.

External HG recipient identity must contain both a stable avatar identity and validated home-grid origin; never route on display name alone.

All mutating requests require authorization, payload size limits, idempotency and anti-abuse controls. Exact JSON schemas and OpenGenesisLINK adapter contracts will be finalized after repository integration inspection.
