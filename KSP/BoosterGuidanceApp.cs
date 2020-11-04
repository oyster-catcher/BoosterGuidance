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
      GameEvents.onGUIApplicationLauncherReady.Add(delegate
      {
        CreateStockToolbarButton();
      });
      GameEvents.onGUIApplicationLauncherUnreadifying.Add(delegate
      {
        DestroyStockToolbarButton();
      });
    }

    private void CreateStockToolbarButton()
    {
      Localizer.Init();
      if (ApplicationLauncher.Ready && _appLauncherButton == null)
      {
        _appLauncherButton = ApplicationLauncher.Instance.AddModApplication(ShowHideMasterWindow ,
            ShowHideMasterWindow,
            () => { },
            () => { },
            () => { },
            () => { },
            //ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.FLIGHT,
            ApplicationLauncher.AppScenes.FLIGHT,
            GameDatabase.Instance.GetTexture("BoosterGuidance/BoosterGuidanceIcon", false)
            );
      }
      mainw = new MainWindow();
    }

    private void DestroyStockToolbarButton()
    {
      Debug.Log("DestroyStockToolbarButton");
      ApplicationLauncher.Instance.RemoveModApplication(_appLauncherButton);
      _appLauncherButton = null;
    }

    private void ShowHideMasterWindow()
    {
      mainw.ToggleVisibility();
    }

    private void OnGUI()
    {
      mainw.OnGUI();
    }

    public void OnDestroy()
    {
      Debug.Log("[BoosterGuidance] OnDestroy()");
    }

    public static BoosterGuidanceCore Instance;
    private ApplicationLauncherButton _appLauncherButton;
  }
}
