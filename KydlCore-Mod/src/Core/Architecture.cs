namespace KydlCore.Core
{
    /// <summary>Which architecture this DLL is built with; the lab wrapper refuses a DLL without the tag (lab-guarded.ps1 -Marker KydlCore-KYDL-SPEC-V1).</summary>
    internal static class Architecture
    {
        internal const int Version = 1;
        internal static readonly string Tag = "KydlCore-KYDL-SPEC-V1";
    }
}
