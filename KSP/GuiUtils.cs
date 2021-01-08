using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Trajectories;
using Object = UnityEngine.Object;


namespace BoosterGuidance
{
    public interface IEditable
    {
        string text { get; set; }
    }

    //An EditableDouble stores a double value and a text string. The user can edit the string.
    //Whenever the text is edited, it is parsed and the parsed value is stored in val. As a
    //convenience, a multiplier can be specified so that the stored value is actually
    //(multiplier * parsed value). If the parsing fails, the parsed flag is set to false and
    //the stored value is unchanged. There are implicit conversions between EditableDouble and
    //double so that if you are not doing text input you can treat an EditableDouble like a double.
    public class EditableDoubleMult : IEditable
    {
        [Persistent]
        protected double _val;
        public virtual double val
        {
            get { return _val; }
            set
            {
                _val = value;
                _text = (_val / multiplier).ToString();
            }
        }
        public readonly double multiplier;

        public bool parsed;
        [Persistent]
        protected string _text;
        public virtual string text
        {
            get { return _text; }
            set
            {
                _text = value;
                _text = Regex.Replace(_text, @"[^\d+-.]", ""); //throw away junk characters
                double parsedValue;
                parsed = double.TryParse(_text, out parsedValue);
                if (parsed) _val = parsedValue * multiplier;
            }
        }

        public EditableDoubleMult() : this(0) { }

        public EditableDoubleMult(double val, double multiplier = 1)
        {
            this.val = val;
            this.multiplier = multiplier;
            _text = (val / multiplier).ToString();
        }

        public static implicit operator double(EditableDoubleMult x)
        {
            return x.val;
        }
    }

    public class EditableDouble : EditableDoubleMult
    {
        public EditableDouble(double val)
            : base(val)
        {
        }

        public static implicit operator EditableDouble(double x)
        {
            return new EditableDouble(x);
        }
    }

    public class EditableAngle
    {
        [Persistent]
        public EditableDouble degrees = 0;
        [Persistent]
        public EditableDouble minutes = 0;
        [Persistent]
        public EditableDouble seconds = 0;
        [Persistent]
        public bool negative;

        public EditableAngle(double angle)
        {
            angle = MuUtils.ClampDegrees180(angle);

            negative = (angle < 0);
            angle = Math.Abs(angle);
            degrees = (int)angle;
            angle -= degrees;
            minutes = (int)(60 * angle);
            angle -= minutes / 60;
            seconds = Math.Round(3600 * angle);
        }

        public static implicit operator double(EditableAngle x)
        {
            return (x.negative ? -1 : 1) * (x.degrees + x.minutes / 60.0 + x.seconds / 3600.0);
        }

        public static implicit operator EditableAngle(double x)
        {
            return new EditableAngle(x);
        }

        public enum Direction { NS, EW }

        public void DrawEditGUI(Direction direction)
        {
            GUILayout.BeginHorizontal();
            degrees.text = GUILayout.TextField(degrees.text, GUILayout.Width(30));
            GUILayout.Label("°", GUILayout.ExpandWidth(false));
            minutes.text = GUILayout.TextField(minutes.text, GUILayout.Width(30));
            GUILayout.Label("'", GUILayout.ExpandWidth(false));
            seconds.text = GUILayout.TextField(seconds.text, GUILayout.Width(30));
            GUILayout.Label("\"", GUILayout.ExpandWidth(false));
            String dirString = (direction == Direction.NS ? (negative ? "S" : "N") : (negative ? "W" : "E"));
            if (GUILayout.Button(dirString, GUILayout.Width(25))) negative = !negative;
            GUILayout.EndHorizontal();
        }
    }

    public class EditableInt : IEditable
    {
        [Persistent]
        public int val;

        public bool parsed;
        [Persistent]
        public string _text;
        public virtual string text
        {
            get { return _text; }
            set
            {
                _text = value;
                _text = Regex.Replace(_text, @"[^\d+-]", ""); //throw away junk characters
                int parsedValue;
                parsed = int.TryParse(_text, out parsedValue);
                if (parsed) val = parsedValue;
            }
        }

        public EditableInt() : this(0) { }

        public EditableInt(int val)
        {
            this.val = val;
            _text = val.ToString();
        }

        public static implicit operator int(EditableInt x)
        {
            return x.val;
        }

        public static implicit operator EditableInt(int x)
        {
            return new EditableInt(x);
        }
    }

  public static class GuiUtils
  {
    public static void SimpleTextBox(string leftLabel, IEditable ed, string rightLabel = "", float width = 100, GUIStyle rightLabelStyle = null)
    {
      if (rightLabelStyle == null)
        rightLabelStyle = GUI.skin.label;
      GUILayout.BeginHorizontal();
      GUILayout.Label(leftLabel, rightLabelStyle, GUILayout.ExpandWidth(true));
      ed.text = GUILayout.TextField(ed.text, GUILayout.ExpandWidth(true), GUILayout.Width(width));
      GUILayout.Label(rightLabel, GUILayout.ExpandWidth(false));
      GUILayout.EndHorizontal();
    }

    public static void SimpleLabel(string leftLabel, string rightLabel = "")
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(leftLabel, GUILayout.ExpandWidth(true));
      GUILayout.Label(rightLabel, GUILayout.ExpandWidth(false));
      GUILayout.EndHorizontal();
    }

    public static void SimpleLabelInt(string leftLabel, int rightValue)
    {
      SimpleLabel(leftLabel, rightValue.ToString());
    }

    public static int ArrowSelector(int index, int numIndices, Action centerGuiAction)
    {
      if (numIndices == 0) return index;

      GUILayout.BeginHorizontal();
      if (numIndices > 1 && GUILayout.Button("<", GUILayout.ExpandWidth(false))) index = (index - 1 + numIndices) % numIndices;
      centerGuiAction();
      if (numIndices > 1 && GUILayout.Button(">", GUILayout.ExpandWidth(false))) index = (index + 1) % numIndices;
      GUILayout.EndHorizontal();

      return index;
    }

    public static int ArrowSelector(int index, int modulo, string label, bool expandWidth = true)
    {
      Action drawLabel = () => GUILayout.Label(label, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, stretchWidth = expandWidth });
      return ArrowSelector(index, modulo, drawLabel);
    }

    // Credits to https://github.com/jrossignol/WaypointManager/blob/master/source/WaypointManager/CustomWaypointGUI.cs
    // for this code
    public static bool GetBodyRayIntersect(CelestialBody targetBody, bool map, out double latitude, out double longitude, out double altitude)
    {
      latitude = 0;
      longitude = 0;
      altitude = 0;
      if (targetBody.pqsController == null)
      {
        return false;
      }

      Ray mouseRay = map ? PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition) : FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
      if (map) // use scaled space
        mouseRay.origin = ScaledSpace.ScaledToLocalSpace(mouseRay.origin);
      var bodyToOrigin = mouseRay.origin - targetBody.position;
      double curRadius = targetBody.pqsController.radiusMax;
      double lastRadius = 0;
      int loops = 0;
      while (loops < 10)
      {
        Vector3d relSurfacePosition;
        if (PQS.LineSphereIntersection(bodyToOrigin, mouseRay.direction, curRadius, out relSurfacePosition))
        {
          var surfacePoint = targetBody.position + relSurfacePosition;
          double alt = targetBody.pqsController.GetSurfaceHeight(
              QuaternionD.AngleAxis(targetBody.GetLongitude(surfacePoint), Vector3d.down) * QuaternionD.AngleAxis(targetBody.GetLatitude(surfacePoint), Vector3d.forward) * Vector3d.right);
          double error = Math.Abs(curRadius - alt);
          if (error < (targetBody.pqsController.radiusMax - targetBody.pqsController.radiusMin) / 500)
          {
            latitude = targetBody.GetLatitude(surfacePoint);
            longitude = targetBody.GetLongitude(surfacePoint);
            altitude = targetBody.TerrainAltitude(latitude, longitude);
            return true;
          }
          else
          {
            lastRadius = curRadius;
            curRadius = 0.5 * alt + 0.5 * curRadius;
            loops++;
          }
        }
        else
        {
          if (loops == 0)
            break;
          // Went too low, needs to try higher
          else
          {
            curRadius = (lastRadius * 9 + curRadius) / 10;
            loops++;
          }
        }
      }
      return true;
    }

    public static bool GetMouseHit(CelestialBody body, Rect notRect, bool map, out RaycastHit hit)
    {
      hit = new RaycastHit();
      if ((notRect != null) && (notRect.Contains(Input.mousePosition)))
        return false;

      // Cast a ray from screen point
      Ray ray = map ? PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition) : FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
      //return Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, 1 << 15);

      // From https://forum.kerbalspaceprogram.com/index.php?/topic/71085-list-of-layer-masks-to-use-with-physicsraycast/
      //[LOG 21:04:29.355] 0: Default
      //[LOG 21:04:29.355] 1: TransparentFX
      //[LOG 21:04:29.356] 2: Ignore Raycast
      //[LOG 21:04:29.356] 3: 
      //[LOG 21:04:29.356] 4: Water
      //[LOG 21:04:29.356] 5: 
      //[LOG 21:04:29.357] 6: 
      //[LOG 21:04:29.357] 7: 
      //[LOG 21:04:29.357] 8: PartsList_Icons
      //[LOG 21:04:29.358] 9: Atmosphere
      //[LOG 21:04:29.358] 10: Scaled Scenery
      //[LOG 21:04:29.358] 11: UI_Culled
      //[LOG 21:04:29.359] 12: UI_Main
      //[LOG 21:04:29.359] 13: UI_Mask
      //[LOG 21:04:29.359] 14: Screens
      //[LOG 21:04:29.360] 15: Local Scenery
      //[LOG 21:04:29.360] 16: kerbals
      //[LOG 21:04:29.360] 17: Editor_UI
      //[LOG 21:04:29.361] 18: SkySphere
      //[LOG 21:04:29.361] 19: Disconnected Parts
      //[LOG 21:04:29.361] 20: Internal Space
      //[LOG 21:04:29.362] 21: Part Triggers
      //[LOG 21:04:29.362] 22: KerbalInstructors
      //[LOG 21:04:29.362] 23: ScaledSpaceSun
      //[LOG 21:04:29.363] 24: MapFX
      //[LOG 21:04:29.363] 25: EzGUI_UI
      //[LOG 21:04:29.363] 26: WheelCollidersIgnore
      //[LOG 21:04:29.364] 27: WheelColliders
      //[LOG 21:04:29.364] 28: TerrainColliders
      //[LOG 21:04:29.364] 29: 
      //[LOG 21:04:29.365] 30: 
      //[LOG 21:04:29.365] 31: Vectors
      //return Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, (1 << 15) + (1 << 10) + (1 << 4) << (1 << 28));

      //return Physics.Raycast(ray.origin, ray.direction, out hit, (float)body.Radius, (1 << 4) + (1 << 15), QueryTriggerInteraction.Ignore);
      return Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, (1 << 15), QueryTriggerInteraction.Ignore);
    }

    public static bool MouseIsOverWindow(Rect rect)
    {
      return (rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));
    }

    public static void ScreenMessage(string message)
    {
      ScreenMessages.PostScreenMessage(message, 3.0f, ScreenMessageStyle.UPPER_CENTER);
    }
  }

  public class Coordinates
  {
    public double latitude;
    public double longitude;

    public Coordinates(double latitude, double longitude)
    {
      this.latitude = latitude;
      this.longitude = longitude;
    }

    public static string ToStringDecimal(double latitude, double longitude, bool newline = false, int precision = 3)
    {
      double clampedLongitude = MuUtils.ClampDegrees180(longitude);
      double latitudeAbs = Math.Abs(latitude);
      double longitudeAbs = Math.Abs(clampedLongitude);
      return latitudeAbs.ToString("F" + precision) + "° " + (latitude > 0 ? "N" : "S") + (newline ? "\n" : ", ")
          + longitudeAbs.ToString("F" + precision) + "° " + (clampedLongitude > 0 ? "E" : "W");
    }

    public string ToStringDecimal(bool newline = false, int precision = 3)
    {
      return ToStringDecimal(latitude, longitude, newline, precision);
    }

    public static string ToStringDMS(double latitude, double longitude, bool newline = false)
    {
      double clampedLongitude = MuUtils.ClampDegrees180(longitude);
      return AngleToDMS(latitude) + (latitude > 0 ? " N" : " S") + (newline ? "\n" : ", ")
            + AngleToDMS(clampedLongitude) + (clampedLongitude > 0 ? " E" : " W");
    }

    public string ToStringDMS(bool newline = false)
    {
      return ToStringDMS(latitude, longitude, newline);
    }

    public static string AngleToDMS(double angle)
    {
      int degrees = (int)Math.Floor(Math.Abs(angle));
      int minutes = (int)Math.Floor(60 * (Math.Abs(angle) - degrees));
      int seconds = (int)Math.Floor(3600 * (Math.Abs(angle) - degrees - minutes / 60.0));

      return String.Format("{0:0}° {1:00}' {2:00}\"", degrees, minutes, seconds);
    }
  }
}
