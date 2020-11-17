using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trajectories
{
    public static class GLUtils
    {
        static Material _material;
        static Material material
        {
            get
            {
                if (_material == null) _material = new Material(Shader.Find("KSP/Particles/Additive"));
                if (_material == null) _material = new Material(Shader.Find("Particles/Additive"));
                if (_material == null) Debug.Log("[Trajectories] CRITICAL: GLUtils._material is null");
                return _material;
            }
        }

        //Tests if byBody occludes worldPosition, from the perspective of the planetarium camera
        // https://cesiumjs.org/2013/04/25/Horizon-culling/
        public static bool IsOccluded(Vector3d worldPosition, CelestialBody byBody, Vector3d camPos)
        {
            Vector3d VC = (byBody.position - camPos) / (byBody.Radius - 100);
            Vector3d VT = (worldPosition - camPos) / (byBody.Radius - 100);

            double VT_VC = Vector3d.Dot(VT, VC);

            // In front of the horizon plane
            if (VT_VC < VC.sqrMagnitude - 1) return false;

            return VT_VC * VT_VC / VT.sqrMagnitude > VC.sqrMagnitude - 1;
        }

        public static void DrawMapViewGroundMarker(CelestialBody body, double latitude, double longitude, double alt, Color c,  double rotation = 0, double radius = 0)
        {
            DrawGroundMarker(body, latitude, longitude, alt, c, true, rotation, radius);
        }

        public static void DrawGroundMarker(CelestialBody body, double latitude, double longitude, double alt, Color c, bool map, double rotation = 0, double radius = 0)
        {
            Vector3d up = body.GetSurfaceNVector(latitude, longitude);
            //var height = body.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right);
            //if (height < body.Radius) { height = body.Radius; }
            //Vector3d center = body.position + height * up;

            Vector3d center = body.GetWorldSurfacePosition(latitude, longitude, alt + 2);
            Vector3d camPos = map ? ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position) : (Vector3d)FlightCamera.fetch.mainCamera.transform.position;

            //if (IsOccluded(center, body, camPos)) return;

            Vector3d north = Vector3d.Exclude(up, body.transform.up).normalized;

            if (radius <= 0) { radius = map ? body.Radius / 15 : 5; }

            if (!map)
            {
                Vector3 centerPoint = FlightCamera.fetch.mainCamera.WorldToViewportPoint(center);
                if (centerPoint.z < 0)
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
            catch{}
        }
    }
}