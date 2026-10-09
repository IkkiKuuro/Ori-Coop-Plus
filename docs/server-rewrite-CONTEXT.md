# Phase 2: Reescrita total do core do servidor — Context

**Gathered:** 2026-10-05
**Status:** Ready for planning

> Nota: este repositório ainda não possui `.planning/ROADMAP.md` — não há
> número de fase formal. Este arquivo em `.planning/phases/02-server-rewrite/`
> (espelhado em `docs/server-rewrite-CONTEXT.md`) é o registro canônico da
> discussão até que o roadmap seja criado.

## Phase Boundary

Reescrever o código inteiro do servidor dedicado para uma conexão mais estável
e moderna, abandonando completamente o core antigo
(`OriCoopDedicatedServer.Core`: `Server`/`Client`/`Packet`/`ServerHandle`
estáticos + `BeginReceive` callback) e escrevendo um core completamente novo,
compatível com o contexto atual (pacote unificado `PLAYER_STATE` 18 da fase 1,
cliente BepInEx `.NET 3.5`/C# 5, servidor `.NET 8`, UDP porta 7777, 1–10
jogadores). Escopo: novo transporte, framing, sessão, confiabilidade e
arquitetura do dedicado. Não inclui novas capacidades (sync de
inimigos/entidades além do já existente, descoberta LAN, jogo pela internet,
submenu de pause) — essas pertencem a outras fases.

## Implementation Decisions

### Transporte e framing

- **D-01:** Base do transporte = `UdpClient` com `async`/`await` moderno, sem
  dependência externa (sem LiteNetLib/KCP nesta fase). Motivo: o cliente
  BepInEx roda em Unity 5.3 / `.NET 3.5` / C# 5 e não pode absorver lib RUDP
  sem porte dedicado; UDP puro mantém o par cliente-servidor viável.
- **D-02:** Envelope novo com `magic` (2 bytes) + `versão` (1 byte) + `seq`
  (`uint32`) + `clientId` + `packetId` + payload — **Reversibility:** one-way —
  builds antigos deixam de interoperar; o servidor recusa envelope sem
  magic/versão com mensagem clara.
  > Nota de supersessão (build atual): o envelope final tem 24B com 8 campos
  > (`magic, versao, flags, seq, clientId, token, packetId, ackSeq`) — ver
  > `docs/protocol.md` § Envelope versionado. Este D-02 registra a decisão
  > inicial, não o layout final.
- **D-03:** Serialização com `BinaryPrimitives` + `Spans`, little-endian
  explícito, validação de `length` antes de cada leitura (nunca ler além do
  `UnreadLength`).
- **D-04:** Loop de receive = single loop `ReceiveAsync` enfileirando em
  `Channel`, workers processam; shutdown limpo com `CancellationToken`.
  Fim do `BeginReceive` callback aninhado + `BeginSend` sem backpressure e do
  restart manual do listener em `Server.BeginReceiveOrRestart`.

### Sessão e reconexão

- **D-05:** Sessões dinâmicas por ID incremental (nunca reutilizado dentro da
  execução), em `ConcurrentDictionary`; limite só no `count` vs `MaxPlayers`.
  Abandona slots fixos `0..N-1` e o `AddClient` atual (loop `for i < Count`
  com indexador + `IgnoreIpCheck`, sem tratamento de servidor cheio).
- **D-06:** Handshake em 3 vias `Hello(-1)+versão+nick` →
  `Welcome+assignedId+token` → `Confirm(Ready)`. Só após `Confirm` o servidor
  marca `IsReady=true`, envia `CONFIG_SYNC` e aceita snapshots. Nick vazio
  vira `Player_<id>` (comportamento atual preservado).
- **D-07:** Heartbeat do cliente a cada 2 s (ping/heartbeat como mensagem de
  sistema); servidor derruba sessão após 10 s sem nenhum datagrama.
  Reconexão = novo handshake com novo ID + re-sync de estado atual (sem
  tentar segurar o ID antigo).
- **D-08:** Validação de endpoint fixo + token aleatório por sessão no
  envelope; pacote de outro `IPEndPoint` ou com token errado é descartado.
  Troca de IP/porta (NAT) exige novo handshake.

### Confiabilidade por pacote

- **D-09:** `PLAYER_STATE` (18) = unreliable sequenciado: cada snapshot carrega
  `seq`; servidor só repassa se `seq > última` daquele jogador; sem retry, sem
  fila. Perda se resolve no próximo snapshot. Coerente com D-07 da fase 1
  (on-change + heartbeat 2–5 Hz no sender).
- **D-10:** Pacotes críticos (chat `-5`, `CONFIG_SYNC` 16, `TELEPORT_REQUEST`
  15, `SYNC_ABILITY` 10, `SYNC_LEVER` 11, `SYNC_DOOR` 12, `SYNC_WORLDEVENT` 14,
  `SKILL` 7, `COLOR` 6, `DISCONNECT` 4) = confiável com ACK + retry (até 3
  tentativas com intervalo). Elimina o fire-and-forget atual.
- **D-11:** Relay = repasse imediato na chegada, sem agregação em tick fixo;
  mensagens de config/heartbeat só on-change + heartbeat 2–5 Hz.
- **D-12:** Ping como mensagem de sistema no novo envelope (`sendTicks` +
  `seq`, eco do servidor); HUD do cliente continua mostrando ms por jogador
  (substitui o `-7` legado no novo framing).

### Arquitetura e compatibilidade

- **D-13:** Novo core separado em `Transport` (UDP + framing + codecs) /
  `Session` (clientes, heartbeat, timeout, token) / `Game` (regras Ori,
  handlers, comandos). Sem estático global mutável (`Server.Clients`,
  `LatesNetId`, `NetworkVars` estáticos morrem); instância injetável,
  testável, com `ILogger` + `CancellationToken`.
- **D-14:** Quebra total com versão: `magic+versão` recusa build antigo com
  mensagem explícita; sem fallback para envelope antigo; cliente e servidor
  sempre do mesmo build — **Reversibility:** one-way — builds antigos deixam
  de interoperar (coerente com D-12 da fase 1: "não usamos código legado").
- **D-15:** Redesenho total de IDs e payloads autorizado pelo usuário (não só
  o envelope): researcher/planner podem redefinir `PacketType`, ordem de
  campos e codecs, desde que cliente BepInEx e servidor mudem juntos no mesmo
  build e `docs/protocol.md` seja atualizado na mesma mudança —
  **Reversibility:** one-way — redefine o contrato publicado cliente-servidor.
- **D-16:** Operação mantida: `.NET 8`, CLI `--auto`/`--max-players`/`--port`,
  comandos de console (`coop`, `tp`, `dummy`, `clientcolors`, `entitysync`),
  logs em arquivo + console com níveis, config persistida entre restarts
  (hoje `OriCoopServerModule.OnEnable` zera as opções — deixar de zerar).

### Agent's Discretion

- Nenhum "você decide" nesta rodada — o usuário escolheu todas as opções
  recomendadas exceto D-15 (optou por redesenho total em vez de manter
  IDs/ordem). Detalhes finos ficam com researcher/planner: valores exatos de
  magic/versão, tamanho do token, intervalos de retry, backoff, MTU/limite de
  350 chars do chat, layout de logs/métricas, estrutura exata de pastas do novo
  core.

## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Protocolo e contrato

- `docs/protocol.md` — IDs atuais, ordem de campos, `CONFIG_SYNC`,
  `TELEPORT_REQUEST`, `-5` chat, `-7` ping, regra "mudar cliente+servidor
  juntos", formato string `int32 length` + ASCII.
- `docs/anim-sync-CONTEXT.md` — decisões D-07 (on-change + heartbeat),
  D-09/D-11/D-12 (pacote novo, servidor só repassa, sem compat retroativa)
  que este rewrite precisa honrar no relay.
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — IDs atuais (base do
  redesenho autorizado em D-15).
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` — struct de payload da
  fase 1 (se o redesenho tocar `PLAYER_STATE`, partir daqui).

### Core antigo (a abandonar — ler para não repetir)

- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Server.cs` —
  `BeginReceive` callback, `AddClient` com bug de slots, `SendUDPData` com
  `BeginSend`, restart manual do listener.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Client.cs` —
  `UDP.Connect/SendData/HandleData`, `IsReady`, envelope `[clientId + len +
  payload]`.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Packet.cs` —
  `List<byte>`/`BitConverter`, `Write(string)` legado, `ReadString` dual
  Int32/LEB128, sem `uint`, sem validação prévia de length.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/ServerHandle.cs` —
  handlers `-1/-2/-4/-5/-6/-7`, `ClientDoneMessage`, chat com filtro `<>`,
  `ReceiveRpcMessage`/`ReciveNBMessage` repasse cego.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/ServerSend.cs` —
  `SendToAll/SendToClient`, `SyncNetworkVars`.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs` —
  relay `PLAYER_STATE` 18, `TELEPORT_REQUEST`, `SYNC_*`, `DummyManager` ID 999.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/OriCoopServerModule.cs` —
  zera opções no `OnEnable` (não repetir — D-16 exige persistência).
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` — CLI
  `--auto`/`--max-players`/`--port`, loop de console.

### Cliente (muda junto)

- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` —
  contraparte do transporte; handshake, `WriteLegacyString`,
  `SendPlayerSnapshot`, `ReadServerPacket` precisam acompanhar o novo
  envelope (C# 5, sem `$""`, sem `?.`).
- `docs/architecture.md` — separação BepInEx/servidor, autoridade de cada
  lado, ciclo de inicialização.
- `docs/code-map.md` — onde fica cada classe.
- `docs/operations.md` — build (`dotnet build` servidor, `build.ps1` cliente),
  instalação `<ORI_DIR>\Server\` + `BepInEx\plugins\`, checklist 2 clientes +
  desconexão/reconexão, diagnóstico rápido.

## Existing Code Insights

### Reusable Assets

- `CommandSystem/CommandProcessor.cs` + `Commands/` — parser de console a
  preservar como interface (reimplementar sobre o novo core, não copiar o
  acoplamento estático).
- `Game/ServerConfig.cs` + `Game/DummyManager.cs` — regras de config e bot de
  teste a re-hospedar no novo `Game` layer (com persistência de config).
- `Core/API/ServerEvents.cs`, `ServerModule.cs`, `Vector3.cs` — superfície de
  eventos/módulo a redesenhar como instância (não estática).
- `Core/Logger.cs` — base do log atual a evoluir para níveis + arquivo.

### Established Patterns

- Cliente BepInEx em C# 5 / `.NET Framework 3.5` via `build.ps1`/`csc.exe`:
  sem interpolação `$""`, sem `?.`, `Action` com no máximo 4 parâmetros —
  ver `docs/operations.md`. Qualquer mudança de payload precisa respeitar isso.
- Servidor em `.NET 8` / C# moderno via `dotnet build`; deploy exige
  jogo/servidor fechados (`<ORI_DIR>\BepInEx\plugins\`, `<ORI_DIR>\Server\`).
- Sem testes automatizados no repo — validação é manual com 2 clientes
  (`docs/operations.md` checklist + `docs/anim-test-battery.md` T0–T4).
- Cliente e servidor sempre compilados e testados como par (2 clientes +
  desconexão/reconexão).

### Integration Points

- Sender: `SeinCharacterPatch.FixedUpdate` → `PlayerStateReader.Read` →
  `OriCoopPlugin.Publish` → `NetworkService.SendPlayerSnapshot` → UDP →
  dedicado → rebroadcast → `NetworkService.ReadServerPacket` →
  `RemotePlayerManager.HandleSnapshot`. O rewrite troca o meio (transporte,
  framing, sessão, relay) sem mudar a leitura/aplicação nas pontas, salvo o
  redesenho de payloads autorizado em D-15.
- Console: `Program.Main` → `Server.Start` → `CommandProcessor` +
  `OriCoopServerModule.OnEnable` → `ServerEvents`. O novo core mantém o fluxo,
  mas como instâncias com `CancellationToken`.

## Specific Ideas

- "Reescrever o código inteiro do servidor, para uma conexão mais estável e
  moderna, e deixando de lado completamente o core antigo, escrevendo um core
  completamente novo para melhor compatibilidade com o contexto atual."
- Sem reaproveitamento do `Core` antigo: pode ler como referência negativa,
  mas não herdar classes, estáticos ou o callback `BeginReceive`.
- Estabilidade = handshake versionado, heartbeat/timeout, seq/ACK, validação
  de length, endpoint+token, logs que explicam cada drop.

## Deferred Ideas

- Descoberta automática de servidores na LAN (hoje IPv4 manual) — futura fase.
- Jogo pela internet (port-forward, NAT traversal) — futura fase, ainda não
  validado.
- Sync de inimigos/entidades além do `ES` atual — futura fase; cobertura real
  em partidas longas ainda **a confirmar**.
- Submenu nativo "Ori Coop" do pause — fora deste escopo (usar F6 como
  alternativa funcional).
- Persistência de opções já entra nesta fase via D-16; o restante de
  "matriz de compatibilidade Ori/Unity/assemblies" fica para operação futura.

---

*Phase: 2-server-rewrite*
*Context gathered: 2026-10-05*
