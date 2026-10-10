---
status: diagnosed
phase: 03-player-event-core
source: [03-VERIFICATION.md]
started: 2026-10-06
updated: 2026-10-06
---

## Current Test

number: R3-C1
name: C1 no espelho real (A atira, B ve)
expected: |
  Puppet de A em B com orbe seguidor visivel; a cada tiro: ShootAnimation no orbe + projetil real (LineRenderer, com bloom) em linha reta + som real + efeito de disparo, sem dano/colisao. Log de B: fase=recebido + aplicado=espelho-real por tiro.
awaiting: user response

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
issues: 5
pending: 0
skipped: 0
blocked: 0

## Re-test (gap fixes 03-04 deployed, DLL 100864B size+timestamp matched)

Deployed build verified current (build output == Ori DE plugin dir, 100864B, same timestamp) — failures below are real code issues, not a stale deploy.
- DELIVERY CONFIRMED: P2 log shows `fase=recebido` per shot, `EvRecv/EvApplied` moving (7/7, 5/5), `EvDropped: 0`. G-03-3 delivery half resolved; shooter-side no-echo check still pending.
- CLIP STILL UNKNOWN: `aplicado=manteve-atual motivo=clip-desconhecido` on every received event despite T2 substring fallback → real attack-clip name still unmatched.
- NOTHING RENDERS despite T1 clip-independence → effect spawn path itself broken/invisible, and `EvApplied` increments without any visible effect (counter semantics suspect).

## Re-test round 2 (2026-10-10, user resumed C1-C3)

- R2-T1 (C1 A→B): result: issue, reported: "fail" (no detail yet — effect/log lines pending)
- R2-T2 (C2 B→A): result: issue, reported: "fail para todos" (2026-10-10, detail pending)
- R2-T3 (C3 sem eco): result: issue, reported: "fail para todos" (2026-10-10, detail pending)
- R2 status: user will propose new test plan — awaiting proposal

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
  retest_2026_10_06: "DELIVERY CONFIRMED by P2 log (fase=recebido per shot, EvRecv/EvApplied moving, EvDropped 0) — observability half resolved. Kept open pending shooter-side log proving enviado-only / never recebido-aplicado on own shots."
  debug_session: .planning/debug/player-event-delivery.md
- gap_id: G-03-4
  truth: "Puppet remoto resolve o clipe de ataque do Spirit Flame (sem motivo=clip-desconhecido)"
  status: failed
  reason: "User evidence (re-test after 03-04): toda linha recebido vem com aplicado=manteve-atual motivo=clip-desconhecido — fallback substring de T2 nao cobre o nome real do clipe de ataque"
  severity: major
  test: 1
  root_cause: "Undiagnosed — needs investigation: real attack-clip name unknown; AnimationRegistry substring fallback (aim/throw) still misses. Either the clip has an unrelated name (needs F8 DumpCatalog on live client) or the lookup runs against the wrong collection"
  artifacts:
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs"
      issue: "AimThrow alias population still misses despite substring fallback"
  missing:
    - "Pin the real attack-clip name via F8 DumpCatalog during live play, or broaden matching"
  debug_session: .planning/debug/player-event-delivery.md
- gap_id: G-03-5
  truth: "Particula + som + projetil falso do Spirit Flame renderizam no puppet remoto mesmo sem clipe resolvido"
  status: failed
  reason: "User evidence (re-test after 03-04): nada aparece pra ninguem apesar de T1 (efeitos independentes do clipe); EvApplied incrementa sem efeito visivel"
  severity: blocker
  test: 1
  root_cause: "Undiagnosed — needs investigation: candidate causes are null particle prefab/sound clip on the puppet path, spawn at wrong position/layer, exception swallowed in the effect path, or whitelist stripping the spawned objects. EvApplied counting non-rendered events suggests counter semantics also wrong"
  artifacts:
    - path: "src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs"
      issue: "PlaySpiritFlameVisual effect path produces no visible output despite independence reorder"
  missing:
    - "Trace the effect spawn path: null-check particle/sound assets, verify spawn transform, check for swallowed exceptions, verify whitelist survival"
    - "Fix EvApplied semantics (should count rendered effects, not received events)"
  debug_session: .planning/debug/player-event-delivery.md

## Rework (espelho real do Sein) — 2026-10-10

Pivot fake→real (confirmado por inspecao binaria): abandona os fakes
artesanais (particula/som por keyword adivinhada, projetil quad de
scratch) em favor do espelho real do Sein — clone visual do orbe
(`Game.Characters.Ori`, estatico auto-registrado; `Game.Characters.Sein`
e o avatar do jogador) por puppet remoto + prefab real
`SpiritFlame.Projectile` instanciado visual-only
(`SpiritFlameProjectile`/dano removidos) + `ThrowSound` real via
`SoundProvider` + `ThrowEffectGameObject` real. Sem mudanca de fio
(packet 19 intacto), sem mudanca de servidor; `docs/protocol.md` intacto.

Gaps (status permanece `failed` nas linhas acima — nao alteradas; rework
em curso, nao aguardando diagnostico):

- gap_id: G-03-4
  status: failed
  in_fix: true
  fix_approach: "SeinVisualMirror + real prefab visual-only"
- gap_id: G-03-5
  status: failed
  in_fix: true
  fix_approach: "SeinVisualMirror + real prefab visual-only"

Re-testes R3 pendentes (detalhe em `docs/operations.md` § R3 — espelho
real do Sein):

- R3-C1 (C1 A→B no espelho real): issue (2026-10-10) — reported: "ok com ressalva, vemos a bola piscando no ataque, porém não vemos o projetil, o som, nem o VFX". Orb mirror + ShootAnimation OK; projectile/sound/VFX missing. Root cause CONFIRMED by log: `motivo=sem-prefab` every shot (GetComponent on Sein root always null — abilities are CharacterState, not scene MonoBehaviours) + fallback matched wrong clip (`aplicado=grenadeThrowDown` — grenade anim, not spirit flame).
- R3-C1 fix deployed (DLL 103424B, 2026-10-10): patch caches exact prefab from firing instance via reflection (NoteLocalPrefabs, sibling-class CS0039 → GetProperty CurrentSpiritFlame); mirror checks cache first + GetComponentsInChildren fallback; grenade excluded from AimThrow fallback (body holds pose fail-closed, orb ShootAnimation is the visible attack). C3 evidence in same log: enviado-only on shooter, recebido-only on remote, EvDropped 0.
- R3-C1 retest (2026-10-10, DLL 103424B): parcial — som + sync OK (`aplicado=espelho-real`), mas animacao/efeitos congelados e projetil nao viaja. Root cause: LineRenderer real e dirigido por UpdateLineRenderer a cada frame; com SpiritFlameProjectile destruido ele congela (world-space: mover o transform nao adianta). Item novo: tiro carregado (charge) com seus efeitos nao aparece — caminho de charge nao hookado (gap novo, ver G-03-6).
- R3-C1 fix 2 deployed (DLL 105472B, 2026-10-10): LineRenderers do clone desligados; quad viajante com MATERIAL real clonado (bloom) + scroll de textura no mover; Play() forcado nas particulas do ThrowEffect. Requer restart dos 2 clientes.
- R3-C1 retest 2 (2026-10-10, DLL 105472B): parcial — projetil solto + som OK, mas bolt invisivel (so o flash parado aparece) + bloom piscando forte. Teoria unificada: material de linha nao renderiza em quad estatico (bolt invisivel); flash do throw congela no brilho max (fade era do driver) — 1 piscada por tiro. Charge segue sem nada (G-03-6 aberto, nao mexido).
- R3-C1 fix 3 deployed (DLL 105472B rebuild, 2026-10-10): bolt = 1o LineRenderer NATIVO com segmento local (-0.4..0.9) + material clonado com scroll; demais linhas off; throw-effect com Play() + fade 0.35s (materiais isolados) + linha diag-bolt (mat/shader/lines) no log. Requer restart dos 2 clientes.
- R3-C1 retest 3: pending
- R3-C2 (C2 B→A, simetria): pending
- R3-C3 (C3 sem eco): pending

## Gaps (round 2)

- gap_id: G-03-6
  truth: "Tiro carregado (charge) do Spirit Flame replica som + VFX no puppet remoto"
  status: failed
  reason: "User reported (2026-10-10): tiro carregado com seus efeitos sonoros e visuais nao aparece"
  severity: major
  test: R3-C1
  root_cause: "Undiagnosed — charge usa outro metodo de habilidade (nao ThrowSpiritFlames); precisa mapear via inspecao binaria"
  artifacts: []
  missing:
    - "Mapear metodo de disparo carregado (REA) e hookar como novo evento ou flag no payload"
  debug_session: .planning/debug/player-event-delivery.md
