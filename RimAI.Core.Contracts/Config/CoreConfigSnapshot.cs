namespace RimAI.Core.Contracts.Config
{
    /// <summary>
    /// Immutable external configuration snapshot exposed to other mods or UI.
    /// Fields must be additive-only for backward compatibility.
    /// </summary>
    public sealed class CoreConfigSnapshot
    {
        public string Version { get; private set; }
        public string Locale { get; private set; }
        public bool DebugPanelEnabled { get; private set; }
        public bool VerboseLogs { get; private set; }

        public CoreConfigSnapshot(string version, string locale, bool debugPanelEnabled, bool verboseLogs)
        {
            Version = version;
            Locale = locale;
            DebugPanelEnabled = debugPanelEnabled;
            VerboseLogs = verboseLogs;
        }
    }
}


