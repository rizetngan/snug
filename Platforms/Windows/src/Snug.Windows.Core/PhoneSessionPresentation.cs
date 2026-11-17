namespace Snug.Windows.Core;

/// <summary>
/// Mirrors the episode-scoped "dismissed" state in upstream PhoneDesk.State:
/// leaving the phone screen does not recreate it during a transient UU reconnect,
/// but a new remote episode starts with a usable phone screen again.
/// </summary>
public sealed class PhoneSessionPresentation
{
    public bool DismissedInEpisode { get; private set; }

    public bool ShouldPresent(bool wantsPhoneScreen) =>
        wantsPhoneScreen && !DismissedInEpisode;

    public bool CanReenter(bool connected, bool wantsPhoneScreen, bool enabled) =>
        DismissedInEpisode && connected && wantsPhoneScreen && enabled;

    public bool TryReenter(bool connected, bool wantsPhoneScreen, bool enabled)
    {
        if (!CanReenter(connected, wantsPhoneScreen, enabled)) return false;
        EnableAgain();
        return true;
    }

    public void DismissForEpisode() => DismissedInEpisode = true;

    public void EnableAgain() => DismissedInEpisode = false;

    public void OnTransition(ConnectionTransitionKind transition)
    {
        if (transition == ConnectionTransitionKind.Ended)
            DismissedInEpisode = false;
    }
}
