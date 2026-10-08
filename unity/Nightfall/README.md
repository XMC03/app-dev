# Nightfall for Unity

A Unity 2D desktop version of the three-chapter top-down forest adventure. All sprite artwork and sound effects are generated locally in C#; no Asset Store downloads, browser embedding, or API keys are needed.

## Open and play

1. Install **Unity Hub** from <https://unity.com/download>.
2. In Hub, install **Unity 6.6**. This project is pinned to **6000.6.4f1**. If your installed Unity 6.6 editor has a different `6000.6` patch number, use Hub’s editor-version selector to open it with that installed editor. Unity may update the project’s patch version on import. Install your desktop platform's build-support module if you want to export a standalone game.
3. Download/extract the GitHub repository, or clone it.
4. In Unity Hub, choose **Projects → Add → Add project from disk**. Select the **`unity/Nightfall`** folder: the folder containing `Assets`, `Packages`, and `ProjectSettings`.
5. Open the project and wait for Unity to import and compile its scripts.
6. Double-click **`Assets/Scenes/Nightfall.unity`** in the Project window, or choose **Nightfall → Open game scene** from the menu.
7. Press Unity's **Play ▶** button, select the **Game** tab, and click **Enter the forest**.

The forest and interface are created when Play starts. The scene is deliberately minimal in Edit mode; you do not need to assign sprites or wire up components manually.

## Controls

| Action | Control |
| --- | --- |
| Move | WASD or arrow keys |
| Dash | Hold either Shift key while moving |
| Pause / resume | P or Escape, or the Pause button |
| Start / continue / retry | Enter or the screen's main button |
| Restart chapter | R or Restart chapter |
| Sound | Sound button; initially off |

Collect eight crystals to open the gate in the **northeast corner**. Reach it to advance. Cross all three glades to win. Each glade restores your three hearts and dash stamina. Spirits navigate around trees. Losing or restarting preserves completed chapters during the current run.

Release Shift to recharge dash, especially after it is depleted. The game pauses when the application loses focus. The best successful campaign time and sound preference use Unity's `PlayerPrefs`; chapter progress is not saved across application launches.

This version targets **desktop keyboard and mouse**. The browser edition's touch buttons have not been ported.

## Build a standalone game

1. In **File → Build Profiles**, select Windows, macOS, or Linux, and switch to that platform. Install the matching build-support module through Unity Hub if needed.
2. Choose **Nightfall → Build desktop game** and select an output folder. The menu includes the game scene automatically.
3. Run the resulting executable/application. Share the **entire output folder**, including its accompanying data files, with players. They do not need Unity installed.

You can also use Unity's normal Build Profiles window: `Assets/Scenes/Nightfall.unity` is already enabled in the scene list.

## Structure

- `Assets/Scripts/NightfallRules.cs` — independent C# gameplay simulation and obstacle navigation.
- `Assets/Scripts/NightfallGame.cs` — Unity input, camera, sprite rendering, interface, particles, sound and persistence.
- `Assets/Scenes/Nightfall.unity` — startup scene with the game component attached.
- `Assets/Editor/NightfallProject.cs` — scene-opening and desktop-build menus.
- `ProjectSettings` / `Packages` — editor version, desktop settings and required built-in modules. The renderer uses Unity's built-in render pipeline and legacy keyboard input.

## Validation status

The C# rules compile and pass executable checks for movement, collision, pause/restart, damage, dash, enemy navigation, objective reachability, and the full three-chapter campaign with active enemies. All Unity C# source files pass a C# 9 syntax check, and scene/build references and metadata GUIDs are checked.

**Unity Editor is not installed in the development environment used to create this project. Editor import, Play mode, graphics, Unity audio, and standalone builds have not been run here.** The C# checks do not replace those checks. After opening the project, confirm the Console has no errors, play a chapter, pause/resume, try sound, and build on your chosen desktop platform.

To run the independent checks with .NET SDK 8 or newer, from the repository root:

```sh
dotnet run --project unity/RuleChecks/RuleChecks.csproj --configuration Release
python3 unity/validate_project.py
```

The checks use compiler assemblies included with the .NET SDK and require no NuGet packages. Do not add `RuleChecks` to the Unity Assets folder.
