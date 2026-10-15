using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SinmaiAssist.Cheat;

public enum MinimumCombo { None, FC, FCPlus, AP, APPlus }

// No Unity/game calls: inputs are copied on the game thread; search runs on a worker.
internal static class ScorePlanner
{
    internal sealed class Note
    {
        public int Index;
        public int Kind;
        public bool Ex;
        public float Time;
        public bool ForceMiss;
        public int? FixedTiming;
    }

    internal sealed class Request
    {
        public Note[] Notes;
        public uint[,] Scores;
        public MinimumCombo Minimum;
        public decimal Lower, Upper;
        public bool IncludeUpper;
        public bool PreferUpper;
        public bool ExcludeTheory;
        public int Seed;
        public int NodeLimit = 500000;
        public int TimeLimitMs = 1000;
    }

    internal sealed class Result
    {
        public int[] Timings;
        public decimal Achievement;
        public string Error;
        public bool Success => Timings != null;
    }

    private sealed class Choice
    {
        public long Loss;
        public int[] Timings;
    }

    public static Result Generate(Request request, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested) return Fail("Score generation cancelled.");
        if (request.Notes == null || request.Notes.Length == 0 || request.Scores == null ||
            request.Scores.GetLength(0) != 6 || request.Scores.GetLength(1) != 15 ||
            request.Lower < 0 || request.Upper < request.Lower || !Enum.IsDefined(typeof(MinimumCombo), request.Minimum))
            return Fail("Invalid score plan input.");

        var random = new Random(request.Seed);
        long normal = 0, bonus = 0;
        foreach (var note in request.Notes)
        {
            if (note.Kind < 0 || note.Kind > 4) return Fail("Unsupported note type.");
            normal = checked(normal + request.Scores[note.Kind, 7]);
            if (note.Kind == 3) bonus = checked(bonus + request.Scores[5, 7]);
        }
        if (normal == 0) return Fail("Chart has no scoring notes.");
        long denominator = checked(normal * (bonus == 0 ? 1 : bonus));
        long perfect = checked((bonus == 0 ? 100 : 101) * denominator);
        var choices = new Choice[request.Notes.Length][];
        long divisor = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            var note = request.Notes[i];
            if (note.ForceMiss && request.Minimum != MinimumCombo.None)
                return Fail("Forced Miss conflicts with the minimum combo restriction.");
            var equivalent = new Dictionary<long, List<int>>();
            if (note.FixedTiming.HasValue && (note.FixedTiming < 0 || note.FixedTiming >= 15 ||
                !Allowed(note.FixedTiming.Value, request.Minimum, note.Ex)))
                return Fail("Existing judgements violate the requested combo restriction.");
            for (int timing = 0; timing < 15; timing++)
            {
                if (note.FixedTiming.HasValue && timing != note.FixedTiming.Value) continue;
                bool miss = timing == 0 || timing == 14;
                if (note.ForceMiss ? !miss : !Allowed(timing, request.Minimum, note.Ex)) continue;
                long loss = checked(100 * (bonus == 0 ? 1 : bonus) *
                    (request.Scores[note.Kind, 7] - (long)request.Scores[note.Kind, timing]));
                if (note.Kind == 3)
                    loss = checked(loss + normal * (request.Scores[5, 7] - (long)request.Scores[5, timing]));
                if (loss < 0) return Fail("Unsupported scoring table.");
                if (request.Minimum == MinimumCombo.APPlus && loss != 0) continue;
                if (!equivalent.TryGetValue(loss, out var timings)) equivalent[loss] = timings = new List<int>();
                timings.Add(timing);
                divisor = Gcd(divisor, loss);
            }
            if (equivalent.Count == 0) return Fail("No allowed judgement for a note.");
            choices[i] = equivalent.Select(p => new Choice { Loss = p.Key, Timings = p.Value.ToArray() }).ToArray();
        }
        if (divisor == 0) divisor = 1;
        foreach (var list in choices) foreach (var choice in list) choice.Loss /= divisor;

        // Convert the achievement interval to an exact integer loss interval.
        decimal rawLow = (perfect - request.Upper * denominator) / divisor;
        decimal rawHigh = (perfect - request.Lower * denominator) / divisor;
        long low = Math.Max(0, (long)(request.IncludeUpper ? decimal.Ceiling(rawLow) : decimal.Floor(rawLow) + 1));
        if (request.ExcludeTheory) low = Math.Max(1, low);
        long high = (long)decimal.Floor(rawHigh);
        if (high < low) return Fail("No achievable score in this rank interval.");

        // Shuffle equal types, then solve larger contributions first. Equivalent timing
        // variants are sampled only after finding a score, so DX/Fast/Late can vary too.
        var order = Enumerable.Range(0, choices.Length).ToArray();
        Shuffle(order, random);
        order = order.OrderByDescending(i => choices[i].Max(c => c.Loss)).ToArray();
        var min = new long[order.Length + 1];
        var max = new long[order.Length + 1];
        var gcd = new long[order.Length + 1];
        for (int i = order.Length - 1; i >= 0; i--)
        {
            var list = choices[order[i]];
            min[i] = checked(min[i + 1] + list.Min(c => c.Loss));
            max[i] = checked(max[i + 1] + list.Max(c => c.Loss));
            gcd[i] = gcd[i + 1];
            foreach (var choice in list) gcd[i] = Gcd(gcd[i], choice.Loss - list.Min(c => c.Loss));
        }
        low = Math.Max(low, min[0]);
        high = Math.Min(high, max[0]);
        if (high < low) return Fail("Target rank is incompatible with this chart / combo restriction.");
        long target = request.PreferUpper ? low : low + (long)(random.NextDouble() * (high - low));

        // Iterative bounded DFS avoids stack overflows on long charts. Memoization and
        // suffix bounds reject unreachable scores without unbounded random retries.
        var attempts = new Choice[order.Length][];
        var next = new int[order.Length];
        var selected = new Choice[order.Length];
        var sums = new long[order.Length + 1];
        var failed = new HashSet<(int, long)>();
        int depth = 0, nodes = 0;
        var watch = Stopwatch.StartNew();
        while (depth >= 0)
        {
            if (++nodes > request.NodeLimit || watch.ElapsedMilliseconds > request.TimeLimitMs)
                return Fail("Score search limit reached. Try another rank / combo restriction.");
            if (cancellation.IsCancellationRequested) return Fail("Score generation cancelled.");
            if (depth == order.Length)
            {
                if (sums[depth] >= low && sums[depth] <= high)
                {
                    var result = new int[order.Length];
                    for (int i = 0; i < order.Length; i++)
                    {
                        var variants = selected[i].Timings;
                        result[order[i]] = variants[random.Next(variants.Length)];
                    }
                    return new Result { Timings = result,
                        Achievement = (perfect - (decimal)sums[depth] * divisor) / denominator };
                }
                depth--;
                continue;
            }
            if (attempts[depth] == null)
            {
                long total = sums[depth];
                long reachableLow = Math.Max(low - total, min[depth]);
                long reachableHigh = Math.Min(high - total, max[depth]);
                bool possible = reachableLow <= reachableHigh;
                if (possible && gcd[depth] != 0)
                    possible = decimal.Ceiling((decimal)(reachableLow - min[depth]) / gcd[depth]) <=
                        decimal.Floor((decimal)(reachableHigh - min[depth]) / gcd[depth]);
                if (!possible || failed.Contains((depth, total))) { depth--; continue; }
                var list = choices[order[depth]].ToArray();
                Shuffle(list, random);
                double desired = max[depth] == min[depth] ? list[0].Loss :
                    list.Min(c => c.Loss) + (double)(target - total - min[depth]) /
                    (max[depth] - min[depth]) * (list.Max(c => c.Loss) - list.Min(c => c.Loss));
                attempts[depth] = list.OrderBy(c => Math.Abs(c.Loss - desired)).ToArray();
                next[depth] = 0;
            }
            if (next[depth] == attempts[depth].Length)
            {
                failed.Add((depth, sums[depth]));
                attempts[depth] = null;
                depth--;
                continue;
            }
            selected[depth] = attempts[depth][next[depth]++];
            sums[depth + 1] = checked(sums[depth] + selected[depth].Loss);
            depth++;
            if (depth < order.Length) attempts[depth] = null;
        }
        return Fail("No achievable score in this rank / combo combination.");
    }

    private static bool Allowed(int timing, MinimumCombo minimum, bool ex)
    {
        if (ex && timing != 0 && timing != 7 && timing != 14) return false;
        if (minimum == MinimumCombo.None) return true;
        if (timing == 0 || timing == 14) return false;
        if (minimum == MinimumCombo.FC) return true;
        if (timing == 1 || timing == 13) return false;
        return minimum < MinimumCombo.AP || (timing >= 5 && timing <= 9);
    }

    private static Result Fail(string message) => new Result { Error = message };
    private static long Gcd(long a, long b) { while (b != 0) { long n = a % b; a = b; b = n; } return a; }
    private static void Shuffle<T>(T[] array, Random random)
    {
        for (int i = array.Length - 1; i > 0; i--) { int j = random.Next(i + 1); (array[i], array[j]) = (array[j], array[i]); }
    }
}
