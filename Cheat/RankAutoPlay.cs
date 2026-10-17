using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor;
using Type = System.Type;

namespace SinmaiAssist.Cheat;

internal static class RankAutoPlay
{
    public static string TargetRank = "SSS";
    public static MinimumCombo Minimum;
    public static bool Requested { get; private set; }
    public static bool Active => _active != null;
    public static bool IsBusy => _pending != null;
    public static string Status = "";
    public static string Stage { get; private set; } = "Idle";
    public static string LastFailure { get; private set; } = "";
    private static void State(string stage,string message)
    {Stage=stage;Status=message;MelonLogger.Msg("Rank control ["+stage+"]: "+message);}
    public static void ReportBlocked(string message)
    {LastFailure="Could not enable rank control: "+message;State(Active?"Active":"Failed",LastFailure);Runtime.Commands.Status=Status;}
    public static bool Installed;
    public static bool Compatible => ScorePlanAdapter.IsCompatible &&
        AccessTools.Field(typeof(NoteBase),"NoteIndex")?.FieldType==typeof(int) &&
        AccessTools.Field(typeof(SlideRoot),"NoteIndex")?.FieldType==typeof(int) &&
        AccessTools.Property(typeof(NoteBase),"MonitorId")?.PropertyType==typeof(int) &&
        AccessTools.Property(typeof(SlideRoot),"MonitorId")?.PropertyType==typeof(int) &&
        AccessTools.Method(typeof(GameScoreList),"GetScoreAt")?.ReturnType==typeof(JudgeResultSt) && typeof(JudgeResultSt).IsValueType;
    private static bool _needsPlan, _wasPaused, _verified;
    private static Task<ScorePlanAdapter.Plan[]> _pending;
    private static CancellationTokenSource _cancel;
    private static ScorePlanAdapter.Plan[] _active;
    private static readonly Dictionary<int, Dictionary<int, int>> Timings = new Dictionary<int, Dictionary<int, int>>();
    private static readonly Dictionary<Type, (FieldInfo index, PropertyInfo player)> NoteAccess = new Dictionary<Type, (FieldInfo, PropertyInfo)>();
    private static Process.GameProcess _owner;
    private static string _pendingTarget;
    public static string ActiveTarget { get; private set; }
    public static void Request()
    {
        BatchController.RequireIdle();
        if (PracticeLoop.Session) throw new InvalidOperationException("Restart song to leave practice before rank control.");
        if (IsBusy) throw new InvalidOperationException("Rank plan is already being generated.");
        Requested = true; _needsPlan = true;
        AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Rank;
        State("Armed",PlaySession.Playing ? "Preparing rank control..." : "Rank control armed for the next song.");
    }
    public static void ResetSong()
    {
        bool armed = Requested || Runtime.AutoPlaySelection.PreferredMode=="Rank";
        Cancel(false); Requested = armed; _needsPlan=armed;
        if(armed)
        {
            AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Rank;
            if(Stage!="Armed")State("Armed","Rank control armed for the next song.");
        }
    }
    public static void Cancel(bool clearRequest)
    {
        if (_pending != null)
        {
            _cancel?.Cancel();
            // Observe exceptions from superseded workers without touching Unity on them.
            _pending.ContinueWith(t => { var ignored=t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _pending=null; _cancel?.Dispose(); _cancel=null;
            if (_owner == PlaySession.Owner && PlaySession.Playing) PlaySession.SetPause(_wasPaused);
        }
        _active=null; Timings.Clear(); _needsPlan=false; _verified=false;
        if (clearRequest) Requested=false;
        if (AutoPlay.autoPlayMode == AutoPlay.AutoPlayMode.Rank && !Requested) AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.None;
        ActiveTarget=null;
        State(Requested?"Armed":"Idle",Requested?"Rank control armed for the next song.":"Rank control stopped for this song.");
    }
    public static void Update()
    {
        if (IsBusy)
        {
            if (_owner != PlaySession.Owner || !PlaySession.Playing) { Cancel(false); return; }
            if (!_pending.IsCompleted) return;
            var task=_pending; _pending=null;
            _cancel.Dispose(); _cancel=null;
            try
            {
                var plans=task.GetAwaiter().GetResult();
                string error=plans.FirstOrDefault(p=>!p.Result.Success)?.Result.Error;
                if (error != null) throw new InvalidOperationException(error);
                if (!ScorePlanAdapter.CanGenerate(PlaySession.IsParty,out var reason)) throw new InvalidOperationException(reason);
                var entered=Enumerable.Range(0,2).Where(p=>MAI2.Util.Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry);
                if(!entered.SequenceEqual(plans.Select(p=>p.Player))) throw new InvalidOperationException("Entered players changed during planning.");
                foreach (var p in plans)
                {
                    if (!ReferenceEquals(p.Notes, NotesManager.Instance(p.Player).getReader().GetNoteList()) ||
                        !ReferenceEquals(p.Score, MAI2.Util.Singleton<GamePlayManager>.Instance.GetGameScore(p.Player)))
                        throw new InvalidOperationException("Song snapshot changed during planning.");
                    foreach (var n in p.Request.Notes)
                        if (p.Score.GetScoreAt(n.Index).Judged!=n.FixedTiming.HasValue ||
                            (n.FixedTiming.HasValue && p.Score.GetScoreAt(n.Index).Timing != n.FixedTiming.Value))
                            throw new InvalidOperationException("Existing judgements changed during planning.");
                }
                var next=new Dictionary<int, Dictionary<int,int>>();
                foreach (var p in plans)
                {
                    var map=new Dictionary<int,int>();
                    for (int i=0;i<p.Request.Notes.Length;i++)
                    {
                        var n=p.Request.Notes[i]; map[n.Index]=p.Result.Timings[i];
                        AddConnections(p.Notes[n.Index], p.Result.Timings[i], map);
                    }
                    next[p.Player]=map;
                }
                Timings.Clear(); foreach(var item in next) Timings[item.Key]=item.Value;
                _active=plans; _verified=false; AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Rank;
                ActiveTarget=_pendingTarget;
                LastFailure="";
                State("Active","Active " + ActiveTarget + ": " + string.Join(" | ", plans.Select(p=>$"P{p.Player+1} {p.Result.Achievement:0.0000}%")));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally { PlaySession.SetPause(_wasPaused); }
        }
        if (!_needsPlan || !PlaySession.Playing) return;
        _needsPlan=false;
        bool froze=false;
        try
        {
            if (!Installed || !Compatible || !PlaySession.Installed || !SinmaiAssist.MainConfig.Cheat.AutoPlay)
                throw new InvalidOperationException("AutoPlay module or required interfaces unavailable.");
            if (FastSkip.IsBusy || PracticeLoop.Session) throw new InvalidOperationException("Another score operation is active.");
            if (!ScorePlanAdapter.CanGenerate(PlaySession.IsParty, out var reason)) throw new InvalidOperationException(reason);
            _wasPaused=PlaySession.Paused; _owner=PlaySession.Owner;
            PlaySession.SetPause(true);
            froze=true;
            var plans=ScorePlanAdapter.Capture(FastSkip.SkipMode.Random,0,false,TargetRank,Minimum,true);
            if (plans.All(p=>p.Request.Notes.All(n=>n.FixedTiming.HasValue))) throw new InvalidOperationException("Song is already fully judged.");
            _pendingTarget=TargetRank + " / " + Minimum;
            _cancel=new CancellationTokenSource(); var token=_cancel.Token;
            _pending=Task.Run(()=> { foreach(var p in plans) p.Result=ScorePlanner.Generate(p.Request,token); return plans; });
            State("Planning","Generating remaining-note plan...");
        }
        catch (Exception ex)
        {
            Fail(ex);
            if (froze && _owner==PlaySession.Owner && PlaySession.Playing) PlaySession.SetPause(_wasPaused);
        }
    }
    private static void Fail(Exception ex)
    {
        // Retain selection and any old valid plan, but never resume the previous
        // ordinary AutoPlay mode when a new rank plan cannot be produced.
        Requested=true;AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Rank;
        LastFailure=(Active?"Previous rank plan retained: ":"Rank planning failed; manual control: ")+ex.Message;
        State(Active?"Active":"Failed",LastFailure);
        Runtime.Commands.Status=Status;
    }
    private static void AddConnections(NoteData note,int timing,Dictionary<int,int> map)
    {
        foreach(var child in note.child.Where(n=>n.type.isConnectSlide()))
        { map[child.indexNote]=timing; AddConnections(child,timing,map); }
    }
    public static bool TryTiming(object note, out NoteJudge.ETiming timing)
    {
        timing=NoteJudge.ETiming.End;
        if (!Active || note==null) return false;
        var type=note.GetType();
        if (!NoteAccess.TryGetValue(type,out var access))
        { access=(AccessTools.Field(type,"NoteIndex"), AccessTools.Property(type,"MonitorId")); NoteAccess[type]=access; }
        if (access.index==null || access.player==null) return false;
        int index=(int)access.index.GetValue(note), player=(int)access.player.GetValue(note);
        if (!Timings.TryGetValue(player,out var map) || !map.TryGetValue(index,out int result)) return false;
        timing=(NoteJudge.ETiming)result; return true;
    }
    public static void Verify()
    {
        if (!Active || _verified) return;
        _verified=true;
        foreach(var p in _active)
        {
            p.Score.CalcUpdate();
            bool same=p.Request.Notes.Select((n,i)=>p.Score.GetScoreAt(n.Index).Judged && p.Score.GetScoreAt(n.Index).Timing==p.Result.Timings[i]).All(v=>v);
            decimal actual=p.Score.GetAchivement();
            if (!same || Math.Abs(actual-p.Result.Achievement)>0.000000000001m || actual<p.Request.Lower ||
                (p.Request.IncludeUpper ? actual>p.Request.Upper : actual>=p.Request.Upper))
            {
                Status=$"P{p.Player+1}: actual result differs from rank plan; see log.";
                LastFailure=Status;
                MelonLogger.Error(Status); return;
            }
        }
        Status="Rank plan verified: " + ActiveTarget;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(GameScoreList),"SetResult")]
    private static void ScoreTiming(GameScoreList __instance,int index,ref NoteJudge.ETiming timing)
    {
        if (!Active) return;
        var p=_active.FirstOrDefault(p=>ReferenceEquals(p.Score,__instance));
        if(p!=null && Timings[p.Player].TryGetValue(index,out int result)) timing=(NoteJudge.ETiming)result;
    }
}

// Override native hold/EX/Touch conversions as well as the ordinary Tap getter.
[HarmonyPatch]
internal static class RankJudgeResultPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets() => typeof(NoteBase).Assembly.GetTypes()
        .Where(t=>typeof(NoteBase).IsAssignableFrom(t) || typeof(SlideRoot).IsAssignableFrom(t)).SelectMany(t=>t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
        .Where(m=>m.Name=="GetJudgeResult" && m.ReturnType==typeof(NoteJudge.ETiming) && m.GetParameters().Length==0);
    [HarmonyPostfix]
    private static void Result(object __instance,ref NoteJudge.ETiming __result)
    {
        if (__result!=NoteJudge.ETiming.End && RankAutoPlay.TryTiming(__instance,out var timing)) __result=timing;
    }
}

[HarmonyPatch]
internal static class RankInputPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets() => typeof(InputManager).GetMethods(BindingFlags.Public|BindingFlags.Static)
        .Where(m=>m.ReturnType==typeof(bool) && (m.Name.StartsWith("InGameButton",StringComparison.Ordinal) || m.Name.StartsWith("InGameTouchPanel",StringComparison.Ordinal)));
    [HarmonyPrefix]
    private static bool IgnoreManual(ref bool __result)
    {
        if (!RankAutoPlay.Active || !RankNoteInputScope.InNoteUpdate) return true;
        __result=false; return false;
    }
}

[HarmonyPatch]
internal static class RankNoteInputScope
{
    [ThreadStatic] private static int _depth;
    public static bool InNoteUpdate => _depth>0;
    [HarmonyTargetMethod]
    private static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("Monitor.Game.GameCtrl"),"UpdateNotes");
    [HarmonyPrefix]
    private static void Enter(out bool __state)
    {
        __state=RankAutoPlay.Active;
        if(__state) _depth++;
    }
    [HarmonyFinalizer]
    private static Exception Leave(bool __state,Exception __exception)
    {
        if(__state) _depth--;
        return __exception;
    }
}
