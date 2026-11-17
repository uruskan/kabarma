#!/usr/bin/env bash
# M1 altin varliklari (URETIM.md §4) - Tripo API. Her biri once --dry-run ile dogrulanir, gecerliyse calistirilir.
# Kullanim: pipeline/gen_golden.sh [ad ...]   (bos: hepsi). Ciktilar .tools/tripo/<ad>/, sonuc JSON .tools/tripo/<ad>.json
set -u
cd "$(dirname "$0")/../.tools/tripo" || exit 1
STYLE="Stylised hand-painted game asset, grounded believable materials with subtle wear, chunky readable silhouette, soft bevelled forms, no thin spikes, warm sunny sci-fi farm on a golden meadow moon."
Q=(--model tripo-v3.1 -p texture_quality=detailed -p texture_version=v3.5)

declare -A PROMPT FACE THEN RETARGET QUAL
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


# M1 dunya cesitliligi (SANAT-SES §6: biyom basina 8-12 bitki, 4-6 kaya) - standart doku
PROMPT[cali]="A round meadow shrub bush with dense leafy clumps and a few clusters of small orange berries. $STYLE"; FACE[cali]=3000; QUAL[cali]=standard
PROMPT[bugday]="A clump of wild golden wheat stalks with heavy seed heads, growing from a small tuft of green leaves. $STYLE"; FACE[bugday]=3000; QUAL[bugday]=standard
PROMPT[cicek]="A cluster of meadow wildflowers: chunky orange and cream daisy-like blossoms on thick green stems with broad leaves. $STYLE"; FACE[cicek]=3000; QUAL[cicek]=standard
PROMPT[ot]="A tall clump of soft feathery meadow grass with plume tips, golden green colour. $STYLE"; FACE[ot]=2500; QUAL[ot]=standard
PROMPT[turuncu_agac]="A small round tree with a thick short trunk and a puffy crown of warm orange and amber leaves, autumn mood. $STYLE"; FACE[turuncu_agac]=5000; QUAL[turuncu_agac]=standard
PROMPT[egrelti]="A broad-leaf ground plant with five big rounded fan leaves spreading low from the centre, deep green with lighter veins. $STYLE"; FACE[egrelti]=2500; QUAL[egrelti]=standard
PROMPT[kaya_duz]="A wide flat weathered boulder, smooth rounded top, lavender grey stone with moss patches. $STYLE"; FACE[kaya_duz]=3000; QUAL[kaya_duz]=standard
PROMPT[kaya_dik]="A tall standing stone pillar slightly leaning, rounded worn edges, lavender grey with lichen. $STYLE"; FACE[kaya_dik]=3000; QUAL[kaya_dik]=standard
PROMPT[bakir]="A rounded rock outcrop with chunky veins and nuggets of shiny orange copper ore embedded, grey-purple stone. $STYLE"; FACE[bakir]=3000; QUAL[bakir]=standard
ITEMS=("$@"); [ ${#ITEMS[@]} -eq 0 ] && ITEMS=(zipzip salgam tezgah paketleme el_arabasi ocak kapsul capa)
for n in "${ITEMS[@]}"; do
  q=("${Q[@]}"); [ -n "${QUAL[$n]:-}" ] && q=(--model tripo-v3.1 -p texture_quality="${QUAL[$n]}" -p texture_version=v3.5)
  args=("${PROMPT[$n]}" "${q[@]}" -p face_limit="${FACE[$n]}" --name "$n")
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
