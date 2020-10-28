using System;
using UnityEngine;

namespace BoosterGuidance
{
  public class Simulate
  {
    static bool Hit(CelestialBody body, Vector3d r)
    {
      return (r - body.position).magnitude < body.Radius;
    }
    static public Vector3d ToGround(double tgtAlt, Vessel vessel, CelestialBody body, Controller controller, double dt, out double T)
    {
      Debug.Log("Simulate() - start");
      T = 0;
      Vector3d r = vessel.GetWorldPos3D() - body.position;
      Vector3d v = vessel.GetObtVelocity();
      Vector3d lastv = v;
      double minThrust, maxThrust;
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      double amin = minThrust / vessel.totalMass;
      double amax = maxThrust / vessel.totalMass;
      
      double y = r.magnitude - body.Radius;
      while (y > tgtAlt)
      {
        Vector3d vel_air = v - body.getRFrmVel(r);
        double throttle = 0;
        Vector3d steer = Vector3d.zero;
        if (controller != null)
          controller.GetControlOutputs(vessel, T, out throttle, out steer);
        Vector3d f = Trajectories.StockAeroUtil.SimAeroForce(vessel, vel_air, r);
        r = r + v * dt;
        lastv = v;
        v = v - (f / vessel.totalMass) * dt;
        double R = r.magnitude;
        Vector3d g = r * (-body.gravParameter / (R * R * R));
        v = v + g * dt;
        if (throttle > 0)
          v = v + steer * (amin + throttle * (amax - amin));
        y = r.magnitude - body.Radius;
        T = T + dt;
      }
      // Correct to point of intersect on surface
      double vy = Vector3d.Dot(lastv, Vector3d.Normalize(r));
      double p = 0;
      if (vy < -0.1)
      {
        p = (tgtAlt - y) / -vy; // Backup proportion
        r = r - lastv * p;
        T = T - p;
      }

      // Compensate for body rotation giving world position in the surface point now
      // that would be hit in the future
      float ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
      Quaternion bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
      r = bodyRotation * r ;
      Debug.Log("Simulate() - end");
      return r + body.position;
    }
  }
}
