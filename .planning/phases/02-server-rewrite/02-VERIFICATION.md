---
phase: 02-server-rewrite
verified: 2026-10-05T00:00:00Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 02: Server Rewrite Verification Report

**Phase Goal:** Stable modern UDP connection, old core abandoned, new core compatible with current context.
**Verified:** 2026-10-05 (independent re-verification; server Release build + full SmokeProbe re-run by verifier)
**Status:** passed

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Old core abandoned — new core is the only path, Core out of build | ✓ VERIFIED | `OriCoopDedicatedServer.csproj` has no `ProjectReference` to Core; `<Compile Remove="Game/**/*.cs" />`; Release output contains only `OriCoopDedicatedServer.dll` (no `Core.dll`); `Program.cs` runs `ServerBoot` by default, `--net2` accepted as documented no-op |
| 2 | Envelope versioned (magic + version, old builds refused) | ✓ VERIFIED | `OriCoopShared/NetProtocol.cs`: `Magic = 0x4F43`, `Version = 2`, 24-byte header offsets; `EnvelopeCodec.TryDecode` validates length + magic + version before any read and returns Reject reason; probe `invalid-magic` PASS |
| 3 | Sessions dynamic (incremental IDs, 3-way handshake, endpoint+token, timeout, server-full) | ✓ VERIFIED | `SessionManager.cs`: `ConcurrentDictionary`, `Interlocked` allocator skipping 0/999, `HandleHello/HandleConfirm`, `ValidatePacket` (token+endpoint), `SweepExpired`; probe `handshake`, `token`, `timeout`, `server-full` PASS |
| 4 | Reliability split (PLAYER_STATE unreliable drop-old; criticals ACK+retry) | ✓ VERIFIED | `AckTracker.cs` (250 ms x3, per-destination pending, original-bytes resend); `PlayerStateRelay.ShouldRelay` wrap-safe drop-old; `IsCriticalPacket` list; SysAck 103 immediate; probe `relay`, `ping`, `reliable` PASS |
| 5 | Game ported (handlers, 8-bool config persisted, dummy 999, ServerBoot, commands) | ✓ VERIFIED | `GameHandlers.cs` (teleport/sync/skill/color/dummy/config dispatch, defensive length-checks), `ConfigStore.cs` (8 bools, `serverconfig.json` load/save, never zeroes), `DummyBot.cs` (ID 999, header identity), `ServerBoot.cs` (wires all, `Logs/server.log`, Changed→broadcast), `Commands/{CommandRegistry,OriCommands,ConsoleCommand}.cs` with zero Core dependency; probe `game-fase1`, `game-fase2` PASS incl. `PERSIST_OK` lineage |
| 6 | Client mirrors new framing in same build (C#5 constraints) | ✓ VERIFIED | `NetworkService.cs`: envelope `0x4F43`/v2 encode+decode, `Hello`/`Confirm`, `_pending` ACK+retry, `_lastRelaySeq` drop-old cleared on Welcome/Reject, `_queuedSnapshot` + `FlushSnapshot` on net thread (no send in FixedUpdate), tolerant bool read; `PacketType.cs` D-15 redesign (dead IDs removed, never reused) |
| 7 | Docs updated and consistent | ✓ VERIFIED | `docs/protocol.md` (0x4F43/v2, 8-bool CONFIG_SYNC), `docs/architecture.md` (Net/ layers, serverconfig.json, Logs/server.log), `docs/operations.md` (SMOKE_OK battery log, --net2 no-op, deploy paths), `docs/code-map.md`; no new pages added/removed so README index untouched |
| 8 | Binaries deploy to ORI_DIR, no stubs left behind | ✓ VERIFIED | SUMMARies record plugin + server DLL deploys with closed-process + boot validation (AGENTS.md rule); grep `TODO\|FIXME\|XXX\|placeholder\|coming soon` empty in `Net/` and `NetworkService.cs`; verifier rebuilt server Release 0 warnings/0 errors and re-ran full probe `SMOKE_OK` (15 PASS) against the fresh build |

**Score:** 8/8 truths verified (0 present-but-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `OriCoopShared/NetProtocol.cs` | Shared envelope constants (C#5-compatible) | ✓ VERIFIED | Magic/Version/header offsets + Msg 100-106 + timing constants, substantive + referenced by server, probe, client |
| `Net/Transport/EnvelopeCodec.cs` | Encode/decode with pre-read validation | ✓ VERIFIED | TryEncode/TryDecode, length+magic+version first, reject reasons |
| `Net/Transport/UdpTransport.cs` | Modern async UDP + Channel | ✓ VERIFIED | Present; exercised by every probe run (receive loop + send) |
| `Net/Session/Session.cs` + `SessionManager.cs` | Dynamic sessions, handshake, sweeper | ✓ VERIFIED | Read in full; allocator/handshake/validate/sweep all substantive |
| `Net/Reliability/AckTracker.cs` | Per-destination ACK+retry 250ms x3 | ✓ VERIFIED | Read in full; Track/Complete/CollectDue/PurgeFor wired to host timers |
| `Net/NetServerHost.cs` | Dispatch orchestrator, no statics | ✓ VERIFIED | Read in full (867 lines); Game-first dispatch, chat rules, disconnect broadcast |
| `Net/Game/{GameHandlers,ConfigStore,DummyBot,ServerBoot,PlayerStateRelay}.cs` | Ported game layer, instances | ✓ VERIFIED | All read; DI instances, no static mutable globals |
| `Net/Game/Commands/{CommandRegistry,OriCommands,ConsoleCommand}.cs` | Console commands on new core | ✓ VERIFIED | `ConsoleCommand` ported off Core types; help mirrors registry names |
| `Net/Diagnostics/ServerLogger.cs` | Leveled file+console logger | ✓ VERIFIED | Present; `Logs/server.log` written on every run |
| `SmokeProbe/Program.cs` | 6-mode automated battery | ✓ VERIFIED | `all` = 15 PASS re-run by verifier, exit 0 |
| `BepInEx/Networking/NetworkService.cs` | Client new framing | ✓ VERIFIED | Envelope, retry, drop-old, net-thread snapshot flush |
| `OriCoopShared/PacketType.cs` | D-15 contract redesign | ✓ VERIFIED | Dead IDs removed/documented, CONFIG_SYNC 16 + 8 bools |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Program.cs | ServerBoot → NetServerHost | `new ServerBoot(port, maxPlayers)` + `StartAsync` | WIRED | Only path; no Core branch remains |
| NetServerHost | SessionManager | Hello/Confirm/ValidatePacket/SweepExpired | WIRED | All session ops delegate to manager |
| NetServerHost | AckTracker | Track on reliable send, Complete on Ack, CollectDue on 50ms timer | WIRED | Retry pump + give-up-without-drop verified in code and probe |
| NetServerHost | GameHandlers | `Game.DispatchAsync` before generic critical relay | WIRED | Ordering fix from 02-03 confirmed in source (line ~273) |
| ServerBoot | ConfigStore.Changed | `OnConfigChanged` → `GameBroadcastReliableAsync` | WIRED | Toggle→save+broadcast path present |
| DummyBot | NetServerHost (IGameTransport) | `BroadcastStateAsync` with clientId 999 | WIRED | Probe saw 999 snapshots + teleport + bye |
| Client NetworkService | Server envelope | Same magic/version/offsets, Hello(-1)→Welcome→Confirm | WIRED | Probe (same codec) + client grep evidence |
| Legacy `Game/` + Core project | Build output | `Compile Remove` + no `ProjectReference` | SEVERED (intended) | Files on disk as reference only; output has no Core.dll |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Relay path | snapshot bytes | Original client datagram re-emitted to other sessions | Yes — probe confirmed byte-identical delivery to B, no echo | ✓ FLOWING |
| Chat path | formatted sender+text | Server-built payload, per-destination seq + pending | Yes — truncation/strip/help-unicast all probe-verified | ✓ FLOWING |
| CONFIG_SYNC | 8 bools | ConfigStore snapshot → unicast on join + broadcast on change | Yes — probe fase1/fase2 saw exact `[on,off×7]` and persistence | ✓ FLOWING |
| Dummy snapshots | `BuildPlayerStatePayload` | DummyBot position/anim state, header clientId 999 | Yes — probe received 999 snapshots, teleport echo, toggle bye | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Server Release builds clean | `dotnet build OriCoopDedicatedServer.csproj -c Release` | `0 Aviso(s) 0 Erro(s)` | ✓ PASS |
| Full automated battery | `dotnet run SmokeProbe -c Release -- --port 7789 --test all` | `SMOKE_OK`, 15 PASS (readiness, invalid-magic, handshake, relay, ping, reliable, chat-rules, token, timeout, server-full, game-fase1, game-fase2, game) | ✓ PASS |
| Cutover output | `ls bin/Release/net8.0/*.dll` | Only `OriCoopDedicatedServer.dll`, no `Core.dll` | ✓ PASS |
| Single `--test ping` alone | Same probe, `--test ping` without server | `SMOKE_FAIL: ping` at handshake (no server listening — single modes attach to a running server by design) | ? SKIP (usage error by verifier; covered by `all` run above, not a code gap) |

### Requirements Coverage

Phase defined by `02-CONTEXT.md` decisions D-01..D-16 (no ROADMAP.md/REQUIREMENTS.md in repo — the CONTEXT file is the canonical record per its own header). Coverage: D-01 (UdpClient async, no RUDP lib) ✓, D-02 (envelope magic+version+seq+clientId+packetId, one-way) ✓, D-03 (BinaryPrimitives LE, length-first) ✓, D-04 (single ReceiveAsync loop + Channel + CancellationToken) ✓, D-05 (dynamic IDs, Count vs MaxPlayers, SERVER_FULL) ✓, D-06 (Hello/Welcome/Confirm, IsReady gate, Player_ID fallback) ✓, D-07 (2 s heartbeat, 10 s sweep, new-ID reconnect) ✓, D-08 (endpoint+token per datagram) ✓, D-09 (unreliable sequenced PLAYER_STATE) ✓, D-10 (ACK+retry criticals incl. chat/config/teleport/syncs/skill/color/disconnect) ✓, D-11 (immediate relay, on-change+heartbeat) ✓, D-12 (Ping 104/Pong 105 sys-msgs) ✓, D-13 (Transport/Session/Game split, no static globals, ILogger+CT) ✓, D-14 (version break, no fallback, same-build pair) ✓, D-15 (packet/payload redesign, protocol.md in same change) ✓, D-16 (net8 CLI flags, console commands, file+console logs, persisted config never zeroed) ✓.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | `TODO/FIXME/XXX/placeholder/coming soon` in `Net/` and `NetworkService.cs` | — | None — zero hits |
| `Game/NetworkHandler.cs` (legacy, out of build) | comment | `BUG #3` marker | ℹ️ Info | None — legacy file excluded from compilation, reference only |
| `src/.../Logs/` (untracked) | — | Runtime `server.log` output from battery runs (orchestrator's + verifier's) | ℹ️ Info | None — untracked, not in build; consider gitignore entry in a later pass |

### Human Verification Required

None for the phase goal — all automated truths verified. The known manual item below is explicitly out of scope (deferred, documented, not a gap):

- **In-game validation with 2 real clients** (`docs/operations.md` marks **a confirmar**): connect two game clients through the deployed server, move/teleport/chat/dummy, disconnect/reconnect. This exercises Unity-side rendering/application, which no automated probe can cover. Recorded in operations.md checklist; belongs to a playtest pass, not this phase's code goal.

### Gaps Summary

No gaps. All 8 must-haves verified against the actual codebase (files read, build re-run, probe re-run). Previous single-mode probe failure during verification was verifier usage error (single modes require a pre-running server), superseded by the full `SMOKE_OK` run. `git status` shows only untracked runtime `Logs/` from battery runs — no source residue.

---
_Verified: 2026-10-05_
_Verifier: the agent (gsd-verifier)_
