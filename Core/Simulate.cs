using System;
using UnityEngine;

namespace BoosterGuidance
{
  public class Simulate
  {
    double MinHeightAtMinThrust(double y, double vy, double amin, double g)
    {
      double minHeight = 0;
      if (amin < g)
        return -float.MaxValue;
      double tHover = -vy / amin; // time to come to hover
      minHeight = y + vy * tHover + 0.5 * amin * tHover * tHover - 0.5 * g * tHover * tHover;
      return minHeight;
    }

    static bool Hit(CelestialBody body, Vector3d r)
    {
      return (r - body.position).magnitude < body.Radius;
    }

    static private Vector3d GetForces(Vessel vessel, Vector3d r, Vector3d v, Vector3d att, double totalMass, double minThrust, double maxThrust,
      Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, double t,
      BLController controller, Vector3d tgt_r, double aeroFudgeFactor,
      out Vector3d steer, out Vector3d vel_air, out double throttle)
    {
      Vector3d F = Vector3d.zero;
      double y = r.magnitude - body.Radius;
      steer = -Vector3d.Normalize(v);
      throttle = 0;

      float lastAng = (float)((-1) * body.angularVelocity.magnitude / Math.PI * 180.0);
      Quaternion lastBodyRot = Quaternion.AngleAxis(lastAng, body.angularVelocity.normalized);
      vel_air = v - body.getRFrmVel(r + body.position);
 
      if (controller != null)
      {
        controller.GetControlOutputs(vessel, totalMass, r + body.position, v, att, y, minThrust, maxThrust, t, body, tgt_r, out throttle, out steer);
        /*
        if (controller.phase == BLControllerPhase.LandingBurn)
        {
          // TODO: Only call this once
          KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust, false, controller.landingBurnEngines);
          amin = minThrust / totalMass;
          amax = maxThrust / totalMass;
        }
        */
        if (throttle > 0)
        {
          F = steer * (minThrust + throttle * (maxThrust - minThrust));
          Debug.Log("[BoosterGuidance] y=" + y + " thrustF=" + F.magnitude + " accel=" + (F.magnitude / totalMass));
        }
        //att = steer; // assume attitude is always correct
      }
      F = F + aeroModel.GetForces(body, r, vel_air, Math.PI) * aeroFudgeFactor; // retrograde

      // gravity
      double R = r.magnitude;
      Vector3d g = r * (-body.gravParameter / (R * R * R));
      F = F + g * totalMass;

      return F;
    }


    static public Vector3d ToGround(double tgtAlt, Vessel vessel, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, BLController controller,
      Vector3d tgt_r, out double T, string logFilename="", Transform logTransform=null, double maxT=600)
      // logTransform is transform at the current time
    {
      float ang;
      Quaternion bodyRotation;
      System.IO.StreamWriter f = null;
      if (logFilename != "")
      {
        f = new System.IO.StreamWriter(logFilename);
        f.WriteLine("time x y z vx vy vz ax ay az att_err airspeed totalMass");
        f.WriteLine("# tgtAlt=" + tgtAlt);
      }

      T = 0;
      Vector3d r = vessel.GetWorldPos3D() - body.position;
      Vector3d v = vessel.GetObtVelocity();
      Vector3d a = Vector3d.zero;
      Vector3d lastv = v;
      double minThrust, maxThrust;
      double totalMass = vessel.GetTotalMass();
      // Initially thrust is for all operational engines
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      if (controller != null)
        controller.noCorrect = true;
      double y = r.magnitude - body.Radius;
      // TODO: att should be supplied as vessel transform will be wrong in simulation
      Vector3d att = new Vector3d(vessel.transform.up.x, vessel.transform.up.y, vessel.transform.up.z);

      double dt = 1; // above atmosphere
      double initialY = y;
      while ((y > tgtAlt) && (T < maxT))
      {
        y = r.magnitude - body.Radius;
        // Get all forces, i.e. aero-dynamic and thrust
        Vector3d vel_air;
        Vector3d steer;
        double throttle;
        double aeroFudgeFactor = 1.35; // Assume aero forces 10% higher which causes overshoot of target and more vertical final descent
        Vector3d F = GetForces(vessel, r, v, att, totalMass, minThrust, maxThrust, aeroModel, body, T, controller, tgt_r, aeroFudgeFactor, out steer, out vel_air, out throttle);
        att = steer; // assume can turn immediately

        if (f != null)
        {
          // NOTE: Cancel out rotation of planet
          ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
          // Rotation 1 second earlier
          float prevang = (float)((-(T - 1)) * body.angularVelocity.magnitude / Math.PI * 180.0);
          // Consider body rotation at this time
          bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
          Quaternion prevbodyRotation = Quaternion.AngleAxis(prevang, body.angularVelocity.normalized);
          Vector3d tr = bodyRotation * r;
          Vector3d tr1 = prevbodyRotation * r;
          Vector3d tr2 = bodyRotation * (r + v);
          Vector3d ta = bodyRotation * a;
          tr = logTransform.InverseTransformPoint(tr + body.position);
          Vector3d tv = logTransform.InverseTransformVector(tr2 - tr1);
          ta = logTransform.InverseTransformVector(ta);
          f.WriteLine(string.Format("{0} {1:F5} {2:F5} {3:F5} {4:F5} {5:F5} {6:F5} {7:F1} {8:F1} {9:F1} 0 {10:F1} {11:F2}", T, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, ta.x, ta.y, ta.z, vel_air.magnitude, totalMass));
        }

        // Equations of motion
        r = r + v * dt;
        lastv = v;
        v = v + (F / totalMass) * dt;

        T = T + dt;

        // Deplete mass as fuel consumed
        // TODO: Approximation using number of engines used is the same
        if (controller != null)
          totalMass -= controller.peakMassFlow * throttle * 10; // 10 is fudge factor!
      }
      if (T > maxT)
        Debug.Log("[BoosterGuidance] Simulation time exceeds maxT=" + maxT);
      // Correct to point of intersect on surface
      double vy = Vector3d.Dot(lastv, Vector3d.Normalize(r));
      double p = 0;
      if (vy < -0.1)
      {
        p = (tgtAlt - y) / -vy; // Backup proportion
        r = r - lastv * p;
        T = T - p;
      }
      if (f != null)
        f.Close();

      if (controller != null)
      {
        Debug.Log("[BoosterGuidance] initialY=" + initialY + " initialMass=" + vessel.totalMass + " finalMass=" + totalMass + " peakMassFlow=" + controller.peakMassFlow);
      }

      // Compensate for body rotation giving world position in the surface point now
      // that would be hit in the future
      ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
      bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
      r = bodyRotation * r ;
      return r + body.position;
    }

    // Simulate trajectory to ground and work out point to fire landing burn assuming air resistance will help slow the vessel down
    // This point will be MUCH later than thrust would be applied minus air resistance
    static public double CalculateLandingBurnAlt(double tgtAlt, Vector3d wr, Vector3d v, Vessel vessel, double totalMass, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body,
      double maxT = 600, string filename="")
    {
      double T = 0;
      Vector3d a = Vector3d.zero;
      double minThrust, maxThrust;
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      Vector3d r = wr - body.position;
      double y = r.magnitude - body.Radius;
      double amin = minThrust / totalMass;
      double amax = maxThrust / totalMass;
      double LandingBurnAlt = y;
      Vector3d att = -Vector3d.Normalize(v);

      double suicideFactor = 0.8;
      double touchdownSpeed = 1;

      System.IO.StreamWriter f = null;
      if (filename != "")
      {
        f = new System.IO.StreamWriter(filename);
        f.WriteLine("time y vy dvy");
      }

      double dt = 0.5;
      while ((y > tgtAlt) && (T < maxT))
      {
        // Get all forces, i.e. aero-dynamic and thrust
        Vector3d steer, vel_air;
        double throttle;
        double aeroFudgeFactor = 1;
        Vector3d F = GetForces(vessel, r, v, -Vector3d.Normalize(v), totalMass, minThrust, maxThrust, aeroModel, body, T, null, Vector3d.zero, aeroFudgeFactor, out steer, out vel_air, out throttle);

        double R = r.magnitude;
        Vector3d g = r * (-body.gravParameter / (R * R * R));

        //float lastAng = (float)((-1) * body.angularVelocity.magnitude / Math.PI * 180.0);
        //Quaternion lastBodyRot = Quaternion.AngleAxis(lastAng, body.angularVelocity.normalized);
        //Vector3d vel_air = v - body.getRFrmVel(r + body.position);
        //Vector3d F = aeroModel.GetForces(body, r, vel_air, Math.PI); // retrograde

        //Debug.Log("[BoosterGuidance] tgtAlt=" + tgtAlt + " y=" + y + " r=" + r + " v=" + v + " amin=" + amin + " amax=" + amax + " wv=" + v.magnitude + " vel_air=" + vel_air.magnitude);

        // Calculate suicide burn velocity
        //Vector3d g = r * (-body.gravParameter / (R * R * R));
        double av = amax - g.magnitude;
        if (av < 0)
          av = 0.1; // pretend we have more thrust to look like we are doing something rather than giving up!!
        // dvy in 3 seconds time (allowing time for engine start up)
        double dvy3 = Math.Sqrt((1 + suicideFactor) * av * ((y - tgtAlt) - vel_air.magnitude * 3)) + touchdownSpeed;

        // Find latest point when velocity is less than desired velocity
        // as it means it is too high in the next time step meaning this is the time to
        // apply landing burn thrust

        if (dvy3 > vel_air.magnitude)
          LandingBurnAlt = y;
        if (f != null)
          f.WriteLine(string.Format("{0} {1:F1} {2:F1} {3:F1}", T, y, vel_air.magnitude, dvy3));

        // Equations of motion
        r = r + v * dt;
        v = v + (F / vessel.totalMass) * dt;
        v = v + (g + a) * dt;
        y = r.magnitude - body.Radius;

        T = T + dt;
      }
      if (T > maxT)
        Debug.Log("[BoosterGuidance] Simulation time exceeds maxT=" + maxT);
      if (f != null)
      {
        f.WriteLine("# LandingBurnAlt=" + LandingBurnAlt);
        f.Close();
      }
      return LandingBurnAlt;
    }
  }
}
