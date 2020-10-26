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
    public double targetT = 0; // Time to land
    public double targetError = 0; // Distance from target to landing point in m
    public double attitudeError = 0; // Error in desired attitude in degrees

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

    protected int ComputeMinMaxThrust(Vessel vessel, out double minThrust, out double maxThrust, bool log=false)
    {
      List<ModuleEngines> allEngines = new List<ModuleEngines>();

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
  }
}
