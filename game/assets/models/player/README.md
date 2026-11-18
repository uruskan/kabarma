# Player character: "Tulumlu" colonist

Generated with Genex's character lane (Meshy, one-shot `direct-text`, rigged), 2026-11-17.

- Generation id: `cmwki6h6s038n3gt9vgvkv9gy` (Studio job `5fb2eb0c-1102-4d8e-8608-16ddf76efab6`, delivered by wait job `af89e8bf-172c-4223-9f1d-38cf4762a14e`)
- Options: `direct-text`, height 1.6, pose a-pose, polycount 10000, texture 2k
- Credits spent: **64** for the character with its free controller pack. A second paid batch of library clips was attached later (wait job `f05fdd46-efc3-4329-8aeb-f0d24fa05fb6`, catalog ids 28, 59, 178, 181, 187, 261, 274, 611).
- Prompt: "Chunky stylised low-poly space colonist, big head one third of body, no neck. Rounded off-white space helmet, front is a dark navy glass visor, no face. Blue one-piece work overalls with warning-yellow trims. Oversized dark grey gloves and big dark grey boots. Boxy rounded backpack. Soft chunky bevelled shapes, no thin details, smooth shading, flat saturated colours."
- Raw deliveries (all original files, including the unrigged source mesh and the pose QA and trajectory JSON files): `res://assets/genex/af89e8bf-172c-4223-9f1d-38cf4762a14e/` and `res://assets/genex/f05fdd46-efc3-4329-8aeb-f0d24fa05fb6/` (not in git; on the main checkout).

## How player.glb is built

`player.glb` is made by `pipeline/merge_player.py` (Blender 5.3, no credits). It reads `game/data/player_anims.json` for clip ids and source action names.

```powershell
$GX = "C:\Users\uruskan\Documents\GitHub\kabarma\game\assets\genex"
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --factory-startup --python-exit-code 1 -P pipeline\merge_player.py -- `
  --base "$GX\af89e8bf-172c-4223-9f1d-38cf4762a14e\chunky-stylised-low-poly-space-colonist-cmwki6h6-rigged-a1-glb.glb" `
  --clips "$GX\af89e8bf-172c-4223-9f1d-38cf4762a14e" "$GX\f05fdd46-efc3-4329-8aeb-f0d24fa05fb6" `
  --anims game\data\player_anims.json --out game\assets\models\player\player.glb
```

1. **glTF stage (plain Python):** the clip GLBs have no skin, so Blender would import them as empty objects and drop their keys. The script copies their animation channels into the skinned base GLB by node name, under the ids from `player_anims.json`.
2. **Blender stage:** it scales location keys by the armature scale, removes horizontal hips motion from clips marked `inPlace`, applies the scale, and adds a `Backpack` bone (child of the chest bone `Spine02`).
   - **The bag is a separate rigid mesh.** Its faces are cut out of the body mesh into `Backpack_Mesh`, which has no skin and is parented to the `Backpack` bone. Godot imports it as a `BoneAttachment3D` on the skeleton, so the bag cannot change shape in any clip; it only turns and moves with the chest (owner's decision, 18 November).
   - **Which faces count as the bag:** every face centre inside the measured bag box (|x| < 0.265, z 0.74–1.315) that is behind y = 0.095. Between y = 0.03 and 0.095, only grey faces count; the navy suit and yellow trims stay on the body. On the top edge, faces weighted to the head stay too, because they belong to the helmet.
   - **Seam pinning:** body vertices that sit on the cut seam, plus grey "bridge" vertices that joined the bag to the back, are weighted fully to `Spine02`. This keeps the seam closed.
   - No code animates the `Backpack` bone. Every action gets its own NLA track, with the action slot set explicitly so all clips export.
3. **Check:** each clip must last at least 0.2 s and have at least 20 channels. The `Backpack` joint must exist, and `Backpack_Mesh` must be unskinned and a child of that joint. No body vertex may be weighted to `Backpack`. Otherwise the script exits with code 1. It also fails if a clip that is not marked `placeholder` is missing.
4. **Measure:** `pipeline/measure_bag.py --glb game/assets/models/player/player.glb --bone Backpack --max 0.002` samples every clip and prints:
   - bone location and scale tracks;
   - the bag's deviation from its rest shape in the bone's rigid frame;
   - grey body vertices left in the bag box.

   On 18 November, before the split, the bag edges moved 8–37 cm in the bone frame. After the split, the bag moves 0.00 cm in all 15 clips. No clip has bone scale tracks, or location tracks other than `Hips`.

To add a clip that is produced later, put its Genex folder in `--clips`, set `action` in `player_anims.json`, remove `placeholder`, and run the command again.

## Mesh facts (player.glb)

- 1 skinned mesh, **10,348 triangles**, 10,830 vertices. The art bible's player budget is 8,000, and Meshy's lowest setting is 10,000 faces.
- Skeleton: 25 joints. The 24 from Meshy are Hips, Spine, Spine01, Spine02, neck, Head, headfront, head_end, L/R Shoulder, Arm, ForeArm, Hand, UpLeg, Leg, Foot and ToeBase. The merge adds **Backpack** (parent `Spine02`). No vertex is weighted to it; it carries the separate rigid `Backpack_Mesh` (2,010 vertices, 1,512 faces).
- Scale applied (the `Armature` node is 1.0; bone units are metres).
- Bounds: 1.056 m wide × 1.60 m tall × 0.57 m deep. Feet at y = 0, centred on the origin, facing +Z.
- 1 material (double-sided). On import, Godot extracts the textures to `player_Image_0.jpg` (base colour), `player_Image_1.jpg` and `player_Image_2.jpg`. The older `player_Material_0*.jpg/png` files came from the previous merge and are no longer used by `player.glb`.

## Clips in player.glb (15)

| Id | Source action | Length | Used in M1 for |
|---|---|---|---|
| `idle` | Idle_3 (catalog 243) | 10.0 s | standing |
| `walk` | controller `walking_man` | 1.04 s | walking; also the stand-in for the 7 other walk directions |
| `run` | controller `running` | 0.67 s | running; also the stand-in for the 7 other run directions |
| `sprint` | run_fast_10_inplace (657) | 0.83 s | sprint |
| `jump` | Regular_Jump (466) | 1.88 s | cut in code into `jump_start`, `jump_air` (loop, hips flattened) and `jump_land` |
| `hurt` | Hit_Reaction (178) | 1.67 s | taking damage |
| `knockdown` | Knock_Down | 2.54 s | (ragdoll is used instead) |
| `shock_fall` | Electrocuted_Fall | 6.38 s | (not used in M1) |
| `carry` | Carry_Heavy_Object_Walk_inplace | 6.54 s | heavy carry (M2) |
| `push` | Push_and_Walk_Forward | 8.04 s | (M1 cart push is a code layer) |
| `kneel` | Female_Crouch_Pick_Up_Place_Side | 9.54 s | (M1 planting is a code pose) |
| `cheer` | Victory_Cheer | 9.38 s | gestures (M4) |
| `wave` | Big_Wave_Hello | 5.38 s | gestures (M4) |
| `walk_casual` | Casual_Walk_inplace (613) | 4.21 s | (not used in M1) |
| `crouch_walk` | Cautious_Crouch_Walk_Forward_inplace (616) | 1.17 s | (not used in M1) |

None of the clips has root motion; the CharacterBody3D moves in code. Loop modes are set in code from `player_anims.json`.

Clips built in code at run time (`game/scripts/actors/PlayerPoses.cs`): `plant`, `eat`, `sleep`, `getup_front_pose`, `getup_back_pose`. Code layers (`PlayerBody.cs`): `push_cart`, `wade`.

The full list of 30 M1 clips, with stand-ins and production requests, is `game/data/player_anims.json` (`m1: true`). The separate animation-only GLBs in this folder (`idle.glb`, `walk.glb`, and so on) are earlier single-clip exports and are not loaded by the game.

## Known deviations from the art bible

- **Overalls are dark navy (about #132C47), not cooperative blue #3E78B2.** The main texture colours are navy (~45%), mid grey #868781 (~18%), off-white and yellow (~11%), and dark slate (~11%). This would fail the palette check (ΔE < 8). Fix it in Godot without credits: tint or brighten the blue in the base-colour texture, or recolour the material.
- There are a few small texture details (stripe trims, a couple of text-like decals) on the clothing. They are minor at game-camera distance.
- 10,348 triangles is about 30% over the 8,000 player budget.

`.import` files are made by Godot on first open.
