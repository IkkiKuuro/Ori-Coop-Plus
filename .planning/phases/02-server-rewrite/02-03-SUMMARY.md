---
phase: 02-server-rewrite
plan: 03
subsystem: net-game
tags: [game-handlers, config-sync, dummy-999, serverboot, commands, d-15-redesign, smoke-probe, persistence]
requires: [envelope-0x4F43v2, net2-host, ack-retry-250ms-x3, session-hardened, sweeper-10s, chat-rules, smoke-6-modes]
provides: [game-instances, config-8bools-persisted, dummy-server-local, serverboot, command-registry, smoke-game-mode]
affects: [02-04-cutover]
tech-stack:
  added: []
  patterns: [IGameTransport-host-bridge, instance-per-layer-DI, Changed-event-broadcast, defensive-length-parse, serverconfig-json-BCL]
key-files:
  created:
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/CommandRegistry.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs
  modified:
    - src/OriCoopPlus/OriCoopShared/PacketType.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs
    - src/OriCoopDedicatedServer/SmokeProbe/Program.cs
    - docs/protocol.md
    - docs/operations.md
decisions:
  - "Dummy sem carimbo de ID no corpo (identidade via header 999); carimbo legado quebraria o parser new-core"
  - "Dispatch da Game antes do relay generico de criticos (criticos relayados so sem Game ligada)"
  - "Cliente BepInEx intocado: le 6 bools e ignora os 2 extras; adocao plena dos 8 no cutover 02-04"
  - "Envios originados no servidor vao confiaveis (D-10); so snapshots sao unreliable"
  - "Comandos instanciam mas console so e fiado no 02-04 (broadcast-on-change ja ligado no ServerBoot)"
metrics:
  duration: "aprox. 1 sessao"
  completed: "2026-10-05"
actuals:
  tokens: 37900
  tasks: 3
  commits: 3
status: complete
---

# Phase 02 Plan 03: Game port + payload redesign (D-15) Summary

Porte da camada Game para o novo core com o redesenho de IDs/payloads autorizado (D-15, quebra one-way): IDs mortos removidos do contrato, `CONFIG_SYNC` estendido para 8 bools, handlers/config/dummy/comandos como instancias testaveis sem estatico global, config persistida em `serverconfig.json` entre restarts. Tudo provado pelo SmokeProbe com o novo modo `game` (`GAME_OK`) mais regressao total (`SMOKE_OK`).

## Tarefas concluidas

| # | Tipo | Nome | Commit | Verificacao |
|---|------|------|--------|-------------|
| 1 | auto | Redesenho do contrato: PacketType + regras de remocao | `6057ef2` | build Release 0 erros; `REQUEST_PLAYERS` em linhas nao-comentario = 0; 0 refs mortas no `Net/` novo |
| 2 | auto | Game layer: handlers + ConfigStore persistente + DummyBot server-local | `0088116` | build Release 0 erros + 0 warnings em `Net/`; `--test game` => `GAME_OK`; restart preserva hash (`PERSIST_OK`); deploy `Server/` (112.640 bytes) |
| 3 | auto | Comandos por instancia sobre o novo core | `496f2cf` | build Release 0 erros + 0 warnings em `Net/`; `--test all` => `SMOKE_OK` (13 PASS); grep de estaticos no `Net/` vazio |

GAME_OK detalhado (servidores proprios `--net2`, portas dedicadas): fase 1 — `CONFIG_SYNC` unicast pos-`Confirm` com 8 bools `[on,off,off,off,off,off,off,off]`; snapshot de A registrado; `TELEPORT_REQUEST` B→A responde pos+nick com anuncio em chat; `DUMMY_ACTION 0` spawna e B recebe `PLAYER_STATE` com `clientId 999` + nick `Bot_Amigo`; teleport ao 999 responde `Bot_Amigo`; segundo toggle gera `DISCONNECT` do 999. Fase 2 (`AllowTeleport=false` no `serverconfig.json` do exe) — `CONFIG_SYNC` carrega `off` (persistencia entre processos); teleport negado chega so ao solicitante (`Teleporte desativado`) e nada chega a testemunha em 900 ms.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Dispatch da Game inalancavel para pacotes criticos**
- **Found during:** Task 2 (primeira rodada do `--test game`: `sem TELEPORT_RESPONSE`)
- **Issue:** o bloco `IsGamePacket` foi inserido DEPOIS do relay generico de criticos — como TELEPORT/SYNCs/SKILL/COLOR/CONFIG sao criticos (D-10), caiam no `RelayReliableAsync` e nunca chegavam aos handlers (so DUMMY_ACTION 17 e BREAKABLE 13, nao-criticos, alcancavam)
- **Fix:** dispatch da Game movido para antes do relay generico (quando `Game` ligada, todo pacote de jogo vai aos handlers; sem ela, mantem o fallback 02-02)
- **Files modified:** `Net/NetServerHost.cs`
- **Commit:** `0088116`

**2. [Rule 1 - Bug] Teste aguardava a negacao no cliente errado**
- **Found during:** Task 2 (fase 2: `sem chat de negacao para o solicitante`)
- **Issue:** o probe enviava o teleport por B mas aguardava o chat-deny em A; a negacao e unicast ao solicitante (B)
- **Fix:** fase 2 aguarda o deny em B e verifica silencio (so `-5`/`15` reprovam; retries de config ignorados) em A
- **Files modified:** `SmokeProbe/Program.cs`
- **Commit:** `0088116`

**3. [Rule 1 - Bug] Negativos do handshake viraram corrida com o join da Game**
- **Found during:** Task 3 (regressao `all`: `Confirm com token errado nao foi recusado`)
- **Issue:** o join agora envia COLOR + CONFIG_SYNC confiaveis; o `ExpectReject` de tiro unico lia um retry em vez do Reject (em 02-02 nao havia envios no join)
- **Fix:** `ExpectReject` drena ate o Reject ou o prazo, ignorando ruido (mesmo que o cliente real faz)
- **Files modified:** `SmokeProbe/Program.cs`
- **Commit:** `496f2cf`

**4. [Rule 2 - Correctness] Warnings novos de nulidade no codigo novo**
- **Found during:** Tasks 2 e 3 (builds: CS8629/CS8602/CS8600 em `ConfigStore`, `GameHandlers`, `CommandRegistry`, probe)
- **Issue:** mesma regra do 02-02 — zero warnings novos em `Net/`
- **Fix:** `bool?` intermediaria, `Session?` + null-check no teleport, `out`/retornos anulaveis no registry
- **Files modified:** `Net/Game/*.cs`, `SmokeProbe/Program.cs`
- **Commit:** `0088116`, `496f2cf`

**5. [Rule 2 - Correctness] Dummy com carimbo quebraria o parser new-core**
- **Found during:** Task 2 (revisao do layout antes do primeiro smoke)
- **Issue:** o `DummyManager` antigo carimbava `[18][999]...` no corpo, mas o cliente new-core (02-01, mesmo build) le snapshots SEM ID no corpo — o `999` seria lido como posicao
- **Fix:** `BuildPlayerStatePayload` sem carimbo (identidade do dummy via `clientId 999` do header); relays de SKILL/SYNC mantem carimbo do remetente (cliente atual ignora esses pacotes; formato legado preservado p/ adocao futura)
- **Files modified:** `Net/Game/GameHandlers.cs`, `Net/Game/DummyBot.cs`
- **Commit:** `0088116`

### Ajustes documentados (sem desvio de escopo)

- **Cliente BepInEx intocado (sem rebuild):** o leitor atual le os 6 bools lideres e ignora os 2 bytes extras (payload = resto do datagrama, sem framing por tamanho consumido) — mudança aditiva-tolerante; remocao dos membros 3/5 do enum nao tem nenhuma referencia no cliente (grep vazio). Adocao plena dos 8 bools no cliente entra no cutover 02-04.
- **Envios do servidor vao confiaveis:** join (COLOR inicial, CONFIG_SYNC, historico), respostas de teleport, relays reconstruidos (SKILL/SYNCs) e chats — D-10 lista esses pacotes como criticos; so snapshots (relay + dummy) sao unreliable (D-09). O cliente real nao envia ACK (igual ao chat 02-02): retry 3x e give-up com warning, sem efeito colateral.
- **`SYNC_BREAKABLE` sem relay:** sem sender em todo o `src/` e sem gating (o handler antigo tambem descartava); caso explicito com length-check + log debug, marcado "a confirmar" no codigo.
- **Comandos instanciam, console fia no 02-04:** `OriCommands.RegisterAll` + `CommandRegistry` compilam contra o novo core e o mecanismo toggle→save+broadcast ja esta ligado (`ServerBoot` assina `Changed`); o exercicio E2E via console e do cutover, que fia o loop.
- **Deploy:** `OriCoopDedicatedServer.dll/exe` frescos copiados para `C:\...\Ori DE\Server\` (112.640 bytes, sem processo em pe); `D:\SteamLibrary` inexistente nesta maquina; cliente BepInEx intocado.
- **Limpeza:** `net2-server.log` e `serverconfig.json` de teste removidos da arvore (o modo `game` restaura o original do `bin/` ao final); `git status` limpo.

## Auth gates

Nenhum — zero pacotes NuGet (`System.Text.Json` e BCL do .NET 8), zero servicos externos. Auditoria: nenhum `dotnet add`; nenhum arquivo `.Core` modificado (`Game/`, `Program.cs` e `Net/` alterados sao do exe novo, nao do `Core` antigo).

## Known Stubs

Nenhum — grep por `TODO|FIXME|placeholder|coming soon|not available` nos arquivos do plano retorna vazio. Comportamentos explicitamente futuros: leitura dos 2 novos bools no cliente BepInEx (02-04); fiacao dos comandos no loop de console (02-04); relay de `SYNC_BREAKABLE` (sem sender; a confirmar).

## Threat Flags

Nenhum fora do `<threat_model>` do plano. Mitigacoes aplicadas: T-02-07 (AllowTeleport + snapshot recente validados; nega unicast; destino dummy exige `IsActive`; `FindSession` so `IsReady`); T-02-08 (length-check antes de cada campo em todos os handlers + `TryParse*`; descarte logado com ID; strings com bound check); T-02-09 (so 8 bools nao-sensiveis no JSON ao lado do exe; corrompido nao e sobrescrito ate mudanca explicita); T-02-SC (BCL apenas, nenhum `dotnet add`).

## Self-Check: PASSED

- Arquivos: 6 criados + 6 modificados presentes em disco.
- Commits `6057ef2`, `0088116`, `496f2cf` presentes em `git log`; nenhuma delecao acidental; `git status` limpo.
- Verificacoes re-executadas pos-commit: server Release 0 erros + 0 warnings em `Net/`, probe 0/0, `--test all` => `SMOKE_OK` (13 PASS: 10 modos 02-01/02-02 + 2 fases game), `PERSIST_OK` (hash stop/start identico), grep de estaticos legados no `Net/` vazio, deploy no `ORI_DIR` com tamanho do build fresco.
