﻿using System;
using UnityEngine;

namespace BoosterGuidance
{
  static public class Targets
  {
    public static TargetingCross targetingCross;
    public static PredictionCross predictedCross;
    public static bool map = false; // detect switch from map/flight to re-create targets

    static Material _material;
    static Material material
    {
      get
      {
        if (_material == null) _material = new Material(Shader.Find("KSP/Particles/Additive"));
        if (_material == null) _material = new Material(Shader.Find("Particles/Additive"));
        if (_material == null) Debug.Log("[BoosterGuidance] CRITICAL: Targets._material is null");
        return _material;
      }
    }

    static public void InitTargets()
    {
      // No need to re-init
      if ((map == MapView.MapIsEnabled) && (targetingCross != null) && (predictedCross != null))
        return;

      //if (targetingCross != null)
      //  targetingCross.enabled = false;
      //if (predictedCross != null)
      //  predictedCross.enabled = false;
      if (MapView.MapIsEnabled)
      {
        targetingCross = PlanetariumCamera.fetch.gameObject.AddComponent<Targets.TargetingCross>();
        predictedCross = PlanetariumCamera.fetch.gameObject.AddComponent<Targets.PredictionCross>();
      }
      else
      {
        targetingCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<Targets.TargetingCross>();
        predictedCross = FlightCamera.fetch.mainCamera.gameObject.AddComponent<Targets.PredictionCross>();
      }
      map = MapView.MapIsEnabled;
      targetingCross.SetColor(Color.yellow);
      //targetingCross.enabled = true;
      predictedCross.SetColor(Color.red);
      //predictedCross.enabled = false;
    }

    public class TargetingCross : MonoBehaviour
    {
      public const double markerSize = 2.0d; // in meters

      // I find use of statics weird, and the duplication of code in PredictionCross
      // but its the only way I've found to avoid an accumulation of targets in subsequent flights
      // It doesn't seem possible to trap all the deletions needs
      public static double impactLat = 0d;
      public static double impactLon = 0d;
      public static double impactAlt = 0d;
      public GameObject mesh = null;
      private double cross_dist = 0d;
      private Color color = Color.green;

      public Vector3? ImpactPosition { get; internal set; }
      public CelestialBody ImpactBody { get; internal set; }
      public Color Color { get; internal set; }

      public void SetLatLonAlt(CelestialBody body, double lat, double lon, double alt)
      {
        ImpactBody = body;
        impactLat = lat;
        impactLon = lon;
        impactAlt = alt;
        ImpactPosition = ImpactBody.GetWorldSurfacePosition(impactLat, impactLon, impactAlt) - ImpactBody.position;
      }

      public void SetColor(Color a_color)
      {
        color = a_color;
      }

      public void OnPostRender()
      {
        if (ImpactBody == null)
          return;
        if (!enabled)
          return;
        // resize marker in respect to distance from camera - only for flight view
        Vector3d cam_pos = (!MapView.MapIsEnabled) ? (Vector3d)FlightCamera.fetch.mainCamera.transform.position : (Vector3d)PlanetariumCamera.fetch.transform.position;
        cam_pos = cam_pos - ImpactBody.position;
        cross_dist = System.Math.Max(Vector3.Distance(cam_pos, ImpactPosition.Value) / 80.0d, 1.0d);

        // Is marker on this side of planet?
        double d = Vector3d.Dot(cam_pos - ImpactPosition.Value, ImpactPosition.Value);

        // draw ground marker at this position
        //Debug.Log("[BoosterGuidance] TargetingCross.DrawGroundMarker lat=" + impactLat + " impactLon=" + impactLon + " impactAlt=" + impactAlt+ "d="+d);
        //Debug.Log("[BoosterGuidance] cam_pos=" + (Vector3)cam_pos);
        if (MapView.MapIsEnabled)
          DrawGroundMarker(ImpactBody, impactLat, impactLon, impactAlt, color, MapView.MapIsEnabled, 0, ImpactBody.Radius / 40);
        else
          DrawGroundMarker(ImpactBody, impactLat, impactLon, impactAlt, color, MapView.MapIsEnabled, 0, Math.Min(Math.Max(markerSize * cross_dist, 5), 15000));
      }
    }

    public class PredictionCross : MonoBehaviour
    {
      public const double markerSize = 2.0d; // in meters

      public static double impactLat = 0d;
      public static double impactLon = 0d;
      public static double impactAlt = 0d;
      private Color color = Color.green;

      public Vector3? ImpactPosition { get; internal set; }
      public CelestialBody ImpactBody { get; internal set; }
      public Color Color { get; internal set; }

      public void SetLatLonAlt(CelestialBody body, double lat, double lon, double alt)
      {
        ImpactBody = body;
        impactLat = lat;
        impactLon = lon;
        impactAlt = alt;
        ImpactPosition = ImpactBody.GetWorldSurfacePosition(impactLat, impactLon, impactAlt) - ImpactBody.position;
      }

      public void SetColor(Color a_color)
      {
        color = a_color;
      }

      public void OnPostRender()
      {
        if (ImpactBody == null)
          return;
        if (!enabled)
          return;
        // resize marker in respect to distance from camera - only for flight view
        //Vector3d cam_pos = (Vector3d)FlightCamera.fetch.mainCamera.transform.position - ImpactBody.position;
        Vector3d cam_pos = (!MapView.MapIsEnabled) ? (Vector3d)FlightCamera.fetch.mainCamera.transform.position : (Vector3d)PlanetariumCamera.fetch.transform.position;
        cam_pos = cam_pos - ImpactBody.position;
        double cross_dist = System.Math.Max(Vector3.Distance(cam_pos, ImpactPosition.Value) / 80.0d, 1.0d);
        // draw ground marker at this position
        //Debug.Log("[BoosterGuidance] PredictionCross.DrawGroundMarker lat=" + impactLat + " impactLon=" + impactLon + " impactAlt=" + impactAlt);
        if (MapView.MapIsEnabled)
          Targets.DrawGroundMarker(ImpactBody, impactLat, impactLon, impactAlt, color, MapView.MapIsEnabled, 0, ImpactBody.Radius / 40);
        else
          Targets.DrawGroundMarker(ImpactBody, impactLat, impactLon, impactAlt, color, MapView.MapIsEnabled, 0, Math.Min(Math.Max(markerSize * cross_dist, 5), 15000));
      }
    }

    public static void DrawGroundMarker(CelestialBody body, double latitude, double longitude, double alt, Color c, bool map, double rotation = 0, double radius = 0)
    {
      Vector3d up = body.GetSurfaceNVector(latitude, longitude);
      Vector3d center = body.GetWorldSurfacePosition(latitude, longitude, alt + 0.5f);
      Vector3d north = Vector3d.Exclude(up, body.transform.up).normalized;

      if (!map)
      {
        Vector3 centerPoint = FlightCamera.fetch.mainCamera.WorldToViewportPoint(center);
        if ((centerPoint.z < 0) || (centerPoint.x < -1) || (centerPoint.x > 1) || (centerPoint.y < -1) || (centerPoint.y > 1))
          return;
      }

      GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation - 10, up) * north),
                  center + radius * (QuaternionD.AngleAxis(rotation + 10, up) * north), c, map);
      GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation + 110, up) * north),
                  center + radius * (QuaternionD.AngleAxis(rotation + 130, up) * north), c, map);
      GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation - 110, up) * north),
                  center + radius * (QuaternionD.AngleAxis(rotation - 130, up) * north), c, map);
    }

    public static void GLVertex(Vector3d worldPosition, bool map = false)
    {
      Vector3 screenPoint = map ? PlanetariumCamera.Camera.WorldToViewportPoint(ScaledSpace.LocalToScaledSpace(worldPosition)) : FlightCamera.fetch.mainCamera.WorldToViewportPoint(worldPosition);
      GL.Vertex3(screenPoint.x, screenPoint.y, 0);
    }

    public static void GLTriangle(Vector3d worldVertices1, Vector3d worldVertices2, Vector3d worldVertices3, Color c, bool map)
    {
      try
      {
        GL.PushMatrix();
        material?.SetPass(0);
        GL.LoadOrtho();
        GL.Begin(GL.TRIANGLES);
        GL.Color(c);
        GLVertex(worldVertices1, map);
        GLVertex(worldVertices2, map);
        GLVertex(worldVertices3, map);
        GL.End();
        GL.PopMatrix();
      }
      catch { }
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

    public static void SetVisibility(bool target, bool prediction)
    {
      InitTargets();
      targetingCross.enabled = target;
      predictedCross.enabled = prediction;
      //Debug.Log("[BoosterGuidance] SetVisibility target=" + target + " prediction=" + prediction);
    }

    public static void RedrawTarget(CelestialBody body, double lat, double lon, double alt)
    {
      InitTargets();
      bool showTargets = true;
      Transform transform = Targets.SetUpTransform(body, lat, lon, alt);
      // Only sure when set (ideally use a separate flag!)
      // TODO
      targetingCross.enabled = showTargets && ((lat != 0) || (lon != 0) || (alt != 0));
      targetingCross.SetLatLonAlt(body, lat, lon, alt);
    }

    public static void RedrawPrediction(CelestialBody body, double lat, double lon, double alt)
    {
      InitTargets();
      bool showTargets = true;
      predictedCross.enabled = showTargets;
      predictedCross.SetLatLonAlt(body, lat, lon, alt);
    }
  }
}