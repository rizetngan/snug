namespace Snug.Windows.Core;

/// <summary>Only an explicitly accepted, still-current quit-stalled notice can
/// authorize a force exit. PID alone is not a process identity on Windows.</summary>
public sealed record QuitProcessIdentity(int ProcessId, long StartedUtcTicks, string ExecutablePath)
{
    public bool Matches(QuitProcessIdentity? other) =>
        other is not null && ProcessId == other.ProcessId && StartedUtcTicks == other.StartedUtcTicks &&
        StringComparer.OrdinalIgnoreCase.Equals(ExecutablePath, other.ExecutablePath);
}

public sealed record QuitStalledNotice(string AppName, QuitProcessIdentity Identity, DateTimeOffset ExpiresAt)
{
    public bool CanForce(QuitProcessIdentity? current, DateTimeOffset now) =>
        now <= ExpiresAt && Identity.Matches(current);
}