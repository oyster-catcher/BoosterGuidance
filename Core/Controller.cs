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
    protected List<ModuleEngines> allEngines = new List<ModuleEngines>();

    protected double tgtLatitude, tgtLongitude, tgtAlt;
    // Set with SetTarget()
    public double TgtLatitude  { get { return tgtLatitude; } }
    public double TgtLongitude { get { return tgtLongitude; } }
    public double TgtAlt       { get { return tgtAlt; } }
    public bool enabled = false;
    public Vessel vessel = null; // Vessel being controlled

    // Outputs for reading
    public double targetT = 0; // Time to land
    public double targetError = 0; // Distance from target to landing point in m
    public double attitudeError = 0; // Error in desired attitude in degrees
    public string info; // Information message

    public Controller()
    {
    }

    virtual public string GetControlOutputs(Vessel vessel,
               double totalMass,
               Vector3d r, Vector3d v, Vector3d att,
               double amin, double amax, double t,
               CelestialBody body,
               bool simulate,
               out double throttle, out Vector3d steer,
               out bool landingGear,
               bool bailOutLandingBurn = false,
               bool showCpuTime = false)

    {
      throttle = 0;
      steer = Vector3d.up;
      landingGear = false;
      return "";
    }

    virtual public string PhaseStr()
    {
      return "n/a";
    }

    virtual public bool Set(string key, string val)
    {
      return false;
    }
  }
}
