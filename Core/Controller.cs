using KSP;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoosterGuidance
{
  public class Controller
  {
    protected double minThrust = 0;
    protected double maxThrust = 10000;
    protected double tgtLatitude, tgtLongitude, tgtAlt;
    protected List<ModuleEngines> allEngines = new List<ModuleEngines>();
    public double targetT = 0; // Time to land
    public double targetError = 0; // Distance from target to landing point in m
    public double attitudeError = 0; // Error in desired attitude in degrees

    // RO
    //List<ModuleEngines> shutdown = new List<ModuleEngines>();

    public Controller()
    {
    }

    virtual public void GetControlOutputs(Vessel vessel, double t,
               out double throttle, out Vector3d steer)
    {
      throttle = 0;
      steer = Vector3d.up;
    }

    virtual public string PhaseStr()
    {
      return "n/a";
    }

    virtual public bool Set(string key, string val)
    {
      return false;
    }

    virtual public void SetTarget(double latitude, double longitude, double alt)
    {
      tgtLatitude = latitude;
      tgtLongitude = longitude;
      tgtAlt = alt;
    }

    /*
    // Compute engine thrust if one set of symmetrical engines is shutdown
    // (primarily for a Falcon 9 landing to shutdown engines for slow touchdown)
    public List<ModuleEngines> ShutdownOuterEngines(Vessel vessel, float desiredThrust, bool log = false)
    {
      List<ModuleEngines> shutdown = new List<ModuleEngines>();

      // Find engine parts and sort by closest to centre first
      List<Tuple<double, ModuleEngines>> allEngines = new List<Tuple<double, ModuleEngines>>();
      foreach (Part part in vessel.GetActiveParts())
      {
        Vector3 relpos = vessel.transform.InverseTransformPoint(part.transform.position);
        part.isEngine(out List<ModuleEngines> engines);
        double dist = Math.Sqrt(relpos.x * relpos.x + relpos.z * relpos.z);
        foreach (ModuleEngines engine in engines)
          allEngines.Add(new Tuple<double, ModuleEngines>(dist, engine));
      }
      allEngines.Sort();

      // Loop through engines starting a closest to axis
      // Accumulate minThrust, once minThrust exceeds desiredThrust shutdown this and all
      // further out engines
      float minThrust = 0, maxThrust = 0;
      double shutdownDist = float.MaxValue;
      foreach (var engDist in allEngines)
      {
        ModuleEngines engine = engDist.Item2;
        if (engine.isOperational)
        {
          minThrust += engine.GetEngineThrust(engine.realIsp, 0);
          maxThrust += engine.GetEngineThrust(engine.realIsp, 1);
          if (shutdownDist == float.MaxValue)
          {
            if ((minThrust < desiredThrust) && (desiredThrust < maxThrust)) // good amount of thrust
              shutdownDist = engDist.Item1 + 0.1f;
            if (minThrust > desiredThrust)
              shutdownDist = engDist.Item1 - 0.1f;
          }
        }
        if (engDist.Item1 > shutdownDist)
        {
          engine.Shutdown();
          shutdown.Add(engine);
        }
      }
    }
    return shutdown;
    */
  }
}
