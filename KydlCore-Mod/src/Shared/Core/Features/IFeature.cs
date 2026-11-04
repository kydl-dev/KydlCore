namespace KydlCore.Core;

/// <summary>
/// One separate functionality of a mod (MIGRATION_DECISIONS A4, PROJECT_RULES 11). The mod's Bootstrap adds every feature to its
/// <see cref="FeatureRegistry"/>; the registry starts it while <see cref="IsEnabled"/> is true and stops it when that turns false
/// (live: the player's config change applies at once). Start registers what the feature contributes (patch handlers, hub tabs,
/// KY-Items behaviour); Stop removes ALL of it again, so no stale UI registration stays behind.
/// </summary>
public interface IFeature
{
    /// Stable id, e.g. "camera" (used in logs and test hooks).
    string Id { get; }

    /// The config gate: `EnableAll AND the feature's own Enable` read at call time (never cached). A mod with one feature: `EnableAll` only.
    bool IsEnabled();

    /// Turns the feature on. Expected outcome: true when everything it contributes is registered.
    bool Start();

    /// Turns the feature off. Expected outcome: true when nothing it contributed is left registered.
    bool Stop();
}
