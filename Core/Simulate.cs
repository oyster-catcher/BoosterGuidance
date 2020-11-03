﻿using System;
using UnityEngine;
using KSP;

namespace BoosterGuidance
{
  public class Simulate
  {
    double MinHeightAtMinThrust(double y, double vy, double amin, double g)
    {
      double minHeight = 0;
      if (amin < g)
        return -float.MaxValue;
      double tHover = -vy / amin; // time to come to hover
      minHeight = y + vy * tHover + 0.5 * amin * tHover * tHover - 0.5 * g * tHover * tHover;
      return minHeight;
    }

    static bool Hit(CelestialBody body, Vector3d r)
    {
      return (r - body.position).magnitude < body.Radius;
    }

    static public Vector3d ToGround(double tgtAlt, Vessel vessel, Trajectories.VesselAerodynamicModel aeroModel, CelestialBody body, BLController controller,
      Vector3d tgt_r, out double T, string logFilename="", Transform logTransform=null, double maxT=600)
      // logTransform is transform at the current time
    {
      System.IO.StreamWriter f = null;
      if (logFilename != "")
      {
        f = new System.IO.StreamWriter(logFilename);
        f.WriteLine("time x y z vx vy vz ax ay az att_err airspeed");
        f.WriteLine("# tgtAlt=" + tgtAlt);
      }

      T = 0;
      Vector3d r = vessel.GetWorldPos3D() - body.position;
      Vector3d v = vessel.GetObtVelocity();
      Vector3d a = Vector3d.zero;
      Vector3d lastv = v;
      double minThrust, maxThrust;
      KSPUtils.ComputeMinMaxThrust(vessel, out minThrust, out maxThrust);
      if (controller != null)
        controller.noCorrect = true;
      double y = r.magnitude - body.Radius;
      double amin = minThrust / vessel.totalMass;
      double amax = maxThrust / vessel.totalMass;
      Vector3d att = new Vector3d(vessel.transform.up.x, vessel.transform.up.y, vessel.transform.up.z);
      float ang;
      Quaternion bodyRotation;

      double dt = 2; // above atmosphere
      while ((y > tgtAlt) && (T < maxT))
      {
        if (y < 100000) // inside atmosphere (Kerbin)
          dt = 0.1;
        float lastAng = (float)((-1) * body.angularVelocity.magnitude / Math.PI * 180.0);
        Quaternion lastBodyRot = Quaternion.AngleAxis(lastAng, body.angularVelocity.normalized);
        Vector3d vel_air = v - body.getRFrmVel(r + body.position);
        if (f != null)
        {
          // NOTE: Cancel out rotation of planet
          ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
          // Rotation 1 second earlier
          float prevang = (float)((-(T-1)) * body.angularVelocity.magnitude / Math.PI * 180.0);
          // Consider body rotation at this time
          bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
          Quaternion prevbodyRotation = Quaternion.AngleAxis(prevang, body.angularVelocity.normalized);
          Vector3d tr = bodyRotation * r;
          Vector3d tr1 = prevbodyRotation * r;
          Vector3d tr2 = bodyRotation * (r + v);
          Vector3d ta = bodyRotation * a;
          tr = logTransform.InverseTransformPoint(tr + body.position);
          Vector3d tv = logTransform.InverseTransformVector(tr2 - tr1);
          ta = logTransform.InverseTransformVector(ta);
          f.WriteLine(string.Format("{0} {1:F5} {2:F5} {3:F5} {4:F5} {5:F5} {6:F5} {7:F1} {8:F1} {9:F1} 0 {10:F1}", T, tr.x, tr.y, tr.z, tv.x, tv.y, tv.z, ta.x, ta.y, ta.z, vel_air.magnitude));
        }
        double throttle = 0;
        Vector3d steer = Vector3d.zero;
        if (controller != null)
        {
          bool shutdownEnginesNow;
          controller.GetControlOutputs(vessel, r + body.position, v, att, y, amin, amax, T, body, tgt_r, out throttle, out steer, out shutdownEnginesNow, logFilename != "");
          if (shutdownEnginesNow)
          {
            amin = amin * 0.33; // TODO: Hack so we can land, assumes Falcon 9 shutting down 2 outer engines, leaving one
            amax = amax * 0.33;
          }
          if (throttle > 0)
            a = steer * (amin + throttle * (amax - amin));
          else
            a = Vector3d.zero;
        }
        Vector3d F = aeroModel.GetForces(body, r, vel_air, Math.PI); // retrograde
        if (f != null)
        {
          Vector3d F5 = aeroModel.GetForces(body, r, vel_air, 5 * Math.PI / 180); // 5 degrees
          double sideFA = (F5 - F5 * Vector3d.Dot(Vector3d.Normalize(F5), Vector3d.Normalize(F))).magnitude; // just leave sideways component
          double sideFT = maxThrust * Math.Sin(5 * Math.PI / 180); // sideways component of thrust at 5 degrees
          Debug.Log("[BoosterGuidance] y=" + y + " speed=" + (vel_air.magnitude) + " sidea(aero)=" + (sideFA / vessel.totalMass) + " sidea(maxthrust)=" + (sideFT / vessel.totalMass));
        }

        r = r + v * dt;
        lastv = v;
        v = v + (F / vessel.totalMass) * dt;
        double R = r.magnitude;
        Vector3d g = r * (-body.gravParameter / (R * R * R));
        v = v + (g + a) * dt;
        y = r.magnitude - body.Radius;
        T = T + dt;
      }
      if (T > maxT)
        Debug.Log("[BoosterGuidance] Simulation time exceeds maxT=" + maxT);
      // Correct to point of intersect on surface
      double vy = Vector3d.Dot(lastv, Vector3d.Normalize(r));
      double p = 0;
      if (vy < -0.1)
      {
        p = (tgtAlt - y) / -vy; // Backup proportion
        r = r - lastv * p;
        T = T - p;
      }
      if (f != null)
        f.Close();

      // Compensate for body rotation giving world position in the surface point now
      // that would be hit in the future
      ang = (float)((-T) * body.angularVelocity.magnitude / Math.PI * 180.0);
      bodyRotation = Quaternion.AngleAxis(ang, body.angularVelocity.normalized);
      r = bodyRotation * r ;
      return r + body.position;
    }
  }
}