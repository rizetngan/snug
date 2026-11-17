using System.Xml.Linq;
using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class VddResolutionProfileTests
{
    private const string Template = """
        <?xml version="1.0" encoding="utf-8"?>
        <vdd_settings>
          <monitors><count>1</count></monitors>
          <gpu><friendlyname>default</friendlyname></gpu>
          <global><g_refresh_rate>60</g_refresh_rate></global>
          <resolutions>
            <resolution><width>1080</width><height>1900</height><refresh_rate>60</refresh_rate></resolution>
          </resolutions>
          <edid><CustomEdid>true</CustomEdid></edid>
        </vdd_settings>
        """;

    [Fact]
    public void StandardIphoneProfileAdvertisesHardwareVerifiedTwentyPointSteps()
    {
        var modes = VddResolutionProfile.Create(PhoneChoice.Standard);

        Assert.Equal(28, modes.Count);
        Assert.Equal(new VddResolutionMode(1080, 1900, 60), modes[0]);
        Assert.Equal(new VddResolutionMode(2160, 3800, 60), modes[^1]);
        Assert.All(modes, mode => Assert.Equal(60, mode.RefreshRate));
        Assert.Equal(modes.Count, modes.Select(mode => (mode.Width, mode.Height)).Distinct().Count());
        Assert.True(modes.Zip(modes.Skip(1)).All(pair => pair.First.Width < pair.Second.Width));
    }

    [Fact]
    public void ApplyReplacesOnlyResolutionListAndPreservesDriverOptions()
    {
        var updated = XDocument.Parse(VddResolutionProfile.Apply(Template, PhoneChoice.Standard));
        var root = updated.Root!;

        Assert.Equal("1", root.Element("monitors")!.Element("count")!.Value);
        Assert.Equal("default", root.Element("gpu")!.Element("friendlyname")!.Value);
        Assert.Equal("true", root.Element("edid")!.Element("CustomEdid")!.Value);
        Assert.Equal(28, root.Element("resolutions")!.Elements("resolution").Count());
        Assert.Equal("1080", root.Element("resolutions")!.Elements("resolution").First().Element("width")!.Value);
        Assert.Equal("1900", root.Element("resolutions")!.Elements("resolution").First().Element("height")!.Value);
    }

    [Fact]
    public void ApplyCreatesMissingResolutionSectionAfterGlobalSettings()
    {
        const string template = "<vdd_settings><global><g_refresh_rate>60</g_refresh_rate></global><edid /></vdd_settings>";

        var updated = XDocument.Parse(VddResolutionProfile.Apply(template, PhoneChoice.Standard));
        var root = updated.Root!;

        Assert.NotNull(root.Element("resolutions"));
        Assert.True(root.Elements().ToList().FindIndex(element => element.Name == "resolutions")
            > root.Elements().ToList().FindIndex(element => element.Name == "global"));
    }

    [Fact]
    public void CustomPhoneUsesItsOwnScreenRatio()
    {
        var choice = new PhoneChoice(null, new PhoneScreen(750, 1334, 375));

        var modes = VddResolutionProfile.Create(choice);

        Assert.True(modes[0].Width > 1080);
        Assert.True(modes[0].Height < modes[0].Width * 2);
        Assert.Equal(60, modes[0].RefreshRate);
    }

    [Theory]
    [InlineData(540, 540)]
    [InlineData(550, 560)]
    [InlineData(570, 580)]
    [InlineData(800, 800)]
    [InlineData(1070, 1080)]
    [InlineData(1080, 1080)]
    public void SnapWidthRoundsUpToTheNextDriverAdvertisedMode(int requested, int expected)
    {
        Assert.Equal(expected, VddResolutionProfile.SnapWidth(PhoneChoice.Standard, requested));
    }

    [Fact]
    public void StandardProfileAdvertisesModesAtMostTwentyPointsApart()
    {
        var widths = VddResolutionProfile.SupportedWidths(PhoneChoice.Standard);
        Assert.Equal(28, widths.Count);
        Assert.Equal(540, widths[0]);
        Assert.Equal(1080, widths[^1]);
        Assert.All(widths.Zip(widths.Skip(1)), pair =>
            Assert.InRange(pair.Second - pair.First, 1, 20));
    }

    [Fact]
    public void InvalidRefreshRateIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VddResolutionProfile.Create(PhoneChoice.Standard, 0));
    }
}
