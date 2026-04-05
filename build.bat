@echo off
echo === Building Among Us Mod ===
dotnet build AmongUsMod\AmongUsMod.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo.
echo === Build Successful ===
echo Output: AmongUsMod\bin\Release\net6.0\AmongUsMod.dll
echo.
echo Copy AmongUsMod.dll to:
echo   [Among Us folder]\BepInEx\plugins\AmongUsMod.dll
echo.
pause
