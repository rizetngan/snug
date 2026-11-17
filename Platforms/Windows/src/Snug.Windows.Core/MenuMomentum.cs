namespace Snug.Windows.Core;

/// <summary>Menu fling follows upstream Scrolling.swift: 100 ms release
/// samples, 60 Hz decay .95, starting speed 200 and stopping speed 40.
/// Offsets are DIPs and accumulated without rounding.</summary>
public sealed class MenuMomentum
{
    private const double FlingWindow = .1;
    private const double MinSampleSpan = .04;
    private const double MaxSpeed = 4000;
    private readonly List<(double Time,double Offset)> _samples = [];
    private double _scale = 1;
    private double _velocity;
    public bool IsCoasting => _velocity != 0;

    public void Begin(double time,double offset,double scale = 1)
    {
        Stop(); _scale=Math.Max(1,scale); _samples.Clear();
        Sample(time,offset);
    }

    public void Sample(double time,double offset)
    {
        if(_samples.Count>0 && time<=_samples[^1].Time) return;
        _samples.Add((time,offset));
        if(_samples.Count>30) _samples.RemoveAt(0);
    }

    public bool Release(double time)
    {
        var recent=_samples.Where(s=>s.Time<=time && time-s.Time<=FlingWindow).ToArray();
        if(recent.Length<2)return false;
        var first=recent[0];var last=recent[^1];
        if(last.Time-first.Time<MinSampleSpan)
        {
            var older=_samples.Where(s=>last.Time-s.Time>=MinSampleSpan).ToArray();
            if(older.Length>0)first=older[^1];
        }
        var dt=last.Time-first.Time;
        if(dt<=0)return false;
        var v=Math.Clamp((last.Offset-first.Offset)/dt,-MaxSpeed*_scale,MaxSpeed*_scale);
        _velocity=Math.Abs(v)>200*_scale?v:0;
        return IsCoasting;
    }

    public double Advance(double elapsed)
    {
        if(!IsCoasting||elapsed<=0)return 0;
        // Match upstream decay at 60Hz but account for delayed render ticks.
        // Limit catch-up after a stalled UI rather than suddenly jumping rows.
        var frames=Math.Min(.05,elapsed)*60;
        var decay=Math.Pow(.95,frames);
        var delta=_velocity*.95*(1-decay)/(.05*60);
        _velocity*=decay;
        if(Math.Abs(_velocity)<40*_scale)Stop();
        return delta;
    }

    public void Stop()=>_velocity=0;
}