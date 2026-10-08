# Slime Ascent: Devour the Dungeon

A Unity 6.6, single-player, offline, top-down dungeon action RPG prototype based on the supplied GDD. You play a fragile slime that steals the power of monsters and heroes, climbs three floors, defeats the Dungeon Guardian, Devours its Core, and escapes into the human world.

## Open it in Unity

1. On the `codex/slime-ascent` GitHub branch, choose **Code → Download ZIP**, then extract it.
2. Open **Unity Hub → Projects → Add → Add project from disk**.
3. Select the extracted **`unity/SlimeAscent`** folder, containing `Assets`, `Packages`, and `ProjectSettings`.
4. Open it with **Unity 6.6** (`6000.6.4f1`). If your 6000.6 patch differs, select your installed Unity 6.6 editor in Hub.
5. Wait for the scripts to compile. In the Project panel, open **Assets → Scenes → Opening**. You can also choose **Slime Ascent → Open opening scene** from the menu.
6. Press **Play ▶**, select the **Game** tab, and click **Begin a new ascent**.
7. Watch the short opening, or press Enter to skip it. The dungeon is generated when playing; Edit-mode scenes are intentionally minimal.

The project uses the built-in render pipeline. Included PNG sprite atlases and illustrations load automatically; sound is synthesized locally in C#. There are no separate asset downloads, API keys, or manual Inspector references to assign. It uses Unity's legacy Input Manager, which Unity 6.6 may mark deprecated; that warning does not mean the game has a compile error.

**Updating an earlier copy:** download the latest ZIP from `codex/slime-ascent` and add its `unity/SlimeAscent` folder as a fresh project in Hub. Copying only the scripts will omit the new `Assets/Resources/Art` textures and their import settings.

## Visual improvements

- Four visibly different slime forms, each with four directions and four-frame idle, movement, Bite, Slam/Tackle, hurt and Devour animations.
- Distinct Rat, Bat, Lizard, Beetle, Warrior, Mage and Guardian sprites, with facing cues and animation.
- Lower-floor moss, pools and fungi; Middle-floor carpets and hero camps; Upper-floor obsidian, crystals and sanctuary runes.
- Animated fire, gas, projectiles, impact effects, Devour essence, skill auras and active Rift glow; hit flashes and floating damage numbers.
- Circular Guardian and falling-rock warnings sized to their damage areas.
- Illustrated title/opening and human-world ending backgrounds, plus evolution-form previews in Tab.

See the [art preview](Assets/Resources/Art/art-preview.png) and [asset layout and customization guide](ART.md). Decorative scenery does not change map collision or gameplay rules.

## Controls

| Action | Control | Notes |
| --- | --- | --- |
| Move | WASD (arrows also supported) | Mouse sets your facing direction. |
| Bite | Left mouse button | Hold for repeated short-range attacks. |
| Slam | Right mouse button | Heavy forward attack; knocks prey back and smashes nearby barrels/linked trap controls. |
| Tackle / dodge | Space | Forward charge with brief damage protection. |
| Devour | Hold E | Stay close to defeated or weakened prey until the ring fills. |
| Hide | Hold Left Shift | Works only near the green marked cracks; release to leave. Attacks and skills are blocked while hidden. |
| Skills | 1–4 | Spend Essence on learned abilities. |
| Enter Rift Gate | F | Stand near the gate. The final gate requires the Dungeon Core. |
| Evolution menu | Tab | View traits and purchase fire-chain upgrades; simulation pauses. |
| Pause / resume | P or Escape | Focus loss also pauses. |

Aim/click in the dungeon area between the HUD and skill bar. Clicking menu controls does not fire a mouse attack.

## Your first run

1. Approach the rat in the first room from behind. Bite or Slam it, then hold E near its body.
2. Devour more small prey to gain levels, Essence and traits. Weak living prey can also be Devoured, but can flee or interrupt you.
3. Green cracks are safe hiding spots while Shift is held. Patrol cones and the detection meter show enemy awareness.
4. Slam the oil barrels to spread fire, or use learned Flame Spit. Gas chains work similarly. Yellow pressure plates warn before rocks fall on the marked linked tiles.
5. Use each small Rift Gate to climb. Hunting the optional prey first makes the next floor easier.
6. On the Upper floor, dodge the Guardian's marked slam area. Defeat it, hold E near the dropped Core, then press F at the Rift Gate.

Heroes and dungeon monsters can fight each other. This creates weakened prey and corpses that you can exploit.

## Progression

- **Rat:** Enhanced Senses — a nearby-threat warning contributes to your detection display.
- **Cave Bat:** Echo Sense — skill 3 reveals enemy directions for six seconds.
- **Fire Lizard:** Fire Resistance and Flame Spit (skill 1). In Tab, spend Essence after repeated Lizard Devours to evolve **Fire Resistance → Heat Immunity → Flame Body**.
- **Armored Beetle:** Harden (skill 2) reduces incoming damage for four seconds.
- **Hero Mage:** Mana Sense (skill 4) reveals Mage/Guardian directions for six seconds.
- **Hero Warrior:** XP, Essence and Biomass rewards; the GDD's five named traits remain the trait set.

Leveling raises maximum HP, damage, movement speed and Biomass capacity. Essence is consumed by skills. Biomass represents digestion load: consuming large prey fills it, and it decreases over time. If it is full, wait before Devouring again.

## Checkpoint and death

There is **one automatic local checkpoint at each floor's entrance**. It preserves the previous floors' level, XP, traits and Essence and restores full HP. Dying restarts the current floor after three seconds, discarding gains made after its entry checkpoint. The pause menu can restart immediately.

**Continue** resumes the saved floor entry after closing the game. Beginning a new run overwrites the checkpoint when the Lower floor starts. To clear it explicitly, choose **Slime Ascent → Clear local checkpoint**; the editor asks before removal.

## Scenes and builds

The project includes five actual scenes in build order:

1. `Opening` — menu and short opening.
2. `Lower` — introductory prey and hazards.
3. `Middle` — hero hunting parties and additional prey.
4. `Upper` — Guardian and Dungeon Core.
5. `HumanWorld` — separate non-playable escape ending with fleeing humans.

The fixed-angle orthographic camera follows the slime without zoom or rotation. The game uses a 1280×720 reference layout with a 16:9 viewport and letterboxing in other window shapes.

To export: install the matching desktop build-support module in Hub, select Windows/macOS/Linux in **File → Build Profiles**, then choose **Slime Ascent → Build desktop game**. Share the entire resulting build folder, including its data files. No executable build is included in this repository.

## Design scope and implementation decisions

See [GDD coverage and tuning](DESIGN.md) for the requirement mapping and the specific values chosen where the GDD leaves details open. The worksheet's prompts were treated as document context; the filled design supplied the gameplay requirements. The original assignment PDF and personal group information are not included in the repository.

This is a source prototype with included pixel sprite atlases, generated background illustrations and synthesized sound/music. It does not add multiplayer, crafting, equipment, quests, procedural levels, additional bosses, or a playable human world.

## Validation — what is and is not verified

**19 executable C# checks pass**, including source syntax, movement/collision, attacks, Devour and rewards, all five traits, the full fire chain, Essence skills, fire/gas propagation, linked rocks, hiding, faction battles, Guardian telegraphs, checkpoints, floor transitions, Core consumption, escape, and the initial rat encounter using directional input with the original enemies active.

Combat and transition checks also use controlled fixtures to isolate rules. They do **not** establish that a human can complete the whole campaign or that its difficulty is balanced.

**Unity Editor is not installed in the creation environment.** Unity assembly compilation, editor import, Play mode, visual output, sound/music playback, actual scene loading, and desktop builds have **not** been run here. Source syntax and metadata checks do not replace those checks. The first local Unity run should confirm the Console has no errors and exercise each floor, its gate transition, checkpoint restart and ending.

Independent checks, from the repository root with .NET SDK 8+ and Python 3:

```sh
dotnet run --project unity/SlimeChecks/SlimeChecks.csproj --configuration Release
python3 unity/validate_slime_project.py
```

These tools require no NuGet packages. Keep `SlimeChecks` outside the Unity Assets directory.

Optional artwork checks require Python with Pillow (`python3 -m pip install Pillow`):

```sh
python3 tests/art/check_slime_art.py
```

These checks cover all 612 nonempty atlas cells, distinct forms and enemy facings, action-frame variation, illustration dimensions and pixel-safe texture settings. They do not verify Unity rendering. Python and .NET are development tools; neither is required to open or play the project in Unity.
