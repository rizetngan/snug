namespace Snug.Windows.Core;

public sealed record ProtectedDisplayMode(
    string DeviceId, string DeviceName, bool IsPrimary,
    int X, int Y, uint Width, uint Height, uint Hertz,
    uint BitsPerPixel, uint Orientation, uint FixedOutput, uint Flags)
{
    public bool SameMode(ProtectedDisplayMode other) =>
        IsPrimary == other.IsPrimary && X == other.X && Y == other.Y &&
        Width == other.Width && Height == other.Height && Hertz == other.Hertz &&
        BitsPerPixel == other.BitsPerPixel && Orientation == other.Orientation &&
        FixedOutput == other.FixedOutput && Flags == other.Flags;
}

public sealed record DisplayModeDrift(ProtectedDisplayMode Before, ProtectedDisplayMode Current);

public static class DisplayModeProtection
{
    /// <summary>Match a stable, unique monitor identity. Never use a recycled DISPLAY number.
    /// A disconnected, newly attached, or ambiguous monitor is left alone.</summary>
    public static IReadOnlyList<DisplayModeDrift> FindDrift(
        IReadOnlyList<ProtectedDisplayMode> before, IReadOnlyList<ProtectedDisplayMode> current)
    {
        var result = new List<DisplayModeDrift>();
        foreach (var old in before)
        {
            if (string.IsNullOrWhiteSpace(old.DeviceId) ||
                before.Count(d => SameIdentity(d, old)) != 1) continue;
            var matches = current.Where(d => SameIdentity(d, old)).ToArray();
            if (matches.Length != 1 || old.SameMode(matches[0])) continue;
            result.Add(new(old, matches[0]));
        }
        return result;
    }

    private static bool SameIdentity(ProtectedDisplayMode a, ProtectedDisplayMode b) =>
        StringComparer.OrdinalIgnoreCase.Equals(a.DeviceId, b.DeviceId);
}