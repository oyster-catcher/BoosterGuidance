using System;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;
using UnityEngine.Profiling;

namespace BoosterGuidance
{
  public class MainWindow : MonoBehaviour
  {
    // constants
    Color tgt_color = new Color(1, 1, 0, 0.5f);
    Color pred_color = new Color(1, 0.2f, 0.2f, 0.5f);

    // private
    bool hidden = true;
    private static GuiUtils.TargetingCross targetingCross;
    private static GuiUtils.TargetingCross predictedCross;
    BLController activeController = null;
    DictionaryValueList<Vessel, BLController> controllers = new DictionaryValueList<Vessel, BLController>();
    BLController[] flying = { null, null, null, null, null }; // To connect to Fly() functions. Must be 5 or change EnableGuidance()
    Rect windowRect = new Rect(150, 150, 220, 596);
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    EditableInt tgtAlt = 0;
    // Re-Entry Burn
    EditableInt reentryBurnAlt = 70000;
    EditableInt reentryBurnTargetSpeed = 700;
    EditableInt reentryBurnMaxAoA = 10;
    float reentryBurnSteerGain = 0.004f; // angle to steer = gain * targetError(in m)
    // Aero descent
    EditableInt aeroDescentMaxAoA = 10;
    float aeroDescentSteerKp = 250;
    float aeroDescentSteerKd = 0;
    // Powered descent
    EditableInt poweredDescentMaxAoA = 10;
    float poweredDescentSteerKp = 250;

    Vessel currentVessel = null; // to detect vessel switch
    bool showTargets = true;
    bool logging = false;
    bool pickingPositionTarget = false;
    string info = "Disabled";
    double pickLat, pickLon, pickAlt;
    bool trackTarget = true; // Continuously track target until params changed

    // GUI Elements
    Color red = new Color(1, 0, 0, 0.5f);
    GameObject steer_obj = null;
    LineRenderer steer_line = null;

    public MainWindow()
    {
      Awake();
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
      targetingCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<GuiUtils.TargetingCross>();
      targetingCross.SetColor(Color.yellow);
      predictedCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<GuiUtils.TargetingCross>();
      predictedCross.SetColor(Color.red);
    }

    public void OnDestroy()
    {
      Debug.Log("OnDestroy");
      if (targetingCross != null)
        Destroy(targetingCross);
      targetingCross = null;
      if (predictedCross != null)
        Destroy(predictedCross);
      predictedCross = null;
      if (steer_obj != null)
        Destroy(steer_obj);
      steer_obj = null;
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
        hidden = true;
        return;
      }

      // Check for vessel change
      if (currentVessel != FlightGlobals.ActiveVessel)
      {
        Debug.Log("[BoosterGuidance] controllers=" + controllers + " activeController="+activeController);
        try
        {
          activeController = controllers[FlightGlobals.ActiveVessel];
        }
        catch (KeyNotFoundException e)
        {
          Debug.Log("[BoosterGuidance] KeyNotFound activeController="+activeController);
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
      //GUILayout.Label(Localizer.Format("#BoosterGuidance_Label_Target"));//Target coordinates:
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
      // TODO - Need to be specific to controller so works when switching vessel
      bool prevLogging = logging;
      logging = GUILayout.Toggle(logging, "Logging");
      if ((!prevLogging) && (logging)) // logging switched on
        StartLogging();
      if ((prevLogging) && (!logging)) // logging switched off
      {
        Debug.Log("[BoosterGuidance] prevLogging=" + prevLogging + " logging=" + logging);
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
      if (GUILayout.Button(new GUIContent("Boostback","Enable thrust towards target when out of atmosphere")))
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
      //GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Enable altitude", reentryBurnAlt, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      //GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Target speed", reentryBurnTargetSpeed, "m/s", 40);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Gain", GUILayout.Width(30));
      reentryBurnSteerGain = GUILayout.HorizontalSlider(reentryBurnSteerGain, 0, 0.005f);
      //GUILayout.Label(reentryBurnSteerGain.ToString(), GUILayout.Width(20));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      //GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Max Angle-of-Attack", reentryBurnMaxAoA, "°", 25);
      if (GUILayout.Button("▼"))
        reentryBurnMaxAoA -= 1;
      if (GUILayout.Button("▲"))
        reentryBurnMaxAoA += 1;
      GUILayout.EndHorizontal();

      // Aero Descent
      SetEnabledColors((phase == BLControllerPhase.AeroDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("Aero Descent", "No thrust aerodynamic descent, steering with gridfins within atmosphere")))
        EnableGuidance(BLControllerPhase.AeroDescent);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer Kp", GUILayout.Width(60));
      aeroDescentSteerKp = GUILayout.HorizontalSlider(aeroDescentSteerKp, 0, 500);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      //GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Max Angle-of-Attack", aeroDescentMaxAoA, "°", 25);
      if (GUILayout.Button("▼"))
        aeroDescentMaxAoA = Math.Max(0, aeroDescentMaxAoA-1);
      if (GUILayout.Button("▲"))
        aeroDescentMaxAoA += 1;
      GUILayout.EndHorizontal();

      // Powered Descent
      SetEnabledColors((phase == BLControllerPhase.PoweredDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Powered Descent"))
        EnableGuidance(BLControllerPhase.PoweredDescent);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Steer Kp", GUILayout.Width(60));
      poweredDescentSteerKp = GUILayout.HorizontalSlider(poweredDescentSteerKp, 0, 500);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      //GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Max Angle-of-Attack", poweredDescentMaxAoA, "°", 25);
      if (GUILayout.Button("▼"))
        poweredDescentMaxAoA = Math.Max(0, poweredDescentMaxAoA-1);
      if (GUILayout.Button("▲"))
        poweredDescentMaxAoA += 1;
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if (!activeController.enabled)
      {
        if (GUILayout.Button("Enable Guidance"))
          EnableGuidance(BLControllerPhase.BoostBack);
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
    }

    public void Hide()
    {
      hidden = true;
    }

    public void UpdateController(BLController controller)
    {
      if (controller != null)
      {
        controller.reentryBurnAlt = reentryBurnAlt;
        controller.reentryBurnTargetSpeed = reentryBurnTargetSpeed;
        controller.reentryBurnSteerGain = reentryBurnSteerGain;
        controller.poweredDescentMaxAoA = poweredDescentMaxAoA;
        controller.tgtLatitude = tgtLatitude;
        controller.tgtLongitude = tgtLongitude;
        controller.tgtAlt = tgtAlt;
        controller.suicideFactor = 0.75;
        controller.poweredDescentSteerKp = poweredDescentSteerKp;
        controller.aeroDescentSteerKp = aeroDescentSteerKp;
        // Note that the Kp gain in the PIDs below is set by combining the relevant Kp from above
        // and a gain factor based on air resistance an throttle to determine whether to steer
        // aerodynamically or by thrust, and how sensitive the vessel is to that
        controller.pid_aero = new PIDclamp("aero", 1, 0, 0, aeroDescentMaxAoA);
        controller.pid_powered = new PIDclamp("powered", 1, 0, 0, poweredDescentMaxAoA);
      }
    }

    // Controller changed - update window
    public void UpdateWindow(BLController controller)
    {
      reentryBurnAlt = (int)controller.reentryBurnAlt;
      reentryBurnTargetSpeed = (int)controller.reentryBurnTargetSpeed;
      reentryBurnMaxAoA = (int)controller.reentryBurnMaxAoA;
      // TODO: Read from PID
      aeroDescentSteerKp = (float)controller.aeroDescentSteerKp;
      poweredDescentMaxAoA = (int)controller.poweredDescentMaxAoA;
      tgtLatitude = controller.tgtLatitude;
      tgtLongitude = controller.tgtLongitude;
      tgtAlt = (int)controller.tgtAlt;
    }

    void SimulateLog()
    {
      double T;
      if (activeController == null)
        return;
      var vessel = activeController.vessel;
      var aeroModel = Trajectories.AerodynamicModelFactory.GetModel(vessel, vessel.mainBody);
      Transform logTransform = RedrawTarget(vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
      BLController tc = new BLController(activeController);
      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);
      tc.noCorrect = true;
      string name = activeController.vessel.name;
      Simulate.ToGround(tgtAlt, vessel, aeroModel, vessel.mainBody, tc, tgt_r, out T, "simulate_without_boostback.dat", logTransform);
      Simulate.ToGround(tgtAlt, vessel, aeroModel, vessel.mainBody, null, tgt_r, out T, "simulate_without_control.dat", logTransform);
    }

    void StartLogging()
    {
      SimulateLog(); // one off simulate down to ground
      if (activeController != null)
      {
        Transform logTransform = RedrawTarget(activeController.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
        string name = activeController.vessel.name;
        activeController.StartLogging(name+".actual.dat", logTransform);
      }
    }

    void StopLogging()
    {
      if (activeController != null)
        activeController.StopLogging();
    }

    void OnUpdate()
    {
      // Redraw targets
      if (!pickingPositionTarget)
      {
        // Need to redraw as size changes (may be less often)
        //Debug.Log("RedrawTarget mapView="+MapView.MapIsEnabled);
        RedrawTarget(FlightGlobals.ActiveVessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
      }
      else
      {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
          // Previous position
          RedrawTarget(activeController.vessel.mainBody, tgtLatitude, tgtLongitude, tgtAlt);
          pickingPositionTarget = false;
        }
        RaycastHit hit;
        Vessel vessel = FlightGlobals.ActiveVessel;
        if (GuiUtils.GetMouseHit(vessel.mainBody, windowRect, out hit))
        {
          // Moved or picked
          vessel.mainBody.GetLatLonAlt(hit.point, out pickLat, out pickLon, out pickAlt);
          RedrawTarget(vessel.mainBody, pickLat, pickLon, pickAlt);

          if (Input.GetMouseButton(0))  // Picked
          {
            tgtLatitude = pickLat;
            tgtLongitude = pickLon;
            tgtAlt = (int)pickAlt;
            pickingPositionTarget = false;
            string message = "Picked target";
            ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
            UpdateController(activeController);
          }
        }
      }
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
      tgtAlt = (int)FlightGlobals.ActiveVessel.altitude;
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
      //string info = "Enabled " + activeController.PhaseStr() + "(slot " + i + ")";
      string info = "Enabled " + activeController.PhaseStr();
      ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
      activeController.enabled = true;
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
          Destroy(predictedCross);
          predictedCross.enabled = false;
          if (steer_obj != null)
            Destroy(steer_obj);
          steer_obj = null;
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
      double amin;
      double amax;
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
      amin = minThrust / vessel.totalMass;
      amax = maxThrust / vessel.totalMass;
      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);
      bool shutdownEnginesNow;
      controller.GetControlOutputs(vessel, vessel.GetWorldPos3D(), vessel.GetObtVelocity(), vessel.transform.up, vessel.altitude, amin, amax,
        Time.time, vessel.mainBody, tgt_r, out throttle, out steer, out shutdownEnginesNow);

      //
      GuiUtils.DrawVector(ref steer_obj, ref steer_line, Vector3d.zero, steer*40, null, red, showTargets);

      if (shutdownEnginesNow)
      {
        Debug.Log("[BoosterGuidance] Shutting down outer engines");
        // Request hovering thrust
        KSPUtils.ShutdownOuterEngines(vessel, (float)(FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude * vessel.totalMass), true);
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
        RedrawPrediction(vessel.mainBody, lat, lon, alt + 1); // 1m above ground to avoid getting hidden
        info = string.Format("Tgt error: {0:F0}m Time: {1:F0}s", controller.targetError, controller.targetT);
      }
      state.mainThrottle = (float)throttle;
      vessel.Autopilot.SAS.lockedMode = false;
      vessel.Autopilot.SAS.SetTargetOrientation(steer, false);
    }
  }
}