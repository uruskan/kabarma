# Player character: "Tulumlu" colonist

Generated with Genex's character lane (Meshy, one-shot `direct-text`, rigged), 2026-11-17.

- Generation id: `cmwki6h6s038n3gt9vgvkv9gy` (Studio job `5fb2eb0c-1102-4d8e-8608-16ddf76efab6`, delivered by wait job `af89e8bf-172c-4223-9f1d-38cf4762a14e`)
- Options: `direct-text`, height 1.6, pose a-pose, polycount 10000, texture 2k
- Credits spent: **64** (one character; no previews, no paid animation). The clips came with the character's free controller pack.
- Prompt: "Chunky stylised low-poly space colonist, big head one third of body, no neck. Rounded off-white space helmet, front is a dark navy glass visor, no face. Blue one-piece work overalls with warning-yellow trims. Oversized dark grey gloves and big dark grey boots. Boxy rounded backpack. Soft chunky bevelled shapes, no thin details, smooth shading, flat saturated colours."
- Raw delivery (all original files, including the unrigged source mesh and the pose QA and trajectory JSON files): `res://assets/genex/af89e8bf-172c-4223-9f1d-38cf4762a14e/`

## Mesh facts (player.glb)

- 1 skinned mesh, **10,348 triangles**, 10,830 vertices. The art bible's player budget is 8,000, and Meshy's lowest setting is 10,000 faces.
- Skeleton: 24 joints (Hips, Spine, Spine01, Spine02, neck, Head, headfront, head_end, L/R Shoulder, Arm, ForeArm, Hand, UpLeg, Leg, Foot, ToeBase), root node `Armature` (scale 0.01; bone units are cm inside it).
- Bounds: 1.056 m wide × 1.60 m tall × 0.57 m deep. Feet at y = 0, centred on the origin, facing +Z (Meshy rigs rest facing +Z).
- 1 material (`Material_0.004`, double-sided), with 2k textures for base colour (JPEG), normal (JPEG) and metallic-roughness (PNG).
- Rig pose QA passed (a-pose).

## Files

| File | What it is | Animation(s) inside | Length |
|---|---|---|---|
| `player.glb` | **Use this one.** Rigged mesh with every clip merged in and renamed | `idle`, `walk`, `walk_casual`, `run`, `run_fast`, `jump`, `crouch_walk` | see below |
| `idle.glb` | Animation-only GLB (skeleton, no mesh) | `Idle_3` | 9.97 s |
| `walk.glb` | Animation-only | `Armature\|walking_man\|baselayer` | 1.07 s |
| `walk_casual.glb` | Animation-only (catalog 613 "Casual Walk") | `Casual_Walk_inplace` | 4.17 s |
| `run.glb` | Animation-only | `Armature\|running\|baselayer` | 0.67 s |
| `run_fast.glb` | Animation-only (catalog 657 "Run Fast 10") | `Armature\|run_fast_10_inplace\|baselayer` | 0.83 s |
| `jump.glb` | Animation-only (catalog 466 "Regular Jump") | `Regular_Jump` | 1.87 s |
| `crouch_walk.glb` | Animation-only (catalog 616 "Cautious Crouch Walk Forward") | `Cautious_Crouch_Walk_Forward_inplace` | 1.13 s |

Animation names in `player.glb`, with their sources:

| Name | Source clip | Length | Loop | Notes |
|---|---|---|---|---|
| `idle` | Idle_3 (catalog 243) | 9.97 s | yes | |
| `walk` | controller walk (`walking_man`) | 1.07 s | yes | in place (hips move only up and down) |
| `walk_casual` | Casual_Walk_inplace (613) | 4.17 s | yes | slower, relaxed walk |
| `run` | controller run (`running`) | 0.67 s | yes | in place |
| `run_fast` | run_fast_10_inplace (657) | 0.83 s | yes | sprint |
| `jump` | Regular_Jump (466) | 1.87 s | no | in place; hips rise about 0.68 m. Take-off, air and landing in one clip |
| `crouch_walk` | Cautious_Crouch_Walk_Forward_inplace (616) | 1.13 s | yes | |

None of the clips has root motion. Move the CharacterBody3D in code. In Godot, set the looping clips to loop in the import settings (Advanced Import, Animation, Loop Mode), or set `loop_mode` in code. Glb loop flags are not carried.

## Not included

- **Hurt/hit reaction:** the catalog has `Hit_Reaction` (id 178) and `Hit_Reaction_1` (179), but each costs extra credits (about 5–6) on top of the character, so they were left out under the 64-credit cap. Add one later with `character.animate` and `action: "Hit_Reaction"` on `cmwki6h6s038n3gt9vgvkv9gy`.
- **Separate fall/land:** the catalog has none. `jump` holds the whole arc. For a long fall, freeze or slow `jump` mid-air.

## Known deviations from the art bible

- **Overalls are dark navy (about #132C47), not cooperative blue #3E78B2.** The main texture colours are navy (~45%), mid grey #868781 (~18%), off-white and yellow (~11%), and dark slate (~11%). This would fail the palette check (ΔE < 8). Fix it in Godot without credits: tint or brighten the blue in the base-colour texture, or recolour the material.
- There are a few small texture details (stripe trims, a couple of text-like decals) on the clothing. They are minor at game-camera distance.
- 10,348 triangles is about 30% over the 8,000 player budget.

`.import` files are made by Godot on first open.
