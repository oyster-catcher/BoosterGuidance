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

    public Vessel vessel = null; // Vessel being controlled
    public double targetT = 0; // Time to land
    public double targetError = 0; // Distance from target to landing point in m
    public double attitudeError = 0; // Error in desired attitude in degrees

    // RO
    //List<ModuleEngines> shutdown = new List<ModuleEngines>();

    public Controller()
    {
    }

    virtual public void GetControlOutputs(Vessel vessel,
               Vector3d r, Vector3d v, Vector3d att,
               double y, double amin, double amax, double t,
               CelestialBody body,
               Vector3d tgt_r,
               out double throttle, out Vector3d steer,
               out bool shutdownOuterEngines,
               bool log=false)
    {
      shutdownOuterEngines = false;
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
  }
}
