---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# Technology Stack

**Analysis Date:** 2026-10-09

## Languages

**Primary:**
- C# (two dialects in one repo, deliberately kept apart)
  - Client plugin: `LangVersion 7.3`, compiles to `.NET Framework 3.5` — `src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:5-6`. No interpolation `$""`, no `?.`, no `Action` with more than 4 parameters (enforced by `csc.exe` of the .NET Framework — see `docs/operations.md:61-64`).
  - Dedicated server: `LangVersion latest`, `.NET 8.0`, `Nullable enable`, `ImplicitUsings enable` — `src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj:5-8`.
- Shared contract code (`src/OriCoopPlus/OriCoopShared/*.cs`) is compiled into **both** targets, so it is restricted to C# 5 constructs (`const`, `enum`, `[Serializable]` classes) — see the comment in `src/OriCoopPlus/OriCoopShared/NetProtocol.cs:3-4`.

**Secondary:**
- PowerShell — build and tooling: `src/OriCoopPlus/OriCoopBepInEx/build.ps1`, `scripts/*.ps1` (~30 reverse-engineering/inspection helpers).

## Runtime

**Environment:**
- Client: Unity 5.3.2f1, 32-bit Mono, hosted by BepInEx 5.x. Loaded as `OriCoopBepInEx.dll` from `<ORI_DIR>\BepInEx\plugins\` (see `README.md:78-117`, `docs/bepinex-architecture.md:3-4`).
- Server: .NET 8 desktop/console runtime on Windows. `OriCoopDedicatedServer.exe` runs standalone, not hosted by the game.
- Client threading: Unity main thread for all gameplay/Unity API access; a dedicated background `System.Threading.Thread` for UDP receive (`src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:157-159`), with a main-thread dispatch queue (`OriCoopPlugin.EnqueueMainThread`, `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:39-48`, drained in `Update()` at `:683-696`).
- Server threading: TAP/async single receive loop + `Channel<T>` producer/consumer (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:83-126`), plus `System.Threading.Timer` for retry and sweep ticks (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:69-71`).

**Package Manager:**
- NuGet (SDK-style `PackageReference`). Restore recorded in `src/OriCoopPlus/OriCoopBepInEx/obj/OriCoopBepInEx.csproj.nuget.g.props`.
- Lockfile: **not committed** (no `packages.lock.json`, no `nuget.config` found).

## Frameworks

**Core:**
- BepInEx 5.x (client plugin host) — `BaseUnityPlugin` + `ConfigEntry<T>` via `[BepInPlugin("com.ikkikuuro.oricoop", "Ori Coop", "0.1.0")]` at `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:15-16`.
- HarmonyLib 2.x (`0Harmony`) — method patching: `_harmony.PatchAll()` at `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:294-295`. Patches live in `src/OriCoopPlus/OriCoopBepInEx/Patches/*.cs`.
- Unity IMGUI (legacy HUD) — `OnGUI()` + `GUIStyle` in `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:599-676`, gated off by default.
- Native game UI cloning — the mod injects a "Ori Coop" button into the game's own pause menu by cloning `CleverMenuItem`/`MessageBox` (`src/OriCoopPlus/OriCoopBepInEx/UI/NativeUIHelper.cs`, `OriCoopMenuScreen.cs`, `InventoryScreenPatch.cs`; documented in `docs/native-ui-architecture.md`).
- Dedicated server: **no framework** — plain `Microsoft.NET.Sdk` console app with BCL-only code (`System.Net.Sockets`, `System.Threading.Channels`, `System.Text.Json`, `System.Security.Cryptography`).

**Testing:**
- No unit-test framework present (no xUnit/NUnit/MSTest `PackageReference`, no `*.test.*`/`*.spec.*` files).
- `SmokeProbe` (`src/OriCoopDedicatedServer/SmokeProbe/SmokeProbe.csproj`) is a headless BCL-only smoke harness, not a test framework: it spawns the real server and drives the wire protocol, printing `SMOKE_OK`/`SMOKE_FAIL: <reason>` (`src/OriCoopDedicatedServer/SmokeProbe/Program.cs:8-13,56-63`).

**Build/Dev:**
- `dotnet build` (MSBuild) for the server and the solution — `src/OriCoopPlus/OriCoopPlus.sln`.
- `csc.exe` invoked directly from PowerShell for the net35 client, because the .NET Framework compiler enforces C# 5 — `src/OriCoopPlus/OriCoopBepInEx/build.ps1:45,96`.
- Reverse-engineering toolset in `scripts/` (Mono.Cecil-based inspectors, `dump.exe`/`dump2.exe`, `scripts/build.rsp` for `csc` response files). These reference game assemblies under `<ORI_DIR>\oriDE_Data\Managed\`.

## Key Dependencies

**Critical:**
- `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 — the **only** NuGet `PackageReference` in the repo; `PrivateAssets="All"`, build-time only (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:15`). Required to compile `net35` without the full .NET Framework targeting pack.
- `Assembly-CSharp.dll` — the game's entire gameplay assembly (`SeinCharacter`, `Game.Characters`, `GameplayCamera`, `CharacterAnimationSystem`, `CameraFrustumOptimizer`, `TextureAnimationWithTransitions`). Referenced by absolute path with `Private=False` (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:38-41`).
- `UnityEngine.dll`, `UnityEngine.UI.dll` — game-provided, `Private=False` (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:30-37`).
- `BepInEx.dll`, `0Harmony.dll` — vendored in `API/Client/` as a fallback when the game directory is absent (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:19-28`).

**Infrastructure:**
- `System.Net.Sockets.UdpClient` — the entire transport on both sides (client `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:112`; server `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:56`).
- `System.Threading.Channels` — bounded (1024, `DropOldest`) receive queue on the server (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:37-42`).
- `System.Text.Json` — `serverconfig.json` read/write (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs:158,200`).
- `System.Security.Cryptography.RandomNumberGenerator` — per-session token generation (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs:43-51`).

## Configuration

**Environment:**
- The only environment variable read anywhere is `ORI_MANAGED_PATH`, consumed by `src/OriCoopPlus/OriCoopBepInEx/build.ps1:14` to locate `oriDE_Data\Managed`. Build paths otherwise come from the MSBuild `$(OriGameDir)` property, which probes two hardcoded Steam locations with a final fallback (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:9-11`).
- **No `.env` / credentials / secret files exist in this repository** (verified by a scan for `.env*`, `*.env`, `*.pem`, `*.key`, `id_rsa*`). Runtime configuration is entirely local files.

**Build:**
- `src/OriCoopPlus/OriCoopPlus.sln` — both `OriCoopBepInEx` and `OriCoopDedicatedServer`.
- `src/OriCoopPlus/OriCoopBepInEx/build.ps1` — csc-based net35 build.
- `scripts/build.rsp` — hand-maintained `csc` response file for the client (stale absolute paths from a different machine, `c:\Users\raiso\...`).
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj` — includes the shared contract via `<Compile Include="..\..\OriCoopPlus\OriCoopShared\*.cs" />` (`:14`), so the shared files are compiled twice (once per target) rather than linked as an assembly.

**Runtime configuration files:**
- Client: BepInEx config file `BepInEx\config\com.ikkikuuro.oricoop.cfg`, sections `Network` / `UI` / `Diagnostics` — bound at `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:271-276`. Keys: `Host` (127.0.0.1), `Port` (7777), `PlayerId` (-1), `Nickname` (Ori_Player), `EnableLegacyFloatingHud` (false), `AnimVerbose` (false).
- Server: `serverconfig.json` next to the exe — filename constant at `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs:24`; 8 canonical bool flags in wire order at `:27-37` (`AllowTeleport`, `ShareAbilities`, `ShareStoryOnly`, `ShareWorldEvents`, `ShareDoorsAndLevers`, `ShowNicknames`, `ClientColors`, `EntitySync`), plus a per-player color map. Never reset on boot (`:19-20`).
- Server logs: `Logs/server.log` (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs:62-72`). Both `Logs/` and `bin/`+`obj/` are gitignored (`.gitignore`).

## Platform Requirements

**Development:**
- Windows (win32).
- .NET SDK capable of building `net8.0` (docs reference SDK 8.0.425, `docs/operations.md:53`).
- Visual Studio 2022 or VS Code with .NET support (`README.md:37`, `docs/operations.md:7-8`).
- A local Ori DE install so `oriDE_Data\Managed` and `BepInEx\core` exist; the csproj auto-detects `C:\Program Files (x86)\Steam\steamapps\common\Ori DE` or `D:\SteamLibrary\steamapps\common\Ori DE`.
- `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` for the client-only `build.ps1` path.

**Production:**
- Windows, Ori DE (Steam), BepInEx 5.x installed in the game folder.
- Deployment is a manual file copy — there is no installer, no package manager, no CI (see INTEGRATIONS.md).

---

*Stack analysis: 2026-10-09*
