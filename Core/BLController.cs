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
    public float touchdownSpeed = 1;
    public double poweredDescentAlt = 5000; // Powered descent below this altitude
    public double aeroDescentAlt = 60000; // Aerodynamic descent below this altitude
    public double reentryBurnAlt = 70000;
    public double reentryBurnTargetSpeed = 700;
    public double maxAoA = 10;

    // Private parameters
    private double minError = float.MaxValue;
    private double reentryBurnSteerGain = 0.02;
    private double steerGain = 0.05;
    private System.IO.StreamWriter fp = null;
    private double logStartTime;
    private double logLastTime = 0;
    private double logInterval = 0.1;

    // Outputs
    public Vector3d predWorldPos = Vector3d.zero;
    public BLControllerPhase phase = BLControllerPhase.BoostBack;

    public BLController(Vessel vessel, string logFilename="")
    {
      if (logFilename != "")
      {
        fp = new System.IO.StreamWriter(logFilename);
        fp.WriteLine("time phase x y z vx vy vz ax ay az att_err amin amax");
      }
    }

    ~BLController()
    {
      CloseLog();
    }

    public BLController(BLController v)
    {
      tgtLatitude = v.tgtLatitude;
      tgtLongitude = v.tgtLongitude;
      tgtAlt = v.tgtAlt;
      poweredDescentAlt = v.poweredDescentAlt;
      aeroDescentAlt = v.aeroDescentAlt;
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

    public void CloseLog()
    {
      fp.Close();
      fp = null;
    }

    public override void GetControlOutputs(Vessel vessel,
                    double t,
                    out double throttle, out Vector3d steer)
    {
      CelestialBody body = vessel.mainBody;
      Vector3d r = vessel.GetWorldPos3D();
      Vector3d v = vessel.GetSrfVelocity();
      double y = vessel.altitude - tgtAlt;
      Vector3d up = Vector3d.Normalize(r - body.position);
      double vy = Vector3d.Dot(v, up);
      double minThrust;
      double maxThrust;
      ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      double amin = minThrust / vessel.totalMass;
      double amax = maxThrust / vessel.totalMass;
      double throttleGain = 5.0 / (amax - amin); // correct 1 m/s error in 0.2 second
      float minThrottle = (minThrust > 0) ? 0.01f : 0; // assume RO and limited ignitions if limited throttling
      targetError = 0;

      Vector3d att = new Vector3d(vessel.transform.up.x, vessel.transform.up.y, vessel.transform.up.z);

      // No thrust - retrograde (default and Coasting phase
      throttle = 0;
      steer = -Vector3d.Normalize(v);
      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);

      // TODO: We need to assume no boostback, only powered descent
      BLController tc = new BLController(this);
      tc.aeroDescentAlt = poweredDescentAlt; // No aero descent
      tc.phase = BLControllerPhase.Coasting;
      predWorldPos = Simulate.ToGround(tgtAlt, vessel, body, tc, 2, out targetT);
      Vector3d error = predWorldPos - tgt_r;
      targetError = error.magnitude;
      attitudeError = 0;

      // BOOSTBACK
      if (phase == BLControllerPhase.BoostBack)
      {
        // Aim to close max of 20% of error in 1 second
        steer = -Vector3d.Normalize(error);

        attitudeError = Math.Acos(Vector3d.Dot(att, steer)) * 180 / Math.PI;
        //Debug.Log("Attitude error=" + attitudeError);
        double ba = 0;
        if (attitudeError < 90)
          ba = 0.01 * Math.Max(10, error.magnitude) / Math.Min(targetT, 10);
        throttle = Mathf.Clamp((float)((ba - amin) / (amax - amin)), minThrottle, 1);
        // Stop if error has grown significantly
        if ((targetError > minError * 1.2) || (targetError < 10))
        {
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
      Vector3d adj = error * steerGain * 0.01;
      double maxAdj = Math.Sin(maxAoA * Mathf.PI / 180);
      if (adj.magnitude > maxAdj)
        adj = Vector3d.Normalize(adj) * maxAdj;

      // RE-ENTRY BURN
      if (phase == BLControllerPhase.ReentryBurn)
      {
        // This is like the aerodynamic adjustment but in the opposite direction
        if (y > 50000) // TODO: Consider atmospheric drag
          steer = -Vector3d.Normalize(v) - adj;
        else
          steer = -Vector3d.Normalize(v) + adj;
        if (v.magnitude > reentryBurnTargetSpeed)
          throttle = 1;
        else
          phase = BLControllerPhase.AeroDescent;
      }

      // Desired speed in POWERED DESCENT
      double g = FlightGlobals.getGeeForceAtPosition(r).magnitude;
      double av = maxThrust / vessel.totalMass - g;
      double dvy = -touchdownSpeed;
      if (y > 0)
        dvy = -Math.Sqrt(1.5 * av * y) - touchdownSpeed; // Factor is 2 for perfect suicide burn, lower for margin and hor vel
      else
        dvy = -touchdownSpeed;
 

      // AERO DESCENT
      if (phase == BLControllerPhase.AeroDescent)
      {
        steer = -Vector3d.Normalize(v) + adj;
        float ddot = (float)Vector3d.Dot(Vector3d.Normalize(att), Vector3d.Normalize(steer));
        double att_err = Mathf.Acos(ddot) * 180 / Mathf.PI;

        if (vy < dvy) // Going too fast for suicide burn
          phase = BLControllerPhase.PoweredDescent;
      }

      // POWERED DESCENT (suicide burn)
      if (phase == BLControllerPhase.PoweredDescent)
      {
        // Aero-dynamically steer until velocity too low
        if (vy < 0)
        {
          if (v.magnitude > 200)
            // Aero-dynamic steer
            steer = -Vector3d.Normalize(v) + adj;
          else
            // Just cancel velocity but ensure damped by keeping upright
            steer = Vector3d.Normalize(5 * up - Vector3d.Normalize(v) - adj);
        }

         // dv based on time to impact
        throttle = HGUtils.Clamp((dvy - vy) * throttleGain + (g - amin) / (amax - amin), minThrottle, 1);
        // TODO: Need to shutdown engines if thrust too highdvy
        Debug.Log("[BoosterGuidance] t="+t+" y="+y+" dvy="+dvy+" vy="+vy+" throttle="+throttle);
      }

      // Logging
      if (fp != null)
      {
        if (t > logLastTime + logInterval)
        {
          if (logStartTime == 0)
            logStartTime = t;
          double x = ((r - tgt_r) - Vector3d.Dot(r - tgt_r, up) * up).magnitude; // remove vertical component to find downrange distance
          Vector3d a = base.GetCurrentThrust() * att / vessel.totalMass;
          fp.WriteLine("{0:F1} {1} {2:F1} {3:F1} {4:F1} {5:F1} {6:F1} {7:F1} {8:F1} {9:F1} {10:F1} {11:F1} {12:F1} {13:F1}", t-logStartTime, phase, x, y, 0, 0, v.magnitude, 0, a.x, a.y, a.z, attitudeError, amin, amax);
          logLastTime = t;
        }
      }
    }
  }
}
