---
status: investigating
trigger: "UAT gap diagnosis — find_root_cause_only (do NOT fix; plan-phase --gaps handles fixes). 3 UAT gaps, one failure domain: Spirit Flame event delivery/visual (phase 03-player-event-core, tests C1/C2/C3)."
created: 2026-10-06T00:00:00Z
updated: 2026-10-06T00:00:00Z
---

## Current Focus

hypothesis: "PlaySpiritFlameVisual aborts everything at the AimThrow clip-null gate (RemotePlayerPuppet.cs:419-437) because AnimationRegistry populates s_stateClips by EXACT full-name match against uncalibrated SEED aliases, while the snapshot classifier uses SUBSTRING match — asymmetric, so TryResolveState(AimThrow) misses and particle+sound+projectile never spawn (C1/C2). C3 log absence is a verbose-gating artifact, not delivery proof."
test: "Read-only code tracing of full chain patch->core->send->relay->receive->dispatch->visual + asymmetry check reader-vs-registry + verbose-gate audit"
expecting: "Single render-gate root cause for C1/C2; C3 explained by IsAnimVerbose gating + dead ShowNetworkLogs flag; delivery path verified correct hop-by-hop, probe-proven"
next_action: "Return ROOT CAUSE FOUND (diagnose-only mode, no fix)"

## Symptoms

expected: "C1: Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao. C2: mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'. C3: fase=enviado so no atirador, fase=recebido/aplicado so no remoto."
actual: "C1: nenhum projétil ou som aparece (+ mencao secundaria 'projétil sem o bloom padrão'). C2: ninguém vê nada de tiro. C3: ambos os logs mostram apenas deteccao local [EVENT] SpiritFlame detected; nenhuma linha recebido/aplicado visivel. (C4 spam-movement smooth passou; C6 reconnect passou.)"
errors: "none reported (no crashes)"
reproduction: "2-client live session, fire Spirit Flame on either client"
started: "discovered during UAT of phase 03-player-event-core (2026-10-06)"

## Eliminated

- hypothesis: "Harmony patch targets wrong method / never fires"
  evidence: "Both clients' logs show per-shot '[EVENT] SpiritFlame detected dir=(±1,0,0)' — Postfix on SeinSpiritFlameAbility.ThrowSpiritFlames fires; owner filter passes. Wave-0 metadata probe confirmed target."
  timestamp: 2026-10-06T00:00:00Z
- hypothesis: "Server relay drops packet 19 (parse/gate/dispatch)"
  evidence: "GameHandlers.TryParse/Build/HandlePlayerEventAsync + IsGamePacket + DispatchAsync reviewed correct; SmokeProbe packet-19 case passes (byte-identical, no-echo, drop-old same+older, no SysAck). No deterministic breaker in repo server path."
  timestamp: 2026-10-06T00:00:00Z
- hypothesis: "Client send path no-ops (IsConnected/BindTransport) or receive branch misparses"
  evidence: "SendPlayerEvent/SendEventSystem envelope identical to working snapshot path except disjoint seq counter; case-19 receive validated field-by-field (33/32/20/8 ExpectRemaining arithmetic correct); movement sync over same socket/thread/queue works, proving connectivity, session, main-thread marshal. No deterministic breaker found."
  timestamp: 2026-10-06T00:00:00Z
- hypothesis: "Event seq-domain drop (first events look 'old')"
  evidence: "_eventSendSeq starts 0->1, _lastEventSeq/EventRelayGate accept-on-first-seen, both cleared on Welcome/session-leave. First event seq=1 accepted on all three sites."
  timestamp: 2026-10-06T00:00:00Z
- hypothesis: "Snapshot hysteresis path should have shown the attack clip anyway"
  evidence: "ApplySnapshotDirect ConfirmDelaySec=0.15s hysteresis swallows sub-150ms attack states (pending overwritten when shooter returns to Idle/Run) — by design; the event path is the only carrier. Consistent with C1."
  timestamp: 2026-10-06T00:00:00Z

## Evidence

- timestamp: 2026-10-06T00:00:00Z
  checked: "RemotePlayerPuppet.PlaySpiritFlameVisual (lines 406-455)"
  found: "Lines 418-437: if TryResolveState(AimThrow) fails (even after RefreshFromSein), method RETURNS before SpawnMuzzleParticle/PlayTransientShotSound/SpawnFakeProjectile. One clip-lookup miss suppresses ALL FOUR effects. Only mark is a verbose-gated 'motivo=clip-desconhecido' line."
  implication: "Single choke point explains total visual silence (C1+C2) with zero default-visible logging."
- timestamp: 2026-10-06T00:00:00Z
  checked: "AnimationRegistry.RegisterClips (lines 458-480) vs PlayerStateReader.TryDeriveFromName (lines 255-259)"
  found: "ASYMMETRY: registry populates s_stateClips by EXACT full-name TryGetValue against SEED aliases {'aim','oriaim','throw','orithrow'} (comment: 'aliases best-effort — calibrar nomes exatos via dump F8'); reader classifies AimThrow by SUBSTRING Contains('aim')/Contains('throw'). Real attack clip name almost certainly equals none of the four exact aliases, and the F8 calibration dump never ran before UAT."
  implication: "TryResolveState(AimThrow) very likely misses deterministically on every event -> clip gate always aborts -> C1/C2."
- timestamp: 2026-10-06T00:00:00Z
  checked: "All [EVENT] transition logging + counters gating"
  found: "enviado (PlayerEventCore:58-64), recebido (:72-79), unknown-kind (:96-103), aplicado=recebido (RemotePlayerPuppet:443-447), partial-reason (:878-886), AND TrackPlayerEvent counters all inside if(IsAnimVerbose()). Default AnimVerbose=false, cfg-file-only. In-game SetVerboseLogging sets ShowNetworkLogs (OriCoopPlugin.cs:238), which is WRITE-ONLY (zero readers) — the UI verbose toggle produces no EVENT lines."
  implication: "C3 'no recebido/aplicado lines' is uninterpretable without confirmed cfg edit+restart on both clients; absence proves nothing about delivery. C3 is a test-instrumentation artifact, not independent delivery-break proof."
- timestamp: 2026-10-06T00:00:00Z
  checked: "Full delivery chain code review (patch, core, NetworkService send+receive, plugin wiring, manager, server host+handlers)"
  found: "Every hop correct in repo; server hop additionally probe-proven. No deterministic total breaker anywhere on the wire path. Client echo-guard (senderId != _assignedId) correct; no-publish-back discipline intact."
  implication: "Delivery break is unlikely; residual fallback is deployed-binary skew (stale pre-19 server drops 19 silently), unverifiable read-only."
- timestamp: 2026-10-06T00:00:00Z
  checked: "C1 secondary remark 'projétil sem o bloom padrão'"
  found: "Ambiguous: scratch-built fake has no bloom by construction (Sprites/Default orange bolt), so a brief fake sighting would look exactly 'without bloom'; equally could be the local real shot. Cannot weight; does not contradict the clip-gate hypothesis (intermittent resolve or single-client observation possible)."
  implication: "Kept as secondary observation only; primary remains total delivery-to-render failure per C2/C3."

## Resolution

root_cause: "P1 (probable, C1+C2): PlaySpiritFlameVisual clip-null early-return (RemotePlayerPuppet.cs:419-437) — TryResolveState(AimThrow) misses because s_stateClips is populated by EXACT name match against uncalibrated SEED aliases (AnimationRegistry.cs:458-480) while the reader uses SUBSTRING match (PlayerStateReader.cs:255-259); F8 dump calibration never ran. One miss aborts clip+particle+sound+projectile. P2 (C3): verbose-gating artifact — all transition lines/counters require IsAnimVerbose (cfg-file-only, default false); in-game verbose toggle writes dead flag ShowNetworkLogs. P3 (residual fallback): stale deployed pre-19 server binary silently dropping 19 — unverifiable read-only."
fix: "NOT APPLIED (diagnose-only). Suggested direction: spawn particle/sound/fake BEFORE (independent of) the clip resolve, make clip best-effort; fix alias population (substring match like reader, or pin real name via F8 DumpCatalog); ungate EVENT lines/counters (or honor ShowNetworkLogs) so C3 is observable; re-run pilot with AnimVerbose=true in cfg both clients and expect remote 'fase=recebido' + 'motivo=clip-desconhecido' as hypothesis confirmation."
verification: "Read-only chain trace + probe-record review; in-game confirmation still required (verbose ON: look for recebido without aplicado on remote)."
files_changed: []
