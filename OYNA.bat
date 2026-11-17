@echo off
rem Kabarma MVP: derle ve oyna (cift tikla).
cd /d "%~dp0"
set G=.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe
if not exist "%G%" (
  echo Godot .NET bulunamadi: .tools\Godot_v4.7.2-stable_mono_win64\ klasorunu kontrol et.
  pause
  exit /b 1
)
dotnet build game\Kabarma.Game.csproj -nologo -v q
if errorlevel 1 ( echo Derleme hatasi. & pause & exit /b 1 )
start "" "%G%" --path game
