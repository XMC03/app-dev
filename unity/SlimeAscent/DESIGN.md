# GDD coverage and tuning

The user supplied **Slime Ascent: Devour the Dungeon** as the design for a new Unity game. Assignment prompts such as “define the gameplay” are worksheet context, not additional instructions to rewrite the PDF or submit academic work.

| GDD requirement | Implementation |
| --- | --- |
| Villain slime in a parallel Underworld | Slime player, opening narrative, three dungeon floors and separate human-world ending. |
| Three hand-built floors with rooms and tunnels | Lower/Middle/Upper scenes; each has three tile rooms joined by narrow corridors, with floor-specific enemy populations. |
| Rat, Bat, Lizard, Beetle, Warrior, Mage plus Guardian | All seven prey categories keep their HP/rewards. The requested human designs add Warrior/Rogue melee and Cleric/Ranger/Healer caster variants. Lizard and ranged humans use projectiles; small prey can flee; Guardian uses a circular telegraphed slam. |
| Move, Bite, Slam, Tackle, Devour, Hide, gates, evolution | GDD keyboard/mouse bindings, with arrows and P offered as additional controls. |
| Patrol, notice, chase, flee and vision cones | Cone-based suspicion with wall occlusion; state changes, route finding around walls, weakened-creature fleeing. |
| Heroes hunt monsters; player chooses opportunities | Opposing factions attack nearby rivals and leave weakened bodies or corpses. |
| Five learnable traits | Enhanced Senses, Echo Sense, Fire Resistance, Harden and Mana Sense. |
| Full fire evolution chain | Lizard Devours unlock resistance; repeat Devours plus Essence buy immunity and Flame Body in Tab. |
| Essence powers skills | Four learned slots; costs, cooldowns, finite Essence and Devour replenishment. |
| HP, damage, speed and Biomass progression | XP levels increase all four; Biomass is finite digestion load. |
| Fire and poison gas chain reactions | Three connected Oil/Gas tiles per corresponding room; timed propagation and damage ticks. Slam barrels or ignite with Flame Spit. |
| Pressure plate to falling rocks | Plate/linked control triggers two rock tiles with a visible delay. |
| Final Guardian carries the Core | Core appears only after defeat, requires held-E Devour, then unlocks the final F gate. |
| Floor-start checkpoint and death restart | One PlayerPrefs checkpoint containing entry progress; death reloads the current scene after three seconds. |
| HUD and feedback | HP/XP/Essence/Biomass/detection bars, skill costs/cooldowns, Devour prompt and progress ring, enemy class/HP and wind-up warnings, boss warning, effects and messages. |
| Sound and one music loop per floor | Locally synthesized feedback clips and three distinct ambient note loops; separate ending music. |
| Pixel art, four directions, evolution effects | Four-direction animated slime and monster banks; five human designs with six supplied-style combat poses and horizontal mirroring; themed tiles, props, effects and auras. True human walking/back-view frames were not supplied. |
| Opening and human-world ending | Included illustrated backgrounds; opening floor diagram and ending narrative overlay; fleeing human sprites in the separate ending scene. |
| Fixed 16:9, following top-down camera | Orthographic fixed-angle follow; reference 1280×720; letterboxing. |
| No feature creep | No online play, gear/inventory, crafting, shops, quests, procedural maps, extra floors/bosses or playable human-world area. |

## Explicit decisions for unspecified details

These are prototype tuning choices, not additional requirements from the PDF:

- Small Rift Gates are available immediately. Optional hunting prepares you for stronger opponents; the GDD does not specify an XP quota to open them. Only the final Rift is Core-locked.
- “Weakened” means at or below **25% HP**. Devour needs **1.25 seconds** within **1.05 units** and clear sight. The target remains locked while eligible. Releasing E, leaving range, losing visibility, hiding, taking damage, attacking, Flame Spit or dashing interrupts it.
- Species skills unlock on the first matching Devour, deterministically, so progression can be tested. No unspecified random skill-drop odds are introduced.
- Initial HP **36**, Bite damage **7**, speed **3.2 units/s**, Biomass capacity **6**. Each level adds **12 HP**, **2 damage**, **0.12 speed** (bounded increase) and **2 Biomass capacity**.
- XP requirement is **30 + 20 × (level − 1)**. Biomass digestion removes **0.18 units/s**. Prey mass ranges from **1** (Rat/Bat) to **4** (Warrior/Core).
- Bite cooldown **0.25s**; Slam **1.1s**; Tackle **1.2s**. Slam deals **2.5×** Bite damage; Tackle **1.3×**.
- Skill slots: **1 Flame Spit** (6 Essence), **2 Harden** (8), **3 Echo Sense** (5), **4 Mana Sense** (5).
- Heat Immunity requires **2 cumulative Lizard Devours + 8 Essence**; Flame Body requires **3 + 14 Essence**. Tab pauses the game while choosing upgrades.
- A floor-entry checkpoint restores full HP and resets digestion load. Current-floor XP/traits/Essence gains are lost on death; previous-floor gains remain. No mid-room saves.
- Hide is held Shift near a green marked crack, with no movement, attacks, Devour or skills while hidden.
- Collision is implemented as circle-versus-tile-box math in the shared rules, with subdivided movement to prevent tunneling. Hazard spreading uses tile adjacency, not physics simulation.

## Requested human classes and mechanics update

The follow-up request authorizes the five supplied human class designs and mechanics improvements. They reuse the two GDD human reward/trait categories, preserving checkpoint data and the five-trait progression. Every class appears in the existing campaign; no additional enemy slots are needed.

| Class | Wind-up | Action | Recovery |
| --- | --- | --- | --- |
| Warrior | 0.45s | 22-damage melee; 1.35-unit, 60-degree half-angle cone. | 1s |
| Rogue | 0.28s | 16-damage melee; same cone, 2.8-unit/s pursuit. | 0.8s |
| Cleric | 0.45s | 15-damage projectile, speed 5. | 1.6s |
| Ranger | 0.45s | 13-damage arrow, speed 7; fires within 4.5 units. | 1.8s |
| Healer | 0.65s heal / 0.45s attack | Restores 12 HP within 4 units to a living human, or fires a 10-damage spell. | 4s heal / 1.6s attack |

Other normal melee creatures wind up for 0.3 seconds. Normal attacks can be interrupted by Slam/Tackle's 0.3-second stagger. The Guardian keeps its original 1.1-second circular slam warning and is immune to knockback/stagger. A defeated Guardian clears its warning.

Ranged attacks commit their direction when aiming starts. They sample travel in segments of at most 0.08 units; wall tests run before hit/hazard tests. Visibility traverses crossed grid cells, including thin corners. Heals check life state, range and sight again when the cast completes.

Dodge/Slam inputs buffer for 0.15 seconds; priority is Dodge, then Slam, then Bite. Dodge travels for 0.2 seconds at 8 units/s, without stacking walking, and damages each encountered creature once. The rules retain elapsed frame time through at-most-0.02-second substeps and cap catch-up at 0.25 seconds; edge-triggered commands are consumed once. A scene/phase change ends the current catch-up loop.

Difficulty, camera framing, art readability, audio volume and timing need human Play-mode evaluation in Unity. The independent checks cover rules and data references, not the editor's rendering or scene lifecycle.
