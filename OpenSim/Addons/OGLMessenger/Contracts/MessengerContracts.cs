using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSim.Addons.OGLMessenger.Contracts;

/// <summary>
/// Identity is never derived from a display name. Foreign HG contacts are not login principals.
/// </summary>
public sealed record AvatarIdentity(Guid AgentId, string? HomeGridUri)
{
    public bool IsLocal => string.IsNullOrEmpty(HomeGridUri);
}

public enum MessengerPresence
{
    Offline,
    Mobile,
    Inworld,
    InworldAndMobile,
    Unknown
}

public enum DeliveryState
{
    Accepted,
    Routed,
    Delivered,
    Failed,
    RemoteUnconfirmed
}

public sealed record MessengerMessage(
    Guid MessageId,
    Guid ClientMessageId,
    AvatarIdentity Sender,
    AvatarIdentity Recipient,
    string Body,
    DateTimeOffset CreatedAt,
    DeliveryState DeliveryState);

public sealed record SendMessageRequest(
    Guid ClientMessageId,
    AvatarIdentity Recipient,
    string Body);

public sealed record FriendPresence(
    AvatarIdentity Friend,
    MessengerPresence Status,
    bool StatusVisible);

public sealed record GroupMembership(
    Guid GroupId,
    string Name,
    bool CanSendChat,
    bool CanSendNotices);

/// <summary>
/// The implementation must validate credentials against the existing local avatar account service.
/// No plaintext passwords or credential hashes are persisted by the messenger.
/// </summary>
public interface ILocalAvatarAuthenticator
{
    Task<AvatarIdentity?> AuthenticateAsync(string avatarName, string password, CancellationToken cancellationToken);
}

/// <summary>
/// Bridges existing OpenGenesisLINK IM routing; does not directly impersonate a viewer session.
/// </summary>
public interface IGridMessageBridge
{
    Task<DeliveryState> SendAsync(AvatarIdentity authenticatedSender, MessengerMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendPresence>> GetFriendsAsync(AvatarIdentity authenticatedUser, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupMembership>> GetGroupsAsync(AvatarIdentity authenticatedUser, CancellationToken cancellationToken);
}

/// <summary>
/// Store-first and idempotent: (sender, clientMessageId) must be unique.
/// An outbox worker dispatches accepted messages and records retry-safe delivery state.
/// </summary>
public interface IMessengerMessageStore
{
    Task<MessengerMessage> AcceptAsync(
        AvatarIdentity authenticatedSender,
        SendMessageRequest request,
        CancellationToken cancellationToken);
    Task<MessengerMessage?> FindAsync(Guid messageId, CancellationToken cancellationToken);
    Task SetDeliveryStateAsync(Guid messageId, DeliveryState state, CancellationToken cancellationToken);
}
