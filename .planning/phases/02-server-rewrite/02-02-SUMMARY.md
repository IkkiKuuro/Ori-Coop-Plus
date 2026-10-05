---
phase: 02-server-rewrite
plan: 02
subsystem: net-reliability
tags: [ack, retry, session, sweeper, token, endpoint, chat, smoke-probe, hardening]
requires: [envelope-0x4F43v2, net2-host, smoke-probe, protocol-envelope-docs]
provides: [ack-retry-250ms-x3, session-hardened, sweeper-10s, chat-rules, smoke-6-modes]
affects: [02-03-game-handlers, 02-04-cutover]
tech-stack:
  added: []
  patterns: [Timer-retry-pump, per-destination-pending-key, endpoint-token-validate, sweeper-broadcast-disconnect, tolerant-chat-parse]
key-files:
  created:
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/AckTracker.cs
  modified:
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs
    - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs
    - src/OriCoopDedicatedServer/SmokeProbe/Program.cs
    - docs/protocol.md
    - docs/operations.md
decisions:
  - "Criticos relayados em bytes originais com pendencia por destino (mesma seq, chaves distintas por endpoint)"
  - "Chat do servidor usa seq propria do servidor (nao preserva seq do remetente) com pendencia por destino"
  - "Cada teste do probe libera slots com DISCONNECT (teto MaxPlayers respeitado no modo all)"
  - "Help cai para lista estatica quando o registry do Core esta vazio no modo --net2"
metrics:
  duration: "aprox. 1 sessao"
  completed: "2026-10-05"
actuals:
  tokens: 35400
  tasks: 3
  commits: 3
status: complete
---

# Phase 02 Plan 02: Hardening ACK+retry, sessao e chat Summary

Hardening do novo core `--net2`: confiabilidade com ACK+retry dos pacotes criticos (D-10), validacao endpoint+token por datagrama (D-08), sweeper de timeout 10 s com broadcast de DISCONNECT (D-07), allocator final com Reject 106 SERVER_FULL (D-05) e regras de chat/nick preservadas (D-11). Tudo provado pelo SmokeProbe estendido com 6 modos.

## Tarefas concluidas

| # | Tipo | Nome | Commit | Verificacao |
|---|------|------|--------|-------------|
| 1 | auto | ACK + retry dos pacotes criticos (servidor) | `3a820c8` | build Release 0 erros; 0 warnings em `Net/` |
| 2 | auto | Sessao hardened: token+endpoint, sweeper, allocator, Reject | `e060981` | build Release 0 erros + `SmokeProbe --test all` (modos 02-01) verde |
| 3 | auto | Regras de chat/nick + SmokeProbe reliable/timeout/token | `004a009` | `SmokeProbe --test all` => `SMOKE_OK` (10 PASS); singles `RELIABLE_OK`/`TIMEOUT_OK`/`TOKEN_OK` |

SMOKE_OK detalhado (servidor fresco `--net2 --auto --port 7787 --max-players 10`): readiness, invalid-magic, handshake (IDs 1-2), relay, ping (herdados 02-01, ainda verdes) + reliable (SysAck 103 imediato; B recebe 2+ copias via retry 250 ms e confirma; PLAYER_STATE sem SysAck) + chat-rules (400 chars→350; `a<b>c>d`→`abcd`; nick `Evil<Nick>`→`EvilNick`; help unicast, D nada recebe) + token (token errado e endpoint trocado sem Pong; sessao viva) + timeout (DISCONNECT do ID 12 apos silencio, A em keep-alive) + server-full (servidor proprio `--max-players 2`: terceiro Hello→`Reject 106 SERVER_FULL`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Suite `all` atingia o teto de 10 sessoes antes do teste de timeout**
- **Found during:** Task 3 (primeira rodada do `all`: `FAIL timeout (handshake A)`)
- **Issue:** sessoes dos testes anteriores acumulavam (2+2+1+2+2+1=10, nenhuma removida — testes rapidos, sweeper de 10 s nao alcancava); o `Hello` do timeout recebia `Reject SERVER_FULL` (que, ironicamente, prova o D-05 funcionando)
- **Fix:** cada teste do probe libera seus slots com `DISCONNECT` (protocolo real, nao atalho) ao final; `SendDisconnect` helper no probe
- **Files modified:** `SmokeProbe/Program.cs`
- **Commit:** `004a009`

**2. [Rule 1 - Bug] Comentario-doc do `IsCriticalPacket` truncado numa edicao**
- **Found during:** Task 2 (leitura pos-edit da regiao do sweeper)
- **Issue:** edicao que inseriu `HandleDisconnectAsync`/`OnSweepTick` removeu as 2 linhas de abertura do doc-comment seguinte
- **Fix:** linhas `/// <summary>` + abertura restauradas; build confirma 0 erros
- **Files modified:** `Net/NetServerHost.cs`
- **Commit:** `e060981`

**3. [Rule 2 - Correctness] Warning novo de nulidade no callback do Timer**
- **Found during:** Task 1 (build: `CS8622 OnRetryTick(object state)` vs `TimerCallback`)
- **Issue:** mesmo padrao do legado `DummyManager.Tick`, mas regra e zero warnings novos em `Net/`
- **Fix:** assinatura `OnRetryTick(object? state)`; grep pos-build confirma 0 warnings em `Net/` e `Session/`
- **Files modified:** `Net/NetServerHost.cs`
- **Commit:** `3a820c8`

### Ajustes documentados (sem desvio de escopo)

- **Verificacao T1/T2 consolidada no T3:** o plano pedia `--test reliable` (T1) e `--test timeout,token` (T2), mas os modos do probe so nascem no T3; T1/T2 foram verificados com build 0-erro + regressao `all` dos modos 02-01, e a prova final cobre os 6 modos de uma vez (log em `smoke-0202b.log`, reproduzido em singles contra servidor manual).
- **Chat do servidor com seq propria:** relay generico de criticos reemite bytes originais (T1), mas o chat formatado (sender `SERVER`/nick) e construido pelo servidor com `NextServerSeq` + pendencia por destino — mesma garantia D-10, formato legivel pelo cliente new-core.
- **Deploy:** `OriCoopDedicatedServer.dll/exe` frescos copiados para `C:\...\Ori DE\Server\` (78.848 bytes, sem processo em pe); cliente BepInEx intocado neste plano (sem rebuild).
- **Cosmetico Windows/UDP:** envios para sockets ja fechados geram rajadas `ConnectionReset` no log do servidor; o loop absorve e segue (documentado em `operations.md`).

## Auth gates

Nenhum — zero pacotes NuGet, zero servicos externos. Auditoria: `PackageReference` ausente nos csproj (grep vazio em `src/OriCoopDedicatedServer`); nenhum arquivo `.Core` modificado (`git diff --name-only` confirma); cliente BepInEx nao recompilado.

## Known Stubs

Nenhum novo — grep por `TODO|FIXME|placeholder|coming soon|not available` nos arquivos do plano retorna vazio. Comportamentos explicitamente futuros: handlers de `CONFIG_SYNC`/`TELEPORT`/`SYNC_*`/`SKILL`/`COLOR` alem de relay generico (02-03); re-sync pos-reconexao alem de novo ID + limpeza (02-03/02-04); `MissedSweeps` preservado zerado para diagnostico futuro.

## Threat Flags

Nenhum fora do `<threat_model>` do plano. Mitigacoes aplicadas: T-02-04 (endpoint fixo + token por datagrama, descarte logado com motivo, `Confirm` valida endpoint); T-02-05 (teto 3 retries a 250 ms por pendencia, unreliable fora do tracker, give-up sem derrubar sessao); T-02-06 (timeout 10 s + `Count` vs `MaxPlayers` + `Reject 106 SERVER_FULL`); T-02-SC (BCL apenas, nenhum `dotnet add`).

## Self-Check: PASSED

- Arquivos: `AckTracker.cs` criado + 6 modificados presentes em disco.
- Commits `3a820c8`, `e060981`, `004a009` presentes em `git log`.
- Verificacoes re-executadas pos-commit: server Release 0 erros, `SMOKE_OK` (10 PASS, 6 modos), singles `RELIABLE_OK`/`TIMEOUT_OK`/`TOKEN_OK`, `git status` sem residuos (logs de runtime removidos), `.Core` intocado, deploy no `ORI_DIR` com tamanho do build fresco.
