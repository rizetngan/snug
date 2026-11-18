namespace Snug.Windows.Core;

/// <summary>
/// Filters TCP rows to a GameViewerServer-owned, established remote peer.
/// Loopback and the persistent HTTP(S) relay/control ports are not session
/// evidence by themselves.
/// </summary>
public static class UuTcpPeerEvidence
{
    public const uint EstablishedState = 5;

    public static bool IsActivePeer(
        uint owningProcessId,
        uint expectedProcessId,
        uint state,
        uint remoteAddress,
        int remotePort)
    {
        if (expectedProcessId == 0 || owningProcessId != expectedProcessId
            || state != EstablishedState || remoteAddress == 0)
            return false;

        var firstOctet = remoteAddress & 0xFFu;
        return firstOctet != 127 && remotePort is > 0 and not 80 and not 443;
    }
}