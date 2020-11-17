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
    public float touchdownSpeed = 3;
    public double poweredDescentAlt = 5000; // Powered descent below this altitude
    public double aeroDescentSteerKp = 0.01f;
    public double aeroDescentAlt = 60000; // Aerodynamic descent below this altitude
    public double reentryBurnAlt = 70000;
    public double reentryBurnTargetSpeed = 700;
    public double reentryBurnMaxAoA = 10;
    public double poweredDescentMaxAoA = 10;
    public double suicideFactor = 0.8;
    public bool noCorrect = false;
    public double lowestY = 0;
    public double transitionToThrustSteer = 600; // Steer via thrust under this speed
    public double reentryBurnSteerGain = 0.1;
    public PIDclamp pid_aero = new PIDclamp("aeroSteer", 1, 0, 0, 10);
    public PIDclamp pid_powered = new PIDclamp("poweredSteer", 1, 0, 0, 10);

    // Private parameters
    private double minError = float.MaxValue;
    private System.IO.StreamWriter fp = null;
    private double logStartTime;
    private double logLastTime = 0;
    private double logInterval = 0.1;
    private Transform logTransform;
    private double noSteerAlt = 50; // Don't steer once < 50m
    private Trajectories.VesselAerodynamicModel aeroModel = null;
    //private bool deployGears = true;
    private double touchdownMargin = 20;
    private double liftFactor = 10;
    private double steerDeadZoneGain = 0.008;

    // Outputs
    public Vector3d predWorldPos = Vector3d.zero;
    public BLControllerPhase phase = BLControllerPhase.Unset;
    double steerGain = 0;

    // Cache previous values - only calculate new at log interval
    private double lastt = 0;
    private double lastThrottle = 0;
    private Vector3d lastSteer = Vector3d.zero;

    public BLController()
    {
    }

    public void AttachVessel(Vessel a_vessel)
    {
      vessel = a_vessel;
      aeroModel = Trajectories.AerodynamicModelFactory.GetModel(vessel, vessel.mainBody);
      lowestY = KSPUtils.FindLowestPointOnVessel(vessel);
    }

    ~BLController()
    {
      StopLogging();
    }

    public BLController(BLController v)
    {
      phase = v.phase;
      vessel = v.vessel;
      tgtLatitude = v.tgtLatitude;
      tgtLongitude = v.tgtLongitude;
      tgtAlt = v.tgtAlt;
      lowestY = v.lowestY;
      reentryBurnAlt = v.reentryBurnAlt;
      reentryBurnMaxAoA = v.reentryBurnMaxAoA;
      reentryBurnSteerGain = v.reentryBurnSteerGain;
      reentryBurnTargetSpeed = v.reentryBurnTargetSpeed;
      poweredDescentAlt = v.poweredDescentAlt;
      aeroModel = v.aeroModel;
      aeroDescentSteerKp = v.aeroDescentSteerKp;
      aeroDescentAlt = v.aeroDescentAlt;
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
      if ((filename != "") && (fp == null))
      {
        logTransform = transform;
        fp = new System.IO.StreamWriter(filename);
        fp.WriteLine("time phase x y z vx vy vz ax ay az att_err amin amax steer_gain target_error");
      }
    }

    public void StopLogging()
    {
      if (fp != null)
      {
        Debug.Log("[BoosterGuidance] StopLogging()");
        fp.Close();
      }
      fp = null;
    }

    // Vector adjustment to steer vector (must be descending)
    // If gain is positive steer aerodynamically (towards target)
    // otherwise steer to fire thrust in opposite direction
    private Vector3d GetSteerAdjust(Vector3d tgtError, double gain, double maxAoA)
    {
      Vector3d adj = tgtError * gain * Math.PI / 180;
      double maxAdj = maxAoA / 45.0; // approx as 45 degress sideways component to unit vector is 1
      if (adj.magnitude > maxAdj)
        adj = Vector3d.Normalize(adj) * maxAdj;
      return adj;
    }

    private Vector3d GetSteerAdjust(Vector3d tgtError, double ang)
    {
      return Vector3d.Normalize(tgtError) * ang / 45.0;
    }

    // liftFactor is the proportion of the atmospheric drag which is turned into lift at an angle-of-attack of 45 degrees
    // a rough estimation is that is 50%. Perhaps it should always be 50%?
    private double CalculateSteerGain(double throttle, Vector3d vel_air, Vector3d r, double y)
    {
      Vector3d Faero = aeroModel.GetForces(vessel.mainBody, r, vel_air, Math.PI); // 180 degrees (retrograde);
      double sideFA = liftFactor * Faero.magnitude; // liftFactor proportion of drag is lift force available by aerodynamic steering at 45 degrees
      double thrust = minThrust + throttle * (maxThrust - minThrust);
      double sideFT = thrust * Math.Sin(45 * Math.PI / 180); // sideways component of thrust at 45 degrees
      double gain = 0;
      // When its a toss up whether thrust or aerodynamic steering is better gain can become very high or infinite
      // Dont steer in the region since the gain estimate might also have the wrong sign
      if (Math.Abs(sideFA - sideFT) > 0)
        gain = vessel.totalMass / (sideFA - sideFT);
      if (Math.Abs(gain) > steerDeadZoneGain)
        gain = 0;
      //if (!noCorrect)
      //  Debug.Log("[BoosterGuidance] "+ phase + " y=" + y + " vy=" + (vel_air.magnitude) + " sideFT=" + sideFT + " sideFA=" + sideFA + " throttle=" + throttle + " gain=" + gain + "liftFactor=" + liftFactor);
      // Should vary between 1 = max aero dynamic steering (fast), and -1 = max thrust steering (slow)
      return gain;
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
      double y = alt - tgtAlt - touchdownMargin + lowestY;
      Vector3d up = Vector3d.Normalize(r - body.position);
      Vector3d vel_air = v - body.getRFrmVel(r);
      double vy = Vector3d.Dot(vel_air, up); // TODO - Or vel_air?
      double throttleGain = 0;
      //float minThrottle = (amin > 0) ? 0.01f : 0; // assume RO and limited ignitions if limited throttling
      float minThrottle = 0.01f;
      targetError = 0;

      if (amax > 0) // check in case out of fuel or something
        throttleGain = 50.0 / (amax - amin); // cancel velocity error in 0.02 secs

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
        // Only simulate phases beyond boostback so boostback minimizes error and simulate includes just
        // the remaining phases and doesn't try to redo reentry burn for instance
        if (phase == BLControllerPhase.BoostBack)
          tc.phase = BLControllerPhase.Coasting;
        predWorldPos = Simulate.ToGround(tgtAlt - lowestY, vessel, aeroModel, body, tc, tgt_r, out targetT);
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
        if ((y < reentryBurnAlt) && (vy < 0)) // falling
          phase = BLControllerPhase.ReentryBurn;
      }

      // COASTING
      if (phase == BLControllerPhase.Coasting)
      {
        if ((y < reentryBurnAlt) && (vy < 0))
          phase = BLControllerPhase.ReentryBurn;
      }

      // Set default gains for steering
      double gain = 0;
      steerGain = 0;

      // RE-ENTRY BURN
      if (phase == BLControllerPhase.ReentryBurn)
      {
        steerGain = -reentryBurnSteerGain; // override as gain is wrong at high speeds (always steer with thrust)
        steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, steerGain, reentryBurnMaxAoA);
        if (vel_air.magnitude > reentryBurnTargetSpeed)
        {
          throttle = HGUtils.LinearMap((float)y, (float)reentryBurnAlt, (float)reentryBurnAlt - 2000, 0, 1);
          if (amax > 50)
            throttle = 50 / amax; // reduce throttle for high thrust engines to 50m/s/s2
          if (!noCorrect)
            gain = reentryBurnSteerGain * CalculateSteerGain(throttle, vel_air, r, y);
        }
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
        //aeroDescentSteerGain = CalculateSteerGain(0, vel_air, r, y)
        //gain = poweredDescentSteerGain * steerGain;
        pid_aero.kp = aeroDescentSteerKp * CalculateSteerGain(0, vel_air, r, y);
        steerGain = CalculateSteerGain(0, vel_air, r, y);
        double ang = pid_aero.Update(error.magnitude, Time.deltaTime);
        //steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, gain, aeroDescentMaxAoA);
        steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, ang);
        float ddot = (float)Vector3d.Dot(Vector3d.Normalize(att), Vector3d.Normalize(steer));
        double att_err = Mathf.Acos(ddot) * 180 / Mathf.PI;

        if (vy < dvy) // Going too fast for suicide burn
        {
          phase = BLControllerPhase.PoweredDescent;
        }
      }

      // POWERED DESCENT (suicide burn)
      if (phase == BLControllerPhase.PoweredDescent)
      {
        if (amax > 0)
        {
          double err_dv = vy - dvy; // +ve is velocity too high
          double da = g - (20 * err_dv); // required accel to change vy, cancel out g (only works if vertical)
          throttle = (da - amin) / (amax - amin);
          throttle = HGUtils.Clamp(throttle, minThrottle, 1);
          //if (!noCorrect)
          //  Debug.Log("[BoosterGuidance] y=" + y + " vy=" + vy + " dvy=" + dvy + " err_dv=" + err_dv + " da=" + da + " throttle=" + throttle);
        }
        if ((!noCorrect) && (alt > noSteerAlt))
        {
          //gain = poweredDescentSteerGain * CalculateSteerGain(throttle, vel_air, r, y);
          pid_powered.kp = aeroDescentSteerKp * CalculateSteerGain(throttle, vel_air, r, y);
          steerGain = CalculateSteerGain(throttle, vel_air, r, y);
          double ang = pid_powered.Update(error.magnitude, Time.deltaTime);
          //steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, gain, aeroDescentMaxAoA);
          steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, ang);
          // Steer retrograde with added up component to damp oscillations at slow speed near ground
          //steer = -Vector3d.Normalize(vel_air) + GetSteerAdjust(error, gain, poweredDescentMaxAoA);
          steer = -Vector3d.Normalize(vel_air - 20*up) + GetSteerAdjust(error, ang);
        }
        else
        {
          // Just cancel velocity with significant upwards component to stay upright
          steer = -Vector3d.Normalize(vel_air - 40 * up);
          //Debug.Log("[BoosterGuidance] y=" + y + " vel_air=" + vel_air + " up=" + up + " steer=" + steer);
        }
    
        // Decide to shutdown engines for final touch down? (within 3 secs)
        // Criteria should be if
        // height
        double minHeight = KSPUtils.MinHeightAtMinThrust(y, vy, amin, g);
        // Criteria for shutting down engines
        // - we could not reach ground at minimum thrust (would ascend)
        // - falling less than 20m/s (otherwise can decide to shutdown engines when still high and travelling fast)
        //Debug.Log("[BoosterGuidance] y=" + y + " minHeight=" + minHeight + " vy=" + vy + " vel_air=" + (vel_air.magnitude)+" amin="+amin+" g="+g);
        bool cant_reach_ground = (minHeight > 0) && (vel_air.magnitude < 20);

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
          fp.WriteLine("{0:F1} {1} {2:F1} {3:F1} {4:F1} {5:F1} {6:F1} {7:F1} {8:F1} {9:F1} {10:F1} {11:F1} {12:F1} {13:F1} {14:F2} {15:F1}", t - logStartTime, phase, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, attitudeError, a.x, a.y, a.z, attitudeError, amin, amax, steerGain, targetError);
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
