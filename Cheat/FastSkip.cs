using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SinmaiAssist.Cheat;

internal class FastSkip
{
    public enum SkipMode { Default, Custom, Random }
    public static SkipMode Mode;
    public static bool SkipButton;
    public static bool Force1Miss;
    public static int CustomAchivement;
    public static bool CustomInputValid = true;
    public static string Status = "";
    public static bool IsBusy => _pending != null;
    private static readonly FieldInfo Sequence = AccessTools.Field(typeof(GameProcess), "_sequence");
    private static readonly FieldInfo Monitors = AccessTools.Field(typeof(GameProcess), "_monitors");
    private static readonly FieldInfo Messages = AccessTools.Field(typeof(GameProcess), "_message");
    private static readonly FieldInfo Container = AccessTools.Field(typeof(GameProcess), "container");
    private static readonly MethodInfo UpdateSub = AccessTools.Method(typeof(GameProcess), "UpdateSubbMonitorData");
    private static readonly MethodInfo Release = AccessTools.Method(typeof(GameProcess), "SetRelease");
    private static readonly MethodInfo Party = AccessTools.Method(typeof(GameProcess), "IsPartyPlay");
    private static Task<ScorePlanAdapter.Plan[]> _pending;
    private static CancellationTokenSource _cancellation;
    private static GameProcess _owner;
    private static bool _completed;

    [HarmonyPrepare]
    public static bool Prepare()
    {
        bool ready = Sequence != null && Monitors != null && Messages != null && Container != null &&
            UpdateSub != null && Release != null && Party != null && ScorePlanAdapter.IsCompatible;
        if (!ready) MelonLogger.Error("FastSkip disabled: required game scoring / process interface is missing.");
        return ready;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void OnStart(GameProcess __instance)
    {
        CancelPending(); _owner = __instance; _completed = false; Status = "";
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnRelease")]
    public static void OnRelease() { CancelPending(); _owner = null; }

    public static void CancelPending()
    {
        _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = null;
        _pending = null; SkipButton = false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    public static void Skip(GameProcess __instance)
    {
        if(BatchController.Active){SkipButton=false;return;}
        bool requested = SkipButton;
        SkipButton = false;
        if (PracticeLoop.Session || RankAutoPlay.IsBusy) { if (requested) Status = "Skip unavailable during practice / rank planning."; return; }
        if (requested) RankAutoPlay.Cancel(true);
        if (_owner != __instance) OnStart(__instance);
        int sequence = Convert.ToInt32(Sequence.GetValue(__instance));
        if (_completed || sequence < 4 || sequence >= 9 || GameManager.IsNoteCheckMode)
        {
            if (_pending != null) CancelPending();
            return;
        }
        try
        {
            if (_pending != null)
            {
                if (sequence != 4) { CancelPending(); Status = "Song ended before generation completed."; return; }
                if (!_pending.IsCompleted) return;
                var pending = _pending; _pending = null;
                _cancellation?.Dispose(); _cancellation = null;
                var plans = pending.GetAwaiter().GetResult();
                string error = plans.FirstOrDefault(p => !p.Result.Success)?.Result.Error;
                if (error != null) { Status = error; return; }
                if (!ScorePlanAdapter.CanGenerate((bool)Party.Invoke(__instance, null), out error)) { Status = error; return; }
                ScorePlanAdapter.Apply(plans);
                Status = string.Join(" | ", plans.Select(p => $"P{p.Player + 1}: {p.Result.Achievement:0.0000}%"));
                Complete(__instance);
                return;
            }
            if (!requested) return;
            if (Mode == SkipMode.Default) { Complete(__instance); return; }
            if (sequence != 4) { Status = "Score generation is only available during play."; return; }
            if (Mode == SkipMode.Custom && !CustomInputValid) { Status = "Enter an integer score from 0 to 101."; return; }
            if (!ScorePlanAdapter.CanGenerate((bool)Party.Invoke(__instance, null), out var reason)) { Status = reason; return; }
            var plansToGenerate = ScorePlanAdapter.Capture(Mode, CustomAchivement, Force1Miss);
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _pending = Task.Run(() =>
            {
                foreach (var plan in plansToGenerate) plan.Result = ScorePlanner.Generate(plan.Request, token);
                return plansToGenerate;
            });
            Status = "Generating judgements...";
        }
        catch (Exception ex)
        {
            CancelPending(); Status = "Skip failed; see MelonLoader log."; MelonLogger.Error(ex);
        }
    }

    private static void Complete(GameProcess process)
    {
        var game = Singleton<GamePlayManager>.Instance;
        var monitors = (GameMonitor[])Monitors.GetValue(process);
        var container = (ProcessDataContainer)Container.GetValue(process);
        var messages = (Message[])Messages.GetValue(process);
        _completed = true;
        for (int i = 0; i < monitors.Length; i++)
            if (Singleton<UserDataManager>.Instance.GetUserData(i).IsEntry)
                game.GetGameScore(i).FinishPlay(game.GetGameScore(i == 0 ? 1 : 0));
        for (int i = 0; i < monitors.Length; i++)
        {
            if (!Singleton<UserDataManager>.Instance.GetUserData(i).IsEntry) continue;
            game.SetSyncResult(i);
            UpdateSub.Invoke(process, new object[] { i });
            container.processManager.SendMessage(messages[i]);
        }
        Release.Invoke(process, null);
    }
    internal static void CompleteForBatch(GameProcess process)
    {
        if(process==null||!PlaySession.Playing)throw new InvalidOperationException("Batch game process changed before score commit.");
        Complete(process);
    }
}
