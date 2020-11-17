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
  public class BoosterGuidanceCore : MonoBehaviour
  {
    static MainWindow mainw;

    public void Awake()
    {
      Debug.Log("[BoosterGuidance] Awake");
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
      if ((ApplicationLauncher.Ready) && (_appLauncherButton == null))
      {
        Debug.Log("[BoosterGuidance] AddModApplication");
        _appLauncherButton = ApplicationLauncher.Instance.AddModApplication(OnStockTrue,
            OnStockFalse,
            () => { },
            () => { },
            () => { },
            () => { },
            ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.FLIGHT,
            //ApplicationLauncher.AppScenes.FLIGHT,
            GameDatabase.Instance.GetTexture("BoosterGuidance/BoosterGuidanceIcon", false)
            );
        mainw = new MainWindow();
      }
    }

    private void DestroyStockToolbarButton()
    {
      Debug.Log("[BoosterGuidance] DestroyStockToolbarButton");
      ApplicationLauncher.Instance.RemoveModApplication(_appLauncherButton);
      _appLauncherButton = null;
    }

    private void OnStockTrue()
    {
      mainw.Show();
    }

    private void OnStockFalse()
    {
      mainw.Hide();
    }

    private void OnGUI()
    {
      mainw.OnGUI();
    }

    public void OnDestroy()
    {
      Debug.Log("[BoosterGuidance] OnDestroy()");
      DestroyStockToolbarButton();
    }

    public static BoosterGuidanceCore Instance;
    private static ApplicationLauncherButton _appLauncherButton;
  }
}
