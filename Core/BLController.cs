// Booster Landing Controller
//   - does boostback, coasting, re-entry burn, and final descent

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace BoosterGuidance
{
  public class BLController : Controller
  {
    // Parameters
    public float touchdownSpeed = 1;
    public double powerDescentAlt = 5000; // Powered descent below this altitude
    public double aeroDescentAlt = 70000; // Aerodynamic descent below this altitude
    public double boostbackGain = 0.0001;
    public double minError = Double.MaxValue;
    public bool shutdown = false; // shutdown engines once

    public BLController()
    {

    }

    public BLController(BLController v)
    {
      powerDescentAlt = v.powerDescentAlt;
      aeroDescentAlt = v.aeroDescentAlt;
    }

    public bool Set(string k, string v) {
      if (k=="powerDescentAlt")
      {
        powerDescentAlt = Convert.ToDouble(v);
        return true;
      }
      if (k=="aeroDescentAlt")
      {
        aeroDescentAlt = Convert.ToDouble(v);
        return true;
      }
      return false;
    }

    public override void GetControlOutputs(Vessel vessel, Body body,
                    double t,
                    out double throttle, out Vector3d steer)
    {
      double y = vessel.r.y;
      double vy = vessel.v.y;
      double amin = vessel.maxThrust / vessel.totalMass;
      double amax = vessel.maxThrust / vessel.totalMass;


      // No thrust - retrograde
      throttle = 0;
      steer = -Vector3d.Normalize(vessel.v);

      // Powered Descent Phase
      if (y < powerDescentAlt)
      {
        double av = vessel.maxThrust / vessel.totalMass - body.g;
        double dvy = -touchdownSpeed;
        if (y > 0)
        {
          dvy = -Math.Sqrt(1.2*av*y) - touchdownSpeed; // Factor is 2 for perfect suicide burn, lower for margin and hor vel
          // dv based on time to impact
          throttle = HGUtils.Clamp((dvy - vessel.v.y)*0.05,0,1);
          steer = -Vector3d.Normalize(vessel.v);
        }
        else
        {
          throttle = 0;
          steer = Vector3d.up;
        }
        steer = Vector3d.Normalize(steer);
        return;
      }

      // Aero Descent Phase
      if (y < aeroDescentAlt)
      {
        BLController tc1 = new BLController(this);
        tc1.powerDescentAlt = Double.MaxValue;
        tc1.aeroDescentAlt = 0;
        Vessel final1 = Simulate.ToGround(vessel,body,tc1,t,1);
        //System.Console.Error.WriteLine("t="+t+" final_r="+final1.r+ "vessel.r="+vessel.r+" Hit="+(body.Hit(vessel.r)));
        return;
      }
      if (y < aeroDescentAlt)
        return;

      if (shutdown)
        return;
      // Simulate rest of trajectory to find how much thrust to use
      BLController tc = new BLController(this);
      tc.aeroDescentAlt = 1e+20;
      Vessel final = Simulate.ToGround(vessel,body,tc,t,1);
      //System.Console.Error.WriteLine("t="+t+" final_r="+final.r+ "vessel.r="+vessel.r+" Hit="+(body.Hit(vessel.r)));
        
      throttle = HGUtils.Clamp(final.r.magnitude*boostbackGain,0,1);
      steer = new Vector3d(-final.r.x,0,-final.r.z); // horizontal
      if (steer.magnitude > minError * 1.2)
      {
        throttle = 0;
        steer = -vessel.v; // Retrograde
        shutdown = true;
      }
      steer = Vector3d.Normalize(steer);
    }
  }
}
