using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
//using UnityGUIFramework;
using KSP.UI.Screens;

namespace BoosterGuidance
{
  [KSPAddon(KSPAddon.Startup.Flight, false)]
  public class BoosterFlightControl : MonoBehaviour
  {
    private bool _displayGUI;

    public void Start()
    {
      //DebugHelper.Debug("EngineFlightControl:Start");
      Instance = this;
      GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
      OnGUIAppLauncherReady();
    }

    private void OnGUIAppLauncherReady()
    {
      if (ApplicationLauncher.Ready && _appLauncherButton == null)
      {
        _appLauncherButton = ApplicationLauncher.Instance.AddModApplication(() => _displayGUI = true,
            () => _displayGUI = false,
            () => { },
            () => { },
            () => { },
            () => { },
            ApplicationLauncher.AppScenes.FLIGHT,
            GameDatabase.Instance.GetTexture("BoosterGuidance/BoosterGuidanceIcon", false)
            );
      }
    }
    public static BoosterFlightControl Instance;
    private ApplicationLauncherButton _appLauncherButton;
  }
}
