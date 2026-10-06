---
status: diagnosed
phase: 03-player-event-core
source: [03-VERIFICATION.md]
started: 2026-10-06
updated: 2026-10-06
---

## Current Test

[testing complete]

## Tests

### 1. C1 - A atira, B ve (docs/operations.md checklist 03-03)
expected: Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao
result: issue
reported: "fail; nenhum projétil ou som aparece, e o projétil está sem o bloom padrão dele"
severity: major

### 2. C2 - B atira, A ve (simetria do relay)
expected: Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'
result: issue
reported: "fail, ninguém vê nada de tiro"
severity: major

### 3. C3 - sem eco (ambos os LogOutput.log)
expected: fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual
result: issue
reported: "prints de ambos os logs mostram apenas linhas [EVENT] SpiritFlame detected (deteccao local); nenhuma linha fase=recebido/aplicado visivel em nenhum dos lados"
severity: major

### 4. C4 - spam sob movimento + campos de observacao D-12
expected: Movimento suave sob rajadas; anotar tiros/s + EvRecv/EvApplied/EvDropped para calibrar throttle futuro
result: pass
note: "spam pros lados funciona bem; animacao nao e tao precisa para spam (observacao — imprecisao por tiro sob rajada e esperada em unreliable sem throttle, D-09/D-12)"

### 5. C5 - tipo desconhecido segura pose (exige injecao)
expected: Puppet mantem ultima anim, nunca Idle generico
result: pass

### 6. C6 - desconexao/reconexao limpa
expected: Puppet some e respawna no lugar certo; eventos voltam a replicar sem reiniciar o servidor
result: pass

## Summary

total: 6
passed: 3
issues: 3
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-03-1
  truth: "Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao"
  status: failed
  reason: "User reported: fail; nenhum projétil ou som aparece, e o projétil está sem o bloom padrão dele"
  severity: major
  test: 1
  root_cause: "PlaySpiritFlameVisual aborts all four effects at the AimThrow clip-null gate (RemotePlayerPuppet.cs:419-437 returns before particle/sound/projectile) because AnimationRegistry populates s_stateClips by EXACT full-name match against uncalibrated SEED aliases while the snapshot classifier uses SUBSTRING match — TryResolveState(AimThrow) deterministically misses (F8 dump calibration never ran)"
  artifacts:
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs"
      issue: "clip-null early return at lines 418-437 suppresses particle/sound/projectile — single choke point"
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs"
      issue: "alias population at lines 458-480 uses exact-match against uncalibrated SEED aliases; reader uses substring Contains at PlayerStateReader.cs:255-259"
  missing:
    - "Spawn particle + transient sound + fake projectile independently of clip resolve (clip becomes best-effort)"
    - "Fix AimThrow alias population: substring match like the reader, or F8 DumpCatalog-pinned exact name"
  debug_session: .planning/debug/player-event-delivery.md
- gap_id: G-03-2
  truth: "Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'"
  status: failed
  reason: "User reported: fail, ninguém vê nada de tiro"
  severity: major
  test: 2
  root_cause: "Same as G-03-1: clip-null gate suppresses all effects on the receiving puppet in both directions; delivery path itself verified correct hop-by-hop (patch fires, send envelope identical to working snapshot path, probe-proven relay, validated case-19 receive, working main-thread marshal)"
  artifacts:
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs"
      issue: "clip-null early return suppresses all effects symmetrically"
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs"
      issue: "exact-match alias population misses real attack-clip name"
  missing:
    - "Same fix as G-03-1 (shared choke point, one fix covers both directions)"
  debug_session: .planning/debug/player-event-delivery.md
- gap_id: G-03-3
  truth: "fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual"
  status: failed
  reason: "User evidence: ambos os logs mostram apenas deteccao local [EVENT] SpiritFlame detected; nenhuma linha recebido/aplicado visivel — eventos podem nao estar chegando (envio ou relay)"
  severity: major
  test: 3
  root_cause: "Verbose-gating artifact, not a delivery failure: every transition line (enviado/recebido/aplicado) and all EvRecv/EvApplied/EvDropped counters sit inside if (IsAnimVerbose()) (cfg-file-only, default false), and the in-game verbose toggle writes the write-only dead flag ShowNetworkLogs (zero readers, OriCoopPlugin.cs:92/238). Absence of recebido/aplicado lines proves nothing about delivery; hop-by-hop code review found no deterministic wire breaker"
  artifacts:
    - path: "src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs"
      issue: "transition lines at 58/72/96 inside IsAnimVerbose gate"
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs"
      issue: "counters/transitions at 431/443/880 verbose-gated"
    - path: "src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs"
      issue: "SetVerboseLogging writes dead flag ShowNetworkLogs; should also set _animVerbose"
  missing:
    - "Ungate EVENT transition lines + TrackPlayerEvent counters (or honor ShowNetworkLogs in the gate)"
    - "Fix SetVerboseLogging to also set _animVerbose so the in-game toggle works"
    - "Discriminating check: AnimVerbose=true in cfg on both clients should show fase=recebido on remote (delivery works) + motivo=clip-desconhecido (gate P1 confirmed)"
  debug_session: .planning/debug/player-event-delivery.md
