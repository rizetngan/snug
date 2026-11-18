using Snug.Windows.Core;
using Xunit;

namespace Snug.Windows.Core.Tests;

public sealed class UuTcpPeerEvidenceTests
{
    private const uint ServerPid = 20992;
    private const uint Established = UuTcpPeerEvidence.EstablishedState;
    private const uint RemoteIpv4 = 0xF3BABA2A; // non-loopback IPv4 in native table byte order
    private const uint LoopbackIpv4 = 0x0100007F; // 127.0.0.1

    [Fact]
    public void EstablishedExternalPeerOnNonWebPortIsPositiveEvidence()
    {
        Assert.True(UuTcpPeerEvidence.IsActivePeer(ServerPid, ServerPid, Established, RemoteIpv4, 2533));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(443)]
    [InlineData(0)]
    public void PersistentWebRelayAndInvalidPortsAreNotPeerEvidence(int port)
    {
        Assert.False(UuTcpPeerEvidence.IsActivePeer(ServerPid, ServerPid, Established, RemoteIpv4, port));
    }

    [Fact]
    public void LoopbackConnectionIsNotPeerEvidence()
    {
        Assert.False(UuTcpPeerEvidence.IsActivePeer(ServerPid, ServerPid, Established, LoopbackIpv4, 2533));
    }

    [Fact]
    public void ConnectionOwnedByAnotherProcessIsIgnored()
    {
        Assert.False(UuTcpPeerEvidence.IsActivePeer(ServerPid + 1, ServerPid, Established, RemoteIpv4, 2533));
    }

    [Fact]
    public void NonEstablishedConnectionIsIgnored()
    {
        Assert.False(UuTcpPeerEvidence.IsActivePeer(ServerPid, ServerPid, 4, RemoteIpv4, 2533));
    }

    [Fact]
    public void MissingRemoteAddressIsIgnored()
    {
        Assert.False(UuTcpPeerEvidence.IsActivePeer(ServerPid, ServerPid, Established, 0, 2533));
    }
}