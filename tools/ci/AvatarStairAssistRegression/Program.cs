using System;
using OpenMetaverse;
using OpenSim.Region.PhysicsModule.BulletS;

static void Assert(bool passed, string description)
{
    if (!passed)
        throw new InvalidOperationException("FAILED: " + description);
}

static bool Step(float surface, float normal, out float rise)
{
    return BSActorAvatarMove.TryComputeStairRise(
        21f, surface, normal, 0.45f, 0.10f, 0.70f, out rise);
}

Assert(Step(21.20f, 1f, out float small) && Math.Abs(small - 0.1f) < 0.0001f,
    "20 cm normal step is accepted and rise per frame is bounded");
Assert(Step(21.08f, 0.85f, out float shallow) && shallow > 0f && shallow < 0.10f,
    "low step can be climbed gradually");
Assert(!Step(21.50f, 1f, out _), "50 cm wall exceeds configured 45 cm limit");
Assert(!Step(21.30f, 0.01f, out _), "vertical wall face is never walkable");
Assert(!Step(21.30f, 0.65f, out _), "steep slope fails normal threshold");
Assert(!Step(21f, 1f, out _), "flat floor does not cause an upward pop");
Assert(!Step(20.90f, 1f, out _), "descending stairs do not cause an upward pop");
Assert(!Step(21.03f, 1f, out _), "very small geometry noise is ignored");

Assert(!BSActorAvatarMove.TryComputeStairRise(
    21f, float.PositiveInfinity, 1f, 0.45f, 0.1f, 0.7f, out _),
    "invalid geometry cannot move the avatar");
Assert(!BSActorAvatarMove.TryComputeStairRise(
    21f, 21.15f, float.NaN, 0.45f, 0.1f, 0.7f, out _),
    "invalid normals cannot move the avatar");
Assert(!BSActorAvatarMove.TryComputeStairRise(
    21f, 21.15f, 1f, 0.45f, 0f, 0.7f, out _),
    "disabled maximum rise gives no movement");

Vector3 forward = BSActorAvatarMove.HorizontalStairDirection(new Vector3(3f, 0f, 0f));
Assert(Math.Abs(forward.X - 1f) < 0.0001f && Math.Abs(forward.Y) < 0.0001f,
    "forward movement is normalized");
Vector3 backwards = BSActorAvatarMove.HorizontalStairDirection(new Vector3(-2f, 0f, 0f));
Assert(Math.Abs(backwards.X + 1f) < 0.0001f, "backwards movement is respected");
Vector3 strafe = BSActorAvatarMove.HorizontalStairDirection(new Vector3(0f, -3f, 0f));
Assert(Math.Abs(strafe.Y + 1f) < 0.0001f, "strafing direction is respected");
Vector3 diagonal = BSActorAvatarMove.HorizontalStairDirection(new Vector3(2f, 2f, 0f));
Assert(Math.Abs(diagonal.Length() - 1f) < 0.0001f && diagonal.X > 0f && diagonal.Y > 0f,
    "diagonal input uses its horizontal travel direction");
Assert(BSActorAvatarMove.HorizontalStairDirection(new Vector3(0f, 0f, 3f)) == Vector3.Zero,
    "vertical/jump-only movement does not trigger steps");
Assert(BSActorAvatarMove.HorizontalStairDirection(new Vector3(float.NaN, 1f, 0f)) == Vector3.Zero,
    "non-finite input cannot trigger stepping");

Console.WriteLine("PASS: OGL BulletSim avatar stair height, slope, velocity and safety regression");
