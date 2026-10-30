using Kydl.Ui;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KydlCore.Ui
{
    /// <summary>
    /// Unity-only preview of the mod's menu (NOT compiled into the mod: only Runtime/ is). Add this component to any GameObject of an empty
    /// scene and press Play: the menu opens in the Game view in the same look as in game (game fonts from Assets/Kydl.Ui/Preview/GameAssets).
    /// Set the Game view to 1920x1080 (the panel scales from that like in game).
    /// </summary>
    public sealed class MenuPreview : MonoBehaviour
    {
        const string FontFolder = "Assets/Kydl.Ui/Preview/GameAssets/";
        // NotoSansSC-Regular (8.5 MB, D-043): only KydlCore-Unity has it, OUTSIDE Assets/Kydl.Ui (KydlUiSync mirrors that folder into every Unity project).
        const string KydlFontFolder = "Assets/KydlCore_UI/Preview/PreviewFonts/";

        void OnEnable()
        {
            LoadGameFonts();
            UiKit.Reset();
            var root = UiKit.Root;
            if (root == null) { Debug.LogError("[MenuPreview] UI Toolkit panel could not be made"); return; }
            root.Add(MainMenuView.Build(UiText.English, () => Debug.Log("[MenuPreview] close pressed")));
            Debug.Log("[MenuPreview] KydlCore menu shown");
        }

        void OnDisable() { UiKit.Reset(); }

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
