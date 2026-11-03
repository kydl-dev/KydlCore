using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KydlCore.Ui
{
    /// <summary>
    /// Unity-only preview of the mod's menu (NOT compiled into the mod: only Runtime/ is). Add this component to any GameObject of an empty
    /// scene and press Play: the menu opens in the Game view in the same look as in game (game fonts from Assets/KydlCore.Ui/Preview/GameAssets).
    /// Set the Game view to 1920x1080 (the panel scales from that like in game).
    /// </summary>
    public sealed class MenuPreview : MonoBehaviour
    {
        /// <summary>What the preview shows: the KY hub (fake mods with 1, 3 and 4 tabs) or the example menu.</summary>
        public enum PreviewKind { Hub, HubManyTabs, MainMenu }

        public PreviewKind kind = PreviewKind.Hub;
        HubView _hub;

        const string FontFolder = "Assets/KydlCore.Ui/Preview/GameAssets/";
        // NotoSansSC-Regular (8.5 MB, D-043): only KydlCore-Unity has it, OUTSIDE Assets/KydlCore.Ui (KydlUiSync mirrors that folder into every Unity project).
        const string KydlFontFolder = "Assets/KydlCore_UI/Preview/PreviewFonts/";

        void OnEnable()
        {
            LoadGameFonts();
            UiKit.Reset();
            var root = UiKit.Root;
            if (root == null) { Debug.LogError("[MenuPreview] UI Toolkit panel could not be made"); return; }
            if (kind == PreviewKind.Hub || kind == PreviewKind.HubManyTabs)
            {
                var model = kind == PreviewKind.Hub ? FakeHub() : FakeHubMany();
                _hub = new HubView(model, UiText.English, (mod, tab) => FakePage(), () => Debug.Log("[MenuPreview] close pressed"));
                root.Add(_hub.Root);
                Debug.Log("[MenuPreview] KY hub shown (fake mods)");
                return;
            }
            root.Add(MainMenuView.Build(UiText.English, () => Debug.Log("[MenuPreview] close pressed")));
            Debug.Log("[MenuPreview] KydlCore menu shown");
        }

        void OnDisable()
        {
            if (_hub != null) { _hub.Dispose(); _hub = null; }
            UiKit.Reset();
        }

        // Many mods and many tabs (T-035): row 1 and row 2 fill more than 2 lines, one title is longer than the window.
        static HubModel FakeHubMany()
        {
            var m = new HubModel();
            foreach (string n in new[] { "ChestDecor", "DrainTarPit", "KydlCore", "RearViewCamera", "SteveTheTroll", "ValheimDiscordRelay", "WhipItUp", "WaterAndTarPools", "FollowPlayer", "RidingCrop", "SpareMod", "AnotherSpareMod", "TheModWithAnExtremelyLongNameThatDoesNotFitInOneLineOfTheWindowAtAll" })
                m.AddMod("com.kydldev." + n, n);
            string g = "com.kydldev.ChestDecor";
            string[] tabs = { "Look", "Placement", "Camera", "General", "Appearance", "Advanced", "Keys", "Colours", "Storage", "Sorting", "Sounds", "Effects", "Language", "About" };
            for (int i = 0; i < tabs.Length; i++) m.AddTab(g, tabs[i].ToLowerInvariant(), tabs[i], i);
            m.Select(g, "placement");
            return m;
        }

        // Fake mods for the hub: 4 tabs, 1 tab, no registered tab (= the automatic "Settings" tab), 3 tabs. Names are PascalCase, as in game.
        static HubModel FakeHub()
        {
            var m = new HubModel();
            m.AddMod("com.kydldev.ChestDecor", "ChestDecor");
            m.AddTab("com.kydldev.ChestDecor", "look", "Look", 0);
            m.AddTab("com.kydldev.ChestDecor", "placement", "Placement", 1);
            m.AddTab("com.kydldev.ChestDecor", "camera", "Camera", 2);
            m.AddTab("com.kydldev.ChestDecor", "general", "General", 3);
            m.AddMod("com.kydldev.DrainTarPit", "DrainTarPit");
            m.AddTab("com.kydldev.DrainTarPit", "general", "General", 0);
            m.AddMod("com.kydldev.KydlCore", "KydlCore");
            m.AddMod("com.kydldev.RearViewCamera", "RearViewCamera");
            m.AddTab("com.kydldev.RearViewCamera", "camera", "Camera", 0);
            m.AddTab("com.kydldev.RearViewCamera", "image", "Image", 1);
            m.AddTab("com.kydldev.RearViewCamera", "keys", "Keys", 2);
            m.Select("com.kydldev.ChestDecor", "placement");
            return m;
        }

        static VisualElement FakePage()
        {
            var page = new VisualElement();
            page.Add(UiControls.Toggle("Example switch", true, on => Debug.Log("[MenuPreview] switch " + on)));
            page.Add(UiControls.Row("Example value", UiControls.Slider(0f, 10f, 5f, "0.0", v => Debug.Log("[MenuPreview] slider " + v))));
            return page;
        }

        // UiKit finds the game fonts by name among the loaded fonts (NotoSansSC-Regular, NotoEmoji-Regular; the old Norse / Norsebold for the
        // build without KYDL_CORE_PUBLIC): load them first, like the game has them loaded.
        static void LoadGameFonts()
        {
#if UNITY_EDITOR
            foreach (var f in new[] { "Norse.otf", "Norsebold.otf", "NotoEmoji-Regular.ttf" })
                if (AssetDatabase.LoadAssetAtPath<Font>(FontFolder + f) == null) Debug.LogWarning("[MenuPreview] font missing: " + FontFolder + f + " (Kydl > Sync shared UI kit)");
            const string noto = "NotoSansSC-Regular.otf";
            var font = AssetDatabase.LoadAssetAtPath<Font>(KydlFontFolder + noto);
            if (font == null) Debug.LogWarning("[MenuPreview] font missing: " + KydlFontFolder + noto + " (copy it from the ripped game, Assets/Font)");
            else Debug.Log("[MenuPreview] font loaded: '" + font.name + "' from " + KydlFontFolder + noto);
#endif
        }
    }
}
