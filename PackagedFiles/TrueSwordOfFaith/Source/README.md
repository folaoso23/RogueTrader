# TrueSwordOfFaith settings DLL

Adds in-game settings for True Sword Of Faith. The settings appear behind the cog next to
the mod in **DLC & Mods**. Every change applies immediately, with no game restart.

| Setting | Range | Default | What it changes |
|---|---|---|---|
| Momentum used | 0 to -50 (slider reversed) | -50 | `WarhammerContextActionAddMomentum.Value` on SancticSwordOfFaithAbility |
| Minimum momentum | 0 to 175 | 175 | `AbilityMinimumMomentum.MinimumMomentum` on SancticSwordOfFaithAbility |
| Weapon min damage | 1 to 100 | 12 | `WarhammerDamage` on SancticSwordOfFaithWeapon |
| Weapon max damage | 1 to 100 | 18 | `WarhammerMaxDamage` on SancticSwordOfFaithWeapon |

Max damage can never be lower than min damage. Raising min damage pushes max damage up,
and lowering max damage pushes min damage down.

Settings are saved to `Modifications\TrueSwordOfFaith_Data.json` as soon as a slider is
released. They are also saved when the settings window closes and when the game quits.
They are clamped to the ranges above on load.

## Building

Requires the .NET SDK. From this folder:

```
dotnet build -c Release
```

The build copies `TrueSwordOfFaith.dll` into `..\Assemblies`, where the game loads it from.
The project references the game's DLLs from the default Steam install. If the game is
installed elsewhere, pass `-p:GamePath="<game folder>"`.

When packaging the mod, leave out `bin\` and `obj\` (or this whole folder). The game
only needs `Assemblies\TrueSwordOfFaith.dll`.

## How it works

**Entry point.** The game calls the method marked `[OwlcatModificationEnterPoint]` for
every DLL in `Assemblies\`. It runs after `OwlcatModificationSettings.json` is read and
before any blueprints load.

**Settings window.** The cog appears when `OnDrawGUI` is set. Clicking the cog opens the
window only if `OnShowGUI` is also set. Without it the game logs
`Mod TrueSwordOfFaith has no OnShowGui` and nothing opens. That's why `OnShowGUI` is set
to an empty callback. `OnHideGUI` saves the settings, but it doesn't fire on every way out
of the menu. So `OnGUI` also saves once a changed slider is released (`GUIUtility.hotControl == 0`),
and `Application.quitting` saves anything still pending.

**Applying values on load.** The `.patch` files use `PatchType: 2`, which only MicroPatches
understands. The game itself only knows Replace (0) and Edit (1). MicroPatches applies these
patches in a Harmony postfix on `OwlcatModification.GetResourceReplacement`. This DLL adds
its own postfix that runs after MicroPatches (`Priority.Last`, `HarmonyAfter("MicroPatches")`).
That postfix must return `__result`. Harmony runs all `void` postfixes before any postfix that
returns a value, and MicroPatches' postfix returns one. A `void` postfix would therefore run
first, and MicroPatches would write the `.patch` values over the settings.
That postfix writes the saved settings onto the freshly patched blueprint. The values in the
`.patch` files are therefore only fallbacks; the settings always win.

**Applying values live.** The game reads all four values from the blueprint at the moment it
uses them: momentum when the ability runs, the momentum requirement when checking if the
ability can be used, and damage in `RuleCalculateStatsWeapon`. Moving a slider fetches the
cached blueprint with `ResourcesLibrary.TryGetBlueprint` and changes the field in place, so
the next use picks up the new value. Item tooltips may show the old number until they refresh.

**Finding the right element.** The momentum action and the minimum-momentum component are
matched by the element ids used in `SancticSwordOfFaithAbility.patch` (`418548a0...` and
`976c4aaa...`). If a game update renames them, the log shows
`Could not find the Sword of Faith ... to update`.

## Adding another setting

1. Add a field with its default to `Settings`, and clamp it in `Load`.
2. Add a slider in `OnGUI` with `IntSlider`. Pass `left > right` for a reversed slider.
3. Write the value onto the blueprint in `ApplyAbilitySettings` / `ApplyWeaponSettings`.
   For a different blueprint, add its GUID to `IsConfiguredBlueprint` and `ApplySettings`.
4. Before relying on a live change, check that the game reads the field from the blueprint
   each time, rather than copying it onto the unit or item when it's created.
