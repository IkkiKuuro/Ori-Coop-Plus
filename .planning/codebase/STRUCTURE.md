---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# Codebase Structure

**Analysis Date:** 2026-10-09

## Directory Layout

```
Ori-Coop-Plus/
├── API/                          # binary dependencies used at build time (Client/Server)
├── CHANGELOGS/                   # per-game changelog notes (DD.txt, HK.txt)
├── ICONS/                        # launcher/game icons (png)
├── Modules/                      # prebuilt WW Launcher game modules (legacy binaries)
│   ├── COTLModules/ DDModules/ FARLSModules/ HKModules/
│   ├── ORIDEModules/ ORIWOTWModules/ WWModules/
│   └── ORIDEModules/BACKUP_ORIGINAL/   # untouched copies of the Ori DE modules
├── docs/                         # maintained technical documentation (AGENTS.md rule)
├── scripts/                      # reverse-engineering + tooling PowerShell scripts
├── src/                          # all first-party source
│   ├── OriCoopPlus/
│   │   ├── OriCoopPlus.sln       # VS solution (client + server projects)
│   │   ├── OriCoopBepInEx/       # BepInEx 5.x client plugin (net35)
│   │   │   ├── Plugin/ Client/ Diagnostics/ Domain/
│   │   │   ├── Events/ Networking/ Patches/ UI/
│   │   │   ├── build.ps1         # csc.exe build without Visual Studio
│   │   │   └── OriCoopBepInEx.csproj
│   │   └── OriCoopShared/        # wire contract source, linked into both projects
│   └── OriCoopDedicatedServer/
│       ├── OriCoopDedicatedServer/   # .NET 8.0 server exe
│       │   ├── Program.cs
│       │   ├── start_server.bat
│       │   └── Net/
│       │       ├── Transport/ Reliability/ Session/
│       │       ├── Game/ (+ Commands/) Diagnostics/
│       │   └── SmokeProbe/           # headless protocol harness
├── .planning/                    # GSD phase plans/summaries
│   └── codebase/                 # ← this directory
├── AGENTS.md                     # repo maintenance + deployment rules
├── README.md                     # project overview and build instructions
└── AllGames.txt / AllMods.txt / WWGames.txt / apiVersion.txt
```

## Directory Purposes

**`src/OriCoopPlus/OriCoopBepInEx/Plugin/`:**
- Purpose: BepInEx entry point and Unity-side orchestration
- Contains: `OriCoopPlugin.cs` (`BaseUnityPlugin`, `IPlayerStateSink`)
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Domain/`:**
- Purpose: engine-free DTOs and the client transport contract
- Contains: `PlayerState.cs`, `INetworkService.cs`, `IPlayerStateSink.cs`
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Networking/`:**
- Purpose: UDP transport, envelope codec (client side), retry and ping machinery
- Contains: `NetworkService.cs` (single ~1100-line file, private `PendingSend`)
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Patches/`:**
- Purpose: HarmonyLib hooks — observation only, then delegation
- Contains: `SeinCharacterPatch`, `SpiritFlamePatch`, `FrustumCullingBypassPatch`, `AnimationPrewarmPatch`, `SeinInputPatch`, `PlayerStateReader`
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Client/`:**
- Purpose: remote-player visual entities and animation
- Contains: manager, factory, puppet, visual controller, animation registry
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs`, `RemotePuppetFactory.cs`, `RemotePlayerPuppet.cs`, `RemoteVisualController.cs`, `AnimationRegistry.cs`

**`src/OriCoopPlus/OriCoopBepInEx/UI/`:**
- Purpose: native pause-menu integration, connection dialog, HUD and name tags
- Contains: `OriCoopMenuScreen`, `ServerConnectionDialog`, `InventoryScreenPatch`, `NativeUIHelper`, `FloatingNameTag`, `AnimLogViewer`
- Key files: `src/OriCoopPlus/OriCoopBepInEx/UI/OriCoopMenuScreen.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Events/`:**
- Purpose: typed character-event hub (publish + local dispatch, no echo)
- Contains: `PlayerEventCore.cs`, `PlayerEventKind.cs`, `SpiritFlameEventData.cs`
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs`

**`src/OriCoopPlus/OriCoopBepInEx/Diagnostics/`:**
- Purpose: rate-limited observability counters and ring buffer
- Contains: `ReplicationObservability.cs`
- Key files: `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs`

**`src/OriCoopPlus/OriCoopShared/`:**
- Purpose: single source of truth for the wire contract, compiled into both modules
- Contains: `NetProtocol.cs`, `PacketType.cs`, `PlayerEventProtocol.cs`, `AnimationSyncData.cs`, `CoopConfig.cs`
- Key files: `src/OriCoopPlus/OriCoopShared/NetProtocol.cs`

**`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/`:**
- Purpose: UDP I/O and 24-byte envelope codec
- Contains: `UdpTransport.cs`, `EnvelopeCodec.cs`
- Key files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs`

**`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/`:**
- Purpose: player identity, handshake, endpoint pinning, timeout sweep
- Contains: `SessionManager.cs`, `Session.cs`
- Key files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs`

**`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/`:**
- Purpose: ACK tracking and retry scheduling for critical packets
- Contains: `AckTracker.cs`
- Key files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/AckTracker.cs`

**`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/`:**
- Purpose: composition root, authoritative rules, config, dummy player, relay gate
- Contains: `ServerBoot.cs`, `GameHandlers.cs`, `ConfigStore.cs`, `DummyBot.cs`, `PlayerStateRelay.cs`
- Key files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs`

**`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/`:**
- Purpose: console command surface and aliases
- Contains: `CommandRegistry.cs`, `ConsoleCommand.cs`, `OriCommands.cs`
- Key files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs`

**`src/OriCoopDedicatedServer/SmokeProbe/`:**
- Purpose: headless BCL-only protocol integration harness
- Contains: `Program.cs` (spawns a server, runs scripting scenarios)
- Key files: `src/OriCoopDedicatedServer/SmokeProbe/Program.cs`

**`docs/`:**
- Purpose: maintained architecture, protocol and operations documentation
- Contains: 13 markdown files, index in `docs/README.md`
- Key files: `docs/architecture.md`, `docs/protocol.md`, `docs/bepinex-architecture.md`, `docs/operations.md`, `docs/code-map.md`

**`scripts/`:**
- Purpose: one-off PowerShell reverse-engineering tools against the game assemblies
- Contains: Cecil-based inspectors, dumpers, string extractors, test harnesses; `scripts/README.md`
- Key files: `scripts/build.rsp`, `scripts/README.md`

**`API/`:**
- Purpose: build-time binary references that ship with the repo
- Contains: `Client/BepInEx.dll`, `Client/0Harmony.dll`, `Server/WWDedicatedServer.*`, `Client/BepInEx.zip`
- Key files: `API/Client/BepInEx.dll`

## Key File Locations

**Entry Points:**
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` — client plugin `Awake`, main-thread pump
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` — server `Main`, arg parsing, console loop
- `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` — protocol smoke harness
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/start_server.bat` — packaged launch wrapper

**Configuration:**
- `src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj` — net35 target, `OriGameDir` fallbacks, DLL references, shared-source link
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj` — net8.0, nullable, shared-source link
- `src/OriCoopPlus/OriCoopPlus.sln` — solution joining client + server
- `serverconfig.json` (runtime, generated next to the server exe) — 8 gameplay bools persisted by `ConfigStore`

**Core Logic:**
- `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` — envelope layout and protocol constants
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — packet IDs (with retired IDs documented)
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — client transport
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs` — server dispatch/relay
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs` — game rules + `IGameTransport`

**Testing:**
- `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` — the only automated harness (protocol-level, exit-code based)

## Naming Conventions

**Files:**
- One public type per file, file name equals the type name: `SessionManager.cs`, `AckTracker.cs`, `SpiritFlamePatch.cs`
- Client namespaces mirror folders: `OriCoopBepInEx.Client`, `OriCoopBepInEx.Networking`, `OriCoopBepInEx.Patches`, `OriCoopBepInEx.UI`, `OriCoopBepInEx.Events`, `OriCoopBepInEx.Diagnostics`, `OriCoopBepInEx.Domain`, `OriCoopBepInEx.Plugin`
- Server namespaces mirror folders: `OriCoopDedicatedServer.Net.Session`, `...Net.Transport`, `...Net.Reliability`, `...Net.Game`, `...Net.Game.Commands`, `...Net.Diagnostics`
- Shared contract lives in flat namespace `OriCoop` regardless of file

**Directories:**
- PascalCase, layered by responsibility; `Patches/` groups HarmonyLib classes; `Commands/` is nested under `Game/` on the server only
- `Net/` is the server-side root for everything network-related; there is no equivalent wrapper on the client (folders sit directly under the project root)

**Types and members:**
- Interfaces prefixed `I`: `INetworkService`, `IGameTransport`, `IServerContext`, `ILogger`, `IPlayerStateSink`, `ISessionTarget`
- Private backing fields `_camelCase`; statics on the client use the `s_` prefix (`s_transport`, `s_animHashCache`, `s_ring`)
- Booleans as `Is*`/`Allow*`/`Share*`/`Show*` (`IsConnected`, `IsReady`, `AllowTeleport`)
- `Try*` prefix for validating parsers returning `bool` with `out` params (`TryDecode`, `TryParseHello`, `TryGet`, `TryParseChatText`)

**Packets/IDs:**
- Enum members are UPPER_SNAKE (`PLAYER_STATE`, `SYNC_DOOR`, `TELEPORT_REQUEST`)
- System messages are numeric consts on `NetProtocol` (`MsgHello 100` … `MsgReject 106`)
- Retired IDs stay in `PacketType` with `LEGACY_REMOVED` comments — never delete or reuse

## Where to Add New Code

**New Feature (gameplay capability, e.g. a new synced ability):**
- Primary code: add a `PacketType` member in `src/OriCoopPlus/OriCoopShared/PacketType.cs`, a handler branch in `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs`, a send/parse pair in `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`, and an event on `Domain/INetworkService.cs`
- Client reaction: extend `Plugin/OriCoopPlugin.cs` handlers and/or `Client/RemotePlayerManager.cs`
- Config gating: extend the 8 bools in `Net/Game/ConfigStore.cs` (order matters — it is the wire order)
- Tests: add a scenario to `src/OriCoopDedicatedServer/SmokeProbe/Program.cs`
- Docs: update `docs/protocol.md` and `docs/code-map.md` (mandatory per `AGENTS.md`)

**New Harmony patch / game observation:**
- Implementation: a new `internal static class` with `[HarmonyPatch]` in `src/OriCoopPlus/OriCoopBepInEx/Patches/`. Keep it observation-only — delegate to `PlayerEventCore` or `OriCoopPlugin`.

**New character event:**
- Add a `PlayerEventKind` member in `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventKind.cs`, publish from `PlayerEventCore`, register the handler in `RemotePlayerManager`'s constructor.

**New UI surface:**
- Implementation: `src/OriCoopPlus/OriCoopBepInEx/UI/`, cloning native `MenuScreen`/`CleverMenuItem` patterns from `OriCoopMenuScreen.cs` / `NativeUIHelper.cs`.

**New server command:**
- Implementation: a nested `private sealed class XCommand : ConsoleCommand` in `Net/Game/Commands/OriCommands.cs`, registered by `OriCommands.RegisterAll`. Optionally implement `ISessionTarget` to answer in the requester's chat.

**Utilities / shared helpers:**
- Pure wire helpers: `src/OriCoopPlus/OriCoopShared/` (must stay C# 5-compatible — consts, structs, no new BCL APIs)
- Client-only engine-free helpers: `src/OriCoopPlus/OriCoopBepInEx/Domain/`
- Server-only helpers: a new file in the matching `Net/` subfolder

## Special Directories

**`bin/`, `obj/`:**
- Purpose: build output
- Generated: yes
- Committed: no (`.gitignore` `[Bb]in/`, `[Oo]bj/`)

**`Logs/`:**
- Purpose: `FileConsoleLogger` output at runtime (`server.log`)
- Generated: yes, at server start via `ServerBoot.DefaultLogPath()`
- Committed: no (`.gitignore`)

**`Modules/`:**
- Purpose: prebuilt WW Launcher modules for other games (legacy distribution binaries)
- Generated: no
- Committed: yes; `ORIDEModules/BACKUP_ORIGINAL/` holds untouched reference copies

**`API/`:**
- Purpose: build references shipped in-repo (BepInEx, Harmony, WW server binaries) plus `BepInEx.zip`
- Generated: no
- Committed: yes

**`.planning/`:**
- Purpose: GSD planning artifacts (`phases/`, `codebase/`)
- Generated: partially
- Committed: yes

---

*Structure analysis: 2026-10-09*
