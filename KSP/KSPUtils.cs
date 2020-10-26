// Utility functions that depend on KSP

using System;
using UnityEngine;

namespace BoosterGuidance
{
  public class KSPUtils
  {
    // Find Y offset to lowest part from origin of the vessel
    public static double FindLowestPointOnVessel(Vessel vessel)
    {
      Vector3 CoM, up;

      CoM = vessel.localCoM;
      Vector3 bottom = Vector3.zero; // Offset from CoM
      up = FlightGlobals.getUpAxis(CoM); //Gets up axis
      Vector3 pos = vessel.GetWorldPos3D();
      Vector3 distant = pos - 1000 * up; // distant below craft
      double miny = 0;
      foreach (Part p in vessel.parts)
      {
        if (p.collider != null) //Makes sure the part actually has a collider to touch ground
        {
          Vector3 pbottom = p.collider.ClosestPointOnBounds(distant); //Gets the bottom point
          double y = Vector3.Dot(up, pbottom - pos); // relative to centre of vessel
          if (y < miny)
          {
            bottom = pbottom;
            miny = y;
          }
        }
      }
      return miny;
    }
  }
}
