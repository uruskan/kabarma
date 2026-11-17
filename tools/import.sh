#!/usr/bin/env bash
# Yeni ya da degisen varliklari Godot'a iceri aktarir (model ekledikten sonra calistir).
cd "$(dirname "$0")/.."
timeout 900 .tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path game --import > .tools/run/import.log 2>&1
grep -iE "error" .tools/run/import.log | grep -v "leaked\|in use at exit" | head -10
echo "import done"
