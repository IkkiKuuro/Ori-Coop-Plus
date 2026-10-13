# Phase 06 — damage-health-save — Context (bootstrapped 2026-10-13)

## Carrying forward from prior phases
- **Phase 01** (anim-sync): remote movement animation sync via ActionVisualState enum (1 byte). Puppet reproduces local player's animation; unknown clips keep last animation. Fail-closed. No damage/collision from remote events.
- **Phase 02** (server-rewrite): async UDP transport, 24-byte envelope, handshake, reliable critical packets (SysAck + retry), unreliable sequenced PLAYER_STATE 18. No world-state sync beyond player.
- **Phase 03** (player-event-core): core + Spirit Flame pilot. Central event bus, Harmony detection (no polling), local-only publish, unreliable PLAYER_EVENT 19. Fail-closed on unknown. Pilot = Spirit Flame projectile (visual fake, no gameplay effect).
- **Phase 04** (world-sync-application): `WorldSyncManager` injected via Harmony, consuming `NetworkService` reads and applying via main-thread pump (`EnqueueMainThread`). `_openedDoors` keyed by `MoonGuid(A,B,C,D)`. Eco-gating: if local state == received state, suppress. Server persists last state per `MoonGuid` and sends history on `Join`. Test strategy: 2 clients + DummyBot + SmokeProbe.
- **Phase 05** (enemy-sync): EntitySync coverage = posição + animation state only (1 byte ActionVisualState enum). Identifier = MoonGuid existente. Morte = vanilla (visual only). Test strategy: 2 clients + DummyBot + SmokeProbe (enemy spawn + movimentação + parada).

## Decisions — damage-health-sync (Phase 06)

### 1. Cobertura do Sync: Damage, Health, Energy
- **Choice:** Sincronizar `damage` (float), `health` (float), `energy` (float) por pacote confiável.
- **Rationale:** REQ-07 e REQ-08 derivam de `docs/animation-enemies-damage-data.md` e `docs/game-entry-map.md`. O jogador precisa ter vida/energia consistente entre clientes para mecânicas de respawn, revive e capacidade de usar habilidades. O padrão da fase 02 (SysAck + retry 250ms x3 para pacotes críticos) será usado para esse canal, diferentemente do unreliable PLAYER_EVENT 19 da fase 03.
- **Payload por pacote:** `damage: float` (0 = nenhum, negativo = cura), `health: float` (0.0 = morto, 1.0 = vida cheia), `energy: float` (0.0 = sem energia, 1.0 = energia cheia).
- **Falha cerrada:** se o pacote não chegar após retries, manter último estado conhecido (last known health/energy).
- **Arquivo sugerido:** `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` — novo ID de pacote (próximo livre após 18); `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — writer/reader; `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs` — relay.

### 2. Morte e Revive
- **Choice:** Sincronizar `isDead: bool` + `deathTime: float` (tempo desde death em ms). Revive é tratado como `health` positivo recebido.
- **Rationale:** REQ-07 pede sync de damage/death/respawn. O `isDead` flag permite coordenar a transição visual (anim de morte) e gameplay (colisão desabilitada, UI de downed). O `deathTime` permite timeout de revive após X segundos ou consumo de item de revive.
- **Falha cerrada:** se `health` chegar a 0 e `isDead` for verdadeiro, puppet entra em estado de ` ragdoll`/local avoidance; se `health` > 0 recebido após `isDead`, triggers revive sequence.
- **Arquivo sugerido:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — extender `ApplySnapshot` para `isDead` + `deathTime`; `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` — animação de morte + revive.

### 3. Save/Progresso: Pickups, Inventory, Abilities
- **Choice:** Sincronizar pickups pickup + inventory state (itens coletáveis, XP/skill points) + shared abilities flags — **relay only, sem aplicação local** para abilities (iguais à decisão da fase 04 para world-sync).
- **Rationale:** REQ-08 pega de `docs/game-entry-map.md`. O objetivo é que ambos os clientes tenham a mesma visão do que foi coletado e do progresso, mas a aplicação real (dar item, dar XP) será fatiada em fase futura (fase 07 ou posterior). O padrão "relay without application" garante que o mod não quebre jogos single-player ou modos competitivos onde a aplicação de abilities seria injusta.
- **Detalhes do payload:** `pickups_collected: int` (bitfield de IDs de pickup); `inventory_slots: fixed16` (quantidade/ID de cada item); `abilities_shared: bool` (8 bools: AllowTeleport, ShareAbilities, ShareStoryOnly, ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames, ClientColors, EntitySync — padrão da fase 02 CONFIG_SYNC 16).
- **Falha cerrada:** se pacote perder-se, client pede retransmissão via ACK (mesmo padrão da fase 02).
- **Arquivo sugerido:** `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/PlayerStateRelay.cs` — relay de pacote; `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — leitura/escrita.

### 4. Integração com Protocolos Existentes
- **Choice:** Novo ID de pacote `DAMAGE_SYNC = 20` (nunca reutilizar IDs — regra da fase 02), ou reutilizar estrutura do `PLAYER_STATE 18` com flags extras?
- **Rationale:** Manter backward compatibility e evitar quebra de contrato. A opção mais segura é **novo ID `DAMAGE_SYNC = 20`** com payload `damage + health + energy + isDead + deathTime`, enviado confiável (SysAck + retry). Isso não afeta o `PLAYER_STATE 18` existente (posição + animação do jogador).
- **Alternativa:** Estender `PLAYER_STATE 18` com 3 floats extras (damage, health, energy) — mas isso seria uma quebra de contrato one-way com builds antigos.
- **Decisão:** **Novo ID `DAMAGE_SYNC = 20`** confiável, separado de `PLAYER_STATE 18`.

### 5. Gating de Eco (Eco-Gating)
- **Choice:** Se o estado local de health/damage já equivale ao recebido, suprimir aplicação (igual eco-gating da fase 04).
- **Rationale:** Evita que danos múltiplos causem stack de healing/damage ou respawn oscilante quando ambos os clientes já estão sincronizados.

## Constraints (inherited)
- Protocol: 24-byte UDP envelope (fase 02), 3-way handshake, IDs never reused (fase 02).
- Packet IDs: Novo ID `DAMAGE_SYNC = 20` confiável (fase 06); `PLAYER_STATE 18` unchanged.
- MoonGuid(A,B,C,D) is the stable ID for synchronizable objects (fases 04‑05).
- One-way break with old builds authorized (fase 02).
- Puppet visuals are visual-only: damage/health/energy sync não muta gameplay diretamente — flags de `isDead` e aplicações de revive sao opt-in via Diagnostics (fase 01 constraint).
- Fail-closed on unknown: se MoonGuid ou pacote não reconhecido, manter último estado conhecido.
- REQ-07: Sync player damage, death/respawn, health/energy. Status: open.
- REQ-08: Sync pickups, inventory, XP/skill points, shared abilities beyond relay. Status: open.

## Context pointers
- **Spec:** `docs/animation-enemies-damage-data.md` (tipos de inimigo, dano, save — referência para structs de dano/save).
- **Spec:** `docs/game-entry-map.md` § enemies e § pickups/inventory/abilities — ponto de partida para definição de sync.
- **Codebase:** `src/OriCoopPlus/NetworkService.cs` — padrões de writer/reader (WriteLegacyString, ReadServerPacket); event EntitySyncChanged (já existente).
- **Codebase:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — ApplySnapshot, isDead handling (já existente para fase 03 Spirit Flame).
- **Codebase:** `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/NetworkHandler.cs` — relay de pacotes (fase 02‑04).
- **Harmony patches existentes:** `src/OriCoopPlus/OriCoopBepInEx/Patches/` (fase 01‑05).
- **Intel:** `.planning/intel/` + `.planning/INGEST-CONFLICTS.md` (0 blockers).
- **Roadmap:** `.planning/ROADMAP.md` — fase 06 proposta após 05 completas.
- **Requirements:** REQ-07 — Damage/health/energy sync (derived from animation-enemies-damage-data). REQ-08 — Save/progress sync: pickups, inventory, abilities (derived from game-entry-map). Status: open.

## Next steps (for researcher/planner)
- Implementar novo pacote `DAMAGE_SYNC = 20` com payload `damage + health + energy + isDead + deathTime`, confiável (SysAck + retry).
- Estender `NetworkService` para writer/reader do novo pacote; estender `NetworkHandler` no servidor para relay.
- Extender `RemotePlayerPuppet` para `isDead` + `deathTime` + animação de morte/revival.
- Implementar sincronização de pickups/inventory/abilities via relay (sem aplicação local).
- Planejar testes com 2 clientes: aplicação de dano, morte/revival, pickup collection, sync de abilities flags.
- Depois: phase 07 (abilities application) ou fechar o ciclo de sync de jogador completo.

---
*Gerado por `/gsd-discuss-phase` (modo default) em 2026-10-13. Áreas: 5/5 concluídas (01‑05). CONTEXT.md pronto para uso de researcher e planner.*