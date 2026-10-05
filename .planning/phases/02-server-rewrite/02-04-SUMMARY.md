---
phase: 02-server-rewrite
plan: 04
subsystem: net-cutover
tags: [client-framing, config-8bools, ack-retry, drop-old, cutover, docs, deploy, one-way-break]
requires: [envelope-0x4F43v2, net2-host, ack-retry-250ms-x3, session-hardened, sweeper-10s, chat-rules, smoke-6-modes, game-instances, config-8bools-persisted, dummy-server-local, serverboot, command-registry, smoke-game-mode]
provides: [client-full-framing, snapshot-queue-net-thread, serverboot-default, core-out-of-build, logs-dir, final-docs, deployed-binaries]
affects: []
tech-stack:
  added: []
  patterns: [pending-send-dict-retry-pump, tolerant-bool-read, enqueue-drain-snapshot, instance-ConsoleCommand-port, Compile-Remove-legacy]
key-files:
  created:
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/ConsoleCommand.cs
  modified:
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs
    - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ServerBoot.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/CommandRegistry.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs
    - docs/protocol.md
    - docs/architecture.md
    - docs/code-map.md
    - docs/operations.md
decisions:
  - "Snapshot sai da FixedUpdate: Publish enfileira, thread de rede drena (aceitacao literal do plano)"
  - "--net2 tolerado como no-op no cutover (SmokeProbe ainda o passa)"
  - "ConsoleCommand portado para o novo core; help do chat vira lista estatica"
  - "Log default muda para Logs/server.log com criacao do diretorio"
  - "SKILL/COLOR/SYNC_* recebidos: framing validado + descarte (sem consumidor nesta build)"
  - "README.md intocado: sem paginas novas/removidas, indice segue valido"
metrics:
  duration: "aprox. 1 sessao"
  completed: "2026-10-05"
actuals:
  tokens: 18100
  tasks: 3
  commits: 3
status: complete
---

# Phase 02 Plan 04: Cliente completo + cutover + docs/deploy Summary

Par cliente-servidor do mesmo build entregue como unidade shippavel: cliente BepInEx completo no novo framing (CONFIG 8 bools, ACK+retry, drop-old, heartbeat na thread de rede, zero legado), servidor com o novo core como unico path (Core antigo fora do build), docs finais consistentes e binarios implantados no ORI_DIR com checklist registrado.

## Tarefas concluidas

| # | Tipo | Nome | Commit | Verificacao |
|---|------|------|--------|-------------|
| 1 | auto | Cliente completo no novo framing (todos os pacotes) | `6e88cb3` | `build.ps1` exit 0 (DLL 83.968 bytes, C#5, 0 warnings); `SmokeProbe --test all` => `SMOKE_OK`; grep `-2/-6/-3/-7` vazio |
| 2 | auto | Cutover: novo core unico, Core fora do build | `19d331c` | server Release 0 erros + 0 warnings; exe default (sem `--net2`) responde `coop` (`Teleporte: ATIVADO`) + `stop` limpo; `Core.dll` ausente na saida |
| 3 | auto | Docs finais + build + deploy + checklist manual | `5997882` | ambos Release exit 0; `--test all` => `SMOKE_OK` (15 PASS); grep `0x4F43` + `serverconfig.json` positivo; DLLs implantadas com processos fechados; boot do implantado validado |

SMOKE_OK pos-cutover (servidor do build, `--net2` passado pelo probe e tolerado como no-op): readiness, invalid-magic, handshake, relay, ping, reliable, chat-rules, token, timeout, server-full + game fase 1/2 (config 8, teleport permitido/negado, dummy 999, persistencia).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Snapshots saiam da FixedUpdate (aceitacao exigia nenhum envio em FixedUpdate)**
- **Found during:** Task 3 (releitura da aceitacao contra `SeinCharacterPatch`: `Publish` era chamado em `FixedUpdate` e enviava direto no socket)
- **Issue:** heartbeat/ping ja estavam na thread de rede, mas o envio de `PLAYER_STATE` acontecia na thread do jogo; a aceitacao pedia literalmente "nenhum envio em FixedUpdate" para a pausa de 30 s nao depender do loop do jogo
- **Fix:** `SendPlayerSnapshot` agora so enfileira (`_queuedSnapshot` sob lock); a thread de rede drena o mais recente a cada iteracao (on-change + heartbeat 2,5 Hz via `FlushSnapshot`). Comportamento de envio identico, origem movida para a thread de rede
- **Files modified:** `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`
- **Commit:** `5997882` (follow-up da Task 1 dentro da janela da Task 3; rebuild `build.ps1` verde apos)

**2. [Rule 1 - Bug] Porta interativa limitada a 9999 em vez de 1-65535**
- **Found during:** Task 2 (rewrite do `Program.cs`: o prompt antigo fazia clamp `> 9999 ? 9999`)
- **Issue:** plano exigia normalizacao de porta 1-65535 tambem no prompt; o teto 9999 era inconsistente com `--port`
- **Fix:** prompt passa a usar `ClampMaxPlayers`/`NormalizePort` (1-10 e 1-65535)
- **Files modified:** `Program.cs`
- **Commit:** `19d331c`

**3. [Rule 2 - Correctness] Baselines de drop-old/pendencias sobreviviam a re-sessao**
- **Found during:** Task 1 (revisao do re-sync pos-Confirm: `_lastRelaySeq` por remetente persistia entre sessoes)
- **Issue:** apos `Reject` + re-handshake (ex. restart do servidor), seqs baixas do servidor seriam descartadas como antigas para sempre
- **Fix:** `Welcome` e `Reject` limpam `_lastRelaySeq` e `_pending`; o `CONFIG` + snapshots que o servidor reenvia pos-`Confirm` sao sempre aplicados
- **Files modified:** `NetworkService.cs`
- **Commit:** `6e88cb3`

**4. [Rule 2 - Correctness] `ConsoleCommand` do Core impedia remover a referencia**
- **Found during:** Task 2 (`CommandRegistry`, `OriCommands` e `BuildHelpText` usavam tipos do Core antigo)
- **Issue:** remover o `ProjectReference` quebrava a compilacao em 3 arquivos do novo core
- **Fix:** interface `ConsoleCommand` portada para `Net.Game.Commands` (mesma forma, sem dependencia legada); `BuildHelpText` do chat vira lista estatica espelhando `OriCommands.RegisterAll`
- **Files modified:** `ConsoleCommand.cs` (novo), `CommandRegistry.cs`, `OriCommands.cs`, `NetServerHost.cs`
- **Commit:** `19d331c`

**5. [Rule 2 - Correctness] Warnings novos no codigo reescrito**
- **Found during:** Task 2 (build: 2x CS8600 em `Program.cs`, `Console.ReadLine()` em contexto `Nullable enable`)
- **Issue:** disciplina das waves anteriores — zero warnings novos
- **Fix:** `string?` nas leituras do prompt
- **Files modified:** `Program.cs`
- **Commit:** `19d331c` (build final 0 warnings, 0 erros)

### Ajustes documentados (sem desvio de escopo)

- **SKILL/COLOR/SYNC_* recebidos sao validados e descartados:** o framing (marcador + comprimentos espelhando os builders do servidor) e checado e o `SysAck` ja foi enviado; nao ha consumidor no jogo nesta build, entao nada e aplicado. Sem eventos mortos na interface; fia-se quando houver consumidor.
- **`--net2` tolerado como no-op:** o SmokeProbe o passa ao subir o servidor; apos o cutover ele nao altera nada. Registrado no `Program.cs`, `code-map.md` e `operations.md`.
- **`SendNicknameUpdate` inalterado:** reenvia `Confirm` com nick (servidor tolera e re-dispara o join); documentado como tal em `protocol.md`.
- **README.md intocado:** nenhuma pagina criada/removida; todos os links do indice verificados presentes.
- **Deploy parcial:** `D:\SteamLibrary\...` inexistente; tudo em `C:\...`. `Core.dll`/`Core.pdb` removidos do `Server\` (fora da instalacao). `serverconfig.json` criado com padroes no `Server\` pelo boot de validacao.
- **Limpeza:** `Logs/server.log`, `net2-server.log` e `serverconfig.json` de teste removidos da arvore do repo; `git status` limpo fora dos arquivos do plano.

## Auth gates

Nenhum — zero pacotes NuGet, zero servicos externos. Auditoria: nenhum `PackageReference` adicionado (csproj so removeu o `ProjectReference` do Core); cliente via `csc` sem referencias novas.

## Known Stubs

Nenhum — grep por `TODO|FIXME|placeholder|coming soon|not available` no codigo novo retorna vazio (o unico hit, comentario `BUG #3` em `Game/NetworkHandler.cs`, e arquivo legado fora do build). Comportamentos explicitamente futuros: aplicacao de SKILL/COLOR/SYNC_* recebidos no jogo (framing pronto, sem consumidor); teste em jogo com 2 clientes reais (**a confirmar**, registrado em `operations.md`).

## Threat Flags

Nenhum fora do `<threat_model>` do plano. Mitigacoes aplicadas: T-02-10 (token guardado do `Welcome` e reenviado em todo datagrama; `Reject 106` reseta para re-handshake e limpa pendencias); T-02-11 (log em `Logs/server.log` com timestamp e niveis; drops/timeouts/retries logados nos dois lados); T-02-12 (processos verificados ausentes antes da copia; `Core.dll` legado removido do `Server\`); T-02-SC (BCL apenas, nenhum `dotnet add`/`Install-Package`).

## Self-Check: PASSED

- Arquivos: 1 criado + 13 modificados presentes em disco.
- Commits `6e88cb3`, `19d331c`, `5997882` presentes em `git log`; nenhuma delecao acidental (`Game/` legado e Core seguem no disco, fora do build).
- Verificacoes re-executadas pos-commit: `build.ps1` exit 0 (83.968 bytes), server Release 0/0, `--test all` => `SMOKE_OK`, exe default `coop`+`stop` ok, implantados (plugin 83.968 + server dll 103.936) com boot validado, `git status` sem residuos.
