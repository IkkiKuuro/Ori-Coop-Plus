# Ori Coop Plus — Roadmap

Bootstrapped 2026-10-13 via `/gsd-ingest-docs` (merge mode, 18 docs).
Phases 01–03 predate the roadmap; their directories under
`.planning/phases/` are the record. Coverage: REQ-01…REQ-04.

## Phase 01 — anim-sync: remote movement animation sync — complete

Evidence: `phases/01-anim-sync/01-RESEARCH.md`, `01-01…01-04-PLAN.md` +
`01-01…01-04-SUMMARY.md`. Puppet animation without standstill loop or random
sprites (REQ-02). No CONTEXT/VERIFICATION files — re-verify on next anim touch.

## Phase 02 — server-rewrite: dedicated server core rewrite — complete

Evidence: `phases/02-server-rewrite/02-CONTEXT.md`, `02-RESEARCH.md`,
`02-01…02-04-PLAN.md` + SUMMARYs, `02-VERIFICATION.md`. Async transport,
token sessions, reliability, immediate relay (REQ-03).

## Phase 03 — player-event-core: event core + Spirit Flame pilot — complete

Evidence: `phases/03-player-event-core/03-CONTEXT.md`, `03-RESEARCH.md`,
`03-PATTERNS.md`, `03-01…03-04-PLAN.md` + SUMMARYs, `03-REVIEW.md`,
`03-VALIDATION.md`, `03-UAT.md`, `03-VERIFICATION.md`. Core + pilot event
(REQ-04).

## Phase 04 — world-sync application (proposed)

Apply SYNC_LEVER / SYNC_DOOR / SYNC_WORLDEVENT locally with echo guards and
dedup (REQ-05). Spec: `docs/world-sync.md`.

## Phase 05 — enemy sync (proposed)

EntitySync coverage + stable enemy identity; health/death/phases as far as the
contracts allow (REQ-06). Spec: `docs/game-entry-map.md` § enemies.

## Phase 06 — damage/health/save sync (proposed)

Player damage, death/respawn, health/energy, pickups/inventory/abilities
application (REQ-07, REQ-08). Spec: `docs/animation-enemies-damage-data.md`.

## Backlog (not phases yet)

- Anim catalog exact-name calibration via in-game F8 dump (`docs/anim-catalog.md`).
- LAN discovery, internet play (explicitly out of scope so far).
- In-game 2-tab log viewer (`docs/anim-sync-CONTEXT.md` D-19).
