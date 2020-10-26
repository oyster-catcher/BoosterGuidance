using System;
namespace BoosterGuidance
{
  public class CelestialBodyProxy
  {
    CelestialBody body;
    public Vector3d position => body.position;
    public double Radius => body.Radius;

    public CelestialBodyProxy(CelestialBody a_body)
    {
      body = a_body;
    }

    public bool Hit(Vector3d r)
    {
      return (r - body.position).magnitude < body.Radius;
    }
  }
}
