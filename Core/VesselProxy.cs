using System;
namespace BoosterGuidance
{
  public class VesselProxy
  {
    Vessel vessel;
    public double minThrust, maxThrust;
    public double totalMass => vessel.totalMass;
    public Vector3d r => vessel.GetWorldPos3D();
    public Vector3d v => vessel.GetObtVelocity();

    public VesselProxy(Vessel a_vessel)
    {
      vessel = a_vessel;
    }

    public VesselProxy(VesselProxy a_proxy)
    {
      vessel = a_proxy.vessel;
    }
  }
}
