using System;
using System.Linq;
using HarmonyLib;
using MAI2.Util;
using Manager;
using UnityEngine;

namespace SinmaiAssist.Cheat;

internal static class PracticeLoop
{
    public static double? A, B;
    public static int Count;
    public static double PreRoll = 3, Interval = 5;
    public static float Speed = 1;
    public static bool PreservePitch = true;
    public static float ActiveSpeed { get; private set; } = 1;
    public static bool ActivePreservePitch { get; private set; } = true;
    public static void AdjustSpeed(float delta) => Speed=(float)Math.Round(Math.Max(.5,Math.Min(2,Speed+delta)),2);
    public static bool Session { get; private set; }
    public static bool Running { get; private set; }
    public static string Status = "";
    public static bool InputValid = true;
    public static bool Installed;
    public static readonly LoopState State = new LoopState();
    public static double WindowA { get; private set; }
    public static double WindowB { get; private set; }
    private static bool _entered, _waiting;
    private static double _lastReal;
    public static void ResetSong() { A = B = null; Session = Running = _entered = _waiting = _intervalPaused = false; InputValid=true;Status = ""; }
    public static void Start()
    {
        BatchController.RequireIdle();
        if (!InputValid) throw new InvalidOperationException("Correct the practice inputs first.");
        if (float.IsNaN(Speed) || float.IsInfinity(Speed) || Speed < .5f || Speed > 2f) throw new InvalidOperationException("Practice speed must be 0.50x to 2.00x.");
        if (!Installed || !PlaySession.CanPractice || !PlaySession.Installed) throw new InvalidOperationException("Practice reset interfaces unavailable.");
        string timelineError = PlaySession.RequireSeek();
        if (timelineError != null) throw new InvalidOperationException(timelineError);
        if (!ScorePlanAdapter.CanGenerate(PlaySession.IsParty, out var reason)) throw new InvalidOperationException(reason);
        if (!A.HasValue || !B.HasValue) throw new InvalidOperationException("Set A and B first.");
        var notes = Enumerable.Range(0, 2).Where(p => Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry)
            .SelectMany(p => NotesManager.Instance(p).getReader().GetNoteList())
            .Where(n => n != null && !n.type.isConnectSlide()).ToArray();
        var endMethod=AccessTools.Method(typeof(NoteJudge),"GetNoteCheckEnd");
        var startMethod=AccessTools.Method(typeof(NoteJudge),"GetNoteCheckStart");
        double margin=Enum.GetValues(typeof(NoteJudge.EJudgeType)).Cast<NoteJudge.EJudgeType>()
            .Where(t=>t!=NoteJudge.EJudgeType.End).Max(t=>Math.Max(Math.Abs(Convert.ToDouble(startMethod.Invoke(null,new object[]{t}))),
                Convert.ToDouble(endMethod.Invoke(null,new object[]{t}))));
        margin+=Enumerable.Range(0,2).Where(p=>Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry)
            .Max(p=>Math.Abs(Singleton<GamePlayManager>.Instance.GetGameScore(p).UserOption.GetJudgeTimingFrame())*16.666667)+17;
        var spans = notes.Where(n => ScorePlanAdapter.EndTime(n)>n.time.msec).Select(n => new LoopState.Span {
                Start = n.time.msec, End = ScorePlanAdapter.EndTime(n) }).ToArray();
        double end = notes.Length == 0 ? 0 : notes.Max(n => ScorePlanAdapter.EndTime(n)+margin);
        State.Configure(A.Value, B.Value, end, Count, spans);
        WindowA=Math.Max(0,State.A-margin);WindowB=State.B+margin;
        RankAutoPlay.Cancel(true); FastSkip.CancelPending();
        Session = true; Running = true;
        try { BeginRound(); }
        catch { Fault();PlaySession.SetPause(true);throw; }
    }
    private static void BeginRound()
    {
        _entered = _waiting = _intervalPaused = false;
        State.BeginRound();
        PlaySession.SetPause(true);
        PracticeAudio.PreparePracticeMusic(PreservePitch);
        ActiveSpeed = Speed; ActivePreservePitch = PreservePitch;
        PlaybackRate.Refresh();
        PlaySession.SetMovieSpeed(ActiveSpeed);
        PlaySession.Seek(Math.Max(0, WindowA - PreRoll * 1000), true);
        _lastReal = Time.realtimeSinceStartup;
        Running = true;
        PlaySession.SetPause(false);
        Status = "Pre-roll";
    }
    public static void Stop()
    {
        if (!Session) return;
        Running = false; PlaySession.SetPause(true); Status = "Stopped. Start again or restart the whole song.";
    }
    public static void Update()
    {
        double now = Time.realtimeSinceStartup, delta = Math.Max(0, now - _lastReal); _lastReal = now;
        if (!Session || !Running) return;
        if (_waiting)
        {
            TickWait(delta);
            return;
        }
        if (PlaySession.Paused) return;
        if (!_entered && PlaySession.Time >= WindowA) { PlaySession.ClearRound(); _entered = true; Status = "Practice"; }
        if (!_entered) Status=$"Pre-roll {Math.Max(0,WindowA-PlaySession.Time)/1000:0.0}s";
        if (PlaySession.Time < WindowB) return;
        State.Complete(Interval);
        PlaySession.SetPause(true);
        if (State.Finished) { Running = false; Status = "Completed. Last round retained; restart song for normal scoring."; }
        else
        {
            _waiting = true;
            // Keep chart clocks paused, while allowing the real-time interval to elapse.
            _lastReal = now;
            Status = $"Next round in {State.WaitRemaining:0.0}s";
        }
    }
    public static void TogglePause()
    {
        if (!Session || !Running) throw new InvalidOperationException("Start practice again or restart the whole song.");
        if (_waiting) { _intervalPaused = !_intervalPaused; _lastReal = Time.realtimeSinceStartup; }
        else PlaySession.TogglePause();
    }
    private static bool _intervalPaused;
    public static bool IsIntervalPaused => _intervalPaused;
    private static void TickWait(double delta)
    {
        if (!_waiting || !Running) return;
        if (State.Wait(delta, _intervalPaused)) BeginRound();
        else Status = $"Next round in {State.WaitRemaining:0.0}s" + (_intervalPaused ? " (paused)" : "");
    }
    public static bool IncludeJudge(int index, GameScoreList score)
    {
        if (!Session) return true;
        if (!Running || _waiting || PlaySession.Time<WindowA) return false;
        if (!_entered) { PlaySession.ClearRound(); _entered=true; Status="Practice"; }
        for(int p=0;p<2;p++)
        {
            if(!ReferenceEquals(score,Singleton<GamePlayManager>.Instance.GetGameScore(p))) continue;
            var notes=NotesManager.Instance(p).getReader().GetNoteList();
            return index>=0 && index<notes.Count && notes[index].time.msec>=State.A && notes[index].time.msec<State.B;
        }
        return false;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(GameScoreList), "SetResult")]
    private static bool Filter(GameScoreList __instance, int index) => IncludeJudge(index, __instance);
    internal static void Fault() { Running=false; Status="Practice paused after a reset error. Restart the whole song."; }
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Process.GameProcess), "SetRelease")]
    private static void PreventPracticeResult()
    {
        if (Session) Singleton<GamePlayManager>.Instance.SetQuickRetryFrag(true);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Monitor.GameMonitor), "IsAllJudged")]
    private static bool KeepPracticeOpen(ref bool __result)
    {
        if (!Session) return true;
        __result = false;
        return false;
    }
}
