using System;
using UnityEngine;

namespace KydlCore.Features.Items.Contracts;

/// <summary>
/// What a mod contributes to KY-Items (spec 5, MIGRATION_DECISIONS B13): a namespaced id, the owning mod, a display name, the prefab, recipe information
/// and callbacks. Hand it to <see cref="KyItems.Contribute"/> from the mod's Awake / feature Start. The registry is Jotunn-free; with
/// <see cref="RegisterInGame"/> KydlCore also adds the prefab to the game (items with their recipe, pieces to the hammer in the KY-Items category).
/// </summary>
public sealed class KyContribution
{
    /// <summary>"&lt;modid&gt;:&lt;item&gt;", e.g. "stevethetroll:slippers" (modid = the GUID after "com.kydldev.", lowercase).</summary>
    public string Id = "";
    /// <summary>The owning mod's GUID ("com.kydldev.SteveTheTroll").</summary>
    public string ModGuid = "";
    /// <summary>The shown name or its localisation token ("$item_slippers"); empty = read from the prefab.</summary>
    public string DisplayName = "";
    /// <summary>The prefab (required).</summary>
    public GameObject Prefab;
    public KyKind Kind;
    /// <summary>How it is made (null = not craftable / the mod handles it).</summary>
    public KyRecipe Recipe;
    /// <summary>Free tags (may be empty).</summary>
    public string[] Tags = new string[0];
    /// <summary>True = KydlCore adds the prefab to the game (instead of the mod calling Jotunn itself).</summary>
    public bool RegisterInGame;
    /// <summary>Called once the contribution is accepted (with the prefab), e.g. to attach the mod's behaviour components.</summary>
    public Action<GameObject> OnRegistered;
}

/// <summary>Recipe information of a contribution.</summary>
public sealed class KyRecipe
{
    /// <summary>Crafting station prefab name ("piece_workbench"); "" = crafted by hand (items) / no station (pieces).</summary>
    public string CraftingStation = "";
    public int MinStationLevel = 1;
    /// <summary>How many one craft makes (items).</summary>
    public int Amount = 1;
    public KyResource[] Resources = new KyResource[0];
}

/// <summary>One resource of a recipe.</summary>
public sealed class KyResource
{
    public string Item = "";
    public int Amount = 1;
    public int AmountPerLevel;
    public bool Recover = true;
    public KyResource() { }
    public KyResource(string item, int amount) { Item = item ?? ""; Amount = amount; }
}

/// <summary>The answer of <see cref="KyItems.Contribute"/>: accepted (with the normalised id) or rejected with the reason.</summary>
public sealed class KyResult
{
    public bool Accepted { get; private set; }
    public string Id { get; private set; }
    public string Reason { get; private set; }
    KyResult() { }
    internal static KyResult Ok(string id, string reason = "") { return new KyResult { Accepted = true, Id = id ?? "", Reason = reason ?? "" }; }
    internal static KyResult Rejected(string id, string reason) { return new KyResult { Accepted = false, Id = id ?? "", Reason = reason ?? "" }; }
    public override string ToString() { return (Accepted ? "accepted " : "rejected ") + Id + (string.IsNullOrEmpty(Reason) ? "" : ": " + Reason); }
}
