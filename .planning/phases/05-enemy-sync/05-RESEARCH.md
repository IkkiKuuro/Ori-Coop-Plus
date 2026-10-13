# RESEARCH: Phase 05 — enemy-sync

**Researched:** 2026-10-13
**Phase:** 05-enemy-sync
**Status:** Complete — decisions captured in 05-CONTEXT.md

## Canonical References (MANDATORY — downstream agents MUST read before planning or implementing)

### Codebase References
- `src/OriCoopPlus/NetworkService.cs` — event `EntitySyncChanged` (linha 94, 974, 992); `ReadServerPacket`/`WriteServerPacket`; padrões `WriteLegacyString` (int32 length + ASCII); restrições C# 5 (sem `$""`, sem `?.`, Action no máximo 4 parâmetros).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — `ApplySnapshot`, `ApplyAnimation`, `SetAnimation`, catálogo de clipes e fallback ( onde o fail-closed de D-15 da fase 1 se apoia).
- `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` — catálogo e fallback para animação desconhecida.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/ConfigStore.cs` — flag `EntitySync` no bit 7; `SetEntitySync`, `SetFlag(7, value)`.
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` — `_network.EntitySyncChanged += OnEntitySyncChanged` (linha 137); inscrição/remoção de event (linhas 285, 163).
- `docs/animation-enemies-damage-data.md` — tipos de enemy (AcidSlugEnemy, RammingEnemy, DashOwlEnemy, DropSlugEnemy, FishEnemy, FloatingRockTurretEnemy/FloatingRockLaserEnemy, JumperEnemy com estados Charging/Idle/Stomped/Stunned/Thrown/RunBack/Shooting/Stomped/Stunned/Thrown/Walk), `SwarmEnemy` (+ `SwarmEnemyManager`), `WormEnemy`/`MortarWormEnemy`, `KamikazeSootEnemy`/`ChargingSootEnemy`, `SlugEnemy`, `StarSlugEnemy`, `OwlEnemy`/`DropOwlEnemy`, `GroundEnemy`, `DeadBatEnemy`, `EnemyZone`, `EnemyStopper`, `DeadEnemyRagdoll`. Anotação: **nenhuma classe `Boss`** (zero tipos — já registrado).
- `docs/game-entry-map.md` § MoonGuid(A,B,C,D) — "Todo objeto sincronizável é identificado por 4 ints. MoonGuid.Serialize grava A,B,C,D; WorldEventsManager persiste cada entrada como MoonGuid + Value. O fio do mod carrega o GUID desmontado em ints — é assim que remetente e receptor falam do **mesmo** objeto." § enemies — ponto de partida para definição de sync de entidades.
- `docs/protocol.md` — envelope 24B `0x4F43`/v2, regra "mudar cliente+servidor juntos", ordem de campos, `PLAYER_STATE` 18, tabela de IDs removidos (1, 2, 3, 5, -1..-7 nunca reutilizar), confiabilidade por pacote (críticos com ACK vs snapshots unreliable).
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — IDs atuais (base do ID novo autorizado).
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` — padrão de payload compacto (referência para o payload mínimo do piloto).

### Prior Decisions (carried forward)
- **Phase 01:** Puppets são visual-only: nenhum dano, colisão ou mutação de estado de jogo remoto. Fail-closed em animação desconhecida.
- **Phase 02:** 24-byte UDP envelope, handshake 3-way, IDs never reused, SysAck + retry 250ms x3 para pacotes críticos.
- **Phase 03:** Central event bus, Harmony detection (sem polling), local-only publish, unreliable PLAYER_EVENT 19. Fail-closed desconhecido.
- **Phase 04:** WorldSyncManager injetado via Harmony, main-thread pump (`EnqueueMainThread`), `_openedDoors` keyado por `MoonGuid(A,B,C,D)`, eco-gating (se estado local == recebido → suprimir), histórico no Join para late-joiners.

### Grey Areas — Already Decided (no research needed)
Already decided via `/gsd-discuss-phase 5` (2026-10-13):
1. **Cobertura do EntitySync:** posição + state de animação apenas (1 byte `ActionVisualState` enum). Sem saúde, sem dano, sem flag morte. Sincronização visual-only, consistente com política de puppets da fase 01.
2. **Identificador de inimigo:** usar `MoonGuid(A,B,C,D)` existente — padrão já estabelecido pela fase 04 para portas, alavancas e eventos mundiais. Reutilização evita novo esquema.
3. **Estados de morte/fase:** apenas posição + animação — morte trata como vanilla (reset). Sincronização visual só, sem efeito gameplay. Próxima fase (06) tratará isso separadamente.

## Research Gaps (if any — all decisions already captured)
No gray areas remain undiscussed — all 3 areas were resolved in the discuss-phase workflow. Researcher may still identify codebase integration details (field offsets, enum values, packet layouts) during implementation, but these are not blockers for planning.

## Downstream Requirements
- **REQ-06:** Enemy/boss sync (derived from game-entry-map, animation-enemies-damage-data). Status: open. Cobertura: posição + animação via EntitySync; identificador MoonGuid; morte = vanilla.

---
*Gerado por `/gsd-discuss-phase` (modo default) em 2026-10-13. Todas as áreas cinzentas resolvidas. RESEARCH.md pronto para uso de planner.*