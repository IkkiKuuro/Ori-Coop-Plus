# Phase 04 — world-sync-application — Context (bootstrapped 2026-10-13)

## Carrying forward from prior phases
- **Phase 01** (anim-sync): remote movement animation sync via ActionVisualState enum (1 byte). Puppet reproduces local player's animation; unknown clips keep last animation. Fail-closed.
- **Phase 02** (server-rewrite): async UDP transport, 24-byte envelope, handshake, reliable critical packets (SysAck + retry), unreliable sequenced PLAYER_STATE 18. No world-state sync beyond player.
- **Phase 03** (player-event-core): core + Spirit Flame pilot. Central event bus, Harmony detection (no polling), local-only publish, unreliable PLAYER_EVENT 19. Fail-closed on unknown.

## Decisions — world-sync-application (Phase 04)

### 1. Estrutura dos aplicadores
- **Choice:** Novo `WorldSyncManager` (padrão legado), injetado no `OriCoopPlugin` via Harmony.
- **Responsabilidades:**
  - Consome pacotes `SendSyncLever`, `SendSyncDoor`, `SendSyncWorldEvent` lidos por `NetworkService`.
  - Chama `SetLeverDirection` / aplica `CurrentState` em GameObjects via **main-thread pump** (`EnqueueMainThread` — mesmo padrão do `PlayerEventCore` da fase 03).
  - Mantém dicionário `_openedDoors` keyado por `MoonGuid(A,B,C,D)`.
  - Gating de eco: se o estado local já equivale ao recebido, não re-aplica (evita oscilação).
- **Arquivo sugerido:** `src/OriCoopPlus/OriCoopBepInEx/WorldSyncManager.cs` (novo).

### 2. Late-joiner e estado inicial
- **Choice:** Servidor guarda último estado por objeto (`MoonGuid + Direction/Value`) e envia no `Join`.
- **Detalhes:**
  - `Session` object persiste `Dictionary<MoonGuid, LeverState>` onde `LeverState = { Direction, LastSyncTick }`.
  - No `OnPlayerJoin`, o servidor envia `SyncLever/Door/WorldEvent` históricos para todos os objetos já alterados.
  - Cliente aplica o histórico antes de passar a processar eventos futuros.
  - Se não houver histórico para um GUID, assume estado vanilla (reset).
- **Arquivo sugerido:** `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Session.cs` (já existente — estender).

### 3. Detectores e gating de eco
- **Choice:** Patches `Harmony` em `OnPushLever* / FixedUpdate` que disparam `SendSyncLever/Door/WorldEvent`.
- **Mecanismo:**
  - Patches identificam `Lever` / `Door` / `WorldEvent` vanilla via type/name matching.
  - Leitura de `CurrentState` (field no objeto vanilla) após cada push — compara com último valor enviado; se igual, **suprime** o evento (eco-gating).
  - `MoonGuid(A,B,C,D)` desmontado a partir do endereço do objeto ou field `guid` — usado como chave de dedup.
  - Não polling: evento só é disparado pelo patch de empurrar, não por sondagem periódica.
- **Arquivo sugerido:** Harmony patches em `src/OriCoopPlus/OriCoopBepInEx/Patches/WorldSyncPatches.cs`.

### 4. Estratégia de teste
- **Choice:** `2 clientes + DummyBot + SmokeProbe`.
- **Cenários:**
  - Cliente A puxa alavanca → estado refletido no Cliente B + DummyBot.
  - Cliente B entra depois (late-joiner) → recebe histórico + estado aplicado.
  - DummyBot valida animação/sync após cada operação.
  - SmokeProbe: cenários autônomos — puxar alavanca, abrir porta, disparar evento mundial.
  - Falsos positivos: eco-gating verificado (mesmo estado → sem re-aplicar).

## Constraints (inherited)
- Protocol: 24-byte UDP envelope, 3-way handshake, critical packets reliable (SysAck 103 + retry 250ms x3).
- Packet IDs never reused (from `docs/protocol.md`).
- Pitch-closed on unknown: fail if MoonGuid not recognised.
- One-way break with old builds authorized (same constraint as phase 02).

## Context pointers
- **Spec:** `docs/world-sync.md` (já existente — seção "Aplicadores").
- **Codebase:** `src/OriCoopPlus/NetworkService.cs` (linhas 990-1036 — receptor atual que valida e descarta).
- **Harmony patches existentes:** `src/OriCoopPlus/OriCoopBepInEx/Patches/` (fase 01‑03).
- **Intel:** `.planning/intel/` + `.planning/INGEST-CONFLICTS.md` (0 blockers).
- **Roadmap:** `.planning/ROADMAP.md` — fase 04 proposta após 01‑03 completas.

## Next steps (for researcher/planner)
- Implementar `WorldSyncManager` injetado via Harmony, consumindo `NetworkService` reads e aplicando via main-thread pump.
- Estender `Session` para persistir e servir histórico de estado a late-joiners.
- Criar patches Harmony com `CurrentState` read + eco-gating.
- Planejar testes com 2 clientes + DummyBot + SmokeProbe (SyncLever, SyncDoor, SyncWorldEvent).
- Depois: phase 05 (enemy sync) e phase 06 (damage/health/save sync).

---
*Gerado por `/gsd-discuss-phase` (modo default) em 2026-10-13. Áreas: 4/4 concluídas. CONTEXT.md pronto para uso de researcher e planner.*