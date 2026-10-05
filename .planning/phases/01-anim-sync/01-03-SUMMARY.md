---
phase: 01-anim-sync
plan: "03"
subsystem: multiplayer-network
tags: [udp, protocol, csharp-5, dotnet-8]

# Dependency graph
requires:
  - phase: 01-anim-sync plan 01
    provides: "Pacote 18 writer/reader/relay e rota direta"
provides:
  - Remoção total do split POSITION/ANIM (sender, relay, receptor, inferência)
  - Envio on-change + heartbeat 400 ms
  - Dummy bot migrado para pacote 18
affects: [01-04 (HUD/idade usa Timestamp do pacote 18)]

actuals:
  tokens: 3400
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns: ["IDs 1-2 marcados LEGACY_REMOVED, nunca reutilizar"]

key-files:
  created: []
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
    - src/OriCoopPlus/OriCoopShared/PacketType.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/DummyManager.cs
    - docs/protocol.md

key-decisions:
  - "Dummy migrado para 18 (não previsto no plano, necessário para /dummy não quebrar)"
  - "Leitores legados do cliente removidos junto (tráfego só tem 18)"

patterns-established:
  - "On-change (estado/hash/pos epsilon 0.05) + heartbeat 400 ms como padrão de envio"

requirements-completed: []

coverage:
  - id: D1
    description: "Zero referências a pacotes 1/2 no caminho anim/posição; só pacote 18 trafega"
    verification:
      - kind: other
        ref: "build.ps1: SUCESSO 69632 bytes; grep confirma só LEGACY_REMOVED no cliente"
        status: pass
    human_judgment: false
  - id: D2
    description: "Servidor (relay + dummy) compila e opera só com pacote 18"
    verification:
      - kind: other
        ref: "dotnet build Release: 0 erros (9 warnings nulabilidade pré-existentes); smoke --auto porta 7779: Server started + módulo CARREGADO"
        status: pass
    human_judgment: false
  - id: D3
    description: "Heartbeat ~2.5 pkt/s parado, envio imediato em mudança, regressão OK"
    verification: []
    human_judgment: true
    rationale: "Requer 2 clientes em jogo (bateria T3)"

duration: 30min
completed: 2026-10-05
status: complete
---

# Phase 01 (anim-sync) Plan 03 Summary

**Legado POSITION/ANIM removido; on-change + heartbeat; dummy no pacote 18; cliente compila**

## Performance

- **Tasks:** 3/3 complete — **Commit:** `71ac357` (feat) — Build cliente: 69632 bytes

## Accomplishments

- `SendPlayerSnapshot`: só pacote 18, com gate on-change/heartbeat
  (`SamePosition` epsilon 0.05, 400 ms)
- `RemotePlayerManager`: `MergedState`, `_states`, inferência delta/dt e
  `looksLike*` excluídos; `HandleSnapshot` direto
- `OriCoopPlugin`: ramos `isAnimOnly` excluídos; `PacketType` 1/2 marcados
  `LEGACY_REMOVED`; `protocol.md` marca fragmentação como REMOVIDA
- `DummyManager.Tick` transmite pacote 18 (Idle + facing + nick)

## Deviations from Plan

Adição necessária: migração do dummy (plano não mencionava, mas remover o
relay POSITION sem migrar o bot quebraria `/dummy`). Sem scope creep.

## Issues Encountered

- Servidor não compilado aqui (sem SDK): `NetworkHandler`/`DummyManager`
  revisados à mão contra a API `Packet`; build do servidor é item da bateria.
- Commits sem prefixo de plano no grep; hashes acima.

## Next Phase Readiness

- Pronto para plano 04; par cliente+servidor precisa ser buildado junto na
  máquina com SDK antes do teste em jogo

---
*Phase: 01-anim-sync*
*Completed: 2026-10-05*
