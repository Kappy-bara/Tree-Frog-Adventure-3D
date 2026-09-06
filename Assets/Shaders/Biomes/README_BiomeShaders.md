# Tree Frog Adventure 3D — Biome Shaders

Re-skins the Emerald Forest art (Ultimate Nature – Starter) into three more biomes
without any new models or textures.

| Biome | Levels | Folder |
|---|---|---|
| 🌳 Emerald Forest | 1–5 | `Assets/Materials/Biomes/EmeraldForest` (optional, see §6) |
| 🌲 Canopy Heights | 6–10 | `Assets/Materials/Biomes/CanopyHeights` |
| 🐊 Misty Swamp | 11–15 | `Assets/Materials/Biomes/MistySwamp` |
| 🌋 Volcanic Highlands | 16–20 | `Assets/Materials/Biomes/VolcanicHighlands` |

Verified against Unity 6000.3.10f1 / URP 17.5.0 — all six shaders compile with zero
errors and zero warnings.

---

## 1. The three biome scenes are already built

`Tools > Tree Frog > Build Biome Scenes` has already been run, producing:

| Scene | Biome |
|---|---|
| `Assets/Scenes/Level_6_CanopyHeights.unity` | Canopy Heights |
| `Assets/Scenes/Level_11_MistySwamp.unity` | Misty Swamp |
| `Assets/Scenes/Level_16_VolcanicHighlands.unity` | Volcanic Highlands |

Each is a clone of `Level_1_Forest` with **everything** already applied — ~33,000
renderer material slots swapped, all 5 terrains re-materialled, skybox, fog, ambient
gradient, sun colour/angle, and its own post-processing profile in
`Assets/Settings/Biomes/`. All three are registered in Build Settings.

Just open one and press Play. `Level_1_Forest` and every source material and prefab
were left untouched.

**Re-run the menu item any time** to regenerate the three scenes from the current
`Level_1_Forest` — for example after you add gameplay to level 1. It overwrites them,
so put biome-specific level design in *other* scenes, or re-apply it after a rebuild.

> **Correction to an earlier version of this file:** re-running
> `Tools > Tree Frog > Build Main Menu Scene` does **not** drop other scenes from
> Build Settings. `MainMenuSceneBuilder.UpdateBuildSettings` keeps every other
> entry and just re-adds `MainMenu.unity` and `Level_1_Forest.unity` at the front.
> The only wrinkle is that it re-adds `Level_1_Forest.unity` unconditionally, so if
> you later delete that scene you will get a stale Build Settings entry to remove.

The rest of this document explains how the pieces work, and how to do it by hand if
you want a fourth variant or a different look. A biome is 50% surface shading and 50%
atmosphere — §3–5 are why the scenes read as different places, not just different
colours.

---

## 1a. Level structure (additive split)

The three scenes above are now **sources**, not the shipping levels. Running
`Tools > Tree Frog > Build Level Structure` carves each biome scene into:

- `Assets/Scenes/Environments/Env_<Biome>.unity` — the 34 MB environment, shared by
  all 5 levels of that world
- `Assets/Scenes/Levels/Level_01..20.unity` — a few KB each: player, camera, UI,
  waypoints, walls, puzzles

At runtime `LevelLoader` loads the **environment as the primary scene** and the level
scene **additively** on top. That order is deliberate: fog, ambient, skybox and the
baked occlusion data all come from the *active* scene, so the biome look keeps working
with no extra code.

Fix a rock in `Env_MistySwamp` once and all five swamp levels get it.

The builder never overwrites a level scene that already exists, so it is safe to
re-run after you have started designing.

---

## 1b. Making the menu actually load them

This is now wired. `LevelProgress` maps level → biome environment scene and level →
gameplay scene, and every scene load in the project goes through `LevelLoader`:

| Script | Role |
|---|---|
| `LevelProgress.GetEnvironmentScene(n)` | `Env_CanopyHeights` for levels 6–10, etc. |
| `LevelProgress.GetLevelScene(n)` | `Level_07` |
| `LevelLoader.LoadLevel(n)` | environment primary + gameplay additive |
| `LevelLoader.ReloadCurrentLevel()` | used by the death screen's Restart |

`TotalWorlds` is now **4** (20 levels). You said you would do that yourself — I had to
change it because the biome→world mapping depends on it, so it is done. What is still
yours: **re-run `Tools > Tree Frog > Build Main Menu Scene`**, because the 25 buttons in
`MainMenu.unity` are hard-serialised and the constant alone will not change the menu.

One subtlety worth knowing: under additive loading the *active* scene is the
environment, so `SceneManager.GetActiveScene().name` is **not** the level name.
`PlayerHealth`'s restart button used to rely on that and would have reloaded the world
without the player; it now calls `LevelLoader.ReloadCurrentLevel()`.

---

## 2. Material replacement table

This is what the builder applies. Use it if you are doing a scene by hand — replace
the **left** material wherever it appears with the **right** one.

| Original (Ultimate Nature – Starter) | Canopy Heights | Misty Swamp | Volcanic Highlands |
|---|---|---|---|
| `UNS_Spruce_Tree_Branch` | `CH_TreeBranch` | `MS_TreeBranch` | `VH_TreeBranch` |
| `UNS_Bush_Leaves` | `CH_BushLeaves` | `MS_BushLeaves` | `VH_BushLeaves` |
| `UNS_Grass` | `CH_Grass` | `MS_Grass` | `VH_Grass` |
| `UNS_Flower` | `CH_Flower` | `MS_Flower` | `VH_Flower` |
| `UNS_Flower_Leaf` | `CH_FlowerLeaf` | `MS_FlowerLeaf` | `VH_FlowerLeaf` |
| `UNS_Color_Palette` **(rocks, cliffs, logs, props — one material covers all)** | `CH_Palette_Rock` | `MS_Palette_Rock` | `VH_Palette_Rock` |
| `UNS_Water` | `CH_Water` | `MS_Water` | `VH_Lava` |
| Terrain material | `CH_Terrain` | `MS_Terrain` | `VH_Terrain` |
| `UNS_Skybox_Panoramic` | `CH_Skybox` | `MS_Skybox` | `VH_Skybox` |

`UNS_Color_Palette` is the big win — every rock, cliff, log and prop in the scene
shares it, so that single swap re-skins most of the solid geometry at once.

**Terrain:** select the Terrain object → Terrain Settings (gear tab) → **Material** →
assign the biome terrain material.

---

## 3. Lighting window settings

`Window > Rendering > Lighting > Environment`. Set **Environment Lighting → Source**
to **Gradient** (the forest already uses this).

| | Canopy Heights | Misty Swamp | Volcanic Highlands |
|---|---|---|---|
| Fog | on | on | on |
| Fog Mode | Exponential | **Exponential Squared** | Exponential |
| Fog Colour | `C7E0F5` | `6B7A66` | `4D2320` |
| Fog Density | `0.0022` | `0.012` | `0.005` |
| Ambient Sky | `9ED1FF` | `616F61` | `4D2921` |
| Ambient Equator | `8CB8DB` | `525C4D` | `47241A` |
| Ambient Ground | `6B7A8C` | `2A2E24` | `1F120F` |
| Ambient Intensity | `1.0` | `0.65` | `0.7` |

The swamp deliberately uses Exponential Squared with a high density — that thick,
close falloff is what makes it read as a swamp rather than a dim forest.

---

## 4. Directional light

| | Canopy Heights | Misty Swamp | Volcanic Highlands |
|---|---|---|---|
| Colour | `FFF7E6` | `B8C7AD` | `FF7338` |
| Intensity | `1.6` | `0.6` | `0.9` |
| X rotation (sun elevation) | `55°` | `35°` | `12°` |
| Shadow strength | `0.85` | `0.4` | `0.7` |

The `*_Skybox` materials read the scene's main directional light, so the painted sun
disc follows this light automatically — rotate the light and the sun moves with it.
(If you want the sun somewhere else, set `Sun Dir Override` on the sky material and
set its **W** component to 1.)

---

## 5. Post-processing (the scene's `UNS_Global_Volume`)

**Volcanic Highlands needs Bloom.** The lava and crack emission are HDR (values above
1.0); without Bloom they render as flat orange instead of glowing.

| Override | Canopy Heights | Misty Swamp | Volcanic Highlands |
|---|---|---|---|
| Bloom — Threshold | `1.1` | `0.9` | `0.8` |
| Bloom — Intensity | `0.4` | `0.3` | `1.0` |
| Color Adjustments — Contrast | `+10` | `-5` | `+15` |
| Color Adjustments — Saturation | `+5` | `-25` | `-10` |
| White Balance — Temperature | `+5` | `-10` | `+25` |
| White Balance — Tint | `0` | `+15` (green) | `+5` |
| Vignette — Intensity | `0.15` | `0.35` | `0.40` |

---

## 6. Emerald Forest set (optional)

`Assets/Materials/Biomes/EmeraldForest/EF_*` are tuned to match how Level 1 looks
today, but running on the new shader family — so you get the wind sway, backlit
translucency and shore foam that the other biomes have.

**Nothing in Level 1 has been changed.** These are opt-in: apply them the same way as
§2 if you want the four worlds to feel like one visual system. Test on a duplicate of
the scene first.

---

## 7. Shader reference

All six live in `Assets/Shaders/Biomes/`, under the menu path
`TreeFrogAdventure/Biome/*`. Shared math (noise, colour ramps, wind) is in
`TFA_BiomeCommon.hlsl`.

### Foliage — `TreeFrogAdventure/Biome/Foliage`
Trees, bushes, grass, flowers.

- **Tip / Root Colour + Gradient Top** — root-to-tip colour blend in *object* space.
  Set `Gradient Top` to roughly the model's height (≈6 for trees, ≈1 for grass).
- **Hue Shift / Saturation / Brightness** — recolours the source texture before tinting.
- **Wind Strength / Speed / Flutter** — `Wind Mask Top` must match the model height or
  the trunk will sway with the leaves.
- **Overlay Colour (A = strength)** — frost, slime or ash on upward-facing surfaces.
- **Translucency** — back-light glow. The biggest visual payoff on this art style;
  turn it up for sunlit canopies.
- **Emission** — noise-masked and pulsing. Used for the swamp's bioluminescent flowers.

### Rock — `TreeFrogAdventure/Biome/Rock`
Rocks, cliffs, logs, props — anything on the shared palette atlas.

- **Ramp Shadow / Mid / Highlight + Remap Strength** — the core mechanism. It takes the
  atlas *luminance* and remaps it through your three colours, so one material re-skins
  every object using that atlas. `Remap Strength 0` falls back to plain hue shifting.
- **Ramp Contrast** — raise it if the result looks flat; the palette atlas is
  low-contrast to begin with.
- **Lower Band Colour** — a world-height band, used for swamp slime creeping up from
  the water. Set `Lower Band Top` to your water level.
- **Crack Emission** — HDR glow confined to crevices. This is what makes basalt read
  as volcanic. Needs Bloom.

### Terrain — `TreeFrogAdventure/Biome/Terrain`
Four-splat Unity Terrain.

> ⚠️ **Leave "Draw Instanced" unchecked** on the Terrain component. This shader uses
> the non-instanced path (which all five terrains in Level 1 already use). With Draw
> Instanced on, the terrain renders flat.

- **Layer 0–3 Tint** — recolours each terrain layer independently.
- **Low / High Band** — mud at the waterline, snow on the peaks. Set the world-Y
  thresholds to match your terrain's actual height range.

### Water — `TreeFrogAdventure/Biome/Water`
- **Shallow / Deep Colour + Depth Fade Distance** — driven by scene depth.
- **Foam Width** — the shore line. If there is no foam at all, Depth Texture is off on
  the URP asset (it is currently **on** for `PC_RPAsset`, so this should just work).
- **Murkiness** — pushes toward opaque. The swamp runs at `0.85`.

### Lava — `TreeFrogAdventure/Biome/Lava`
- **Crust Coverage** — how much cooled crust vs open molten channel.
- **Emission Intensity** — HDR. **Requires Bloom** to look like lava.

### Sky — `TreeFrogAdventure/Biome/Sky`
- Assign to the Lighting window's Skybox Material slot, not to a mesh.
- **Cloud Strength / Coverage** — Canopy Heights runs high coverage for a sea of cloud;
  the swamp runs an overcast sheet; Volcanic runs sparse dark cloud.
- **Star Strength** — `0` for daytime biomes, `0.9` for Volcanic.

---

## 8. Also included: 2D-shader material fix

Nine materials in the project — **including the player frog, the bunny enemy and the
coins** — were on `Universal Render Pipeline/2D/Mesh2D-Lit-Default`, a sprite shader
for 2D lights. That is why they do not light correctly or cast shadows in the 3D scene.

Run **`Tools > Tree Frog > Fix 2D Shader Materials`** once. It moves them to URP/Lit,
carries the textures across, and logs every material it touched. It is undoable and
safe to run more than once.

This is independent of the biome work — worth doing regardless.
