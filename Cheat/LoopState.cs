using System;
using System.Collections.Generic;
using System.Linq;

namespace SinmaiAssist.Cheat;

// Pure interval and repeat policy; times are chart milliseconds, wait uses real seconds.
internal sealed class LoopState
{
    internal struct Span { public double Start, End; }
    public double A { get; private set; }
    public double B { get; private set; }
    public int Completed { get; private set; }
    public int Limit { get; private set; }
    public bool Finished => Limit > 0 && Completed >= Limit;
    public double WaitRemaining { get; private set; }
    public bool Counted { get; private set; }
    public void Configure(double a, double b, double end, int limit, IEnumerable<Span> spans)
    {
        if (double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b) ||
            a < 0 || b <= a || b > end || limit < 0) throw new ArgumentException("Choose 0 <= A < B within the song.");
        var all = spans.ToArray();
        // Transitive expansion keeps connected/overlapping long-note groups whole.
        bool changed;
        do
        {
            changed = false;
            foreach (var span in all)
            {
                if (span.Start < a && span.End > a) { a = Math.Max(0, span.Start); changed = true; }
                if (span.Start < b && span.End > b) { b = span.End; changed = true; }
            }
        } while (changed);
        A = a; B = b; Limit = limit; Completed = 0; Counted = false; WaitRemaining = 0;
    }
    public void Complete(double interval)
    {
        if (Counted) return;
        Counted = true; Completed++; WaitRemaining = interval;
    }
    public bool Wait(double seconds, bool paused)
    {
        if (paused || !Counted || Finished) return false;
        WaitRemaining = Math.Max(0, WaitRemaining - Math.Max(0, seconds));
        return WaitRemaining == 0;
    }
    public void BeginRound() { Counted = false; WaitRemaining = 0; }
}
