using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
//using UnityGUIFramework;
using KSP.Localization;
using KSP.UI.Screens;

namespace BoosterGuidance
{
  public delegate bool TryParse<T>(string str, out T value);

  [KSPAddon(KSPAddon.Startup.Flight, false)]
  public class BoosterGuidanceCore : PartModule
  {
    MainWindow mainw;

    public void Start()
    {
      //DebugHelper.Debug("EngineFlightControl:Start");
      //Instance = this;
      GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
      OnGUIAppLauncherReady();
    }

    private void OnGUIAppLauncherReady()
    {
      Localizer.Init();
      if (ApplicationLauncher.Ready && _appLauncherButton == null)
      {
        _appLauncherButton = ApplicationLauncher.Instance.AddModApplication(CreateWindow ,
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            ApplicationLauncher.AppScenes.FLIGHT,
            GameDatabase.Instance.GetTexture("BoosterGuidance/BoosterGuidanceIcon", false)
            );
      }
      mainw = new MainWindow();
    }

    private void CreateWindow()
    {
      mainw.ToggleVisibility();
    }

    private void OnGUI()
    {
      mainw.OnGUI();
    }

    public static BoosterGuidanceCore Instance;
    private ApplicationLauncherButton _appLauncherButton;
  }
}
