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
    return (XMRInstance)RuntimeHelpers.GetUninitializedObject(typeof(XMRInstance));
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
