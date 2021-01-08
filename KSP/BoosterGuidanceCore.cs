using System;
using System.Linq;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using UnityEngine.Profiling;

namespace BoosterGuidance
{
  public class BoosterGuidanceCore : PartModule
  {
    // Saved settings

    [KSPField(isPersistant = true, guiActive = false)]
    public double tgtLatitude = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public double tgtLongitude = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public double tgtAlt = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public double reentryBurnAlt = 55000;

    [KSPField(isPersistant = true, guiActive = false)]
    public double reentryBurnTargetSpeed = 700;

    [KSPField(isPersistant = true, guiActive = false)]
    public float reentryBurnSteerKp = 0.01f;

    [KSPField(isPersistant = true, guiActive = false)]
    public float reentryBurnMaxAoA = 30;

    [KSPField(isPersistant = true, guiActive = false)]
    public float aeroDescentSteerKp = 10;

    [KSPField(isPersistant = true, guiActive = false)]
    public float aeroDescentMaxAoA = 15;

    [KSPField(isPersistant = true, guiActive = false)]
    public float landingBurnSteerKp = 10;

    [KSPField(isPersistant = true, guiActive = false)]
    public float landingBurnMaxAoA = 15;

    [KSPField(isPersistant = true, guiActive = false)]
    public int touchdownMargin = 20;

    [KSPField(isPersistant = true, guiActive = false)]
    public float touchdownSpeed = 2;

    [KSPField(isPersistant = true, guiActive = false)]
    public int noSteerHeight = 200;

    [KSPField(isPersistant = true, guiActive = false)]
    public bool deployLandingGear = true;

    [KSPField(isPersistant = true, guiActive = false)]
    public int deployLandingGearHeight = 500;

    [KSPField(isPersistant = true, guiActive = false)]
    public string landingBurnEngines = "current";

    [KSPField(isPersistant = true, guiActive = false)]
    public float igniteDelay = 3;

    [KSPField(isPersistant = true, guiActive = false)]
    public string phase = "Unset";

    public bool logging = false;
    public bool useFAR = false;
    public string logFilename = "unset";
    private string info = "Disabled";

    // Flight controller with copy of these settings
    BLController controller = null;

    // List of all active controllers
    public static List<BLController> controllers = new List<BLController>();

    public void OnDestroy()
    {
      if (controller != null)
        DisableGuidance();
      Debug.Log("[BoosterGuidance] BoosterGuidanceCore.OnDestroy()");
    }

    // Find first BoosterGuidanceCore module for vessel
    static public BoosterGuidanceCore GetBoosterGuidanceCore(Vessel vessel)
    {
      foreach (var part in vessel.Parts)
      {
        foreach (var mod in part.Modules)
        {
          if (mod.GetType() == typeof(BoosterGuidanceCore))
          {
            Debug.Log("[BoosterGuidance] vessel=" + vessel.name + "part=" + part.name + " module=" + mod.name + " modtype=" + mod.GetType());
            return (BoosterGuidanceCore)mod;
          }
        }
      }
      Debug.Log("[BoosterGuidance] No BoosterGuidanceCore module for vessel " + vessel.name);
      return null;
    }

    public void AttachVessel(Vessel vessel)
    {
      // Sets up Aero forces function again respected useFAR flag
      controller.AttachVessel(vessel, useFAR);
    }

    public void AddController(BLController controller)
    {
      controllers.Add(controller);
    }

    public void RemoveController(BLController controller)
    {
      controllers.Remove(controller);
    }

    public void CopyToOtherCore(BoosterGuidanceCore other)
    {
      //Debug.Log("[BoosterGuidance] CopyToOtherCore");
      other.deployLandingGear = deployLandingGear;
      other.deployLandingGearHeight = deployLandingGearHeight;
      // can't sensibly clone this to other cores as once staged they will have different sets of engines
      other.landingBurnEngines = "current";
      other.landingBurnMaxAoA = landingBurnMaxAoA;
      other.landingBurnSteerKp = landingBurnSteerKp;
      other.reentryBurnAlt = reentryBurnAlt;
      other.reentryBurnMaxAoA = reentryBurnMaxAoA;
      other.reentryBurnSteerKp = reentryBurnSteerKp;
      other.reentryBurnTargetSpeed = reentryBurnTargetSpeed;
      other.tgtAlt = tgtAlt;
      other.tgtLatitude = tgtLatitude;
      other.tgtLongitude = tgtLongitude;
      other.touchdownMargin = touchdownMargin;
      other.touchdownSpeed = touchdownSpeed;
      other.igniteDelay = igniteDelay;
      other.phase = phase;
    }
    
    public void SetTarget(double latitude, double longitude, double alt)
    {
      tgtLatitude = latitude;
      tgtLongitude = longitude;
      tgtAlt = (int)alt;
      if (controller != null)
        controller.SetTarget(latitude, longitude, alt);
      // Assume already done
      //Targets.RedrawTarget(vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
    }

    public void SetPhase(BLControllerPhase phase)
    {
      if (controller != null)
        controller.SetPhase(phase);
    }

    public BLControllerPhase Phase()
    {
      if (controller != null)
        return controller.phase;
      else
        return BLControllerPhase.Unset;
    }

    public string SetLandingBurnEngines()
    {
      controller.SetLandingBurnEngines();
      landingBurnEngines = controller.GetLandingBurnEngineString();
      return controller.landingBurnEngines.Count().ToString();
    }

    public string UnsetLandingBurnEngines()
    {
      controller.UnsetLandingBurnEngines();
      landingBurnEngines = controller.GetLandingBurnEngineString();
      return "current";
    }

    public double LandingBurnHeight()
    {
      if (controller != null)
        return controller.landingBurnHeight;
      else
        return 0;
    }

    public bool Enabled()
    {
      return (controller != null) && (controller.enabled);
    }

    // BoosterGuidanceCore params changed so update controller
    public void Changed()
    {
      if (controller != null)
      {
        controller.InitReentryBurn(reentryBurnSteerKp, reentryBurnMaxAoA, reentryBurnAlt, reentryBurnTargetSpeed);
        controller.InitAeroDescent(aeroDescentSteerKp, aeroDescentMaxAoA);
        controller.InitLandingBurn(landingBurnSteerKp, landingBurnMaxAoA);
        controller.SetTarget(tgtLatitude, tgtLongitude, tgtAlt);
        controller.touchdownMargin = touchdownMargin;
        controller.touchdownSpeed = touchdownSpeed;
        controller.deployLandingGear = deployLandingGear;
        controller.deployLandingGearHeight = deployLandingGearHeight;
        controller.igniteDelay = igniteDelay;
      }
      CopyToOtherCores();
    }

    public void CopyToOtherCores()
    {
      foreach (var part in vessel.Parts)
      {
        foreach (var mod in part.Modules)
        {
          if (mod.GetType() == typeof(BoosterGuidanceCore))
          {
            var other = (BoosterGuidanceCore)mod;
            CopyToOtherCore(other);
          }
        }
      }
    }

    public void EnableGuidance()
    {
      KSPActionParam param = new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate);
      EnableGuidance(param);
    }

    [KSPAction("Enable BoosterGuidance")]
    public void EnableGuidance(KSPActionParam param)
    {
      controller = new BLController(vessel, useFAR);
      controller.SetLandingBurnEnginesFromString(landingBurnEngines);
      Changed(); // updates controller

      if ((tgtLatitude == 0) && (tgtLongitude == 0) && (tgtAlt == 0))
      {
        GuiUtils.ScreenMessage("No target set!");
        return;
      }
      if (!controller.enabled)
      {
        Debug.Log("[BoosterGuidnace] Enabled Guidance for vessel " + FlightGlobals.ActiveVessel.name);
        Vessel vessel = FlightGlobals.ActiveVessel;
        Targets.RedrawTarget(vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        vessel.Autopilot.Enable(VesselAutopilot.AutopilotMode.StabilityAssist);
        if (logging)
          StartLogging();
        vessel.OnFlyByWire += new FlightInputCallback(Fly);
      }
      controller.SetPhase(BLControllerPhase.Unset);
      GuiUtils.ScreenMessage("Enabled " + controller.PhaseStr());
      controller.enabled = true;
      AddController(controller);
    }

    public void StartLogging()
    {
      if (controller != null)
        controller.StartLogging(logFilename);
    }

    public void StopLogging()
    {
      if (controller != null)
        controller.StopLogging();
    }

    public void DisableGuidance()
    {
      KSPActionParam param = new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate);
      DisableGuidance(param);
    }

    [KSPAction("Disable BoosterGuidance")]
    public void DisableGuidance(KSPActionParam param)
    {
      Debug.Log("[BoosterGuidance] Disabling Guidance");
      if (controller != null)
      {
        Debug.Log("[BoosterGuidance] RemoveController");
        RemoveController(controller);
        Debug.Log("[BoosterGuidance] stopLogging");
        controller.StopLogging();
        Debug.Log("[BoosterGuidance] Disabled Guidance for vessel " + controller.vessel.name);
      }
      if ((vessel) && vessel.enabled) // extra checks
      {
        Debug.Log("[BoosterGuidance] Removing FlyByWire");
        vessel.OnFlyByWire -= new FlightInputCallback(Fly);
        vessel.Autopilot.Disable();
      }
      GuiUtils.ScreenMessage("Disabled Guidance");
      controller = null;
    }

    public void Fly(FlightCtrlState state)
    {
      double throttle;
      Vector3d steer;
      double minThrust;
      double maxThrust;

      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);

      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);

      bool landingGear;
      bool bailOutLandingBurn = true; // cut thrust if near ground and have too much thrust to reach ground
      controller.GetControlOutputs(vessel, vessel.GetTotalMass(), vessel.GetWorldPos3D(), vessel.GetObtVelocity(), vessel.transform.up, vessel.altitude, minThrust, maxThrust,
        controller.vessel.missionTime, vessel.mainBody, tgt_r, false, out throttle, out steer, out landingGear, bailOutLandingBurn);
      if ((landingGear) && KSPUtils.DeployLandingGear(vessel))
        GuiUtils.ScreenMessage("Deploying landing gear");

      if (vessel.checkLanded())
      {
        DisableGuidance();
        state.mainThrottle = 0;
        GuiUtils.ScreenMessage("Vessel " + controller.vessel.name + " landed!");
        return;
      }
   
      // Set active engines in landing burn
      if (controller.phase == BLControllerPhase.LandingBurn)
      {
        if (controller.landingBurnEngines != null)
        {
          foreach (ModuleEngines engine in KSPUtils.GetAllEngines(vessel))
          {
            if (controller.landingBurnEngines.Contains(engine))
            {
              if (!engine.isOperational)
                engine.Activate();
            }
            else
            {
              if (engine.isOperational)
                engine.Shutdown();
            }
          }
        }
      }

      // Draw predicted position if controlling that vessel
      if (vessel == FlightGlobals.ActiveVessel)
      {
        double lat, lon, alt;
        // prediction is for position of planet at current time compensating for
        // planet rotation
        vessel.mainBody.GetLatLonAlt(controller.predWorldPos, out lat, out lon, out alt);
        alt = vessel.mainBody.TerrainAltitude(lat, lon); // Make on surface
        Targets.RedrawPrediction(vessel.mainBody, lat, lon, alt + 1); // 1m above ground

        Targets.DrawSteer(vessel.vesselSize.x * Vector3d.Normalize(steer), null, Color.green);
      }
      state.mainThrottle = (float)throttle;
      vessel.Autopilot.SAS.lockedMode = false;
      vessel.Autopilot.SAS.SetTargetOrientation(steer, false);
    }

    public string Info()
    {
      // update if present, otherwise use last message
      // e.g. distance from target at landing
      if (controller != null)
        info = controller.info;
      return info;
    }
  }
}