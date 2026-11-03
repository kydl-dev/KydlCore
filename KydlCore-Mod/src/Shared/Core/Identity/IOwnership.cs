namespace KydlCore.Core;

/// <summary>
/// Network ownership of one object (its ZDO). Executors check <see cref="IsOwner"/> FIRST and answer Rejected("not the owner")
/// otherwise: only the owner acts, everyone else reads (Architecture V2, owner-gated executors).
/// </summary>
public interface IOwnership
{
    /// This client owns the object's ZDO.
    bool IsOwner { get; }

    /// The owning player's id (0 = no owner / not networked).
    long OwnerId { get; }
}