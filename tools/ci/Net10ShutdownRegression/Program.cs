using System;
using System.Linq;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ClientStack.LindenUDP;

static void Assert(bool valid, string title)
{
    if (!valid) throw new Exception("FAILED: " + title);
}
KeyframeMotion kfm = new KeyframeMotion(null, KeyframeMotion.PlayMode.PingPong,
    KeyframeMotion.DataFormat.Rotation | KeyframeMotion.DataFormat.Translation);
kfm.SetKeyframes(new[] {
    new KeyframeMotion.Keyframe {
        Position = new Vector3(1.5f, -2.5f, 3.5f),
        Rotation = new Quaternion(0, 0, 0, 1),
        StartRotation = new Quaternion(0, 0, 0, 1),
        TimeMS = 150, TimeTotal = 250,
        AngularVelocity = new Vector3(1, 2, 3),
        StartPosition = new Vector3(3, 2, 1)
    }
});
byte[] data = kfm.Serialize();
Assert(data.Length > 25 && data[0] == 0x4f && data[1] == 0x47, "OGKF header");
KeyframeMotion restored = KeyframeMotion.FromData(null, data);
Assert(restored != null && !restored.HasLegacySerialization, "safe typed decode");
Assert(data.SequenceEqual(restored.Serialize()), "full typed state roundtrip");
byte[] truncated = data.Take(data.Length - 1).ToArray();
Assert(KeyframeMotion.FromData(null, truncated) == null, "truncated payload rejected");
byte[] invalidVersion = (byte[])data.Clone();
invalidVersion[4] = 0xff;
Assert(KeyframeMotion.FromData(null, invalidVersion) == null, "future version rejected");
byte[] nonFinite = (byte[])data.Clone();
// Magic (4) + version (1) + mode (4) + flags (4) = 13; first vector X
Array.Copy(BitConverter.GetBytes(float.NaN), 0, nonFinite, 13, 4);
Assert(KeyframeMotion.FromData(null, nonFinite) == null, "NaN rejected");
Assert(KeyframeMotion.FromData(null, new byte[] { 1, 2, 3 }) == null, "unknown payload rejected");
Assert(KeyframeMotion.FromData(null, new byte[4 * 1024 * 1024 + 1]) == null,
    "oversize payload rejected");
// Historical BinaryFormatter SerializedStreamHeader; no deserialization is permitted.
byte[] old = { 0, 1, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0 };
KeyframeMotion legacy = KeyframeMotion.FromData(null, old);
Assert(legacy != null && legacy.HasLegacySerialization, "legacy payload kept inert");
Assert(old.SequenceEqual(legacy.Serialize()), "legacy payload retained byte-identically");
legacy.SetKeyframes(new KeyframeMotion.Keyframe[0]);
Assert(!legacy.HasLegacySerialization, "new movement replaces legacy payload");

UnackedPacketCollection ack = new UnackedPacketCollection();
ack.Clear();
Assert(ack.Count() == 0, "ACK queue empty after close");
ack.Acknowledge(123, Environment.TickCount, false);
ack.Remove(123);
ack.Clear();
Assert(ack.Count() == 0, "ACK queue tolerates late callback and repeated close");
Console.WriteLine("PASS: .NET 10 OGKF serialization, legacy preservation and UDP teardown regression");
