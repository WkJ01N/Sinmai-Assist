using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SinmaiAssist.GUI;
using SinmaiAssist.Utils;

internal static class InfoChecks
{
    private sealed class ReferenceId { public override string ToString() => "reference-id"; }
    private readonly struct ValueId { public override string ToString() => "value-id"; }
    private static class ReferenceApi { public static ReferenceId KeychipId => new ReferenceId(); }
    private static class ValueApi { public static ValueId KeychipId => new ValueId(); }
    private static class ThrowingApi { public static ValueId KeychipId => throw new InvalidOperationException("not initialized"); }

    public static void Run()
    {
        int resolves = 0;
        var reference = new RuntimePropertyReader(() => { resolves++; return typeof(ReferenceApi); }, "KeychipId");
        Assert(reference.ReadString() == "reference-id" && reference.ReadString() == "reference-id" && resolves == 1,
            "reference type result and cached getter");
        var value = new RuntimePropertyReader(() => typeof(ValueApi), "KeychipId");
        Assert(value.ReadString() == "value-id", "boxed value type result");
        Assert(!typeof(ReferenceApi).GetProperty("KeychipId").PropertyType.IsValueType &&
            typeof(ValueApi).GetProperty("KeychipId").PropertyType.IsValueType, "signature mismatch fixture");

        int calls = 0;
        bool fail = true, loaded = false;
        var log = new List<string>();
        var lazy = new RuntimePropertyReader(() => loaded ? typeof(ValueApi) : null, "KeychipId");
        var snapshot = new InfoSnapshot(new[]
        {
            new InfoSnapshot.Field { Label = "Stable", Read = () => { calls++; return "available"; } },
            new InfoSnapshot.Field { Label = "Recovering", Read = () => fail ? throw new BadImageFormatException("bad signature") : "recovered" },
            new InfoSnapshot.Field { Label = "Keychip", Read = lazy.ReadString },
            new InfoSnapshot.Field { Label = "Empty", Read = () => null },
            new InfoSnapshot.Field { Label = "Native error", Read = new RuntimePropertyReader(() => typeof(ThrowingApi), "KeychipId").ReadString }
        }, log.Add);
        snapshot.Update(false, 0, "header", false);
        Assert(calls == 0, "hidden overlay does not sample");
        snapshot.Update(true, 0, "header", true);
        Assert(calls == 1 && snapshot.Text.Contains("Stable: available") && snapshot.Text.Contains("Recovering: Unavailable") &&
            snapshot.Text.Contains("Empty: Not ready") && snapshot.Text.Contains("Safe Mode"), "partial failure preserves other fields");
        Assert(log.Count == 3 && log.Any(l => l.Contains("InvalidOperationException: not initialized")), "native exception unwrapped");
        string text = snapshot.Text;
        for (int i = 0; i < 100; i++)
        {
            snapshot.Update(true, 0.5, "header", false);
            Assert(snapshot.Text == text, "repaint consumes unchanged text");
        }
        Assert(calls == 1 && log.Count == 3, "one second sampling and deduplicated logging");
        for (int i = 1; i <= 15; i++) snapshot.Update(true, i, "header", false);
        Assert(log.Count == 3 && calls == 16, "no error flood or permanent ten-failure lockout");
        fail = false; loaded = true;
        snapshot.Update(true, 16, "header", false);
        Assert(snapshot.Text.Contains("Recovering: recovered") && snapshot.Text.Contains("Keychip: value-id"), "initialization recovery");
        snapshot.Update(false, 16.1, "header", false);
        snapshot.Update(true, 16.2, "new heading", false);
        Assert(calls == 18 && snapshot.Text.StartsWith("new heading"), "toggle immediately refreshes");
        fail = true;
        snapshot.Update(true, 17.2, "header", false);
        Assert(log.Count == 3, "same error remains deduplicated after recovery");
        Assert(InfoPanelLayout.Width(1920) == 600 && InfoPanelLayout.Width(320) == 300 && InfoPanelLayout.Width(10) == 0,
            "width and narrow-screen bounds");
        Assert(InfoPanelLayout.Height(1080, 240) == 244 && InfoPanelLayout.Height(120, 240) == 76 &&
            InfoPanelLayout.Height(30, 240) == 0, "multiline height and screen bounds");
        Console.WriteLine("PASS: Show Info class/struct getters, caching, partial failures, recovery, toggles, log deduplication and layout bounds.");
    }

    public static void VerifyBinary(string modPath, string runtimeAmDaemonPath, string legacyModPath = null)
    {
        using var stream = File.OpenRead(modPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        Assert(!reader.TypeReferences.Any(h => TypeName(reader, h) == "AMDaemon.SerialId"),
            "new DLL must not encode an external SerialId class/struct assumption");
        Assert(KeychipReturnKind(reader) == null, "new DLL must not directly call get_KeychipId");
        byte runtimeKind = SerialIdKind(runtimeAmDaemonPath);
        if (legacyModPath != null)
        {
            using var oldStream = File.OpenRead(legacyModPath);
            using var oldPe = new PEReader(oldStream);
            byte? oldKind = KeychipReturnKind(oldPe.GetMetadataReader());
            Assert(oldKind != null && oldKind != runtimeKind, "legacy Show Info signature mismatch must be detected");
            Console.WriteLine($"PASS: reproduced legacy return-type mismatch (0x{oldKind:X2} vs runtime 0x{runtimeKind:X2}) through metadata.");
        }
        Console.WriteLine("PASS: built DLL has no direct SerialId / Keychip getter signature; runtime AMDaemon type inspected.");
    }

    private static byte SerialIdKind(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (reader.GetString(type.Namespace) != "AMDaemon" || reader.GetString(type.Name) != "SerialId") continue;
            string parent = type.BaseType.Kind == HandleKind.TypeReference ? TypeName(reader, (TypeReferenceHandle)type.BaseType) : "";
            return parent == "System.ValueType" || parent == "System.Enum" ? (byte)0x11 : (byte)0x12;
        }
        throw new Exception("Runtime AMDaemon.SerialId not found.");
    }

    private static byte? KeychipReturnKind(MetadataReader reader)
    {
        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (reader.GetString(member.Name) != "get_KeychipId" || member.Parent.Kind != HandleKind.TypeReference ||
                TypeName(reader, (TypeReferenceHandle)member.Parent) != "AMDaemon.System") continue;
            var signature = reader.GetBlobReader(member.Signature);
            var header = signature.ReadSignatureHeader();
            if (header.IsGeneric) signature.ReadCompressedInteger();
            signature.ReadCompressedInteger(); // parameter count
            return signature.ReadByte(); // ECMA-335 ELEMENT_TYPE_VALUETYPE / CLASS
        }
        return null;
    }

    private static string TypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    private static void Assert(bool condition, string description) { if (!condition) throw new Exception(description); }
}
