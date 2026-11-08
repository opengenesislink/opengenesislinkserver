using OpenSim.Region.CoreModules.World.Land;

static void Check(bool success, string what)
{
    if (!success) throw new InvalidOperationException("Mesh LI regression failed: " + what);
}
Check(OglMeshLandImpactMath.LandImpact(10, false, 0, 0, 5) == 10, "classic 10-prims count 10");
Check(OglMeshLandImpactMath.ServerWeight(10, 0, false) == 5, "10 static mesh parts have server weight 5");
Check(OglMeshLandImpactMath.LandImpact(10, true, 2, 3, 5) == 5, "10 optimized mesh parts have 5 LI");
Check(OglMeshLandImpactMath.LandImpact(1, true, 0.05, 0.10, 0.5) == 1, "single mesh at least one LI");
Check(OglMeshLandImpactMath.LandImpact(3, true, 0.3, 0.6, 1.5) == 2, "round linked object only once");
Check(OglMeshLandImpactMath.LandImpact(10, true, 15.1, 1, 5) == 16, "high download cost raises impact");
Check(OglMeshLandImpactMath.LandImpact(10, true, 0.1, 20, 5) == 20, "complex physics raises impact");
Check(OglMeshLandImpactMath.ServerWeight(10, 0, true) == 10, "physical no static discount");
Check(OglMeshLandImpactMath.ServerWeight(10, 5, false) == 6, "script cost contributes");
Check(OglMeshLandImpactMath.ServerWeight(10, 300, false) == 10, "script weight bounded at parts");
Check(OglMeshLandImpactMath.LandImpact(10, true, double.NaN, 0, 5) == 10, "invalid weights fail closed");
Check(OglMeshLandImpactMath.LandImpact(10, true, -1, 0, 5) == 10, "negative weights fail closed");
Check(OglMeshLandImpactMath.LandImpact(0, true, 1, 1, 1) == 0, "empty object");
Check(OglMeshLandImpactMath.StreamingWeight(400, 300, 200, 100, 3) > 0, "valid mesh LOD streaming");
Check(OglMeshLandImpactMath.StreamingWeight(80000, 50000, 20000, 10000, 12) >
      OglMeshLandImpactMath.StreamingWeight(800, 500, 200, 100, 12), "large LOD costs more");
Check(double.IsNaN(OglMeshLandImpactMath.StreamingWeight(0, 0, 0, 0, 1)), "invalid LOD fails closed");
Console.WriteLine("OpenGenesisLINK G1 mesh land impact arithmetic: PASS");
