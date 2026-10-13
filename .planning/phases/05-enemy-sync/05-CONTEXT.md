# Phase 05 — enemy-sync — Context (bootstrapped 2026-10-13)

## Carrying forward from prior phases
- **Phase 01** (anim-sync): remote movement animation sync via ActionVisualState enum (1 byte). Puppet reproduces local player's animation; unknown clips keep last animation. Fail-closed. No damage/collision from remote events.
- **Phase 02** (server-rewrite): async UDP transport, 24-byte envelope, handshake, reliable critical packets (SysAck + retry), unreliable sequenced PLAYER_STATE 18. No world-state sync beyond player.
- **Phase 03** (player-event-core): core + Spirit Flame pilot. Central event bus, Harmony detection (no polling), local-only publish, unreliable PLAYER_EVENT 19. Fail-closed on unknown. Pilot = Spirit Flame projectile (visual fake, no gameplay effect).
- **Phase 04** (world-sync-application): `WorldSyncManager` injected via Harmony, consuming `NetworkService` reads and applying via main-thread pump (`EnqueueMainThread`). `_openedDoors` keyed by `MoonGuid(A,B,C,D)`. Eco-gating: if local state == received state, suppress. Server persists last state per `MoonGuid` and sends history on `Join`. Test strategy: 2 clients + DummyBot + SmokeProbe.

## Decisions — enemy-sync (Phase 05)

### 1. Cobertura do EntitySync
- **Choice:** Posição + state de animação apenas (1 byte `ActionVisualState` enum).
- **Rationale:** Mantém sincronização leve e visual-only, consistente com a política de puppets da fase 01 (nenhum dano, colisão ou mutação de estado de jogo remoto). O `EntitySync` event existente em `NetworkService.cs:94` é usado assim como está — sem extender com campos adicionais nesta fase.
- **Falha cerrada:** pacote de entity sync desconhecido mantém a última animação válida, nunca chuta para Idle genérico.
- **Arquivo que se beneficia:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — aplica estado de animation recebido; sem alteração de gameplay.
- **Código de suporte:** `NetworkService.EntitySyncChanged` event (linha 94) jà existente — inscrito em `OriCoopPlugin.cs:137`.

### 2. Identificador de inimigo
- **Choice:** Usar `MoonGuid(A,B,C,D)` existente — padrão já estabelecido pela fase 04 para portas, alavancas e eventos mundiais.
- **Rationale:** Reutilização do padrão evita introduzir novo esquema de identificação; `MoonGuid` já é o mecanismo de chave única para objetos sincronizáveis no mod. O `MoonGuid` é desmontado a partir do field `guid` do objeto vanilla ou do endereço do objeto.
- **Compatibilidade:** Entidades vanilla (inimigos) já têm fields associados no `Assembly-CSharp.dll` que podem ser mapeados para `int A, B, C, D`. Se um inimigo não tiver `MoonGuid` mapeado, fallback para hash do endereço do entity (still unique por sessão).
- **Arquivo de suporte:** `docs/game-entry-map.md` § enemies — já lista tipos de enemy (AcidSlugEnemy, RammingEnemy, DashOwlEnemy, etc.) e anota ausência de classe `Boss`.

### 3. Estados de morte/fase
- **Choice:** Apenas posição + animação — morte trata como vanilla (reset). Sincronização visual só, sem efeito gameplay.
- **Rationale:** Consistente com a política de puppets da fase 01 (remote events não mutam estado local) e a fail-closed da fase 03 (desconhecido mantém última animação válida). Morte do inimigo é tratada localmente pelo vanilla; o sync only garante que ambos os clientes mostrem a mesma animação de morte, mas não coordenam revive/respawn nesta fase.
- **Próxima fase (06):** Caso seja necessário coordenação de morte/respawn, isso será fatiado em fase separada (damage/health/save sync).
- **Arquivo afetado:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — aplica animação de morte recebida, mas não altera estado de vida ou colisão.

## Constraints (inherited)
- Protocol: 24-byte UDP envelope, 3-way handshake, critical packets reliable (SysAck 103 + retry 250ms x3).
- Packet IDs never reused (from `docs/protocol.md`).
- MoonGuid(A,B,C,D) is the stable ID for synchronizable objects (from `docs/game-entry-map.md`, `docs/world-sync.md`).
- One-way break with old builds authorized (same constraint as phase 02).
- Puppet visuals are visual-only: no damage, collision, or local game-state mutation from remote events (from phase 01 constraint).
- Fail-closed on unknown: if MoonGuid not recognised, keep last valid animation.

## Context pointers
- **Spec:** `docs/animation-enemies-damage-data.md` (lista de tipos de inimigo, anotação de que não há classe `Boss`).
- **Spec:** `docs/game-entry-map.md` § enemies — ponto de partida para definição de sync de entidades.
- **Codebase:** `src/OriCoopPlus/NetworkService.cs` (event `EntitySyncChanged` linha 94, 974, 992 — jà existente).
- **Codebase:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — aplica estado de animation no puppet.
- **Codebase:** `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs` — padrão de leitura do Sein a estender para o detector de entity sync.
- **Harmony patches existentes:** `src/OriCoopPlus/OriCoopBepInEx/Patches/` (fase 01‑03).
- **Intel:** `.planning/intel/` + `.planning/INGEST-CONFLICTS.md` (0 blockers).
- **Roadmap:** `.planning/ROADMAP.md` — fase 05 proposta após 04 completas.
- **Requirements:** REQ-06 — Enemy/boss sync (derived from game-entry-map, animation-enemies-damage-data). Status: open.

## Next steps (for researcher/planner)
- Implementar suporte a `MoonGuid` para entidades/inimigos no `NetworkService` e `EntitySync` pipeline.
- Estender `RemotePlayerPuppet` para aplicar animation state recebido via `EntitySync` (posição + 1 byte ActionVisualState).
- Verificar que `EntitySyncChanged` event (NetworkService.cs:94) está devidamente inscrito no `OriCoopPlugin` e que o fluxo de dados flui do packet receive até a aplicação no puppet.
- Planejar testes com 2 clientes: spawn de inimigo no cliente A → animation state sincronizado no cliente B.
- Depois: phase 06 (damage/health/save sync) — definir se morte/inimigo phases serão adicionadas.

---
*Gerado por `/gsd-discuss-phase` (modo default) em 2026-10-13. Áreas: 3/3 concluídas. CONTEXT.md pronto para uso de researcher e planner.*