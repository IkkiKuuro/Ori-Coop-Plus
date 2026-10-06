---
phase: 03-player-event-core
plan: 04
subsystem: netcode
tags: [udp, player-event, spirit-flame, gap-closure, visual-only, observability, csharp5, bepindex, dedicated-server]
requires:
  - phase: 03-player-event-core plan 02
    provides: [full-fidelity visual path, disjoint event seq domain, hardened case-19]
  - phase: 03-player-event-core plan 03
    provides: [SmokeProbe player-event case, EVENT/ANIM observability vocabulary, pilot checklist]
provides:
  - Clip-independent remote Spirit Flame effects (particle + sound + fake projectile always spawn, clip best-effort)
  - Reader-style AimThrow substring population (clip-null becomes the rare exception)
  - Default-config EVENT telemetry (ungated transition lines + counters, working in-game verbose toggle)
  - Deployed gap-closure binaries, probe-green, awaiting human 2-client pilot
affects: [human-pilot-C1-C6, future-event-categories, entities-world-sync]

actuals:
  tokens: 2700    # chars/4 over the realized diff (10694 chars)
  tasks: 4        # auto tasks completed (task 5 is a blocking human checkpoint)
  commits: 5      # 4 task commits + 1 summary commit

tech-stack:
  added: []
  patterns: [best-effort clip with unconditional effects, reader-mirrored substring alias population, ungated discrete-event telemetry]

key-files:
  created: []
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs
    - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
    - docs/operations.md

key-decisions:
  - "Effects-first ordering in PlaySpiritFlameVisual: the three try/catch-guarded spawns run before clip resolve, so a clip miss can never suppress them — clip stays best-effort with an always-visible motivo line"
  - "AimThrow substring fallback lowercases once and Contains-tests aim/throw on both outer and inner names, Sein-provenance-first and first-wins — future F8-calibrated exact names keep precedence, no SEED-table edit"
  - "Ungated lines are bounded by discrete shot rate (D-12 no-throttle), not per-frame — no log-spam risk; per-effect partial-skip VerboseEvent stays verbose-gated as pure tuning noise"
  - "SetVerboseLogging null-guards _animVerbose exactly like IsAnimVerbose does, then sets it alongside ShowNetworkLogs"

requirements-completed: [D-05, D-07, D-12, D-13, D-14, D-15]

duration: ~25min
completed: 2026-10-06
status: complete
---

# Phase 03 Plan 04: Player-Event Gap Closure (G-03-1/G-03-2/G-03-3) Summary

**Remote Spirit Flame effects no longer depend on clip resolve, AimThrow populates via reader-style substring match, and EVENT transition lines plus counters move with default config — built, probe-green, deployed, awaiting the human 2-client pilot (C1–C6).**

## Performance

- **Duration:** ~25min
- **Started:** 2026-10-06 ~15:05 UTC-3
- **Completed:** 2026-10-06
- **Tasks:** 4/4 auto tasks (task 5 is a blocking human checkpoint, returned separately)
- **Files modified:** 5 (0 created, 5 modified)

## Accomplishments

- G-03-1/G-03-2: `PlaySpiritFlameVisual` reordered — `SpawnMuzzleParticle` + `PlayTransientShotSound` + `SpawnFakeProjectile` (each already try/catch-guarded, implementations untouched) run unconditionally before clip resolve. Clip-null is now a log-and-continue: always-Recorded + always-LogInfo `aplicado=manteve-atual motivo=clip-desconhecido`, no `SetAnimation`, no Idle fallback (D-15 fail-closed). Clip-hit path keeps the `IsPlayingClip`-guarded `SetAnimation` plus an ungated `aplicado=<clip>` line (D-13 all four effects, D-14 zero gameplay mutation).
- G-03-1/G-03-2: `RegisterSeinClips` gained a lowercase-`Contains` aim/throw fallback on both outer wrapper and inner `TextureAnimation` names, mirroring the `PlayerStateReader` classifier. Sein-provenance (`s_seinClipRefs`) stays first, first-wins holds (calibrated exact names keep precedence), `TryResolveState`/`TryResolveExact`/`Resolve` semantics unchanged, no SEED-table edit, no LINQ (C# 5).
- G-03-3: every `TrackPlayerEvent` in `PlayerEventCore` sits outside any `IsAnimVerbose` gate — `fase=enviado`, `fase=recebido`, and `motivo=tipo-desconhecido` always Record + LogInfo; handler-exception drop counter unchanged (already ungated). `SetVerboseLogging` now sets the `AnimVerbose` entry as well as `ShowNetworkLogs` (null-guarded like `IsAnimVerbose`). `VerboseEvent` partial-skip helper stays gated. No publish call on any receive path (D-07 intact).
- Task 4 loop closed per AGENTS.md: client `build.ps1` SUCCESS 0 warnings (`OriCoopBepInEx.dll` 100,864 B), server Release 0 warnings 0 errors, SmokeProbe full suite `SMOKE_OK` including `PASS player-event` (byte-identical relay, no echo, drop-old, no SysAck — wire path regression-locked, packet 19 frozen 37 B untouched). Binaries deployed to `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\` (plugins DLL 100,864 B + `Server\` exe 152,064 B / dll 107,008 B / pdb, sizes match build outputs; no `OriDE.exe`/server process held a lock). `docs/operations.md` pilot pre-reqs now state default-config telemetry + working in-game toggle; `docs/protocol.md` untouched (no contract change).

## Task Commits

Each task was committed atomically:

1. **Task 1: effects independent of clip resolve (G-03-1, G-03-2)** — `33c2334` (feat, 1 file).
2. **Task 2: AimThrow substring fallback (G-03-1, G-03-2)** — `ed9901d` (feat, 1 file, +31).
3. **Task 3: ungate transitions/counters + fix toggle (G-03-3)** — `b8e5ea2` (feat, 2 files).
4. **Task 4: dual build + deploy + probe + docs** — `6df46b2` (chore, 1 file).
5. **Task 5: human 2-client pilot (C1–C6)** — BLOCKING checkpoint, returned to orchestrator, not attempted in-game.

**Plan metadata:** this SUMMARY commit (see below).

## Files Created/Modified

- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — MODIFIED: `PlaySpiritFlameVisual` effects-first reorder + ungated clip-miss/clip-hit lines (fail-closed, no `SetAnimation` on null path).
- `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` — MODIFIED: aim/throw substring fallback in `RegisterSeinClips` (outer + inner, provenance-first, first-wins).
- `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs` — MODIFIED: all three decision lines + all `TrackPlayerEvent` calls hoisted outside `IsAnimVerbose`.
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` — MODIFIED: `SetVerboseLogging` also writes `_animVerbose`.
- `docs/operations.md` — MODIFIED: pilot pre-reqs note default-config EVENT telemetry + working toggle (behavior-observed change only).

## Decisions Made

- Effects-first (rather than clip-first with fall-through spawns): a single ordering eliminates the whole class — no future early-return can reintroduce the choke point. Rationale: the spawns are individually guarded and side-effect-free on failure, so unconditional execution is safe.
- Substring fallback scoped to AimThrow only (not a general classifier in the registry): the diagnosed deterministic miss is the attack clip; generalizing would risk mis-mapping unrelated clips into wrong states. The reader stays the general classifier.
- Ungated lines use `LogInfo` (BepInEx) consistent with prior verbose lines, not `Debug.Log` — same sink the pilot checklist already reads (`LogOutput.log`).

## Deviations from Plan

None - plan executed exactly as written. (Task 3's puppet ungating was folded into the task-1 edit since both touch the same two EVENT lines; verified the final state satisfies both tasks' acceptance criteria.)

## Issues Encountered

- PowerShell (this shell) has no `tail` and `build.ps1` lives under `src/OriCoopPlus/OriCoopBepInEx/`, not repo root — ran with the full relative path. No impact.
- `git diff --stat` against pre-plan base shows only the 5 plan files; `.planning/` untracked scaffolding (`config.json`, `debug/`, plan files) intentionally left uncommitted.

## Threat Flags

None beyond the plan's `<threat_model>`: no wire-code change (T-03-04-01 accepted, probe re-verified); ungated lines fire once per discrete shot at D-12 no-throttle rate with ring-200 + 3 s summary unchanged (T-03-04-02 mitigated); spawn implementations untouched — keep-list strip, inactive-clone discipline, pooled mesh-only fake, scene-level SFX (T-03-04-03 mitigated); log lines carry only sender id + kind + motive tokens (T-03-04-04 accepted); no package installs (T-03-04-SC).

## Known Stubs

No placeholder stubs in the changed code (no TODO/FIXME/placeholder/empty-value flow added). Best-effort misses (particle/clip keyword miss → silent skip or motivo line) are specified fail-closed behavior, diagnosable in the pilot — not stubs.

## Build & Deploy Record

- `build.ps1` (via `src\OriCoopPlus\OriCoopBepInEx\build.ps1`): SUCCESS, `OriCoopBepInEx.dll` 100,864 B, 0 warnings (post task 1–3).
- `dotnet build OriCoopDedicatedServer.csproj --configuration Release`: SUCCESS, 0 warnings, 0 errors (exe 152,064 B / dll 107,008 B).
- `dotnet run SmokeProbe -- --port 7779 --test all`: `SMOKE_OK`, all 15 cases PASS including `PASS player-event (B recebeu evento de A intacto; sem eco; drop-old ok; sem SysAck)`.
- Deployed 2026-10-06 ~15:15 UTC-3 with no locking processes: plugin DLL → `...\Ori DE\BepInEx\plugins\` (100,864 B), server exe/dll/pdb → `...\Ori DE\Server\` (sizes match). `D:\SteamLibrary\...` does not exist on this machine.
- `docs/protocol.md` unchanged — reason: no contract change (37 B body, field order, flags 0, header-only identity all preserved).

## Code-Read Gates (acceptance verification)

- Effects (`SpawnMuzzleParticle`/`PlayTransientShotSound`/`SpawnFakeProjectile`) textually above clip resolve in `PlaySpiritFlameVisual`; null-clip path contains no `SetAnimation` and no `Idle` reference.
- `PlayerEventCore.cs` contains zero `IsAnimVerbose` conditionals (only a comment mention); `grep IsAnimVerbose` hits only `RemotePlayerPuppet` legacy ANIM paths + `VerboseEvent` helper (intentionally still gated) + `OriCoopPlugin.IsAnimVerbose` definition.
- `SetVerboseLogging` writes both `ShowNetworkLogs` and `_animVerbose.Value` (null-guarded).
- No `SendPlayerEvent`/`Publish` call on any receive path (`OnPlayerEventReceived` → `HandlePlayerEvent` only).

## User Setup Required

None - no external service configuration. Outstanding manual step (blocking checkpoint, not setup): the 2-client in-game pilot C1–C6 per the plan's task 5 — fire Spirit Flame on A → attack clip + particle + sound + straight fake bolt on B's puppet and reverse; C3 no-echo decidable from `LogOutput.log` with DEFAULT config; report pass/fail per check plus shots/s + `EvRecv/EvApplied/EvDropped` under spam.

## Next Phase Readiness

- Code-complete for the gap closure; the only remaining item is human verification (task 5 checkpoint).
- If the pilot still shows `motivo=clip-desconhecido` on every shot, the F8 `DumpCatalog` exact-name calibration remains available as a compatible tightening (first-wins preserves it) — not a rework.
- If the pilot shows effects missing despite `fase=recebido/aplicado` lines, the per-effect `aplicado=parcial motivo=sem-*` lines (still verbose-gated by design) identify which spawn failed — toggle verbose in-game and re-run.

## Self-Check: PASSED

- All 5 modified files FOUND on disk; `git diff 801e505..HEAD --stat` shows exactly the 5 plan files (+83/-46).
- Commits `33c2334`, `ed9901d`, `b8e5ea2`, `6df46b2` verified in `git log` (plus `801e505` pre-existing diagnosis commit as base).
- No unintended deletions; no `TODO/FIXME/placeholder` in new code; no package installs.

---
*Phase: 03-player-event-core*
*Completed: 2026-10-06*
