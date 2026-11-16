using System.Xml.Linq;

namespace Snug.Windows.Core;

public sealed record VddResolutionMode(int Width, int Height, int RefreshRate);

/// <summary>
/// Generates the VDD mode list from the same phone geometry used by Snug's ScreenFit.
/// The returned order is intentional: VDD uses the first mode as its preferred mode.
/// </summary>
public static class VddResolutionProfile
{
    public static IReadOnlyList<VddResolutionMode> Create(
        PhoneChoice choice,
        int refreshRate = 60)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (refreshRate is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(refreshRate));

        var fit = new ScreenFit(choice.Screen);
        // The signed VDD driver on Windows 10 can enumerate its adapter but fail
        // to expose any monitor with our previous 55-mode profile. The verified
        // recovery on 2026-11-16 restored a working SNG00F0 monitor using one
        // base resolution. Keep logical ScreenFit widths separate from the driver
        // mode list until a larger set passes real driver and UU acceptance.
        var size = fit.PixelSizeFor(fit.BaseWidth);
        return
        [
            new VddResolutionMode(
                checked((int)Math.Round(size.Width, MidpointRounding.AwayFromZero)),
                checked((int)Math.Round(size.Height, MidpointRounding.AwayFromZero)),
                refreshRate)
        ];
    }

    public static string Apply(
        string settingsXml,
        PhoneChoice choice,
        int refreshRate = 60)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsXml);
        var document = XDocument.Parse(settingsXml, LoadOptions.PreserveWhitespace);
        var root = document.Root
            ?? throw new InvalidOperationException("VDD 配置缺少根节点。");

        var resolutions = root.Element("resolutions");
        if (resolutions is null)
        {
            resolutions = new XElement("resolutions");
            var global = root.Element("global");
            if (global is null) root.Add(resolutions);
            else global.AddAfterSelf(resolutions);
        }

        resolutions.ReplaceNodes(Create(choice, refreshRate).Select(mode =>
            new XElement("resolution",
                new XElement("width", mode.Width),
                new XElement("height", mode.Height),
                new XElement("refresh_rate", mode.RefreshRate))));

        return document.ToString(SaveOptions.None);
    }
}
