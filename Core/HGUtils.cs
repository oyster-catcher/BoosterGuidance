using System;
using System.Collections.Generic;

namespace BoosterGuidance
{
  public class HGUtils
  {
    public static double [] BasisWeights(double t, List<float> times)
    {
      // Returns vector of weights[N] for each acceleration vector at a given time, t
      // for time t in range 0...T, with N vectors
      // T divided into N-1 parts, 0, T/(N-1), 2T/(N-1), (N-1)T/(N-1) == T
      int N = times.Count;
      double [] w = new double[N];
      for( int j = 0; j < N-1; j++ )
      {
        // Find which two thrust vectors are closest
        if(( t >= times[j]) && (t <= times[j+1]))
        {
          double d = (t-times[j])/(times[j+1]-times[j]); // range 0 to 1
          double b = Math.Cos(d*0.5*Math.PI);
          w[j] = b*b;
          w[j+1] = 1-b*b;
          break;
        }
      }
      // Check if t beyond last, assume numerical error and use last thrust
      if (t > times[N-1])
        w[N-1] = 1;

      return w;
    }


    // Calculate weights on position and velocity from thrust vectors up to time tX
    public static void RVWeightsToTime(double tX, double dt, List<float> times, out double[] wr, out double[] wv)
    {
      // TODO
      int N = times.Count;
      wr = new double[N];
      wv = new double[N];
      for(double t = 0; t < tX; t += dt)
      {
        double [] w = BasisWeights(t,times); // Vector for all N weights at time, t
        double tr = tX - t - dt; // time remaining
        for(int i = 0 ; i < N ; i++)
        {
          // extra. vel after accel to time, tr + movement during acceleration over dt
          wr[i] += tr*w[i]*dt + 0.5*w[i]*dt*dt;
          // additional velocity over time, dt
          wv[i] += w[i]*dt;
        }
      }
    }

    public static Vector3d ToVector3d(string v)
    {
      if (v.StartsWith("[") && v.EndsWith("]")) {
        string v1 = v.Replace("[","").Replace("]","");
        if (v1.Split(',').Length == 3) {
          string x = v1.Split(',')[0];
          string y = v1.Split(',')[1];
          string z = v1.Split(',')[2];
          return new Vector3d(Convert.ToDouble(x),Convert.ToDouble(y),Convert.ToDouble(z));
        }
      }
      throw new System.ArgumentException("Expected Vector3d as [x,y,z] but got: "+v);
    }

    public static List<StringPair> ToKeyValuePairs(string[] args)
    {
      List<StringPair> tuples = new List<StringPair>();
      char [] equals={'='};
      foreach(var arg in args)
      {
        var parts = arg.Split(equals,2);
        if (parts.Length==1)
          tuples.Add(new StringPair(parts[0],""));
        if (parts.Length==2)
          tuples.Add(new StringPair(parts[0],parts[1]));
      }
      return tuples;
    }

    // Linear scale remapping of x where inP1 goes to outP1
    // inP2 goes to outP2
    public static float LinearMap(float x, float inP1, float inP2, float outP1, float outP2)
    {
      float v = (x-inP1)/(inP2-inP1);
      return Clamp(outP1 + v*(outP2-outP1), outP1, outP2);
    }

    // Linear scale remapping of x where inP1 goes to outP1
    // inP2 goes to outP2
    public static double LinearMap(double x, double inP1, double inP2, double outP1, double outP2)
    {
      double v = (x - inP1) / (inP2 - inP1);
      return Clamp(outP1 + v * (outP2 - outP1), outP1, outP2);
    }

    public static float Clamp(float x, float min, float max)
    {
      return (x < min) ? min : ((x > max) ? max : x);
    }

    public static double Clamp(double x, double min, double max)
    {
      return (x < min) ? min : ((x > max) ? max : x);
    }

    public static double angle_between(Vector3d a, Vector3d b)
    {
      double dcos = Vector3d.Dot(a.normalized, b.normalized);
      if ((dcos > -1) && (dcos < 1))
        return Math.Acos(dcos) * 180 / Math.PI;
      else
      {
        if (dcos > 1)
          return 0;
        else
          return 180;
      }
    }

    public static System.IO.StreamWriter OpenUnusedFilename(String filename)
    {
      filename = filename.Replace(" ", "_");
      if (!System.IO.File.Exists(filename))
        return new System.IO.StreamWriter(filename);
      int i = 1;
      while (System.IO.File.Exists(filename + "." + i.ToString()))
      {
        i++;
      }
      return new System.IO.StreamWriter(filename + "." + i.ToString());
    }
  }
}
