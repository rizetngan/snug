namespace Snug.Windows.Core;

/// <summary>
/// The existing UU tracker owns session lifetime. Returning a managed window
/// or changing the selected monitor does not alter phone-display intent.
/// </summary>
public static class PhoneSessionPresentation
{
    public static bool ShouldKeepDisplay(bool wantsPhoneScreen, bool enabled) =>
        wantsPhoneScreen && enabled;
}
