// CI-only fixture: creates a known-good NRBF blob on isolated .NET 8.
// Never deserialize input. Never ship this fixture with a simulator.
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

if (args.Length != 1) throw new ArgumentException("Expected output fixture path.");
var frame = new OpenSim.Region.Framework.Scenes.KeyframeMotion.Keyframe
{
    Position = new OpenMetaverse.Vector3 { X = 1.25f, Y = 2.5f, Z = 3.75f },
    Rotation = new OpenMetaverse.Quaternion { X = 0, Y = 0, Z = 0, W = 1 },
    StartRotation = new OpenMetaverse.Quaternion { W = 1 },
    TimeMS = 250,
    TimeTotal = 350,
    AngularVelocity = new OpenMetaverse.Vector3 { X = 1, Y = 2, Z = 3 },
    StartPosition = new OpenMetaverse.Vector3 { X = 6, Y = 5, Z = 4 }
};
var motion = new OpenSim.Region.Framework.Scenes.KeyframeMotion
{
    m_serializedPosition = new OpenMetaverse.Vector3 { X = 6, Y = 5, Z = 4 },
    m_basePosition = new OpenMetaverse.Vector3 { X = 6, Y = 5, Z = 4 },
    m_baseRotation = new OpenMetaverse.Quaternion { W = 1 },
    m_currentFrame = frame,
    m_frames = new List<OpenSim.Region.Framework.Scenes.KeyframeMotion.Keyframe> { frame },
    m_keyframes = new[] { frame },
    m_mode = OpenSim.Region.Framework.Scenes.KeyframeMotion.PlayMode.PingPong,
    m_data = OpenSim.Region.Framework.Scenes.KeyframeMotion.DataFormat.Rotation |
        OpenSim.Region.Framework.Scenes.KeyframeMotion.DataFormat.Translation,
    m_running = true,
    m_iterations = 2,
    m_skipLoops = 1
};
using var output = File.Create(args[0]);
#pragma warning disable SYSLIB0011
new BinaryFormatter().Serialize(output, motion);
#pragma warning restore SYSLIB0011
Console.WriteLine("Created isolated legacy .NET8 fixture.");

namespace OpenMetaverse
{
    [Serializable]
    public struct Vector3 { public float X, Y, Z; }
    [Serializable]
    public struct Quaternion { public float X, Y, Z, W; }
}

namespace OpenSim.Region.Framework.Scenes
{
    [Serializable]
    public sealed class KeyframeMotion
    {
        public enum PlayMode { Forward, Reverse, Loop, PingPong }
        [Flags] public enum DataFormat { Translation = 2, Rotation = 1 }
        [Serializable]
        public struct Keyframe
        {
            public OpenMetaverse.Vector3? Position;
            public OpenMetaverse.Quaternion? Rotation;
            public OpenMetaverse.Quaternion StartRotation;
            public int TimeMS, TimeTotal;
            public OpenMetaverse.Vector3 AngularVelocity, StartPosition;
        }
        public OpenMetaverse.Vector3 m_serializedPosition, m_basePosition;
        public OpenMetaverse.Quaternion m_baseRotation;
        public Keyframe m_currentFrame;
        public List<Keyframe> m_frames = new();
        public Keyframe[]? m_keyframes;
        public PlayMode m_mode;
        public DataFormat m_data;
        public bool m_running;
        public int m_iterations, m_skipLoops;
    }
}
