---
phase: 02-server-rewrite
plan: 01
subsystem: net-tracer
tags: [udp, envelope, handshake, relay, ping, smoke-probe, one-way-break]
requires: []
provides: [envelope-0x4F43v2, net2-host, smoke-probe, protocol-envelope-docs]
affects: [02-02-reliability, 02-03-game-handlers, 02-04-cutover]
tech-stack:
  added: []
  patterns: [BinaryPrimitives-LE-codec, Channel-bounded-DropOldest, ConcurrentDictionary-sessions, RNG-session-token, wrap-safe-uint32-seq, raw-bytes-relay]
key-files:
  created:
    - src/OriCoopPlus/OriCoopShared/NetProtocol.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/EnvelopeCodec.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/PlayerStateRelay.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Diagnostics/ServerLogger.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
    - src/OriCoopDedicatedServer/SmokeProbe/SmokeProbe.csproj
    - src/OriCoopDedicatedServer/SmokeProbe/Program.cs
  modified:
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs
    - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
    - docs/protocol.md
    - docs/operations.md
decisions:
  - "Checkpoint D-02/D-14/D-15: PROCEED com quebra one-way (aprovado na origem do spawn)"
  - "Confirm vazio marca IsReady; payload de nick no Confirm fica para 02-03"
  - "Servidor ignora EOF de stdin em vez de encerrar (robustez sob dotnet run)"
  - "Cliente ignora PlayerId da config e sempre faz handshake (IDs sao do servidor)"
  - "Reject com sessao atribuida reseta cliente para -1 (re-handshake, D-07)"
  - "SessionManager.cs usa #nullable disable (padrao de nulls classico; repo ja tem warnings legados)"
metrics:
  duration: "aprox. 1 sessao"
  completed: "2026-10-05"
actuals:
  tokens: 22000
  tasks: 3
  commits: 2
status: complete
---

# Phase 02 Plan 01: Tracer `--net2` fim-a-fim Summary

Tracer vertical production-quality do novo core: envelope versionado 0x4F43/v2 + handshake Hello(100)/Welcome(101)/Confirm(102) + relay de PLAYER_STATE(18) + Ping(104)/Pong(105), com cliente e servidor do mesmo build e core antigo intacto atras do default.

## Tarefas concluidas

| # | Tipo | Nome | Commit | Verificacao |
|---|------|------|--------|-------------|
| 1 | checkpoint:decision | Quebra one-way D-02/D-14/D-15 | (aprovado no spawn: proceed) | decisao registrada; nenhum codigo antes do proceed |
| 2 | tracer | Servidor: envelope + transporte + sessao + relay | `78bf688` | `dotnet build` Release exit 0 (0 erros; warnings so legados fora de `Net/`); grep `0x4F43` positivo; boot `--net2` + `stop` limpo; `git status` sem toque em `.Core` |
| 3 | auto | Cliente + SmokeProbe + docs + deploy | `1af4e4d` | `build.ps1` exit 0 (DLL 79.360 bytes, C#5); `SmokeProbe --test all` => `SMOKE_OK` exit 0; DLLs implantadas e boot validado no `ORI_DIR` |

SMOKE_OK detalhado (servidor fresco `--net2 --auto --port 7779`, auto-gerenciado pelo probe): readiness via Reject; invalid-magic recebe Reject com mensagem e nao cria sessao (IDs seguintes ainda 1 e 2); handshake A=1 B=2, Confirm com token errado e Confirm fantasma recusados; relay entrega a B o snapshot de A byte-identico (clientId+seq preservados), sem eco, drop-old de seq repetida; ping eco em 2 ms.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Servidor suicidava sob stdin redirecionado (EOF lido como "stop")**
- **Found during:** Task 3 (primeira rodada do SmokeProbe: readiness PASS, depois silencio)
- **Issue:** `RunNet2Host` tratava `Console.ReadLine() == null` (EOF herdado do `dotnet run` do probe) como pedido de parada; o servidor encerrava segundos apos subir e o `invalid-magic` falhava sem Reject
- **Fix:** EOF de stdin agora so loga (Debug) e aguarda `CancellationToken` (Ctrl+C/morte do processo); probe tambem passou a redirecionar stdin do filho
- **Files modified:** `Program.cs`, `SmokeProbe/Program.cs`
- **Commit:** `1af4e4d`

**2. [Rule 3 - Blocking] `dotnet run` aninhado sem diagnostico visivel**
- **Found during:** Task 3 (falha acima era opaca: stdout/stderr do servidor capturados e descartados)
- **Issue:** com `RedirectStandardOutput=true` e sem leitor, o log do servidor sumia e ainda havia risco de deadlock por buffer cheio
- **Fix:** probe drena stdout/stderr async para buffer e imprime tail de 30 linhas junto ao `SMOKE_FAIL`
- **Files modified:** `SmokeProbe/Program.cs`
- **Commit:** `1af4e4d`

**3. [Rule 2 - Correctness] Warnings de nulidade introduzidos pelo codigo novo**
- **Found during:** Task 2 (build com 18 warnings, ~7 nos arquivos novos)
- **Issue:** `out Session`/`Buffer.BlockCopy(payload)`/`ReadLine()` sob `<Nullable>enable`
- **Fix:** guards com narrowing (`payload != null &&`), `null!` em campos lazy (`_udp`, `_receiveTask`), `var` sobre `ReadLine()` (inferido como `string?`), `#nullable disable` apenas em `SessionManager.cs`; restantes (9) sao pre-existentes em `Game/` e no prompt interativo antigo
- **Files modified:** `EnvelopeCodec.cs`, `SessionManager.cs`, `UdpTransport.cs`, `Program.cs`
- **Commit:** `78bf688` (+ parte em `1af4e4d`)

### Ajustes documentados (sem desvio de escopo)

- **Deploy parcial:** `D:\SteamLibrary\...\Ori DE` nao existe nesta maquina; DLLs implantadas somente em `C:\Program Files (x86)\Steam\...\Ori DE` (`BepInEx\plugins\` + `Server\`), com jogo/servidor fechados e boot do binario implantado validado.
- **SmokeProbe cresce alem do minimo:** negativos extras (token errado, sessao fantasma, sem eco, seq repetida) e modos isolados `handshake|relay|ping` — mesma cobertura do plano, custo pequeno.
- **Sem `STATE.md`/`ROADMAP.md`:** o repo ainda nao os possui (o proprio `02-CONTEXT.md` registra a ausencia de roadmap); posicao e decisoes ficam neste SUMMARY.

## Auth gates

Nenhum — zero pacotes NuGet, zero servicos externos. Auditoria: `PackageReference` ausente nos 3 csproj (servidor, Core por referencia, SmokeProbe); cliente via `csc` sem referencias novas.

## Known Stubs

Nenhum — grep por `TODO|FIXME|placeholder|coming soon|not available` nos arquivos do plano retorna vazio. Comportamentos futuros explicitos ficam para os donos: `MsgAck` ignorado (02-02), pacotes de jogo fora `PLAYER_STATE` logados e descartados (02-03), sweeper de timeout 10 s (02-02), `PlayerId` da config sem efeito ate revisao (02-04).

## Threat Flags

Nenhum fora do `<threat_model>` do plano. Mitigacoes aplicadas neste tracer: T-02-01 parcial (token RNG por sessao + `clientId -1` restrito ao Hello + token exigido no Confirm e nos pacotes de sessao; validacao endpoint+token por datagrama e sweeper ficam 02-02); T-02-02 (length+magic+versao antes de qualquer leitura, recusa com `Reject` logado); T-02-03 parcial (Channel 1024 `DropOldest`; sweeper 02-02); T-02-SC (BCL-only, nenhum `dotnet add`).

## Self-Check: PASSED

- Arquivos criados: 10/10 presentes em disco; modificados: 4/4 presentes.
- Commits `78bf688` e `1af4e4d` presentes em `git log`.
- Verificacoes re-executadas pos-commit: server Release 0 erros, `build.ps1` exit 0, `SMOKE_OK` exit 0, `git status` limpo, `.Core` intocado.
