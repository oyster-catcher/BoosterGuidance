// Utility functions that depend on KSP

using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using ModuleWheels;

namespace BoosterGuidance
{
  public class KSPUtils
  {
    // Find Y offset to lowest part from origin of the vessel
    public static double FindLowestPointOnVessel(Vessel vessel)
    {
      Vector3 CoM, up;

      CoM = vessel.localCoM;
      Vector3 bottom = Vector3.zero; // Offset from CoM
      up = FlightGlobals.getUpAxis(CoM); //Gets up axis
      Vector3 pos = vessel.GetWorldPos3D();
      Vector3 distant = pos - 1000 * up; // distant below craft
      double miny = 0;
      foreach (Part p in vessel.parts)
      {
        if (p.collider != null) //Makes sure the part actually has a collider to touch ground
        {
          Vector3 pbottom = p.collider.ClosestPointOnBounds(distant); //Gets the bottom point
          double y = Vector3.Dot(up, pbottom - pos); // relative to centre of vessel
          if (y < miny)
          {
            bottom = pbottom;
            miny = y;
          }
        }
      }
      return miny;
    }

    public static List<ModuleEngines> GetActiveEngines(Vessel vessel)
    {
      List<ModuleEngines> activeEngines = new List<ModuleEngines>();
      foreach (Part part in vessel.parts)
      {
        part.isEngine(out List<ModuleEngines> engines);
        foreach (ModuleEngines engine in engines)
        {
          if (engine.isOperational)
            activeEngines.Add(engine);
        }
      }
      return activeEngines;
    }

    public static List<ModuleEngines> GetAllEngines(Vessel vessel)
    {
      List<ModuleEngines> engines = new List<ModuleEngines>();
      foreach (Part part in vessel.parts)
      {
        part.isEngine(out List<ModuleEngines> partEngines);
        foreach (ModuleEngines engine in partEngines)
          engines.Add(engine);
      }
      return engines;
    }

    public static List<ModuleEngines> GetOperationalEngines(Vessel vessel)
    {
      List<ModuleEngines> opEngines = new List<ModuleEngines>();
      foreach (Part part in vessel.parts)
      {
        part.isEngine(out List<ModuleEngines> partEngines);
        foreach (ModuleEngines engine in partEngines)
          if (engine.isOperational)
            opEngines.Add(engine);
      }
      return opEngines;
    }

    private static void GetEngineMinMaxThrust(ModuleEngines engine, out double minThrust, out double maxThrust, bool log=false)
    {
      float isp = (engine.realIsp > 0) ? engine.realIsp : 280; // guess!
      float pressure = (float)FlightGlobals.getStaticPressure() * 0.01f; // so 1.0 at Kerbin sea level?
      float atmMaxThrust = engine.MaxThrustOutputAtm(true, true, pressure, FlightGlobals.getExternalTemperature());
      minThrust = engine.GetEngineThrust(isp, 0); // can't get atmMinThrust (this ignore throttle limiting but thats ok)
      maxThrust = atmMaxThrust; // this uses throttle limiting and should give vac thrust as pressure/temp specified too
      if (log)
      {
        //Vector3d relpos = vessel.transform.InverseTransformPoint(part.transform.position);
        Debug.Log("  engine=" + engine + " isp=" + isp + " MinThrust=" + engine.GetEngineThrust(isp, 0) + " MaxThrust=" + atmMaxThrust + " operational=" + engine.isOperational);
      }
    }

    public static List<ModuleEngines> ComputeMinMaxThrust(Vessel vessel, out double totalMinThrust, out double totalMaxThrust, bool log = false, List<ModuleEngines> useEngines = null)
    {
      totalMinThrust = 0;
      totalMaxThrust = 0;

      // If no engines specified find all operational engines
      if (useEngines == null)
        useEngines = GetOperationalEngines(vessel);

      foreach(ModuleEngines engine in useEngines)
      {
        double minThrust, maxThrust;
        GetEngineMinMaxThrust(engine, out minThrust, out maxThrust);
        totalMinThrust += minThrust;
        totalMaxThrust += maxThrust;
      }
      return useEngines;
    }

    public static double GetCurrentThrust(List<ModuleEngines> allEngines)
    {
      double thrust = 0;
      foreach (ModuleEngines engine in allEngines)
        thrust += engine.GetCurrentThrust();
      return thrust;
    }

    public static double MinHeightAtMinThrust(double y, double vy, double amin, double g)
    {
      double minHeight = 0;
      if (amin < g)
        return -float.MaxValue;
      double tHover = -vy / amin; // time to come to hover
      minHeight = y + vy * tHover + 0.5 * amin * tHover * tHover - 0.5 * g * tHover * tHover;
      return minHeight;
    }

    static int Closest(KeyValuePair<double, ModuleEngines> a, KeyValuePair<double, ModuleEngines> b)
    {
      return a.Key.CompareTo(b.Key);
    }

    // Compute engine thrust if one set of symmetrical engines is shutdown
    // (primarily for a Falcon 9 landing to shutdown engines for slow touchdown)
    public static List<ModuleEngines> ShutdownOuterEngines(Vessel vessel, float desiredThrust, bool log = false)
    {
      List<ModuleEngines> shutdown = new List<ModuleEngines>();
      Debug.Log("ShutdownOuterEngines desiredThrust=" + desiredThrust + " mass=" + vessel.totalMass);
      // Find engine parts and sort by closest to centre first
      List<KeyValuePair<double, ModuleEngines>> allEngines = new List<KeyValuePair<double, ModuleEngines>>();
      foreach (Part part in vessel.GetActiveParts())
      {
        Vector3 relpos = vessel.transform.InverseTransformPoint(part.transform.position);
        part.isEngine(out List<ModuleEngines> engines);
        double dist = Math.Sqrt(relpos.x * relpos.x + relpos.z * relpos.z);
        foreach (ModuleEngines engine in engines)
          allEngines.Add(new KeyValuePair<double, ModuleEngines>(dist, engine));
      }
      allEngines.Sort(Closest);

      // Loop through engines starting a closest to axis
      // Accumulate minThrust, once minThrust exceeds desiredThrust shutdown this and all
      // further out engines
      float minThrust = 0, maxThrust = 0;
      double shutdownDist = float.MaxValue;
      foreach (var engDist in allEngines)
      {
        ModuleEngines engine = engDist.Value;
        if (engine.isOperational)
        {
          minThrust += engine.GetEngineThrust(engine.realIsp, 0);
          maxThrust += engine.GetEngineThrust(engine.realIsp, 1);
          if (shutdownDist == float.MaxValue)
          {
            if ((minThrust < desiredThrust) && (desiredThrust < maxThrust)) // good amount of thrust
              shutdownDist = engDist.Key + 0.1f;
            if (minThrust > desiredThrust)
              shutdownDist = engDist.Key - 0.1f;
          }

          if (engDist.Key > shutdownDist)
          {
            if (log)
              Debug.Log("[BoosterGuidance] ComputeShutdownMinMaxThrust(): minThrust=" + minThrust + " desiredThrust=" + desiredThrust + " SHUTDOWN");
            engine.Shutdown();
            shutdown.Add(engine);
          }
          else
            if (log)
              Debug.Log("[BoosterGuidance] ComputeShutdownMinMaxThrust(): minThrust=" + minThrust + " desiredThrust=" + desiredThrust + " KEEP");
        }
      }
      Debug.Log(shutdown.Count + " engines shutdown");
      return shutdown;
    }

    public static bool DeployLandingGears(Vessel vessel)
    {
      bool deployed = false;
      for (int i = 0; i < vessel.parts.Count; i++)
      {
        Part p = vessel.parts[i];
        foreach (ModuleWheelDeployment wd in p.FindModulesImplementing<ModuleWheelDeployment>())
        {
          if (wd.fsm.CurrentState == wd.st_retracted || wd.fsm.CurrentState == wd.st_retracting)
          {
            wd.EventToggle();
            deployed = true;
          }
        }
      }
      return deployed;
    }
  }
}
