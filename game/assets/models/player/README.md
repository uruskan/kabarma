# Player character: "Tulumlu" colonist (Tripo, 19 November 2026)

Replaces the Genex/Meshy model of 17 November (owner did not like it: bag cut out of the body, arms close to the torso, meshes stretching).

- Source: Tripo web, H3.1 text mode, auto-retopology (Smart Mesh, ~15k), humanoid auto-rig with the **Mixamo skeleton preset**. Prompts, credits and UI notes: `.tools/tripo_web/oyuncu/istemler.md` (not in git; main checkout).
- Raw files (main checkout, `.tools/tripo_web/oyuncu/`): `kabarma_oyuncu_rig.glb` (65 bones incl. fingers, 15 Tripo clips), `kabarma_canta.glb` (1.9M-triangle backpack, separate object).
- Credits for this pipeline: 0 (all work is local).

## How player.glb is built

`pipeline/build_player_tripo.py` (Blender 5.3) with `pipeline/prep_bag.py` for the bag. Old Meshy pipeline (`merge_player.py`, `measure_bag.py`) is no longer used for the player.

```powershell
$B = "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe"; $O = "<main checkout>\.tools\tripo_web\oyuncu"
& $B -b --factory-startup --python-exit-code 1 -P pipeline\prep_bag.py -- --in "$O\kabarma_canta.glb" --out "$O\kabarma_canta_low.glb"   # ~3,000 tris, 1K textures
& $B -b --factory-startup --python-exit-code 1 -P pipeline\build_player_tripo.py -- --new "$O\kabarma_oyuncu_rig.glb" `
  --old <Meshy player.glb from git history, commit before oyuncu2> --bag "$O\kabarma_canta_low.glb" `
  --anims game\data\player_anims.json --out game\assets\models\player\player.glb --bag-height 0.38
```

1. Body and bones scaled to 1.6 m (scale applied; Armature node stays 1.0). Bones **renamed to the old names** so no C# changed: `mixamorig:` prefix dropped; Mixamo `Spine`/`Spine1`/`Spine2` become `Spine02`/`Spine01`/`Spine` (old chain was Hips > Spine02 > Spine01 > Spine > neck > Head); `Neck` > `neck`, `HeadTop_End` > `head_end`. Finger bones and `Toe_End` are extra.
2. All 15 clips (idle, walk, run, sprint, jump, hurt, knockdown, shock_fall, carry, push, kneel, cheer, wave, walk_casual, crouch_walk) are **retargeted from the Meshy clips** (frame sampled at 30 Hz): new bone world rotation = old bone world rotation × a constant twist; bone directions follow the old clip, so A-pose vs T-pose rest difference disappears. Hips motion scaled by hip height ratio. Tripo's own idle, cheer and greet clips stayed in T-pose and its walk cycle (2.33 s) did not fit the game's walk speed, so none were used (`--keep-native` can opt in).
3. Backpack: `Backpack_Mesh` (3,000 tris) is a separate mesh weighted 100% to the chest bone `Spine`, so it moves rigidly and cannot change shape in any clip. (A bone-child bag, as in the Meshy pipeline, landed 0.24 m off-centre in Godot because the Mixamo bone frames differ; skinning avoids it.) An empty `Backpack` joint (child of `Spine`) stays for compatibility. Capa-on-back and bag charms are placed from the bag mesh box at run time (`CosmeticView.BagBox`, `Player.CalibrateBack`), not from `grips.json` `back` (that table is now only a fallback).
4. Check at the end of the script: all clips present and longer than 0.2 s, `Backpack` joint exists, `Backpack_Mesh` is skinned (Spine weight only).

## Facts

- Body about 13.6k triangles + bag 3k = 16.6k (budget 8,000 in the art bible; 10–15k in the task; over: optimization list). 2K body textures (JPEG, extracted by Godot), bag 1K.
- 67 joints. Hand bones already have the roll that `grips.json` sockets expect (+Y wrist to fingers, palm −Z, right thumb −X); sockets moved to `pos y 0.08, z −0.03` for the smaller hand.
- Visor: `Visor.cs` fits its 2D face patch on the helmet front automatically from the mesh above the head bone.
- Separate files `idle.glb`, `walk.glb` etc. in this folder are old single-clip exports, not loaded by the game.
