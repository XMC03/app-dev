# Slime Ascent artwork

All artwork is included in `Assets/Resources/Art`. `SlimeArt.cs` loads the textures and slices their cells at runtime, so there is no Sprite Editor setup or Inspector wiring. Scenes still generate their objects when Play starts.

The original sprite atlases are drawn from geometry by `tools/generate_slime_art.py`, using Python and Pillow. The title and ending illustrations were generated with OpenAI image generation for this game and included unchanged as PNGs. No Asset Store package is required.

## Atlas contract

Every sprite cell is 64×64 pixels. Rows below are counted from the top; the loader converts to Unity's bottom-left texture coordinates.

| File | Dimensions | Layout |
| --- | --- | --- |
| `slime.png` | 1536×1024 | Rows: four forms × up/right/down/left. Columns: idle/walk/bite/slam/hurt/devour × four frames. |
| `enemies.png` | 256×1792 | Rows: Rat/Bat/Lizard/Beetle/Warrior/Mage/Guardian × four directions. Columns: four frames. |
| `tiles.png` | 768×192 | One row per floor, twelve variants per row. |
| `props.png` | 512×256 | Thirty-two row-major decorative and interactive props. |
| `effects.png` | 768×256 | Three effects per row, four frames per effect; twelve effects total. |
| `title.png`, `ending.png` | 1672×941 each | Full-screen illustrations, with quiet space at the left for text. |

`art-manifest.json` records form, action, direction, species and effect order. `art-preview.png` is an overview for browsing; it is not a gameplay screenshot.

Keep the PNG `.meta` files: they preserve Point filtering, disabled mipmaps, no compression, original non-power-of-two dimensions and a 4096 maximum texture size. Texture type is **Default**, since sprites are sliced by code. Changing it to Sprite/Multiple is unnecessary.

## Rebuild or customize

From the repository root:

```sh
python3 -m pip install Pillow
python3 tools/generate_slime_art.py
python3 tests/art/check_slime_art.py
```

The generator rewrites the five atlases, manifest and preview. It leaves the two illustrations and all `.meta` files untouched. To supply hand-drawn sprites, replace the PNGs while preserving the dimensions, transparent cell padding and documented order. Altering the atlas layout also requires updating `SlimeArt.cs` and the manifest.

Decorative floor details add no collision. Guardian and rock telegraphs use a round 30-pixel-radius frame at 32 pixels per unit; their display scales match the rules' 1.7-unit and 0.8-unit damage radii. Test any visual replacements in Unity Play mode for readability, including the Upper-floor boss encounter.
