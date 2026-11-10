# NexVerse Messenger — Generation 1

Status: architecture foundation, NOT production-ready.

## Scope
Only local Stadt NexVerse accounts can authenticate in the PWA. Existing avatar credentials are verified by a trusted server-side OpenGenesisLINK adapter. External Hypergrid avatars are **contacts only**; they cannot sign in.

The PWA is served over HTTPS. Grid communication can continue using its existing HTTP endpoints; the browser must never call the Grid's HTTP login endpoint directly from an HTTPS origin.

## Components
- OGLMessenger: server-side adapter for avatar authentication, IM, offline IM, friends, presence, group chat and group notices.
- Messaging API: .NET 10 service, durable MariaDB storage, Redis presence, SignalR realtime delivery, Web Push.
- PWA: installable React/TypeScript application for iOS 16.4+ home-screen web push, Android and desktop.

## Invariants
1. Never store avatar passwords or expose grid password hashes to the PWA.
2. Authentication must validate **local** account ownership, not a visitor's foreign avatar identity.
3. Store refresh sessions as hashed opaque tokens, rotate on use, revoke on logout and suspicious reuse; use Secure HttpOnly SameSite cookies with CSRF protection.
4. Preserve native IM identity and routing; bridge messages to existing OpenGenesisLINK modules, not a second independent inworld IM implementation.
5. Idempotency keys and durable delivery/outbox prevent duplicate messages on reconnect/retry.
6. Remote HG offline delivery and remote online state are best effort, not guaranteed.
7. Group chat and group notices are separate permission-checked operations.
8. Visibility and block lists must apply to both presence and delivery.
9. The PWA is not continuously online when suspended; Web Push is only a wake-up notification.
10. Validate service-to-service requests (mTLS or signed short-lived tokens), rate limit, audit security events and never log message content or credentials by default.

## Implementation gates
- Identify existing IM, offline IM, friends, groups, HG routing and login interfaces in current server code.
- Implement a read-only adapter first, then outbound IM and inbound event hooks.
- Test local-to-local, local-to-HG, offline, duplicate delivery, logout and group permission cases before enabling production routing.
