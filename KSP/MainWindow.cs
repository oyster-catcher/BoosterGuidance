using System;
using System.Linq;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using UnityEngine.Profiling;

// New rules
// - Window must be associated with BoosterGuidanceCore and BLController thro' core
// - All settings directly update core when changed
// - Controller settings need to update too - how?


namespace BoosterGuidance
{
  [KSPAddon(KSPAddon.Startup.Flight, false)]

  public class MainWindow : MonoBehaviour
  {
    // constants
    Color tgt_color = new Color(1, 1, 0, 0.5f);
    Color pred_color = new Color(1, 0.2f, 0.2f, 0.5f);

    // private
    int tab = 0;
    bool hidden = true;
    BoosterGuidanceCore core = null;
    float maxReentryGain = 1;
    float maxAeroDescentGain = 1;
    float maxLandingBurnGain = 1;
    float maxSteerAngle = 30; // 30 degrees
    Rect windowRect = new Rect(150, 150, 220, 520);

    // Main GUI Elements
    bool showTargets = true;
    bool debug = false; // show steer
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    EditableInt tgtAlt = 0;
    EditableInt reentryBurnAlt = 55000;
    EditableInt reentryBurnTargetSpeed = 700;
    string numLandingBurnEngines = "current";

    // Advanced GUI Elements
    EditableInt touchdownMargin = 20;
    EditableDouble touchdownSpeed = 2;
    EditableInt noSteerHeight = 100;
    bool deployLandingGear = true;
    EditableInt deployLandingGearHeight = 500;
    EditableInt igniteDelay = 3; // Needed for RO

    // Targeting
    ITargetable lastVesselTarget = null;
    double lastNavLat = 0;
    double lastNavLon = 0;    
    bool pickingPositionTarget = false;

    double pickLat, pickLon, pickAlt;

    // GUI Elements
    Color red = new Color(1, 0, 0, 0.5f);
    static bool hasFAR = false;

    public void Start()
    {
      hasFAR = Trajectories.AerodynamicModelFactory.HasFAR();
      Debug.Log("[BoosterGuidance] Start hasFAR="+hasFAR);
    }

    public void OnGUI()
    {
      if (!hidden)
        windowRect = GUI.Window(0, windowRect, WindowFunction, "Booster Guidance");
    }

    public void OnDestroy()
    {
      hidden = true;
      Targets.targetingCross.enabled = false;
      Targets.predictedCross.enabled = false;
    }

    private BoosterGuidanceCore CheckCore(Vessel vessel)
    {
      if (core == null)
      {
        Debug.Log("[BoosterGuidance] core==null vessel=" + vessel + " map=" + MapView.MapIsEnabled);
        core = BoosterGuidanceCore.GetBoosterGuidanceCore(vessel);
        if (core != null)
        {
          UpdateFromCore();
          Targets.SetVisibility(showTargets, showTargets && core.Enabled() && (FlightGlobals.ActiveVessel == core.vessel));
        }
        else
        {
          return core;
        }
      }
      else
      {
        if (core.vessel != vessel)
        {
          Debug.Log("[BoosterGuidance] core.vessel!=vessel vessel=" + vessel + " map=" + MapView.MapIsEnabled);
          // Get new BoosterGuidanceCore as vessel changed
          core = BoosterGuidanceCore.GetBoosterGuidanceCore(vessel);
          UpdateFromCore();
        }
      }
      if (core == null)
        Debug.Log("[BoosterGuidance] Vessel " + vessel.name + " has no BoosterGuidanceCore");
      return core;
    }

    void SetEnabledColors(bool phaseEnabled)
    {
      if (phaseEnabled)
      {
        GUI.skin.button.normal.textColor = new Color(1, 1, 1, 1);
        GUI.skin.label.normal.textColor = new Color(1, 1, 1, 1);
        GUI.skin.toggle.normal.textColor = new Color(1, 1, 1, 1);
        GUI.skin.box.normal.textColor = new Color(1, 1, 1, 1);
        GUI.skin.textArea.normal.textColor = new Color(1, 1, 1, 1);
        GUI.skin.textField.normal.textColor = new Color(1, 1, 1, 1);
      }
      else
      {
        GUI.skin.button.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
        GUI.skin.label.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
        GUI.skin.toggle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
        GUI.skin.box.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
        GUI.skin.textArea.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
        GUI.skin.textField.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1);
      }
    }

    void WindowFunction(int windowID)
    {
      BoosterGuidanceCore core = CheckCore(FlightGlobals.ActiveVessel);
      if (core == null)
      {
        GUILayout.BeginHorizontal();
        GUILayout.Label("No BoosterGuidance Core");
        GUILayout.EndHorizontal();
        return;
      }

      OnUpdate();
      SetEnabledColors(true);
      // Close button
      if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
      {
        Hide();
        return;
      }

      // Check for target being set
      if (core.vessel.targetObject != lastVesselTarget)
      {
        if (core.vessel.targetObject != null)
        {
          Vessel target = core.vessel.targetObject.GetVessel();
          tgtLatitude = target.latitude;
          tgtLongitude = target.longitude;
          tgtAlt = (int)target.altitude;
          core.SetTarget(tgtLatitude, tgtLongitude, tgtAlt);
          string msg = String.Format(Localizer.Format("#BoosterGuidance_TargetSetToX"),target.name);
          GuiUtils.ScreenMessage(msg);
        }
        lastVesselTarget = core.vessel.targetObject;
      }

      // Check for navigation target
      NavWaypoint nav = NavWaypoint.fetch;
      if (nav.IsActive)
      {
        // Does current nav position differ from last one used? A hack because
        // a can't see a way to check if the nav waypoint has changed
        // Doing it this way means lat and lon in window can be edited without them
        // getting locked to the nav waypoint
        if ((lastNavLat != nav.Latitude) || (lastNavLon != nav.Longitude))
        {
          Coordinates pos = new Coordinates(nav.Latitude, nav.Longitude);
          Debug.Log("[BoosterGuidance] Target set to nav location "+pos.ToStringDMS());
          tgtLatitude = nav.Latitude;
          tgtLongitude = nav.Longitude;
          lastNavLat = nav.Latitude;
          lastNavLon = nav.Longitude;
          // This is VERY unreliable
          //tgtAlt = (int)nav.Altitude;
          tgtAlt = (int)FlightGlobals.ActiveVessel.mainBody.TerrainAltitude(tgtLatitude, tgtLongitude);
          core.SetTarget(tgtLatitude, tgtLongitude, tgtAlt);
          string msg = String.Format(Localizer.Format("#BoosterGuidance_TargetSetToX"), pos.ToStringDMS());
          GuiUtils.ScreenMessage(msg);
        }
      }
      else
      {
        lastNavLat = 0;
        lastNavLon = 0;
      }

      // Check for unloaded vessels
      foreach(var controller in BoosterGuidanceCore.controllers)
      {
        if (!controller.vessel.loaded)
        {
          GuiUtils.ScreenMessage("Guidance disabled for " + controller.vessel.name + " as out of physics range");
          DisableGuidance();
        }
      }
      

      tab = GUILayout.Toolbar(tab, new string[] { Localizer.Format("#BoosterGuidance_Main"), Localizer.Format("Advanced")});
      bool changed = false;
      switch(tab)
      {
        case 0:
          changed = MainTab(windowID);
          break;
        case 1:
          changed = AdvancedTab(windowID);
          break;
      }

      if (changed)
        UpdateCore();
    }

    bool AdvancedTab(int windowID)
    {
      // Suicide factor
      // Margin
      // Touchdown speed
      // No steer height
   
      GUILayout.BeginHorizontal();
      deployLandingGear = GUILayout.Toggle(deployLandingGear, Localizer.Format("#BoosterGuidance_DeployGear"));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_DeployGearHeight"), deployLandingGearHeight, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_NoSteerHeight"), noSteerHeight, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_TouchdownMargin"), touchdownMargin, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_TouchdownSpeed"), touchdownSpeed, "m/s", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_IgniteDelay"), igniteDelay, "s", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      debug = GUILayout.Toggle(debug, Localizer.Format("#BoosterGuidance_Debug"));
      Targets.showSteer = debug;
      GUILayout.EndHorizontal();

      // Show all active vessels
      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      GUILayout.Label(Localizer.Format("#BoosterGuidance_OtherVessels")+":");
      GUILayout.EndHorizontal();
      GUIStyle info_style = new GUIStyle();
      info_style.normal.textColor = Color.white;
      foreach(var controller in BoosterGuidanceCore.controllers)
      {
        try
        {
          if ((controller != null) && (controller.enabled) && (controller.vessel != FlightGlobals.ActiveVessel))
          {
            GUILayout.BeginHorizontal();
            GUILayout.Label(controller.vessel.name + " (" + (int)controller.vessel.altitude + "m)");
            GUILayout.FlexibleSpace();
            //if (GUILayout.Button("X", GUILayout.Width(26))) // Cancel guidance
            //  DisableGuidance();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("  " + controller.info, info_style);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("  " + controller.PhaseStr(), info_style);
            GUILayout.EndHorizontal();
          }
        }
        catch(Exception e) // TODO - Use correct exception to invalid reference
        {
          Debug.Log("[BoosterGuidance] Removing controller: " + e.Message);
          BoosterGuidanceCore.controllers.Remove(controller);
        }
      }

      GUI.DragWindow();
      return GUI.changed;
    }

    bool MainTab(int windowID)
    {
      bool targetChanged = false;
      BoosterGuidanceCore core = CheckCore(FlightGlobals.ActiveVessel);
      BLControllerPhase phase = core.Phase();

      // Target:

      // Draw any Controls inside the window here
      GUILayout.Label(Localizer.Format("#BoosterGuidance_Target"));//Target coordinates:

      GUILayout.BeginHorizontal();
      double step = 1.0 / (60 * 60); // move by 1 arc second
      tgtLatitude.DrawEditGUI(EditableAngle.Direction.NS);
      if (GUILayout.Button("▲"))
      {
        tgtLatitude += step;
        targetChanged = true;
      }
      if (GUILayout.Button("▼"))
      {
        tgtLatitude -= step;
        targetChanged = true;
      }
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      tgtLongitude.DrawEditGUI(EditableAngle.Direction.EW);
      if (GUILayout.Button("◄"))
      {
        tgtLongitude -= step;
        targetChanged = true;
      }
      if (GUILayout.Button("►"))
      {
        tgtLongitude += step;
        targetChanged = true;
      }
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      if (GUILayout.Button(Localizer.Format("#BoosterGuidance_PickTarget")))
        PickTarget();
      if (GUILayout.Button("Set Here"))
        SetTargetHere();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      showTargets = GUILayout.Toggle(showTargets, Localizer.Format("#BoosterGuidance_ShowTargets"));

      bool prevLogging = core.logging;
      // TODO
      string filename = FlightGlobals.ActiveVessel.name;
      filename = filename.Replace(" ", "_");
      filename = filename.Replace("(", "");
      filename = filename.Replace(")", "");
      core.logFilename = filename;
      core.logging = GUILayout.Toggle(core.logging, Localizer.Format("#BoosterGuidance_Logging"));
      if (core.Enabled())
      {
        if ((!prevLogging) && (core.logging)) // logging switched on
          core.StartLogging();
        if ((prevLogging) && (!core.logging)) // logging switched off
          core.StopLogging();
      }
      GUILayout.EndHorizontal();

      // Info box
      GUILayout.BeginHorizontal();
      GUILayout.Label(core.Info());
      GUILayout.EndHorizontal();

      // Boostback
      SetEnabledColors((phase == BLControllerPhase.BoostBack) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent(Localizer.Format("#BoosterGuidance_Boostback"), "Enable thrust towards target when out of atmosphere")))
        EnableGuidance(BLControllerPhase.BoostBack);
      GUILayout.EndHorizontal();

      // Coasting
      SetEnabledColors((phase == BLControllerPhase.Coasting) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent(Localizer.Format("#BoosterGuidance_Coasting"), "Turn to retrograde attitude and wait for Aero Descent phase")))
        EnableGuidance(BLControllerPhase.Coasting);
      GUILayout.EndHorizontal();

      // Re-Entry Burn
      SetEnabledColors((phase == BLControllerPhase.ReentryBurn) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent(Localizer.Format("#BoosterGuidance_ReentryBurn"), "Ignite engine on re-entry to reduce overheating")))
        EnableGuidance(BLControllerPhase.ReentryBurn);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_EnableAltitude"), reentryBurnAlt, "m", 65);
      core.reentryBurnAlt = reentryBurnAlt;
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox(Localizer.Format("#BoosterGuidance_TargetSpeed"), reentryBurnTargetSpeed, "m/s", 40);
      core.reentryBurnTargetSpeed = reentryBurnTargetSpeed;
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      core.reentryBurnSteerKp = Mathf.Clamp(core.reentryBurnSteerKp, 0, maxReentryGain);
      core.reentryBurnSteerKp = GUILayout.HorizontalSlider(core.reentryBurnSteerKp, 0, maxReentryGain);
      GUILayout.Label(((int)(core.reentryBurnMaxAoA)).ToString()+ "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Aero Descent
      SetEnabledColors((phase == BLControllerPhase.AeroDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent(Localizer.Format("#BoosterGuidance_AeroDescent"), "No thrust aerodynamic descent, steering with gridfins within atmosphere")))
        EnableGuidance(BLControllerPhase.AeroDescent);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      core.aeroDescentSteerKp = Mathf.Clamp(core.aeroDescentSteerKp, 0, maxAeroDescentGain);
      core.aeroDescentSteerKp = GUILayout.HorizontalSlider(core.aeroDescentSteerKp, 0, maxAeroDescentGain); // max turn 2 degrees for 100m error
      GUILayout.Label(((int)core.aeroDescentMaxAoA).ToString() + "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Landing Burn
      SetEnabledColors((phase == BLControllerPhase.LandingBurn) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(Localizer.Format("#BoosterGuidance_LandingBurn")))
        EnableGuidance(BLControllerPhase.LandingBurn);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label(Localizer.Format("#BoosterGuidance_EnableAltitude"));
      String text = "n/a";
      if (core.Enabled())
      {
        if (core.LandingBurnHeight() > 0)
          text = ((int)(core.LandingBurnHeight() + tgtAlt)).ToString() + "m";
        else
        {
          if (core.LandingBurnHeight() < 0)
            text = Localizer.Format("#BoosterGuidance_TooHeavy");
        }
      }
      GUILayout.Label(text, GUILayout.Width(60));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label(Localizer.Format("#BoosterGuidance_Engines"));
      if (numLandingBurnEngines == Localizer.Format("#BoosterGuidance_Current"))
        GUILayout.Label(numLandingBurnEngines);
      else
        GUILayout.Label(numLandingBurnEngines);
      if (numLandingBurnEngines == "current")  // Save active engines
      {
        if (GUILayout.Button(Localizer.Format("#BoosterGuidance_Set")))  // Set to currently active engines
          numLandingBurnEngines = core.SetLandingBurnEngines();
      }
      else
      {
        if (GUILayout.Button(Localizer.Format("#BoosterGuidance_Unset")))  // Set to currently active engines
          numLandingBurnEngines = core.UnsetLandingBurnEngines();
      }
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      core.landingBurnSteerKp = Mathf.Clamp(core.landingBurnSteerKp, 0, maxLandingBurnGain);
      core.landingBurnSteerKp = GUILayout.HorizontalSlider(core.landingBurnSteerKp, 0, maxLandingBurnGain);
      string max = Localizer.Format("#BoosterGuidance_Max");
      GUILayout.Label(((int)(core.landingBurnMaxAoA)).ToString() + "°("+max+")", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if (!core.Enabled())
      {
        if (GUILayout.Button(Localizer.Format("#BoosterGuidance_EnableGuidance")))
          core.EnableGuidance();
      }
      else
      {
        if (GUILayout.Button(Localizer.Format("#BoosterGuidance_DisableGuidance")))
          core.DisableGuidance();
      }
      GUILayout.EndHorizontal();

      GUI.DragWindow();
      return (GUI.changed) || targetChanged;
    }

    public void Show()
    {
      hidden = false;
      tab = 0; // Switch to main tab incase Advanced tab got broken
    }

    public void Hide()
    {
      hidden = true;
      Targets.targetingCross.enabled = false;
      Targets.predictedCross.enabled = false;
    }

    // Core changed - update window
    public void UpdateFromCore()
    {
      reentryBurnAlt = (int)core.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)core.reentryBurnTargetSpeed;
      reentryBurnAlt = (int)core.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)core.reentryBurnTargetSpeed;
      tgtLatitude = core.tgtLatitude;
      tgtLongitude = core.tgtLongitude;
      tgtAlt = (int)core.tgtAlt;
      // This is bit-field
      numLandingBurnEngines = core.landingBurnEngines;
      igniteDelay = (int)core.igniteDelay;
      noSteerHeight = (int)core.noSteerHeight;
      deployLandingGear = core.deployLandingGear;
      deployLandingGearHeight = (int)core.deployLandingGearHeight;
      Targets.RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);

      // Apply limits
      core.reentryBurnSteerKp = Mathf.Clamp(core.reentryBurnSteerKp, 0, maxReentryGain);
      core.aeroDescentSteerKp = Mathf.Clamp(core.aeroDescentSteerKp, 0, maxAeroDescentGain);
      core.landingBurnSteerKp = Mathf.Clamp(core.landingBurnSteerKp, 0, maxLandingBurnGain);

      // Set MaxAoA from core - overriden
      core.reentryBurnMaxAoA = maxSteerAngle * (core.reentryBurnSteerKp / maxReentryGain);
      core.aeroDescentMaxAoA = maxSteerAngle * (core.aeroDescentSteerKp / maxAeroDescentGain);
      core.landingBurnMaxAoA = maxSteerAngle * (core.landingBurnSteerKp / maxLandingBurnGain);
    }

    public void UpdateCore()
    {
      core.SetTarget(tgtLatitude, tgtLongitude, tgtAlt);
      // Set Angle - of - Attack from gains
      core.reentryBurnMaxAoA = maxSteerAngle * (core.reentryBurnSteerKp / maxReentryGain);
      core.aeroDescentMaxAoA = maxSteerAngle * (core.aeroDescentSteerKp / maxAeroDescentGain);
      core.landingBurnMaxAoA = maxSteerAngle * (core.landingBurnSteerKp / maxLandingBurnGain);
      // Other
      core.touchdownMargin = touchdownMargin;
      core.touchdownSpeed = (float)touchdownSpeed;
      core.deployLandingGear = deployLandingGear;
      core.deployLandingGearHeight = deployLandingGearHeight;
      core.Changed();
      Targets.RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
    }

    void OnPickingPositionTarget()
    {
      if (GuiUtils.MouseIsOverWindow(windowRect))
        return;

      if (Input.GetKeyDown(KeyCode.Escape))
      {
        // Previous position
        Targets.RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        pickingPositionTarget = false;
      }
      RaycastHit hit;
      Vessel vessel = FlightGlobals.ActiveVessel;
      bool isHit = false;

      if (!MapView.MapIsEnabled)
      {
        if (GuiUtils.GetMouseHit(vessel.mainBody, windowRect, MapView.MapIsEnabled, out hit))
        {
          isHit = true;
          // Moved or picked
          vessel.mainBody.GetLatLonAlt(hit.point, out pickLat, out pickLon, out pickAlt);
        }
      }
      if (!isHit)
      {
        if (GuiUtils.GetBodyRayIntersect(vessel.mainBody, MapView.MapIsEnabled, out pickLat, out pickLon, out pickAlt))
          isHit = true;
      }
      

      if (isHit)
      {
        Targets.RedrawTarget(vessel.mainBody, pickLat, pickLon, pickAlt);
        if ((Input.GetMouseButton(0)) && (!GuiUtils.MouseIsOverWindow(windowRect))) // Picked
        {
          // Update GUI
          tgtLatitude = pickLat;
          tgtLongitude = pickLon;
          tgtAlt = (int)pickAlt;
          pickingPositionTarget = false;
          core.SetTarget(pickLat, pickLon, pickAlt);
        }
      }
    }

    void OnUpdate()
    {
      // Set visibility of targets
      Targets.InitTargets(); // ensure updated with map switch
      Targets.SetVisibility(showTargets, core.Enabled() && showTargets);
      if (pickingPositionTarget)
        OnPickingPositionTarget();
    }


    void PickTarget()
    {
      showTargets = true;
      pickingPositionTarget = true;
      GuiUtils.ScreenMessage(Localizer.Format("#BoosterGuidance_ClickToPickTarget"));
    }


    void SetTargetHere()
    {
      tgtLatitude = FlightGlobals.ActiveVessel.latitude;
      tgtLongitude = FlightGlobals.ActiveVessel.longitude;
      double lowestY = KSPUtils.FindLowestPointOnVessel(FlightGlobals.ActiveVessel);
      tgtAlt = (int)FlightGlobals.ActiveVessel.altitude + (int)lowestY;
      core.SetTarget(FlightGlobals.ActiveVessel.latitude, FlightGlobals.ActiveVessel.longitude, tgtAlt);
      core.Changed();
      GuiUtils.ScreenMessage(Localizer.Format("#BoosterGuidance_TargetSetToVessel"));
    }

    void EnableGuidance(BLControllerPhase phase)
    {
      BoosterGuidanceCore core = BoosterGuidanceCore.GetBoosterGuidanceCore(FlightGlobals.ActiveVessel);
      KSPActionParam param = new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate);
      core.useFAR = hasFAR;
      Debug.Log("[BoosterGuidance] Vessel=" + FlightGlobals.ActiveVessel.name + " useFAR=" + core.useFAR);
      core.EnableGuidance(param);
      core.SetPhase(phase);
    }

    void DisableGuidance()
    {
      BoosterGuidanceCore core = BoosterGuidanceCore.GetBoosterGuidanceCore(FlightGlobals.ActiveVessel);
      core.DisableGuidance();
    }
  }
}