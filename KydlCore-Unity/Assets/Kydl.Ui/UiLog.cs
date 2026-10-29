using System;

namespace Kydl.Ui
{
    /// <summary>
    /// The shared UI code (Kydl.Ui) logs through these hooks: in Unity they write to the console, in the game the mod points them at
    /// its own ModLog (Plugin.Awake: debug-only lines through ModLog.Verbose, so they follow the mod's debug switch and the 2.5 s repeat filter).
    /// </summary>
    internal static class UiLog
    {
        public static Action<string> VerboseSink = s => UnityEngine.Debug.Log("[Kydl UI] " + s);
        public static Action<string> WarnSink = s => UnityEngine.Debug.LogWarning("[Kydl UI] " + s);
        public static Action<string> ErrorSink = s => UnityEngine.Debug.LogError("[Kydl UI] " + s);

        public static void Verbose(string s) { VerboseSink?.Invoke(s); }
        public static void Warn(string s) { WarnSink?.Invoke(s); }
        public static void Error(string s) { ErrorSink?.Invoke(s); }
    }
}
