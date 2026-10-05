# Phase 2: Reescrita do servidor — Discussion Log

**Date:** 2026-10-05
**Phase:** 2-server-rewrite (sem ROADMAP.md formal; diretório `.planning/phases/02-server-rewrite/`)

## Áreas selecionadas

Usuário selecionou as 4 áreas: Transporte e framing, Sessão e reconexão,
Confiabilidade por pacote, Arquitetura e compat.

## Transporte e framing

1. Base do transporte — opções: UdpClient async moderno / Socket puro SAEA /
   Lib RUDP → **escolhido: UdpClient async moderno** (compatível com cliente
   Unity/.NET 3.5, sem dependência externa).
2. Envelope — novo com magic+versão+seq / manter legado / você decide →
   **escolhido: novo com magic+versão+seq** (one-way, recusa build antigo).
3. Serialização — BinaryPrimitives+Spans / manter Packet / você decide →
   **escolhido: BinaryPrimitives + Spans**, string int32+ASCII preservado no
   payload salvo redesenho D-15.
4. Loop — single loop async+Channel / threadpool por pacote / você decide →
   **escolhido: single loop async + Channel** com CancellationToken.
5. Checkpoint: Próxima área (sem perguntas extras).

## Sessão e reconexão

1. Slots fixos vs dinâmicas → **dinâmicas por ID crescente**,
   ConcurrentDictionary, limite por count.
2. Handshake 3 vias vs 2 etapas atual → **Hello+Welcome+Confirm** com versão,
   token e nick.
3. Heartbeat/timeout → **HB 2s + timeout 10s + resume com novo ID** (re-sync,
   sem segurar ID antigo).
4. Validação de endpoint → **endpoint fixo + token**; troca de IP exige novo
   handshake.
5. Checkpoint: Próxima área.

## Confiabilidade por pacote

1. PLAYER_STATE → **unreliable + seq, descarta velho**, sem retry.
2. Críticos (chat/config/teleporte/abilities/doors) → **confiável com
   ACK+retry (3x)**.
3. Ritmo → **relay imediato + HB 2–5 Hz** (coerente com D-07 fase 1).
4. Ping → **novo ping no envelope** (sendTicks+seq, eco; HUD mantém ms).
5. Checkpoint: Próxima área.

## Arquitetura e compat

1. Estrutura → **Transport/Session/Game**, sem estático global, instância
   injetável/testável.
2. Compat → **quebra total + versão** (sem fallback; mesmo build).
3. IDs/payloads → **redesenhar tudo** (usuário autorizou redefinição completa
   de PacketType/ordem/campos; cliente+servidor juntos; atualizar
   docs/protocol.md).
4. Operação → **manter CLI+comandos, logs estruturados**, .NET 8, config
   persistida (não zerar no OnEnable).
5. Fechamento: "Estou pronto p/ contexto" (sem áreas extras).

## Notas do agente

- Prioridade D-15 (redesenho total) conflita parcialmente com a hipótese
  inicial de "manter IDs+ordem"; registrada como decisão explícita do usuário
  com reversibilidade one-way.
- Sem "você decide" nesta rodada — discreção do agente limitada a detalhes
  finos (magic, token, retry, MTU, logs).
- Deferred: LAN discovery, internet/NAT, sync inimigos/entidades, submenu
  pause — futuras fases.

## Arquivos gerados

- `.planning/phases/02-server-rewrite/02-CONTEXT.md` (canônico)
- `docs/server-rewrite-CONTEXT.md` (espelho; exigência AGENTS.md)
- `docs/README.md` (índice atualizado)
