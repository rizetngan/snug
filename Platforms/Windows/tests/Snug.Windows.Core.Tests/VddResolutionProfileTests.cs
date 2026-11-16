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
    public void StandardIphoneProfileAdvertisesOnlyTheValidatedBaseMode()
    {
        var modes = VddResolutionProfile.Create(PhoneChoice.Standard);

        Assert.Single(modes);
        Assert.Equal(new VddResolutionMode(1080, 1900, 60), modes[0]);
        Assert.Equal(modes[0], modes[^1]);
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
        Assert.Single(root.Element("resolutions")!.Elements("resolution"));
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

    [Fact]
    public void InvalidRefreshRateIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VddResolutionProfile.Create(PhoneChoice.Standard, 0));
    }
}
