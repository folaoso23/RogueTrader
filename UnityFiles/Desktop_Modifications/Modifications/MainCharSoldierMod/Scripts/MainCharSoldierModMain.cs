using HarmonyLib;
using Kingmaker;
using Kingmaker.Modding;
using Owlcat.Runtime.Core.Logging;
using System.Reflection;
using UnityEngine;

public static class MainCharSoldierModMain
{
    public static OwlcatModification Modification { get; private set; }

    public static bool IsEnabled { get; private set; } = true;

    public static LogChannel Logger => Modification.Logger;

    // ReSharper disable once UnusedMember.Global
    [OwlcatModificationEnterPoint]
    public static void Initialize(OwlcatModification modification)
    {
        PFLog.Mods.Log("MainCharSoldier test script loaded successfully!");
        Modification = modification;
        Debug.LogError("MainCharSoldier TEST script");
        var harmony = new Harmony(modification.Manifest.UniqueName);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        modification.OnDrawGUI += OnGUI;
        modification.IsEnabled += () => IsEnabled;
        modification.OnSetEnabled += enabled => IsEnabled = enabled;
        modification.OnShowGUI += () => Logger.Log("OnShowGUI");
        modification.OnHideGUI += () => Logger.Log("OnHideGUI");
    }
    
    private static void OnGUI()
    {
        GUILayout.Label("Hello Vlad!");
        GUILayout.Button("Some Button");
    }

}
