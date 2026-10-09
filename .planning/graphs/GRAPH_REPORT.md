# Graph Report - Ori-Coop-Plus  (2026-10-09)

## Corpus Check
- 154 files · ~793,500 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 43 file(s) not represented in the graph (top: .dll 23, .cache 8, .exe 3)

## Summary
- 1674 nodes · 3930 edges · 148 communities (98 shown, 50 thin omitted)
- Extraction: 69% EXTRACTED · 31% INFERRED · 0% AMBIGUOUS · INFERRED: 1208 edges (avg confidence: 0.93)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `55ca63d2`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- GameHandlers
- system
- Program
- ConfigStore
- RemotePlayerPuppet
- Session
- OriCoopPlugin
- OriCommands
- AnimationRegistry
- NetServerHost
- ActionVisualState
- NetworkService
- OriCoopMenuScreen
- RemoteVisualController
- Accomplishments
- ServerConnectionDialog
- ServerBoot
- INetworkService
- Diagnostico rapido
- .PlaySpiritFlameVisual
- CommandRegistry
- .Log
- Phase 03 Plan 02: Visual Hardening + Wire Hardening Summary
- Layers
- .CleanPuppetComponents
- Mapa de entradas e entidades do jogo
- PlayerEventCore
- .CloneNativeButton
- .Publish
- .ApplyConfirmedAnimation
- Phase 03 Plan 01: Player Event Core Tracer Summary
- Phase 03: player-event-core (PlayerEventCore + piloto Spirit Flame) - Research
- PacketType
- .ReadServerPacket
- .Awake
- ILogger
- Core (no new libraries — in-repo components only)
- HelpCommand
- CancellationToken
- Fase: sincronização de animações de jogadores — Context
- AnimationSyncData
- Phase 2: Reescrita total do core do servidor — Context
- .HandleGamePacketAsync
- Phase 2: Reescrita total do core do servidor — Context
- Phase 03 Plan 03: Observability + Probe + Pilot Gate Summary
- Test Coverage Gaps
- RESEARCH — sincronização de animações (anim-sync)
- Goal Achievement
- IServerContext
- operations.md
- Arquitetura da UI Nativa e Integracao do Menu "Ori Coop"
- .Read
- AckTracker
- Component Responsibilities
- Phase 02: Server Rewrite — Research
- Tests
- Shared Patterns
- RemotePlayerManager
- 01-01-PLAN.md
- Checklist de teste manual
- Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context
- Protocolo e sincronizacao
- Phase 02 Plan 01: Tracer `--net2` fim-a-fim Summary
- Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context
- WW Launcher
- .Postfix
- docs/README.md
- CoopSkillType
- AnimLogViewer
- Phase 01 (anim-sync) Plan 01 Summary
- Phase 02 Plan 04: Cliente completo + cutover + docs/deploy Summary
- Phase 2: Reescrita do servidor — Discussion Log
- Areas & selections
- Bateria de testes — rework de sincronia de animações
- OriCoopBepInEx
- Known Bugs
- External Integrations
- Technology Stack
- Phase 02 Plan 03: Game port + payload redesign (D-15) Summary
- Locked Decisions
- ReplicationObservability
- Phase 03 — Validation Strategy
- Contexto do jogo e do mod
- File Classification
- Accomplishments
- Phase 01 (anim-sync) Plan 04 Summary
- Phase 02 Plan 02: Hardening ACK+retry, sessao e chat Summary
- Architecture Patterns
- Ferramentas e Scripts de Desenvolvimento e Engenharia Reversa
- CustomMessageProvider
- Tech Debt
- Common Pitfalls
- RESEARCH COMPLETE
- Solucao de problemas
- Arquitetura
- Documentacao do WW Launcher
- Validation Architecture
- Mapa do codigo
- .FormatPlayerLine
- 02-01-PLAN.md
- 02-02-PLAN.md
- 02-03-PLAN.md
- 02-04-PLAN.md
- 03-01-PLAN.md
- 03-02-PLAN.md
- 03-03-PLAN.md
- Agent's Discretion
- Sources
- SmokeProbe.csproj

## God Nodes (most connected - your core abstractions)
1. `NetworkService` - 80 edges
2. `GameHandlers` - 69 edges
3. `Program` - 60 edges
4. `RemotePlayerPuppet` - 60 edges
5. `OriCoopPlugin` - 60 edges
6. `NetServerHost` - 56 edges
7. `Layers` - 52 edges
8. `ConfigStore` - 51 edges
9. `ActionVisualState` - 49 edges
10. `DummyBot` - 45 edges

## Surprising Connections (you probably didn't know these)
- `Artifacts this phase produces` --references--> `CommandRegistry`  [INFERRED]
  .planning/phases/02-server-rewrite/02-01-PLAN.md → src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/CommandRegistry.cs
- `Required Artifacts` --references--> `ConsoleCommand`  [INFERRED]
  .planning/phases/02-server-rewrite/02-VERIFICATION.md → src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/ConsoleCommand.cs
- `Summary` --references--> `Session`  [INFERRED]
  .planning/phases/02-server-rewrite/02-RESEARCH.md → src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs
- `Fragmentacao POSITION/ANIM (causa raiz do bug #2 — REMOVIDA)` --references--> `RemotePlayerManager`  [INFERRED]
  docs/protocol.md → src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
- ``RemoteVisualController` material isolation depends on the shader having `_Color`` --references--> `RemoteVisualController`  [INFERRED]
  .planning/codebase/CONCERNS.md → src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs

## Import Cycles
- None detected.

## Communities (148 total, 50 thin omitted)

### Community 0 - "GameHandlers"
Cohesion: 0.05
Nodes (28): Integration Points, Adding global statics on the server, Anti-Patterns, Cloning the whole Sein for a remote player, Rebuilding relayed packets, Reusing removed packet IDs, `DummyBot` echo buffer can enqueue unboundedly under jitter, Lock-ordering hazard between `DummyBot._sync` and `GameHandlers._sync` (+20 more)

### Community 1 - "system"
Cohesion: 0.08
Nodes (17): OriCoopDedicatedServer, OriCoopDedicatedServer.Net.Reliability, OriCoopDedicatedServer.Net.Game, OriCoopBepInEx.Events, OriCoopDedicatedServer.Net.Transport, OriCoopDedicatedServer.Net, OriCoopBepInEx.Patches, OriCoopDedicatedServer.Net.Game.Commands (+9 more)

### Community 2 - "Program"
Cohesion: 0.11
Nodes (13): Error Handling, Function Design, Common Patterns, Coverage, Fixtures and Factories, Mocking, Test File Organization, Test Framework (+5 more)

### Community 3 - "ConfigStore"
Cohesion: 0.06
Nodes (23): `ConfigStore.Load` corrupt-file path resets defaults but still reports success, Codebase Structure, Directory Layout, Key File Locations, ConfigDto, AllowTeleport, ClientColors, EntitySync (+15 more)

### Community 4 - "RemotePlayerPuppet"
Cohesion: 0.12
Nodes (7): Files Created/Modified, Next Phase Readiness, FakeFlameMover, RemotePlayerPuppet, Nickname, PlayerId, TransientEventCleanup

### Community 5 - "Session"
Cohesion: 0.09
Nodes (17): No authentication, no encryption, session token is trivially guessable, Scaling Limits, `SessionManager.SweepExpired` mutates the dictionary it is enumerating, Naming Conventions, Session, EndPoint, Id, IsReady (+9 more)

### Community 6 - "OriCoopPlugin"
Cohesion: 0.07
Nodes (12): 1.5 UI e chaves, OriCoopPlugin, AssignedPlayerId, ConnectedPlayerCount, CurrentPing, Instance, IsConnected, Nickname (+4 more)

### Community 7 - "OriCommands"
Cohesion: 0.10
Nodes (21): ClientColorsCommand, Aliases, Command, Description, CoopCommand, Aliases, Command, Description (+13 more)

### Community 8 - "AnimationRegistry"
Cohesion: 0.20
Nodes (6): Catálogo de animações de movimentação do Ori, G-03-4 — attack clip never resolves: `aplicado=manteve-atual motivo=clip-desconhecido`, Reflection walk over every MonoBehaviour field, depth 5, with `visited` list identity scan, `Resources.FindObjectsOfTypeAll` global scan on the animation registry, AnimationRegistry, IsPrewarmed

### Community 10 - "ActionVisualState"
Cohesion: 0.08
Nodes (25): ActionVisualState, AimThrow, Bash, Carry, ChargeJump, Crouch, Dash, DoubleJump (+17 more)

### Community 11 - "NetworkService"
Cohesion: 0.13
Nodes (7): Config change broadcast, Data Flow, Key Abstractions, Primary Request Path — local player state outbound, NetworkService, IsConnected, LastRejectReason

### Community 12 - "OriCoopMenuScreen"
Cohesion: 0.17
Nodes (4): 4.3 Transição e Preservação da Pausa Vanilla, 5.3 Resolução do Bug 3: Tela em branco ao clicar em "Ori Coop" e sobreposição ao reabrir, OriCoopMenuScreen, Instance

### Community 13 - "RemoteVisualController"
Cohesion: 0.14
Nodes (9): Inicializacao, Protocolo de Validação de Renderização e Animação, Teste em rede local (LAN), Teste local no mesmo computador, Dependencies at Risk, Puppets are never culled, so off-screen players cost full Update/LateUpdate, String-typed reflection against game internals will break on any game patch, RemoteVisualController (+1 more)

### Community 14 - "Accomplishments"
Cohesion: 0.19
Nodes (7): Character event (Spirit Flame pilot), Accomplishments, `Client/RemotePlayerManager.cs` (MODIFY — service/router, event-driven), Network → main-thread marshal, No Analog Found, SpiritFlameEventData, SpiritFlamePatch

### Community 15 - "ServerConnectionDialog"
Cohesion: 0.16
Nodes (3): ServerConnectionDialog, Instance, IsOpen

### Community 16 - "ServerBoot"
Cohesion: 0.11
Nodes (13): Code Style, Coding Conventions, Comments, Import Organization, Language Dialects (know your target before writing), Module Design, ServerBoot, Config (+5 more)

### Community 18 - "Diagnostico rapido"
Cohesion: 0.17
Nodes (7): Camadas, Diagnostico rapido, Piloto Spirit Flame 03-03 — checklist de 2 clientes (2026-10-06, manual), Validacao de Correcao Realizada, Error Handling, SeinCharacterPatch, FloatingNameTag

### Community 19 - ".PlaySpiritFlameVisual"
Cohesion: 0.13
Nodes (15): G-03-3 — no proof the shooter never echoes its own event (open, major), Accomplishments, Build & Deploy Record, Code-Read Gates (acceptance verification), Decisions Made, Deviations from Plan, Files Created/Modified, Issues Encountered (+7 more)

### Community 20 - "CommandRegistry"
Cohesion: 0.16
Nodes (9): Auto-fixed Issues, CommandRegistry, All, Context, ConsoleCommand, Aliases, Command, Description (+1 more)

### Community 21 - ".Log"
Cohesion: 0.16
Nodes (4): Server binds all interfaces and advertises LAN addresses, ReceivedDatagram, UdpTransport, Reader

### Community 22 - "Phase 03 Plan 02: Visual Hardening + Wire Hardening Summary"
Cohesion: 0.12
Nodes (14): Pacote de evento do personagem PLAYER_EVENT (19), Accomplishments, Build & Deploy Record, Decisions Made, Deviations from Plan, Issues Encountered, Known Stubs, Performance (+6 more)

### Community 23 - "Layers"
Cohesion: 0.18
Nodes (13): Layers, Missing Critical Features, Directory Purposes, IPlayerStateSink, AnimationState, PlayerInputState, PlayerSnapshot, Vector2Data (+5 more)

### Community 25 - "Mapa de entradas e entidades do jogo"
Cohesion: 0.11
Nodes (17): Alvos de combate — Reconhecido, Breakables — Contrato sem fluxo localizado, Desbloqueio de habilidades — Implementado, Entidades Unity genéricas — Reconhecido / A confirmar, Eventos globais — Implementado, Habilidades, Inimigos e bosses, Lacunas para a próxima etapa (+9 more)

### Community 26 - "PlayerEventCore"
Cohesion: 0.14
Nodes (15): Agent's Discretion, Captura e escuta (como o core ouve o Ori), Escopo e fatiamento, Implementation Decisions, Reprodução remota (como o puppet mostra), Transporte por tipo (como viaja no fio), Next Phase Readiness, Agent's Discretion (+7 more)

### Community 28 - ".Publish"
Cohesion: 0.15
Nodes (13): Established Patterns, Existing Code Insights, Integration Points, Established Patterns, Existing Code Insights, Integration Points, Reusable Assets, Sending snapshots from `FixedUpdate` (+5 more)

### Community 29 - ".ApplyConfirmedAnimation"
Cohesion: 0.24
Nodes (5): G-03-5 — effects do not render even though the code is clip-independent (blocker, open), Accomplishments, Files Created/Modified, `Client/RemotePlayerPuppet.cs` (MODIFY — component, event-driven), Fail-closed unknown catalog entries

### Community 30 - "Phase 03 Plan 01: Player Event Core Tracer Summary"
Cohesion: 0.12
Nodes (14): Auto-fixed Issues, Build & Deploy Record, Deviations from Plan, Files Created/Modified, Gate Resolution (Task 1), Issues Encountered, Known Thin-Scope (by plan, not stubs), Performance (+6 more)

### Community 31 - "Phase 03: player-event-core (PlayerEventCore + piloto Spirit Flame) - Research"
Cohesion: 0.12
Nodes (15): Alternatives Considered, Code Examples, Environment Availability, Known Threat Patterns for this stack, Metadata, New packet receive branch (client) — shape to clone, Open Questions, Package Legitimacy Audit (+7 more)

### Community 32 - "PacketType"
Cohesion: 0.12
Nodes (16): PacketType, ANIM, COLOR, CONFIG_SYNC, DISCONNECT, DUMMY_ACTION, PLAYER_EVENT, PLAYER_STATE (+8 more)

### Community 33 - ".ReadServerPacket"
Cohesion: 0.21
Nodes (5): Cliente (onde o core mora), Cliente (muda junto), Cliente (muda junto), Cliente (onde o core mora), `Networking/NetworkService.cs` (MODIFY — service/transport, streaming)

### Community 34 - ".Awake"
Cohesion: 0.21
Nodes (6): Ciclo de inicializacao, Cliente, Servidor, Client `NetworkService.ReceiveLoop` re-allocates a thread per connect, `PlayerEventCore` has a static transport binding with no ownership, `Plugin/OriCoopPlugin.cs` (MODIFY — provider/lifecycle, request-response + event-driven)

### Community 35 - "ILogger"
Cohesion: 0.20
Nodes (13): Servidor dedicado (novo core, unico path), Cross-Cutting Concerns, Logging, Naming Patterns, Monitoring & Observability, Special Directories, FileConsoleLogger, ILogger (+5 more)

### Community 36 - "Core (no new libraries — in-repo components only)"
Cohesion: 0.23
Nodes (7): Architectural Constraints, Assumptions Log, Core (no new libraries — in-repo components only), Don't Hand-Roll, Pitfall 3: Reusing one `_lastRelaySeq` domain across packet types (cross-type starvation), Summary, NetProtocol

### Community 37 - "HelpCommand"
Cohesion: 0.13
Nodes (12): Where to Add New Code, `OriCoopShared/PacketType.cs` (MODIFY — config/contract, transform), ISessionTarget, TargetSession, HelpCommand, Aliases, Command, Description (+4 more)

### Community 39 - "Fase: sincronização de animações de jogadores — Context"
Cohesion: 0.14
Nodes (13): Agent's Discretion, Aplicação no puppet, Canonical References, Deferred Ideas, Diagnóstico, Estratégia de sincronia, Fase: sincronização de animações de jogadores — Context, Implementation Decisions (+5 more)

### Community 40 - "AnimationSyncData"
Cohesion: 0.18
Nodes (11): Reusable Assets, Transporte de rede, Accomplishments, Deviations from Plan, Issues Encountered, Next Phase Readiness, Performance, Phase 01 (anim-sync) Plan 02 Summary (+3 more)

### Community 41 - "Phase 2: Reescrita total do core do servidor — Context"
Cohesion: 0.14
Nodes (13): Agent's Discretion, Arquitetura e compatibilidade, Canonical References, Confiabilidade por pacote, Core antigo (a abandonar — ler para não repetir), Deferred Ideas, Implementation Decisions, Phase 2: Reescrita total do core do servidor — Context (+5 more)

### Community 42 - ".HandleGamePacketAsync"
Cohesion: 0.21
Nodes (5): Server receive/dispatch path, Ajustes documentados (sem desvio de escopo), Auto-fixed Issues, Deviations from Plan, Observable Truths

### Community 43 - "Phase 2: Reescrita total do core do servidor — Context"
Cohesion: 0.14
Nodes (13): Agent's Discretion, Arquitetura e compatibilidade, Canonical References, Confiabilidade por pacote, Core antigo (a abandonar — ler para não repetir), Deferred Ideas, Implementation Decisions, Phase 2: Reescrita total do core do servidor — Context (+5 more)

### Community 44 - "Phase 03 Plan 03: Observability + Probe + Pilot Gate Summary"
Cohesion: 0.14
Nodes (13): Accomplishments, Auto-fixed Issues, Build & Deploy Record, Deviations from Plan, Issues Encountered, Known Stubs, Next Phase Readiness, Performance (+5 more)

### Community 45 - "Test Coverage Gaps"
Cohesion: 0.15
Nodes (12): Client shared-state caches are never reset on disconnect, Codebase Concerns, `DummyBot.OnTick` blocks a timer thread on a sync-over-async send, Fragile Areas, `GameHandlers.RelayReliableAsync` reuses the sender's `seq` as the ACK key, Performance Bottlenecks, `Puppet.Update` allocates via `Vector3.Lerp` + `Vector3.Distance` every frame, `RemoteVisualController` material isolation depends on the shader having `_Color` (+4 more)

### Community 46 - "RESEARCH — sincronização de animações (anim-sync)"
Cohesion: 0.15
Nodes (11): 1.1 Leitura do Sein local, 1.3 Retransmissão do servidor, 1.4 Puppet e animação, 1.6 Build e restrições (obrigatório em todos os planos), 1. Fatos confirmados (com fonte), 2. Perguntas abertas (a confirmar em execução), 3. Desenho aprovado do protocolo (ID 18 `PLAYER_STATE`), 4. Desenho da histerese (D-13/D-14) (+3 more)

### Community 47 - "Goal Achievement"
Cohesion: 0.17
Nodes (10): Anti-Patterns Found, Behavioral Spot-Checks, Data-Flow Trace (Level 4), Gaps Summary, Goal Achievement, Human Verification Required, Key Link Verification, Phase 02: Server Rewrite Verification Report (+2 more)

### Community 48 - "IServerContext"
Cohesion: 0.15
Nodes (11): DummyCommand, Aliases, Command, Description, IServerContext, Config, Dummy, Game (+3 more)

### Community 49 - "operations.md"
Cohesion: 0.17
Nodes (10): Documentacao e contexto, Implantacao e substituicao obrigatoria apos build, Regras de manutencao do repositorio, 2026-10-05 — Remocao do Core legado, Build, Build do servidor sem `dotnet` na maquina (fallback historico), Comandos do mod, Instalacao (+2 more)

### Community 50 - "Arquitetura da UI Nativa e Integracao do Menu "Ori Coop""
Cohesion: 0.17
Nodes (12): 1. Visao Geral da UI Nativa, 2. Telas de Pausa no Ori DE: `InventoryManager` vs `PauseScreen`, 3. Estrutura de Componentes de Navegação, 4.1 Injeção do Botão no `InventoryManager`, 4.2 Estrutura da Tela `OriCoopMenuScreen`, 4. Arquitetura da Injeção do Submenu "Ori Coop", 5.1 Resolução do Bug 1: Falta de foco/interatividade e despausa indevida, 5.2 Resolução do Bug 2: Interface In-Game para Conexão ao Servidor (`ServerConnectionDialog`) (+4 more)

### Community 51 - ".Read"
Cohesion: 0.30
Nodes (3): Reusable Assets, Reusable Assets, PlayerStateReader

### Community 52 - "AckTracker"
Cohesion: 0.30
Nodes (3): AckTracker, Count, Pending

### Community 53 - "Component Responsibilities"
Cohesion: 0.18
Nodes (10): Architecture, Component Responsibilities, Pattern Overview, System Overview, Decode-then-reject amplifies logging, `Hello` flood occupies player slots, Nickname and chat are only bracket-stripped, not validated, Security Considerations (+2 more)

### Community 54 - "Phase 02: Server Rewrite — Research"
Cohesion: 0.17
Nodes (11): Alternatives Considered, Architectural Responsibility Map, Architecture Patterns, Core (all BCL, .NET 8 — zero NuGet), Package Legitimacy Audit, Phase 02: Server Rewrite — Research, Project Constraints (from AGENTS.md), Standard Stack (+3 more)

### Community 55 - "Tests"
Cohesion: 0.17
Nodes (11): 1. C1 - A atira, B ve (docs/operations.md checklist 03-03), 2. C2 - B atira, A ve (simetria do relay), 3. C3 - sem eco (ambos os LogOutput.log), 4. C4 - spam sob movimento + campos de observacao D-12, 5. C5 - tipo desconhecido segura pose (exige injecao), 6. C6 - desconexao/reconexao limpa, Current Test, Gaps (+3 more)

### Community 56 - "Shared Patterns"
Cohesion: 0.18
Nodes (10): Blind relay (server never parses/merges game bytes), C# 5 / .NET 3.5 client discipline, Defensive wire reads + per-packet error containment, Envelope + marker-led body (wire contract), Local-player-only publish filter (no echo), Metadata, Observability + logging vocabulary, Phase 03: player-event-core - Pattern Map (+2 more)

### Community 57 - "RemotePlayerManager"
Cohesion: 0.18
Nodes (10): `Diagnostics/ReplicationObservability.cs` (MODIFY — utility, event-driven), `Domain/INetworkService.cs` (MODIFY — interface, request-response), `Events/PlayerEventCore.cs` (NEW — service/event bus, event-driven), `Events/PlayerEventKind.cs` (NEW — model/enum, transform), `Events/SpiritFlameEventData.cs` (NEW — model/DTO, transform), `OriCoopShared/PlayerEventProtocol.cs` (NEW, optional — config/codec consts, transform), `Patches/SpiritFlamePatch.cs` (NEW — patch/detector, event-driven), Pattern Assignments (+2 more)

### Community 59 - "Checklist de teste manual"
Cohesion: 0.20
Nodes (10): Checklist de teste manual, Cutover 02-04 — cliente completo + core unico + deploy (2026-10-05), Game 02-03 — handlers, config persistente, dummy 999 + comandos (2026-10-05, automatizado), Hardening 02-02 — ACK+retry, sessao, sweeper e chat (2026-10-05, automatizado), Registro de Validacao de Build, Instalacao e Servidor, Scaffolding BepInEx, Tracer 02-01 — novo core `--net2` + SmokeProbe (2026-10-05, automatizado), Validacao cobertura total Sein no puppet (build 2026-10-06 — pendente de teste em jogo) (+2 more)

### Community 60 - "Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context"
Cohesion: 0.20
Nodes (9): Arquitetura e operação, Canonical References, Deferred Ideas, Established Patterns, Existing Code Insights, Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context, Phase Boundary, Protocolo e contrato (+1 more)

### Community 61 - "Protocolo e sincronizacao"
Cohesion: 0.20
Nodes (9): Confiabilidade e sessao (hardening 02-02), Configuracao distribuida, Envelope versionado (novo core, build atual), Fragmentacao POSITION/ANIM (causa raiz do bug #2 — REMOVIDA), IDs removidos no redesenho (D-15, quebra one-way), Pacote unificado PLAYER_STATE (18), Protocolo e sincronizacao, Regras para mudancas (+1 more)

### Community 62 - "Phase 02 Plan 01: Tracer `--net2` fim-a-fim Summary"
Cohesion: 0.20
Nodes (9): Ajustes documentados (sem desvio de escopo), Auth gates, Auto-fixed Issues, Deviations from Plan, Known Stubs, Phase 02 Plan 01: Tracer `--net2` fim-a-fim Summary, Self-Check: PASSED, Tarefas concluidas (+1 more)

### Community 63 - "Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context"
Cohesion: 0.20
Nodes (9): Arquitetura e operação, Canonical References, Deferred Ideas, Established Patterns, Existing Code Insights, Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context, Phase Boundary, Protocolo e contrato (+1 more)

### Community 64 - "WW Launcher"
Cohesion: 0.20
Nodes (10): 1. Compilar o mod, 2. Instalar os modulos, 3. Abrir o servidor, 4. Entrar pelo jogo, 5. Comandos do servidor, Documentacao, Ori Coop Plus, Requisitos (+2 more)

### Community 66 - "docs/README.md"
Cohesion: 0.31
Nodes (3): Arquitetura do Cliente BepInEx do Ori Coop, Build e Compatibilidade, Configuração do Entrypoint do BepInEx no Unity 5.3.2f1

### Community 67 - "CoopSkillType"
Cohesion: 0.22
Nodes (7): Identificadores de pacotes, Botched-to-illegal enum values left in the shared contract, CoopConfig, CoopSkillType, NONE, Spirit, Stomp

### Community 69 - "Phase 01 (anim-sync) Plan 01 Summary"
Cohesion: 0.22
Nodes (8): Decisions Made, Deviations from Plan, Issues Encountered, Next Phase Readiness, Performance, Phase 01 (anim-sync) Plan 01 Summary, Task Commits, User Setup Required

### Community 70 - "Phase 02 Plan 04: Cliente completo + cutover + docs/deploy Summary"
Cohesion: 0.22
Nodes (8): Ajustes documentados (sem desvio de escopo), Auth gates, Deviations from Plan, Known Stubs, Phase 02 Plan 04: Cliente completo + cutover + docs/deploy Summary, Self-Check: PASSED, Tarefas concluidas, Threat Flags

### Community 71 - "Phase 2: Reescrita do servidor — Discussion Log"
Cohesion: 0.22
Nodes (8): Arquitetura e compat, Arquivos gerados, Confiabilidade por pacote, Notas do agente, Phase 2: Reescrita do servidor — Discussion Log, Sessão e reconexão, Transporte e framing, Áreas selecionadas

### Community 72 - "Areas & selections"
Cohesion: 0.22
Nodes (8): 1. Escopo de eventos, 2. Captura e escuta, 3. Transporte por tipo, 4. Reprodução remota, Agent's discretion items, Areas & selections, Deferred ideas, Phase 3: Core de eventos do personagem — Discussion Log

### Community 73 - "Bateria de testes — rework de sincronia de animações"
Cohesion: 0.25
Nodes (7): Bateria de testes — rework de sincronia de animações, Registrar resultado, T0 — Base (build + conexão), T1 — Tracer pacote 18 (plano 01-01), T2 — Dicionário + dump (plano 01-02), T3 — Sem legado (plano 01-03), T4 — Diagnóstico em jogo (plano 01-04)

### Community 74 - "OriCoopBepInEx"
Cohesion: 0.25
Nodes (7): net35, Microsoft.NETFramework.ReferenceAssemblies (1.0.3), OriCoopDedicatedServer, net8.0, Microsoft.NET.Sdk, OriCoopBepInEx, Microsoft.NET.Sdk

### Community 75 - "Known Bugs"
Cohesion: 0.36
Nodes (5): G-03-1 / G-03-2 — remote Spirit Flame produces no projectile, no sound, no clip (open, major), Known Bugs, `SendNicknameUpdate` sends a body the server ignores, Artifacts this phase produces, Next Phase Readiness

### Community 76 - "External Integrations"
Cohesion: 0.25
Nodes (7): APIs & External Services, Authentication & Identity, CI/CD & Deployment, Data Storage, Environment Configuration, External Integrations, Webhooks & Callbacks

### Community 77 - "Technology Stack"
Cohesion: 0.25
Nodes (7): Configuration, Frameworks, Key Dependencies, Languages, Platform Requirements, Runtime, Technology Stack

### Community 78 - "Phase 02 Plan 03: Game port + payload redesign (D-15) Summary"
Cohesion: 0.25
Nodes (7): Ajustes documentados (sem desvio de escopo), Auth gates, Deviations from Plan, Known Stubs, Phase 02 Plan 03: Game port + payload redesign (D-15) Summary, Self-Check: PASSED, Tarefas concluidas

### Community 79 - "Locked Decisions"
Cohesion: 0.25
Nodes (8): Agent's Discretion, Arquitetura e compatibilidade, Confiabilidade por pacote, Deferred Ideas (OUT OF SCOPE), Locked Decisions, Sessão e reconexão, Transporte e framing, User Constraints (from CONTEXT.md)

### Community 80 - "ReplicationObservability"
Cohesion: 0.46
Nodes (3): Decisions Made, Supporting, ReplicationObservability

### Community 81 - "Phase 03 — Validation Strategy"
Cohesion: 0.25
Nodes (7): Manual-Only Verifications, Per-Task Verification Map, Phase 03 — Validation Strategy, Sampling Rate, Test Infrastructure, Validation Sign-Off, Wave 0 Requirements

### Community 82 - "Contexto do jogo e do mod"
Cohesion: 0.29
Nodes (6): Configuracao local, Contexto do jogo e do mod, Jogo alvo, Limites conhecidos, Recursos cooperativos, Vocabulário do jogo usado pelo codigo

### Community 83 - "File Classification"
Cohesion: 0.43
Nodes (3): Remote state inbound, Artifacts this phase produces, File Classification

### Community 84 - "Accomplishments"
Cohesion: 0.29
Nodes (6): Accomplishments, Deviations from Plan, Issues Encountered, Next Phase Readiness, Performance, Phase 01 (anim-sync) Plan 03 Summary

### Community 85 - "Phase 01 (anim-sync) Plan 04 Summary"
Cohesion: 0.29
Nodes (6): Accomplishments, Deviations from Plan, Issues Encountered, Next Phase Readiness, Performance, Phase 01 (anim-sync) Plan 04 Summary

### Community 86 - "Phase 02 Plan 02: Hardening ACK+retry, sessao e chat Summary"
Cohesion: 0.29
Nodes (6): Auth gates, Known Stubs, Phase 02 Plan 02: Hardening ACK+retry, sessao e chat Summary, Self-Check: PASSED, Tarefas concluidas, Threat Flags

### Community 87 - "Architecture Patterns"
Cohesion: 0.29
Nodes (7): Anti-Patterns to Avoid, Architecture Patterns, Pattern 1: Local-only Harmony event detector (D-06 + D-07), Pattern 2: Unreliable-sequenced game packet (D-09 class), Pattern 3: Fail-closed catalog resolve (D-15), Recommended Project Structure, System Architecture Diagram

### Community 88 - "Ferramentas e Scripts de Desenvolvimento e Engenharia Reversa"
Cohesion: 0.29
Nodes (6): 1. Reparacao e Diagnostico da Instalacao do Jogo, 2. Inspecao de Assemblies e Engenharia Reversa (Reflection e Cecil), 3. Inspecao de Sistemas Especificos do Jogo, 4. Ambiente, Download e Compilacao, 5. Arquivos de Dump e Referencia, Ferramentas e Scripts de Desenvolvimento e Engenharia Reversa

### Community 90 - "Tech Debt"
Cohesion: 0.33
Nodes (6): Duplicated "help" command list, README and docs reference a server assembly that no longer exists, Repo is a launcher fork carrying 28 tracked binaries and 6 unrelated games, `Scripts/` is 40+ ad-hoc reverse-engineering scratch files, Tech Debt, Two divergent build paths for the same client sources

### Community 91 - "Common Pitfalls"
Cohesion: 0.33
Nodes (6): Common Pitfalls, Pitfall 1: Patching the wrong overload / method (silent no-fire), Pitfall 4: SFX silently stripped by the puppet whitelist, Pitfall 5: Fake projectile inherits gameplay (damage/collision) or NRE-loops, Pitfall 6: Breaking C# 5 in new client code (build passes nowhere useful), Pitfall 7: Forgetting the same-build + docs rule (D-10 one-way break)

### Community 92 - "RESEARCH COMPLETE"
Cohesion: 0.33
Nodes (6): Confidence Assessment, File Created, Key Findings, Open Questions, Ready for Planning, RESEARCH COMPLETE

### Community 93 - "Solucao de problemas"
Cohesion: 0.33
Nodes (6): O jogador aparece sem nome, O patch nao envia snapshots, O plugin nao aparece no BepInEx, O teleporte esta desativado, O Windows nao deixa copiar a DLL, Solucao de problemas

### Community 95 - "Arquitetura"
Cohesion: 0.40
Nodes (5): Arquitetura, Compatibilidade, Estado e responsabilidades, Projetos, Visao geral

### Community 96 - "Documentacao do WW Launcher"
Cohesion: 0.40
Nodes (5): Como usar esta documentacao, Documentacao do WW Launcher, Escopo e confiabilidade, Manutencao da documentacao, Projetos e jogos conhecidos

### Community 97 - "Validation Architecture"
Cohesion: 0.40
Nodes (5): Phase Requirements → Test Map, Sampling Rate, Test Framework, Validation Architecture, Wave 0 Gaps

### Community 98 - "Mapa do codigo"
Cohesion: 0.50
Nodes (3): Fontes de catalogo, Mapa do codigo, Ori Coop Plus

### Community 100 - "02-01-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 101 - "02-02-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 102 - "02-03-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 103 - "02-04-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 104 - "03-01-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 105 - "03-02-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 106 - "03-03-PLAN.md"
Cohesion: 0.50
Nodes (3): Artifacts this phase produces, STRIDE Threat Register, Trust Boundaries

### Community 107 - "Agent's Discretion"
Cohesion: 0.50
Nodes (4): Agent's Discretion, Deferred Ideas (OUT OF SCOPE), Locked Decisions, User Constraints (from CONTEXT.md)

### Community 108 - "Sources"
Cohesion: 0.50
Nodes (4): Primary (HIGH confidence), Secondary (MEDIUM confidence), Sources, Tertiary (LOW confidence)

## Knowledge Gaps
- **151 isolated node(s):** `Debug`, `Info`, `Warning`, `Error`, `TargetSession` (+146 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 663 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **50 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Layers` connect `Layers` to `GameHandlers`, `ConfigStore`, `RemotePlayerPuppet`, `Session`, `OriCommands`, `AnimationRegistry`, `NetServerHost`, `OriCoopMenuScreen`, `RemoteVisualController`, `Accomplishments`, `ServerConnectionDialog`, `ServerBoot`, `INetworkService`, `Diagnostico rapido`, `CommandRegistry`, `.Log`, `.CleanPuppetComponents`, `PlayerEventCore`, `.CloneNativeButton`, `ILogger`, `HelpCommand`, `CancellationToken`, `IServerContext`, `.Read`, `AckTracker`, `Component Responsibilities`, `RemotePlayerManager`, `AnimLogViewer`?**
  _High betweenness centrality (0.153) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `NetworkService` (e.g. with `Estratégia de sincronia` and `Limites conhecidos`) actually correct?**
  _`NetworkService` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Debug`, `Info`, `Warning` to the rest of the system?**
  _151 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `GameHandlers` be split into smaller, more focused modules?**
  _Cohesion score 0.05455251407493691 - nodes in this community are weakly interconnected._
- **Why does `RemotePlayerPuppet` connect `RemotePlayerPuppet` to `GameHandlers`, `system`, `Program`, `ActionVisualState`, `NetworkService`, `RemoteVisualController`, `Accomplishments`, `Diagnostico rapido`, `.PlaySpiritFlameVisual`, `Layers`, `.CleanPuppetComponents`, `.ApplyConfirmedAnimation`, `Core (no new libraries — in-repo components only)`, `AnimationSyncData`, `Test Coverage Gaps`, `Component Responsibilities`, `RemotePlayerManager`, `01-01-PLAN.md`, `ReplicationObservability`?**
  _High betweenness centrality (0.087) - this node is a cross-community bridge._
- **Are the 19 inferred relationships involving `GameHandlers` (e.g. with `Servidor` and `Servidor dedicado (novo core, unico path)`) actually correct?**
  _`GameHandlers` has 19 INFERRED edges - model-reasoned connections that need verification._
- **Should `system` be split into smaller, more focused modules?**
  _Cohesion score 0.07618187292984041 - nodes in this community are weakly interconnected._