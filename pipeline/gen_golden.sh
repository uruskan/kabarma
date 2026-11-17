#!/usr/bin/env bash
# M1 altin varliklari (URETIM.md §4) - Tripo API. Her biri once --dry-run ile dogrulanir, gecerliyse calistirilir.
# Kullanim: pipeline/gen_golden.sh [ad ...]   (bos: hepsi). Ciktilar .tools/tripo/<ad>/, sonuc JSON .tools/tripo/<ad>.json
set -u
cd "$(dirname "$0")/../.tools/tripo" || exit 1
STYLE="Stylised hand-painted game asset, grounded believable materials with subtle wear, chunky readable silhouette, soft bevelled forms, no thin spikes, warm sunny sci-fi farm on a golden meadow moon."
Q=(--model tripo-v3.1 -p texture_quality=detailed -p texture_version=v3.5)

declare -A PROMPT FACE THEN RETARGET
PROMPT[zipzip]="A small mischievous rabbit-like pest creature standing upright on two big hind feet, round plump body, long upright ears, big round eyes, tiny front paws held up, short fluffy tail. Warm orange-tan fur with cream belly. $STYLE"
FACE[zipzip]=6000; THEN[zipzip]="rig-check,rig:rig_type=biped"; RETARGET[zipzip]="preset:idle preset:run preset:jump preset:hurt"
PROMPT[salgam]="A single fresh turnip vegetable with leaves: round bulb, white lower half blending into a purple top, a thin tap root, a bunch of four broad green leaves. $STYLE"
FACE[salgam]=4000
PROMPT[tezgah]="A sturdy workbench crafting station for a space farmer: thick wooden worktop on a blue painted metal frame, a vice clamp on one end, a pegboard back with a few big tools, a small drawer, stubby legs. $STYLE"
FACE[tezgah]=8000
PROMPT[paketleme]="A packing bench station for crates: wooden table with a roll of brown paper on a metal bar, a stack of small wooden crates, a big stamping handle press, yellow safety trims, blue metal legs. $STYLE"
FACE[paketleme]=8000
PROMPT[el_arabasi]="A chunky farm wheelbarrow: deep blue metal tub, one big rubber front wheel with yellow hub, two wooden handles with grips, two short metal legs at the back. $STYLE"
FACE[el_arabasi]=6000; THEN[el_arabasi]="segment:segmentation_granularity=simple"
PROMPT[ocak]="A cozy hearth stove beacon that is the heart of a farm base: round stone-and-metal body, glowing orange firebox window on the front, a blue domed metal cap with a short chimney, two small indicator lamps, a wide stable base. $STYLE"
FACE[ocak]=8000
PROMPT[kapsul]="A small landing pod capsule from a farming cooperative: rounded off-white hull with blue and yellow bands, a big square front hatch door, four short landing legs, a round cooperative logo on the side, scorch marks at the bottom. $STYLE"
FACE[kapsul]=10000; THEN[kapsul]="segment:segmentation_granularity=simple"
PROMPT[capa]="A handheld multi-tool gadget for a space farmer: chunky pistol-grip body in off-white plastic and blue metal, a round yellow emitter nozzle at the front, a big top handle, a small glowing teal gauge, a slot on top for swappable heads. $STYLE"
FACE[capa]=4000

ITEMS=("$@"); [ ${#ITEMS[@]} -eq 0 ] && ITEMS=(zipzip salgam tezgah paketleme el_arabasi ocak kapsul capa)
for n in "${ITEMS[@]}"; do
  args=("${PROMPT[$n]}" "${Q[@]}" -p face_limit="${FACE[$n]}" --name "$n")
  [ -n "${THEN[$n]:-}" ] && args+=(--then "${THEN[$n]}")
  plan=$(tripo make "${args[@]}" --dry-run --json 2>/dev/null)
  ok=$(echo "$plan" | python -c "import sys,json;d=json.load(sys.stdin);print('1' if d['valid'] else '0', d['generation_credits_estimate'], '|'.join(d['warnings']), '|'.join(d['errors']))")
  echo "PLAN $n $ok"
  [ "${ok%% *}" = "1" ] || { echo "SKIP $n (invalid plan)"; continue; }
  tripo make "${args[@]}" --out "$n" --json --yes --no-open > "$n.json" 2> "$n.err"
  code=$?
  echo "DONE $n exit=$code $(python -c "import json;d=json.load(open('$n.json'));print(d.get('credits_consumed'),d.get('model_file'))" 2>/dev/null)"
  [ $code -eq 4 ] && { echo "STOP: credits"; break; }
  if [ $code -eq 0 ] && [ -n "${RETARGET[$n]:-}" ]; then
    rig=$(python -c "import json;print(json.load(open('$n.json'))['task_id'])")
    tripo anim retarget "$rig" --animation ${RETARGET[$n]} --animate-in-place --out "$n/anim" --json --yes --no-open > "$n.anim.json" 2> "$n.anim.err"
    echo "ANIM $n exit=$? $(python -c "import json;d=json.load(open('$n.anim.json'));print(d.get('credits_consumed'),d.get('model_file'))" 2>/dev/null)"
  fi
done
tripo balance
