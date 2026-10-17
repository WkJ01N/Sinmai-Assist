using System;
using SinmaiAssist.Cheat;

namespace SinmaiAssist.Runtime;

// User intent survives per-song plan disposal and native startup resets.
internal static class AutoPlaySelection
{
    public static string PreferredMode { get; private set; }
    public static void Select(string mode)
    {
        BatchController.RequireIdle();
        if(mode=="Rank")
        {
            try { RankAutoPlay.Request(); }
            catch(Exception ex) { RankAutoPlay.ReportBlocked(ex.Message); throw; }
            PreferredMode=mode;
            return;
        }
        var parsed=(AutoPlay.AutoPlayMode)Enum.Parse(typeof(AutoPlay.AutoPlayMode),mode);
        RankAutoPlay.Cancel(true);
        AutoPlay.autoPlayMode=parsed;PreferredMode=mode;
    }
    public static void RestoreAtSongStart()
    {
        if(!BatchController.Active&&PreferredMode!=null)Select(PreferredMode);
    }
    public static void Synchronize(int nativeMode)
    {
        if(BatchController.Active||AutoPlay.DisableUpdate||PreferredMode=="Rank"||RankAutoPlay.Requested||RankAutoPlay.Active||RankAutoPlay.IsBusy)return;
        if(Enum.IsDefined(typeof(AutoPlay.AutoPlayMode),nativeMode)&&nativeMode!=(int)AutoPlay.AutoPlayMode.Rank)
            AutoPlay.autoPlayMode=(AutoPlay.AutoPlayMode)nativeMode;
    }
}
