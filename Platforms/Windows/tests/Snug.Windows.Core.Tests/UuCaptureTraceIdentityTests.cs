using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuCaptureTraceIdentityTests
{
    [Theory]
    [InlineData("Snug-UU-Monitor-0123456789abcdef0123456789abcdef", true)]
    [InlineData("Snug-UU-Monitor-Service", false)]
    [InlineData("Snug-UU-Monitor-01234567-89ab-cdef-0123-456789abcdef", false)]
    [InlineData("Snug-UU-Capture-Probe-0123456789abcdef0123456789abcdef", false)]
    [InlineData("Snug-UU-Pilot-0123456789abcdef0123456789abcdef", false)]
    [InlineData("Other-Snug-UU-Monitor-0123456789abcdef0123456789abcdef", false)]
    [InlineData("Snug-UU-Monitor-not-a-guid", false)]
    [InlineData("NT Kernel Logger", false)]
    public void CleanupIsRestrictedToLegacyPersistentMonitor(string name, bool expected) =>
        Assert.Equal(expected, UuCaptureTraceIdentity.IsLegacyMonitor(name));
}