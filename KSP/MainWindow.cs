using System;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using UnityEngine.Profiling;

namespace BoosterGuidance
{
  [KSPAddon(KSPAddon.Startup.Flight, false)]
  public class MainWindow : MonoBehaviour
  {
    // constantsBurnSt
    Color tgt_color = new Color(1, 1, 0, 0.5f);
    Color pred_color = new Color(1, 0.2f, 0.2f, 0.5f);

    // private
    int tab = 0;
    bool hidden = true;
    private static GuiUtils.TargetingCross targetingCross;
    private static GuiUtils.PredictionCross predictedCross;
    BLController activeController = null;
    DictionaryValueList<Vessel, BLController> controllers = new DictionaryValueList<Vessel, BLController>();
    BLController[] flying = { null, null, null, null, null }; // To connect to Fly() functions. Must be 5 or change EnableGuidance()
    Rect windowRect = new Rect(150, 150, 220, 564);
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    EditableInt tgtAlt = 0;
    // Re-Entry Burn
    EditableInt reentryBurnAlt = 55000;
    EditableInt reentryBurnTargetSpeed = 700;
    float reentryBurnMaxAoA = 20; // will be set from reentryBurnSteerGain
    float reentryBurnSteerGain = 0.004f; // angle to steer = gain * targetError(in m)
    // Aero descent
    double aeroDescentMaxAoA = 0; // will be set from Kp
    float aeroDescentSteerLogKp = 5.5f;
    float aeroDescentSteerKdProp = 0; // Kd set to this prop. of aeroDescentSteerKp
    // Landing burn
    float landingBurnSteerLogKp = 2.5f;
    double landingBurnMaxAoA = 0; // will be set from Kp
    string numLandingBurnEngines = "current";

    // Advanced settings
    EditableInt touchdownMargin = 10;
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
    bool trackTarget = true; // Continuously track target until params changed

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
      tab = GUILayout.Toolbar(tab, new string[] { "Main", "Advanced" });
      switch(tab)
      {
        case 0:
          MainTab(windowID);
          break;
        case 1:
          AdvancedTab(windowID);
          break;
      }
    }

    void AdvancedTab(int windowID)
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

      if (GUI.changed)
        UpdateController(activeController); // copy settings from window
      GUI.DragWindow();
    }

    void MainTab(int windowID)
    {
      // Check targets are on map vs non-map
      if (map != MapView.MapIsEnabled)
        Awake();

      // Check for vessel change
      if (currentVessel != FlightGlobals.ActiveVessel)
      {
        Debug.Log("[BoosterGuidance] controllers=" + controllers + " activeController=" + activeController);
        try
        {
          activeController = controllers[FlightGlobals.ActiveVessel];
        }
        catch (KeyNotFoundException e)
        {
          Debug.Log("[BoosterGuidance] KeyNotFound " + e.ToString() + " activeController=" + activeController);
          if (activeController != null)
            activeController = new BLController(activeController); // Clone existing
          else
            activeController = new BLController();
          UpdateController(activeController); // Update from window settings
          activeController.AttachVessel(FlightGlobals.ActiveVessel);
          controllers[FlightGlobals.ActiveVessel] = activeController;
        }
        Debug.Log("[BoosterGuidance] Switched vessel name=" + activeController.vessel.name + " enabled=" + activeController.enabled);
        // Pick up settings from controller
        UpdateWindow(activeController); // update window for controller attached to vessel
        RedrawTarget(activeController.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        currentVessel = FlightGlobals.ActiveVessel;
      }
      BLControllerPhase phase = activeController.phase;

      // Check for target being set
      if ((FlightGlobals.ActiveVessel.targetObject != null) && (trackTarget))
      {
        Vessel target = FlightGlobals.ActiveVessel.targetObject.GetVessel();
        tgtLatitude = target.latitude;
        tgtLongitude = target.longitude;
        tgtAlt = (int)target.altitude;
        trackTarget = false; // Only pick up co-ordinates once
      }
      if (FlightGlobals.ActiveVessel.targetObject == null)
        trackTarget = true; // start tracking again when a new target is selected

      // Target:

      // Draw any Controls inside the window here
      GUILayout.Label("Target");//Target coordinates:

      GUILayout.BeginHorizontal();
      double step = 1.0 / (60 * 60); // move by 1 arc second
      tgtLatitude.DrawEditGUI(EditableAngle.Direction.NS);
      if (GUILayout.Button("▲"))
        tgtLatitude += step;
      if (GUILayout.Button("▼"))
        tgtLatitude -= step;
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      tgtLongitude.DrawEditGUI(EditableAngle.Direction.EW);
      if (GUILayout.Button("◄"))
        tgtLongitude -= step;
      if (GUILayout.Button("►"))
        tgtLongitude += step;
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

      // Was target changed manually?
      // if so stop tracking target as this would override the changes
      if (tgtAlt != preTgtAlt)
        trackTarget = false;

      GUILayout.BeginHorizontal();
      showTargets = GUILayout.Toggle(showTargets, "Show targets");
      targetingCross.enabled = showTargets;
      predictedCross.enabled = showTargets;
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
      reentryBurnSteerGain = GUILayout.HorizontalSlider(reentryBurnSteerGain, 0, 0.005f);
      reentryBurnMaxAoA = (reentryBurnSteerGain / 0.005f) * 30;
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
      aeroDescentMaxAoA = 30 * (aeroDescentSteerLogKp / 7);
      GUILayout.Label(((int)aeroDescentMaxAoA).ToString() + "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();
      /*
      GUILayout.BeginHorizontal();
      aeroDescentSteerKdProp = GUILayout.HorizontalSlider(aeroDescentSteerKdProp, 0, 2);
      GUILayout.EndHorizontal();
      */
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
      landingBurnMaxAoA = 30 * (landingBurnSteerLogKp / 7);
      GUILayout.Label(((int)(landingBurnMaxAoA)).ToString() + "°(max)", GUILayout.Width(60));
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if (!activeController.enabled)
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

      if (GUI.changed)
        UpdateController(activeController); // copy settings from window
      GUI.DragWindow();
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
        controller.reentryBurnSteerGain = reentryBurnSteerGain;
        controller.landingBurnMaxAoA = landingBurnMaxAoA;
        controller.tgtLatitude = tgtLatitude;
        controller.tgtLongitude = tgtLongitude;
        controller.tgtAlt = tgtAlt;
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
      }
    }

    // Controller changed - update window
    public void UpdateWindow(BLController controller)
    {
      reentryBurnAlt = (int)controller.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)controller.reentryBurnTargetSpeed;
      // TODO: Read from PID
      aeroDescentSteerLogKp = Mathf.Log((float)controller.aeroDescentSteerKp);
      landingBurnSteerLogKp = Mathf.Log((float)controller.landingBurnSteerKp);
      tgtLatitude = controller.tgtLatitude;
      tgtLongitude = controller.tgtLongitude;
      tgtAlt = (int)controller.tgtAlt;
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
        //SimulateLog(name); // one off simulate down to ground
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
      if (GuiUtils.GetMouseHit(vessel.mainBody, windowRect, MapView.MapIsEnabled, out hit))
      {
        // Moved or picked
        vessel.mainBody.GetLatLonAlt(hit.point, out pickLat, out pickLon, out pickAlt);
        RedrawTarget(vessel.mainBody, pickLat, pickLon, pickAlt);

        if (Input.GetMouseButton(0))  // Picked
        {
          ScreenMessages.PostScreenMessage("Mouse hit - click", 3.0f, ScreenMessageStyle.UPPER_CENTER);
          tgtLatitude = pickLat;
          tgtLongitude = pickLon;
          tgtAlt = (int)pickAlt;
          pickingPositionTarget = false;
          string message = "Picked target";
          ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
          UpdateController(activeController);
        }
        else
          ScreenMessages.PostScreenMessage("Mouse hit - no click", 3.0f, ScreenMessageStyle.UPPER_CENTER);
      }
      else
      {
        ScreenMessages.PostScreenMessage("No mouse hit", 3.0f, ScreenMessageStyle.UPPER_CENTER);
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
      ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }


    void SetTargetHere()
    {
      tgtLatitude = FlightGlobals.ActiveVessel.latitude;
      tgtLongitude = FlightGlobals.ActiveVessel.longitude;
      double lowestY = KSPUtils.FindLowestPointOnVessel(FlightGlobals.ActiveVessel);
      tgtAlt = (int)FlightGlobals.ActiveVessel.altitude + (int)lowestY;
      RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt + activeController.lowestY);
      string message = "Target set to vessel";
      ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }

    Transform RedrawTarget(CelestialBody body, double lat, double lon, double alt)
    {
      Transform transform = GuiUtils.SetUpTransform(body, lat, lon, alt);
      targetingCross.enabled = showTargets;
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
      if (!activeController.enabled)
      {
        Debug.Log("[BoosterGuidance] Enable Guidance for vessel " + FlightGlobals.ActiveVessel.name);
        Vessel vessel = FlightGlobals.ActiveVessel;
        RedrawTarget(vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        vessel.Autopilot.Enable(VesselAutopilot.AutopilotMode.StabilityAssist);
        activeController.AttachVessel(vessel);

        // Find slot for controller
        int i = GetFreeSlot();
        if (i != -1)
        {
          flying[i] = activeController;
          Debug.Log("[BoosterGuidance] Allocating slot " + i + " name=" + vessel.name + " to " + activeController);
        }
        else
        {
          ScreenMessages.PostScreenMessage("All " + flying.Length + " guidance slots used", 3.0f, ScreenMessageStyle.UPPER_CENTER);
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
      string info = "Enabled " + activeController.PhaseStr();
      ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
      activeController.enabled = true;
      StartLogging();
    }


    void DisableGuidance(BLController controller)
    {
      if (controller.enabled)
      {
        Vessel vessel = controller.vessel;
        vessel.Autopilot.Disable();
        int i = GetSlotFromVessel(vessel);
        Debug.Log("[BoosterGuidance] DisableGuidance() slot=" + i);
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
          ScreenMessages.PostScreenMessage("Guidance disabled!", 3.0f, ScreenMessageStyle.UPPER_CENTER);
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
      if (vessel.checkLanded())
      {
        string msg = "Vessel " + controller.vessel.name + " landed!";
        ScreenMessages.PostScreenMessage(msg, 3.0f, ScreenMessageStyle.UPPER_CENTER);
        state.mainThrottle = 0;
        DisableGuidance(controller);
        // Find distance from target
        if (activeController == controller)
          info = string.Format("Landed {0:F1}m from target", controller.targetError);
        return;
      }

      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);

      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);

      bool landingGear, gridFins;
      controller.GetControlOutputs(vessel, vessel.GetTotalMass(), vessel.GetWorldPos3D(), vessel.GetObtVelocity(), vessel.transform.up, vessel.altitude, minThrust, maxThrust,
        controller.vessel.missionTime, vessel.mainBody, tgt_r, false, out throttle, out steer, out landingGear, out gridFins);
      //Debug.Log("[BoosterGuidance] alt=" + controller.vessel.altitude + " gear_height=" + controller.deployLandingGearHeight + " deploy=" + controller.deployLandingGear+" deploy_now="+landingGear);
      if ((landingGear) && KSPUtils.DeployLandingGears(vessel))
        ScreenMessages.PostScreenMessage("Deploying landing gear", 1.0f, ScreenMessageStyle.UPPER_CENTER);
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

      if ((tgtLatitude == 0) && (tgtLongitude == 0) && (tgtAlt == 0))
      { 
        // No target. Set target to below craft
        controller.tgtLatitude = vessel.latitude;
        controller.tgtLongitude = vessel.longitude;
        controller.tgtAlt = vessel.mainBody.TerrainAltitude(tgtLatitude, tgtLongitude);
        if (activeController == controller)
        {
          UpdateWindow(controller);
          RedrawTarget(controller.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
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
        info = string.Format("Err: {0:F0}m {1:F0}° Time: {2:F0}s [{3:F0}ms]", controller.targetError, controller.attitudeError, controller.targetT, controller.elapsed_secs * 1000);

      }
      state.mainThrottle = (float)throttle;
      vessel.Autopilot.SAS.lockedMode = false;
      vessel.Autopilot.SAS.SetTargetOrientation(steer, false);
    }
  }
}