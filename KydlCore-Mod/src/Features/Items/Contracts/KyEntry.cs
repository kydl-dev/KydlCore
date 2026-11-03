using System.Collections.Generic;
using UnityEngine;

namespace KydlCore.Features.Items.Contracts
{
    /// <summary>Whether a <see cref="KyEntry"/> is an inventory item or a build piece.</summary>
    public enum KyKind { Item, Piece }

    /// <summary>
    /// One of the user's prefabs in the <see cref="KyItems"/> registry (DESIGN 3 / 6): where it comes from (mod GUID and name), what it is and how it is
    /// named. Read only for consumers; made by the registry.
    /// </summary>
    public sealed class KyEntry
    {
        /// <summary>The namespaced KY-Items id ("stevethetroll:slippers") of an explicit contribution; "" for a prefab found the legacy way.</summary>
        public string Id = "";
        /// <summary>The prefab name (the key: one entry per prefab name, case ignored).</summary>
        public string Prefab;
        /// <summary>The prefab itself (null in unit tests).</summary>
        public GameObject PrefabObject;
        /// <summary>The GUID of the mod that registered it (e.g. "com.kydldev.DrainTarPit"); "" when unknown.</summary>
        public string ModGuid = "";
        /// <summary>The mod's name, or "build category KY-Items" for a piece found only by its category.</summary>
        public string ModName = "";
        public KyKind Kind;
        /// <summary>The localisation key / name from the game data ("$item_x" or a plain name); <see cref="DisplayName"/> is the shown text.</summary>
        public string NameToken = "";
        Sprite _icon;
        /// <summary>
        /// The icon, read from the prefab EVERY time it is asked: a mod may swap it after registering (DrainTarPit puts a placeholder first and renders the
        /// real icons when a world is loaded), so a copy taken at registration would be the placeholder for good. Falls back to the icon seen at registration.
        /// </summary>
        public Sprite Icon
        {
            get { Sprite live = LiveIcon(); return live != null ? live : _icon; }
            set { _icon = value; }
        }

        Sprite LiveIcon()
        {
            if (PrefabObject == null) return null;
            ItemDrop drop = PrefabObject.GetComponent<ItemDrop>();
            if (drop != null)
            {
                Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                return icons != null && icons.Length > 0 ? icons[0] : null;
            }
            Piece piece = PrefabObject.GetComponent<Piece>();
            return piece != null ? piece.m_icon : null;
        }
        /// <summary>Free tags the registering mod gave (may be empty).</summary>
        public IReadOnlyList<string> Tags = new string[0];
        /// <summary>How the registry found it: "Contribute" (the spec API), "Register", "Jotunn" / "category" (legacy, until every mod contributes).</summary>
        public string Source = "";

        /// <summary>The name shown to the player: the token localised when the game's text is ready, else the token or the prefab name.</summary>
        public string DisplayName
        {
            get
            {
                string token = string.IsNullOrEmpty(NameToken) ? Prefab : NameToken;
                return Localization.instance != null ? Localization.instance.Localize(token) : token;
            }
        }
    }
}
