using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Yengine;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("YEngine BinaryFormatter regression: " + message);
}
static void Reject<T>(Action action, string reason) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name + ": " + reason);
}
static XMRInstance Fake()
{
    XMRInstance result =
        (XMRInstance)RuntimeHelpers.GetUninitializedObject(typeof(XMRInstance));
    // The production XMRInstance has a finalizer which expects all constructor
    // locks to exist. These deliberately incomplete test doubles must never
    // enter its normal disposal path during GC.
    SetField(result, "m_disposed", true);
    return result;
}
static void SetField(XMRInstance instance, string name, object value)
{
    // Runtime fields belong to the private base XMRInstAbstract, not the
    // concrete XMRInstance. Reflection must explicitly walk the hierarchy.
    for (Type type = typeof(XMRInstance); type != null; type = type.BaseType)
    {
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }
    }
    throw new Exception("Missing YEngine field " + name);
}
static byte[] WriteStateValue(object value)
{
    XMRInstance instance = Fake();
    using MemoryStream memory = new MemoryStream();
    using BinaryWriter writer = new BinaryWriter(memory);
    SetField(instance, "migrateOutWriter", writer);
    SetField(instance, "migrateOutObjects", new Dictionary<object, int>());
    instance.SendObjValue(value);
    writer.Flush();
    return memory.ToArray();
}
static object ReadStateValue(byte[] data)
{
    XMRInstance instance = Fake();
    using MemoryStream memory = new MemoryStream(data, false);
    using BinaryReader reader = new BinaryReader(memory);
    SetField(instance, "migrateInReader", reader);
    SetField(instance, "migrateInObjects", new Dictionary<int, object>());
    return instance.RecvObjValue();
}
static T InvokeState<T>(string name, params object[] args)
{
    MethodInfo method = typeof(XMRInstance).GetMethod(name,
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new Exception("Missing YEngine state helper " + name);
    try { return (T)method.Invoke(null, args); }
    catch (TargetInvocationException ex) when (ex.InnerException != null)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        throw;
    }
}

try
{
    // Live serializer/deserializer: the .NET 10 branches must be exercised,
    // not just a static check that the unsafe API text disappeared.
    foreach (Exception original in new Exception[] {
        new InvalidOperationException("safe-test"),
        new NullReferenceException("null-test"),
        new ArgumentException("arg-test"),
        new FormatException("format-test")
    })
    {
        byte[] payload = WriteStateValue(original);
        Check(payload[0] == 23, "new SYSERIALSAFE tag");
        Exception restored = ReadStateValue(payload) as Exception;
        Check(restored != null &&
              restored.GetType() == original.GetType() &&
              restored.Message == original.Message, "exception typed roundtrip");
    }

    var thrown = (ScriptThrownException)ScriptThrownException.Wrap("LSL thrown-value");
    byte[] wrapped = WriteStateValue(thrown);
    Check(wrapped[0] == 24, "new THROWNEXSAFE tag");
    var recovered = ReadStateValue(wrapped) as ScriptThrownException;
    Check(recovered != null && (string)recovered.thrown == "LSL thrown-value",
          "script-thrown wrapper value roundtrip");

    int[] inputArray = { 10, 20, 30 };
    int[] roundtripArray = ReadStateValue(WriteStateValue(inputArray)) as int[];
    Check(roundtripArray != null && roundtripArray.Length == 3 &&
          roundtripArray[2] == 30, "allowlisted array roundtrip");

    Reject<NotSupportedException>(() => WriteStateValue(new Uri("https://example.invalid/")),
        "arbitrary CLR objects must not be serialized");
    Reject<NotSupportedException>(() => WriteStateValue(new SystemException("unlisted")),
        "unknown CLR exception types must fail closed");
    Reject<InvalidDataException>(() => WriteStateValue(new int[65537]),
        "oversized array must not serialize");
    Reject<InvalidDataException>(() => WriteStateValue(new string('x', 262145)),
        "oversized string must not serialize");

    // Original ordinal tag numbers are retained for backward compatibility.
    // Legacy BinaryFormatter object graphs MUST NOT be executed or restored.
    Reject<InvalidDataException>(() => ReadStateValue(new byte[] { 21 }),
        "legacy SYSERIAL must reject without reading an object graph");
    Reject<InvalidDataException>(() => ReadStateValue(new byte[] { 22 }),
        "legacy THROWNEX must reject without reading an object graph");

    using (MemoryStream memory = new MemoryStream())
    {
        using (BinaryWriter writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
        {
            writer.Write((byte)9); // SYSARRAY
            writer.Write("System.IO.FileInfo, mscorlib"); // forbidden type lookup
            writer.Write(1);
        }
        Reject<InvalidDataException>(() => ReadStateValue(memory.ToArray()),
            "arbitrary CLR array types must not trigger reflection loading");
    }

    // Saved state XML is written atomically, bounded, and DTD-free.
    string root = Path.Combine(Path.GetTempPath(), "ogl-yengine-regression-" +
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string path = Path.Combine(root, "test-script.state");
        File.WriteAllText(path, "<ScriptState>old</ScriptState>");
        Reject<InvalidDataException>(() =>
            InvokeState<object>("WriteStateFileAtomically", path,
                new string('x', 8 * 1024 * 1024 + 1)),
            "oversized XML write");
        Check(File.ReadAllText(path) == "<ScriptState>old</ScriptState>",
            "bad state write preserved original bytes");
        Check(Directory.GetFiles(root).Length == 1,
            "bad state write did not leave temporary files");

        InvokeState<object>("WriteStateFileAtomically", path,
            "<ScriptState>safe</ScriptState>");
        XmlDocument loaded = InvokeState<XmlDocument>("ReadPersistedStateXmlFile", path);
        Check(loaded.DocumentElement?.InnerText == "safe",
            "atomic state file persisted and reloaded");

        string dangerous = "<!DOCTYPE test [<!ENTITY ext SYSTEM \"file:///etc/passwd\">]>" +
            "<ScriptState>&ext;</ScriptState>";
        File.WriteAllText(path, dangerous);
        Reject<XmlException>(() =>
            InvokeState<XmlDocument>("ReadPersistedStateXmlFile", path),
            "DTD cannot resolve local file");
        Check(File.ReadAllText(path) == dangerous,
            "invalid saved state remains preserved for migration");

        // Exercise the actual private LoadInitialState() path, not only XML
        // helpers: malformed historical data must stop this script without
        // deleting its .state or silently replaying state_entry().
        XMRInstance failedRestore = Fake();
        SetField(failedRestore, "m_StateFileName", path);
        MethodInfo loadInitial = typeof(XMRInstance).GetMethod("LoadInitialState",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Missing YEngine LoadInitialState method");
        Reject<InvalidDataException>(() =>
        {
            try { loadInitial.Invoke(failedRestore, Array.Empty<object>()); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                    ex.InnerException).Throw();
                throw;
            }
        }, "LoadInitialState refuses invalid historical .state without reset");
        Check(File.ReadAllText(path) == dangerous,
            "LoadInitialState preserved original .state bytes");

    // A failed snapshot used to leave suspendOnCheckRunHold=true forever.
    // Force MigrateOut to throw from an incomplete synthetic script.
    XMRInstance incomplete = Fake();
    incomplete.m_Item = new TaskInventoryItem { AssetID = OpenMetaverse.UUID.Random() };
    incomplete.m_ObjCode = (ScriptObjCode)RuntimeHelpers.GetUninitializedObject(
        typeof(ScriptObjCode));
    incomplete.m_ObjCode.sourceHash = "ci-snapshot-failure";
    SetField(incomplete, "m_Engine",
        RuntimeHelpers.GetUninitializedObject(typeof(Yengine)));
    using (var ready = new System.Threading.ManualResetEvent(true))
    {
        SetField(incomplete, "m_DetachReady", ready);
        SetField(incomplete, "m_RunLock", new object());
        Reject<NullReferenceException>(() => incomplete.GetExecutionState(
            new XmlDocument()), "snapshot failure injection reached MigrateOut");
        Check(!incomplete.suspendOnCheckRunHold,
            "failed snapshot released script suspension flag");
    }

    // OAR/region-crossing path SetXMLState used File.Create and unbounded
    // XmlDocument.LoadXml. It must reject invalid inputs without changing
    // the last known-good .state file.
    var engine = (Yengine)RuntimeHelpers.GetUninitializedObject(typeof(Yengine));
    typeof(Yengine).GetField("m_ScriptBasePath",
        BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(engine, root);
    var stateId = OpenMetaverse.UUID.Random();
    string importedPath = XMRInstance.GetStateFileName(root, stateId);
    File.WriteAllText(importedPath, "<ScriptState>preserve</ScriptState>");
    string beforeImport = File.ReadAllText(importedPath);
    string importPrefix = $"<State Engine=\"YEngine\" Asset=\"{stateId}\">" +
        "<ScriptState Engine=\"YEngine\"><Snapshot>";
    string importSuffix = "</Snapshot></ScriptState></State>";

    Check(!engine.SetXMLState(stateId, importPrefix + "???INVALID???" + importSuffix),
        "invalid OAR snapshot base64 rejected");
    Check(File.ReadAllText(importedPath) == beforeImport,
        "invalid OAR snapshot did not replace saved state");
    Check(!engine.SetXMLState(stateId,
        "<!DOCTYPE a [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
        importPrefix + "&x;" + importSuffix), "OAR DTD rejected");
    Check(File.ReadAllText(importedPath) == beforeImport,
        "OAR XML with DTD left previous state unchanged");
    Check(!engine.SetXMLState(stateId, new string('x', 8 * 1024 * 1024 + 1)),
        "oversized OAR XML rejected");
    Check(File.ReadAllText(importedPath) == beforeImport,
        "oversized OAR XML left saved state unchanged");

    Check(engine.SetXMLState(stateId, importPrefix + "AQID" + importSuffix),
        "valid bounded YEngine OAR state imported");
    Check(File.ReadAllText(importedPath).Contains("<Snapshot>AQID</Snapshot>"),
        "valid OAR state atomically installed");
    Check(Directory.GetFiles(Path.GetDirectoryName(importedPath)!).Length == 1,
        "OAR importer left no temporary files");

        File.WriteAllText(path, "original");
        Reject<NotSupportedException>(() => Util.SerializeToFile(path, "new"),
            "obsolete generic serializer must fail closed");
        Check(File.ReadAllText(path) == "original",
            "generic serializer did not truncate existing state");
        Reject<NotSupportedException>(() => Util.DeserializeFromFile(path),
            "obsolete generic deserializer must fail closed");
    }
    finally { Directory.Delete(root, true); }

    Console.WriteLine("PASS: YEngine .NET 10 safe script state: exception/LSL/array roundtrip, " +
                      "legacy tag rejection, protected .state files and Util fail-closed");
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
    return 1;
}
