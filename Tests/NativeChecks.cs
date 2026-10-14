using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class NativeChecks
{
    public static void Run(string assemblyPath)
    {
        using var stream=File.OpenRead(assemblyPath);
        using var pe=new PEReader(stream);
        var metadata=pe.GetMetadataReader();
        var types=metadata.TypeDefinitions.Where(h=>metadata.GetTypeDefinition(h).GetDeclaringType().IsNil).ToDictionary(h=>FullName(metadata,h),h=>h);
        var required=new Dictionary<string,string[]>
        {
            ["Process.GameProcess"]=new[]{"_sequence","_monitors","_message","OnStart","OnUpdate","OnRelease","SetRelease","UpdateSubbMonitorData","IsPartyPlay"},
            ["Process.ProcessBase"]=new[]{"container"},
            ["Manager.GameScoreList"]=new[]{"_judgeResultList","_resultList","<Combo>k__BackingField","<MaxCombo>k__BackingField","<Chain>k__BackingField","<MaxChain>k__BackingField","<Fast>k__BackingField","<Late>k__BackingField","<ComboAddThisFrame>k__BackingField","<ResetCombo>k__BackingField","SetResult","CalcUpdate","FinishPlay","UpdateAfter"},
            ["Manager.GamePlayManager"]=new[]{"_chainCounter","SetSyncResult"},
            ["Monitor.NoteBase"]=new[]{"AppearMsec","IsExNote","PlayJudgeSe","JudgeResult","JudgeTimingDiffMsec","JudgeType","get_ButtonId","IsNoteCheckTimeStart","SetAutoPlayJudge"},
            ["Monitor.SlideRoot"]=new[]{"TailMsec","lastWaitTime","lastWaitTimeForJudge","Judge"},
            ["Monitor.TouchNoteB"]=new[]{"TouchArea","Judge"},
            ["Manager.NotesManager"]=new[]{"_curMSec","_msecStartGap","StartPlay","StopPlay","clearNotes","UpdateTimer"}
        };
        int count=0;
        foreach(var pair in required)
        {
            if(!types.TryGetValue(pair.Key,out var handle)) throw new Exception("Missing game type: "+pair.Key);
            var type=metadata.GetTypeDefinition(handle);
            var members=type.GetFields().Select(f=>metadata.GetString(metadata.GetFieldDefinition(f).Name))
                .Concat(type.GetMethods().Select(m=>metadata.GetString(metadata.GetMethodDefinition(m).Name))).ToHashSet();
            foreach(var name in pair.Value)
            {
                if(!members.Contains(name)) throw new Exception("Missing native interface: "+pair.Key+"."+name);
                count++;
            }
        }
        var sequence=metadata.TypeDefinitions.Select(h=>metadata.GetTypeDefinition(h)).Single(t=>
            metadata.GetString(t.Name)=="GameSequence" && !t.GetDeclaringType().IsNil &&
            FullName(metadata,t.GetDeclaringType())=="Process.GameProcess");
        foreach(var fieldHandle in sequence.GetFields())
        {
            var field=metadata.GetFieldDefinition(fieldHandle);
            string name=metadata.GetString(field.Name);
            if(name!="Play" && name!="Release") continue;
            var constant=metadata.GetConstant(field.GetDefaultValue());
            int value=metadata.GetBlobReader(constant.Value).ReadInt32();
            if(value!=(name=="Play"?4:9)) throw new Exception("Unsupported game sequence values.");
        }
        Console.WriteLine($"PASS: {count} native scoring, note and process interfaces; game sequence constants.");
    }

    private static string FullName(MetadataReader metadata,TypeDefinitionHandle handle)
    {
        var type=metadata.GetTypeDefinition(handle);
        string space=metadata.GetString(type.Namespace),name=metadata.GetString(type.Name);
        return string.IsNullOrEmpty(space)?name:space+"."+name;
    }
}
