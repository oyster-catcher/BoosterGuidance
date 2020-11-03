// Booster Landing Controller
//   - does boostback, coasting, re-entry burn, and final descent

using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace BoosterGuidance
{
  public enum BLControllerPhase
  {
    Unset,
    BoostBack,
    Coasting,
    ReentryBurn,
    AeroDescent,
    PoweredDescent
  }

  public class BLController : Controller
  {
    // Public parameters
    public float touchdownSpeed = 1.5f;
    public double poweredDescentAlt = 5000; // Powered descent below this altitude
    public double aeroDescentAlt = 60000; // Aerodynamic descent below this altitude
    public double reentryBurnAlt = 70000;
    public double reentryBurnTargetSpeed = 700;
    public double reentryBurnMaxAoA = 0;
    public double aeroDescentMaxAoA = 0;
    public double poweredDescentMaxAoA = 0;
    public double suicideFactor = 0.8;
    public bool noCorrect = false;

    // Private parameters
    private double minError = float.MaxValue;
    private double reentryBurnSteerGain = 0.0001;
    private double steerGain = 0.0008;
    private double poweredSteerGain = 0.0003;
    private System.IO.StreamWriter fp = null;
    private double logStartTime;
    private double logLastTime = 0;
    private double logInterval = 0.1;
    private Transform logTransform;
    private double noSteerAlt = 300; // Don't steer once < 300m
    private Trajectories.VesselAerodynamicModel aeroModel = null;

    // Outputs
    public Vector3d predWorldPos = Vector3d.zero;
    public BLControllerPhase phase = BLControllerPhase.BoostBack;

    // Cache previous values - only calculate new at log interval
    private double lastt = 0;
    private double lastThrottle = 0;
    private Vector3d lastSteer = Vector3d.zero;

    public BLController(Vessel vessel)
    {
       aeroModel = Trajectories.AerodynamicModelFactory.GetModel(vessel, vessel.mainBody);
    }

    ~BLController()
    {
      StopLogging();
    }

    public BLController(BLController v)
    {
      tgtLatitude = v.tgtLatitude;
      tgtLongitude = v.tgtLongitude;
      tgtAlt = v.tgtAlt;
      reentryBurnAlt = v.reentryBurnAlt;
      reentryBurnMaxAoA = v.reentryBurnMaxAoA;
      reentryBurnSteerGain = v.reentryBurnSteerGain;
      reentryBurnTargetSpeed = v.reentryBurnTargetSpeed;
      poweredDescentAlt = v.poweredDescentAlt;
      poweredDescentMaxAoA = v.poweredDescentMaxAoA;
      aeroModel = v.aeroModel;
      aeroDescentAlt = v.aeroDescentAlt;
      aeroDescentMaxAoA = v.aeroDescentMaxAoA;
      suicideFactor = v.suicideFactor;
    }

    public override bool Set(string k, string v)
    {
      if (k == "poweredDescentAlt")
      {
        poweredDescentAlt = Convert.ToDouble(v);
        return true;
      }
      if (k == "aeroDescentAlt")
      {
        aeroDescentAlt = Convert.ToDouble(v);
        return true;
      }
      return false;
    }

    public void SetPhase(BLControllerPhase a_phase)
    {
      minError = float.MaxValue; // reset so boostback doesn't give up
      phase = a_phase;
    }

    public override string PhaseStr()
    {
      if (phase == BLControllerPhase.BoostBack)
        return "Boostback";
      if (phase == BLControllerPhase.Coasting)
        return "Coasting";
      if (phase == BLControllerPhase.ReentryBurn)
        return "Re-entry Burn";
      if (phase == BLControllerPhase.AeroDescent)
        return "Aero Descent";
      if (phase == BLControllerPhase.PoweredDescent)
        return "Powered Descent";
      return "n/a";
    }

    public void StartLogging(string filename, Transform transform)
    {
      if (filename != "")
      {
        logTransform = transform;
        fp = new System.IO.StreamWriter(filename);
        fp.WriteLine("time phase x y z vx vy vz ax ay az att_err amin amax Fhor_aero Fhor_thrust");
      }
    }

    public void StopLogging()
    {
      if (fp != null)
        fp.Close();
      fp = null;
    }

    // Vector adjustment to steer vector (must be descending)
    // If gain is positive steer aerodynamically (towards target)
    // otherwise steer to fire thrust in opposite direction
    private Vector3d GetSteerAdjust(Vector3d tgtError, double gain, double maxAoA)
    {
      Vector3d adj = tgtError * gain;
      double maxAdj = Math.Sin(maxAoA * Mathf.PI / 180);
      if (adj.magnitude > maxAdj)
        adj = Vector3d.Normalize(adj) * maxAdj;
      return adj;
    }

    public override void GetControlOutputs(
                    Vessel vessel,
                    Vector3d r, // world pos
                    Vector3d v, // world velocity
                    Vector3d att, // attitude
                    double alt, // altitude
                    double amin, double amax,
                    double t,
                    CelestialBody body,
                    Vector3d tgt_r, // target in world co-ordinates
                    out double throttle, out Vector3d steer,
                    out bool shutdownEnginesNow,
                    bool log=false)
    {
      double y = alt - tgtAlt;
      Vector3d up = Vector3d.Normalize(r - body.position);
      Vector3d vel_air = v - body.getRFrmVel(r);
      double vy = Vector3d.Dot(vel_air, up); // TODO - Or vel_air?
      double throttleGain = 7.0 / (amax - amin); // correct 1 m/s error in ~0.15 second
      float minThrottle = (amin > 0) ? 0.01f : 0; // assume RO and limited ignitions if limited throttling
      targetError = 0;

      if (t < lastt + logInterval)
      {
        steer = lastSteer;
        throttle = lastThrottle;
      }

      // No thrust - retrograde relative to surface (default and Coasting phase
      shutdownEnginesNow = false;
      throttle = 0;
      steer = -Vector3d.Normalize(vel_air);

      Vector3d error = Vector3d.zero;
      predWorldPos = Vector3d.zero;
      attitudeError = 0;
      if (!noCorrect)
      {
        BLController tc = new BLController(this);
        tc.noCorrect = true;
        // Only simulate phases beyond boostback to boostback minimizes error and simulate includes just
        // the remaining phases and doesn't try to redo reentry burn for instance
        if (phase == BLControllerPhase.BoostBack)
          tc.phase = BLControllerPhase.Coasting;
        predWorldPos = Simulate.ToGround(tgtAlt, vessel, aeroModel, body, tc, tgt_r, out targetT);
        tgt_r = body.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);
        error = predWorldPos - tgt_r;
        attitudeError = 0;
      }
      targetError = error.magnitude;

      // BOOSTBACK
      if (phase == BLControllerPhase.BoostBack)
      {
        // Aim to close max of 20% of error in 1 second
        steer = -Vector3d.Normalize(error);
        attitudeError = Math.Acos(Vector3d.Dot(att, steer)) * 180 / Math.PI;
        double dv = error.magnitude / targetT; // estimated delta V needed
        double ba = 0;
        if (attitudeError < 5+dv*0.5) // more accuracy needed when close to target
          ba = Math.Max(0.3 * dv, 10 / targetT);
        throttle = Mathf.Clamp((float)((ba - amin) / (amax - amin)), minThrottle, 1);
        // Stop if error has grown significantly
        if ((targetError > minError * 1.5) || (targetError < 10))
        {
          if (targetError < 5000) // check if error changes dramatically but still far from target
            phase = BLControllerPhase.Coasting;
        }
        minError = Math.Min(targetError, minError);
        if ((y < reentryBurnAlt) && (vy < 0))
          phase = BLControllerPhase.ReentryBurn;
      }

      // COASTING
      if (phase == BLControllerPhase.Coasting)
      {
        if (y < reentryBurnAlt)
          phase = BLControllerPhase.ReentryBurn;
      }

      // Required adjustment to AoA in AERO DESCENT and POWERED DESCENT BURN
      Vector3d adj = error * steerGain;
      double maxAdj = Math.Sin(reentryBurnMaxAoA * Mathf.PI / 180);
      if (adj.magnitude > maxAdj)
        adj = Vector3d.Normalize(adj) * maxAdj;

      // RE-ENTRY BURN
      if (phase == BLControllerPhase.ReentryBurn)
      {
        steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, -reentryBurnSteerGain, reentryBurnMaxAoA);
        if (vel_air.magnitude > reentryBurnTargetSpeed)
          throttle = HGUtils.LinearMap((float)y, (float)reentryBurnAlt, (float)reentryBurnAlt - 2000, 0, 1);
        else
          phase = BLControllerPhase.AeroDescent;
      }

      // Desired speed in POWERED DESCENT
      double g = FlightGlobals.getGeeForceAtPosition(r).magnitude;
      double av = amax - g;
      if (av < 0)
        av = 0.1; // pretend we have more thrust to look like we are doing something rather than giving up!!
      double dvy = -touchdownSpeed;
      if (y > 0)
        dvy = -Math.Sqrt((1 + suicideFactor) * av * y) - touchdownSpeed; // Factor is 2 for perfect suicide burn, lower for margin and hor vel
      else
        dvy = -touchdownSpeed;
 

      // AERO DESCENT
      if (phase == BLControllerPhase.AeroDescent)
      {
        steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, steerGain, aeroDescentMaxAoA);
        float ddot = (float)Vector3d.Dot(Vector3d.Normalize(att), Vector3d.Normalize(steer));
        double att_err = Mathf.Acos(ddot) * 180 / Mathf.PI;

        //Debug.Log("[BoosterGuidance] Check y=" + y + " vy=" + vy + " dvy=" + dvy+" av="+av+" suicideFactor="+suicideFactor);
        if (vy < dvy) // Going too fast for suicide burn
        {
          phase = BLControllerPhase.PoweredDescent;
        }
      }

      // POWERED DESCENT (suicide burn)
      double sideFA = 0;
      double sideFT = 0;
      if (phase == BLControllerPhase.PoweredDescent)
      {
        throttle = HGUtils.Clamp((dvy - vy) * throttleGain + (g - amin) / (amax - amin), minThrottle, 1);
        //Debug.Log("[BoosterGuidance] y=" + y + " vy=" + vy + " dvy=" + dvy + " throttle=" + throttle);

        // No steering
        steer = Vector3d.Normalize(20*up - vel_air); // retrograde surface + upright component for damping

        if (!noCorrect)
        {
          // Which way to steer? Compare aerodynamic lift vs thrust from engines to give sideways force
          Vector3d Faero = aeroModel.GetForces(body, r, vel_air, Math.PI); // 0 degrees (retrograde)
          Vector3d FsideAero = aeroModel.GetForces(body, r, vel_air, Math.PI + 5 * Math.PI / 180); // 5 degrees
          sideFA = (FsideAero - FsideAero * Vector3d.Dot(Vector3d.Normalize(Faero), Vector3d.Normalize(Faero))).magnitude; // just leave sideways component
          double thrust = minThrust + throttle * (maxThrust - minThrust);
          sideFT = thrust * Math.Sin(5 * Math.PI / 180); // sideways component of thrust at 5 degrees

          if (sideFT > sideFA)
            // Steer so engine thrust pushes in correct direction (works when going fast?)
            steer = steer + GetSteerAdjust(error, -poweredSteerGain, poweredDescentMaxAoA);
          else
            // Steer aerodynamically as this has more effect
            steer = steer + GetSteerAdjust(error, poweredSteerGain, poweredDescentMaxAoA);
        }

        // Decide to shutdown engines for final touch down? (within 3 secs)
        // Criteria should be if amin maintained with current engines we could not reach next target
        // height
        double minHeight = KSPUtils.MinHeightAtMinThrust(y, vy, amin, g);
        // Criteria for shutting down engines
        // - we could not reach ground at minimum thrust (would ascend)
        // - falling less than 20m/s (otherwise can decide to shutdown engines when still high and travelling fast)
        bool cant_reach_ground = (minHeight > 0) && (vy > -20);

        // Shutdown engines requesting hovering thrust
        if (cant_reach_ground)
          shutdownEnginesNow = true;
      }

      // Logging
      if (fp != null)
      {
        if (t > logLastTime + logInterval)
        {
          if (logStartTime == 0)
            logStartTime = t;
          Vector3d a = att * (amin + throttle * (amax - amin)); // this assumes engine is ignited though
          Vector3d tr = logTransform.InverseTransformPoint(r);
          Vector3d tv = logTransform.InverseTransformVector(vel_air);
          Vector3d ta = logTransform.InverseTransformVector(a);
          fp.WriteLine("{0:F1} {1} {2:F1} {3:F1} {4:F1} {5:F1} {6:F1} {7:F1} {8:F1} {9:F1} {10:F1} {11:F1} {12:F1} {13:F1} {14:F1} {15:F1}", t-logStartTime, phase, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, attitudeError, a.x, a.y, a.z, attitudeError, amin, amax, sideFA, sideFT);
          logLastTime = t;
        }
      }
      lastt = t;
      steer = Vector3d.Normalize(steer);
      // Cache
      lastSteer = steer;
      lastThrottle = throttle;
    }
  }
}
