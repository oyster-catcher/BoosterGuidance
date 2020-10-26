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
    AeroDescent,
    PoweredDescent
  }

  public class BLController : Controller
  {
    // Parameters
    public float touchdownSpeed = 1;
    public double poweredDescentAlt = 5000; // Powered descent below this altitude
    public double aeroDescentAlt = 70000; // Aerodynamic descent below this altitude
    private double minError = float.MaxValue;
    public Vector3d predWorldPos = Vector3d.zero;
    public bool shutdown = false; // shutdown engines once
    public BLControllerPhase phase = BLControllerPhase.BoostBack;

    public BLController(Vessel vessel)
    {
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
      phase = a_phase;
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
      double vy = Vector3d.Dot(v,up);
      double minThrust;
      double maxThrust;
      ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      double amin = minThrust / vessel.totalMass;
      double amax = maxThrust / vessel.totalMass;
      double throttleGain = 5.0/(amax-amin); // correct 1 m/s error in 0.2 second
      targetError = 0;

      // No thrust - retrograde (default and Coasting phase)
      throttle = 0;
      steer = -Vector3d.Normalize(v);
      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);
      // Find where this will be in the future?

      // TODO: We need to assume no boostback, only powered descent
      BLController tc = new BLController(this);
      tc.aeroDescentAlt = poweredDescentAlt; // No aero descent
      tc.phase = BLControllerPhase.Coasting;
      predWorldPos = Simulate.ToGround(tgtAlt, vessel, body, tc, 2, out targetT);
      Vector3d error = tgt_r - predWorldPos;
      targetError = error.magnitude;

      // BOOSTBACK
      if (phase == BLControllerPhase.BoostBack)
      {
        double ba = 0.003 * error.magnitude;
        // TODO: Consider time to impact
        throttle = Mathf.Clamp((float)((ba - amin) / (amax - amin)), 0, 1);
        // Stop if error has grown significantly
        if (steer.magnitude > minError * 1.2)
        {
          phase = BLControllerPhase.Coasting;
        }
        steer = Vector3d.Normalize(steer);
        if (y < aeroDescentAlt)
          phase = BLControllerPhase.AeroDescent;
      }

      // COASTING
      if (y < aeroDescentAlt)
        phase = BLControllerPhase.AeroDescent;

      // AERO DESCENT
      if (phase == BLControllerPhase.AeroDescent)
      {
        if (y < poweredDescentAlt)
          phase = BLControllerPhase.PoweredDescent;
      }

 
      // POWERED DESCENT
      if (phase == BLControllerPhase.PoweredDescent)
      {
        double g = FlightGlobals.getGeeForceAtPosition(r).magnitude;
        double av = maxThrust / vessel.totalMass - g;
        double dvy = -touchdownSpeed;

        if (y > 0)
          dvy = -Math.Sqrt(1.2 * av * y) - touchdownSpeed; // Factor is 2 for perfect suicide burn, lower for margin and hor vel
        else
          dvy = -touchdownSpeed;
        // dv based on time to impact
        throttle = HGUtils.Clamp((dvy - vy) * throttleGain + (g - amin) / (amax - amin), 0, 1);
        if (vy < 0)
          steer = Vector3d.Normalize(5 * up - v);
        else
          steer = up;

        steer = Vector3d.Normalize(steer);
      }
    }
  }
}
