@echo off
setlocal enabledelayedexpansion

echo === Among Us Mod Installer (Windows) ===
echo.

:: Find Among Us installation
set "STEAM_PATH=C:\Program Files (x86)\Steam\steamapps\common\Among Us"
set "EPIC_PATH=C:\Program Files\Epic Games\AmongUs"

if exist "%STEAM_PATH%\Among Us.exe" (
    set "GAME_PATH=%STEAM_PATH%"
    echo Found Steam install: %STEAM_PATH%
) else if exist "%EPIC_PATH%\Among Us.exe" (
    set "GAME_PATH=%EPIC_PATH%"
    echo Found Epic install: %EPIC_PATH%
) else (
    echo Among Us not found at default locations.
    set /p GAME_PATH="Enter Among Us folder path: "
    if not exist "!GAME_PATH!\Among Us.exe" (
        echo Invalid path. Exiting.
        pause
        exit /b 1
    )
)

echo.

:: Step 1: Install BepInEx if not present
if not exist "%GAME_PATH%\BepInEx" (
    echo [1/3] Downloading BepInEx 6 Bleeding Edge...
    powershell -Command "& {Invoke-WebRequest -Uri 'https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.755+3fab71a.zip' -OutFile '%TEMP%\bepinex.zip'}"

    echo Extracting BepInEx...
    powershell -Command "& {Expand-Archive -Path '%TEMP%\bepinex.zip' -DestinationPath '%GAME_PATH%' -Force}"
    del "%TEMP%\bepinex.zip"

    echo BepInEx installed!
    echo.
    echo [2/3] First launch to generate IL2CPP interop assemblies...
    echo Starting Among Us — close it after it reaches the main menu...
    start "" "%GAME_PATH%\Among Us.exe"
    echo.
    echo Press any key AFTER you've closed Among Us...
    pause >nul
) else (
    echo [1/3] BepInEx already installed.
    echo [2/3] Interop assemblies exist.
)

:: Step 3: Build and install mod
echo [3/3] Building and installing mod...
dotnet build AmongUsMod\AmongUsMod.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED. Make sure .NET 6 SDK is installed:
    echo   https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

:: Create plugins dir if needed
if not exist "%GAME_PATH%\BepInEx\plugins" mkdir "%GAME_PATH%\BepInEx\plugins"

:: Copy mod DLL to plugins
copy /Y "AmongUsMod\bin\Release\net6.0\AmongUsMod.dll" "%GAME_PATH%\BepInEx\plugins\" >nul

echo.
echo === Installation Complete! ===
echo.
echo Mod installed to: %GAME_PATH%\BepInEx\plugins\AmongUsMod.dll
echo.
echo Features:
echo   - Always Impostor: You will always be the Impostor
echo   - AI NPC Bots: Empty slots filled with bots that move, do tasks, and vote
echo.
echo Launch Among Us normally from Steam/Epic to play with mods.
echo Check %GAME_PATH%\BepInEx\LogOutput.log for mod activity.
echo.
pause
