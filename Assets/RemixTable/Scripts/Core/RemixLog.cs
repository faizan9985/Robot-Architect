using UnityEngine;

namespace RemixTable
{
    /// <summary>All Remix Table logging goes through here so it carries the [RemixTable] prefix.
    /// Keep it quiet during normal play: log on state changes and problems, never per frame.</summary>
    public static class RemixLog
    {
        public const string Prefix = "[RemixTable] ";

        public static void Info(string message, Object context = null) => Debug.Log(Prefix + message, context);
        public static void Warn(string message, Object context = null) => Debug.LogWarning(Prefix + message, context);
        public static void Error(string message, Object context = null) => Debug.LogError(Prefix + message, context);
    }
}
