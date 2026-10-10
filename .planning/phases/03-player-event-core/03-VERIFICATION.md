---
phase: 03-player-event-core
verified: 2026-10-06T16:30:00Z
status: human_needed
score: 6/12 must-haves verified
behavior_unverified: 6
overrides_applied: 0
behavior_unverified_items:
  - truth: "Local Spirit Flame shot produces exactly one unreliable packet 19 per shot, visible in LogOutput.log with the [EVENT] detect line, only on the shooter client (D-06, D-07)"
    test: "C1/C3 — fire Spirit Flame on client A with 2 LAN clients; inspect both LogOutput.log files"
    expected: "Shooter log shows [EVENT] SpiritFlame detected per shot (one line per shot); remote log shows no detect line and no fase=enviado"
    why_human: "No test can fire the Harmony Postfix inside the running game; emission count and log lines only exist during live play"
  - truth: "Remote client renders the sender puppet attack clip for the event and never publishes anything back (D-07 no-echo, thin visual)"
    test: "C1/C2/C3 — fire A→B and B→A; watch remote puppet and both logs"
    expected: "Remote puppet plays attack clip per shot; enviado lines only on shooter, recebido/aplicado only on remote; shooter never plays its own event visual"
    why_human: "Clip rendering happens inside Unity on the puppet; no automated probe can observe sprite/animation output (no-echo half is code+probe proven, rendering half is not)"
  - truth: "Remote Spirit Flame shows the same attack clip plus muzzle particle plus shot sound plus a straight-line visual projectile, with zero damage, collision, or local state change (D-13)"
    test: "C1/C2 — side-by-side fire; watch + listen on remote; confirm local game state unchanged"
    expected: "Clip + particle + audible shot + straight bolt on remote; no damage numbers, no world change, no FPS collapse"
    why_human: "Particle presence, correct sound identity, bolt trajectory, and absence of gameplay side effects require eye/ear judgment in the running game"
  - truth: "Shot bursts never suppress or freeze movement snapshots on either end (separate sequence domains, D-09 class)"
    test: "C4 — hold fire while strafing on both clients; watch remote puppets"
    expected: "Puppet movement stays smooth between shots; game does not stutter under spam"
    why_human: "Smoothness under spam is a feel/frame observation; the disjoint-domain mechanism is code-verified but its observable effect needs live traffic"
  - truth: "Unknown event kinds keep the last valid animation and log a reason; nothing falls back to generic Idle (D-15)"
    test: "C5 — inject a packet-19 with a kind outside the catalog into a live 2-client session"
    expected: "Puppet holds last pose; verbose log shows aplicado=manteve-atual motivo=tipo-desconhecido; never generic Idle"
    why_human: "Requires a crafted packet inside a live game session; no injection tooling exists in the repo, so this needs a manual/temporary harness plus eye verification"
  - truth: "Event transitions (sent, received, applied, dropped with motive) are observable in the ring log and throttled summary, mirroring the anim vocabulary (D-12 flood observation)"
    test: "During C4 spam with AnimVerbose on, read the [NET-METRICS] line and [EVENT] ring lines (F9 or LogOutput.log)"
    expected: "EvRecv/EvApplied move with the burst; EvDropped stays 0 on clean traffic; ring shows fase=enviado/recebido lines with motives"
    why_human: "Counter movement only happens under real shots in the running game; hooks are code-verified but unexercised without live play"
human_verification:
  - test: "C1 — A atira, B ve (docs/operations.md checklist 03-03)"
    expected: "Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao"
    why_human: "Unity-side rendering; no automated probe can cover it"
  - test: "C2 — B atira, A ve (simetria do relay)"
    expected: "Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'"
    why_human: "Unity-side rendering; no automated probe can cover it"
  - test: "C3 — sem eco (ambos os LogOutput.log)"
    expected: "fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual"
    why_human: "Requires live 2-client log comparison"
  - test: "C4 — spam sob movimento + campos de observacao D-12"
    expected: "Movimento suave sob rajadas; anotar tiros/s + EvRecv/EvApplied/EvDropped para calibrar throttle futuro"
    why_human: "Feel/frame observation plus manual counter readout"
  - test: "C5 — tipo desconhecido segura pose (exige injecao)"
    expected: "Puppet mantem ultima anim, nunca Idle generico"
    why_human: "No packet-injection tooling in repo; needs a temporary harness in a live session"
  - test: "C6 — desconexao/reconexao limpa"
    expected: "Puppet some e respawna no lugar certo; eventos voltam a replicar sem reiniciar o servidor"
    why_human: "Live session lifecycle; cannot be automated without game clients"
---

# Phase 03: Player Event Core Verification Report

**Phase Goal:** PlayerEventCore + Spirit Flame pilot — client event core (Harmony-detected on SeinSpiritFlameAbility.ThrowSpiritFlames, local-only publish via m_sein filter) → new packet ID 19 unreliable-sequenced (frozen 37B: marker+kind+dir+origin+timestamp) → server blind relay → puppet visual-only reproduction (clip+particle+sound, no damage, fake projectile), fail-closed on unknown events, extensible registry for future categories.
**Verified:** 2026-10-06T16:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Local Spirit Flame shot → exactly one unreliable packet 19 per shot, `[EVENT]` detect line, only on shooter (D-06, D-07) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Patch + filter + publish path present and wired (see Artifacts/Links); live-fire emission count needs C1/C3 |
| 2 | Packet 19 wire format (24B envelope + 37B body) relayed byte-identical to ready peers except sender (D-09, D-10, D-11) | ✓ VERIFIED | Verifier re-ran `SmokeProbe --test all` → `PASS player-event (B recebeu evento de A intacto; sem eco; drop-old ok; sem SysAck)` inside `SMOKE_OK`; 37B layout byte-consistent across client/server/probe codecs |
| 3 | Remote renders sender puppet attack clip, never publishes back (D-07 no-echo) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | No-echo proven 3 layers (server excludes sender — probe PASS; `RaisePlayerEvent` suppresses own id, NetworkService.cs:1106-1113; zero publish calls on receive path by grep); clip rendering needs C1/C2 |
| 4 | docs/protocol.md documents packet 19 with frozen field order in the same change (D-10 one-way rule) | ✓ VERIFIED | protocol.md: ID-table row 19 (line 26) + `PLAYER_EVENT (19)` subsection with frozen offset table 0/4/5/17/29 (lines 243-271) + unreliable-class + no-throttle + separate-domain statements, all in the phase commits |
| 5 | Remote shows clip + particle + sound + straight-line projectile, zero gameplay effect (D-13) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | `PlaySpiritFlameVisual` (RemotePlayerPuppet.cs:406-455) extends clip → muzzle particle → transient sound → pooled fake; zero damage/collider/rigidbody/damage refs in new code by grep; visual fidelity needs C1/C2 |
| 6 | Fake projectile visual-only from birth; payload/handler/spawn ready for future real physics (D-14) | ✓ VERIFIED | Fake is scratch-built quad (MeshFilter+MeshRenderer only, `BuildFakeProjectile`), pooled cap 8, straight-line `FakeFlameMover`, auto-despawn; spawn uses event origin+direction; payload shape (dir+origin+ts) already carries what physics needs |
| 7 | Shot bursts never suppress movement snapshots — separate sequence domains (D-09 class) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Three disjoint sites code-verified: client `_eventSendSeq`/`SendEventSystem` (send), `_lastEventSeq`/`IsNewerThanLastEvent` + Welcome-clear (receive), server nested `EventRelayGate` + `Forget` on leave (relay); snapshot counter/gate untouched; smoothness effect needs C4 |
| 8 | Unknown kinds keep last valid animation + reason log, never generic Idle (D-15) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | `DispatchLocal` fail-closed branch (PlayerEventCore.cs:94-103: keep-last + `motivo=tipo-desconhecido`, never publishes) + puppet clip-null fail-closed return (RemotePlayerPuppet.cs:429-437); live pose-hold needs C5 injection |
| 9 | Only Spirit Flame ships; no Stomp/Bash/Dash/Glide/VFX-generic/SFX-generic/death paths (D-01, D-04) | ✓ VERIFIED | `PlayerEventKind` has only `Unknown=0, SpiritFlame=1`; sole `RegisterHandler` call site registers SpiritFlame (RemotePlayerManager.cs:16); Stomp/Bash/etc. grep hits are pre-existing phase-1 anim/snapshot code, not event code |
| 10 | Event transitions observable in ring log + throttled summary (D-12 flood observation) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | `TrackPlayerEvent(kind, applied)` + `EvRecv/EvApplied/EvDropped/EvKind` in 3s `[NET-METRICS]` summary (ReplicationObservability.cs:95-122) + `[EVENT]` ring vocabulary; hooks called on publish/dispatch outcomes; counter movement needs live spam (C4) |
| 11 | SmokeProbe case proves byte-identical relay, no echo, drop-old, no SysAck (D-08, D-09) | ✓ VERIFIED | `TestPlayerEvent` (SmokeProbe Program.cs:547-664: fixed 37B payload, identical-bytes, no-echo, same-seq + older-seq drop, no-SysAck-103) wired into `--test all` and `relay` group; verifier re-run: 17 PASS incl. `PASS player-event`, `SMOKE_OK`, exit 0 |
| 12 | docs/operations.md holds repeatable 2-client checklist; docs stay indexed (D-10/D-13 gate) | ✓ VERIFIED | operations.md pilot section (lines 475-522): pre-gate commands, prerequisites, C1–C6, D-12 observation fields, round result `a confirmar`; `docs/README.md` untouched and consistent (no new page added) |

**Score:** 6/12 truths verified (6 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `OriCoopShared/PacketType.cs` | `PLAYER_EVENT = 19` | ✓ VERIFIED | Line 36; next free integer, banned IDs untouched |
| `OriCoopShared/PlayerEventProtocol.cs` | Shared 37B consts (offsets 4/5/17/29) | ✓ VERIFIED | Consts-only, compiles C#5 + .NET8; offsets sum 4+1+24+8=37 |
| `OriCoopBepInEx/Events/PlayerEventKind.cs` | Byte enum Unknown=0, SpiritFlame=1, append-only | ✓ VERIFIED | 14 lines, substantive + referenced by core/manager |
| `OriCoopBepInEx/Events/SpiritFlameEventData.cs` | DTO struct, no Unity types | ✓ VERIFIED | Mutable fields + explicit ctor; Unity conversion at edges |
| `OriCoopBepInEx/Events/PlayerEventCore.cs` | `PublishSpiritFlame` + `DispatchLocal` + kind registry, never publishes on receive | ✓ VERIFIED | Bus + `Dictionary<byte,PlayerEventHandler>`; fail-closed unknown branch; grep: only publish call is the patch (correct direction) |
| `OriCoopBepInEx/Patches/SpiritFlamePatch.cs` | Harmony Postfix on Wave-0-confirmed target, local-only filter | ✓ VERIFIED | `[HarmonyPatch(typeof(SeinSpiritFlameAbility), "ThrowSpiritFlames")]`, typed `___m_sein` + `ResolveOwner` fallback, `owner != Game.Characters.Sein → return`, facing-based dir, unconditional temp detect log, zero network code |
| `NetworkService.cs` SendPlayerEvent + case-19 branch | flags=0 unreliable, separate seq domain, per-field validation | ✓ VERIFIED | `SendEventSystem(...,0)` via dedicated `_eventSendSeq`; case-19: marker check + `IsNewerThanLastEvent` + per-field `ExpectRemaining` (33/32/20/8) + main-thread marshal; Welcome-clear of `_lastEventSeq` |
| `Domain/INetworkService.cs` | Send decl + received event | ✓ VERIFIED | `SendPlayerEvent` (line 41) + `PlayerEventReceived` event (line 23) |
| `Plugin/OriCoopPlugin.cs` | BindTransport + OnPlayerEventReceived 4-site wiring | ✓ VERIFIED | Subscribe+Bind at lines 142-143 and 286-287, unsubscribe+unbind at 168-169; `OnPlayerEventReceived` enqueues to main thread → `HandlePlayerEvent` (lines 326-336) |
| `Client/RemotePlayerManager.cs` | `HandlePlayerEvent` lazy-puppet-at-origin + no-echo discipline | ✓ VERIFIED | Sender guard, lazy `CreatePuppet` + `SnapTo`, `DispatchLocal` delegation; zero publish calls on receive path (grep) |
| `Client/RemotePlayerPuppet.cs` | `PlaySpiritFlameVisual` clip+particle+sound+fake | ✓ VERIFIED | Clip resolve via `AimThrow` + fail-closed null return + `IsPlayingClip` guard; `SpawnMuzzleParticle` (inactive-clone + keep-list strip), `PlayTransientShotSound` (scene-level AudioSource, never under puppet), pooled `FakeFlameMover` straight-line fake |
| `Net/Game/GameHandlers.cs` | `HandlePlayerEventAsync` + TryParse/Build pair, dedicated gate | ✓ VERIFIED | Parse-then-gate order (`TryParsePlayerEvent` → `_eventRelay.ShouldRelay` → `RelayUnreliableAsync` original bytes); `IsGamePacket` + `DispatchAsync` case 19; `OnSessionLeft` forgets sender |
| `Net/NetServerHost.cs` | Route + 19 absent from critical list | ✓ VERIFIED | `IsCriticalPacket` lists chat/config/teleport/syncs/skill/color/disconnect only; doc comment names 19 as never-critical (unreliable) |
| `Diagnostics/ReplicationObservability.cs` | Event counters + `TrackPlayerEvent` + summary | ✓ VERIFIED | Counters + track entry + `EvRecv/EvApplied/EvDropped/EvKind` in throttled summary; ring capacity/lock discipline unchanged |
| `SmokeProbe/Program.cs` | Packet-19 relay case in `--test all` | ✓ VERIFIED | `TestPlayerEvent` + `BuildPlayerEventBody` (frozen 37B) + `EVENT_OK`/`event` mode; header comment updated |
| `docs/protocol.md` | Packet-19 row + frozen field-order table | ✓ VERIFIED | See truth 4 |
| `docs/operations.md` | Pilot checklist C1–C6 + D-12 fields | ✓ VERIFIED | See truth 12 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| SpiritFlamePatch | PlayerEventCore.PublishSpiritFlame | Postfix call, facing dir + Sein-position origin + timestamp | WIRED | Patch line 57; zero network code in patch (D-05) |
| PlayerEventCore | NetworkService.SendPlayerEvent | `INetworkService` transport bound by plugin | WIRED | Core line 49 → `SendSystem(id, body, 0)` flags=0 (line 476); no-throttle, no `SendReliable` on path (D-09, D-12) |
| Client UDP | GameHandlers case 19 | Envelope 24B, `IsGamePacket` + `DispatchAsync` single landing zone | WIRED | Inherits host `IsReady` + token/endpoint gating |
| GameHandlers | Ready peers except sender | `RelayUnreliableAsync(originalDatagram, sender, ct)` byte-verbatim, no `fromId` stamp | WIRED | Probe asserts identical bytes + no echo (D-11) |
| NetworkService case-19 | RemotePlayerManager.HandlePlayerEvent | `RaisePlayerEvent` (own-id suppressed) → `PlayerEventReceived` → `OnPlayerEventReceived` → `EnqueueMainThread` | WIRED | Network-thread → Unity-main-thread crossing marshaled; own events never dispatched locally |
| RemotePlayerManager | PlayerEventCore.DispatchLocal → puppet clip | Registry lookup → `HandleSpiritFlame` → `PlaySpiritFlameVisual` | WIRED | Only SpiritFlame kind has a handler; unknown kinds hit fail-closed (D-15, D-01/D-04) |
| Publish/dispatch sites | TrackPlayerEvent counters → ring + 3s summary | `fase=enviado/recebido` lines + EvRecv/EvApplied/EvDropped | WIRED | Hooks on publish, known-kind receive, exception-drop, unknown-drop |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| SpiritFlamePatch | direction (±X facing), origin (Sein position), timestamp | Live `SeinCharacter` (`FaceLeft`, `transform.position`) at fire instant | Yes — read from game objects per shot | ✓ FLOWING |
| Packet-19 body | kind + dir(3f) + origin(3f) + ts(8B) = 37B | Client `SendPlayerEvent` sequential writes | Yes — frozen offsets match server/probe codecs | ✓ FLOWING |
| Server relay | original datagram bytes | Re-emitted verbatim, sender header preserved | Yes — probe byte-identical assertion PASS | ✓ FLOWING |
| Puppet visual | origin + direction | Event payload → `SnapTo`/particle/projectile spawn | Yes — code path complete to visual spawn; live rendering pending human | ✓ FLOWING (code) |
| Envelope clientId | sender identity | Header only, never in body | Yes — sole identity; body carries no sender id (D-11) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Server Release builds clean | `dotnet build OriCoopDedicatedServer.csproj -c Release` (verifier re-run) | `0 Aviso(s) 0 Erro(s)` | ✓ PASS |
| Client builds C#5-clean | `build.ps1` (verifier re-run) | `SUCESSO ... 100864 bytes`, 0 warnings | ✓ PASS |
| Full automated battery | `SmokeProbe -- --port 7785 --test all` (verifier re-run) | 17 PASS incl. `PASS player-event`, `SMOKE_OK`, exit 0 | ✓ PASS |
| Deployed copies match build outputs | sizes/timestamps check | Plugin DLL 100864B in `<ORI_DIR>\BepInEx\plugins\`; server exe 152064B + dll 107008B in `<ORI_DIR>\Server\`, matching | ✓ PASS |
| Phase commits present | `git log --oneline` | All 10 task commits (`3f63fc7`, `504f37f`, `5d0e472`, `5731005`, `3026a97`, `83e9e60`, `dbe0330`, `1191dc2` + plan docs) | ✓ PASS |

### Probe Execution

No conventional `scripts/*/tests/probe-*.sh` probes exist in this repo. The phase-declared probe is the SmokeProbe packet-19 case, executed above via `dotnet run --project SmokeProbe -- --test all` (also `--test relay`/`--test event` shapes verified in 03-03 summary). Result: PASS.

### Requirements Coverage

Phase defined by `03-CONTEXT.md` decisions D-01..D-15 (no ROADMAP.md/REQUIREMENTS.md in repo — CONTEXT.md is the canonical record per its header). Every ID accounted for:

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| D-01 | 03-02 | One category per phase | ✓ SATISFIED | Only `SpiritFlame=1` registered/handled; no other event kinds |
| D-02 | 03-01 | Phase 3 = core + pilot | ✓ SATISFIED | `PlayerEventCore` bus+registry + end-to-end Spirit Flame path shipped |
| D-03 | 03-01 | Pilot = Spirit Flame | ✓ SATISFIED | Detector targets `SeinSpiritFlameAbility.ThrowSpiritFlames` (Wave-0 metadata-verified) |
| D-04 | 03-02 | Only Spirit Flame; rest are future phases; core extensible | ✓ SATISFIED | Append-only enum + registry; future categories add kind+handler without rework |
| D-05 | 03-01 | Central core; patches only detect | ✓ SATISFIED | Patch has zero network code; core owns transport calls |
| D-06 | 03-01 | Harmony detection, no polling | ✓ SATISFIED (code) / NEEDS HUMAN (live fire) | Postfix detector present; per-shot firing needs C1 |
| D-07 | 03-01/03-02 | Local-only publish; remote never republishes | ✓ SATISFIED (code+probe) / NEEDS HUMAN (live) | `m_sein` filter + 3-layer no-echo (probe + suppress + zero publish on receive); live log proof needs C3 |
| D-08 | 03-03 | Reuse MP framing conventions as reference | ✓ SATISFIED | Marker-led body, header identity, blind relay cloned; dead SKILL/SYNC IDs untouched, never resurrected |
| D-09 | 03-01/02/03 | Unreliable-sequenced pilot class | ✓ SATISFIED (wire) / NEEDS HUMAN (smoothness) | flags=0, out of critical list, probe no-SysAck, disjoint seq domains; C4 for feel |
| D-10 | 03-01/03-03 | New ID (19), one-way, same-change docs, same-build | ✓ SATISFIED | ID 19 approved+shipped both ends; protocol.md same-change; deployed pair |
| D-11 | 03-01/03-02 | Minimal frozen payload, header-only identity | ✓ SATISFIED | 37B frozen order, no sender id in body, relay verbatim |
| D-12 | 03-02/03-03 | No throttle in pilot; flood becomes measurable | ✓ SATISFIED (code) / NEEDS HUMAN (readout) | No rate limiter on path (grep: throttle only in comments); EvRecv/EvApplied/EvDropped + observation fields land; readings need C4 |
| D-13 | 03-02/03-03 | Full visual fidelity, zero gameplay effect | NEEDS HUMAN | Code verified visual-only by construction; fidelity judgment needs C1/C2 |
| D-14 | 03-02 | Fake projectile now, real physics later | ✓ SATISFIED | Scratch visual-only fake + spawn/payload shape ready for entities phase |
| D-15 | 03-02/03-03 | Fail-closed on unknown events | ✓ SATISFIED (code) / NEEDS HUMAN (live) | `DispatchLocal` + clip-null fail-closed paths; pose-hold proof needs C5 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | `TODO/FIXME/XXX/placeholder/coming-soon` in `Events/`, `SpiritFlamePatch.cs`, new puppet/probe code | — | None — zero hits |
| — | — | Damage/collider/rigidbody in event/puppet-new code | — | None — zero hits (only pre-existing UI/teleport/factory-strip references) |
| — | — | `Publish`/republish on remote receive path | — | None — zero calls (discipline comment only) |
| 03-REVIEW.md | WR-01..WR-08, IN-01..03 | Advisory code-review warnings (unconditional detect log, verbose-gated counters, NaN validation, gate-before-validate ordering, probe single-read negatives, subclass-override check, bare catches, dead pool-cap branch, unlocked counters, mirror-facing, comment precision) | ⚠️ Warning (advisory, non-blocking per reviewer verdict) | None ship-blocking — reviewer: "no blockers … safe to proceed". WR-02 (counters blind in default runs) and WR-05 (probe negative-assertion drain loops) flagged fix-first for a follow-up; none invalidate the phase goal |

### Human Verification Required

The 2-client in-game pilot run (C1–C6 checklist in `docs/operations.md`, round result marked **a confirmar**). No automated probe can cover Unity-side rendering — this is the expected human gate, not a gap:

1. **C1 — A atira, B ve:** fire Spirit Flame on A side-by-side; confirm remote puppet clip + particle + shot sound + straight fake bolt, no damage/collision. *Why human: Unity rendering + audio judgment.*
2. **C2 — B atira, A ve:** repeat opposite direction (relay symmetry). *Why human: same as C1.*
3. **C3 — sem eco:** compare both `LogOutput.log`: `fase=enviado` only on shooter, `recebido/aplicado` only on remote. *Why human: live 2-client log comparison.*
4. **C4 — spam sob movimento + D-12 fields:** hold fire while strafing both; puppet stays smooth; record shots/s + `EvRecv/EvApplied/EvDropped`. *Why human: feel observation + manual counter readout for future throttle calibration.*
5. **C5 — tipo desconhecido segura pose:** inject packet-19 with out-of-catalog kind in a live session (no injection tooling in repo — needs temp harness); expect pose hold, never generic Idle. *Why human: crafted packet in live game + eye verification.*
6. **C6 — desconexao/reconexao limpa:** leave with one client and rejoin; puppet despawns/respawns correctly, events resume without server restart. *Why human: live session lifecycle.*

Record date, DLL bytes, and per-check pass/fail in `docs/operations.md` when executed.

### Gaps Summary

No gaps. All 16 required artifacts exist, are substantive, and are wired end-to-end (patch → core → packet 19 → blind relay → puppet visual, with main-thread marshaling and 3-layer no-echo); the wire contract is automation-locked by the new SmokeProbe case inside a verifier-re-run `SMOKE_OK` (17 PASS); both builds are verifier-re-run green with deployed copies matching; docs (protocol row + frozen table, operations C1–C6 checklist) are in place; scope discipline holds (Spirit Flame only). The 6 behavior-unverified truths are code-complete items awaiting the known 2-client human gate — explicitly expected per phase planning (`a confirmar` round result), not implementation gaps. Prior phase-02 battery shows no regression (re-run `SMOKE_OK` includes all pre-existing cases).

---

_Verified: 2026-10-06T16:30:00Z_
_Verifier: the agent (gsd-verifier)_
