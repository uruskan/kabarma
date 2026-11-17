#!/usr/bin/env bash
# Kullanim: tools/run.sh <ad> [--window] -- <oyun argumanlari>
# Ornek:   tools/run.sh bot -- --bot --fast=3 --quit_after=400 --mute
#          tools/run.sh shots --window -- --bot --fast=2 --shots=5,30 --quit_after=40
set -u
cd "$(dirname "$0")/.."
NAME=${1:-run}; shift || true
HEAD="--headless"
if [ "${1:-}" = "--window" ]; then HEAD="--resolution 1600x900"; shift; fi
[ "${1:-}" = "--" ] && shift
G=.tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe
mkdir -p .tools/run
dotnet build game/Kabarma.Game.csproj -nologo -v q 2>&1 | grep -E " error |rror\(s\)" | head -20
timeout ${TIMEOUT:-900} $G $HEAD --path game -- "$@" > .tools/run/$NAME.log 2>&1
echo "exit=$? log=.tools/run/$NAME.log"
grep -E "BOOT|STEP|BOT|NIGHT|SOLD|REPORT|RESULT|SCREENSHOT|QUIT|PLAYER" .tools/run/$NAME.log | head -80
echo "--- errors:"
grep -E "ERROR|SCRIPT ERROR|Unhandled|Exception|   at " .tools/run/$NAME.log | sort | uniq -c | sort -rn | head -25
