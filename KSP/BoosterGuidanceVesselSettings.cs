using System;

namespace BoosterGuidance
{
  public class BoosterGuidanceVesselSettings : PartModule
  {
    [KSPField(isPersistant = true, guiActive = false)]
    public float tgtLatitude = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public float tgtLongitude = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public int tgtAlt = 0;

    [KSPField(isPersistant = true, guiActive = false)]
    public int reentryBurnAlt = 55000;

    [KSPField(isPersistant = true, guiActive = false)]
    public int reentryBurnTargetSpeed = 700;

    [KSPField(isPersistant = true, guiActive = false)]
    public float reentryBurnSteerKp = 0.0003f;

    [KSPField(isPersistant = true, guiActive = false)]
    public float aeroDescentSteerKp = 10;

    [KSPField(isPersistant = true, guiActive = false)]
    public float landingBurnSteerKp = 10;

    [KSPField(isPersistant = true, guiActive = false)]
    public int touchdownMargin = 15;

    [KSPField(isPersistant = true, guiActive = false)]
    public float touchdownSpeed = 2;

    [KSPField(isPersistant = true, guiActive = false)]
    public int noSteerHeight = 200;

    [KSPField(isPersistant = true, guiActive = false)]
    public bool deployLandingGear = true;

    [KSPField(isPersistant = true, guiActive = false)]
    public int deployLandingGearHeight = 500;

    [KSPField(isPersistant = true, guiActive = false)]
    public string phase = "Unset";
  }
}
