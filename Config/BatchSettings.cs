using System;
using System.Collections.Generic;
using System.Linq;
using SinmaiAssist.Cheat;

namespace SinmaiAssist.Config;

public sealed class BatchWeight
{
    public string Rank { get; set; } = "Base";
    public MinimumCombo Combo { get; set; }
    public bool UseBaseCombo { get; set; } = true;
    public bool Theory { get; set; }
    public double Weight { get; set; } = 1;
    public BatchWeight Copy() => (BatchWeight)MemberwiseClone();
}
public sealed class BatchLevelProfile
{
    public string Level { get; set; } = "12+";
    public List<BatchWeight> Outcomes { get; set; } = new List<BatchWeight>();
    public BatchLevelProfile Copy() => new BatchLevelProfile {Level=Level,Outcomes=Outcomes.Select(w=>w.Copy()).ToList()};
}
public sealed class BatchSettings
{
    public bool AllDifficulties { get; set; }
    public bool ByCategories { get; set; }
    public int SongCount { get; set; } = 10;
    public int CategoryCount { get; set; } = 1;
    public bool CrossCategories { get; set; }
    public string TargetRank { get; set; } = "SSS";
    public MinimumCombo Minimum { get; set; }
    public bool SkipExisting { get; set; } = true;
    public bool Boost { get; set; }
    public string BoostThrough { get; set; } = "12+";
    public List<BatchLevelProfile> Profiles { get; set; } = CreateProfiles();
    public BatchSettings Copy()
    { var copy=(BatchSettings)MemberwiseClone();copy.Profiles=Profiles.Select(p=>p.Copy()).ToList();return copy; }
    public bool Same(BatchSettings other)
    {
        if(other==null||AllDifficulties!=other.AllDifficulties||ByCategories!=other.ByCategories||SongCount!=other.SongCount||CategoryCount!=other.CategoryCount||
            CrossCategories!=other.CrossCategories||TargetRank!=other.TargetRank||Minimum!=other.Minimum||SkipExisting!=other.SkipExisting||Boost!=other.Boost||BoostThrough!=other.BoostThrough||Profiles.Count!=other.Profiles.Count)return false;
        return Profiles.Zip(other.Profiles,(a,b)=>a.Level==b.Level&&a.Outcomes.Count==b.Outcomes.Count&&a.Outcomes.Zip(b.Outcomes,(x,y)=>
            x.Rank==y.Rank&&x.Combo==y.Combo&&x.UseBaseCombo==y.UseBaseCombo&&x.Theory==y.Theory&&x.Weight==y.Weight).All(v=>v)).All(v=>v);
    }
    public static readonly string[] Ranks={"D","C","B","BB","BBB","A","AA","AAA","S","S+","SS","SS+","SSS","SSS+"};
    public static int LevelOrdinal(string text)
    {
        if(string.IsNullOrEmpty(text))return -1;
        bool plus=text.EndsWith("+",StringComparison.Ordinal);
        return int.TryParse(plus?text.Substring(0,text.Length-1):text,out int value) && value>=1 && value<=15 ? value*2+(plus?1:0) : -1;
    }
    public static string LevelName(int ordinal) => (ordinal/2).ToString()+(ordinal%2==1?"+":"");
    public static List<BatchLevelProfile> CreateProfiles()
    {
        var profiles=new List<BatchLevelProfile>();
        for(int level=2;level<=31;level++)
        {
            int d=Math.Max(0,25-level);double ap=Math.Min(80,20+5*d);
            double basic=Math.Max(0,60-10*d),rank=5+2*d,fc=5+3*d,perfect=10+5*d;
            double scale=(100-ap)/(basic+rank+fc+perfect);
            profiles.Add(new BatchLevelProfile {Level=LevelName(level),Outcomes=new List<BatchWeight>{
                new BatchWeight{Weight=basic*scale},
                new BatchWeight{Rank="SSS+",Weight=rank*scale},
                new BatchWeight{Rank="SSS+",Combo=MinimumCombo.FCPlus,UseBaseCombo=false,Weight=fc*scale},
                new BatchWeight{Rank="SSS+",Combo=MinimumCombo.AP,UseBaseCombo=false,Weight=perfect*scale},
                new BatchWeight{Theory=true,Weight=ap}}});
        }
        return profiles;
    }
    public void Validate()
    {
        if(SongCount<=0 || SongCount>1000000 || CategoryCount<=0 || CategoryCount>1000000 || !Ranks.Contains(TargetRank) || !Enum.IsDefined(typeof(MinimumCombo),Minimum) || LevelOrdinal(BoostThrough)<0)
            throw new ArgumentException("Invalid batch count, target or level threshold.");
        if(Minimum==MinimumCombo.APPlus && TargetRank!="SSS+")throw new ArgumentException("AP+ requires a theoretical score; choose SSS+ instead of a lower base rank.");
        if(Profiles==null || Profiles.Any(p=>p==null) || Profiles.Select(p=>p.Level).Distinct().Count()!=Profiles.Count)
            throw new ArgumentException("Invalid batch probability profiles.");
        foreach(var profile in Profiles)
        {
            if(LevelOrdinal(profile.Level)<0 || profile.Outcomes==null || profile.Outcomes.Count==0 || profile.Outcomes.Any(w=>w==null ||
                double.IsNaN(w.Weight)||double.IsInfinity(w.Weight)||w.Weight<0 || w.Weight>1000000 ||
                !Enum.IsDefined(typeof(MinimumCombo),w.Combo) || (w.Rank!="Base"&&!Ranks.Contains(w.Rank))) || profile.Outcomes.Sum(w=>w.Weight)<=0)
                throw new ArgumentException("Invalid probability weights; use non-negative finite weights and a positive total.");
        }
    }
}
