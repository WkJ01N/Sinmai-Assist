using System;
using System.Collections.Generic;
using System.Linq;
using SinmaiAssist.Config;
using YamlDotNet.Serialization;

namespace SinmaiAssist.Cheat;

public sealed class BatchGoal
{
    public string Rank { get; set; }
    public MinimumCombo Minimum { get; set; }
    public bool Theory { get; set; }
    public override string ToString()=>Theory?"AP+":Rank+" / "+(Minimum==MinimumCombo.None?"Any":Minimum==MinimumCombo.FCPlus?"FC+":Minimum.ToString());
}
public sealed class BatchChart
{
    public int[] Difficulty { get; set; }
    public string[] Levels { get; set; }
    public BatchGoal[] Goals { get; set; }
    public int Attempts { get; set; }
    public string Outcome { get; set; }
    public string Error { get; set; }
    public bool Saved { get; set; }
    public bool VerifyHistory { get; set; }
    [YamlIgnore] public bool Done => Outcome!=null;
}
public sealed class BatchSong
{
    public int MusicId { get; set; }
    public int ScoreType { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public List<BatchChart> Charts { get; set; } = new List<BatchChart>();
    [YamlIgnore] public bool Done => Charts.All(c=>c.Done);
    [YamlIgnore] public string Outcome => !Done?"Pending":Charts.Count==0||Charts.All(c=>c.Outcome=="Failed")?"Failed":
        Charts.Any(c=>c.Outcome=="Failed")?"Partial failure":Charts.All(c=>c.Outcome=="Already met")?"Already met":"Success";
}
public sealed class BatchJob
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string[] Users { get; set; }
    public BatchSettings Settings { get; set; }
    public List<BatchSong> Songs { get; set; } = new List<BatchSong>();
    public List<string> Events { get; set; } = new List<string>();
    public List<BatchUploadStep> Uploads { get; set; } = new List<BatchUploadStep>();
    public bool Finished { get; set; }
    [YamlIgnore] public int Processed => Songs.Count(s=>s.Done);
}
internal sealed class BatchEntry
{
    public int MusicId,ScoreType;
    public string Name,Category;
    public List<BatchChart> Charts=new List<BatchChart>();
}
internal static class BatchPolicy
{
    public static List<BatchSong> Queue(IReadOnlyList<List<BatchEntry>> categories,int category,int row,BatchSettings settings,int seed)
    {
        settings.Validate();
        if(category<0||category>=categories.Count||row<0||row>categories[category].Count)throw new ArgumentException("Invalid song cursor.");
        var songs=new List<BatchSong>();var seen=new Dictionary<string,BatchSong>();var random=new Random(seed);
        int last=settings.ByCategories?Math.Min(categories.Count,category+settings.CategoryCount):settings.CrossCategories?categories.Count:category+1;
        for(int c=category;c<last;c++)
        for(int r=c==category?row:0;r<categories[c].Count;r++)
        {
            var entry=categories[c][r];if(entry.MusicId<=3||entry.MusicId>=100000)continue;
            string key=entry.MusicId+":"+entry.ScoreType;
            if(!seen.TryGetValue(key,out var song))
            {
                if(!settings.ByCategories&&songs.Count>=settings.SongCount)return songs;
                song=new BatchSong{MusicId=entry.MusicId,ScoreType=entry.ScoreType,Name=entry.Name,Category=entry.Category};
                songs.Add(song);seen[key]=song;
            }
            foreach(var chart in entry.Charts)
            {
                if(song.Charts.Any(t=>t.Difficulty.SequenceEqual(chart.Difficulty)))continue;
                var frozen=new BatchChart{Difficulty=(int[])chart.Difficulty.Clone(),Levels=(string[])chart.Levels.Clone()};
                frozen.Goals=frozen.Levels.Select((level,p)=>frozen.Difficulty[p]<0?null:Draw(settings,level,random)).ToArray();
                song.Charts.Add(frozen);
            }
        }
        return songs;
    }
    public static BatchGoal Draw(BatchSettings settings,string level,Random random)
    {
        var baseline=new BatchGoal{Rank=settings.TargetRank,Minimum=settings.Minimum,Theory=settings.Minimum==MinimumCombo.APPlus};
        int ordinal=BatchSettings.LevelOrdinal(level);
        if(!settings.Boost||ordinal<0||ordinal>BatchSettings.LevelOrdinal(settings.BoostThrough)||baseline.Theory)return baseline;
        var profile=settings.Profiles.FirstOrDefault(p=>p.Level==level);
        if(profile==null)return baseline;
        double value=random.NextDouble()*profile.Outcomes.Sum(w=>w.Weight);
        var selected=profile.Outcomes.Last(w=>w.Weight>0);
        foreach(var weight in profile.Outcomes){value-=weight.Weight;if(value<0){selected=weight;break;}}
        string rank=selected.Rank=="Base"?settings.TargetRank:selected.Rank;
        if(Array.IndexOf(BatchSettings.Ranks,rank)<Array.IndexOf(BatchSettings.Ranks,settings.TargetRank))rank=settings.TargetRank;
        var combo=selected.UseBaseCombo?settings.Minimum:(MinimumCombo)Math.Max((int)settings.Minimum,(int)selected.Combo);
        bool theory=selected.Theory||combo==MinimumCombo.APPlus;
        return new BatchGoal{Rank=theory?"SSS+":rank,Minimum=theory?MinimumCombo.APPlus:combo,Theory=theory};
    }
}
