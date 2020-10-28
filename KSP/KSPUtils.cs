// Utility functions that depend on KSP

using System;
using System.Collections.Generic;
using UnityEngine;

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

    public static List<ModuleEngines> ComputeMinMaxThrust(Vessel vessel, out double minThrust, out double maxThrust, bool log = false)
    {
      List<ModuleEngines> allEngines = new List<ModuleEngines>();
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
      return allEngines;
    }

    public static double GetCurrentThrust(List<ModuleEngines> allEngines)
    {
      double thrust = 0;
      foreach (ModuleEngines engine in allEngines)
        thrust += engine.GetCurrentThrust();
      return thrust;
    }
  }
}
