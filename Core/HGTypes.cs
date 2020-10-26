namespace BoosterGuidance
{
  public class SolveTarget
  {
    public const int X = 1;
    public const int Y = 2;
    public const int Z = 4;

    public Vector3d r;
    public int raxes; // combination of X, Y, Z
    public Vector3d v;
    public int vaxes; // combination of X, Y, Z
    public float t;
  }

  public class StringPair
  {
    public string Item1;
    public string Item2;

    public StringPair(string k, string v)
    {
      Item1 = k;
      Item2 = v;
    }
  }
}
