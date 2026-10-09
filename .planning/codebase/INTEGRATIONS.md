---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# External Integrations

**Analysis Date:** 2026-10-09

## APIs & External Services

**None.** This repository contains no outbound network integration of any kind. A
grep for `HttpClient`, `WebClient`, `UnityWebRequest` and `System.Net.Http`
across `src/` returns **zero matches**. The mod is fully offline.

The only entities the code talks to are:

**Host game (Ori and the Blind Forest: Definitive Edition):**
- The client plugin runs inside the game process and reaches the game purely through compile-time references and runtime reflection:
  - `Assembly-CSharp.dll` — typed access to `SeinCharacter`, `Game.Characters.Sein`, `GameplayCamera`, `CharacterAnimationSystem`, `CameraFrustumOptimizer`, `TextureAnimationWithTransitions`, `SpriteAnimatorWithTransitions` (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:521-590`, `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs`).
  - `UnityEngine.dll` / `UnityEngine.UI.dll` — transform, input, GUI, materials.
  - `BepInEx.dll` — plugin lifecycle + `ConfigEntry` persistence.
  - `0Harmony.dll` — method patching (`docs/bepinex-architecture.md:82-87`).
  - Reflection is used only as a guarded fallback (e.g. `SeinCharacter.Speed` property set at `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:384-391`).
- The dedicated server does **not** load the game or any game assembly — it has zero external references (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj:13-15`).

**Non-integration links (content only, no code dependency):**
- Discord community invite `https://discord.gg/F2sHzGC3kb` — `README.md:7`.
- GitHub raw download of `Launcher.zip` — `README.md:9`.
- Third-party mod reference (HKMP) credited in `Modules/HKModules/Readme.md:1-3` — attribution only.

## Data Storage

**Databases:**
- None. No SQL, NoSQL, ORM, or embedded database anywhere in the repository.

**File Storage:**
- Server, written at runtime by `src/OriCoopDedicatedServer/OriCoopDedicatedServer/`:
  - `serverconfig.json` — 8 gameplay bools + per-player color map; `System.Text.Json`, indented, rewritten on every change (`Net/Game/ConfigStore.cs:158,200`). Loaded at boot without resetting values (`:19-20`).
  - `Logs/server.log` — UTF-8 append, levels Debug/Info/Warning/Error (`Net/Game/ServerBoot.cs:62-72`, `Net/Diagnostics/ServerLogger.cs:43-67`).
- Client, written by BepInEx / the game, not by mod code:
  - `BepInEx\config\com.ikkikuuro.oricoop.cfg` — written via `Config.Save()` (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:128,257`).
  - `BepInEx\LogOutput.log` — written by the BepInEx host; mod writes only through `Logger.LogInfo/LogWarning/LogError/LogMessage`.
- Vendor blobs committed in the repo (no runtime I/O): `API/Client/*.dll`, `API/Server/*`, `Modules/**/*.dll`, `scripts/*.txt` (assembly dumps), `Launcher.zip`.
- Paths: `/Logs`, `/src/OriCoopDedicatedServer/OriCoopDedicatedServer/Logs` are gitignored (`.gitignore`).

**Caching:**
- None. No Redis, no MemoryCache, no HTTP cache. The only in-memory "cache" is the animation catalog prewarmed at runtime (`src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs`), which holds game objects, not external data.

## Authentication & Identity

**Auth Provider:** None — custom, no third-party identity service.

**Implementation:** a three-way handshake over the mod's own versioned UDP envelope, defined in `src/OriCoopPlus/OriCoopShared/NetProtocol.cs:32-38` and documented in `docs/protocol.md:73-120`:

1. Client → server: `Hello 100` with `clientId = -1`, `token = 0`, `protoVer` byte + nick (int32-prefixed ASCII).
2. Server → client: `Welcome 101` with `assignedId int`, `token uint`, `serverVer byte`. ID allocation is server-authoritative; the client's configured `PlayerId` is only a hint and is overwritten (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:466`).
3. Client → server: `Confirm 102`; server flips `IsReady = true` (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs:19`), then unicasts `COLOR` + `CONFIG_SYNC` reliably.

**Session credentials:** a 4-byte random token per session from `RandomNumberGenerator.Create()` (`Net/Session/Session.cs:43-51`). Endpoint is pinned to the session (`Session.EndPoint`, `:15`).

**Security posture (stated plainly):** there are no passwords, no TLS, no signatures. The token is carried in cleartext in a 24-byte fixed header on every datagram (`NetProtocol.cs:7-13`) and is a session-binding/anti-spoof measure, not an authentication barrier. Anything on the LAN can speak the protocol.

## Monitoring & Observability

**Error Tracking:**
- None. No Sentry, Application Insights, Crashlytics, or equivalent.

**Logs:**
- Server: custom injectable `ILogger` + `FileConsoleLogger` writing to `Console` and `Logs/server.log` with levels and tags, under a lock — `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Diagnostics/ServerLogger.cs:16-67`. Tags in use include `NET2`, `SERVER`, `CMD`, `UDP`, `CONFIG`, `DUMMY`.
- Client: BepInEx `Logger` (goes to `BepInEx\LogOutput.log`) plus structured replication telemetry with rate-limited/debounced visibility transitions and packet-rate counters — `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs` (layers map in `docs/bepinex-architecture.md:65-68`).
- Debug toggles: server `/coop` command family (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs`); client `Diagnostics.AnimVerbose` config + F8 anim-catalog dump (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:738-741`).

**Metrics:** None. RTT is surfaced to the user as `Ping 104`/`Pong 105` echo (`NetProtocol.cs:36-37`, client at `Networking/NetworkService.cs:37`) and rendered in the legacy HUD only.

## CI/CD & Deployment

**Hosting:**
- None. No cloud, no containers, no orchestration. The server binds `IPAddress.Any` on a user-chosen UDP port and runs on the player's own machine (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:56`).

**CI Pipeline:**
- None detected. No `.github/`, `.gitlab*`, `azure-pipelines.yml`, `Jenkinsfile`, or `.circleci` exists. Builds are manual (`README.md:59-65`, `docs/operations.md:22-25`).

**Release / deployment (manual, mandatory per repo rules in `AGENTS.md`):**
1. `dotnet build .\src\OriCoopPlus\OriCoopPlus.sln --configuration Release`
2. `dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release`
3. Copy `OriCoopBepInEx.dll` → `<ORI_DIR>\BepInEx\plugins\`
4. Copy `OriCoopDedicatedServer.exe`/`.dll`/`.deps.json`/`.runtimeconfig.json`/`.pdb` → `<ORI_DIR>\Server\`
5. Validate with `SmokeProbe`: `dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test all` (expects `SMOKE_OK`).

`AGENTS.md` additionally requires the built DLL/EXE to be replaced in the live game folder immediately after every successful build, closing `OriDE.exe` first.

**Rollout constraint:** client and server must always be deployed as a pair from the same build — the `0x4F43`/v2 envelope rejects anything else with `Reject 106`, and there is no fallback (`docs/protocol.md:30-33, 87-92`).

## Environment Configuration

**Required env vars:**
- `ORI_MANAGED_PATH` — **optional, build-time only**, consumed by `src/OriCoopPlus/OriCoopBepInEx/build.ps1:14` to locate `oriDE_Data\Managed`.
- No runtime environment variables are read by either module. All runtime settings come from local config files or console arguments.

**Build-time MSBuild property:**
- `$(OriGameDir)` — auto-detected from two known Steam paths with a hardcoded fallback (`src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:9-11`); drives every `<HintPath>` in that project.

**Secrets location:**
- Not applicable — there are no secrets. No `.env`, `credentials.*`, `*.pem`, `*.key`, or `id_rsa*` files exist in the repository (verified by scan; nothing was read or recorded).

## Webhooks & Callbacks

**Incoming:**
- None. No HTTP listener exists. The only "incoming unsolicited" traffic is the LAN discovery probe, which is a UDP broadcast of the same `Hello 100` handshake packet answered by a real server — implemented client-side in `src/OriCoopPlus/OriCoopBepInEx/UI/ServerConnectionDialog.cs:223-303` (`probe.EnableBroadcast = true; probe.Send(..., IPAddress.Broadcast, port)` at `:247,258`). It is a client-initiated scan, not a callback endpoint.

**Outgoing:**
- None. All outbound traffic is UDP to the explicitly configured server host/port (`src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:112-145`), defaulting to `127.0.0.1:7777`.

---

*Integration audit: 2026-10-09*
