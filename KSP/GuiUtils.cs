using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
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

    // Quad should be described a,b,c,d in anti-clockwise order when looking at it
    static void AddQuad(Vector3[] vertices, ref int vi, int[] triangles, ref int ti,
                  Vector3d a, Vector3d b, Vector3d c, Vector3d d,
                  bool double_sided = false)
    {
      vertices[vi + 0] = a;
      vertices[vi + 1] = b;
      vertices[vi + 2] = c;
      vertices[vi + 3] = d;
      triangles[ti++] = vi;
      triangles[ti++] = vi + 2;
      triangles[ti++] = vi + 1;
      triangles[ti++] = vi;
      triangles[ti++] = vi + 3;
      triangles[ti++] = vi + 2;
      if (double_sided)
      {
        triangles[ti++] = vi;
        triangles[ti++] = vi + 1;
        triangles[ti++] = vi + 2;
        triangles[ti++] = vi;
        triangles[ti++] = vi + 2;
        triangles[ti++] = vi + 3;
      }
      vi += 4;
    }

    // pos is ground position, but draw up to height
    static public GameObject DrawTarget(Vector3d pos, Transform a_transform, Color color, double size, float height)
    {
      double[] r = new double[] { size * 0.5, size * 0.55, size * 0.95, size };
      Vector3d gpos = pos;
      Vector3d tpos = pos + new Vector3d(0, height, 0);

      Vector3d vx = new Vector3d(1, 0, 0);
      Vector3d vz = new Vector3d(0, 0, 1);

      GameObject o = new GameObject();
      o.transform.SetParent(a_transform, false);
      MeshFilter meshf = o.AddComponent<MeshFilter>();
      MeshRenderer meshr = o.AddComponent<MeshRenderer>();
      meshr.material = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
      meshr.material.color = color;
      meshr.receiveShadows = false;

      Mesh mesh = new Mesh();
      Vector3[] vertices = new Vector3[36 * 4 + 4 + 4 + 4 + 8];
      int[] triangles = new int[(36 * 2 * 2 - 8 + 2 + 2 + 2 + 4 + 4) * 3]; // take away gaps
      int i, j;
      int v = 0, t = 0;
      for (j = 0; j < 4; j++) // four concentric rings
      {
        for (i = 0; i < 36; i++)
        {
          float a = -(i * 10) * Mathf.PI / 180.0f;
          vertices[v++] = gpos + vx * Mathf.Sin(a) * r[j] + vz * Mathf.Cos(a) * r[j];
        }
      }
      for (j = 0; j < 2; j++)
      {
        int start = j * 72;
        for (i = 0; i < 36; i++)
        {
          if ((j == 1) || (i % 9 != 0)) // make 4 gaps in inner ring
          {
            triangles[t++] = start + i;
            triangles[t++] = start + (i + 1) % 36;
            triangles[t++] = start + 36 + i % 36;

            triangles[t++] = start + (i + 1) % 36;
            triangles[t++] = start + 36 + (i + 1) % 36;
            triangles[t++] = start + 36 + i % 36;
          }
        }
      }
      // Add cross across centre
      Vector3 cx = vx * size * 0.03;
      Vector3 cz = vz * size * 0.03;
      float cs = 8;
      AddQuad(vertices, ref v, triangles, ref t,
              tpos - cx * cs - cz, tpos + cx * cs - cz, tpos + cx * cs + cz, tpos - cx * cs + cz);
      // One side
      AddQuad(vertices, ref v, triangles, ref t,
              tpos - cx + cz, tpos + cx + cz, tpos + cx + cz * cs, tpos - cx + cz * cs);
      // Other size
      AddQuad(vertices, ref v, triangles, ref t,
              tpos - cx - cz * cs, tpos + cx - cz * cs, tpos + cx - cz, tpos - cx - cz);

      // Draw quads from cross at actual height to the rings on the ground
      cx = vx * size * 0.01;
      cz = vz * size * 0.01;
      AddQuad(vertices, ref v, triangles, ref t,
              gpos - cx, gpos + cx, tpos + cx, tpos - cx, true);
      AddQuad(vertices, ref v, triangles, ref t,
              gpos - cz, gpos + cz, tpos + cz, tpos - cz, true);

      mesh.vertices = vertices;
      mesh.triangles = triangles;
      meshf.mesh = mesh;
      mesh.RecalculateNormals();
      return o;
    }

    static public Transform SetUpTransform(CelestialBody body, double latitude, double longitude, double alt)
    {
      // Set up transform so Y is up and (0,0,0) is target position
      Vector3d origin = body.GetWorldSurfacePosition(latitude, longitude, alt);
      Vector3d vEast = body.GetWorldSurfacePosition(latitude, longitude - 0.1, alt) - origin;
      Vector3d vUp = body.GetWorldSurfacePosition(latitude, longitude, alt + 1) - origin;
      // Convert to body co-ordinates
      origin = body.transform.InverseTransformPoint(origin);
      vEast = body.transform.InverseTransformVector(vEast);
      vUp = body.transform.InverseTransformVector(vUp);

      GameObject go = new GameObject();
      // Need to rotation that converts (0,1,0) to vUp in the body transform
      Quaternion quat = Quaternion.FromToRotation(new Vector3(0, 1, 0), vUp);

      Transform o_transform = go.transform;
      o_transform.SetPositionAndRotation(origin, quat);
      o_transform.SetParent(body.transform, false);
      return o_transform;
    }

    static public GameObject DrawTargetOnSurface(CelestialBody body, double latitude, double longitude, double alt)
    {
      Transform transform = SetUpTransform(body, latitude, longitude, alt);
      return DrawTarget(Vector3d.zero, transform, new Color(1, 1, 0, 0.5f), 100, 0);
    }

    public static bool GetMouseHit(CelestialBody body, Rect notRect, out RaycastHit hit)
    {
      hit = new RaycastHit();
      if ((notRect != null) && (notRect.Contains(Input.mousePosition)))
        return false;

      // Cast a ray from screen point
      Ray ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
      return Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, 1 << 15);
    }

    public static void DrawVector(ref GameObject obj, ref LineRenderer line, Vector3 r_from, Vector3 r_to, Transform a_transform, Color color, bool show)
    {
      if (!show)
      {
        if (obj != null)
        {
          //Destroy(obj);
          obj = null;
          line = null;
        }
        return;
      }

      if (line == null)
      {
        obj = new GameObject("Steer");
        line = obj.AddComponent<LineRenderer>();
      }
      line.transform.parent = a_transform;
      line.useWorldSpace = true;
      line.material = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
      line.material.color = color;
      line.startWidth = 0.3f;
      line.endWidth = 0.3f;
      line.positionCount = 2;
      if (a_transform != null)
      {
        line.SetPosition(0, a_transform.TransformPoint(r_from));
        line.SetPosition(1, a_transform.TransformPoint(r_to));
      }
      else
      {
        line.SetPosition(0, r_from);
        line.SetPosition(1, r_to);
      }
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
