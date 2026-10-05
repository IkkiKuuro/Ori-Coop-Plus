---
phase: 01-anim-sync
plan: "02"
subsystem: multiplayer-animation
tags: [unity-5.3, csharp-5, sprite-animation, catalog]

# Dependency graph
requires:
  - phase: 01-anim-sync plan 01
    provides: "Resolve() com fallback por estado + pacote 18 com hash exato"
provides:
  - Dicionário explícito nome→estado (12 estados) sem heurística de substring
  - Dump F8 do catálogo em runtime
  - Histerese de sender (RunEnter 0.6 / RunExit 0.3, ápice preserva estado aéreo)
affects: [01-04 (viewer mostra transições do novo Resolve)]

actuals:
  tokens: 3100
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns: ["s_nameToState explícito com aliases SEED a calibrar via dump"]

key-files:
  created:
    - docs/anim-catalog.md
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs
    - src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs
    - src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs

key-decisions:
  - "Aliases SEED best-effort com fail-closed: miss nunca vira sprite aleatório"
  - "Constantes de histerese em AnimationSyncData (lado sender), não no puppet — desvio de localização registrado"

patterns-established:
  - "Dump via F8 + tabela em docs para calibrar nomes exatos de clipes"

requirements-completed: []

coverage:
  - id: D1
    description: "Resolve sem substring; 12 estados mapeados; dump F8 lista clipes"
    verification:
      - kind: other
        ref: "build.ps1: SUCESSO 71168 bytes; Contains(\"idle\") ausente no Registry"
        status: pass
    human_judgment: false
  - id: D2
    description: "Nomes exatos confirmados contra o jogo e aliases calibrados"
    verification: []
    human_judgment: true
    rationale: "Requer F8 em jogo e comparação com docs/anim-catalog.md"

duration: 25min
completed: 2026-10-05
status: complete
---

# Phase 01 (anim-sync) Plan 02 Summary

**Dicionário explícito de 12 estados com dump F8 e histerese de sender; build limpo**

## Performance

- **Tasks:** 3/3 complete — **Commits:** `56a9768` (feat) — Build: 71168 bytes

## Accomplishments

- `s_nameToState` (49 aliases SEED) + `s_stateClips`; substring removida;
  `Resolve` desconhecido retorna null (fail-closed)
- `AnimationSyncData.DeriveState` com 4 args + `RunEnterSpeed`/`RunExitSpeed`;
  `PlayerStateReader` rastreia último estado derivado
- `F8` despeja `[ANIM-DUMP]`; `docs/anim-catalog.md` criado e indexado

## Deviations from Plan

Localização das constantes: `RunEnterSpeed`/`RunExitSpeed`/`ConfirmDelaySec`
em `AnimationSyncData` (onde `DeriveState` usa) em vez de
`RemotePlayerPuppet` como dizia o aceite — mesma calibragem, lugar correto.
Sem impacto funcional.

## Issues Encountered

- Nomes reais dos clipes seguem **a confirmar** via dump (cobertura D2 humana).

## Next Phase Readiness

- Pronto para plano 03 (remoção do legado) e 04 (diagnóstico)
- Pendente humano: rodada T2 da bateria (`docs/anim-test-battery.md`)

---
*Phase: 01-anim-sync*
*Completed: 2026-10-05*
