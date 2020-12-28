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
  public class BoosterGuidanceApp : MonoBehaviour
  {
    static MainWindow mainw;

    public void Awake()
    {
      GameEvents.onGUIApplicationLauncherReady.Add(CreateStockToolbarButton);
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
        _appLauncherButton = ApplicationLauncher.Instance.AddModApplication(OnStockTrue,
            OnStockFalse,
            () => { },
            () => { },
            () => { },
            () => { },
            ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.FLIGHT,
            GameDatabase.Instance.GetTexture("BoosterGuidance/BoosterGuidanceIcon", false)
            );
        mainw = new MainWindow();
      }
    }

    private void DestroyStockToolbarButton()
    {
      if (_appLauncherButton != null)
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
      DestroyStockToolbarButton();
    }

    public static BoosterGuidanceApp Instance;
    private static ApplicationLauncherButton _appLauncherButton;
  }
}
