/*
  Copyright© (c) 2016-2017 Youen Toupin, (aka neuoy).

  This file is part of Trajectories.
  Trajectories is available under the terms of GPL-3.0-or-later.
  See the LICENSE.md file for more details.

  Trajectories is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Trajectories is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

  You should have received a copy of the GNU General Public License
  along with Trajectories.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using UnityEngine;

namespace Trajectories
{
    public class AeroForceCache
    {
        public double MaxVelocity { get; private set; }
        public double MaxAoA { get; private set; }
        public double MaxAltitude { get; private set; }

        public int VelocityResolution { get; private set; }
        public int AoAResolution { get; private set; }
        public int AltitudeResolution { get; private set; }

        private Vector2[,,] InternalArrayDrag;
        private Vector2[,,] InternalArrayLift;

        private VesselAerodynamicModel Model;

        public AeroForceCache(double maxCacheVelocity, double maxCacheAoA, double atmosphereDepth, int vRes, int aoaRes, int altRes, VesselAerodynamicModel model)
        {
          Model = model;

          this.MaxVelocity = maxCacheVelocity;
          this.MaxAoA = maxCacheAoA;
          this.MaxAltitude = atmosphereDepth;
          VelocityResolution = vRes;
          AoAResolution = aoaRes;
          AltitudeResolution = altRes;

          InternalArrayDrag = new Vector2[VelocityResolution, AoAResolution, AltitudeResolution];
          InternalArrayLift = new Vector2[VelocityResolution, AoAResolution, AltitudeResolution];
          for (int v = 0; v < VelocityResolution; ++v)
            for (int a = 0; a < AoAResolution; ++a)
              for (int m = 0; m < AltitudeResolution; ++m)
              {
                InternalArrayDrag[v, a, m] = new Vector2(float.NaN, float.NaN);
                InternalArrayLift[v, a, m] = new Vector2(float.NaN, float.NaN);
              }
        }

        public Vector3d GetForce(double velocity, double angleOfAttack, double altitude, out Vector3d dragForce, out Vector3d liftForce)
        {
            //Debug.Log("[Trajectories] GetForce(cached) angleOfAttack=" + angleOfAttack);
            float vFrac = (float)(velocity / MaxVelocity * (double)(InternalArrayDrag.GetLength(0) - 1));
            int vFloor = Math.Max(0, Math.Min(InternalArrayDrag.GetLength(0) - 2, (int)vFrac));
            vFrac = Math.Max(0.0f, Math.Min(1.0f, vFrac - (float)vFloor));

            float aFrac = (float)((angleOfAttack / MaxAoA * 0.5 + 0.5) * (double)(InternalArrayDrag.GetLength(1) - 1));
            int aFloor = Math.Max(0, Math.Min(InternalArrayDrag.GetLength(1) - 2, (int)aFrac));
            aFrac = Math.Max(0.0f, Math.Min(1.0f, aFrac - (float)aFloor));

            float mFrac = (float)(altitude / MaxAltitude * (double)(InternalArrayDrag.GetLength(2) - 1));
            int mFloor = Math.Max(0, Math.Min(InternalArrayDrag.GetLength(2) - 2, (int)mFrac));
            mFrac = Math.Max(0.0f, Math.Min(1.0f, mFrac - (float)mFloor));

            //if (Verbose)
            //{
            //    Util.PostSingleScreenMessage("cache cell", "cache cell: [" + vFloor + ", " + aFloor + ", " + mFloor + "]");
            //    Util.PostSingleScreenMessage("altitude cell", "altitude cell: " + altitude + " / " + MaxAltitude + " * " + (double)(InternalArray.GetLength(2) - 1));
            //}

            Sample3d(vFloor, vFrac, aFloor, aFrac, mFloor, mFrac, out Vector2 resDrag, out Vector2 resLift);
            dragForce = Model.UnpackForces(resDrag, altitude, velocity);
            liftForce = Model.UnpackForces(resLift, altitude, velocity);
            return dragForce + liftForce;
    }

    private Vector2 Sample2d(int vFloor, float vFrac, int aFloor, float aFrac, int mFloor, out Vector2 drag, out Vector2 lift)
        {
            GetCachedForce(vFloor, aFloor, mFloor, out Vector2 d00, out Vector2 l00);
            GetCachedForce(vFloor + 1, aFloor, mFloor, out Vector2 d10, out Vector2 l10);

            GetCachedForce(vFloor, aFloor + 1, mFloor, out Vector2 d01, out Vector2 l01);
            GetCachedForce(vFloor + 1, aFloor + 1, mFloor, out Vector2 d11, out Vector2 l11);


            Vector2 d0 = d01 * aFrac + d00 * (1.0f - aFrac);
            Vector2 d1 = d11 * aFrac + d10 * (1.0f - aFrac);
            Vector2 l0 = l01 * aFrac + l00 * (1.0f - aFrac);
            Vector2 l1 = l11 * aFrac + l10 * (1.0f - aFrac);

            drag = d1 * vFrac + d0 * (1.0f - vFrac);
            lift = l1 * vFrac + l0 * (1.0f - vFrac);
            return drag + lift;
        }

        private Vector2 Sample3d(int vFloor, float vFrac, int aFloor, float aFrac, int mFloor, float mFrac, out Vector2 drag, out Vector2 lift)
        {
            Sample2d(vFloor, vFrac, aFloor, aFrac, mFloor, out Vector2 d0, out Vector2 l0);
            Sample2d(vFloor, vFrac, aFloor, aFrac, mFloor + 1, out Vector2 d1, out Vector2 l1);

            drag = d1 * mFrac + d0 * (1.0f - mFrac);
            lift = d1 * mFrac + d0 * (1.0f - mFrac);
            return drag + lift;
        }

        private Vector2 GetCachedForce(int v, int a, int m, out Vector2 fdrag,out Vector2 flift)
        {
            fdrag = InternalArrayDrag[v, a, m];
            flift = InternalArrayLift[v, a, m];
            if (float.IsNaN(fdrag.x))
            {
              ComputeCacheEntry(v, a, m, out fdrag, out flift);
            }

            return fdrag + flift;
        }

        private Vector2 ComputeCacheEntry(int v, int a, int m, out Vector2 fdrag, out Vector2 flift)
        {
            double vel = MaxVelocity * (double)v / (double)(InternalArrayDrag.GetLength(0) - 1);
            Vector3d velocity = new Vector3d(vel, 0, 0);
            double AoA = MaxAoA * ((double)a / (double)(InternalArrayDrag.GetLength(1) - 1) * 2.0 - 1.0);
            double currentAltitude = MaxAltitude * (double)m / (double)(InternalArrayDrag.GetLength(2) - 1);

            Vector3d total_drag;
            Vector3d total_lift;
            Model.ComputeForces(currentAltitude, velocity, new Vector3(0, 1, 0), AoA, out total_drag, out total_lift);
            fdrag = Model.PackForces(total_drag, currentAltitude, vel);
            flift = Model.PackForces(total_lift, currentAltitude, vel);

            InternalArrayDrag[v, a, m] = fdrag;
            InternalArrayLift[v, a, m] = flift;
            return fdrag + flift;
        }
  }
}
