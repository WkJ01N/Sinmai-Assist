using System.Diagnostics;
using System.Text.RegularExpressions;
using SinmaiAssist.Cheat;

internal static class Program
{
    private static int _checks;
    private static readonly uint[,] Scores = new uint[6, 15];
    private static readonly decimal[] Bounds = { 0, 50, 60, 70, 75, 80, 90, 94, 97, 98, 99, 99.5m, 100, 100.5m, 101 };

    private static void Main(string[] args)
    {
        ConfigChecks.Run();
        InfoChecks.Run();
        uint[][] rows = {
            new uint[] {0,250,400,400,400,500,500,500,500,500,400,400,400,250,0},
            new uint[] {0,500,800,800,800,1000,1000,1000,1000,1000,800,800,800,500,0},
            new uint[] {0,750,1200,1200,1200,1500,1500,1500,1500,1500,1200,1200,1200,750,0},
            new uint[] {0,1000,1250,1500,2000,2500,2500,2500,2500,2500,2000,1500,1250,1000,0},
            new uint[] {0,250,400,400,400,500,500,500,500,500,400,400,400,250,0},
            new uint[] {0,30,40,40,40,50,75,100,75,50,40,40,40,30,0}
        };
        for (int k = 0; k < 6; k++) for (int t = 0; t < 15; t++) Scores[k, t] = rows[k][t];
        if (args.Length > 0) { NativeChecks.Run(args[0]); VerifyGameTable(args[0]); }
        if (args.Length > 1) TestRealCharts(args[1]);
        if (args.Length > 2) InfoChecks.VerifyBinary(args[2], Path.Combine(Path.GetDirectoryName(args[0]), "AMDaemon.NET.dll"), args.Length > 3 ? args[3] : null);

        // Exhaustive oracle, including EX and BREAK: compare narrow intervals against
        // every legal judgement combination, rather than duplicating the search.
        foreach (var kinds in new[] { new[] {0}, new[] {3}, new[] {0,3}, new[] {1,4,3}, new[] {2,0,3} })
        foreach (bool ex in new[] {false, true})
        foreach (MinimumCombo combo in Enum.GetValues<MinimumCombo>())
        {
            var notes = kinds.Select((k, i) => new ScorePlanner.Note { Index = i, Kind = k, Ex = ex && i == 0 }).ToArray();
            var reachable = Enumerate(notes, combo).Distinct().Order().ToArray();
            foreach (decimal value in reachable)
            {
                decimal lower = Math.Max(0, value - 0.000000001m);
                var result = Generate(notes, lower, value + 0.000001m, combo, 123);
                Check(result.Success, $"missed reachable {value} {combo}");
                Validate(notes, result, lower, value + 0.000001m, combo);
            }
            for (int i = 0; i < Bounds.Length - 1; i++)
            {
                bool possible = reachable.Any(v => v >= Bounds[i] && (i == 13 ? v <= Bounds[i+1] : v < Bounds[i+1]));
                var result = Generate(notes, Bounds[i], Bounds[i+1], combo, 77, i == 13);
                Check(result.Success == possible, $"feasibility mismatch {combo} [{Bounds[i]}, {Bounds[i+1]}]: {result.Error}");
                if (result.Success) Validate(notes, result, Bounds[i], Bounds[i+1], combo, i == 13);
            }
        }
        var charts = new[] {
            Enumerable.Range(0, 800).Select(i => new ScorePlanner.Note { Index=i, Kind=i%5, Ex=i%17==0 }).ToArray(),
            Enumerable.Range(0, 3000).Select(i => new ScorePlanner.Note { Index=i, Kind=i%5 }).ToArray(),
            Enumerable.Range(0, 600).Select(i => new ScorePlanner.Note { Index=i, Kind=i%3 }).ToArray()
        };
        var watch = Stopwatch.StartNew();
        int runs = 0;
        foreach (var notes in charts)
        foreach (MinimumCombo combo in Enum.GetValues<MinimumCombo>())
        for (int rank = 0; rank < 14; rank++)
        for (int seed = 0; seed < 5; seed++)
        {
            var result = Generate(notes, Bounds[rank], Bounds[rank+1], combo, seed, rank == 13);
            if (result.Success) Validate(notes, result, Bounds[rank], Bounds[rank+1], combo, rank == 13);
            else Check(!result.Error.Contains("limit"), $"unexpected search timeout: {combo} rank {rank}");
            runs++;
        }
        var sample = charts[0];
        Check(Generate(sample,99,100,MinimumCombo.None,1).Timings.SequenceEqual(Generate(sample,99,100,MinimumCombo.None,1).Timings), "seed reproducibility");
        Check(!Generate(sample,99,100,MinimumCombo.None,1).Timings.SequenceEqual(Generate(sample,99,100,MinimumCombo.None,2).Timings), "randomness");
        var forced = new[] {new ScorePlanner.Note {Index=0,Kind=0,ForceMiss=true}, new ScorePlanner.Note {Index=1,Kind=3}};
        var forcedResult = Generate(forced,0,101,MinimumCombo.None,3,true);
        Check(forcedResult.Success && (forcedResult.Timings[0] == 0 || forcedResult.Timings[0] == 14), "forced miss");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Check(!ScorePlanner.Generate(new ScorePlanner.Request {Notes=sample,Scores=Scores,Lower=99,Upper=100},cancel.Token).Success,"cancellation");
        Check(!ScorePlanner.Generate(new ScorePlanner.Request {Notes=sample,Scores=Scores,Lower=99,Upper=100,NodeLimit=0}).Success,"node budget");
        var custom = new ScorePlanner.Request {Notes=sample,Scores=Scores,Lower=98,Upper=99,IncludeUpper=true,PreferUpper=true,Seed=1};
        var customResult=ScorePlanner.Generate(custom);
        Check(customResult.Success,"custom approximate score");
        sample[0].Time=1;
        Validate(sample,customResult,98,99,MinimumCombo.None,true);
        Console.WriteLine($"PASS: {_checks} assertions; {runs} large-chart cases in {watch.ElapsedMilliseconds} ms.");
    }

    private static ScorePlanner.Result Generate(ScorePlanner.Note[] notes, decimal lower, decimal upper, MinimumCombo combo, int seed, bool inclusive=false)
        => ScorePlanner.Generate(new ScorePlanner.Request {Notes=notes,Scores=Scores,Lower=lower,Upper=upper,Minimum=combo,Seed=seed,IncludeUpper=inclusive});

    private static IEnumerable<decimal> Enumerate(ScorePlanner.Note[] notes, MinimumCombo combo)
    {
        var timings = new int[notes.Length];
        var values = new List<decimal>();
        void Visit(int i)
        {
            if (i == notes.Length) { values.Add(Achievement(notes,timings)); return; }
            for (int t=0;t<15;t++)
            {
                bool allowed = !notes[i].Ex || t==0 || t==7 || t==14;
                allowed &= combo switch {
                    MinimumCombo.None => true,
                    MinimumCombo.FC => t != 0 && t != 14,
                    MinimumCombo.FCPlus => t >= 2 && t <= 12,
                    MinimumCombo.AP => t >= 5 && t <= 9,
                    _ => false
                };
                if (allowed) {timings[i]=t; Visit(i+1);}
            }
        }
        Visit(0); return values;
    }

    private static decimal Achievement(ScorePlanner.Note[] notes, int[] timings)
    {
        long total=0,bonus=0,got=0,gotBonus=0;
        for (int i=0;i<notes.Length;i++)
        {
            int kind=notes[i].Kind; total+=Scores[kind,7]; got+=Scores[kind,timings[i]];
            if(kind==3) {bonus+=Scores[5,7];gotBonus+=Scores[5,timings[i]];}
        }
        return 100m*got/total+(bonus==0?0:(decimal)gotBonus/bonus);
    }

    private static void Validate(ScorePlanner.Note[] notes, ScorePlanner.Result result, decimal lower, decimal upper, MinimumCombo combo, bool inclusive=false)
    {
        decimal achievement=Achievement(notes,result.Timings);
        Check(Math.Abs(achievement-result.Achievement)<0.000000000001m,"native arithmetic match");
        Check(achievement>=lower && (inclusive?achievement<=upper:achievement<upper),"rank boundary");
        for(int i=0;i<notes.Length;i++)
        {
            int t=result.Timings[i];
            Check(!notes[i].Ex || t==0 || t==7 || t==14,"EX rule");
            Check(combo==MinimumCombo.None || (t!=0 && t!=14),"FC rule");
            Check(combo<MinimumCombo.FCPlus || (t!=1 && t!=13),"FC+ rule");
            Check(combo<MinimumCombo.AP || (t>=5 && t<=9),"AP rule");
        }
    }

    private static void VerifyGameTable(string path)
    {
        // SDGB's protected/mixed-mode assembly cannot be executed by CoreCLR.
        // Inspect its IL through ILSpy instead; never launch the game or its initializers.
        var start=new ProcessStartInfo("ilspycmd") {RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        foreach(var arg in new[]{"--disable-updatecheck","-t","NoteScore",Path.GetFullPath(path)}) start.ArgumentList.Add(arg);
        using var process=Process.Start(start);
        string text=process.StandardOutput.ReadToEnd();
        string error=process.StandardError.ReadToEnd();
        process.WaitForExit();
        Check(process.ExitCode==0,"ILSpy inspection: "+error);
        var rows=Regex.Matches(text,@"new JuggeScore\(new uint\[15\]\s*\{([^}]+)\}",RegexOptions.Singleline);
        Check(rows.Count==6,"game table layout");
        for(int kind=0;kind<6;kind++)
        {
            var values=Regex.Matches(rows[kind].Groups[1].Value,@"(\d+)u").Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
            Check(values.Length==15,"game timing count");
            for(int timing=0;timing<15;timing++) Check(values[timing]==Scores[kind,timing],"game score table");
        }
        Console.WriteLine("Verified all 90 scoring entries against the supplied game assembly.");
    }

    private static void TestRealCharts(string root)
    {
        var mapping=new Dictionary<string,(int Kind,bool Ex)> {
            ["TAP"]=(0,false),["STR"]=(0,false),["XTP"]=(0,true),["XST"]=(0,true),
            ["HLD"]=(1,false),["XHO"]=(1,true),["SLD"]=(2,false),
            ["BRK"]=(3,false),["BST"]=(3,false),["BXX"]=(3,true),["BHO"]=(3,false),
            ["BXH"]=(3,true),["BSL"]=(3,false),["XBS"]=(3,true),["TTP"]=(4,false),["THO"]=(1,false)
        };
        int charts=0,cases=0;
        long worst=0;
        foreach(var file in Directory.EnumerateFiles(root,"*_03.ma2",SearchOption.AllDirectories).Where(f=>int.TryParse(Path.GetFileName(f).Split('_')[0],out int id)&&id<100000).Order().Take(12))
        {
            var counts=File.ReadLines(file).Where(l=>l.StartsWith("T_REC_")).Select(l=>l.Split('\t')).ToDictionary(p=>p[0],p=>int.Parse(p[1]));
            if(!counts.TryGetValue("T_REC_ALL",out int total)) continue;
            var notes=new List<ScorePlanner.Note>();
            foreach(var pair in mapping)
                for(int i=0;i<counts.GetValueOrDefault("T_REC_"+pair.Key);i++)
                    notes.Add(new ScorePlanner.Note {Index=notes.Count,Kind=pair.Value.Kind,Ex=pair.Value.Ex,Time=notes.Count});
            Check(notes.Count==total,"real chart summary coverage: "+Path.GetFileName(file));
            var array=notes.ToArray();
            foreach(MinimumCombo combo in Enum.GetValues<MinimumCombo>())
            for(int rank=0;rank<14;rank++)
            for(int seed=0;seed<3;seed++)
            {
                var timer=Stopwatch.StartNew();
                var result=Generate(array,Bounds[rank],Bounds[rank+1],combo,seed,rank==13);
                worst=Math.Max(worst,timer.ElapsedMilliseconds);
                Check(result.Success||!result.Error.Contains("limit"),"real chart search timeout");
                if(result.Success) Validate(array,result,Bounds[rank],Bounds[rank+1],combo,rank==13);
                cases++;
            }
            charts++;
        }
        Check(charts>0,"no supported real chart summaries found");
        Console.WriteLine($"Real chart composition checks: {charts} charts, {cases} cases, slowest search {worst} ms.");
    }
    private static void Check(bool value,string message) {if(!value)throw new Exception(message);_checks++;}
}
