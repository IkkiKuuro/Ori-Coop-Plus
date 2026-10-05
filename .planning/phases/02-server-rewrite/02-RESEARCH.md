# Phase 02: Server Rewrite — Research

**Researched:** 2026-10-05
**Domain:** Modern UDP game-server core (.NET 8) + legacy Unity/Mono UDP client interop (.NET 3.5 / C# 5)
**Confidence:** HIGH (in-repo facts verified by direct reads + SDK compile probe; external guidance honestly tagged)

## Summary

This phase replaces `OriCoopDedicatedServer.Core`'s static `Server`/`Client`/`Packet`/`ServerHandle` + `BeginReceive` callback core with a new instance-based core in three layers (`Transport` / `Session` / `Game`), speaking a new versioned binary envelope over pure UDP. All locked decisions (D-01–D-16) are technically sound and implementable with **zero NuGet dependencies** — every required API (`UdpClient.ReceiveAsync(CancellationToken)`, `System.Threading.Channels`, `BinaryPrimitives`, `ConcurrentDictionary`, `RandomNumberGenerator`, `System.Text.Json`) ships in the .NET 8 shared framework. This was proven in-session by compiling and running an API probe against the installed SDK (8.0.425 / runtime 8.0.31) — see Environment Availability.

The hardest constraint is the **asymmetric codec**: the server may use `BinaryPrimitives` + `Span`, but the Unity 5.3 / .NET 3.5 / C# 5 BepInEx client only has `BinaryWriter`/`BinaryReader` + `BitConverter` + ASCII. The envelope must therefore be a **fixed-size little-endian header** that is trivial to read/write on both sides. The research prescribes an exact 24-byte header layout, a minimal ACK scheme (250 ms retry, 3 retries), per-sender `uint32` sequence numbers with wrap-safe comparison, endpoint+token validation, and a relay model where the server **re-emits the original datagram bytes unchanged** (preserving sender `clientId` + `seq`), which makes drop-old logic identical on server and clients.

**Primary recommendation:** Build the new core as zero-dependency .NET 8 (`Transport`: UDP + 24-byte fixed header codec + Channel; `Session`: `ConcurrentDictionary` + token + 10 s sweeper; `Game`: ported handlers/commands/config as instances), keep the client codec to `BinaryReader`/`BinaryWriter` field-by-field mirroring, reserve IDs `0` (server) / `999` (dummy) out of the dynamic allocator, delete the dead `-2`/`-6` blind-relay paths, collapse `-3` network vars into `CONFIG_SYNC`, and validate exclusively with the manual 2-client checklist (no test harness exists).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Transporte e framing

- **D-01:** Base do transporte = `UdpClient` com `async`/`await` moderno, sem dependência externa (sem LiteNetLib/KCP nesta fase). Motivo: o cliente BepInEx roda em Unity 5.3 / `.NET 3.5` / C# 5 e não pode absorver lib RUDP sem porte dedicado; UDP puro mantém o par cliente-servidor viável.
- **D-02:** Envelope novo com `magic` (2 bytes) + `versão` (1 byte) + `seq` (`uint32`) + `clientId` + `packetId` + payload — **Reversibility:** one-way — builds antigos deixam de interoperar; o servidor recusa envelope sem magic/versão com mensagem clara.
- **D-03:** Serialização com `BinaryPrimitives` + `Spans`, little-endian explícito, validação de `length` antes de cada leitura (nunca ler além do `UnreadLength`).
- **D-04:** Loop de receive = single loop `ReceiveAsync` enfileirando em `Channel`, workers processam; shutdown limpo com `CancellationToken`. Fim do `BeginReceive` callback aninhado + `BeginSend` sem backpressure e do restart manual do listener em `Server.BeginReceiveOrRestart`.

#### Sessão e reconexão

- **D-05:** Sessões dinâmicas por ID incremental (nunca reutilizado dentro da execução), em `ConcurrentDictionary`; limite só no `count` vs `MaxPlayers`. Abandona slots fixos `0..N-1` e o `AddClient` atual (loop `for i < Count` com indexador + `IgnoreIpCheck`, sem tratamento de servidor cheio).
- **D-06:** Handshake em 3 vias `Hello(-1)+versão+nick` → `Welcome+assignedId+token` → `Confirm(Ready)`. Só após `Confirm` o servidor marca `IsReady=true`, envia `CONFIG_SYNC` e aceita snapshots. Nick vazio vira `Player_<id>` (comportamento atual preservado).
- **D-07:** Heartbeat do cliente a cada 2 s (ping/heartbeat como mensagem de sistema); servidor derruba sessão após 10 s sem nenhum datagrama. Reconexão = novo handshake com novo ID + re-sync de estado atual (sem tentar segurar o ID antigo).
- **D-08:** Validação de endpoint fixo + token aleatório por sessão no envelope; pacote de outro `IPEndPoint` ou com token errado é descartado. Troca de IP/porta (NAT) exige novo handshake.

#### Confiabilidade por pacote

- **D-09:** `PLAYER_STATE` (18) = unreliable sequenciado: cada snapshot carrega `seq`; servidor só repassa se `seq > última` daquele jogador; sem retry, sem fila. Perda se resolve no próximo snapshot. Coerente com D-07 da fase 1 (on-change + heartbeat 2–5 Hz no sender).
- **D-10:** Pacotes críticos (chat `-5`, `CONFIG_SYNC` 16, `TELEPORT_REQUEST` 15, `SYNC_ABILITY` 10, `SYNC_LEVER` 11, `SYNC_DOOR` 12, `SYNC_WORLDEVENT` 14, `SKILL` 7, `COLOR` 6, `DISCONNECT` 4) = confiável com ACK + retry (até 3 tentativas com intervalo). Elimina o fire-and-forget atual.
- **D-11:** Relay = repasse imediato na chegada, sem agregação em tick fixo; mensagens de config/heartbeat só on-change + heartbeat 2–5 Hz.
- **D-12:** Ping como mensagem de sistema no novo envelope (`sendTicks` + `seq`, eco do servidor); HUD do cliente continua mostrando ms por jogador (substitui o `-7` legado no novo framing).

#### Arquitetura e compatibilidade

- **D-13:** Novo core separado em `Transport` (UDP + framing + codecs) / `Session` (clientes, heartbeat, timeout, token) / `Game` (regras Ori, handlers, comandos). Sem estático global mutável (`Server.Clients`, `LatesNetId`, `NetworkVars` estáticos morrem); instância injetável, testável, com `ILogger` + `CancellationToken`.
- **D-14:** Quebra total com versão: `magic+versão` recusa build antigo com mensagem explícita; sem fallback para envelope antigo; cliente e servidor sempre do mesmo build — **Reversibility:** one-way — builds antigos deixam de interoperar (coerente com D-12 da fase 1: "não usamos código legado").
- **D-15:** Redesenho total de IDs e payloads autorizado pelo usuário (não só o envelope): researcher/planner podem redefinir `PacketType`, ordem de campos e codecs, desde que cliente BepInEx e servidor mudem juntos no mesmo build e `docs/protocol.md` seja atualizado na mesma mudança — **Reversibility:** one-way — redefine o contrato publicado cliente-servidor.
- **D-16:** Operação mantida: `.NET 8`, CLI `--auto`/`--max-players`/`--port`, comandos de console (`coop`, `tp`, `dummy`, `clientcolors`, `entitysync`), logs em arquivo + console com níveis, config persistida entre restarts (hoje `OriCoopServerModule.OnEnable` zera as opções — deixar de zerar).

### Agent's Discretion

- Nenhum "você decide" nesta rodada — o usuário escolheu todas as opções recomendadas exceto D-15 (optou por redesenho total em vez de manter IDs/ordem). Detalhes finos ficam com researcher/planner: valores exatos de magic/versão, tamanho do token, intervalos de retry, backoff, MTU/limite de 350 chars do chat, layout de logs/métricas, estrutura exata de pastas do novo core.

### Deferred Ideas (OUT OF SCOPE)

- Descoberta automática de servidores na LAN (hoje IPv4 manual) — futura fase.
- Jogo pela internet (port-forward, NAT traversal) — futura fase, ainda não validado.
- Sync de inimigos/entidades além do `ES` atual — futura fase; cobertura real em partidas longas ainda **a confirmar**.
- Submenu nativo "Ori Coop" do pause — fora deste escopo (usar F6 como alternativa funcional).
- Persistência de opções já entra nesta fase via D-16; o restante de "matriz de compatibilidade Ori/Unity/assemblies" fica para operação futura.
</user_constraints>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| UDP socket I/O, framing, codec | API / Backend (dedicated server process) | — | Single owner of bytes-on-wire; Unity client only mirrors the codec |
| Session lifecycle (handshake, HB, timeout, token) | API / Backend | — | Authority for IDs and membership lives server-side (D-05–D-08) |
| Relay + reliability (seq, ACK/retry, drop-old) | API / Backend | Browser/Client (display only) | Server decides what forwards; client only applies latest snapshot per player |
| Game rules (teleport auth, SYNC_* gating, dummy bot, config) | API / Backend (Game layer) | — | Server is authority for config/IDs/teleport per docs/architecture.md |
| Snapshot production/consumption (Sein read, puppet apply) | Browser / Client (Unity) | — | Untouched by rewrite except codec; sender stays on-change + HB |
| Connection UI, HUD ping, nick display | Browser / Client (Unity) | — | Presentation only; must preserve pause-surviving network thread |
| Config persistence file | API / Backend (server working dir) | — | `serverconfig.json` next to exe; never in game client |

## Project Constraints (from AGENTS.md)

1. **`docs/` is mandatory:** every behavior-affecting change must update the matching `docs/` page in the same change; new uncovered subjects get a new page; unconfirmed behavior is marked "a confirmar"; `docs/README.md` index updated; manual/automated tests recorded in `docs/operations.md`. Planner MUST include doc-update tasks per plan (especially `docs/protocol.md` per D-15).
2. **Deploy-after-build is mandatory:** every successful compile of server or client DLL must be immediately copied into the game install (`<ORI_DIR>\BepInEx\plugins\`, `<ORI_DIR>\Server\`); known roots `C:\Program Files (x86)\Steam\steamapps\common\Ori DE`, `D:\SteamLibrary\steamapps\common\Ori DE`; close `OriDE.exe`/server before overwriting. Planner MUST include deploy steps in every build task.
3. **Zero-NuGet corollary (derived):** `docs/operations.md` documents an offline fallback build via raw `csc.dll` + ref packs with no package restore. Any NuGet dependency would break that path — the new core MUST use BCL-only APIs (verified available, see Standard Stack).

## Standard Stack

### Core (all BCL, .NET 8 — zero NuGet)

| Library / API | Version | Purpose | Why Standard |
|---------------|---------|---------|--------------|
| `System.Net.Sockets.UdpClient` + `ReceiveAsync(CancellationToken)` / `SendAsync` | .NET 8 (8.0.31 runtime present) [VERIFIED: local SDK 8.0.425 compile+run probe] | UDP socket I/O | Locked by D-01; modern async overload removes the `BeginReceive` callback entirely |
| `System.Threading.Channels` (`Channel.CreateBounded<T>`, `SingleReader/SingleWriter`, `BoundedChannelFullMode`) | in-box since .NET Core 3.0 [VERIFIED: local SDK 8.0.425 compile+run probe] | Receive→process handoff with backpressure | Locked by D-04; `DropOldest`/`DropNewest` bounds memory under packet flood |
| `System.Buffers.Binary.BinaryPrimitives` + `Span<byte>` / `ReadOnlySpan<byte>` | in-box since .NET Core 2.1 [VERIFIED: local SDK 8.0.425 compile+run probe] | Fixed-header encode/decode, explicit LE | Locked by D-03; no-alloc slicing replaces `List<byte>` + `BitConverter` |
| `System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>` | in-box since .NET 4 [VERIFIED: local SDK 8.0.425 compile+run probe] | Session table | Locked by D-05; lock-free reads on the hot relay path |
| `System.Security.Cryptography.RandomNumberGenerator.GetBytes` | in-box [VERIFIED: local SDK 8.0.425 compile+run probe] | Per-session token (D-08) | Never hand-roll randomness for tokens |
| `System.Text.Json` | in-box since .NET Core 3.0 [ASSUMED] | `serverconfig.json` persistence (D-16) | BCL, no package; fallback is a trivial `key=value` file if planner prefers |
| `System.Threading.CancellationTokenSource` (linked) | in-box [VERIFIED: local SDK 8.0.425 compile+run probe] | Clean shutdown (D-04) | Standard cooperative cancellation |

### Supporting (client side, .NET 3.5 / C# 5 — no new references)

| Library / API | Purpose | When to Use |
|---------------|---------|-------------|
| `BinaryWriter` / `BinaryReader` over `MemoryStream` + `BitConverter` + ASCII | Client envelope codec mirroring the 24-byte header | All new client send/receive paths in `NetworkService.cs` (no `Span`, no `?.`, no `$""`) |
| Existing `Thread` + `ReceiveTimeout` receive loop | Keep client network thread model | Heartbeat/ping MUST stay on this thread (pause survival — see Pitfalls) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Pure UDP + minimal ACK (D-01) | LiteNetLib / KCP / SteamNetworking | Would give congestion control + reliable channels, but client is .NET 3.5/C# 5 Unity Mono — no clean port; rejected by locked D-01 |
| `Channel` handoff (D-04) | Direct dispatch in receive loop | Simpler but couples socket timing to handler cost; a slow handler (e.g., dummy tick) would delay/truncate receives — Channel is correct |
| `ConcurrentDictionary` sessions (D-05) | `Dictionary` + `lock` | Viable but strictly worse on the hot path; no reason to avoid CD |
| `System.Text.Json` config | Hand-rolled `key=value` file | JSON is BCL and debuggable; only fall back if planner wants zero-serialization risk |

**Installation:** none — BCL only. No `npm`/`dotnet add` step exists for this phase.

**Version verification:** `dotnet --version` → `8.0.425`; runtimes include `Microsoft.NETCore.App 8.0.31`; ref packs `Microsoft.NETCore.App.Ref` present [VERIFIED: `dotnet --list-runtimes` + packs dir listing this session]. API probe compiled with 0 warnings/errors and ran `ALL_APIS_OK`.

## Package Legitimacy Audit

> This phase installs **zero external packages** — new core is BCL-only per D-01 and the zero-NuGet corollary above. There is nothing to audit.

| Package | Registry | Verdict | Disposition |
|---------|----------|---------|-------------|
| *(none — `System.*` BCL only)* | — | — | Approved (no install step; planner must NOT add one) |

**Packages removed due to SLOP verdict:** none.
**Packages flagged as suspicious (SUS):** none.
*Planner note: if any plan task proposes adding a NuGet package (logging, CLI parsing, RUDP), it violates D-01 + the offline-build constraint and must be rejected or escalated to the user.*

## Architecture Patterns

### System Architecture Diagram

```text
                    Unity game clients (.NET 3.5 / C# 5)              Dedicated server (.NET 8)
                    ───────────────────────────────────              ─────────────────────────
 Se
...[truncated 20887 chars]