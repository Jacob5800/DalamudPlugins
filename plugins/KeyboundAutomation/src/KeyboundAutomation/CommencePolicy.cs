using System;
using System.Linq;

namespace KeyboundAutomation;

internal static class CommencePolicy
{
    internal static bool Matches(bool hasPoppedEntry, bool isKeybound, string title, string dutyName)
    {
        if (hasPoppedEntry) return isKeybound;
        // Older/missing queue metadata may still be identified by the localized
        // title. Normalize UI line breaks and spacing, preserving an exact match.
        static string Normalize(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        return !string.IsNullOrWhiteSpace(dutyName)
            && string.Equals(Normalize(title), Normalize(dutyName), StringComparison.OrdinalIgnoreCase);
    }
}
