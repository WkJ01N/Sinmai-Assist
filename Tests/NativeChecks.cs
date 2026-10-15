using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Immutable;

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
            ["Process.GameProcess"]=new[]{"_sequence","_monitors","_message","_gameMovie","OnStart","OnUpdate","OnRelease","SetRelease","UpdateSubbMonitorData","IsPartyPlay"},
            ["Process.ProcessBase"]=new[]{"container"},
            ["Manager.ProcessManager"]=new[]{"_processList","AddProcess","ReleaseProcess","IsTimeCounting"},
            ["Manager.GameScoreList"]=new[]{"_judgeResultList","_resultList","<Combo>k__BackingField","<MaxCombo>k__BackingField","<Chain>k__BackingField","<MaxChain>k__BackingField","<Fast>k__BackingField","<Late>k__BackingField","<ComboAddThisFrame>k__BackingField","<ResetCombo>k__BackingField","SetResult","CalcUpdate","FinishPlay","UpdateAfter","GetScoreAt","GetScoreLength"},
            ["Manager.GamePlayManager"]=new[]{"_chainCounter","SetSyncResult","SetQuickRetryFrag","IsQuickRetry","GetScoreListCount","GetGameScore","RemoveScoreList","ClaerLog"},
            ["Process.MusicSelectProcess"]=new[]{"_mainSequence","get_CombineMusicDataList","get_CategoryNameList","get_CurrentCategorySelect","set_CurrentCategorySelect","get_CurrentMusicSelect","set_CurrentMusicSelect","get_DifficultySelectIndex","get_CurrentDifficulty","get_MonitorArray","set_TrialPlayIndex","set_ScoreType","OnGameStart","GetCurrentDifficulty","GetUserScore","IsPlayableMusic","CalcMonitorDifficulty","TimerCountUp","IsTournamentFolder"},
            ["Process.ResultProcess"]=new[]{"OnStart","OnRelease","ToNextCheck"},
            ["Process.NextTrackProcess"]=new[]{"OnStart","ProcessingProcess","isAddAnim","_mode","_monitors"},
            ["FadeProcess"]=new[]{"isAddProcess","toProcess","fromProcess"},
            ["Process.RetryWaitProcess"]=new[]{"_state","OnUpdate"},
            ["Process.UnlockProcess"]=new[]{"_state","OnUpdate"},
            ["Monitor.NextTrackMonitor"]=new[]{"SetNextTrack"},
            ["Monitor.MusicSelectMonitor"]=new[]{"SetDeployList"},
            ["Manager.UserData"]=new[]{"IsAimeUser","CalcTotalValue","get_RatingList","get_ScoreDic"},
            ["Manager.GameManager"]=new[]{"TempMaxTrackCount","MusicTrackNumber","NextMapSelect","NextCharaSelect","PauseFreedomModeTimer","get_IsFreedomTimerPause"},
            ["Monitor.Entry.Parts.Screens.DoneEntry"]=new[]{"IsWeekdayBonus"},
            ["Net.Packet.Mai2.PacketUpsertUserAll"]=new[]{"ToUserAll","Convert",".ctor","Proc"},
            ["Net.Packet.Mai2.PacketUploadUserPlaylog"]=new[]{".ctor","Proc"},
            ["Net.Packet.Helper.PacketHelper"]=new[]{"StartPacket"},
            ["Monitor.GameMonitor"]=new[]{"GameController","Seek","IsAllJudged"},
            ["Monitor.Game.GameCtrl"]=new[]{"ForceNoteCollect","ResetClickFlag","_activeNoteList","_activeSlideList","_judgeGradeObjectList","UpdateNotes"},
            ["NoteJudge"]=new[]{"GetNoteCheckStart","GetNoteCheckEnd"},
            ["Manager.JudgeResultSt"]=new[]{"Judged","Timing"},
            ["Monitor.NoteBase"]=new[]{"AppearMsec","IsExNote","PlayJudgeSe","JudgeResult","JudgeTimingDiffMsec","JudgeType","get_ButtonId","IsNoteCheckTimeStart","SetAutoPlayJudge","NoteIndex","get_MonitorId","GetJudgeResult"},
            ["Monitor.SlideRoot"]=new[]{"TailMsec","lastWaitTime","lastWaitTimeForJudge","Judge","NoteIndex","get_MonitorId","GetJudgeResult"},
            ["Monitor.TouchNoteB"]=new[]{"TouchArea","Judge"},
            ["Manager.NotesManager"]=new[]{"_curMSec","_msecStartGap","StartPlay","StopPlay","clearNotes","UpdateTimer"},
            ["Manager.SoundManager"]=new[]{"StartMusic","PauseMusic","SeekMusic","IsMusicPrepare"},
            ["Manager.SoundCtrl"]=new[]{"_players","Play","Stop","GetPlayTiming"},
            ["Manager.MovieController"]=new[]{"_moviePlayers","SetSeekFrame","Pause"}
        };
        int count=0;
        foreach(var pair in required)
        {
            if(!types.TryGetValue(pair.Key,out var handle)) throw new Exception("Missing game type: "+pair.Key);
            var type=metadata.GetTypeDefinition(handle);
            var members=type.GetFields().Select(f=>metadata.GetString(metadata.GetFieldDefinition(f).Name))
                .Concat(type.GetMethods().Select(m=>metadata.GetString(metadata.GetMethodDefinition(m).Name)))
                .Concat(type.GetProperties().Select(p=>metadata.GetString(metadata.GetPropertyDefinition(p).Name))).ToHashSet();
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
        CheckMediaInterfaces(Path.Combine(Path.GetDirectoryName(assemblyPath),"Assembly-CSharp-firstpass.dll"));
        CheckBatchInterfaces(metadata,types);
    }
    private static void CheckBatchInterfaces(MetadataReader r,Dictionary<string,TypeDefinitionHandle> types)
    {
        var provider=new SignatureKinds(new());
        MethodSignature<string>[] Constructors(string type)=>r.GetTypeDefinition(types[type]).GetMethods().Select(h=>r.GetMethodDefinition(h))
            .Where(m=>r.GetString(m.Name)==".ctor").Select(m=>m.DecodeSignature(provider,(object)null)).ToArray();
        void RequireConstructor(string type,params string[] parameters)
        {if(!Constructors(type).Any(s=>s.ParameterTypes.SequenceEqual(parameters)))throw new Exception("Native batch constructor mismatch: "+type);}
        RequireConstructor("Net.Packet.Mai2.PacketUpsertUserAll","Int32","Manager.UserData","Int32","System.Action`1","System.Action`1");
        RequireConstructor("Net.Packet.Mai2.PacketUpsertUserAll","Int32","UInt64","Net.VO.Mai2.UserAll","System.Collections.Generic.List`1","System.Action`1","System.Action`1");
        RequireConstructor("Net.Packet.Mai2.PacketUploadUserPlaylog","UInt64","Net.VO.Mai2.UserPlaylog","System.Action`1","System.Action`1");
        RequireConstructor("Manager.UserDatas.UserRate","Int32","Int32","UInt32","UInt32");
        var nested=r.TypeDefinitions.ToDictionary(h=>DefinitionName(r,h));
        foreach(var pair in new[]{("Process.MusicSelectProcess+MainSequence","Update",3),("Process.MusicSelectProcess+MainSequence","ReleaseWait",4),
            ("Process.NextTrackProcess+NextTrackMode","NextTrack",0),("Process.UnlockProcess+UnlockSequence","DispEnd",4),("DB.PlayComboflagID","AllPerfectPlus",4)})
        {
            var type=r.GetTypeDefinition(nested[pair.Item1]);var field=type.GetFields().Select(h=>r.GetFieldDefinition(h)).Single(f=>r.GetString(f.Name)==pair.Item2);
            if(r.GetBlobReader(r.GetConstant(field.GetDefaultValue()).Value).ReadInt32()!=pair.Item3)throw new Exception("Native batch state constant mismatch: "+pair.Item1+"."+pair.Item2);
        }
        var musicType=r.GetTypeDefinition(nested["Process.MusicSelectProcess+MusicSelectData"]);
        var members=musicType.GetFields().Select(h=>r.GetString(r.GetFieldDefinition(h).Name)).ToHashSet();
        foreach(string name in new[]{"MusicData","ScoreData","Difficulty","isEnable","isExistsScore"})if(!members.Contains(name))throw new Exception("Native batch queue field missing: "+name);
        Console.WriteLine("PASS: CN upload track-count and immutable-snapshot constructors, selection/result/retry/checkpoint interfaces, AP+ flag and batch state constants.");
    }
    private static void CheckMediaInterfaces(string path)
    {
        using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var r=pe.GetMetadataReader();
        var types=r.TypeDefinitions.ToDictionary(h=>DefinitionName(r,h));
        var required=new Dictionary<string,string[]>{
            ["CriAtomExPlayer"]=new[]{"Start","ResetParameters","SetVoicePoolIdentifier","SetPitch","SetDspTimeStretchRatio","UpdateAll"},
            ["CriAtomExVoicePool"]=new[]{"AttachDspTimeStretch","Dispose"},
            ["CriAtomExStandardVoicePool"]=new[]{".ctor"},
            ["CriMana.Player"]=new[]{"SetSpeed"}};
        foreach(var pair in required)
        {
            if(!types.TryGetValue(pair.Key,out var handle))throw new Exception("Missing media type: "+pair.Key);
            var names=r.GetTypeDefinition(handle).GetMethods().Select(h=>r.GetString(r.GetMethodDefinition(h).Name)).ToHashSet();
            foreach(var name in pair.Value)if(!names.Contains(name))throw new Exception("Missing media interface: "+pair.Key+"."+name);
        }
        Console.WriteLine("PASS: native CRI practice music routing, time stretch, voice-pool lifetime and movie speed interfaces.");
    }

    public static void CheckSignatures(string modPath,string gamePath)
    {
        var actual=new Dictionary<(string,string),bool>();
        foreach(var path in new[]{gamePath,Path.Combine(Path.GetDirectoryName(gamePath),"AMDaemon.NET.dll")})
        {
            using var file=File.OpenRead(path);using var pe=new PEReader(file);var r=pe.GetMetadataReader();
            string assembly=r.GetString(r.GetAssemblyDefinition().Name);
            foreach(var h in r.TypeDefinitions)
            {
                var type=r.GetTypeDefinition(h);
                string parent=type.BaseType.Kind==HandleKind.TypeReference ? RefName(r,(TypeReferenceHandle)type.BaseType) : "";
                actual[(assembly,DefinitionName(r,h))]=parent=="System.ValueType"||parent=="System.Enum";
            }
        }
        using var mod=File.OpenRead(modPath);using var image=new PEReader(mod);var metadata=image.GetMetadataReader();
        var provider=new SignatureKinds(actual);
        foreach(var h in metadata.MemberReferences)
        {
            var member=metadata.GetMemberReference(h);
            if(member.GetKind()==MemberReferenceKind.Field) member.DecodeFieldSignature(provider,(object)null);
            else member.DecodeMethodSignature(provider,(object)null);
        }
        foreach(var h in metadata.MethodDefinitions) metadata.GetMethodDefinition(h).DecodeSignature(provider,(object)null);
        foreach(var h in metadata.FieldDefinitions) metadata.GetFieldDefinition(h).DecodeSignature(provider,(object)null);
        Console.WriteLine($"PASS: {provider.Checked} game/AMDaemon value-vs-reference signature occurrences match runtime metadata.");
    }
    private static string DefinitionName(MetadataReader r,TypeDefinitionHandle h)
    {
        var t=r.GetTypeDefinition(h);
        return t.GetDeclaringType().IsNil?FullName(r,h):DefinitionName(r,t.GetDeclaringType())+"+"+r.GetString(t.Name);
    }
    private static string RefName(MetadataReader r,TypeReferenceHandle h)
    {
        var t=r.GetTypeReference(h);
        return t.ResolutionScope.Kind==HandleKind.TypeReference ? RefName(r,(TypeReferenceHandle)t.ResolutionScope)+"+"+r.GetString(t.Name) :
            (r.GetString(t.Namespace).Length==0?"":r.GetString(t.Namespace)+".")+r.GetString(t.Name);
    }
    private sealed class SignatureKinds : ISignatureTypeProvider<string,object>
    {
        private readonly Dictionary<(string,string),bool> _actual;public int Checked;
        public SignatureKinds(Dictionary<(string,string),bool> actual)=>_actual=actual;
        public string GetTypeFromReference(MetadataReader r,TypeReferenceHandle h,byte kind)
        {
            string name=RefName(r,h);var scope=r.GetTypeReference(h).ResolutionScope;
            while(scope.Kind==HandleKind.TypeReference)scope=r.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
            if(scope.Kind==HandleKind.AssemblyReference && _actual.TryGetValue((r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),name),out bool value))
            {
                if(kind==0x11||kind==0x12)
                {
                    if(value!=(kind==0x11))throw new Exception("Runtime class/struct signature mismatch: "+name);
                    Checked++;
                }
            }
            return name;
        }
        public string GetTypeFromDefinition(MetadataReader r,TypeDefinitionHandle h,byte kind)=>DefinitionName(r,h);
        public string GetTypeFromSpecification(MetadataReader r,object c,TypeSpecificationHandle h,byte kind)=>r.GetTypeSpecification(h).DecodeSignature(this,c);
        public string GetArrayType(string t,ArrayShape s)=>t;public string GetByReferenceType(string t)=>t;public string GetFunctionPointerType(MethodSignature<string> s)=>"fn";
        public string GetGenericInstantiation(string t,ImmutableArray<string> a)=>t;public string GetGenericMethodParameter(object c,int i)=>"M";public string GetGenericTypeParameter(object c,int i)=>"T";
        public string GetModifiedType(string m,string t,bool r)=>t;public string GetPinnedType(string t)=>t;public string GetPointerType(string t)=>t;
        public string GetPrimitiveType(PrimitiveTypeCode c)=>c.ToString();public string GetSZArrayType(string t)=>t;
    }

    private static string FullName(MetadataReader metadata,TypeDefinitionHandle handle)
    {
        var type=metadata.GetTypeDefinition(handle);
        string space=metadata.GetString(type.Namespace),name=metadata.GetString(type.Name);
        return string.IsNullOrEmpty(space)?name:space+"."+name;
    }
}
