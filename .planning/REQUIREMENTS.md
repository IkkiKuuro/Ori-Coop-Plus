# Ori Coop Plus — Requirements

Derived from `docs/` (no PRDs ingested — all entries marked derived, to be
hardened into acceptance criteria per phase).

## REQ-01 — Remote player presence and movement (derived from architecture, protocol)

Remote players appear as visual puppets that track position/velocity with
interpolation and snap on teleport (> 15 u). Status: delivered (phases 01–03).

## REQ-02 — Movement animation sync without loop or random sprites (derived from anim-sync-CONTEXT, anim-catalog)

Puppet reproduces the local player's `ActionVisualState` (enum authority,
1 byte on the wire); unknown clips keep the last animation; hysteresis +
confirmation delay kill Run↔Idle oscillation. Status: delivered (phase 01).

## REQ-03 — Versioned unreliable transport with reliable criticals (derived from protocol, server-rewrite-CONTEXT)

24-byte envelope, handshake, seq-ordered unreliable player state, ACK+retry
critical packets, immediate relay without tick aggregation. Status: delivered
(phase 02).

## REQ-04 — Player event core + Spirit Flame pilot (derived from player-event-core-CONTEXT)

Central event core with registry/bus, Harmony detection (no polling), local-only
publish, unreliable sequenced PLAYER_EVENT 19, visual fidelity with zero
gameplay effect, fail-closed on unknown. Status: delivered (phase 03).

## REQ-05 — World sync application: levers, orb doors, global events (derived from world-sync, game-entry-map)

Apply received SYNC_LEVER 11 / SYNC_DOOR 12 / SYNC_WORLDEVENT 14 locally
(IsSyncing guard, dedup, receiver never republishes). Status: open — client
currently validates and discards (relay without application).

## REQ-06 — Enemy/boss sync (derived from game-entry-map, animation-enemies-damage-data)

Synchronize enemy state (at minimum position + animation via EntitySync;
health/death/phases TBD with a stable enemy identifier). Status: open.

## REQ-07 — Damage/health/energy sync (derived from animation-enemies-damage-data)

Sync player damage, death/respawn, health/energy. Status: open — no
damage/save entry points in `src/` today.

## REQ-08 — Save/progress sync: pickups, inventory, abilities (derived from game-entry-map)

Sync pickups, inventory, XP/skill points, shared abilities beyond relay.
Status: open (relay without application for abilities).
