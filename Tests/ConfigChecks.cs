using SinmaiAssist.Config;

namespace MelonLoader
{
    internal static class MelonLogger
    {
        public static void Msg(string _) { }
        public static void Error(string _) { }
        public static void Warning(string _) { }
    }
}

namespace SinmaiAssist.Config
{
    public class ConfigFixture
    {
        public string Value { get; set; } = "default";
        public NestedFixture Nested { get; set; } = new NestedFixture();
    }
    public class NestedFixture { public string Key { get; set; } = "K"; }
}

internal static class ConfigChecks
{
    public static void Run()
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"Scratch-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string good=Path.Combine(directory,"good.yml"), bad=Path.Combine(directory,"bad.yml");
            const string original="value: retained\nnested:\n  key: F\n";
            File.WriteAllText(good,original);
            File.WriteAllText(bad,"nested: [invalid");
            var first=new ConfigManager<ConfigFixture>(good).GetConfig();
            var recovered=new ConfigManager<ConfigFixture>(bad).GetConfig();
            Assert(first.Value=="retained" && first.Nested.Key=="F","valid configuration values");
            Assert(File.ReadAllText(good)==original,"unrelated valid config preserved");
            Assert(recovered.Value=="default","invalid config recovered");
            Assert(Directory.GetFiles(directory,"bad.yml.broken.*").Length==1,"invalid config backup");
            File.WriteAllText(bad,"");
            Assert(new ConfigManager<ConfigFixture>(bad).GetConfig().Nested.Key=="K","empty yaml defaults");
            File.WriteAllText(bad,"value: null\nnested: null");
            var config=new ConfigManager<ConfigFixture>(bad).GetConfig();
            ConfigManager<ConfigFixture>.FillDefaults(config,new ConfigFixture());
            Assert(config.Value=="default" && config.Nested.Key=="K","explicit null nodes");
            File.WriteAllText(bad,"nested:\n  key: null\n");
            config=new ConfigManager<ConfigFixture>(bad).GetConfig();
            ConfigManager<ConfigFixture>.FillDefaults(config,new ConfigFixture());
            Assert(config.Nested.Key=="K","explicit null key");
            Console.WriteLine("PASS: config recovery, backups, empty YAML, null nodes and preservation checks.");
            if (OperatingSystem.IsWindows()) CheckBuildScript(directory);
        }
        finally
        {
            foreach(var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static void CheckBuildScript(string directory)
    {
        string script=Path.Combine(directory,"BeforeBuild.bat");
        string source=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../BeforeBuild.bat"));
        File.Copy(source,script);
        var start=new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=AppContext.BaseDirectory
        };
        start.ArgumentList.Add("/d"); start.ArgumentList.Add("/c"); start.ArgumentList.Add(script);
        // Exercise the no-Git fallback without touching the user's PATH or Git settings.
        start.Environment["PATH"]=Environment.SystemDirectory+";"+Path.Combine(Environment.SystemDirectory,"WindowsPowerShell/v1.0");
        using var process=System.Diagnostics.Process.Start(start);
        string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert(process.ExitCode==0,"build script failed: "+output+error);
        string info=File.ReadAllText(Path.Combine(directory,"BuildInfo.cs"));
        Assert(info.Contains("CommitHash = \"NOT SET\""),"build script no-Git fallback");
        Assert(!info.Contains("BuildDate = \"\""),"build script timestamp");
        Console.WriteLine("PASS: Windows build script uses its own directory and handles missing Git.");
    }
    private static void Assert(bool condition,string description) { if(!condition)throw new Exception(description); }
}
