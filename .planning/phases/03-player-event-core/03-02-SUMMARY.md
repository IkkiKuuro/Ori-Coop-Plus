---
phase: 03-player-event-core
plan: 02
subsystem: netcode
tags: [udp, unreliable-sequenced, spirit-flame, player-event, visual-only, seq-domain, csharp5, bepindex, dedicated-server]

# Dependency graph
requires:
  - phase: 03-player-event-core plan 01
    provides: [PLAYER_EVENT=19 frozen 37B contract, PlayerEventCore bus+registry, SpiritFlamePatch detector, thin patch->clip path, shared seq domain tracer]
provides:
  - Full-fidelity visual-only remote shots (clip + muzzle particle + transient sound + pooled fake projectile, D-13/D-14)
  - Separate event sequence domain on all three sites (client send counter, client receive dict, server EventRelayGate)
  - Per-field case-19 length validation + fail-closed unknowns end to end (D-15)
  - Hardened packet-19 protocol doc + deployed pilot binaries
affects: [03-03-observability-probe, future-event-categories, entities-world-sync]

# Actuals (#2632) — pairs with the plan's `estimate` to calibrate future estimates.
actuals:
  tokens: 9100    # chars/4 over the realized diff (36433 chars)
  tasks: 3        # tasks completed
  commits: 4      # 3 task commits + 1 summary commit

# Tech tracking
tech-stack:
  added: []
  patterns: [visual-only transient construction (inactive-clone + positive keep-list strip + auto-destroy), pooled fake projectile (mesh+renderer only, Update straight-line mover, lifetime/range despawn), per-domain unreliable seq (separate send counter + per-sender last-seen + dedicated relay gate)]

key-files:
  created: []
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs
    - docs/protocol.md

key-decisions:
  - "Muzzle particle + shot clip resolved at runtime by keyword scan over the local Sein (shoot/flame/spirit/muzzle/projectile/fire/shot), keyword-only with fail-closed silence when absent — no blind fallback to unrelated VFX/SFX"
  - "Fake projectile is a scratch-built quad (MeshFilter+MeshRenderer only, Sprites/Default bolt material with puppet-material fallback), pooled cap 8, speed 18 u/s, 0.8 s / 14 u despawn — never a cloned gameplay prefab"
  - "Server event gate is a nested EventRelayGate with its own per-sender dict (not a second PlayerStateRelay, which would still share Session.LastRecvSeq) + Forget on session leave"
  - "Event body carries no nick by frozen-contract design (D-11), so the manager has nothing to nickname-update on the event path — documented, not implemented"

patterns-established:
  - "Visual-only transient = build/clone inactive -> positive keep-list strip (Transform/ParticleSystem/Renderer/MeshFilter + own helpers, no gameplay type names) -> world-position -> activate -> auto-destroy/pool-return"
  - "Separate unreliable seq domain = dedicated send counter + dedicated per-sender last-seen dict with wrap-safe unsigned-diff drop-old + Welcome/session-leave clearing, documented at each of the three sites"

requirements-completed: [D-01, D-04, D-07, D-09, D-11, D-12, D-13, D-14, D-15]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "Remote Spirit Flame shows attack clip + muzzle particle + shot sound + straight-line fake projectile, zero gameplay effect (D-13/D-14)"
    requirement: "D-13"
    verification:
      - kind: other
        ref: "build.ps1 -> OriCoopBepInEx.dll 99840 bytes, SUCCESS, 0 warnings"
        status: pass
    human_judgment: true
    rationale: "Visual fidelity (particle presence, correct sound, straight bolt trajectory, no damage numbers) requires the 2-client in-game run; automation proves compile + visual-only construction only"
  - id: D2
    description: "Shot bursts never suppress movement snapshots: disjoint seq domains on client send, client receive, server relay (D-09 class)"
    requirement: "D-09"
    verification:
      - kind: other
        ref: "build.ps1 SUCCESS + dotnet build OriCoopDedicatedServer Release 0 warnings 0 errors; code review of three disjoint sites"
        status: pass
    human_judgment: true
    rationale: "Smooth puppet movement under hold-fire spam requires the 2-client in-game run; automation proves both ends compile with disjoint gates"
  - id: D3
    description: "Unknown event kinds keep last valid animation with a reason log, never generic Idle (D-15)"
    requirement: "D-15"
    verification:
      - kind: other
        ref: "PlayerEventCore.DispatchLocal fail-closed branch (plan 01, unchanged) + puppet clip-null fail-closed return; no new kind dispatch added"
        status: pass
    human_judgment: true
    rationale: "Pose-hold on crafted unknown kind requires injecting a probe packet during a live 2-client session"
  - id: D4
    description: "Only Spirit Flame ships; no Stomp/Bash/Dash/Glide/VFX-generic/SFX-generic/death paths added (D-01, D-04)"
    requirement: "D-01"
    verification:
      - kind: other
        ref: "grep PlayerEventKind usages: only SpiritFlame=1 registered/handled; puppet visual entry reachable only via SpiritFlame handler"
        status: pass
    human_judgment: false
  - id: D5
    description: "Protocol doc packet-19 subsection states unreliable-sequenced flags-0, no-throttle, separate domain, byte-identical relay, frozen 37B order; both binaries built and deployed with matching outputs"
    verification:
      - kind: other
        ref: "build.ps1 SUCCESS (99840B) + dotnet build Release 0 warnings; deployed DLL 99840B + server exe/dll matching build outputs, no processes running"
        status: pass
    human_judgment: false

# Metrics
duration: ~40min
completed: 2026-10-06
status: complete
---

# Phase 03 Plan 02: Visual Hardening + Wire Hardening Summary

**Remote Spirit Flame shots now play the attack clip plus a muzzle particle plus the game's shot sound plus a pooled straight-line fake projectile — all visual-only — over a fully disjoint event sequence domain, with docs, builds, and deployed binaries closed per AGENTS.md.**

## Performance

- **Duration:** ~40min
- **Started:** 2026-10-06 ~12:55 UTC-3
- **Completed:** 2026-10-06
- **Tasks:** 3/3
- **Files modified:** 5 (0 created, 5 modified)

## Accomplishments

- Puppet visual full-fidelity: `PlaySpiritFlameVisual` extended from clip-only to clip + muzzle particle (visual-only inactive-clone of a keyword-matched Sein `ParticleSystem`, stripped to renderers + animator, auto-destroyed) + transient scene-level one-shot sound (game's shot clip via keyword scan + static cache, never under the puppet) + pooled fake projectile (scratch quad, mesh + renderer only, `FakeFlameMover` straight-line `Update` motion, 0.8 s / 14 u despawn, pool cap 8).
- Visual-only guarantees from birth (T-03-05): new puppet code references zero damage/collider/rigidbody types — stripping is a positive keep-list (`Transform`/`ParticleSystem`/`Renderer`/`MeshFilter` + own helpers), everything else destroyed generically.
- Separate event sequence domain on all three sites: client `_eventSendSeq` + `SendEventSystem` (send), `_lastEventSeq` + `IsNewerThanLastEvent` with Welcome-clear (receive), server nested `EventRelayGate` with per-sender dict + `Forget` on session leave (relay). Snapshot counter/gate/dict untouched.
- Case-19 hardened to per-field `ExpectRemaining` validation (33/32/20/8 before kind/dir/origin/ts), `InvalidDataException` with packet-19 message contained by the per-packet receive loop; no-throttle send preserved (every shot sends immediately, D-12); unknown kinds fail closed end to end (plan-01 `DispatchLocal` + clip-null path, no new kind dispatch).
- `docs/protocol.md` packet-19 subsection now states no-throttle + separate-domain design + full-fidelity visual list, with real-physics future marked a confirmar; both builds green with zero warnings; binaries deployed to the Ori DE install dirs with matching sizes.

## Task Commits

Each task was committed atomically:

1. **Task 1: Full visual-only reproduction (D-13, D-14)** — `5d0e472` (feat, 2 files, +526/-7).
2. **Task 2: Separate event sequence domain + fail-closed hardening (D-09 class, D-15)** — `5731005` (feat, 2 files, +135/-14).
3. **Task 3: Protocol doc touch-up + dual build + deploy** — `3026a97` (docs, 1 file, +13/-4).

**Plan metadata:** this SUMMARY commit (see below).

## Files Created/Modified

- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — MODIFIED: `FakeFlameMover` (straight-line visual mover + pool return), `TransientEventCleanup` (auto-destroy), full `PlaySpiritFlameVisual` (clip + `SpawnMuzzleParticle` + `PlayTransientShotSound` + `SpawnFakeProjectile`), pool (`TakePooledFake`/`ReturnPooledFake`/`BuildFakeProjectile`, cap 8), quad mesh + bolt material builders, positive keep-list `StripToVisualOnly`, keyword resolvers (`PickFlameParticle`/`ResolveShotClip` + static cache), `FindLocalSeinObject`, verbose helper.
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` — MODIFIED: explicit never-publish receive discipline comment (D-07) + D-11 no-nick note; routing code itself already lazy-created at origin (plan 01) and needed no change.
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — MODIFIED: `_eventSendSeq`/`NextEventSeq`/`SendEventSystem` (send domain), `_lastEventSeq`/`IsNewerThanLastEvent` + Welcome-clear (receive domain), per-field case-19 `ExpectRemaining`, three-site domain comments.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs` — MODIFIED: nested `EventRelayGate` (own per-sender dict, wrap-safe, locked) + `_eventRelay` field, `HandlePlayerEventAsync` routed through it with updated doc comment, `OnSessionLeft` forgets the sender.
- `docs/protocol.md` — MODIFIED: packet-19 subsection (no-throttle D-12, separate-domain design, full visual list, real-physics a-confirmar, 2-client checklist).

## Decisions Made

- Muzzle particle + shot clip resolved at runtime by keyword scan over the local Sein's `ParticleSystem`s / `AudioSource` clips (`shoot/flame/spirit/muzzle/projectile/fire/shot`), keyword-only with fail-closed silence when absent. Rationale: exact prefab/clip names were never pinned (Wave-0 found `ShootEffect`/`ShootingSound` strings only); a blind first-found fallback could clone unrelated ambient VFX or play a jump sound as a gunshot — worse than silence. Verbose reason logs (`sem-efeito`, `sem-clip`) make the miss diagnosable in the 2-client run.
- Fake projectile is a scratch-built quad (manual `Mesh`, `Sprites/Default` orange bolt material with puppet-sprite fallback) instead of a cloned game prefab. Rationale: no verified inert projectile prefab exists (Pitfall 5); scratch construction is visual-only by definition and needs no strip trust. Pool cap 8, speed 18 u/s, 0.8 s / 14 u despawn are pilot-tuned, noted for calibration in the 2-client run.
- Server event gate is a new nested `EventRelayGate` with its own `Dictionary<int,uint>` rather than a second `PlayerStateRelay` instance. Rationale: `PlayerStateRelay.ShouldRelay` reads/writes `Session.LastRecvSeq`, so a second instance would still share the snapshot domain — the dict-based gate is the actual separation. `Forget` on session leave bounds growth (ids are never reused per protocol, but leave-cleanup is cheap insurance).
- `TakePooledFake`/`BuildFakeProjectile` take the puppet's own renderer as material donor (instance path) so the bolt material always resolves even if `Sprites/Default` lookup fails.
- Event body carries no nick by frozen-contract design (D-11 header-only identity), so the manager's "nickname update when present" is vacuous — documented in a comment rather than implemented. No frozen-body change for a cosmetic field.
- `docs/README.md` index untouched (no new page), `docs/operations.md` untouched (pilot checklist belongs to plan 03's observability scope).

## Deviations from Plan

None - plan executed exactly as written. (Implementation-shape choices above are agent's-discretion details inside the plan's explicit allowances, not deviations.)

## Issues Encountered

- PowerShell (this shell) rejects `&&` command chaining — re-ran commit/build commands with `;` separators. No impact on the work.
- `Measure-Object` used instead of `wc -c` for the diff char count (same reason). Actuals: 36433 chars → ~9100 tokens.

## Threat Flags

None — no security-relevant surface beyond the plan's `<threat_model>`: case-19 per-field length checks before every read (T-03-01), `IsReady`-gated relay inherited from the host path with sender-scoped rendering (T-03-02), disjoint wrap-safe drop-old on all three sites (T-03-03), no throttle per accepted D-12 with zero per-packet server state beyond the bounded per-sender last-seen (T-03-04), visual-only-from-birth fake with positive keep-list strip (T-03-05). No new packages (T-03-SC).

## Known Stubs

No placeholder stubs in the new code (grep for TODO/FIXME/placeholder empty). Deliberately best-effort visual resolutions (particle/clip keyword miss → silent skip with verbose reason) are fail-closed behavior, not stubs — the 2-client run confirms or tunes them.

## Build & Deploy Record

- `build.ps1`: SUCCESS, `OriCoopBepInEx.dll` 99,840 bytes, 0 warnings (Task 1, 2, and 3 runs).
- `dotnet build OriCoopDedicatedServer.csproj --configuration Release`: SUCCESS, 0 warnings, 0 errors (Task 2 and 3 runs).
- Deployed (no `OriDE.exe` / server processes running, no lock handling needed): plugin DLL → `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\BepInEx\plugins\` (99,840 B, 12:54, matches build output); server exe (152,064 B) + dll (107,008 B) + pdb → `...\Ori DE\Server\` (12:54, match build outputs). `serverconfig.json`/bats untouched.

## User Setup Required

None - no external service configuration required. Manual step outstanding (not setup): the 2-client in-game run — fire Spirit Flame on A → clip + particle + sound + straight bolt on B's puppet, silence on B's publish path, smooth movement under hold-fire spam, pose-hold on unknown-kind probe, disconnect/reconnect clean (coverage D1–D3, `human_judgment: true`).

## Next Phase Readiness

- Pilot complete and deployed; plan 03 (observability + SmokeProbe case + operations checklist) builds directly on the disjoint domains (event counters per domain) without rework.
- Blocker: none for plan 03 code work. The 2-client manual verification (D1–D3) should happen before or alongside plan 03's calibration, since it tunes what this plan built (particle/bolt visibility, spam smoothness).
- Watch item: statically cached quad mesh / bolt material / shot clip do not survive scene transitions as managed-static refs — `TakePooledFake` revalidates mesh/material per take and `ResolveShotClip` re-resolves while uncached, but a scene change mid-session may log one silent re-resolve. Note for the entities phase if fakes become persistent.

## Self-Check: PASSED

- All 5 modified files FOUND on disk; `git diff --stat 2cf0d80..HEAD` shows exactly the 5 plan files (+674/-25).
- Commits `5d0e472` (task 1), `5731005` (task 2), `3026a97` (task 3) verified in `git log`.
- No unintended deletions (`git diff --diff-filter=D` clean for task commits).
- No `TODO/FIXME/placeholder` stubs in new files; no new packages; no `Publish` call on the manager receive path (word appears only in the discipline comment).

---
*Phase: 03-player-event-core*
*Completed: 2026-10-06*
