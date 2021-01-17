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

    static private void EulerStep(
            double dt,
            Vessel vessel, Vector3d r, // in world space but relative to body.position
            Vector3d v, Vector3d att, double totalMass, double minThrust, double maxThrust,
            Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, double t,
            BLController controller, Vector3d tgt_r, double aeroFudgeFactor,
            out Vector3d steer,
            out Vector3d vel_air, out double throttle,
            out Vector3d out_r, out Vector3d out_v)
    {
      double y = r.magnitude - body.Radius;
      steer = -Vector3d.Normalize(v);
      throttle = 0;

      // gravity
      double R = r.magnitude;
      Vector3d g = r * (-body.gravParameter / (R * R * R));

      // Get steer and throttle
      bool bailOutLandingBurn = true;
      if (controller != null)
      {
        bool landingGear;
        controller.GetControlOutputs(vessel, totalMass, r + body.position, v, att, minThrust, maxThrust, t, body, tgt_r, true, out throttle, out steer, out landingGear, bailOutLandingBurn);
        // Stop throttle so we don't take off again in timestep, dt
        // TODO - Fix HACK!!
        if (y < controller.TgtAlt + 50)
          throttle = 0;
      }

      Vector3d Ft = Vector3d.zero;
      if (throttle > 0)
        Ft = steer * (minThrust + throttle * (maxThrust - minThrust));

      // TODO: Do repeated calls to GetForces() mess up PID controllers which updates their internal estimates?
      vel_air = v - body.getRFrmVel(r + body.position);
      if (aeroModel == null) {
        Debug.Log("EulerStep() - No aeroModel");
      }
      Vector3d F = aeroModel.GetForces(body, r, vel_air, Math.PI) * aeroFudgeFactor + Ft;
      Vector3d a = F / totalMass + g;

      out_r = r + v * dt + 0.5 * a * dt * dt;
      out_v = v + a * dt;
    }


    static private Vector3d GetForces(Vessel vessel, Vector3d r, Vector3d v, Vector3d att, double totalMass, double minThrust, double maxThrust,
      Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, double t, double dt,
      BLController controller, Vector3d tgt_r, double aeroFudgeFactor,
      out Vector3d steer, out Vector3d vel_air, out double throttle)
    {
      Vector3d F = Vector3d.zero;
      double y = r.magnitude - body.Radius;
      steer = -Vector3d.Normalize(v);
      throttle = 0;

      // gravity
      double R = r.magnitude;
      Vector3d g = r * (-body.gravParameter / (R * R * R));

      float lastAng = (float)((-1) * body.angularVelocity.magnitude / Math.PI * 180.0);
      Quaternion lastBodyRot = Quaternion.AngleAxis(lastAng, body.angularVelocity.normalized);
      vel_air = v - body.getRFrmVel(r + body.position);
 
      if (controller != null)
      {
        bool bailOutLandingBurn = true;
        bool simulate = true;
        bool landingGear;
        controller.GetControlOutputs(vessel, totalMass, r + body.position, v, att, minThrust, maxThrust, t, body, tgt_r, simulate, out throttle, out steer, out landingGear, bailOutLandingBurn);
        if (throttle > 0)
        {
          F = steer * (minThrust + throttle * (maxThrust - minThrust));
        }
        att = steer; // assume attitude is always correct
      }
      F = F + aeroModel.GetForces(body, r, vel_air, Math.PI) * aeroFudgeFactor; // retrograde

      F = F + g * totalMass;

      return F;
    }

    /*
    static public Vector3d ToGround(double tgtAlt, Vessel vessel, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, BLController controller,
      Vector3d tgt_r, out double T, string logFilename="", Transform logTransform=null, double timeOffset=0, double maxT=600)
      // logTransform is transform at the current time
    {
      float ang;
      Quaternion bodyRotation;
      System.IO.StreamWriter f = null;
      if (logFilename != "")
      {
        f = new System.IO.StreamWriter(logFilename);
        f.WriteLine("time x y z vx vy vz ax ay az att_err target_error total_mass");
        f.WriteLine("# tgtAlt=" + tgtAlt);
      }

      T = 0;
      Vector3d r = vessel.GetWorldPos3D() - body.position;
      Vector3d v = vessel.GetObtVelocity();
      Vector3d a = Vector3d.zero;
      Vector3d lastv = v;
      double minThrust, maxThrust;
      double totalMass = vessel.totalMass;
      // Initially thrust is for all operational engines
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      double y = r.magnitude - body.Radius;
      // TODO: att should be supplied as vessel transform will be wrong in simulation
      Vector3d att = new Vector3d(vessel.transform.up.x, vessel.transform.up.y, vessel.transform.up.z);
      double targetError = 0;

      if (controller != null)
      {
        // Take target error from previously calculated trajectory
        // We would know this at the end but can't wait until then
        targetError = controller.targetError;
      }

      double dt = 8; // above atmosphere
      double initialY = y;
      
      while ((y > tgtAlt) && (T < maxT))
      {
        y = r.magnitude - body.Radius;
        if (y < 60000) // TODO: change for different planets
          dt = 1;
        //if (y < 10000)
        //  dt = 0.5;
        // Get all forces, i.e. aero-dynamic and thrust
        Vector3d vel_air;
        Vector3d steer;
        double throttle;
        double aeroFudgeFactor = 1.2; // Assume aero forces 20% higher which causes overshoot of target and more vertical final descent
        Vector3d out_r;
        Vector3d out_v;
        EulerStep(dt, vessel, r, v, att, totalMass, minThrust, maxThrust, aeroModel, body, T, controller, tgt_r, aeroFudgeFactor, out steer, out vel_air, out throttle, out out_r, out out_v);
        r = out_r;
        lastv = v;
        v = out_v;
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
          f.WriteLine(string.Format("{0} {1:F5} {2:F5} {3:F5} {4:F5} {5:F5} {6:F5} {7:F1} {8:F1} {9:F1} 0 {10:F2} {11:F2}", T + timeOffset, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, ta.x, ta.y, ta.z, targetError, totalMass));
        }

        T = T + dt;
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

      // Compensate for body rotation giving world position in the surface point now
      // that would be hit in the future
      ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
      bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
      r = bodyRotation * r ;
      return r + body.position;
    }
    */

    static public Vector3d ToGroundVariableStep(double tgtAlt, Vessel vessel, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, BLController controller,
      Vector3d tgt_r, out double T, string logFilename = "", Transform logTransform = null, double timeOffset = 0, double maxT = 600)
    // Changes step size, dt, based on the amount of deacceleration forces, aero or thrust and winds back to choose smaller timesteps
    {
      float ang;
      Quaternion bodyRotation;
      System.IO.StreamWriter f = null;
      if (logFilename != "")
      {
        f = new System.IO.StreamWriter(logFilename);
        f.WriteLine("time x y z vx vy vz ax ay az att_err target_error total_mass");
        f.WriteLine("# tgtAlt=" + tgtAlt);
      }

      T = 0;
      Vector3d r = vessel.GetWorldPos3D() - body.position;
      Vector3d v = vessel.GetObtVelocity();
      Vector3d a = Vector3d.zero;
      Vector3d last_r = r;
      Vector3d last_v = v;
      BLControllerPhase last_phase = controller.phase;
      double minThrust, maxThrust;
      double totalMass = vessel.totalMass;
      // Initially thrust is for all operational engines
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      double y = r.magnitude - body.Radius;
      // TODO: att should be supplied as vessel transform will be wrong in simulation
      Vector3d att = new Vector3d(vessel.transform.up.x, vessel.transform.up.y, vessel.transform.up.z);
      double targetError = 0;

      if (controller != null)
      {
        // Take target error from previously calculated trajectory
        // We would know this at the end but can't wait until then
        targetError = controller.targetError;
      }

      double dt = 8; // above atmosphere
      double last_T = T;

      while ((y > tgtAlt) && (T < maxT))
      {
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
          f.WriteLine(string.Format("{0} {1:F5} {2:F5} {3:F5} {4:F5} {5:F5} {6:F5} {7:F1} {8:F1} {9:F1} 0 {10:F2} {11:F2}", T + timeOffset, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, ta.x, ta.y, ta.z, targetError, totalMass));
        }

        y = r.magnitude - body.Radius;
        if ((y < body.atmosphereDepth) || (y < controller.reentryBurnAlt + 1500*dt))
          dt = 2;
        Vector3d vel_air;
        Vector3d steer;
        double throttle;
        double aeroFudgeFactor = 1.2; // Assume aero forces 20% higher which causes overshoot of target and more vertical final descent
        Vector3d out_r;
        Vector3d out_v;
        // Compute time step change in r and v
        EulerStep(dt, vessel, r, v, att, totalMass, minThrust, maxThrust, aeroModel, body, T, controller, tgt_r, aeroFudgeFactor, out steer, out vel_air, out throttle, out out_r, out out_v);

        /*
        if (throttle > 0.01)
        {
          if (dt > 1)
          {
            // wind back and recalculate
            T = last_T;
            r = last_r;
            v = last_v;
            // Repeat last time step with smaller dt
            //Debug.Log("[BoosterGuidance] simulate repeating last time step");
            // Reset controller?
            controller.phase = last_phase;
            dt = 1;
            EulerStep(dt, vessel, r, v, att, totalMass, minThrust, maxThrust, aeroModel, body, T, controller, tgt_r, aeroFudgeFactor, out steer, out vel_air, out throttle, out out_r, out out_v);
            r = out_r;
            v = out_v;
            T = T + dt;
            // Now run this time step with smaller dt
            EulerStep(dt, vessel, r, v, att, totalMass, minThrust, maxThrust, aeroModel, body, T, controller, tgt_r, aeroFudgeFactor, out steer, out vel_air, out throttle, out out_r, out out_v);
          }
          dt = 1; // keeps dt=1 when throttle is applied
        }
        else
        {
          dt = Math.Min(8, dt * 2); // raise dt again
        }
        */
        
        y = r.magnitude - body.Radius;
        //Debug.Log("[BoosterGuidance] simulate y=" + y + " T=" + T + " dt=" + dt + " throttle="+throttle+" vel_air="+vel_air.magnitude);

        att = steer; // assume can turn immediately
        
        last_phase = controller.phase;
        last_r = r;
        last_v = v;
        last_T = T;
        r = out_r;
        v = out_v;

        T = T + dt;
      }
      if (T > maxT)
        Debug.Log("[BoosterGuidance] Simulation time exceeds maxT=" + maxT);

      // Correct to point of intersect on surface
      double vy = Vector3d.Dot(last_v, Vector3d.Normalize(r));
      double p = 0;
      if (vy < -0.1)
      {
        p = (tgtAlt - y) / -vy; // Backup proportion
        r = r - last_v * p;
        T = T - p;
      }
      if (f != null)
        f.Close();

      //Debug.Log("[BoosterGuidance] simulate() final_y=" + (r.magnitude - body.Radius)+" p="+p+" v(mag)="+lastv.magnitude+" vy="+vy);

      //Debug.Log("[BoosterGuidance] simulate() final_y=" + (r.magnitude - body.Radius)+ " lastv(mag)="+lastv.magnitude);

      // Compensate for body rotation giving world position in the surface point now
      // that would be hit in the future
      ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
      bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
      r = bodyRotation * r;
      return r + body.position;
    }

    // Simulate trajectory to ground and work out point to fire landing burn assuming air resistance will help slow the vessel down
    // This point will be MUCH later than thrust would be applied minus air resistance
    // Height is used to mean the height above the target altitude
    static public double CalculateLandingBurnHeight(double tgtAlt, Vector3d wr, Vector3d v, Vessel vessel, double totalMass, double minThrust, double maxThrust, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body,
      BLController controller=null, double maxT = 600, string filename="")
    {
      double T = 0;
      Vector3d a = Vector3d.zero;
      Vector3d r = wr - body.position;
      double y = r.magnitude - body.Radius;
      double amin = minThrust / totalMass;
      double amax = maxThrust / totalMass;
      double LandingBurnHeight = -1;
      Vector3d att = -Vector3d.Normalize(v);

      double suicideFactor = 0.8;
      double touchdownSpeed = 1;

      System.IO.StreamWriter f = null;
      if (filename != "")
      {
        f = new System.IO.StreamWriter(filename);
        f.WriteLine("time y vy dvy");
      }

      double dt = 4; // was 0.5
      while ((y > tgtAlt) && (T < maxT))
      {
        y = r.magnitude - body.Radius;

        // Get all forces, i.e. aero-dynamic and thrust
        Vector3d steer, vel_air;
        double throttle;
        double aeroFudgeFactor = 1;
        // Need to simulate reentry burn to get reduced mass and less velocity
        // could probably approximation this well without much effort though
        Vector3d F = GetForces(vessel, r, v, -Vector3d.Normalize(v), totalMass, minThrust, maxThrust, aeroModel, body, T, dt, null, Vector3d.zero, aeroFudgeFactor, out steer, out vel_air, out throttle);

        double R = r.magnitude;
        Vector3d g = r * (-body.gravParameter / (R * R * R));

        // Calculate suicide burn velocity
        //Debug.Log("[BoosterGuidance g=" + g);
        double av = amax - g.magnitude;
        if (av < 0)
          av = 0.1; // pretend we have more thrust to look like we are doing something rather than giving up!!
        // dvy in 2 seconds time (allowing time for engine start up)
        double dvy = Math.Sqrt((1 + suicideFactor) * av * (y - tgtAlt)) + touchdownSpeed;

        // Find latest point when velocity is less than desired velocity
        // as it means it is too high in the next time step meaning this is the time to
        // apply landing burn thrust
        if (dvy > vel_air.magnitude)
          LandingBurnHeight = y - tgtAlt;
        if (f != null)
          f.WriteLine(string.Format("{0} {1:F1} {2:F1} {3:F1}", T, y, vel_air.magnitude, dvy));

        // Equations of motion
        r = r + v * dt;
        v = v + (F / totalMass) * dt;

        T = T + dt;
      }
      if (T > maxT)
        Debug.Log("[BoosterGuidance] Simulation time exceeds maxT=" + maxT);
      if (f != null)
      {
        f.WriteLine("# LandingBurnHeight=" + LandingBurnHeight + " amax="+amax);
        f.Close();
      }
      return LandingBurnHeight;
    }
  }
}
