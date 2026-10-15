using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;

namespace SinmaiAssist.Cheat;

// Shared game-thread timeline adapter. No worker is allowed to touch these objects.
internal static class PlaySession
{
    private static readonly FieldInfo Sequence = AccessTools.Field(typeof(GameProcess), "_sequence");
    private static readonly FieldInfo Monitors = AccessTools.Field(typeof(GameProcess), "_monitors");
    private static readonly FieldInfo Movie = AccessTools.Field(typeof(GameProcess), "_gameMovie");
    private static readonly FieldInfo MoviePlayers = Movie==null?null:AccessTools.Field(Movie.FieldType,"_moviePlayers");
    private static readonly PropertyInfo MoviePlayer = MoviePlayers?.FieldType.IsGenericType==true ?
        AccessTools.Property(MoviePlayers.FieldType.GetGenericArguments()[0],"player") : null;
    private static readonly MethodInfo MovieSpeed = MoviePlayer==null?null:AccessTools.Method(MoviePlayer.PropertyType,"SetSpeed",new[]{typeof(float)});
    private static readonly MethodInfo Party = AccessTools.Method(typeof(GameProcess), "IsPartyPlay");
    private static readonly MethodInfo Release = AccessTools.Method(typeof(GameProcess), "SetRelease");
    private static readonly MethodInfo MusicStart = AccessTools.Method(typeof(SoundManager), "StartMusic", System.Type.EmptyTypes);
    private static readonly MethodInfo MusicReady = AccessTools.Method(typeof(SoundManager), "IsMusicPrepare", System.Type.EmptyTypes);
    private static readonly FieldInfo Controller = AccessTools.Field(typeof(GameMonitor), "GameController");
    private static readonly FieldInfo[] GradePools=Controller?.FieldType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
        .Where(f=>f.Name.StartsWith("_judgeGrade",StringComparison.Ordinal)).ToArray() ?? Array.Empty<FieldInfo>();
    internal static GameProcess Owner { get; private set; }
    public static bool Paused { get; private set; }
    public static bool Installed;
    // Native startup advances the chart clock before starting audio. Keep that
    // song-local origin in chart milliseconds, including the speed at startup.
    internal static double? MusicOrigin { get; private set; }
    private static bool _musicWaiting;
    private static bool _musicPreparing, _resumeAfterPrepare;
    private static double _prepareStarted;
    public static bool PreparingMusic => _musicPreparing;
    private static readonly Dictionary<(System.Type,string),MethodInfo> MovieMethods=new Dictionary<(System.Type,string),MethodInfo>();
    public static bool Compatible => Sequence != null && Monitors != null && Movie != null && Party != null && Release != null && Controller != null &&
        MusicStart != null && MusicStart.IsStatic && MusicStart.ReturnType == typeof(void) &&
        MusicReady != null && MusicReady.IsStatic && MusicReady.ReturnType==typeof(bool);
    public static bool Playing => Owner != null && Compatible && Convert.ToInt32(Sequence.GetValue(Owner)) == 4;
    public static double Time => NotesManager.GetCurrentMsec();
    public static string RequirePlay() => !Installed || !Compatible ? "Game timeline interfaces unavailable." : !Playing ? "Only available during a song." : null;
    public static string RequireSeek() => RequirePlay() ??
        (MusicStart == null || !MusicStart.IsStatic || MusicStart.ReturnType != typeof(void)
            ? "Music startup interface unavailable; seeking disabled."
            : !MusicOrigin.HasValue ? "Music startup time unavailable; restart the song before seeking." : null);
    public static bool IsParty => Owner != null && (bool)Party.Invoke(Owner, null);
    public static bool CanPractice => Compatible && Controller.FieldType.GetMethod("ForceNoteCollect") != null &&
        Controller.FieldType.GetMethod("ResetClickFlag") != null && ScorePlanAdapter.IsCompatible &&
        AccessTools.Method(typeof(NoteJudge),"GetNoteCheckEnd") != null &&
        AccessTools.Method(typeof(NoteJudge),"GetNoteCheckStart") != null &&
        AccessTools.Field(Controller.FieldType,"_activeNoteList") != null && AccessTools.Field(Controller.FieldType,"_activeSlideList") != null &&
        FindMovieMethod(Movie.FieldType,"Pause")!=null && FindMovieMethod(Movie.FieldType,"SetSeekFrame")!=null &&
        MovieSpeed!=null && SpeedHackNotesClockPatch.Compatible;

    public static void SetMovieSpeed(float speed)
    {
        if(MovieSpeed==null)throw new InvalidOperationException("Practice movie speed interface unavailable.");
        if(!(MoviePlayers.GetValue(Movie.GetValue(Owner)) is IEnumerable players))throw new InvalidOperationException("Movie players not ready.");
        foreach(var item in players)
        {
            var player=MoviePlayer.GetValue(item);
            if(player!=null)MovieSpeed.Invoke(player,new object[]{speed});
        }
    }

    public static void SetPause(bool pause)
    {
        if (Owner == null) return;
        _resumeAfterPrepare = false;
        if (!pause && _musicWaiting && MusicOrigin.HasValue && Time >= MusicOrigin.Value) PrepareMusicPosition(Time);
        if (!pause && _musicPreparing)
        {
            if (SoundManager.IsMusicPrepare()) _musicPreparing=false;
            else { _resumeAfterPrepare=true;pause=true; }
        }
        var movie=Movie.GetValue(Owner);
        if(movie==null || FindMovieMethod(movie.GetType(),"Pause")==null) throw new InvalidOperationException("Game movie pause interface unavailable.");
        if(!Paused && Playing)NotesManager.UpdateTimer();
        SpeedHackAudioClockPatch.Flush();
        NotesManager.Pause(pause);
        Paused = pause;
        ChartController.IsPlaying = !pause;
        SoundManager.PauseMusic(pause || _musicWaiting);
        MovieCall("Pause", pause);
    }
    public static void TogglePause() => SetPause(_musicPreparing ? _resumeAfterPrepare : !Paused);
    [HarmonyPrefix, HarmonyPatch(typeof(SoundManager), "StartMusic")]
    private static void CaptureMusicOrigin()
    {
        if (!Playing || MusicOrigin.HasValue) return;
        NotesManager.UpdateTimer();
        double time = Time;
        if (!double.IsNaN(time) && !double.IsInfinity(time) && time >= 0) MusicOrigin = time;
    }
    private static void ResumeMusicIfReady()
    {
        if (!Playing || Paused || !_musicWaiting || !MusicOrigin.HasValue || Time < MusicOrigin.Value) return;
        // The threshold can be crossed between frames. Seek the residual instead
        // of starting audio at zero a frame behind the chart.
        SetPause(true);
        PrepareMusicPosition(Time);
        SetPause(false);
    }
    internal static int MusicSeekPosition(double chartTime) => (int)Math.Round(Math.Max(0,chartTime-MusicOrigin.Value),MidpointRounding.AwayFromZero);
    private static void PrepareMusicPosition(double chartTime)
    {
        _musicWaiting=chartTime<MusicOrigin.Value;
        int position=MusicSeekPosition(chartTime);
        SoundManager.SeekMusic(position);
        SoundManager.PauseMusic(true);
        SpeedHackAudioClockPatch.Reset((int)SoundManager.PlayerID.Music,position);
        _musicPreparing=true;_prepareStarted=UnityEngine.Time.realtimeSinceStartup;
    }
    private static void UpdateMusicPreparation()
    {
        if(!_musicPreparing)return;
        if(SoundManager.IsMusicPrepare())
        {
            _musicPreparing=false;
            if(_resumeAfterPrepare)SetPause(false);
        }
        else if(UnityEngine.Time.realtimeSinceStartup-_prepareStarted>5)
        {
            _musicPreparing=_resumeAfterPrepare=false;
            throw new InvalidOperationException("Music preparation timed out. Restart practice, disable Preserve pitch or restart the song.");
        }
    }
    private static MethodInfo FindMovieMethod(System.Type type,string name)
    {
        var key=(type,name);
        if(MovieMethods.TryGetValue(key,out var found)) return found;
        found=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m=>m.Name==name)
            .OrderBy(m=>m.GetParameters().Length).FirstOrDefault(m=> {
                var p=m.GetParameters();
                if(p.Length!=1 && p.Length!=2) return false;
                if(p.Length==2 && p[0].ParameterType!=typeof(int)) return false;
                var last=p[p.Length-1].ParameterType;
                return name=="Pause" ? last==typeof(bool) : last==typeof(double) || last==typeof(float);
            });
        MovieMethods[key]=found;return found;
    }
    private static void MovieCall(string name, object argument)
    {
        var movie = Movie.GetValue(Owner);
        if (movie == null) throw new InvalidOperationException("Game movie not initialized.");
        var method = FindMovieMethod(movie.GetType(), name);
        if (method == null) throw new MissingMethodException(movie.GetType().FullName, name);
        var parameters=method.GetParameters();
        argument=Convert.ChangeType(argument,parameters[parameters.Length-1].ParameterType,System.Globalization.CultureInfo.InvariantCulture);
        if (parameters.Length == 1) method.Invoke(movie, new[] { argument });
        else if (method.GetParameters().Length == 2)
        { method.Invoke(movie, new[] { (object)0, argument }); method.Invoke(movie, new[] { (object)1, argument }); }
        else throw new NotSupportedException("Unsupported movie timeline interface.");
    }
    public static void Seek(double milliseconds, bool resetScore)
    {
        string error = RequireSeek();
        if (error != null) throw new InvalidOperationException(error);
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        bool wasPaused = Paused;
        SetPause(true);
        milliseconds = Math.Max(0, milliseconds);
        NotesManager.StartPlay((float)milliseconds);
        NotesManager.Pause(true);
        NotesManager.UpdateTimer();
        // Seek invokes the game's pooled-object, slide-arrow and Touch-chain reset.
        var monitors=(GameMonitor[])Monitors.GetValue(Owner);
        for(int p=0;p<monitors.Length;p++)
        {
            if(!Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry) continue;
            var monitor=monitors[p];
            monitor.Seek((int)milliseconds);
            var controller = Controller.GetValue(monitor);
            if (controller != null) AccessTools.Method(controller.GetType(), "ResetClickFlag")?.Invoke(controller, null);
            foreach(var name in new[]{"_activeNoteList","_activeSlideList"})
                (AccessTools.Field(controller.GetType(),name)?.GetValue(controller) as IList)?.Clear();
            foreach(var pool in GradePools)
                if(pool.GetValue(controller) is IEnumerable effects)
                    foreach(var effect in effects)
                        if(effect is UnityEngine.Component component) component.gameObject.SetActive(false);
            foreach(var note in NotesManager.Instance(p).getReader().GetNoteList())
            {
                if(note==null) continue;
                note.playAnsSoundHead=note.time.msec<milliseconds;
                note.playAnsSoundTail=note.end.msec<milliseconds;
            }
            foreach(var click in NotesManager.Instance(p).getReader().GetCompositioin()._clickSeList) click.Played=click.time.msec<milliseconds;
        }
        if (resetScore) ClearRound();
        MovieCall("SetSeekFrame", milliseconds);
        PrepareMusicPosition(milliseconds);
        Owner.UpdateNotes();
        SetPause(wasPaused);
    }
    public static void ClearRound()
    {
        // Preserve score/note identities: Initialize reloads the chart and invalidates live objects.
        for (int p = 0; p < 2; p++)
        {
            if (!Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry) continue;
            ScorePlanAdapter.ClearRound(p);
        }
    }
    public static void Retry()
    {
        if (Owner == null) return;
        RankAutoPlay.Cancel(false);
        FastSkip.CancelPending();
        if(PracticeLoop.Session)
        {
            SetPause(true);
            PracticeAudio.Release();
        }
        else SetPause(false);
        Singleton<GamePlayManager>.Instance.SetQuickRetryFrag(true);
        Release.Invoke(Owner, null);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnStart")]
    private static void Start(GameProcess __instance)
    {
        Owner = __instance; Paused = false;
        MusicOrigin = null; _musicWaiting = false;
        _musicPreparing=_resumeAfterPrepare=false;
        SpeedHackAudioClockPatch.Clear();
        ChartController.RecordTime = 0;
        PracticeLoop.ResetSong();
        RankAutoPlay.ResetSong();
        Runtime.SettingsStore.RestoreModeAtSongStart();
    }
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    private static bool Update(GameProcess __instance)
    {
        if (Owner != __instance) return true;
        try
        {
            UpdateMusicPreparation();
            RankAutoPlay.Update();
            if (PracticeLoop.Session)
            {
                if (!Playing) { Retry(); return false; }
                PracticeLoop.Update();
            }
            return !Paused && !RankAutoPlay.IsBusy;
        }
        catch (Exception ex)
        {
            MelonLogger.Error(ex);
            Runtime.Commands.Status = "Timeline failed; restart the song to recover.";
            if (PracticeLoop.Session || RankAutoPlay.IsBusy) { PracticeLoop.Fault(); SetPause(true); return false; }
            return !Paused;
        }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    private static void AfterUpdate(GameProcess __instance)
    {
        if(Owner!=__instance) return;
        ChartController.Timer = Time;
        try
        {
            if (PracticeLoop.Session && Playing) PracticeLoop.Update();
            ResumeMusicIfReady();
            if (Owner != null && Compatible && Convert.ToInt32(Sequence.GetValue(Owner)) >= 5 && !PracticeLoop.Session) RankAutoPlay.Verify();
        }
        catch(Exception ex) { MelonLogger.Error(ex); if(PracticeLoop.Session) { PracticeLoop.Fault();SetPause(true); } }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(GameProcess), "OnRelease")]
    private static void End(GameProcess __instance)
    {
        if (Owner != __instance) return;
        RankAutoPlay.Cancel(false);
        PracticeAudio.Release();
        Owner = null; Paused = false;
        MusicOrigin = null; _musicWaiting = false;
        _musicPreparing=_resumeAfterPrepare=false;
        PracticeLoop.ResetSong();
    }
}
