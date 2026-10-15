// Minimal native boundary doubles. These do not emulate rendering/audio or prove in-game behavior.
using System.Reflection;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.All,AllowMultiple=true)] public sealed class HarmonyPatch:Attribute {public HarmonyPatch(){} public HarmonyPatch(Type _,string __){}}
    public sealed class HarmonyPrefix:Attribute{} public sealed class HarmonyPostfix:Attribute{} public sealed class HarmonyFinalizer:Attribute{}
    public sealed class HarmonyTargetMethod:Attribute{} public sealed class HarmonyTargetMethods:Attribute{}
    public sealed class HarmonyPriority:Attribute {public HarmonyPriority(int _) {}}
    public static class Priority {public const int First=800;}
    public static class AccessTools
    {
        public static FieldInfo Field(Type type,string name) {for(;type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly);if(f!=null)return f;}return null;}
        public static MethodInfo Method(Type type,string name,Type[] parameters=null) {for(;type!=null;type=type.BaseType){var m=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).FirstOrDefault(m=>m.Name==name && (parameters==null || m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(parameters)));if(m!=null)return m;}return null;}
        public static PropertyInfo Property(Type type,string name) {for(;type!=null;type=type.BaseType){var p=type.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly);if(p!=null)return p;}return null;}
        public static Type TypeByName(string name)=>typeof(AccessTools).Assembly.GetType(name);
    }
}
namespace MAI2.Util {public static class Singleton<T> where T:new(){public static T Instance{get;}=new();}}
namespace MelonLoader {public static class MelonLogger{public static readonly List<string> Errors=new();public static void Error(object e)=>Errors.Add(e.ToString());public static void Warning(object e)=>Errors.Add(e.ToString());}}
namespace UnityEngine
{
    public static class Time{public static float realtimeSinceStartup;}
    public sealed class GameObject{public bool Active=true;public void SetActive(bool active)=>Active=active;}
    public class Component{public GameObject gameObject=new();}
}
public static class NoteJudge
{
    public enum ETiming{TooFast,FastGood,FastGreat3rd,FastGreat2nd,FastGreat,FastPerfect2nd,FastPerfect,Critical,LatePerfect,LatePerfect2nd,LateGreat,LateGreat2nd,LateGreat3rd,LateGood,TooLate,End}
    public enum EJudgeType{Tap,HoldOut,SlideOut,Touch,ExTap,Break,End}
    public static float GetNoteCheckStart(EJudgeType _)=>-150;
    public static float GetNoteCheckEnd(EJudgeType _)=>200;
}
namespace SinmaiAssist
{
    public static class SinmaiAssist {public static Configuration MainConfig=new();}
    public sealed class Configuration
    {
        public Cheats Cheat=new();public ModSettings ModSetting=new();public sealed class ModSettings{public bool SafeMode;}
        public sealed class Cheats{public bool AutoPlay=true;public SpeedSettings SpeedHack=new();}
        public sealed class SpeedSettings{public bool Enable;public float SpeedMultiplier=1;}
    }
}
namespace SinmaiAssist.Runtime
{
    internal static class Commands{public static string Status;}
    internal static class SettingsStore{public static int Restores;public static void RestoreModeAtSongStart()=>Restores++;}
}
namespace Manager
{
    public struct Timing {public float msec;}
    public sealed class NoteType {public bool Connect;public bool isConnectSlide()=>Connect;}
    public sealed class NoteData
    {
        public int indexNote;public Timing time,end;public NoteType type=new();public List<NoteData> child=new();
        public bool playAnsSoundHead,playAnsSoundTail,isUsed,isJudged;
    }
    public sealed class NoteDataList:List<NoteData>{}
    public sealed class Click{public Timing time;public bool Played;}
    public sealed class Composition{public List<Click> _clickSeList=new(){new(){time=new(){msec=0}},new(){time=new(){msec=9000}}};}
    public sealed class Reader{public NoteDataList Notes=new();public Composition Composition=new();public NoteDataList GetNoteList()=>Notes;public Composition GetCompositioin()=>Composition;}
    public sealed class NotesManager
    {
        private static readonly NotesManager[] Managers={new(),new()};private static float _curMSec,_msecStartGap;
        public static float Current{get=>_curMSec;set=>_curMSec=value;}public static bool IsPaused;public static int Starts;
        public Reader Reader=new();public static NotesManager Instance(int p)=>Managers[p];public Reader getReader()=>Reader;
        public static float GetCurrentMsec()=>Current;public static void StartPlay(float value){Current=_msecStartGap=value;IsPaused=false;Starts++;}
        public static void Pause(bool value)=>IsPaused=value;public static void UpdateTimer(){}
    }
    public struct JudgeResultSt {public bool Judged;public int Timing;}
    public sealed class Options{public float GetJudgeTimingFrame()=>0;}
    public sealed class GameScoreList
    {
        public JudgeResultSt[] Results=new JudgeResultSt[3];public decimal Achievement;public Options UserOption=new();public int Clears;
        public JudgeResultSt GetScoreAt(int i)=>Results[i];public decimal GetAchivement()=>Achievement;public void CalcUpdate(){}
        public void SetResult(int index,int kind,NoteJudge.ETiming timing){Results[index]=new(){Judged=true,Timing=(int)timing};}
    }
    public sealed class User{public bool IsEntry;}
    public sealed class UserDataManager{public User[] Users={new(){IsEntry=true},new()};public User GetUserData(int p)=>Users[p];}
    public sealed class GamePlayManager
    {
        public GameScoreList[] Scores={new(),new()};public bool QuickRetry;
        public GameScoreList GetGameScore(int p)=>Scores[p];public void SetQuickRetryFrag(bool value)=>QuickRetry=value;
    }
    public static class SoundManager
    {
        public enum PlayerID{Music=2,Bgm=3,Sound_Sepalate_Right=4}
        public static bool Paused;public static int SeekPosition;public static Action OnSeek;public static int Resumes;
        public static bool Ready=true;
        public static bool IsMusicPrepare()=>Ready;
        public static void StartMusic()=>PauseMusic(false);
        public static void PauseMusic(bool value){Paused=value;MAI2.Util.Singleton<SoundCtrl>.Instance.Music.Paused=value;if(!value)Resumes++;}
        public static void SeekMusic(int value)
        {
            OnSeek?.Invoke();SeekPosition=value;Paused=true;
            var player=MAI2.Util.Singleton<SoundCtrl>.Instance.Music;
            player.ResetParameters();player.Paused=true;player.StartTime=value;player.Start();
        }
    }
    public sealed class SoundCtrl
    {
        public sealed class PlayerObj{public CriAtomExPlayer Player=new();}
        private readonly Dictionary<int,PlayerObj> _players=new(){{2,new()},{3,new()},{4,new()}};
        public CriAtomExPlayer Music=>_players[2].Player;
        public void Stop(int playerID,bool forceStop=false)=>_players[playerID].Player.Stop();
        public long GetPlayTiming(int id)
        {
            long raw=_players[id].Player.RawTime;object[] args={raw,id};
            typeof(SinmaiAssist.Cheat.SpeedHackAudioClockPatch).GetMethod("GetPlayTimingPostfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
            return (long)args[0];
        }
    }
    public static class InputManager
    {
        public static bool InGameButtonDown(int monitor,int key)=>true;public static bool InGameTouchPanelAreaDown(int monitor,int key)=>true;
    }
}
namespace Monitor.Game
{
    public sealed class GameCtrl
    {
        private readonly List<object> _activeNoteList=new(){new()};private readonly List<object> _activeSlideList=new(){new()};
        private readonly List<UnityEngine.Component> _judgeGradeObjectList=new(){new()};
        public bool GradeVisible=>_judgeGradeObjectList[0].gameObject.Active;
        public int Collects,Clicks;public int ActiveCount=>_activeNoteList.Count+_activeSlideList.Count;
        public void ForceNoteCollect()=>Collects++;public void ResetClickFlag()=>Clicks++;public void UpdateNotes(){}
    }
}
namespace Monitor
{
    public class NoteBase{protected int NoteIndex;public int MonitorId{get;set;}public NoteBase(int p=0,int i=0){MonitorId=p;NoteIndex=i;}public virtual NoteJudge.ETiming GetJudgeResult()=>NoteJudge.ETiming.Critical;}
    public class SlideRoot{protected int NoteIndex;public int MonitorId{get;set;}public SlideRoot(int p=0,int i=0){MonitorId=p;NoteIndex=i;}public NoteJudge.ETiming GetJudgeResult()=>NoteJudge.ETiming.Critical;}
    public sealed class GameMonitor
    {
        private readonly Game.GameCtrl GameController=new();public Game.GameCtrl Controller=>GameController;public int Seeks;public double BarTime;
        public void Seek(int value){Seeks++;BarTime=Manager.NotesManager.Current;GameController.ForceNoteCollect();}
        public bool IsAllJudged()=>true;
    }
}
namespace Process
{
    public sealed class MoviePlayer{public float Speed=1;public void SetSpeed(float value)=>Speed=value;}
    public sealed class MovieMaterial{public MoviePlayer player{get;}=new();}
    public sealed class Movie
    {
        private readonly List<MovieMaterial> _moviePlayers=new(){new(),new()};
        public bool AllSpeed(float value)=>_moviePlayers.All(p=>p.player.Speed==value);
        public readonly bool[] Paused=new bool[2];public readonly float[] Positions=new float[2];
        public void Pause(int screen,bool value)=>Paused[screen]=value;public void SetSeekFrame(int screen,float value)=>Positions[screen]=value;
    }
    public sealed class GameProcess
    {
        private int _sequence=4;private readonly Monitor.GameMonitor[] _monitors={new(),new()};private readonly Movie _gameMovie=new();
        public Movie Movie=>_gameMovie;public Monitor.GameMonitor[] Monitors=>_monitors;public int Releases,ResultSaves,Retries;
        public void OnStart(){} public void OnUpdate(){}public void OnRelease(){}public void UpdateNotes(){}
        private bool IsPartyPlay()=>false;
        private void SetRelease(){Releases++;if(MAI2.Util.Singleton<Manager.GamePlayManager>.Instance.QuickRetry)Retries++;else ResultSaves++;_sequence=9;}
    }
}
namespace SinmaiAssist.Cheat
{
    public static class BatchController{public static bool Active;public static void RequireIdle(){if(Active)throw new InvalidOperationException("Batch active");}}
    public static class SpeedHack{public static float GetCurrentSpeed()=>SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier;}
    internal static class SpeedHackBgmPitchPatch
    {
        public static bool IsBgmPlayer(int id)=>id>=2&&id<=4;
        public static void UpdateActiveBgmPitch(float pitch)=>MAI2.Util.Singleton<Manager.SoundCtrl>.Instance.Music.SetPitch(pitch);
    }
    public static class AutoPlay {public enum AutoPlayMode{None,Critical,Rank}public static AutoPlayMode autoPlayMode;}
    public static class FastSkip{public enum SkipMode{Random}public static bool IsBusy;public static int Cancels;public static void CancelPending(){IsBusy=false;Cancels++;}}
    internal static class ScorePlanAdapter
    {
        public sealed class Plan {public int Player;public ScorePlanner.Request Request;public ScorePlanner.Result Result;public Manager.NoteDataList Notes;public Manager.GameScoreList Score;}
        public static bool IsCompatible=true,Allowed=true;
        public static Func<Plan[]> Source;
        public static bool CanGenerate(bool party,out string error){error=Allowed?null:"Unsupported mode";return Allowed;}
        public static Plan[] Capture(FastSkip.SkipMode mode,int score,bool miss,string target,MinimumCombo? minimum,bool locked)=>Source();
        public static float EndTime(Manager.NoteData n)=>Math.Max(n.time.msec,n.end.msec);
        public static void ClearRound(int p){var score=MAI2.Util.Singleton<Manager.GamePlayManager>.Instance.GetGameScore(p);score.Results=new Manager.JudgeResultSt[3];score.Achievement=0;score.Clears++;}
    }
}

// Native audio-boundary doubles; no real DSP or buffers are emulated.
public sealed class CriAtomExPlayer
{
    public uint Pool;public float Pitch,Stretch=1;public long StartTime,RawTime;public bool Paused,Stopped;
    public uint StartedPool;public float StartedPitch,StartedStretch;
    public void SetVoicePoolIdentifier(uint value)=>Pool=value;
    public void SetPitch(float value)=>Pitch=value;
    public void SetDspTimeStretchRatio(float value)=>Stretch=value;
    public void UpdateAll(){}
    public void ResetParameters(){Pool=0;Pitch=0;Stretch=1;RawTime=0;Stopped=false;}
    public void Start()
    {
        try{typeof(SinmaiAssist.Cheat.PracticeAudio).GetMethod("BeforeStart",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{this});}
        catch(TargetInvocationException e){throw e.InnerException;}
        StartedPool=Pool;StartedPitch=Pitch;StartedStretch=Stretch;
    }
    public void Stop(){Stopped=true;Paused=true;}
}
public sealed class CriAtomExStandardVoicePool:IDisposable
{
    public static bool FailAttach;public static int Created,Disposed;public bool Attached;
    public CriAtomExStandardVoicePool(int voices,int channels,int rate,bool streaming,uint id){Created++;}
    public void AttachDspTimeStretch(){if(FailAttach)throw new Exception("DSP unavailable");Attached=true;}
    public void Dispose(){Disposed++;}
}
