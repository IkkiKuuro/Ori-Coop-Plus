---
phase: 03-player-event-core
plan: 01
subsystem: netcode
tags: [harmony, udp, unreliable-sequenced, spirit-flame, player-event, csharp5, bepindex, dedicated-server]

# Dependency graph
requires:
  - phase: 02-network-hardening
    provides: [envelope 24B 0x4F43/v2, SendSystem/SendReliable, RelayUnreliableAsync, PlayerStateRelay drop-old, IsCriticalPacket]
provides:
  - PLAYER_EVENT=19 one-way wire contract (frozen 37B body, unreliable flags=0, header-only identity)
  - PlayerEventCore bus+registry (PublishSpiritFlame, DispatchLocal fail-closed)
  - SpiritFlamePatch detector (ThrowSpiritFlames Postfix, local-only filter)
  - End-to-end thin path: patch -> core -> packet 19 -> blind relay -> puppet attack clip
  - docs/protocol.md packet-19 row + frozen field-order table
affects: [03-02-visual-hardening, 03-03-observability-probe, future-event-categories]

# Actuals (#2632) — pairs with the plan's `estimate` to calibrate future estimates.
actuals:
  tokens: 9200    # chars/4 over the realized diff (36733 chars)
  tasks: 3        # tasks completed
  commits: 3      # 2 task commits + 1 summary commit

# Tech tracking
tech-stack:
  added: []
  patterns: [unreliable-sequenced game packet (marker-led body + shared seq gate + byte-identical relay), local-only Harmony detector (owner-Sein filter + core delegation, zero network in patch), fail-closed kind registry (unknown -> keep-last + reason log)]

key-files:
  created:
    - src/OriCoopPlus/OriCoopShared/PlayerEventProtocol.cs
    - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventKind.cs
    - src/OriCoopPlus/OriCoopBepInEx/Events/SpiritFlameEventData.cs
    - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs
    - src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs
  modified:
    - src/OriCoopPlus/OriCoopShared/PacketType.cs
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
    - docs/protocol.md

key-decisions:
  - "Packet 19 APPROVED as the one-way contract (37B frozen body, flags=0, header-only identity, same-build, docs same-change)"
  - "Harmony FALLBACK authorized: Postfix on SeinSpiritFlameAbility.ThrowSpiritFlames + facing-based direction + temp [EVENT] log"
  - "Wave-0 target verified by direct metadata read of the installed Assembly-CSharp.dll (stronger than dnSpy eyeballing): ThrowSpiritFlames(SpiritFlame) confirmed, m_sein:SeinCharacter confirmed, OnShoot refuted for player classes"
  - "Server landing zone = GameHandlers (IsGamePacket + DispatchAsync), not a dedicated NetServerHost branch"
  - "Tracer reuses the shared send-seq counter and shared relay gate; separate event seq domain is plan-02 scope"

patterns-established:
  - "New unreliable game packet = marker-led body + SendSystem(id, body, 0) + IsNewerThanLast + ExpectRemaining + RelayUnreliableAsync, never SendReliable, never IsCriticalPacket"
  - "Harmony detector = [HarmonyPatch] + ___m_sein owner filter vs Game.Characters.Sein + temp [EVENT] log + core delegation, zero network code in patch"
  - "Remote event handling = main-thread enqueue -> manager lazy-puppet -> core DispatchLocal -> puppet clip-only visual, never publish back"

requirements-completed: [D-02, D-03, D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "PLAYER_EVENT=19 contract compiles on both ends (client csc.exe + server dotnet, zero warnings)"
    requirement: "D-10"
    verification:
      - kind: other
        ref: "build.ps1 -> OriCoopBepInEx.dll 94208 bytes, SUCCESS"
        status: pass
      - kind: other
        ref: "dotnet build OriCoopDedicatedServer.csproj Release -> 0 warnings 0 errors"
        status: pass
    human_judgment: false
  - id: D2
    description: "docs/protocol.md packet-19 row + frozen 37B field-order table in the same change"
    requirement: "D-10"
    verification:
      - kind: other
        ref: "docs/protocol.md PLAYER_EVENT subsection + ID table row 19"
        status: pass
    human_judgment: false
  - id: D3
    description: "Binaries deployed to Ori DE install dirs with matching timestamps"
    verification:
      - kind: other
        ref: "BepInEx/plugins/OriCoopBepInEx.dll 94208B 12:45 == build output; Server/exe+dll 12:45 == build output"
        status: pass
    human_judgment: false
  - id: D4
    description: "Two-client run: shooter log shows [EVENT] detect line, remote puppet plays attack clip, no line on remote, no movement freeze"
    requirement: "D-06"
    verification: []
    human_judgment: true
    rationale: "Requires launching the game with 2 clients on LAN; cannot be automated in this environment"

# Metrics
duration: ~55min
completed: 2026-10-06
status: complete
---

# Phase 03 Plan 01: Player Event Core Tracer Summary

**Spirit Flame shot travels patch -> PlayerEventCore -> packet 19 (unreliable) -> blind server relay -> remote puppet attack clip, with the one-way contract locked in docs and both binaries deployed.**

## Performance

- **Duration:** ~55min (resumed continuation incl. Wave-0 metadata probe of the game DLL)
- **Started:** 2026-10-06 ~12:35 UTC-3 (gate already resolved by user)
- **Completed:** 2026-10-06
- **Tasks:** 3/3
- **Files modified:** 14 (5 created, 9 modified)

## Gate Resolution (Task 1)

Blocking decision gate resolved by the user before this continuation. Verbatim answers, recorded here per the plan:

- **Decision 1 (packet-19 one-way contract):** `APPROVED 19` — PLAYER_EVENT=19, frozen 37B body (int marker 19 + byte kind + dir 3xfloat + origin 3xfloat + long timestamp), unreliable-sequenced (flags=0), identity in envelope header clientId only, same-build client+server, docs/protocol.md updated in same change.
- **Decision 2 (Wave-0 Harmony target):** `FALLBACK autorizado` — Postfix on `SeinSpiritFlameAbility.ThrowSpiritFlames` + facing-based direction fallback (Sein.FaceLeft → ±X, origin = Sein position), temporary [EVENT] log line, local-only filter via injected `___m_sein == Game.Characters.Sein`.

Task 1 produced no file changes (decision gate), so it has no commit; the resolution is recorded in this SUMMARY.

## Wave-0 Verification (stronger than planned)

The plan asked for dnSpy/ILSpy confirmation. Instead of eyeballing a decompiler, the exact target was verified by reading the installed game DLL's metadata directly (`Assembly-CSharp.dll`, `C:\Program Files (x86)\Steam\...\oriDE_Data\Managed`):

- `SeinSpiritFlameAbility` **declares** `ThrowSpiritFlames(SpiritFlame)` — instance, void, 1 param (raw sig `20-01-01-12-80-B0`, token resolves to typedef `SpiritFlame`).
- Field `m_sein : SeinCharacter` present on **all three** flame ability classes (`SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `SeinChargeFlameAbility`).
- `OnShoot` hypothesis **refuted** for player abilities (exists only on enemy classes) — the authorized fallback was the right call.
- `SeinCharacter.FaceLeft` (bool), `Position`/`Speed` (Vector3) confirmed as properties.

Probe tooling lived in the temp dir only (never committed to the repo). Consequence: the patch uses an exact typed Postfix `(SeinSpiritFlameAbility __instance, SeinCharacter ___m_sein)` — no guessing.

## Accomplishments

- Shared contract: `PLAYER_EVENT = 19` + consts-only `PlayerEventProtocol` (37B, offsets kind=4/dir=5/origin=17/ts=29) compiling on C# 5 client and .NET 8 server.
- `PlayerEventCore` bus+registry: `PublishSpiritFlame` (stamps kind, calls transport, no-op when disconnected) + `DispatchLocal` (registry lookup, fail-closed unknown-kind log, never publishes).
- `SpiritFlamePatch`: Postfix on the verified target, `___m_sein` owner filter vs `Game.Characters.Sein`, facing-based direction, Sein-position origin, unconditional temp `[EVENT] SpiritFlame detected` log, zero network code.
- Client transport: `SendPlayerEvent` (flags=0, never reliable) + case-19 receive branch (marker check, shared `IsNewerThanLast` drop-old, 33-byte `ExpectRemaining`, main-thread marshal to `OnPlayerEventReceived` → `HandlePlayerEvent`); interface + plugin wiring in all four lifecycle sites.
- Remote side: `HandlePlayerEvent` (sender guard, lazy puppet via factory + `SnapTo`, no publish) → registry → `PlaySpiritFlameVisual` (**clip only**: `AimThrow` state resolve, fail-closed null return, verbose log). Particle/SFX/fake projectile explicitly plan-02 scope.
- Server: `TryParsePlayerEvent`/`BuildPlayerEventPayload` + `HandlePlayerEventAsync` (Warning-drop on truncation, shared `_relay` gate, byte-identical `RelayUnreliableAsync`, no `fromId` stamping); registered in `IsGamePacket` + `DispatchAsync` (**single landing zone via GameHandlers**, which inherits the host's `IsReady` gate); `IsCriticalPacket` untouched, doc comment extended.
- `docs/protocol.md`: ID-table row 19, new PLAYER_EVENT subsection with frozen field-order table, unreliable-class mentions; remote visual marked **a confirmar** (pending the 2-client run).
- Both builds green with **zero warnings**; binaries deployed to the Ori DE install dirs with matching timestamps (no game/server processes running, no lock handling needed).

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate (approve packet-19 contract + Harmony target)** — no commit (decision gate, no file changes); resolution recorded in this SUMMARY.
2. **Task 2: Tracer (local shot → remote puppet clip via packet 19)** — `3f63fc7` (feat, 13 files, +534/-4).
3. **Task 3: Docs lock + dual build + deploy** — `504f37f` (docs, `docs/protocol.md` +32/-2).

**Plan metadata:** this SUMMARY commit (see below).

## Files Created/Modified

- `src/OriCoopPlus/OriCoopShared/PlayerEventProtocol.cs` — NEW shared 37B codec consts.
- `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventKind.cs` — NEW byte enum (Unknown=0, SpiritFlame=1, append-only).
- `src/OriCoopPlus/OriCoopBepInEx/Events/SpiritFlameEventData.cs` — NEW DTO struct (no Unity types).
- `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs` — NEW bus+registry.
- `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs` — NEW detector.
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — PLAYER_EVENT=19.
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — SendPlayerEvent + case-19 branch + event.
- `src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs` — send decl + received event.
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` — BindTransport, OnPlayerEventReceived, 4-site wiring.
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` — HandlePlayerEvent + SpiritFlame handler registration.
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — PlaySpiritFlameVisual (clip only).
- `src/OriCoopDedicatedServer/.../Net/Game/GameHandlers.cs` — TryParse/Build/Handle + predicate + dispatch.
- `src/OriCoopDedicatedServer/.../Net/NetServerHost.cs` — critical-list doc comment names 19.
- `docs/protocol.md` — packet-19 row + frozen field-order table.

## Decisions Made

- Server landing zone = `GameHandlers.IsGamePacket` + `DispatchAsync` case (one landing zone; inherits `IsReady` gating from the host). Rejected the alternative dedicated `NetServerHost` branch beside PLAYER_STATE to keep all game-packet logic behind the Game layer.
- Tracer reuses the single shared send-seq counter + shared `IsNewerThanLast`/`PlayerStateRelay` gate (single counter feeds both streams in send order, so wrap-safe ordering holds). Separate event seq domain is plan-02 scope, documented in code comments — not a silent omission.
- Patch detect line logs **unconditionally** (not verbose-gated) so the 2-client acceptance check cannot fail on a forgotten config flag; it becomes verbose-gated after in-game validation.
- `docs/README.md` index untouched: its protocol link is generic, no packet-19 mention needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] Reflection fallback for the patch owner lookup**
- **Found during:** Task 2 (SpiritFlamePatch)
- **Issue:** Plan assumed `___m_sein` injection always resolves; if the field is ever renamed, injection silently yields null and the pilot dies with zero signal.
- **Fix:** Added private `ResolveOwner` (walks the `__instance` hierarchy for the first `SeinCharacter`-typed field, try/catch → null). Small, C# 5-safe, zero behavior change on the verified path.
- **Files modified:** `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs`
- **Verification:** client `build.ps1` green; fallback unreachable while `m_sein` exists (confirmed present).
- **Committed in:** `3f63fc7` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (hardening, no scope creep)
**Impact on plan:** None on scope or contract; strictly additive robustness.

## Issues Encountered

- PowerShell reflection-only load of `Assembly-CSharp.dll` failed on `UnityEngine` resolution (resolve handler never fired). Solved with a throwaway `dotnet` + `System.Reflection.Metadata` reader (temp dir, uncommitted) — which additionally yielded exact signatures, better evidence than the dnSpy pass the plan envisioned.
- Two working-tree edit slips during implementation (a dropped line in `RemovePlayer`, a dropped brace in `BuildTeleportResponse`) — both caught by immediate re-read and repaired before any commit; final diff contains neither.

## Threat Flags

None — no security-relevant surface beyond the plan's `<threat_model>`: case-19 enforces marker + 33-byte length check before every read (T-03-01), inherits session token/endpoint + `IsReady` gating (T-03-02), wrap-safe drop-old both ends (T-03-03), unreliable-class with no retry/ACK state (T-03-04), visual-only puppet path with no gameplay mutation (T-03-05). No new packages (T-03-SC).

## Known Thin-Scope (by plan, not stubs)

No placeholder stubs in the new code. Deliberately thin per plan, completed in follow-up plans:

- Puppet visual = attack clip only (particle, transient SFX, fake projectile → plan 02).
- Shared seq domain for events (separate event seq domain → plan 02).
- SmokeProbe packet-19 relay case + `docs/operations.md` pilot checklist (→ plan 03).
- Charge-burst path (`SeinChargeFlameAbility.ReleaseChargeBurst`) uses a different fire method — standard shots only in this tracer.
- In-game 2-client verification pending (coverage D4, `human_judgment: true`).

## Build & Deploy Record

- `build.ps1`: SUCCESS, `OriCoopBepInEx.dll` 94,208 bytes, 0 warnings.
- `dotnet build OriCoopDedicatedServer.csproj Release`: SUCCESS, 0 warnings, 0 errors.
- Deployed (no processes running, no locks): plugin DLL → `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\BepInEx\plugins\` (94,208 B, 12:45); server exe+dll+pdb → `...\Ori DE\Server\` (timestamps 12:45, matching build output). `serverconfig.json`/bats untouched.

## User Setup Required

None - no external service configuration required. Manual step outstanding (not setup): the 2-client in-game run (fire Spirit Flame on A → `[EVENT]` line in A's `LogOutput.log`, attack clip on B's puppet, silence on B, no movement freeze).

## Next Phase Readiness

- Tracer committed and deployed; plan 02 (visual fidelity: particle + transient SFX + fake projectile, separate seq domain) and plan 03 (observability + SmokeProbe case + operations checklist) can build directly on `PlayerEventCore`/`PLAYER_EVENT` without rework.
- Blocker: none for plans 02/03 code work. The 2-client manual verification (D4) should happen before or alongside plan 02's visual calibration, since plan 02 tunes what the tracer proves.

## Self-Check: PASSED

- All 5 created files FOUND on disk; all 9 modified files present.
- Commits `3f63fc7` (tracer) and `504f37f` (docs) verified in `git log`.
- No unintended deletions (`git diff --diff-filter=D` clean for task commits).
- No `TODO/FIXME/placeholder` stubs in new files; no new packages.

---
*Phase: 03-player-event-core*
*Completed: 2026-10-06*
