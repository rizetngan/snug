using System.Buffers.Binary;

namespace Snug.Windows.Core;

/// <summary>Fixed-size, one-way local pilot protocol. No commands or private
/// capture payloads. The reader must independently verify process generation.</summary>
public readonly record struct UuCaptureFrame(Guid FeedId, long Sequence, DateTimeOffset ObservedAt,
    UuServerGeneration Server, UuCaptureSnapshot Capture)
{
    public const int Size = 64;
    private const uint Magic = 0x31554353; // SCU1
    public const string PipePrefix = "Snug-UU-Capture-Pilot-";
    public static string PipeName(Guid id) => PipePrefix + id.ToString("N");

    public byte[] Encode()
    {
        var bytes = new byte[Size];
        var p = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(p, Magic);
        FeedId.TryWriteBytes(p[4..20]);
        BinaryPrimitives.WriteInt64LittleEndian(p[20..],Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(p[28..],ObservedAt.UtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(p[36..],Server.Pid);
        BinaryPrimitives.WriteInt64LittleEndian(p[40..],Server.StartedUtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(p[48..],(int)Capture.Phase);
        BinaryPrimitives.WriteInt32LittleEndian(p[52..],Capture.ObservedContexts);
        p[56]=Capture.CompleteInventory?(byte)1:(byte)0;
        return bytes;
    }

    public static bool TryDecode(ReadOnlySpan<byte> p,out UuCaptureFrame frame)
    {
        frame=default;
        if(p.Length!=Size||BinaryPrimitives.ReadUInt32LittleEndian(p)!=Magic||p[56]>1)return false;
        var id=new Guid(p[4..20]);
        var seq=BinaryPrimitives.ReadInt64LittleEndian(p[20..]);
        var now=BinaryPrimitives.ReadInt64LittleEndian(p[28..]);
        var pid=BinaryPrimitives.ReadInt32LittleEndian(p[36..]);
        var started=BinaryPrimitives.ReadInt64LittleEndian(p[40..]);
        var phase=(UuCapturePhase)BinaryPrimitives.ReadInt32LittleEndian(p[48..]);
        var count=BinaryPrimitives.ReadInt32LittleEndian(p[52..]);
        bool complete=p[56]==1;
        if(failed())return false;
        frame=new(id,seq,new DateTimeOffset(now,TimeSpan.Zero),new(pid,started),new(phase,count,complete));
        return true;
        bool failed()=>id==Guid.Empty||seq<=0||pid<=0||started<=0||
            now<started||now>DateTimeOffset.MaxValue.UtcTicks||
            !Enum.IsDefined(phase)||count<0||count>1024||
            (phase==UuCapturePhase.Active&&count==0)||
            (phase!=UuCapturePhase.Active&&count!=0)||
            (phase==UuCapturePhase.Inactive&&!complete)||
            (phase==UuCapturePhase.Unknown&&complete);
    }
}

/// <summary>Freshness and source identity gate. A stale, malformed, or
/// reordered heartbeat must never override compatibility behavior.</summary>
public sealed class UuCaptureFeed(Guid expectedId)
{
    public static readonly TimeSpan Freshness=TimeSpan.FromSeconds(3);
    private UuCaptureFrame? _latest;
    private long _lastSequence;

    public bool Accept(UuCaptureFrame frame,DateTimeOffset now)
    {
        if(frame.FeedId!=expectedId||frame.Sequence<=_lastSequence||
            frame.ObservedAt>now.AddSeconds(1)||now-frame.ObservedAt>Freshness)
        {Invalidate();return false;}
        // Validate all semantic invariants even for an in-process frame.
        if(!UuCaptureFrame.TryDecode(frame.Encode(),out _)){Invalidate();return false;}
        _lastSequence=frame.Sequence;_latest=frame;return true;
    }

    public UuCaptureSnapshot Get(DateTimeOffset now,UuServerGeneration? actual)
    {
        if(_latest is not { } f||f.Server!=actual||now-f.ObservedAt>Freshness||
            f.ObservedAt>now.AddSeconds(1))
            return new(UuCapturePhase.Unknown,0,false);
        return f.Capture;
    }

    public void Invalidate()=>_latest=null;
}