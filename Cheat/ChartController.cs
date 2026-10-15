using System;

namespace SinmaiAssist.Cheat;

public class ChartController
{
    public enum Button { None, Pause, Reset, TimeSkipAdd, TimeSkipAdd2, TimeSkipAdd3, TimeSkipSub, TimeSkipSub2, TimeSkipSub3, Set, Back }
    public static Button ButtonStatus;
    public static bool IsPlaying = true;
    public static double Timer;
    public static int RecordTime;
    internal static void Execute(Button button)
    {
        string error=PlaySession.RequirePlay();
        if(error!=null) throw new InvalidOperationException(error);
        if (button==Button.Reset) { PlaySession.Retry(); return; }
        if (RankAutoPlay.IsBusy) throw new InvalidOperationException("Wait for the rank plan to finish.");
        if (button==Button.Pause)
        {
            if (PracticeLoop.Session) PracticeLoop.TogglePause();
            else PlaySession.TogglePause();
            return;
        }
        if (PracticeLoop.Session) throw new InvalidOperationException("Manual seeking is disabled in practice; restart the song to exit.");
        if (button==Button.Set) { RecordTime=(int)PlaySession.Time; return; }
        double target=PlaySession.Time;
        switch(button)
        {
            case Button.TimeSkipAdd: target+=100; break;
            case Button.TimeSkipAdd2: target+=1000; break;
            case Button.TimeSkipAdd3: target+=2500; break;
            case Button.TimeSkipSub: target-=100; break;
            case Button.TimeSkipSub2: target-=1000; break;
            case Button.TimeSkipSub3: target-=2500; break;
            case Button.Back: target=RecordTime; break;
        }
        Seek(target);
    }
    internal static void Seek(double target)
    {
        string error = PlaySession.RequireSeek();
        if (error != null) throw new InvalidOperationException(error);
        RankAutoPlay.Cancel(true);
        PlaySession.Seek(target,true);
    }
    internal static void Nudge(int delta)
    {
        if(PlaySession.Paused) Seek(Math.Max(0,PlaySession.Time+delta));
    }
}
