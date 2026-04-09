# Among Us Mod — Always Impostor + AI NPC Bots + 3D Crewmates

A BepInEx mod for Among Us (Windows/Steam) that:
1. **Always Impostor** — forces you to be Impostor every game
2. **AI NPC Bots** — fills empty player slots with bot players so you can play without enough humans
3. **3D Crewmates** — replaces the 2D sprites with primitive-based 3D models (body, visor, backpack, legs) that rotate to face movement direction and animate while walking

## Requirements

- **Among Us** (Steam or Epic, Windows)
- **.NET 6 SDK** — https://dotnet.microsoft.com/download/dotnet/6.0
- **Windows 10/11**

## Quick Install (Windows)

### Option A: Automated
1. Open a Command Prompt in this folder
2. Run: `install.bat`
3. Follow the prompts
4. Launch Among Us normally

### Option B: Manual

#### Step 1: Install BepInEx 6
1. Download `BepInEx-Unity.IL2CPP-win-x86` from https://builds.bepinex.dev/projects/bepinex_be
2. Extract into your Among Us folder (where `Among Us.exe` lives)
3. Launch Among Us once and close it (generates interop assemblies)

#### Step 2: Install Reactor
1. Download `Reactor.dll` from https://github.com/NuclearPowered/Reactor/releases
2. Place in `Among Us/BepInEx/plugins/`

#### Step 3: Build the mod
```
dotnet build AmongUsMod/AmongUsMod.csproj -c Release
```

#### Step 4: Install the mod
Copy `AmongUsMod/bin/Release/net6.0/AmongUsMod.dll` to `Among Us/BepInEx/plugins/`

#### Step 5: Play
Launch Among Us normally from Steam/Epic.

## File Structure

```
AmongUsMod/
  AmongUsMod/
    AmongUsModPlugin.cs    — Main plugin entry point
    AlwaysImpostor.cs      — Always-Impostor Harmony patches
    AINpcBots.cs           — AI NPC bot system
    ThreeDCrewmates.cs     — 3D crewmate models replacing 2D sprites
    AmongUsMod.csproj      — Project file with BepInEx/Reactor references
  NuGet.config             — Package sources for BepInEx and Reactor
  build.bat                — Build script (Windows)
  install.bat              — Full install script (Windows)
  README.md                — This file
```

## How It Works

### Always Impostor
- Hooks `RoleManager.SelectRoles` (runs on host after roles are assigned)
- Checks if the local player got an Impostor role
- If not, swaps roles with an existing Impostor (gives them Crewmate, gives you Impostor)
- Also hooks `GameStartManager.BeginGame` to ensure at least 1 impostor is configured

### AI NPC Bots
- Hooks the lobby `GameStartManager.Update` to spawn dummy players when below 10 players
- Hooks `PlayerControl.FixedUpdate` to give bots movement AI (random wandering)
- Crewmate bots complete tasks periodically
- Hooks `MeetingHud.Start` to schedule bot votes during meetings (random vote or skip)
- Bot state resets between games

### 3D Crewmates
- Hooks `PlayerControl.Start` to build a 3D rig (capsule body, sphere visor, cube backpack, capsule legs) parented to each player
- Hides the 2D `CosmeticsLayer` sprite renderers so only the 3D model shows
- Syncs the body material color to `Palette.PlayerColors[ColorId]`
- Hooks `PlayerControl.FixedUpdate` to rotate the rig toward movement direction and bob/swing legs while walking
- Destroys rigs on `PlayerControl.OnDestroy` and `AmongUsClient.OnGameEnd`
- v1 uses Unity primitives — no external asset bundles required. v2 will load FBX/GLB models from an AssetBundle

## Logs
Check `Among Us/BepInEx/LogOutput.log` for mod messages:
- `[AlwaysImpostor]` — role swap messages
- `[AINpc]` — bot spawn/movement/voting messages

## Uninstall
Delete `Among Us/BepInEx/plugins/AmongUsMod.dll` or remove the entire `BepInEx` folder.

## Compatibility
- Built for Among Us v2026.3.31 (Unity 2022.3, IL2CPP)
- Requires BepInEx 6 Bleeding Edge (IL2CPP support)
- Requires Reactor framework
- Host-only mod (only the game host needs it installed)
