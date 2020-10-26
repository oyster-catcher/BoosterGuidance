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
    bool moreOptions = true;
    BLController controller = null;
    Rect windowRect = new Rect(150, 150, 220, 480);
    EditableAngle tgtLatitude = 0;
    EditableAngle tgtLongitude = 0;
    double tgtAlt = 0;
    EditableInt angleOfElevation = 0;
    EditableInt aeroDescentAlt = 50000;
    EditableInt poweredDescentAlt = 10000;
    EditableInt maxAoA = 10;
    EditableInt landingGearAlt = 100;
    bool deployAirbrakes = true;
    bool pickingPositionTarget = false;
    string info = "Disabled";
    private Vessel vessel = null;
    float tgtSize = 3;
    Transform _transform = null;
    GameObject target_obj = null;
    GameObject pred_obj = null;
    double pickLat, pickLon, pickAlt;

    public void OnGUI()
    {
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
      SetEnabledColors(true);
      // Close button
      GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), "");

      BLControllerPhase phase = BLControllerPhase.Unset;
      if (controller != null)
        phase = controller.phase;

      // Target:

      // Draw any Controls inside the window here
      //GUILayout.Label(Localizer.Format("#BoosterGuidance_Label_Target"));//Target coordinates:
      GUILayout.Label("TARGET");//Target coordinates:

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
      GUILayout.Label("Size:", GUILayout.Width(30));
      tgtSize = GUILayout.HorizontalSlider(tgtSize, 3, 12);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Pick Target"))
        PickTarget();
      if (GUILayout.Button("Set Here"))
        SetTargetHere();

      GUILayout.EndHorizontal();

      // Info box
      GUILayout.BeginHorizontal();
      GUILayout.Label(info);
      GUILayout.EndHorizontal();

      // Boostback
      SetEnabledColors((phase == BLControllerPhase.BoostBack) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("BOOSTBACK","Enable thrust towards target when out of atmosphere")))
        EnableGuidance(BLControllerPhase.BoostBack);
      GUILayout.EndHorizontal();

   
      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Elevation angle", angleOfElevation, "°", 25);
      if (GUILayout.Button("▼"))
        angleOfElevation -= 1;
      if (GUILayout.Button("▲"))
        angleOfElevation += 1;
      GUILayout.EndHorizontal();

      // Coasting
      SetEnabledColors((phase == BLControllerPhase.Coasting) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("COASTING", "Turn to retrograde attitude and wait for Aero Descent phase")))
        EnableGuidance(BLControllerPhase.Coasting);
      GUILayout.EndHorizontal();

      // Aero Descent
      SetEnabledColors((phase == BLControllerPhase.AeroDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(new GUIContent("AERO DESCENT", "No thrust aerodynamic descent, steering with gridfins within atmosphere")))
        EnableGuidance(BLControllerPhase.AeroDescent);
      GUILayout.EndHorizontal();

 
      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      deployAirbrakes = GUILayout.Toggle(deployAirbrakes, new GUIContent("Deploy airbrakes", "Deploy airbrakes when this phase in enabled"));
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Enable altitude", aeroDescentAlt, "m", 65);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Max Angle-of-Attack", maxAoA, "°", 25);
      if (GUILayout.Button("▼"))
        maxAoA -= 1;
      if (GUILayout.Button("▲"))
        maxAoA += 1;
      GUILayout.EndHorizontal();

      // Powered Descent
      SetEnabledColors((phase == BLControllerPhase.PoweredDescent) || (phase == BLControllerPhase.Unset));
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("POWERED DESCENT"))
        EnableGuidance(BLControllerPhase.PoweredDescent);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Enable altitude", poweredDescentAlt, "m", 60);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Space(10);
      GuiUtils.SimpleTextBox("Landing gear alt.", landingGearAlt, "m", 60);
      GUILayout.EndHorizontal();

      // Activate guidance
      SetEnabledColors(true); // back to normal
      GUILayout.BeginHorizontal();
      if (controller == null)
      {
        BLController tmpController = new BLController(FlightGlobals.ActiveVessel);
        tmpController.aeroDescentAlt = aeroDescentAlt;
        tmpController.poweredDescentAlt = poweredDescentAlt;
        double throttle;
        Vector3d steer;
        tmpController.GetControlOutputs(FlightGlobals.ActiveVessel, Time.time, out throttle, out steer);
        string strphase = "BOOSTBACK";
        if (tmpController.phase == BLControllerPhase.Coasting)
          strphase = "COASTING";
        if (tmpController.phase == BLControllerPhase.AeroDescent)
          strphase = "AERO DESCENT";
        if (tmpController.phase == BLControllerPhase.PoweredDescent)
          strphase = "POWERED DESCENT";
        if (GUILayout.Button("Enable " + strphase))
          EnableGuidance(tmpController.phase);
      }
      else
      {
        if (GUILayout.Button("Disable Guidance"))
          DisableGuidance();
      }
      GUILayout.EndHorizontal();

      if (GUI.changed) // tgtSize might be changed
      {
        Debug.Log("GUI.changed=true");
        RedrawTarget(pickLat, pickLon, pickAlt);
      }

      GUI.DragWindow();
    }


    public void ToggleVisibility()
    {
    }


    void OnUpdate()
    {
      if (pickingPositionTarget)
      {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
          // Previous position
          RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
          pickingPositionTarget = false;
        }
        RaycastHit hit;
        vessel = FlightGlobals.ActiveVessel;
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
          }
        }
      }
    }


    void PickTarget()
    {
      pickingPositionTarget = true;
      string message = "Click to select a target";
      ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }


    void SetTargetHere()
    {
      tgtLatitude = FlightGlobals.ActiveVessel.latitude;
      tgtLongitude = FlightGlobals.ActiveVessel.longitude;
      vessel = FlightGlobals.ActiveVessel;
      double lowestY = KSPUtils.FindLowestPointOnVessel(vessel);
      tgtAlt = vessel.altitude + lowestY;
      RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      pickLat = tgtLatitude;
      pickLon = tgtLongitude;
      pickAlt = tgtAlt;
    }

    Transform RedrawTarget(double lat, double lon, double alt)
    {
      vessel = FlightGlobals.ActiveVessel;
      if (target_obj != null)
        Destroy(target_obj);
      Transform transform = GuiUtils.SetUpTransform(vessel.mainBody, lat, lon, alt);
      target_obj = GuiUtils.DrawTarget(Vector3d.zero, transform, tgt_color, Math.Pow(2, tgtSize), 0);
      return transform;
    }

    Transform RedrawPrediction(double lat, double lon, double alt)
    {
      vessel = FlightGlobals.ActiveVessel;
      if (pred_obj != null)
        Destroy(pred_obj);
      Transform transform = GuiUtils.SetUpTransform(vessel.mainBody, lat, lon, alt);
      pred_obj = GuiUtils.DrawTarget(Vector3d.zero, transform, pred_color, Math.Pow(2, tgtSize), 0);
      return transform;
    }

    void EnableGuidance(BLControllerPhase phase)
    {
      if (controller != null)
        vessel.OnFlyByWire -= new FlightInputCallback(Fly);
      _transform = RedrawTarget(tgtLatitude, tgtLongitude, tgtAlt);
      vessel = FlightGlobals.ActiveVessel;
      vessel.Autopilot.Enable(VesselAutopilot.AutopilotMode.StabilityAssist);
      vessel.OnFlyByWire += new FlightInputCallback(Fly);
      // Initialise controller
      controller = new BLController(vessel);
      double lowestY = KSPUtils.FindLowestPointOnVessel(vessel);
      Debug.Log("tgtAlt=" + tgtAlt + " lowestY=" + lowestY);
      controller.SetTarget(tgtLatitude, tgtLongitude, tgtAlt - lowestY);
      controller.poweredDescentAlt = poweredDescentAlt.val;
      controller.aeroDescentAlt = aeroDescentAlt.val;
      controller.maxAoA = maxAoA;
      controller.SetPhase(phase);
      string info = "Enabled Guidance";
      if (phase == BLControllerPhase.BoostBack)
        info = "Enabled Boostback";
      if (phase == BLControllerPhase.AeroDescent)
        info = "Enabled Aero Descent";
      if (phase == BLControllerPhase.PoweredDescent)
        info = "Enabled Powered Descent";
      if (phase == BLControllerPhase.Coasting)
        info = "Enabled Coasting";
      ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }


    void DisableGuidance()
    {
      vessel.Autopilot.Disable();
      vessel.OnFlyByWire -= new FlightInputCallback(Fly);
      controller = null;
      vessel = null;
      info = "Guidance disabled!";
      ScreenMessages.PostScreenMessage(info, 3.0f, ScreenMessageStyle.UPPER_CENTER);
      if (pred_obj != null)
        Destroy(pred_obj);
    }


    public void Fly(FlightCtrlState state)
    {
      double throttle;
      Vector3d steer;
      controller.GetControlOutputs(vessel, Time.time, out throttle, out steer);

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
        Debug.Log("Predicted: lat=" + lat + " lon=" + lon + " alt=" + alt);
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