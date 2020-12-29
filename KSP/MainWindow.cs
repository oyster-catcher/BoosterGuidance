using System;
using System.Linq;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using UnityEngine.Profiling;

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
    private static GuiUtils.TargetingCross targetingCross;
    private static GuiUtils.PredictionCross predictedCross;
    BLController activeController = null;
    DictionaryValueList<Guid, BLController> controllers = new DictionaryValueList<Guid, BLController>();
    BLController[] flying = { null, null, null, null, null }; // To connect to Fly() functions. Must be 5 or change EnableGuidance()
    Rect windowRect = new Rect(150, 150, 220, 564);
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    EditableInt tgtAlt = 0;
    // Re-Entry Burn
    EditableInt reentryBurnAlt = 55000;
    EditableInt reentryBurnTargetSpeed = 700;
    float reentryBurnMaxAoA = 20; // will be set from reentryBurnSteerKp
    float reentryBurnSteerKp = 0.004f; // angle to steer = gain * targetError(in m)
    // Aero descent
    double aeroDescentMaxAoA = 0; // will be set from Kp
    float aeroDescentSteerLogKp = 5.5f;
    float aeroDescentSteerKdProp = 0; // Kd set to this prop. of aeroDescentSteerKp
    // Landing burn
    float landingBurnSteerLogKp = 2.5f;
    double landingBurnMaxAoA = 0; // will be set from Kp
    string numLandingBurnEngines = "current";
    ITargetable lastVesselTarget = null;
    double lastNavLat = 0;
    double lastNavLon = 0;

    // Advanced settings
    EditableInt touchdownMargin = 20;
    EditableDouble touchdownSpeed = 2;
    EditableInt noSteerHeight = 100;
    bool deployLandingGear = true;
    EditableInt deployLandingGearHeight = 500;
    EditableInt simulationsPerSec = 10;
    EditableInt igniteDelay = 3; // Needed for RO

    Vessel currentVessel = null; // to detect vessel switch
    bool showTargets = true;
    bool logging = false;
    bool pickingPositionTarget = false;
    string info = "Disabled";
    double pickLat, pickLon, pickAlt;

    // GUI Elements
    Color red = new Color(1, 0, 0, 0.5f);
    bool map;

    public MainWindow()
    {
      //Awake();
    }

    public void OnGUI()
    {
      if (!hidden)
      {
        windowRect = GUI.Window(0, windowRect, WindowFunction, "Booster Guidance");
      }
    }

    public void Awake()
    {
      if (targetingCross != null)
        targetingCross.enabled = false;
      if (predictedCross != null)
        predictedCross.enabled = false;
      if (MapView.MapIsEnabled)
      {
        targetingCross = PlanetariumCamera.fetch.gameObject.AddComponent<GuiUtils.TargetingCross>();
        predictedCross = PlanetariumCamera.fetch.gameObject.AddComponent<GuiUtils.PredictionCross>();
      }
      else
      {
        targetingCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<GuiUtils.TargetingCross>();
        predictedCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<GuiUtils.PredictionCross>();
      }
      map = MapView.MapIsEnabled;
      targetingCross.SetColor(Color.yellow);
      targetingCross.enabled = true;
      predictedCross.SetColor(Color.red);
      predictedCross.enabled = false;
    }

    public void OnDestroy()
    {
      hidden = true;
      targetingCross.enabled = false;
      predictedCross.enabled = false;
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
      OnUpdate();
      SetEnabledColors(true);
      // Close button
      if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
      {
        Hide();
        return;
      }

      if (currentVessel != FlightGlobals.ActiveVessel)
      {
        BLControllerPhase phase = BLControllerPhase.Unset;
        if (currentVessel != null)
          Debug.Log("[BoosterGuidance] Changed vessel old=" + currentVessel.name + " new=" + FlightGlobals.ActiveVessel.name);
        else
          Debug.Log("[BoosterGuidance] Changed vessel new=" + FlightGlobals.ActiveVessel.name);

        // Use existing controller attached to vessel?
        try
        {
          // Already have an associated controller?
          activeController = controllers[FlightGlobals.ActiveVessel.id];
          Debug.Log("[BoosterGuidance] Found existing controller for id="+FlightGlobals.ActiveVessel.id);
          UpdateWindow(activeController);
          Debug.Log("[BoosterGuidance] From controller lat=" + tgtLatitude + " lon=" + tgtLongitude);
        }
        catch (KeyNotFoundException)
        {
          Debug.Log("[BoosterGuidance] No existing controller for id="+FlightGlobals.ActiveVessel.id);
          // No associated controller - vessel not previously controller in this game session
          activeController = new BLController(FlightGlobals.ActiveVessel);
          controllers[FlightGlobals.ActiveVessel.id] = activeController;
          Debug.Log("[BoosterGuidance] Saved active controller for id="+FlightGlobals.ActiveVessel.id);
          activeController.LoadFromVessel();
          Debug.Log("[BoosterGuidance] From vessel lat=" + tgtLatitude + " lon=" + tgtLongitude);
          UpdateWindow(activeController);
          Debug.Log("[BoosterGuidance] Setting phase " + phase + " from loaded vessel");
          if (activeController.phase != BLControllerPhase.Unset)
            EnableGuidance(phase);
        }
        RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        currentVessel = FlightGlobals.ActiveVessel;
      }

      // Show/hide targets
      targetingCross.enabled = showTargets;
      predictedCross.enabled = showTargets && (activeController != null) && (activeController.enabled);

      // Check for target being set
      if (currentVessel.targetObject != lastVesselTarget)
      {
        if (currentVessel.targetObject != null)
        {
          Vessel target = currentVessel.targetObject.GetVessel();
          tgtLatitude = target.latitude;
          tgtLongitude = target.longitude;
          tgtAlt = (int)target.altitude;
          GuiUtils.ScreenMessage("Target set to " + target.name);
        }
        lastVesselTarget = currentVessel.targetObject;
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
          Debug.Log("[BoosterGuidance] Setting to nav target lat=" + nav.Latitude + " lon=" + nav.Longitude);
          GuiUtils.ScreenMessage("[BoosterGuidance] Setting to nav target lat=" + nav.Latitude + " lon=" + nav.Longitude);
          tgtLatitude = nav.Latitude;
          tgtLongitude = nav.Longitude;
          lastNavLat = nav.Latitude;
          lastNavLon = nav.Longitude;
          // This is VERY unreliable
          //tgtAlt = (int)nav.Altitude;
          tgtAlt = (int)currentVessel.mainBody.TerrainAltitude(tgtLatitude, tgtLongitude);
          GuiUtils.ScreenMessage("Target set to " + nav.name);
          UpdateController(activeController);
        }
      }
      else
      {
        lastNavLat = 0;
        lastNavLon = 0;
      }

      // Check for unloaded vessels
      for (int i = 0; i < flying.Length; i++)
      {
        // Slot is filled, vessel exists and not current vessel
        if ((flying[i] != null) && (flying[i].vessel != null) && (flying[i] != activeController))
        {
          if (!flying[i].vessel.loaded)
          {
            GuiUtils.ScreenMessage("[BoosterGuidance] Guidance disabled for " + flying[i].vessel.name + " as out of physics range");
            DisableGuidance(flying[i]);
          }
        }
      }

      // Set Angle-of-Attack from gains
      reentryBurnMaxAoA = (reentryBurnSteerKp / 0.005f) * 30;
      aeroDescentMaxAoA = 30 * (aeroDescentSteerLogKp / 7);
      landingBurnMaxAoA = 30 * (landingBurnSteerLogKp / 7);

      tab = GUILayout.Toolbar(tab, new string[] { "Main", "Advanced" });
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
      {
        if (activeController != null)
          UpdateController(activeController); // copy settings from window
      }
    }

    bool AdvancedTab(int windowID)
    {
      // Suicide factor
      // Margin
      // Touchdown speed
      // No steer height
   
      GUILayout.BeginHorizontal();
      deployLandingGear = GUILayout.Toggle(deployLandingGear, "Deploy landing gear");
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Deploy gear height", deployLandingGearHeight, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("No steer height", noSteerHeight, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Touchdown margin", touchdownMargin, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Touchdown speed", touchdownSpeed, "m/s", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Engine startup", igniteDelay, "s", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Simulations /sec", simulationsPerSec, "", 65);
      GUILayout.EndHorizontal();

      // Show all active vessels
      GUILayout.Space(10);
      GUILayout.BeginHorizontal();
      GUILayout.Label("Other vessels:");
      GUILayout.EndHorizontal();
      GUIStyle info_style = new GUIStyle();
      info_style.normal.textColor = Color.white;
      for (int i = 0; i < flying.Length; i++)
      {
        // Slot is filled, vessel exists and not current vessel
        if ((flying[i] != null) && (flying[i].vessel != null) && (flying[i] != activeController))
        {
          GUILayout.BeginHorizontal();
          GUILayout.Label(flying[i].vessel.name + " ("+ (int)flying[i].vessel.altitude + "m)");
          GUILayout.FlexibleSpace();
          if (GUILayout.Button("X", GUILayout.Width(26))) // Cancel guidance
            DisableGuidance(flying[i]);
          GUILayout.EndHorizontal();

          GUILayout.BeginHorizontal();
          GUILayout.Label("  " + flying[i].info, info_style);
          GUILayout.EndHorizontal();

          GUILayout.BeginHorizontal();
          GUILayout.Label("  " + flying[i].PhaseStr(), info_style);
          GUILayout.EndHorizontal();
        }
      }

      GUI.DragWindow();
      return GUI.changed;
    }

    bool MainTab(int windowID)
    {
      bool targetChanged = false;

      // Check targets are on map vs non-map
      if (map != MapView.MapIsEnabled)
        Awake();


      BLControllerPhase phase = activeController.phase;
      if (!activeController.enabled)
        phase = BLControllerPhase.Unset;

      // Target:

      // Draw any Controls inside the window here
      GUILayout.Label("Target");//Target coordinates:

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
      if (GUILayout.Button("Pick Target"))
        PickTarget();
      if (GUILayout.Button("Set Here"))
        SetTargetHere();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      EditableInt preTgtAlt = tgtAlt;
      GuiUtils.SimpleTextBox("Target altitude", tgtAlt, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      showTargets = GUILayout.Toggle(showTargets, "Show targets");
      
      // TODO - Need to be specific to controller so works when switching vessel
      bool prevLogging = logging;
      logging = GUILayout.Toggle(logging, "Logging");
      if (activeController.enabled)
      {
        if ((!prevLogging) && (logging)) // logging switched on
          StartLogging();
        if ((prevLogging) && (!logging)) // logging switched off
          StopLogging();
      }
      GUILayout.EndHorizontal();

      // Info box
      GUILayout.BeginHorizontal();
      GUILayout.Label(info);
      GUILayout.EndHorizontal();

      // Boostback
      SetEnabledColors((phase == BLControllerPhase.BoostBack) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("Boostback", "Enable thrust towards target when out of atmosphere")))
        EnableGuidance(BLControllerPhase.BoostBack);
      GUILayout.EndHorizontal();

      // Coasting
      SetEnabledColors((phase == BLControllerPhase.Coasting) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("Coasting", "Turn to retrograde attitude and wait for Aero Descent phase")))
        EnableGuidance(BLControllerPhase.Coasting);
      GUILayout.EndHorizontal();

      // Re-Entry Burn
      SetEnabledColors((phase == BLControllerPhase.ReentryBurn) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("Re-Entry Burn", "Ignite engine on re-entry to reduce overheating")))
        EnableGuidance(BLControllerPhase.ReentryBurn);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Enable altitude", reentryBurnAlt, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GuiUtils.SimpleTextBox("Target speed", reentryBurnTargetSpeed, "m/s", 40);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      reentryBurnSteerKp = GUILayout.HorizontalSlider(reentryBurnSteerKp, 0, 0.005f);
      GUILayout.Label(((int)(reentryBurnMaxAoA)).ToString()+ "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Aero Descent
      SetEnabledColors((phase == BLControllerPhase.AeroDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("Aero Descent", "No thrust aerodynamic descent, steering with gridfins within atmosphere")))
        EnableGuidance(BLControllerPhase.AeroDescent);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      aeroDescentSteerLogKp = GUILayout.HorizontalSlider(aeroDescentSteerLogKp, 0, 7);
      GUILayout.Label(((int)aeroDescentMaxAoA).ToString() + "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();
      
      // Landing Burn
      SetEnabledColors((phase == BLControllerPhase.LandingBurn) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Landing Burn"))
        EnableGuidance(BLControllerPhase.LandingBurn);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Enable altitude");
      String text = "n/a";
      if (activeController != null)
      {
        if (activeController.landingBurnHeight > 0)
          text = ((int)(activeController.landingBurnHeight + tgtAlt)).ToString() + "m";
        else
        {
          if (activeController.landingBurnHeight < 0)
            text = "too heavy";
        }
      }
      GUILayout.Label(text, GUILayout.Width(60));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Engines");
      GUILayout.Label(numLandingBurnEngines);
      if (activeController != null)
      {
        if (numLandingBurnEngines == "current")  // Save active engines
        {
          if (GUILayout.Button("Set"))  // Set to currently active engines
            numLandingBurnEngines = activeController.SetLandingBurnEngines();
        }
        else
        {
          if (GUILayout.Button("Unset"))  // Set to currently active engines
          {
            numLandingBurnEngines = "current";
            activeController.UnsetLandingBurnEngines();
          }
        }
      }
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer", GUILayout.Width(40));
      landingBurnSteerLogKp = GUILayout.HorizontalSlider(landingBurnSteerLogKp, 0, 7);
      GUILayout.Label(((int)(landingBurnMaxAoA)).ToString() + "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if ((!activeController.enabled) || (phase == BLControllerPhase.Unset))
      {
        if (GUILayout.Button("Enable Guidance"))
          EnableGuidance(BLControllerPhase.Unset);
      }
      else
      {
        if (GUILayout.Button("Disable Guidance"))
          DisableGuidance(activeController);
      }

      GUILayout.EndHorizontal();

      GUI.DragWindow();
      return (GUI.changed) || targetChanged;
    }


    public void Show()
    {
      hidden = false;
      targetingCross.enabled = showTargets;
      predictedCross.enabled = showTargets;
    }

    public void Hide()
    {
      hidden = true;
      targetingCross.enabled = false;
      predictedCross.enabled = false;
    }

    public void UpdateController(BLController controller)
    {
      if (controller != null)
      {
        controller.reentryBurnAlt = reentryBurnAlt;
        controller.reentryBurnTargetSpeed = reentryBurnTargetSpeed;
        controller.reentryBurnSteerKp = reentryBurnSteerKp;
        controller.landingBurnMaxAoA = landingBurnMaxAoA;
        controller.SetTarget(tgtLatitude, tgtLongitude, tgtAlt);
        controller.suicideFactor = 0.75;
        controller.landingBurnSteerKp = Math.Exp(landingBurnSteerLogKp);
        controller.aeroDescentMaxAoA = aeroDescentMaxAoA;
        controller.aeroDescentSteerKp = Math.Exp(aeroDescentSteerLogKp);
        controller.aeroDescentSteerKdProp = aeroDescentSteerKdProp;
        // Note that the Kp gain in the PIDs below is set by combining the relevant Kp from above
        // and a gain factor based on air resistance an throttle to determine whether to steer
        // aerodynamically or by thrust, and how sensitive the vessel is to that
        controller.pid_aero = new PIDclamp("aero", 1, 0, 0, (float)aeroDescentMaxAoA);
        controller.pid_landing = new PIDclamp("landing", 1, 0, 0, (float)landingBurnMaxAoA);
        controller.igniteDelay = igniteDelay;
        controller.noSteerHeight = noSteerHeight;
        controller.deployLandingGear = deployLandingGear;
        controller.deployLandingGearHeight = deployLandingGearHeight;
        controller.touchdownMargin = touchdownMargin;
        controller.touchdownSpeed = (float)touchdownSpeed;
        controller.simulationsPerSec = (float)simulationsPerSec;
        controller.SaveToVessel(); // save settings to multiple BoosterGuidanceVesselSettings modules
      }
    }

    // Controller changed - update window
    public void UpdateWindow(BLController controller)
    {
      reentryBurnAlt = (int)controller.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)controller.reentryBurnTargetSpeed;
      // TODO: Read from PID
      aeroDescentSteerLogKp = Mathf.Log((float)controller.aeroDescentSteerKp);
      reentryBurnAlt = (int)controller.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)controller.reentryBurnTargetSpeed;
      landingBurnSteerLogKp = Mathf.Log((float)controller.landingBurnSteerKp);
      tgtLatitude = controller.TgtLatitude;
      tgtLongitude = controller.TgtLongitude;
      tgtAlt = (int)controller.TgtAlt;
      if (controller.landingBurnEngines != null)
        numLandingBurnEngines = controller.landingBurnEngines.Count.ToString();
      else
        numLandingBurnEngines = "current";
      igniteDelay = (int)controller.igniteDelay;
      noSteerHeight = (int)controller.noSteerHeight;
      deployLandingGear = controller.deployLandingGear;
      deployLandingGearHeight = (int)controller.deployLandingGearHeight;
      simulationsPerSec = (int)controller.simulationsPerSec;
    }

    void StartLogging()
    {
      if (activeController != null)
      {
        string name = activeController.vessel.name;
        name = name.Replace(" ", "_");
        name = name.Replace("(", "");
        name = name.Replace(")", "");
        Transform logTransform = RedrawTarget(activeController.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        activeController.StartLogging(name, logTransform);
      }
    }

    void StopLogging()
    {
      if (activeController != null)
        activeController.StopLogging();
    }

    void OnPickingPositionTarget()
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        // Previous position
        RedrawTarget(activeController.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
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
        RedrawTarget(vessel.mainBody, pickLat, pickLon, pickAlt);

        if (Input.GetMouseButton(0))  // Picked
        {
          tgtLatitude = pickLat;
          tgtLongitude = pickLon;
          tgtAlt = (int)pickAlt;
          pickingPositionTarget = false;
          string message = "Picked target";
          GuiUtils.ScreenMessage(message);
          UpdateController(activeController);
        }
      }
    }

    void OnUpdate()
    {
      // Redraw targets
      if (!pickingPositionTarget)
      {
        // Need to redraw as size changes (may be less often)
        RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
      }
      else
        OnPickingPositionTarget();
    }


    void PickTarget()
    {
      showTargets = true;
      pickingPositionTarget = true;
      string message = "Click to select a target";
      GuiUtils.ScreenMessage(message);
    }


    void SetTargetHere()
    {
      tgtLatitude = FlightGlobals.ActiveVessel.latitude;
      tgtLongitude = FlightGlobals.ActiveVessel.longitude;
      double lowestY = KSPUtils.FindLowestPointOnVessel(FlightGlobals.ActiveVessel);
      tgtAlt = (int)FlightGlobals.ActiveVessel.altitude + (int)lowestY;
      if (activeController != null)
        UpdateController(activeController);
      RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt + activeController.lowestY);
      GuiUtils.ScreenMessage("[BoosterGuidance] set to vessel");
    }

    Transform RedrawTarget(CelestialBody body, double lat, double lon, double alt)
    {
      Transform transform = GuiUtils.SetUpTransform(body, lat, lon, alt);
      // Only sure when set (ideally use a separate flag!)
      targetingCross.enabled = showTargets && ((lat!=0) || (lon!=0) || (alt!=0));
      targetingCross.SetLatLonAlt(body, lat, lon, alt);
      return transform;
    }
    

    Transform RedrawPrediction(CelestialBody body, double lat, double lon, double alt)
    {
      predictedCross.enabled = showTargets && (activeController != null);
      predictedCross.SetLatLonAlt(body, lat, lon, alt);
      return null;
    }
    
    int GetSlotFromVessel(Vessel vessel)
    {
      for (int j = 0; j < flying.Length; j++)
      {
        if ((flying[j] != null) && (flying[j].vessel == vessel))
          return j;
      }
      return -1;
    }
    
    int GetFreeSlot()
    {
      for (int j = 0; j < flying.Length; j++)
      {
        if (flying[j] == null)
          return j;
      }
      return -1;
    }

    void EnableGuidance(BLControllerPhase phase)
    {
      if ((tgtLatitude == 0) && (tgtLongitude == 0) && (tgtAlt == 0))
      {
        GuiUtils.ScreenMessage("No target set!");
        return;
      }
      if (!activeController.enabled)
      {
        Debug.Log("[BoosterGuidance] Enable Guidance for vessel " + FlightGlobals.ActiveVessel.name);
        Vessel vessel = FlightGlobals.ActiveVessel;
        RedrawTarget(vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        vessel.Autopilot.Enable(VesselAutopilot.AutopilotMode.StabilityAssist);
        // Should be already attached
        //activeController.AttachVessel(vessel);

        // Find slot for controller
        int i = GetFreeSlot();
        if (i != -1)
        {
          flying[i] = activeController;
          Debug.Log("[BoosterGuidance] Allocating slot " + i + " name=" + vessel.name + "(id="+vessel.id+") to " + activeController);
        }
        else
        {
          GuiUtils.ScreenMessage("All " + flying.Length + " guidance slots used");
          return;
        }

        if (i == 0)
          vessel.OnFlyByWire += new FlightInputCallback(Fly0); // 1st vessel
        if (i == 1)
          vessel.OnFlyByWire += new FlightInputCallback(Fly1); // 2nd vessel
        if (i == 2)
          vessel.OnFlyByWire += new FlightInputCallback(Fly2); // 3rd vessel
        if (i == 3)
          vessel.OnFlyByWire += new FlightInputCallback(Fly3); // 4th vessel
        if (i == 4)
          vessel.OnFlyByWire += new FlightInputCallback(Fly4); // 5th vessel
      }
      activeController.SetPhase(phase);
      GuiUtils.ScreenMessage("Enabled " + activeController.PhaseStr());
      activeController.enabled = true;
      if (logging)
        StartLogging();
    }


    void DisableGuidance(BLController controller)
    {
      if (controller.enabled)
      {
        Vessel vessel = controller.vessel;
        vessel.Autopilot.Disable();
        int i = GetSlotFromVessel(vessel);
        Debug.Log("[BoosterGuidance] DisableGuidance() slot=" + i + " name="+vessel.name+"(id="+vessel.id+")");
        if (i == 0)
          vessel.OnFlyByWire -= new FlightInputCallback(Fly0);
        if (i == 1)
          vessel.OnFlyByWire -= new FlightInputCallback(Fly1);
        if (i == 2)
          vessel.OnFlyByWire -= new FlightInputCallback(Fly2);
        if (i == 3)
          vessel.OnFlyByWire -= new FlightInputCallback(Fly3);
        if (i == 4)
          vessel.OnFlyByWire -= new FlightInputCallback(Fly4);
        // Free up slot
        if (i != -1)
          flying[i] = null;
        controller.StopLogging();
        controller.phase = BLControllerPhase.Unset;
        if (controller == activeController)
        {
          GuiUtils.ScreenMessage("Guidance disabled!");
          predictedCross.enabled = false;
        }
        controller.enabled = false;
      }
    }
    public void Fly0(FlightCtrlState state)
    {
      Fly(flying[0], state);
    }

    public void Fly1(FlightCtrlState state)
    {
      Fly(flying[1], state);
    }

    public void Fly2(FlightCtrlState state)
    {
      Fly(flying[2], state);
    }

    public void Fly3(FlightCtrlState state)
    {
      Fly(flying[3], state);
    }

    public void Fly4(FlightCtrlState state)
    {
      Fly(flying[4], state);
    }

    public void Fly(BLController controller, FlightCtrlState state)
    {
      double throttle;
      Vector3d steer;
      double minThrust;
      double maxThrust;


      if (controller == null)
        return;
      Vessel vessel = controller.vessel;

      if (activeController == controller)
        info = controller.info;

      if (vessel.checkLanded())
      {
        GuiUtils.ScreenMessage("Vessel " + controller.vessel.name + " landed!");
        state.mainThrottle = 0;
        DisableGuidance(controller);
        return;
      }
      

      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);

      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);

      bool landingGear, gridFins;
      controller.GetControlOutputs(vessel, vessel.GetTotalMass(), vessel.GetWorldPos3D(), vessel.GetObtVelocity(), vessel.transform.up, vessel.altitude, minThrust, maxThrust,
        controller.vessel.missionTime, vessel.mainBody, tgt_r, false, out throttle, out steer, out landingGear, out gridFins);
      //Debug.Log("[BoosterGuidance] alt=" + controller.vessel.altitude + " gear_height=" + controller.deployLandingGearHeight + " deploy=" + controller.deployLandingGear+" deploy_now="+landingGear);
      if ((landingGear) && KSPUtils.DeployLandingGears(vessel))
        GuiUtils.ScreenMessage("Deploying landing gear");
      //if (gridFins)
      //  ScreenMessages.PostScreenMessage("Deploying grid fins", 1.0f, ScreenMessageStyle.UPPER_CENTER);
    

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
      if (controller == activeController)
      {
        double lat, lon, alt;
        // prediction is for position of planet at current time compensating for
        // planet rotation
        vessel.mainBody.GetLatLonAlt(controller.predWorldPos, out lat, out lon, out alt);
        alt = vessel.mainBody.TerrainAltitude(lat, lon); // Make on surface
        RedrawPrediction(vessel.mainBody, lat, lon, alt + 1); // 1m above grou
      }
      state.mainThrottle = (float)throttle;
      vessel.Autopilot.SAS.lockedMode = false;
      vessel.Autopilot.SAS.SetTargetOrientation(steer, false);
    }
  }
}