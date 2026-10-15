// Offline adapters only. Production policies, serialization, capture and dispatch are linked unchanged.
namespace UnityEngine
{
    public enum KeyCode { None=0, Escape=27, Space=32, A=97, B=98, F=102, G=103, H=104, J=106, K=107, N=110, O=111, P=112, Mouse0=323 }
    public static class Input
    {
        public static bool GetMouseButtonDown(int _) => false;
        public static bool GetKeyDown(KeyCode key)=>global::DebugInput.GetKeyDown(key);
        public static bool GetKey(KeyCode key)=>global::DebugInput.GetKey(key);
    }
}
internal static class DebugInput
{
    public static readonly HashSet<UnityEngine.KeyCode> Down=new(), Held=new();
    public static bool GetKeyDown(UnityEngine.KeyCode key)=>Down.Contains(key);
    public static bool GetKey(UnityEngine.KeyCode key)=>Held.Contains(key)||Down.Contains(key);
    public static void Clear() {Down.Clear();Held.Clear();}
}
namespace SinmaiAssist
{
    public static class SinmaiAssist
    {
        public static Config.MainConfig MainConfig=new();
        public static Config.KeyBindConfig KeyBindConfig=new();
    }
}
namespace SinmaiAssist.Cheat
{
    public static class BatchController
    {
        public static bool Active;
        public static Config.BatchSettings Settings=new();
        public static void Initialize(string _){}
        public static void RequireIdle(){if(Active)throw new InvalidOperationException("Batch active");}
    }
    public static class AutoPlay
    {
        public enum AutoPlayMode {None,Critical,Perfect,Great,Good,Random,RandomAllPerfect,RandomFullComboPlus,RandomFullCombo,Rank}
        public static AutoPlayMode autoPlayMode;
        public static bool DisableUpdate;
    }
    public static class FastSkip
    {
        public enum SkipMode {Default,Custom,Random}
        public static SkipMode Mode;
        public static int CustomAchivement;
        public static bool Force1Miss,CustomInputValid;
    }
    public static class RankAutoPlay
    {
        public static bool Requested,IsBusy;
        public static string TargetRank="SSS";
        public static MinimumCombo Minimum;
        public static void Request()=>Requested=true;
        public static void Cancel(bool clear) {if(clear)Requested=false;}
    }
    public static class PracticeLoop
    {
        public static bool Session;
        public static bool InputValid=true;
        public static int Count;
        public static double PreRoll=3,Interval=5;
        public static float Speed=1;
        public static bool PreservePitch=true;
    }
    public static class SpeedHack
    {
        public static void ApplySpeed(float value,bool _) => global::SinmaiAssist.SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier=value;
    }
}
namespace SinmaiAssist.GUI
{
    public static class BatchPanel { public static bool InputValid=true; public static void SyncInputs()=>InputValid=true; }
    public static class FastSkipPanel { public static void SyncInput() {} }
    public static class ChartControllerPanel { public static void SyncInputs() {Cheat.PracticeLoop.InputValid=true;} }
}
