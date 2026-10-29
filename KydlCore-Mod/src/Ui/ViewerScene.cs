using System;
using Kydl.Common;
using Kydl.Ui;
using UnityEngine;

namespace KydlCore.Ui
{
    /// <summary>
    /// Game side of a ModelViewportView (from WhipItUp's WeaponViewerScene, shared through the mod template): makes a ModelViewerRig on a layer
    /// no game object uses. `makeModel(modelId)` returns an inert copy of what should be shown. One rig per menu open; the viewport disposes it.
    /// </summary>
    internal static class ViewerScene
    {
        /// Layers tried first (high, unnamed ones); the first that has no name in the game is used.
        const int FirstLayer = 31, LastLayer = 24;

        /// Expected outcome: a rig (never null; with no free layer it uses the last one tried and says so).
        internal static IModelScene Make(Func<string, GameObject> makeModel, ViewerSettings settings)
        {
            int layer = PickLayer();
            var main = Camera.main;
            bool mainSees = main != null && (main.cullingMask & (1 << layer)) != 0;
            ModLog.Verbose("ViewerScene: layer " + layer + " ('" + LayerMask.LayerToName(layer) + "'), main camera " + (main != null ? "'" + main.name + "' far clip " + main.farClipPlane.ToString("0") + " m, sees the layer " + mainSees + ", rendering path " + main.actualRenderingPath : "none")
                + " (the rig stands " + Vector3.Distance(main != null ? main.transform.position : Vector3.zero, ModelViewerRig.Park).ToString("0") + " m away)");
            return new ModelViewerRig(makeModel, layer, settings, UiKit.Track, s => ModLog.Verbose(s), "KydlCore_ViewerRig");
        }

        static int PickLayer()
        {
            for (int i = FirstLayer; i >= LastLayer; i--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            ModLog.Warn("ViewerScene: layers " + LastLayer + ".." + FirstLayer + " all have names in the game - using " + FirstLayer + " ('" + LayerMask.LayerToName(FirstLayer) + "'); the view may show other objects");
            return FirstLayer;
        }
    }
}
