using System;
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
    BLController controller = null;
    Rect windowRect = new Rect(150, 150, 220, 516);
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    double tgtAlt = 0;
    // Boostbback
    // Re-Entry Burn
    EditableInt reentryBurnAlt = 70000;
    EditableInt reentryBurnTargetSpeed = 700;
    EditableInt reentryBurnMaxAoA = 10;
    // Aero descent
    EditableInt aeroDescentMaxAoA = 10;
    // Powered descent
    EditableInt poweredDescentMaxAoA = 10;
    float suicideFactor = 0.8f;

    bool showTargets = true;
    bool logging = false;
    bool pickingPositionTarget = false;
    string info = "Disabled";
    //private Vessel vessel = null;
    float tgtSize = 0.1f;
    Transform _transform = null;
    GameObject target_obj = null;
    GameObject pred_obj = null;
    double pickLat, pickLon, pickAlt;
    double touchdownMargin = 30; // Slow to touchdown speed this much above target

    public void OnGUI()
    {
      if (!hidden)
        windowRect = GUI.Window(0, windowRect, WindowFunction, "Booster Guidance");
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
      info = FlightGlobals.ActiveVessel.name;
      SetEnabledColors(true);
      // Close button
      if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
      {
        hidden = true;
        return;
      }

      BLControllerPhase phase = BLControllerPhase.Unset;
      if (controller != null)
        phase = controller.phase;

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
      showTargets = GUILayout.Toggle(showTargets, "Show targets");
      bool prevLogging = logging;
      logging = GUILayout.Toggle(logging, "Logging");
      if ((!prevLogging) && (logging))
        StartLogging();
      if ((prevLogging) && (!logging))
        StopLogging();
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
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Enable altitude", reentryBurnAlt, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Target speed", reentryBurnTargetSpeed, "m/s", 40);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
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
      GUILayout.Space(10);
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
      GUILayout.Label("Suicide factor:");
      // 0 means very conservative suicide burn (~40% less than fastest poss. velocity at height)
      // 1 means perfect suicide burn
      suicideFactor = GUILayout.HorizontalSlider(suicideFactor, 0, 1, GUILayout.Width(60));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Max Angle-of-Attack", poweredDescentMaxAoA, "°", 25);
      if (GUILayout.Button("▼"))
        poweredDescentMaxAoA = Math.Max(0, poweredDescentMaxAoA-1);
      if (GUILayout.Button("▲"))
        poweredDescentMaxAoA += 1;
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if (controller == null)
      {
        if (GUILayout.Button("Enable Guidance"))
          EnableGuidance(controller.phase);
      }
      else
      {
        if (GUILayout.Button("Disable Guidance"))
          DisableGuidance();
      }

      GUILayout.EndHorizontal();

      if (GUI.changed) // tgtSize might be changed
      {
        if (controller != null)
          UpdateController(controller);
        RedrawTarget(pickLat, pickLon, pickAlt);
      }
      GUI.DragWindow();
    }


    public void ToggleVisibility()
    {
      hidden = !hidden;
    }

    public void UpdateController(BLController controller)
    {
      double lowestY = KSPUtils.FindLowestPointOnVessel(controller.vessel);
      if (controller != null)
      {
        controller.reentryBurnAlt = reentryBurnAlt;
        controller.reentryBurnTargetSpeed = reentryBurnTargetSpeed;
        controller.reentryBurnMaxAoA = reentryBurnMaxAoA;
        controller.aeroDescentMaxAoA = aeroDescentMaxAoA;
        controller.poweredDescentMaxAoA = poweredDescentMaxAoA;
        controller.SetTarget(tgtLatitude, tgtLongitude, tgtAlt - lowestY + touchdownMargin);
        controller.suicideFactor = suicideFactor;
      }
    }

    void SimulateLog()
    {
      double T;
      if (controller == null)
        return;
      var vessel = controller.vessel;
      var aeroModel = Trajectories.AerodynamicModelFactory.GetModel(vessel, vessel.mainBody);
      _transform = RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      BLController tc = new BLController(controller);
      Vector3d tgt_r = vessel.mainBody.GetWorldSurfacePosition(tgtLatitude, tgtLongitude, tgtAlt);
      tc.noCorrect = true;
      Simulate.ToGround(tgtAlt, vessel, aeroModel, vessel.mainBody, tc, tgt_r, out T, "simulate_with_control.dat", _transform);
      Simulate.ToGround(tgtAlt, vessel, aeroModel, vessel.mainBody, null, tgt_r, out T, "simulate_without_control.dat", _transform);
    }

    void StartLogging()
    {
      SimulateLog(); // one off simulate down to ground
      if (controller != null)
        controller.StartLogging("vessel.dat", _transform);
    }

    void StopLogging()
    {
      if (controller != null)
        controller.StopLogging();
    }

    void OnUpdate()
    {
      // Redraw targets
      if (!pickingPositionTarget)
      {
        RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      }
      else
      {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
          // Previous position
          RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
          pickingPositionTarget = false;
        }
        RaycastHit hit;
        Vessel vessel = FlightGlobals.ActiveVessel;
        if (GuiUtils.GetMouseHit(vessel.mainBody, windowRect, out hit))
        {
          // Moved or picked
          vessel.mainBody.GetLatLonAlt(hit.point, out pickLat, out pickLon, out pickAlt);
          RedrawTarget(pickLat, pickLon, pickAlt);

          if (Input.GetMouseButton(0))  // Picked
          {
            tgtLatitude = pickLat;
            tgtLongitude = pickLon;
            tgtAlt = pickAlt;
            pickingPositionTarget = false;
            string message = "Picked target";
            ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
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
      Vessel vessel = FlightGlobals.ActiveVessel;
      double lowestY = KSPUtils.FindLowestPointOnVessel(vessel);
      tgtAlt = vessel.altitude + lowestY;
      RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      pickLat = tgtLatitude;
      pickLon = tgtLongitude;
      pickAlt = tgtAlt;
      string message = "Target set to vessel";
      ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }

    Transform RedrawTarget(double lat, double lon, double alt)
    {
      Vessel vessel = FlightGlobals.ActiveVessel;
      if (target_obj != null)
        Destroy(target_obj);
      target_obj = null;
      Transform transform = GuiUtils.SetUpTransform(vessel.mainBody, lat, lon, alt);
      if (showTargets)
      {
        Vector3 heading = transform.position - Camera.main.transform.position;
        float distance = Vector3.Dot(heading, Camera.main.transform.forward);
        target_obj = GuiUtils.DrawTarget(Vector3d.zero, transform, tgt_color, distance * tgtSize, 0);
      }
      return transform;
    }

    Transform RedrawPrediction(double lat, double lon, double alt)
    {
      if (controller != null)
      {
        if (pred_obj != null)
          Destroy(pred_obj);
        pred_obj = null;
        Transform transform = GuiUtils.SetUpTransform(controller.vessel.mainBody, lat, lon, alt);
        if (showTargets)
        {
          Vector3 heading = transform.position - Camera.main.transform.position;
          float distance = Vector3.Dot(heading, Camera.main.transform.forward);
          pred_obj = GuiUtils.DrawTarget(Vector3d.zero, transform, pred_color, distance * tgtSize, 0);
        }
        return transform;
      }
      return null;
    }

    void EnableGuidance(BLControllerPhase phase)
    {
      if (controller == null)
      {
        Vessel vessel = FlightGlobals.ActiveVessel;
        _transform = RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
        vessel.Autopilot.Enable(VesselAutopilot.AutopilotMode.StabilityAssist);
        // Initialise controller
        controller = new BLController(vessel);
        UpdateController(controller);
        vessel.OnFlyByWire += new FlightInputCallback(Fly); // 1st vessel
        controller.SetPhase(phase);
        string info = "Enabled " + controller.PhaseStr();
        ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
      }
    }


    void DisableGuidance()
    {
      if (controller != null)
      {
        Vessel vessel = controller.vessel;
        vessel.Autopilot.Disable();
        vessel.OnFlyByWire -= new FlightInputCallback(Fly);
        controller.StopLogging();
        controller = null;
        vessel = null;
        info = "Guidance disabled!";
        ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
        if (pred_obj != null)
          Destroy(pred_obj);
      }
    }

    public void Fly(FlightCtrlState state)
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
        string msg = "Landed!";
        ScreenMessages.PostScreenMessage(msg, 3.0f, ScreenMessageStyle.UPPER_CENTER);
        state.mainThrottle = 0;
        DisableGuidance();
        // Find distance from target
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

      if (shutdownEnginesNow)
      {
        Debug.Log("[BoosterGuidance] shutdownEnginesNow=true");
        // Request hovering thrust
        KSPUtils.ShutdownOuterEngines(vessel, (float)(FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude * vessel.totalMass), true);
      }

      if (_transform == null)
      {
        // No target. Set target to below craft
        tgtLatitude = vessel.latitude;
        tgtLongitude = vessel.longitude;
        tgtAlt = vessel.mainBody.TerrainAltitude(tgtLatitude, tgtLongitude);
        _transform = RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      }

      // Draw predicted position
      if (_transform != null)
      {
        double lat, lon, alt;
        // prediction is for position of planet at current time compensating for
        // planet rotation
        vessel.mainBody.GetLatLonAlt(controller.predWorldPos, out lat, out lon, out alt);
        alt = vessel.mainBody.TerrainAltitude(lat, lon); // Make on surface
        RedrawPrediction(lat, lon, alt + 5);
      }
      info = string.Format("Tgt error: {0:F0}m Time: {1:F0}s", controller.targetError, controller.targetT);
      state.mainThrottle = (float)throttle;
      vessel.Autopilot.SAS.lockedMode = false;
      vessel.Autopilot.SAS.SetTargetOrientation(steer, false);
    }
  }
}