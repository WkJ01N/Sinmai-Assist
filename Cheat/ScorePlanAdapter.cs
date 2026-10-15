using HarmonyLib;
using MAI2.Util;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SinmaiAssist.Cheat;

internal static class ScorePlanAdapter
{
    internal sealed class Plan
    {
        public int Player;
        public ScorePlanner.Request Request;
        public ScorePlanner.Result Result;
        public NoteDataList Notes;
        public GameScoreList Score;
    }

    private static readonly FieldInfo Results = AccessTools.Field(typeof(GameScoreList), "_judgeResultList");
    private static readonly FieldInfo ChainCounter = AccessTools.Field(typeof(GamePlayManager), "_chainCounter");
    private static readonly FieldInfo[] Fields = typeof(GameScoreList).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly string[] Counters = { "Combo", "MaxCombo", "Chain", "MaxChain", "Fast", "Late", "ComboAddThisFrame" };
    private static readonly FieldInfo[] CounterFields = Counters.Select(n => AccessTools.Field(typeof(GameScoreList), $"<{n}>k__BackingField")).ToArray();
    private static readonly FieldInfo ResetCombo = AccessTools.Field(typeof(GameScoreList), "<ResetCombo>k__BackingField");
    public static bool IsCompatible => Results != null && ChainCounter != null && ResetCombo != null && CounterFields.All(f => f != null);

    public static bool CanGenerate(bool party, out string error)
    {
        error = "Score generation supports normal solo and local two-player play only.";
        if (party || GameManager.IsTutorial || GameManager.IsKaleidxScopeMode || Singleton<GamePlayManager>.Instance.IsQuickRetry()) return false;
        int count = 0;
        for (int i = 0; i < 2; i++)
        {
            var user = Singleton<UserDataManager>.Instance.GetUserData(i);
            if (!user.IsEntry) continue;
            var score = Singleton<GamePlayManager>.Instance.GetGameScore(i);
            if (!user.IsHuman() || user.IsTrial() || !score.IsEnable || score.IsCourse || score.IsChallenge ||
                score.IsLifeTrackSkip || score.IsKaleidxScope || score.IsTrackSkip || score.SessionInfo.musicId >= 100000) return false;
            count++;
        }
        if (count == 0) return false;
        error = null;
        return true;
    }

    public static Plan[] Capture(FastSkip.SkipMode mode, int custom, bool forceMiss, string targetRank = null, MinimumCombo? minimum = null, bool lockJudged = false)
    {
        var config = SinmaiAssist.MainConfig.Cheat.RandomSkip;
        decimal lower = 0, upper = 101;
        bool highest = true;
        if (mode == FastSkip.SkipMode.Random)
        {
            string rankText = (targetRank ?? config.TargetRank ?? "SSS").Replace("+", "P");
            if (!Enum.TryParse("Rank_" + rankText, true, out MusicClearrankID rank) || !rank.IsValid())
                throw new InvalidOperationException("Invalid randomSkip.targetRank.");
            lower = GameManager.ConvAchiveIntToDecimal(rank.GetAchvement());
            highest = rank == MusicClearrankID.Rank_SSSP;
            upper = highest ? 101m : GameManager.ConvAchiveIntToDecimal(((MusicClearrankID)((int)rank + 1)).GetAchvement());
        }
        var table = new uint[6, 15];
        for (int kind = 0; kind < 6; kind++)
            for (int timing = 0; timing < 15; timing++)
                table[kind, timing] = NoteScore.GetJudgeScore((NoteJudge.ETiming)timing, (NoteScore.EScoreType)kind);
        var plans = new List<Plan>();
        for (int player = 0; player < 2; player++)
        {
            if (!Singleton<UserDataManager>.Instance.GetUserData(player).IsEntry) continue;
            var notes = NotesManager.Instance(player).getReader().GetNoteList();
            var score = Singleton<GamePlayManager>.Instance.GetGameScore(player);
            var snapshot = notes.Where(n => n != null && !n.type.isConnectSlide() &&
                GamePlayManager.NoteType2ScoreType(n.type) < NoteScore.EScoreType.BreakBonus)
                .Select(n => new ScorePlanner.Note { Index = n.indexNote,
                    Kind = (int)GamePlayManager.NoteType2ScoreType(n.type), Ex = n.type.isEx() || n.type.isExBreak(), Time = EndTime(n),
                    FixedTiming = lockJudged && score.GetScoreAt(n.indexNote).Judged ? score.GetScoreAt(n.indexNote).Timing : (int?)null }).ToArray();
            if (snapshot.Length == 0 || snapshot.Length != score.ScoreTotal.GetAllNoteNum()) throw new InvalidOperationException("Unsupported chart note layout.");
            if (notes.Any(n => n == null || n.indexNote < 0 || n.indexNote >= score.GetScoreLength()) ||
                notes.Where((n,index)=>n.indexNote!=index).Any() ||
                notes.Select(n => n.indexNote).Distinct().Count() != notes.Count)
                throw new InvalidOperationException("Unsupported note indices.");
            long total = snapshot.Sum(n => (long)table[n.Kind, 7]);
            long bonus = snapshot.Where(n => n.Kind == 3).Sum(n => (long)table[5, 7]);
            if (total != score.ScoreTotal._allPerfectScore || bonus != score.ScoreTotal._breakBonusScore)
                throw new InvalidOperationException("Chart totals do not match the game scoring table.");
            var request = new ScorePlanner.Request { Notes = snapshot, Scores = table,
                Minimum = minimum ?? config.MinimumCombo, Lower = lower, Upper = upper, IncludeUpper = highest,
                Seed = Guid.NewGuid().GetHashCode() };
            if (mode == FastSkip.SkipMode.Custom)
            {
                if (forceMiss) snapshot[new Random(request.Seed).Next(snapshot.Length)].ForceMiss = true;
                decimal maximum = bonus == 0 ? 100m : 101m;
                decimal largestLoss = snapshot.Max(n => 100m * table[n.Kind, 7] / total + (n.Kind == 3 ? (decimal)table[5, 7] / bonus : 0));
                request.Lower = Math.Max(0, Math.Min(custom, maximum) - largestLoss);
                request.Upper = Math.Min(custom, maximum);
                request.IncludeUpper = true;
                request.PreferUpper = true;
                request.Minimum = MinimumCombo.None;
            }
            plans.Add(new Plan { Player = player, Request = request, Notes = notes, Score = score });
        }
        return plans.ToArray();
    }

    internal static float EndTime(NoteData note)
    {
        float time = Math.Max(note.time.msec, note.end.msec);
        var children = new Stack<NoteData>(note.child);
        while (children.Count != 0)
        {
            var child = children.Pop();
            if (!child.type.isConnectSlide()) continue;
            time = Math.Max(time, child.end.msec);
            foreach (var next in child.child) children.Push(next);
        }
        return time;
    }

    internal static void ClearCounters(GameScoreList score)
    {
        foreach (var field in CounterFields) field.SetValue(score, 0u);
        ResetCombo.SetValue(score, false);
    }
    internal static void ClearRound(int player)
    {
        var score = Singleton<GamePlayManager>.Instance.GetGameScore(player);
        var shadow = new GameScoreList(player);
        CopyFields(score, shadow);
        var empty = new JudgeResultSt[score.GetScoreLength()];
        var notes = NotesManager.Instance(player).getReader().GetNoteList();
        for (int i = 0; i < empty.Length; i++)
        {
            empty[i].Type = NoteScore.EScoreType.End;
            empty[i].Timing = 15;
            empty[i].Judged = i < notes.Count && notes[i].type.isConnectSlide();
        }
        Results.SetValue(shadow, empty);
        ClearCounters(shadow);
        shadow.CalcUpdate(); shadow.UpdateAfter();
        CopyFields(shadow, score);
        ChainCounter.SetValue(Singleton<GamePlayManager>.Instance, 0u);
    }

    public static void Apply(Plan[] plans)
    {
        var shadows = new Dictionary<int, GameScoreList>();
        var oldJudged = plans.Select(p => p.Notes.Select(n => n.isJudged).ToArray()).ToArray();
        var restoreTouch = new List<Action>();
        var backups = plans.Select(p => CaptureFields(p.Score)).ToArray();
        uint oldChain = (uint)ChainCounter.GetValue(Singleton<GamePlayManager>.Instance);
        try
        {
            foreach (var plan in plans)
            {
                if (!ReferenceEquals(plan.Notes, NotesManager.Instance(plan.Player).getReader().GetNoteList()) ||
                    !ReferenceEquals(plan.Score, Singleton<GamePlayManager>.Instance.GetGameScore(plan.Player)))
                    throw new InvalidOperationException("Song changed during generation.");
                var shadow = new GameScoreList(plan.Player);
                CopyFields(plan.Score, shadow);
                var results = new JudgeResultSt[plan.Score.GetScoreLength()];
                foreach (var note in plan.Notes)
                {
                    bool excluded = note.type.isConnectSlide() || GamePlayManager.NoteType2ScoreType(note.type) >= NoteScore.EScoreType.BreakBonus;
                    note.isJudged = excluded;
                    if (excluded) results[note.indexNote] = new JudgeResultSt { Type = NoteScore.EScoreType.End, Timing = 15, Judged = true };
                }
                Results.SetValue(shadow, results);
                foreach (var counter in CounterFields) counter.SetValue(shadow, 0u);
                ResetCombo.SetValue(shadow, false);
                var reader = NotesManager.Instance(plan.Player).getReader();
                foreach (var chain in reader.GetTouchChainList().Concat(reader.GetTouchHoldChainList()))
                {
                    var savedChain = chain;
                    var count = chain.EndCount;
                    var judge = chain.ChainJudge;
                    restoreTouch.Add(() => { savedChain.EndCount = count; savedChain.ChainJudge = judge; });
                    chain.EndCount = 0;
                    chain.ChainJudge = NoteJudge.ETiming.TooLate;
                }
                shadows[plan.Player] = shadow;
            }
            uint chainCount = 0;
            var events = plans.SelectMany(p => p.Request.Notes.Select((n, i) => new { Plan = p, Note = n, Timing = p.Result.Timings[i] }))
                .OrderBy(e => e.Note.Time).ThenBy(e => e.Plan.Player).ThenBy(e => e.Note.Index);
            foreach (var e in events)
            {
                shadows[e.Plan.Player].SetResult(e.Note.Index, (NoteScore.EScoreType)e.Note.Kind, (NoteJudge.ETiming)e.Timing);
                chainCount = e.Timing == 0 || e.Timing == 14 ? 0u : chainCount + 1;
                foreach (var shadow in shadows.Values) shadow.SetChain(chainCount);
            }
            foreach (var plan in plans)
            {
                var shadow = shadows[plan.Player];
                shadow.CalcUpdate();
                if((int)shadow.ComboType<(int)plan.Request.Minimum || (plan.Request.ExcludeTheory&&(int)shadow.ComboType==4))
                    throw new InvalidOperationException("Generated result failed native combo validation.");
                if (Math.Abs(shadow.GetAchivement() - plan.Result.Achievement) > 0.000000000001m ||
                    shadow.GetAchivement() < plan.Request.Lower ||
                    (plan.Request.IncludeUpper ? shadow.GetAchivement() > plan.Request.Upper : shadow.GetAchivement() >= plan.Request.Upper))
                    throw new InvalidOperationException("Generated result failed native scoring validation.");
                shadow.UpdateAfter();
            }
            foreach (var plan in plans) CopyFields(shadows[plan.Player], plan.Score);
            ChainCounter.SetValue(Singleton<GamePlayManager>.Instance, chainCount);
            foreach (var plan in plans)
                plan.Score.VsRank = (uint)(1 + plans.Count(other => other.Player != plan.Player &&
                    other.Score.SessionInfo.musicId == plan.Score.SessionInfo.musicId && other.Score.GetAchivement() > plan.Score.GetAchivement()));
        }
        catch
        {
            for (int i = 0; i < plans.Length; i++)
            {
                RestoreFields(plans[i].Score, backups[i]);
                for (int j = 0; j < plans[i].Notes.Count; j++) plans[i].Notes[j].isJudged = oldJudged[i][j];
            }
            foreach (var restore in restoreTouch) restore();
            ChainCounter.SetValue(Singleton<GamePlayManager>.Instance, oldChain);
            throw;
        }
    }

    private static object[] CaptureFields(GameScoreList score) => Fields.Select(f =>
        f.GetValue(score) is Array array ? array.Clone() : f.GetValue(score)).ToArray();
    private static void CopyFields(GameScoreList source, GameScoreList target) => RestoreFields(target, CaptureFields(source));
    private static void RestoreFields(GameScoreList target, object[] values)
    {
        for (int i = 0; i < Fields.Length; i++)
        {
            var field = Fields[i];
            if (field.IsInitOnly)
            {
                if (values[i] is Array array) Array.Copy(array, (Array)field.GetValue(target), array.Length);
            }
            else field.SetValue(target, values[i] is Array array ? array.Clone() : values[i]);
        }
    }
}
