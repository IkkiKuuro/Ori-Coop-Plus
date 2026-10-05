---
phase: 01-anim-sync
plan: "01"
subsystem: multiplayer-animation
tags: [unity-5.3, csharp-5, udp, bepinex, harmony, sprite-animation]

# Dependency graph
requires: []
provides:
  - Pacote 18 PLAYER_STATE de ponta a ponta (sender, relay, receptor)
  - Autoridade do enum ActionVisualState com histerese de 0.15 s no puppet
  - Log verboso [ANIM] atrás de config AnimVerbose
affects: [01-02 (dicionário explícito usa o mesmo Resolve), 01-03 (remove legado mantido aqui), 01-04 (ring buffer alimenta-se do LogAnimTransition)]

# Actuals
actuals:
  tokens: 5200
  tasks: 5
  commits: 5

# Tech tracking
tech-stack:
  added: []
  patterns: ["Pacote 18: campos na ordem playerId,pos,state,flags,animHash,speedX,speedY,nick", "Fail-closed: clipe desconhecido mantém anim atual", "IsPlayerStatePacket como discriminador de rota no manager"]

key-files:
  created: []
  modified:
    - src/OriCoopPlus/OriCoopShared/PacketType.cs
    - src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs
    - src/OriCoopPlus/OriCoopBepInEx/Domain/PlayerState.cs
    - src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
    - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
    - docs/protocol.md

key-decisions:
  - "IsPlayerStatePacket em vez de heurística de detecção: rota direta sem ambiguidade com pacotes legados"
  - "DeriveState com fallback aéreo=Falling (nunca Idle no ar)"
  - "TEMP-TRACER: grounded heurístico mantido com comentário, troca no plano 02/03 se grounded real for localizado"

patterns-established:
  - "uint via int com unchecked em ambas as pontas (Packet do servidor não tem Write(uint))"
  - "string nick sempre por último no pacote para leitura tolerante"

requirements-completed: []

coverage:
  - id: D1
    description: "Pacote 18 trafega pos+estado+flags+hash+velocidade+nick do sender ao receptor via relay do servidor"
    verification:
      - kind: other
        ref: "build.ps1 cliente: SUCESSO 69120 bytes (24 arquivos); servidor: revisão manual (sem SDK)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Puppet remoto assenta em Idle ao parar e toca Run ao correr, sem sprite aleatório, em partida com 2 clientes"
    verification: []
    human_judgment: true
    rationale: "Requer jogo rodando com 2 clientes e observação visual; sem harness automatizado no repo"
  - id: D3
    description: "Log [ANIM] aparece com AnimVerbose=true e silencia com padrão false"
    verification: []
    human_judgment: true
    rationale: "Requer jogo rodando para ler LogOutput.log"

# Metrics
duration: 40min
completed: 2026-10-05
status: complete
---

# Phase 01 (anim-sync) Plan 01 Summary

**Pacote 18 PLAYER_STATE de ponta a ponta com autoridade do enum e histerese de confirmação no puppet; cliente compila limpo**

## Performance

- **Duration:** ~40 min
- **Tasks:** 5/5 complete
- **Files modified:** 10
- **Commits:** 5 atômicos

## Accomplishments

- `PacketType.PLAYER_STATE = 18` + `AnimationSyncData.DeriveState` no contrato compartilhado
- Sender preenche estado/grounded/velocidade/hash reais e envia pacote 18 (legado mantido até plano 03)
- Servidor retransmite pacote 18 sem fundir nem inferir
- `RemotePlayerManager.HandleDirectState` (rota direta) + `RemotePlayerPuppet.ApplySnapshotDirect` com debounce 0.15 s e fail-closed
- `Diagnostics/AnimVerbose` + linhas `[ANIM]`; `docs/protocol.md` documenta pacote 18 e deprecia fragmentação
- Build do cliente: SUCESSO, 69120 bytes

## Task Commits

1. **Contrato compartilhado** - `a105637` (feat)
2. **Sender pacote 18** - `dd05cc7` (feat)
3. **Servidor relay 18** - `120a58e` (feat)
4. **Receptor + histerese** - `ea2dda7` (feat)
5. **Log verboso + docs** - `7df9ea6` (feat)

## Files Created/Modified

- `src/OriCoopPlus/OriCoopShared/PacketType.cs` - ID 18
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` - `DeriveState`
- `src/OriCoopPlus/OriCoopBepInEx/Domain/PlayerState.cs` - `State/IsGrounded/AnimNameHash/IsPlayerStatePacket`
- `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs` - leitura real + TEMP-TRACER
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` - writer/reader pacote 18
- `src/OriCoopDedicatedServer/.../Game/NetworkHandler.cs` - relay 18
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` - `HandleDirectState`
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` - `ApplySnapshotDirect`, `ApplyConfirmedAnimation`, `LogAnimTransition`
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` - `AnimVerbose`, `IsAnimVerbose`
- `docs/protocol.md` - pacote 18 + depreciação da fragmentação

## Decisions Made

- Discriminador explícito `IsPlayerStatePacket` em vez de adivinhar pelo conteúdo (coexiste com legado até plano 03)
- Aéreo sem sinal vertical assume Falling (nunca congela em Idle no ar)

## Deviations from Plan

None - plan executed exactly as written (adição do discriminador estava implícita na "rota direta").

## Issues Encountered

- Sem SDK dotnet nesta máquina: servidor não compilado aqui; relay 18 revisado manualmente contra a API `Packet` (todos os métodos usados existem). Build do servidor + teste 2-clientes ficam como verificação humana.
- Commits sem prefixo `01-01` no grep (mensagens `feat(anim-sync)`); hashes registrados acima.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Wave 2 liberada: plano 02 (dicionário explícito + dump F8) e plano 03 (remover legado + on-change/heartbeat) dependem só deste tracer
- Pendente humano: instalar DLL+servidor do mesmo build e validar 2 clientes (parado=Idle, correr=Run, `[ANIM]` com verbose)

---
*Phase: 01-anim-sync*
*Completed: 2026-10-05*
