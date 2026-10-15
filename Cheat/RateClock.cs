using System;

namespace SinmaiAssist.Cheat;

// Integrate elapsed time. A rate change affects future time, never past time.
internal sealed class RateClock
{
    private double _raw, _rate;
    public double Position { get; private set; }
    public bool Initialized { get; private set; }
    public void Clear() { Initialized=false; }
    public void Reset(double raw, double position, double rate)
    { _raw=raw; Position=position; _rate=rate; Initialized=true; }
    public double Sample(double raw, double rate)
    {
        if (!Initialized || raw < _raw) Reset(raw, raw, rate);
        else { Position += Math.Max(0,raw-_raw)*_rate; _raw=raw; _rate=rate; }
        return Position;
    }
}
