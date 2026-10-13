# Ori Coop Plus — Project

Cooperative multiplayer mod for **Ori and the Blind Forest: Definitive Edition**
(Windows/Steam, Unity 5.3.2f1, 32-bit): a BepInEx 5.x client plugin
(.NET Framework 3.5 / C# 5) plus a dedicated server (.NET 8). Test runs inside a
save with a controllable Ori; menus/prologue with Naru are out of flow.

## Components

- `src/OriCoopPlus/OriCoopBepInEx` — client plugin: `Plugin/OriCoopPlugin`,
  `Client/` (RemotePlayerManager, lightweight RemotePuppetFactory, RemotePlayerPuppet,
  RemoteVisualController, AnimationRegistry), `UI/`, `Diagnostics/`,
  `Domain/`, `Networking/NetworkService`, `Patches/` (SeinCharacterPatch,
  PlayerStateReader, SpiritFlamePatch, SeinInputPatch, FrustumCullingBypassPatch,
  AnimationPrewarmPatch). C# 5 only (no `$""`, no `?.`, max 4 Action params).
- `src/OriCoopPlus/OriCoopShared` — client-server contract (packet IDs, sync structs).
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer` — new async core only:
  `Net/Transport` (EnvelopeCodec 24 B + UdpTransport), `Net/Session`
  (token sessions, 10 s timeout), `Net/Reliability` (SysAck + retry),
  `Net/Game` (ServerBoot, GameHandlers, ConfigStore, DummyBot, PlayerStateRelay),
  `Net/Diagnostics`, `Net/Game/Commands`. Old `Core/` + `Game/` paths removed
  2026-10-05 — do not recreate `Server.cs`/`Client.cs`/`Packet.cs`.
- `scripts/` — PowerShell RE/inspection/build utilities (see `scripts/README.md`).

## Decisions

None locked by ADR (no ADRs ingested). Working conventions (from docs, proposed):

- Protocol changes always move client + server together; `docs/protocol.md`
  updated in the same change; packet IDs never reused.
- Puppet visuals are visual-only: no damage, collision, or local game-state
  mutation from remote events; unknown clips/events fail closed (keep last).
- `docs/` stays current on every relevant change (rule in `AGENTS.md`); deploy
  after every successful build goes straight into the Ori DE install.

## Constraints

- Network: 24-byte UDP envelope (magic `0x4F43`, v2), 3-way handshake
  Hello 100 / Welcome 101 / Confirm 102; critical packets reliable (SysAck 103,
  retry 250 ms x3); PLAYER_STATE 18 / PLAYER_EVENT 19 unreliable. Default port
  7777, 1–10 players, manual IPv4, no LAN discovery. One-way break with old
  builds is authorized.
- Vanilla facts (static audit 2026-10-10, `Assembly-CSharp.dll` SHA-256
  `18e70a4c96f093b6dc0ece83ad2842fd45a130196f17a13008d202e7b69c1d88`):
  no `Boss` / `SpiritWell` / `Breakable` classes; `AbilityType` has 44 values;
  `MoonGuid(A,B,C,D)` is the stable ID for doors/levers/events.
- Unity object model: no UGUI/OnGUI in native UI; `TextureAnimation*` clips are
  shared ScriptableObject assets (exact resolve by hash/name is safe; state
  fallback must use Sein-provenance clips only).

## Context pointers

- Start: `docs/README.md`, `docs/architecture.md`, `docs/game-and-mod.md`.
- Before touching packets/sync: `docs/protocol.md`.
- Before patches/animations: `docs/bepinex-architecture.md`, `docs/anim-catalog.md`.
- Vanilla ground truth: `docs/game-entry-map.md`, `docs/animation-enemies-damage-data.md`.
- Ops: `docs/operations.md` (build, deploy, validation protocol).
- Synthesized intel: `.planning/intel/` + `.planning/INGEST-CONFLICTS.md`
  (ingest 2026-10-13, manifest `.planning/intel/ingest-manifest.yaml`; 4 docs
  excluded to break navigational cross-ref cycles).
