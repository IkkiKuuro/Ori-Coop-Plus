---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
<!-- refreshed: 2026-10-09 -->

# Architecture

**Analysis Date:** 2026-10-09

## System Overview

```text
┌─────────────────────────────────────────────────────────────┐
│                   Ori DE (Unity 5.3.2f1, 32-bit)             │
│  OriDE.exe  →  BepInEx\plugins\OriCoopBepInEx.dll            │
├──────────────────┬──────────────────┬───────────────────────┤
│   Plugin/        │   Client/        │       UI/             │
│  OriCoopPlugin   │  RemotePlayer*   │  NativeUI + Dialog    │
│  (entry point)   │  + AnimRegistry  │  (pause menu)         │
├──────────────────┴──────────────────┴───────────────────────┤
│  Networking/NetworkService   │   Patches/ (HarmonyLib)      │
│  UDP client, 24B envelope    │   Sein/spiritflame/culling  │
└───────────────┬──────────────┴───────────────┬──────────────┘
                │  UDP 24B envelope (0x4F43 v2) │
                ▼                              ▼
┌─────────────────────────────────────────────────────────────┐
│            OriCoopDedicatedServer.exe (.NET 8.0)             │
│  Program.cs  →  ServerBoot (composition root)                │
│      └── NetServerHost (dispatch + relay + ACK)              │
│            ├── Transport/  (UdpTransport + EnvelopeCodec)    │
│            ├── Session/    (SessionManager + Session)        │
│            ├── Reliability/(AckTracker)                      │
│            ├── Game/       (GameHandlers, ConfigStore,       │
│            │                DummyBot, PlayerStateRelay)      │
│            ├── Game/Commands/ (CommandRegistry + OriCommands)│
│            └── Diagnostics/ (FileConsoleLogger)              │
└─────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| `OriCoopPlugin` | BepInEx entry point; owns config, network service, puppet manager, main-thread pump | `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` |
| `NetworkService` | Client UDP transport; encode/decode of the 24B envelope; retry/ping/handshake | `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` |
| `RemotePlayerManager` | Creates/updates/destroys remote puppets; routes player events | `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` |
| `RemotePuppetFactory` | Builds visual-only puppet subtrees cloned from Sein | `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs` |
| `RemotePlayerPuppet` | Interpolation, mirroring, name tag, fake spirit-flame projectile | `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` |
| `RemoteVisualController` | `LateUpdate` watchdog keeping puppets visible | `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs` |
| `AnimationRegistry` | Prewarmed animation catalog + name/hash/state resolution | `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` |
| `PlayerEventCore` | Static hub for character events (publish + local dispatch) | `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs` |
| `ReplicationObservability` | Packet/event counters and ring-buffer diagnostics | `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs` |
| `NetServerHost` | Server orchestration: receive → validate → dispatch/relay → send | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs` |
| `ServerBoot` | DI composition root; config load, lifecycle, stop | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs` |
| `SessionManager` | 3-way handshake, token/endpoint validation, ID allocator, sweeper | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs` |
| `GameHandlers` | Authoritative game rules, gating, teleport, ability history | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs` |
| `ConfigStore` | 8 gameplay bools, `serverconfig.json` persistence + change events | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs` |
| `DummyBot` | Server-side fake player (clientId 999) echoing real players | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs` |
| `CommandRegistry` / `OriCommands` | Console command parsing, aliases, dispatch | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/CommandRegistry.cs`, `.../OriCommands.cs` |
| `EnvelopeCodec` | Symmetric 24B header encode/decode with magic/version validation | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/EnvelopeCodec.cs` |
| `UdpTransport` | Async receive loop feeding a bounded `Channel`, direct sends | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs` |
| `AckTracker` | Reliable-send pendings, 250 ms retry, expiry | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/AckTracker.cs` |
| `SmokeProbe` | Headless protocol integration harness (`SMOKE_OK`/`SMOKE_FAIL`) | `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` |

## Pattern Overview

**Overall:** Peer-to-peer-ish **client/server with authoritative server** and a **relay** core. The client publishes local state, the server stamps identity, gates rules and re-emits original bytes, and every client renders decoupled visual "puppets".

**Key Characteristics:**
- **Server is authority:** session identity (IDs, tokens), config broadcast, teleport resolution, ability history, game-packet gating all live in `Net/`.
- **Zero protocol fallback:** client and server must be the same build; old envelopes are rejected with an explicit reason (`EnvelopeCodec.TryDecode`, `SessionManager.HandleHello` version check).
- **Relay, don't rebuild:** `NetServerHost` re-emits the original datagram bytes so `clientId`/`seq` of the sender are preserved; game-origin packets get a new `clientId 0` envelope.
- **Per-packet reliability classes:** critical packets (chat `-5`, `COLOR 6`, `SKILL 7`, `SYNC_*`, `TELEPORT_REQUEST 15`, `CONFIG_SYNC 16`, `DISCONNECT 4`) get ACK + retry; `PLAYER_STATE 18`, `PLAYER_EVENT 19`, ping are unreliable.
- **Visual-only remote entities:** puppets are cloned subtrees of Sein containing only animators, with gameplay `Behaviour`s disabled — no physics, no singleton mutation.
- **DI instead of global statics on the server:** `ServerBoot` implements `IServerContext` and `NetServerHost` implements `IGameTransport`; all collaborators are constructor-injected instances.

## Layers

**Client — Plugin (entry/bootstrap):**
- Purpose: BepInEx bootstrap, config binding, network wiring, Unity main-thread pump, camera/Sein repair
- Location: `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`
- Contains: `OriCoopPlugin : BaseUnityPlugin, IPlayerStateSink`
- Depends on: all client layers + Unity + `Game.*` (Assembly-CSharp)
- Used by: BepInEx runtime

**Client — Networking:**
- Purpose: UDP socket, envelope encode/decode, handshake, retry pump, event raising
- Location: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`
- Contains: `NetworkService : INetworkService`, private `PendingSend`
- Depends on: `Domain/`, `Events/`, shared `OriCoop`
- Used by: `Plugin`, `Events/PlayerEventCore`

**Client — Domain:**
- Purpose: Engine-free DTOs and transport contract
- Location: `src/OriCoopPlus/OriCoopBepInEx/Domain/`
- Contains: `PlayerSnapshot`, `Vector3Data`, `AnimationState`, `INetworkService`, `IPlayerStateSink`, `ConfigSyncHandler`
- Depends on: nothing (shared `OriCoop` only)
- Used by: every other client layer

**Client — Patches:**
- Purpose: HarmonyLib triggers that observe game methods and never contain network logic (except delegation)
- Location: `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- Contains: `SeinCharacterPatch`, `SpiritFlamePatch`, `FrustumCullingBypassPatch`, `AnimationPrewarmPatch`, `SeinInputPatch`, `PlayerStateReader`
- Depends on: `Plugin`, `Events`
- Used by: Harmony runtime

**Client — Client (remote entities):**
- Purpose: Puppet lifecycle, visual shielding, animation resolution
- Location: `src/OriCoopPlus/OriCoopBepInEx/Client/`
- Contains: `RemotePlayerManager`, `RemotePuppetFactory`, `RemotePlayerPuppet`, `RemoteVisualController`, `AnimationRegistry`, `FakeFlameMover`, `TransientEventCleanup`
- Depends on: `Domain`, `Events`, `Diagnostics`, `UI` (name tag), Unity + `Game.*`
- Used by: `Plugin`, `Events`

**Client — UI:**
- Purpose: Native pause-menu integration, connection dialog, HUD/name tags, toasts
- Location: `src/OriCoopPlus/OriCoopBepInEx/UI/`
- Contains: `OriCoopMenuScreen`, `ServerConnectionDialog`, `InventoryScreenPatch`, `NativeUIHelper`, `FloatingNameTag`, `AnimLogViewer`
- Depends on: `Plugin`, Unity + `Game.UI`
- Used by: `Plugin`, `Client`

**Client — Events:**
- Purpose: Character-event hub with a strict "never publish on receive" rule
- Location: `src/OriCoopPlus/OriCoopBepInEx/Events/`
- Contains: `PlayerEventCore`, `PlayerEventKind`, `SpiritFlameEventData`
- Depends on: `Domain`, `Diagnostics`, `Plugin`
- Used by: `Patches`, `Client`, `Plugin`

**Server — Transport:**
- Purpose: UDP I/O and wire format
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/`
- Contains: `UdpTransport`, `EnvelopeCodec`, `NetEnvelope`, `ReceivedDatagram`
- Depends on: `Diagnostics`, shared `OriCoop`
- Used by: `NetServerHost`

**Server — Session:**
- Purpose: Identity, handshake, endpoint pinning, timeout sweeping
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/`
- Contains: `SessionManager`, `Session`
- Depends on: `Diagnostics`, shared `OriCoop`
- Used by: `NetServerHost`, `GameHandlers`, `CommandRegistry`

**Server — Reliability:**
- Purpose: Reliable-send bookkeeping shared by the host
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/AckTracker.cs`
- Contains: `AckTracker`, `AckTracker.Pending`
- Depends on: shared `OriCoop`
- Used by: `NetServerHost`

**Server — Game:**
- Purpose: Gameplay authority and server-side simulation
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/`
- Contains: `ServerBoot`, `IServerContext`, `GameHandlers`, `IGameTransport`, `ConfigStore`, `DummyBot`, `PlayerStateRelay`
- Depends on: `Session`, `Reliability`, `Transport` (via `IGameTransport`), `Diagnostics`
- Used by: `Program`, `NetServerHost`

**Server — Game/Commands:**
- Purpose: Console command surface
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/`
- Contains: `ConsoleCommand`, `CommandRegistry`, `ISessionTarget`, `OriCommands` (7 nested command classes)
- Depends on: `Game`, `Session`, `Diagnostics`
- Used by: `Program`, chat help path in `NetServerHost`

**Server — Diagnostics:**
- Purpose: Structured logging to console and file
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Diagnostics/ServerLogger.cs`
- Contains: `ILogger`, `ServerLogLevel`, `FileConsoleLogger`
- Depends on: nothing
- Used by: all server layers

## Data Flow

### Primary Request Path — local player state outbound

1. `SeinCharacterPatch.Postfix` runs after `SeinCharacter.FixedUpdate`, filtered to `__instance == Game.Characters.Sein` (`src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs:10`)
2. `PlayerStateReader.Read(__instance)` builds a `PlayerSnapshot` (`src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs`)
3. `OriCoopPlugin.Publish(snapshot)` stamps id/nick and calls `_network.SendPlayerSnapshot` (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:299`)
4. `NetworkService` stores it as `_queuedSnapshot` and drains it on the **network thread** via `FlushSnapshot()` (`src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:527`)
5. `BuildEnvelope` writes the 24-byte header (magic `0x4F43`, version 2, `PacketType.PLAYER_STATE 18`) and `SendRaw` writes to the UDP socket

### Remote state inbound

1. `NetworkService.ReceiveLoop` reads a datagram and calls `ReadServerPacket` (`src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:500`)
2. Header validated (magic + version), `SysAck` sent if `FlagReliable`, piggybacked `AckSeq` completed (`NetworkService.cs:775`)
3. `PLAYER_STATE 18` payload parsed; drop-old wrap-safe filtering per sender via `IsNewerThanLast` (`NetworkService.cs:1057`), then `RaiseSnapshot`
4. `OriCoopPlugin.OnPlayerSnapshotReceived` stores the snapshot and **enqueues** a main-thread action (`Plugin/OriCoopPlugin.cs:310`)
5. `OriCoopPlugin.Update` drains `_mainThreadActions` on the Unity main thread (`Plugin/OriCoopPlugin.cs:683`) → `RemotePlayerManager.HandleSnapshot`
6. `RemotePuppetFactory.CreatePuppet` lazily creates the puppet; `ApplySnapshotDirect` interpolates transform and animation state (`Client/RemotePlayerManager.cs:34`)

### Character event (Spirit Flame pilot)

1. `SpiritFlamePatch.Postfix` on `SeinSpiritFlameAbility.ThrowSpiritFlames`, owner resolved from `___m_sein` with reflection fallback (`Patches/SpiritFlamePatch.cs:22`)
2. `PlayerEventCore.PublishSpiritFlame(direction, origin, timestamp)` → `INetworkService.SendPlayerEvent` (`Events/PlayerEventCore.cs:38`)
3. `SendEventSystem` uses a **separate sequence domain** (`_eventSendSeq`) and unreliable flags (`Networking/NetworkService.cs:593`)
4. Server: `GameHandlers.DispatchAsync` → `EventRelayGate.ShouldRelay` (own drop-old gate, disjoint from snapshot `LastRecvSeq`) → `IGameTransport.RelayUnreliableAsync` re-emits the original bytes (`Net/Game/GameHandlers.cs:88`)
5. Client receive: `ReadServerPacket` → `RaisePlayerEvent` → `OriCoopPlugin.OnPlayerEventReceived` enqueues main-thread → `RemotePlayerManager.HandlePlayerEvent` (lazy puppet) → `PlayerEventCore.DispatchLocal` → `HandleSpiritFlame` → `puppet.PlaySpiritFlameVisual` (`Client/RemotePlayerManager.cs:98`)
6. **No echo:** the receive path never calls `Publish`, by design (D-07)

### Server receive/dispatch path

1. `UdpTransport.ReceiveLoopAsync` awaits `ReceiveAsync(ct)` and `TryWrite`s into a bounded `Channel` (1024, `DropOldest`) (`Net/Transport/UdpTransport.cs:83`)
2. `NetServerHost.RunAsync` reads from the channel and calls `DispatchAsync` (`Net/NetServerHost.cs:75`)
3. `EnvelopeCodec.TryDecode` validates length/magic/version; failure logs and sends `MsgReject` (`Net/NetServerHost.cs:116`)
4. Reliable datagrams get an immediate `SysAck 103`; piggybacked `AckSeq` completes pendings (`Net/NetServerHost.cs:125`)
5. `switch` on `PacketId`: `MsgHello`/`MsgConfirm`/`MsgPing`/`MsgAck` handled locally; everything else routes through `HandleGamePacketAsync`
6. `SessionManager.ValidatePacket` checks id existence, token match and exact endpoint; `_sessions.Touch` refreshes liveness (`Net/Session/SessionManager.cs:61`)
7. Critical packets relay original bytes with `AckTracker` pending; `PLAYER_STATE` relays unreliable; game packets delegate to `GameHandlers.DispatchAsync` (`Net/NetServerHost.cs:222`)

### Config change broadcast

1. `CommandRegistry.ExecuteLine` mutates `ConfigStore`, which persists to `serverconfig.json` and raises `Changed` (`Net/Game/ConfigStore.cs`)
2. `ServerBoot.OnConfigChanged` fires a reliable `CONFIG_SYNC 16` broadcast with `BuildConfigPayload()` (`Net/Game/ServerBoot.cs:173`)
3. Client `NetworkService` raises `ConfigSyncReceived` (8 bools in canonical order) → `OriCoopPlugin.OnConfigSyncReceived` logs and toasts (`Plugin/OriCoopPlugin.cs:416`)

**State Management:**
- **Server:** no global mutable state. `Session` instances hold per-player state in a `ConcurrentDictionary`; `GameHandlers` holds `_lastKnown`, `_unlockedAbilities` and the event gate behind locks.
- **Client:** `OriCoopPlugin.Instance` static is the single accessor; Unity-side state lives in `MonoBehaviour`s (`RemotePlayerPuppet`, `RemoteVisualController`, `FloatingNameTag`). Static caches exist for animation catalog and diagnostics (`AnimationRegistry`, `ReplicationObservability`).
- **Crossing threads:** network callbacks never touch Unity APIs directly; they enqueue into `OriCoopPlugin._mainThreadActions`, drained in `Update`.

## Key Abstractions

**Envelope (24-byte header):**
- Purpose: single wire contract shared by both modules
- Examples: `src/OriCoopPlus/OriCoopShared/NetProtocol.cs`, `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/EnvelopeCodec.cs`, `BuildEnvelope` in `Networking/NetworkService.cs`
- Pattern: fixed offsets, little-endian, magic `0x4F43` + version `2`, `flags` (Reliable `0x01`, AckPresent `0x02`), per-sender wrap-safe `seq`

**Session:**
- Purpose: authenticated player identity bound to a fixed endpoint
- Examples: `Net/Session/Session.cs`, `Net/Session/SessionManager.cs`
- Pattern: 3-way handshake (`Hello` 100 → `Welcome` 101 → `Confirm` 102), random per-session token, `IsReady` flag, time-based sweeper

**Reliability:**
- Purpose: at-least-once delivery for critical packets without congestion control
- Examples: `Net/Reliability/AckTracker.cs`, `PendingSend` in `Networking/NetworkService.cs`, `PumpRetries`
- Pattern: track by (endpoint, seq), retry every 250 ms up to 3 attempts, then expire with a log and keep the session

**Puppet (visual entity):**
- Purpose: represent a remote player without running gameplay code
- Examples: `Client/RemotePuppetFactory.cs`, `Client/RemotePlayerPuppet.cs`, `Client/RemoteVisualController.cs`
- Pattern: clone only the subtree containing `SpriteAnimatorWithTransitions`, disable non-visual `Behaviour`s, `LateUpdate` watchdog

**Game transport facade:**
- Purpose: lets the Game layer send without knowing the socket
- Examples: `IGameTransport` declared in `Net/Game/GameHandlers.cs:19`, implemented explicitly by `NetServerHost`
- Pattern: explicit interface implementation (`async Task IGameTransport.UnicastReliableAsync(...)`)

**Server context facade:**
- Purpose: gives commands access to sessions/config/dummy/stop without statics
- Examples: `IServerContext` in `Net/Game/ServerBoot.cs:15`, implemented by `ServerBoot`
- Pattern: read-only properties over injected instances

**Player event:**
- Purpose: typed, unreliable, sequenced character actions beyond position
- Examples: `Events/PlayerEventCore.cs`, `Events/SpiritFlameEventData.cs`, `src/OriCoopPlus/OriCoopShared/PlayerEventProtocol.cs`
- Pattern: handler dictionary keyed by `byte kind`; body is a frozen 37-byte layout with sender identity only in the envelope header

## Entry Points

**BepInEx plugin:**
- Location: `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` (`Awake`)
- Triggers: BepInEx chainloader after `Assembly-CSharp.dll` load (mandatory `BepInEx.cfg` entrypoint, see `docs/bepinex-architecture.md`)
- Responsibilities: bind config, start network, `Harmony.PatchAll`, add `AnimLogViewer`, drain main-thread queue each `Update`, repair camera/Sein, legacy HUD `OnGUI`

**Dedicated server:**
- Location: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` (`Main`)
- Triggers: `OriCoopDedicatedServer.exe [--auto] [--max-players N] [--port N]` or `start_server.bat`
- Responsibilities: parse args (or interactive prompt), build `ServerBoot` + `CommandRegistry`, run host, drive console loop, graceful stop via `CancellationToken`

**Protocol smoke test:**
- Location: `src/OriCoopDedicatedServer/SmokeProbe/Program.cs`
- Triggers: `SmokeProbe --test all|handshake|relay|event|ping`
- Responsibilities: spawn/connect to a server, run scripted scenarios, exit `SMOKE_OK` (0) or `SMOKE_FAIL: reason` (1)

**Harmony patches:**
- Locations: `Patches/SeinCharacterPatch.cs`, `Patches/SpiritFlamePatch.cs`, `Patches/FrustumCullingBypassPatch.cs`, `Patches/AnimationPrewarmPatch.cs`, `Patches/SeinInputPatch.cs`, `UI/InventoryScreenPatch.cs`
- Triggers: target game methods (`SeinCharacter.FixedUpdate`, `SeinSpiritFlameAbility.ThrowSpiritFlames`, `CameraFrustumOptimizer.ProcessFrustumOptimizable`, `CharacterAnimationSystem.Start`, `InventoryManager.Awake`)
- Responsibilities: observe and delegate; no networking logic inside patches

## Architectural Constraints

- **Threading:** client is split between the Unity main thread (all `Transform`/`GameObject`/`Game.Characters` access) and one network thread (`NetworkService.ReceiveLoop` with a 20 ms `ReceiveTimeout`). Communication is strictly via the `_mainThreadActions` queue. Server uses async/await with a single receive loop plus `System.Threading.Timer` ticks (retry 50 ms, sweep 1000 ms) and the console thread for commands.
- **Global state:** server deliberately avoids mutable statics. Client uses several statics for convenience: `OriCoopPlugin.Instance`, `OriCoopPlugin.EnqueueMainThread`, `PlayerEventCore.s_transport`/`s_handlers`, `AnimationRegistry` caches, `ReplicationObservability` counters, `OriCoopMenuScreen.Instance`, `ServerConnectionDialog.Instance`.
- **Circular imports:** none at project level. `Client/RemotePlayerPuppet` uses `UI/FloatingNameTag` while `UI/NativeUIHelper` calls back into `Plugin`; `Patches` → `Plugin` → `Client`/`Networking` is one-directional. `Events/PlayerEventCore` → `Plugin` (for logging) and `Plugin` → `Events` (bind/dispatch) form a soft cycle resolved by static calls, not project references.
- **Same-build coupling:** `NetProtocol`, `PacketType`, `PlayerEventProtocol` and `AnimationSyncData` are compiled into *both* assemblies via `<Compile Include="..\OriCoopShared\*.cs">`. Any wire change must rebuild and retest both; there is no version negotiation fallback.
- **Unity 5.3 / .NET 3.5 limits:** client targets `net35` with `LangVersion 7.3`; shared code must stay C# 5-compatible (consts only, named delegates instead of multi-arg `Action`). Custom delegates `ConfigSyncHandler` and `PlayerEventHandler` exist because mscorlib does not guarantee high-arity `Action` overloads.
- **Game-dir coupling:** the client project resolves `OriGameDir` at build time with fallbacks to `C:\Program Files (x86)\Steam\steamapps\common\Ori DE` and `D:\SteamLibrary\steamapps\common\Ori DE`.

## Anti-Patterns

### Sending snapshots from `FixedUpdate`

**What happens:** calling `NetworkService.Send*` directly inside a Harmony patch on `FixedUpdate` would push 50 sends/sec of blocking work onto the game thread.
**Why it's wrong:** the game thread would stall on the socket; `NetworkService` explicitly documents "nenhum envio ocorre em FixedUpdate (D-07)".
**Do this instead:** enqueue into `_queuedSnapshot` and let the network thread flush it — see `NetworkService.SendPlayerSnapshot` + `FlushSnapshot` in `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:163`.

### Rebuilding relayed packets

**What happens:** decoding a critical packet and re-encoding it with a new header before relaying.
**Why it's wrong:** it destroys the sender's `clientId`/`seq`, breaking drop-old sequencing and piggybacked ACKs on the receiving side.
**Do this instead:** relay the original datagram bytes — `NetServerHost.RelayReliableAsync` and `IGameTransport.RelayUnreliableAsync` in `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:574`.

### Cloning the whole Sein for a remote player

**What happens:** instantiating the complete `SeinCharacter` root to render a partner.
**Why it's wrong:** runs dozens of native gameplay `Awake`s and mutates the `Game.Characters.Sein`/`Current` singletons, causing NRE spirals and FPS collapse.
**Do this instead:** clone only the animator subtree and strip non-visual components — `RemotePuppetFactory.CreatePuppet` in `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:12`.

### Adding global statics on the server

**What happens:** a `static class GameState` or static config cache shared across instances.
**Why it's wrong:** it breaks the injectable design (D-13), makes `ServerBoot` untestable and reintroduces the old Core's coupling.
**Do this instead:** pass collaborators through `IServerContext`/`IGameTransport` as `ServerBoot` does in `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs:38`.

### Reusing removed packet IDs

**What happens:** assigning a new meaning to IDs 1, 2, 3, 5 or negative legacy IDs.
**Why it's wrong:** they are permanently retired; reuse would make old clients send packets the new core interprets differently.
**Do this instead:** append a new positive ID and update both sides of `src/OriCoopPlus/OriCoopShared/PacketType.cs` in the same build.

## Error Handling

**Strategy:** Defensive, fail-soft and log-heavy. Every layer degrades to "drop and log" instead of throwing into game code.

**Patterns:**
- **Try/Validate-then-act:** `EnvelopeCodec.TryDecode` checks length, magic and version before reading any field and returns a human-readable reject reason reused for `MsgReject` (`Net/Transport/EnvelopeCodec.cs:69`).
- **Read-with-length-checks:** `GameHandlers` validates remaining bytes before every field read and logs the discard (D-03/T-02-08).
- **Empty catch for engine probes:** `FindLocalSein`, `EnsureCameraFollowsLocalPlayer` and `ResolveOwner` wrap reflection/singleton access in `catch { }` because Unity destroyed objects throw on member access.
- **Fire-and-forget with observation:** timer ticks (`OnRetryTick`, `OnSweepTick`) and the config broadcast use `ContinueWith(..., OnlyOnFaulted)` so failures are logged without blocking the timer thread.
- **Fail-closed on unknown events:** `PlayerEventCore.DispatchLocal` keeps the last valid animation and counts the drop when `kind` is unknown (`Events/PlayerEventCore.cs:89`).
- **Exception isolation on the main thread:** `OriCoopPlugin.Update` catches per-action so one bad callback cannot kill the pump (`Plugin/OriCoopPlugin.cs:688`).

## Cross-Cutting Concerns

**Logging:**
- Client: BepInEx `Logger.LogInfo/LogWarning/LogError` through `OriCoopPlugin.Log*` statics, plus `ReplicationObservability.Record` ring buffer and rate-limited counters. Debug `[ANIM]`/`[EVENT]` lines are gated by the `Diagnostics.AnimVerbose` config entry.
- Server: `ILogger`/`FileConsoleLogger` writes leveled lines to the console and appends to `Logs/server.log` next to the working directory, created eagerly in `ServerBoot.DefaultLogPath()`. Tags (`NET2`, `SESSAO`, `ACK`, `CHAT`, `CONFIG`, `CMD`, `UDP`) identify the subsystem.

**Validation:**
- Wire: magic + version + minimum length in `EnvelopeCodec.TryDecode`; the client mirrors the check in `ReadServerPacket`.
- Session: `SessionManager.ValidatePacket` enforces existence, token equality and exact endpoint; a NAT-rebinding client must re-do `Hello`.
- Gameplay: `GameHandlers` length-checks every body; `ConfigStore` never zeroes flags at boot.

**Authentication:**
- Token-based session auth generated by `Session.NewToken()` via `RandomNumberGenerator`; the token must appear in every post-handshake header. ID `-1` with token `0` is legal only for `MsgHello`. No passwords, no encryption — the transport is plain UDP on the LAN/Internet.

---

*Architecture analysis: 2026-10-09*
