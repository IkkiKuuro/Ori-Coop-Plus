---
phase: 03-player-event-core
plan: 03
subsystem: netcode
tags: [udp, unreliable-sequenced, spirit-flame, player-event, observability, smokeprobe, csharp5, bepindex, dedicated-server]

# Dependency graph
requires:
  - phase: 03-player-event-core plan 02
    provides: [full-fidelity visual-only remote shots, separate event seq domain on 3 sites, per-field case-19 validation, fail-closed unknowns]
provides:
  - Event telemetry (TrackPlayerEvent + EvRecv/EvApplied/EvDropped in throttled summary + [EVENT] ring vocabulary, D-12)
  - SmokeProbe packet-19 relay case (byte-identical, no-echo, drop-old same+older, no-SysAck, D-08/D-09)
  - Spirit Flame pilot checklist in docs/operations.md (6 checks + D-12 observation fields, D-10/D-13 gate)
  - Final deployed binaries (client 100864B + server exe/dll)
affects: [future-event-categories, entities-world-sync, verify-work]

# Actuals (#2632) — pairs with the plan's `estimate` to calibrate future estimates.
actuals:
  tokens: 5250    # chars/4 over the realized diff (21015 chars)
  tasks: 3        # tasks completed
  commits: 4      # 3 task commits + 1 summary commit

# Tech tracking
tech-stack:
  added: []
  patterns: [parallel event telemetry mirroring packet shape (counters + kind/applied track entry + shared 3s summary), probe relay-preservation case (fixed known payload + identical-bytes + no-echo + drop-old + no-SysAck), manual pilot gate as dated operations entry]

key-files:
  created: []
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs
    - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs
    - src/OriCoopDedicatedServer/SmokeProbe/Program.cs
    - docs/operations.md

key-decisions:
  - "TrackPlayerEvent mirrors TrackPacket exactly (received always++, applied/dropped by flag, shared 3 s gate); EmitMetricsSummary internalized to parameterless with cached packet context so the event path can trigger the same summary without touching RemotePlayerPuppet"
  - "Server BuildPlayerEventPayload mirror reused from wave 1 — no GameHandlers change in this plan (verify-only)"
  - "Probe relay group semantics: --test relay now runs snapshot-18 + event-19; standalone --test event added for the 19-only case"
  - "Pilot checklist lives as a dated operations.md entry, not a new battery page — no README index change (verified consistent)"

patterns-established:
  - "Event telemetry = counters + TrackPlayerEvent(kind, applied) + Record([EVENT] ...) lines with transition reasons (enviado -> recebido -> aplicado + motivo), throttled summary on the shared 3 s cadence, RingCapacity 200 + s_ringSync unchanged"
  - "Probe game-packet case = fixed known body + identical-bytes + no-echo + same-seq drop + older-seq drop + no-SysAck, wired into all-tests and the relay group"

requirements-completed: [D-08, D-10, D-12, D-13, D-15]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "Event transitions observable with counters and motives (sent/received/applied/dropped) in ring log + throttled summary (D-12)"
    requirement: "D-12"
    verification:
      - kind: other
        ref: "build.ps1 SUCCESS (100864B, 0 warnings); code review of TrackPlayerEvent call sites (publish sent, dispatch received/applied/dropped)"
        status: pass
    human_judgment: true
    rationale: "Counter movement + EVENT ring lines under real shots require the 2-client in-game run with verbose on; automation proves compile + hook placement"
  - id: D2
    description: "SmokeProbe packet-19 relay case green inside SMOKE_OK (identical bytes, no echo, drop-old, no SysAck)"
    requirement: "D-08"
    verification:
      - kind: other
        ref: "dotnet run SmokeProbe -- --port 7779 --test all => SMOKE_OK (PASS player-event line in output)"
        status: pass
    human_judgment: false
  - id: D3
    description: "docs/operations.md pilot checklist (6 checks + spam observation fields); docs/README.md index consistent"
    requirement: "D-10"
    verification:
      - kind: other
        ref: "operations.md pilot section present; README untouched (no new page, operations link already indexed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Two-client pilot executed end to end with per-check results (C1-C6)"
    requirement: "D-13"
    verification: []
    human_judgment: true
    rationale: "Requires launching the game with 2 clients on LAN; cannot be automated in this environment — recorded a-confirmar in operations.md"

# Metrics
duration: ~50min
completed: 2026-10-06
status: complete
---

# Phase 03 Plan 03: Observability + Probe + Pilot Gate Summary

**Event transitions are counted and logged with motive, packet-19 relay behavior is automation-locked inside a green SMOKE_OK suite, and the repeatable two-client pilot checklist is documented — with final binaries deployed.**

## Performance

- **Duration:** ~50min
- **Started:** 2026-10-06 ~15:50 UTC-3
- **Completed:** 2026-10-06
- **Tasks:** 3/3
- **Files modified:** 4 (0 created, 4 modified)

## Accomplishments

- Event observability (D-12): `TrackPlayerEvent(kind, applied)` + `EvRecv/EvApplied/EvDropped/EvKind` in the shared throttled 3 s `[NET-METRICS]` summary; `[EVENT]` sent/received/unknown-kind lines recorded to the ring (capacity 200, lock discipline unchanged), all verbose-gated exactly like the anim transition logs. Publish counts sent, dispatch counts received + applied-or-dropped with motives (`tipo-desconhecido`, handler-exception dropped even outside verbose so silent failures surface in the numbers).
- SmokeProbe packet-19 case (D-08/D-09): `TestPlayerEvent` proves byte-identical relay (sender + seq + 37 B body preserved), no echo to the sender, same-seq resend dropped, older-seq dropped, and no `SysAck` 103 generated for packet 19 — wired into `--test all` and the `relay` group, with a standalone `--test event` mode. Full suite prints `SMOKE_OK` (17 PASS lines incl. the new `PASS player-event`).
- Pilot gate docs (D-10/D-13): `docs/operations.md` holds the repeatable Spirit Flame checklist — automated pre-gate commands with `SMOKE_OK`, prerequisites, C1–C6 (both-directions visual, no-echo via logs, spam-under-movement smoothness, unknown-kind hold, disconnect/reconnect), and the D-12 spam observation fields (shots/s estimate + `EvRecv/EvApplied/EvDropped` readings). `docs/README.md` index verified consistent (no new page, no change).
- Final dual build + deploy per AGENTS.md: client `OriCoopBepInEx.dll` 100,864 B (0 warnings), server Release 0 warnings 0 errors, `SMOKE_OK`; deployed with no blocking processes (no lock handling needed), sizes matching build outputs.

## Task Commits

Each task was committed atomically:

1. **Task 1: Event observability (counters + transition vocabulary)** — `83e9e60` (feat, 2 files, +77/-10).
2. **Task 2: SmokeProbe packet-19 relay case** — `dbe0330` (feat, 1 file, +178/-4).
3. **Task 3: Operations checklist + index + final dual build + deploy** — `1191dc2` (docs, 1 file, +49/-0).

**Plan metadata:** this SUMMARY commit (see below).

## Files Created/Modified

- `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs` — MODIFIED: event counters (`s_eventsReceived/Applied/Dropped`, `s_lastEventKind`), `TrackPlayerEvent(kind, applied)` mirroring `TrackPacket`, `EmitMetricsSummary` internalized to parameterless (cached `s_lastPacketPlayerId/Hash/State`) printing `EvRecv/EvApplied/EvDropped/EvKind` and resetting all six counters. `TrackPacket` public signature unchanged — `RemotePlayerPuppet` untouched.
- `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs` — MODIFIED: `using Diagnostics`; publish records + tracks sent (`fase=enviado`, applied=true); dispatch records + tracks received (applied=true) on known kind, tracks dropped (applied=false) on handler exception, records + tracks dropped on unknown kind (`motivo=tipo-desconhecido`) — all verbose-gated like anim logs except the exception-drop counter.
- `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` — MODIFIED: `PlayerEventId=19` const, `TestPlayerEvent` (fixed-payload send, identical-bytes, no-echo, same-seq drop, older-seq drop, no-SysAck), `BuildPlayerEventBody` (frozen 37 B: marker 19 + kind 1 + dir (1,0,0) + origin (10.5,20.25,-2.5) + fixed ticks) + `WriteI64Stream` helper; wired into `RunAll` (after `TestRelay`), `relay` group runs both, new `event` mode (`EVENT_OK`); header comment + unknown-test usage updated.
- `docs/operations.md` — MODIFIED: pilot checklist section (pre-gate commands, prerequisites, C1–C6, D-12 observation fields, round result `a confirmar`).

## Decisions Made

- `EmitMetricsSummary` went parameterless with cached packet context rather than giving `TrackPlayerEvent` a playerId it doesn't have (publish path has no sender id) or changing `TrackPacket`'s signature (which would have dragged `RemotePlayerPuppet` into the diff). The event path triggering the summary still prints the last anim state — documented in a comment.
- Server `BuildPlayerEventPayload`/`TryParsePlayerEvent` mirror reused verbatim from wave 1: `GameHandlers.cs` is listed in the plan's files but needed zero changes (verify-only). Recorded here, not a deviation — the plan's server work was already landed.
- `--test relay` now runs snapshot-18 + event-19 as a group (plan: "wire into the relay test group"), plus standalone `--test event` for the 19-only iteration loop. Both ship with the server from the same source, so no version-skew risk.
- Pilot checklist as a dated `operations.md` entry, not a new battery page or `anim-test-battery.md` T5: the pilot is 6 checks + observation fields, and the battery file is the anim-rework's scoped instrument. No new page means `docs/README.md` needed no index change — verified consistent.
- Handler-exception drops tracked unconditionally (outside the verbose gate): a throwing visual handler must move the dropped counter even in non-verbose production runs, or the failure is invisible in the numbers. Small, deliberate break from the "all verbose-gated" line for the counter only — log line still goes through `LogWarning` always (as before).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Restored dropped `BuildEnvelope` signature line**
- **Found during:** Task 2 (probe builder insertion)
- **Issue:** The `BuildPlayerEventBody` insert replaced the `private static byte[] BuildEnvelope(...)` signature line instead of anchoring before it, leaving a signature-less block.
- **Fix:** Re-added the signature line; verified by read-back plus full `dotnet build` (0 warnings) and the passing probe run.
- **Files modified:** `src/OriCoopDedicatedServer/SmokeProbe/Program.cs`
- **Commit:** `dbe0330` (Task 2 commit)

**2. [Rule 3 - Blocking] Isolated `--test relay/event` needed a live server**
- **Found during:** Task 2 verification
- **Issue:** `--test relay` connects to an already-running server; the first run failed on handshake with nothing on the port.
- **Fix:** Started a manual server on port 7781 in the background, ran `--test relay` (RELAY_OK) and `--test event` (EVENT_OK) against it, killed it via taskkill, then ran `--test all` on 7779 (spawns its own server) to `SMOKE_OK`.
- **Files modified:** none (harness only)
- **Commit:** n/a (verification flow)

---

**Total deviations:** 2 auto-fixed (one edit slip, one harness-operating need; no scope creep)
**Impact on plan:** None on scope or contract; strictly execution-level.

## Issues Encountered

- PowerShell has no `tail`/`head` — used `Select-Object` instead for output inspection. No impact.
- Background helper server's log showed `Sem ACK ... apos 3 retries` warnings during the probe runs: expected cosmetic noise (server retrying reliable COLOR/CONFIG_SYNC while the probe drains without ACKing), same as documented in `operations.md`. The probe still passed; not a regression.
- Client-side stale-sequence drops (`NetworkService.IsNewerThanLastEvent` silent return, before `DispatchLocal`) remain uncounted: counting them would require touching `NetworkService.cs` (outside this plan's files) or parsing kind pre-gate. Server-side stale drops ARE proven by the new probe case (same-seq + older-seq assertions on the `EventRelayGate`); client spam calibration uses the EvRecv/EvApplied/EvDropped counters that do land here. Not a stub — a documented boundary.

## Threat Flags

None — no security-relevant surface beyond the plan's `<threat_model>`: telemetry is counters + bounded ring lines (capacity 200 unchanged, throttled summary; T-03-04 measurable, no throttle added per D-12); probe uses fixed known bodies against the real gate on ready sessions only (T-03-01/T-03-02/T-03-03 — truncation/marker validation path unchanged and still Warning-drops, opaque-kind relay is by design since the server treats kind as an opaque byte); no new packages (T-03-SC); no crypto added, positions remain LAN-public game state (T-03-05, accepted).

## Known Stubs

No placeholder stubs in the new code (grep for TODO/FIXME/placeholder/coming-soon empty in both source files). Deliberately deferred, not stubs:

- C5 unknown-kind hold has no operator injection tooling — manual execution marked `a confirmar` in `operations.md`; fail-closed path is code-reviewed (`DispatchLocal` + clip-null return).
- C1–C4/C6 two-client visual results marked `a confirmar` — require the LAN game run (coverage D4, `human_judgment: true`), same standing as waves 1–2.

## Build & Deploy Record

- `build.ps1`: SUCCESS, `OriCoopBepInEx.dll` 100,864 bytes, 0 warnings (Task 1 and Task 3 runs).
- `dotnet build OriCoopDedicatedServer.csproj --configuration Release`: SUCCESS, 0 warnings, 0 errors (Task 2 and 3 runs).
- `SmokeProbe --test relay` (vs manual server :7781): `PASS relay` + `PASS player-event`, `RELAY_OK` + `SMOKE_OK`.
- `SmokeProbe --test event` (vs manual server :7781): `PASS player-event`, `EVENT_OK` + `SMOKE_OK`.
- `SmokeProbe --test all` (fresh server :7779): 17 PASS lines incl. `PASS player-event (...)`, `SMOKE_OK`, exit 0 (Task 2 and 3 runs).
- Deployed (no `OriDE.exe`/server processes running, no lock handling needed): plugin DLL → `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\BepInEx\plugins\` (100,864 B, matches build output); server exe (152,064 B) + dll (107,008 B) + pdb → `...\Ori DE\Server\` (match build outputs). `serverconfig.json`/bats untouched. `D:\SteamLibrary\...` does not exist on this machine — `C:\...` only.

## User Setup Required

None - no external service configuration required. Manual step outstanding (not setup): the 2-client pilot run per the new `operations.md` checklist — fire Spirit Flame A→B and B→A, confirm no-echo in logs, spam-under-movement smoothness, unknown-kind pose-hold (needs injector), disconnect/reconnect clean; record date, DLL bytes, and C1–C6 pass/fail in `operations.md` (coverage D4, `human_judgment: true`).

## Next Phase Readiness

- Phase 03 scope is now closed on all three plans: tracer (01) + visual/seq hardening (02) + observability/probe/checklist (03). The phase is ready for verify-work: automated suite green, manual gate documented with `a confirmar` fields awaiting the LAN run.
- Watch item for the entities phase: scene transitions vs statically cached visual assets (noted in 03-02) is unchanged by this plan; the new telemetry (EvRecv/EvApplied/EvDropped + EvKind) is the instrument the throttle-calibration follow-up (D-12) will read.
- Blocker: none. The 2-client manual verification (D4 + operations C1–C6) should happen before throttle tuning, since tuning reads the counters this plan added.

## Self-Check: PASSED

- All 4 modified files FOUND on disk; `git diff 75fbe41..HEAD --stat` shows exactly the 4 plan files (+304/-14).
- Commits `83e9e60` (task 1), `dbe0330` (task 2), `1191dc2` (task 3) verified in `git log`.
- No unintended deletions (`git diff --diff-filter=D` clean for task commits).
- No `TODO/FIXME/placeholder` stubs in new code; no new packages; `PLAYER_EVENT` still absent from `IsCriticalPacket` (unreliable class preserved).
- `docs/README.md` untouched and consistent (operations link already indexed; no new page).

---
*Phase: 03-player-event-core*
*Completed: 2026-10-06*
