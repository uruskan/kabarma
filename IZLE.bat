@echo off
rem Botun ilk 10 dakikayi oynamasini izle (cift tikla).
cd /d "%~dp0"
set G=.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe
dotnet build game\Kabarma.Game.csproj -nologo -v q
start "" "%G%" --path game -- --bot
