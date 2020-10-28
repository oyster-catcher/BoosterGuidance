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

    protected int ComputeMinMaxThrust(Vessel vessel, out double minThrust, out double maxThrust, bool log = false)
    {


      allEngines.Clear();
      int numEngines = 0;
      minThrust = 0;
      maxThrust = 0;
      foreach (Part part in vessel.parts)
      {
        if (log)
          Debug.Log("part=" + part);
        part.isEngine(out List<ModuleEngines> engines);
        foreach (ModuleEngines engine in engines)
        {
          Vector3d relpos = vessel.transform.InverseTransformPoint(part.transform.position);
          float isp = (engine.realIsp > 0) ? engine.realIsp : 280; // guess!
          float pressure = (float)FlightGlobals.getStaticPressure() * 0.01f; // so 1.0 at Kerbin sea level?
          float atmMaxThrust = engine.MaxThrustOutputAtm(true, true, pressure, FlightGlobals.getExternalTemperature());
          if (log)
            Debug.Log("  engine=" + engine + " relpos=" + relpos + " isp=" + isp + " MinThrust=" + engine.GetEngineThrust(isp, 0) + " MaxThrust=" + atmMaxThrust + " operational=" + engine.isOperational);
          if (engine.isOperational)
          {
            minThrust += engine.GetEngineThrust(isp, 0); // can't get atmMinThrust (this ignore throttle limiting but thats ok)
            maxThrust += atmMaxThrust; // this uses throttle limiting and should give vac thrust as pressure/temp specified too
            allEngines.Add(engine);
            numEngines++;
          }
        }
      }
      return numEngines;
    }

    virtual public double GetCurrentThrust()
    {
      double thrust = 0;
      foreach (ModuleEngines engine in allEngines)
        thrust += engine.GetCurrentThrust();
      return thrust;
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
