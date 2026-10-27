using System;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.ElementsSystem;
using Kingmaker.Modding;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.UnitLogic.Mechanics.Actions;
using UnityEngine;

namespace TrueSwordOfFaith;

[Serializable]
public class Settings
{
    public int MomentumGain = Main.MinMomentumGain;
    public int MinimumMomentum = Main.MaxMinimumMomentum;
    public int WeaponDamage = 12;
    public int WeaponMaxDamage = 18;
}

public static class Main
{
    internal const int MinMomentumGain = -50;
    internal const int MaxMomentumGain = 0;
    internal const int MinMinimumMomentum = 0;
    internal const int MaxMinimumMomentum = 175;
    internal const int MinWeaponDamage = 1;
    internal const int MaxWeaponDamage = 100;

    // SancticSwordOfFaithAbility and SancticSwordOfFaithWeapon, see OwlcatModificationSettings.json
    internal const string SwordOfFaithAbilityGuid = "20d5376f942f454daf37f2b81a81e7b1";
    internal const string SwordOfFaithWeaponGuid = "73e83b5b49c4461cb32324498c23474d";

    // Element ids from SancticSwordOfFaithAbility.patch
    private const string AddMomentumActionId = "418548a0d6ad4c688d6d726fe6a1f759";
    private const string MinimumMomentumComponentId = "976c4aaa-5ad0-4b42-8072-f57b2b31a198";

    internal static OwlcatModification Mod;
    internal static Settings Settings;
    private static bool settingsDirty;

    [OwlcatModificationEnterPoint]
    public static void Load(OwlcatModification mod)
    {
        Mod = mod;
        // Stored in Modifications\TrueSwordOfFaith_Data.json
        Settings = mod.LoadData<Settings>();
        Settings.MomentumGain = Mathf.Clamp(Settings.MomentumGain, MinMomentumGain, MaxMomentumGain);
        Settings.MinimumMomentum = Mathf.Clamp(Settings.MinimumMomentum, MinMinimumMomentum, MaxMinimumMomentum);
        Settings.WeaponDamage = Mathf.Clamp(Settings.WeaponDamage, MinWeaponDamage, MaxWeaponDamage);
        Settings.WeaponMaxDamage = Mathf.Clamp(Settings.WeaponMaxDamage, Settings.WeaponDamage, MaxWeaponDamage);
        // The DLC & Mods cog refuses to open the window unless OnShowGUI is set.
        mod.OnShowGUI = () => { };
        // OnHideGUI doesn't fire on every way out of the menu, so OnGUI also saves after each change
        // and quitting saves anything still pending.
        mod.OnHideGUI = SaveSettings;
        mod.OnDrawGUI = OnGUI;
        Application.quitting += SaveSettings;
        new Harmony(mod.Manifest.UniqueName).PatchAll();
        mod.Logger.Log($"Sword of Faith momentum gain: {Settings.MomentumGain}, minimum momentum: {Settings.MinimumMomentum}, " +
            $"damage: {Settings.WeaponDamage}-{Settings.WeaponMaxDamage}");
    }

    private static void OnGUI()
    {
        bool abilityChanged = false;
        // Reversed so dragging right uses more momentum.
        abilityChanged |= IntSlider("Momentum used:", ref Settings.MomentumGain, MaxMomentumGain, MinMomentumGain);
        GUILayout.Label($"Momentum used when using Sword of Faith ({MaxMomentumGain} to {MinMomentumGain}).");
        GUILayout.Space(10f);
        abilityChanged |= IntSlider("Minimum momentum:", ref Settings.MinimumMomentum, MinMinimumMomentum, MaxMinimumMomentum);
        GUILayout.Label($"Momentum required to use Sword of Faith ({MinMinimumMomentum} to {MaxMinimumMomentum}).");
        GUILayout.Space(10f);

        bool weaponChanged = false;
        if (IntSlider("Weapon min damage:", ref Settings.WeaponDamage, MinWeaponDamage, MaxWeaponDamage))
        {
            // Raising min damage drags max damage up with it.
            Settings.WeaponMaxDamage = Mathf.Max(Settings.WeaponMaxDamage, Settings.WeaponDamage);
            weaponChanged = true;
        }
        if (IntSlider("Weapon max damage:", ref Settings.WeaponMaxDamage, MinWeaponDamage, MaxWeaponDamage))
        {
            // Lowering max damage drags min damage down with it.
            Settings.WeaponDamage = Mathf.Min(Settings.WeaponDamage, Settings.WeaponMaxDamage);
            weaponChanged = true;
        }
        GUILayout.Label($"Sword of Faith weapon damage ({MinWeaponDamage} to {MaxWeaponDamage}). Max damage is never lower than min damage.");
        GUILayout.Space(10f);
        GUILayout.Label("Changes take effect immediately.");

        if (abilityChanged)
        {
            ApplyToLoadedBlueprint(SwordOfFaithAbilityGuid);
        }
        if (weaponChanged)
        {
            ApplyToLoadedBlueprint(SwordOfFaithWeaponGuid);
        }

        settingsDirty |= abilityChanged || weaponChanged;
        // Wait until the slider is released so dragging doesn't write the file every frame.
        if (settingsDirty && GUIUtility.hotControl == 0)
        {
            SaveSettings();
        }
    }

    private static void SaveSettings()
    {
        try
        {
            Mod.SaveData(Settings);
            settingsDirty = false;
        }
        catch (Exception ex)
        {
            Mod.Logger.Exception(ex);
        }
    }

    // left may be greater than right for a reversed slider.
    private static bool IntSlider(string label, ref int value, int left, int right)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(220f));
            int newValue;
            if (left <= right)
            {
                newValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, left, right, GUILayout.Width(300f)));
            }
            else
            {
                // Slide over the mirrored value so the left end maps to left and the right end to right.
                int position = Mathf.RoundToInt(GUILayout.HorizontalSlider(left + right - value, right, left, GUILayout.Width(300f)));
                newValue = left + right - position;
            }
            GUILayout.Label(newValue.ToString(), GUILayout.Width(40f));
            if (newValue == value)
            {
                return false;
            }
            value = newValue;
            return true;
        }
    }

    private static void ApplyToLoadedBlueprint(string guid)
    {
        try
        {
            ApplySettings(guid, ResourcesLibrary.TryGetBlueprint(guid));
        }
        catch (Exception ex)
        {
            Mod.Logger.Exception(ex);
        }
    }

    internal static bool IsConfiguredBlueprint(string guid)
    {
        return string.Equals(guid, SwordOfFaithAbilityGuid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(guid, SwordOfFaithWeaponGuid, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ApplySettings(string guid, SimpleBlueprint blueprint)
    {
        if (string.Equals(guid, SwordOfFaithWeaponGuid, StringComparison.OrdinalIgnoreCase))
        {
            ApplyWeaponSettings(blueprint);
        }
        else if (string.Equals(guid, SwordOfFaithAbilityGuid, StringComparison.OrdinalIgnoreCase))
        {
            ApplyAbilitySettings(blueprint);
        }
    }

    private static void ApplyWeaponSettings(SimpleBlueprint blueprint)
    {
        if (blueprint is not BlueprintItemWeapon weapon)
        {
            Mod.Logger.Error("Could not find the Sword of Faith weapon to update");
            return;
        }

        weapon.WarhammerDamage = Settings.WeaponDamage;
        weapon.WarhammerMaxDamage = Settings.WeaponMaxDamage;
    }

    private static void ApplyAbilitySettings(SimpleBlueprint blueprint)
    {
        if (blueprint is not BlueprintScriptableObject bp)
        {
            return;
        }

        var addMomentum = bp.ComponentsArray
            .OfType<AbilityEffectRunAction>()
            .SelectMany(c => c.Actions?.Actions ?? new GameAction[0])
            .OfType<WarhammerContextActionAddMomentum>()
            .FirstOrDefault(a => a.name?.Contains(AddMomentumActionId) ?? false);
        if (addMomentum?.Value != null)
        {
            addMomentum.Value.Value = Settings.MomentumGain;
        }
        else
        {
            Mod.Logger.Error("Could not find the Sword of Faith momentum action to update");
        }

        var minimumMomentum = bp.ComponentsArray
            .OfType<AbilityMinimumMomentum>()
            .FirstOrDefault(c => c.name?.Contains(MinimumMomentumComponentId) ?? false);
        if (minimumMomentum != null)
        {
            minimumMomentum.MinimumMomentum = Settings.MinimumMomentum;
        }
        else
        {
            Mod.Logger.Error("Could not find the Sword of Faith minimum momentum component to update");
        }
    }
}

// Blueprints are loaded lazily, so apply the settings whenever the game (re)loads one of ours.
// Runs after MicroPatches has applied the .patch files.
[HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.GetResourceReplacement))]
internal static class ApplySettingsOnLoad
{
    // Must be a pass-through postfix (returns __result): Harmony runs every void postfix before any
    // pass-through one, so a void postfix would run before MicroPatches' and have the .patch values
    // written over it.
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("MicroPatches")]
    private static object Postfix(object __result, OwlcatModification __instance, object resource, string guid)
    {
        if (__instance == Main.Mod && Main.IsConfiguredBlueprint(guid))
        {
            Main.ApplySettings(guid, (__result ?? resource) as SimpleBlueprint);
        }
        return __result;
    }
}
