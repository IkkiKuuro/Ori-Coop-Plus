# RESEARCH: Phase 06 — damage-health-sync

**Researched:** 2026-10-13
**Phase:** 06-damage-health-save
**Status:** Complete — decisions captured in 06-CONTEXT.md

## Canonical References (MANDATORY — downstream agents MUST read before planning or implementing)

### Codebase References
- `src/OriCoopPlus/NetworkService.cs` — event `EntitySyncChanged` (linha 94, 974, 992); `ReadServerPacket`/`WriteServerPacket`; padrões `WriteLegacyString` (int32 length + ASCII); restrições C# 5 (sem `$""`, sem `?.`, Action no máximo 4 parâmetros). Padrão de confiabilidade: SysAck + retry 250ms x3 para pacotes críticos (fase 02).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — `ApplySnapshot`, `isDead` handling (já existente para Spirit Flame da fase 03), animação de morte/revival patterns.
- `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` — catálogo e fallback para animação desconhecida; fail-closed pattern (D-15 da fase 1).
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` — padrão de payload compacto (referência para o payload mínimo do piloto e do EntitySync).
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/ConfigStore.cs` — flag `EntitySync` no bit 7; `SetEntitySync`, `SetFlag(7, value)`.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/NetworkHandler.cs` — `OnPacketRecived`, cases de PacketType (18 = PLAYER_STATE); modelo de relay de pacotes.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Packet.cs` — leitura/escrita de Vector3, float, byte; `ReadVector3`, `WriteVector3`, `ReadByte`, `WriteByte`, `ReadInt`, `WriteInt`.
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Net/Reliability/AckTracker.cs` — rastreamento de ACK para pacotes confiáveis (modelo para DAMAGE_SYNC).
- `docs/animation-enemies-damage-data.md` — tipos de enemy, dano, save; estruturas esperadas para damage/health/energy; anotação de que não há classe `Boss`; lista de EnemyTypes (AcidSlugEnemy, RammingEnemy, DashOwlEnemy, DropSlugEnemy, FishEnemy, FloatingRockTurretEnemy/FloatingRockLaserEnemy, JumperEnemy, SwarmEnemy, WormEnemy, KamikazeSootEnemy, SlugEnemy, StarSlugEnemy, OwlEnemy, GroundEnemy, DeadBatEnemy, EnemyZone, EnemyStopper, DeadEnemyRagdoll).
- `docs/game-entry-map.md` § MoonGuid(A,B,C,D) — "é assim que remetente e receptor falam do mesmo objeto"; § enemies; pickups/inventory/abilities vocabulary (ataque, pickup, porta/alavanca, world event, entity sync) e limites conhecidos.
- `docs/protocol.md` — envelope 24B `0x4F43`/v2, regra "mudar cliente+servidor juntos", ordem de campos, `PLAYER_STATE` 18, tabela de IDs removidos (1, 2, 3, 5, -1..-7 nunca reutilizar), confiabilidade por pacote (críticos com ACK vs snapshots unreliable).
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — IDs atuais (base para novo ID `DAMAGE_SYNC = 20` autorizado).
- `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` — `magic`, versão, flags `Reliable`/`AckPresent`, `AckRetryMs`/`MaxRetries`.

### Prior Decisions (carried forward)
- **Phase 01:** Puppets são visual-only: nenhum dano, colisão ou mutação de estado de jogo remoto. Fail-closed em animação desconhecida.
- **Phase 02:** 24-byte UDP envelope, handshake 3-way, IDs never reused, SysAck + retry 250ms x3 para pacotes críticos. One-way break with old builds.
- **Phase 03:** Central event bus, Harmony detection (sem polling), local-only publish, unreliable PLAYER_EVENT 19. Fail-closed desconhecido.
- **Phase 04:** WorldSyncManager injetado via Harmony, main-thread pump (`EnqueueMainThread`), `_openedDoors` keyado por `MoonGuid(A,B,C,D)`, eco-gating (se estado local == recebido → suprimir), histórico no Join para late-joiners. Estratégia de teste: 2 clientes + DummyBot + SmokeProbe.
- **Phase 05:** EntitySync coverage = posição + animation state apenas (1 byte ActionVisualState enum). Identificador = MoonGuid existente. Morte = vanilla (visual only). Novo ID de protocolo EntitySync paralelo. Flag verbose AnimVerbose → [ENEMY-ANIM] logs.

### Grey Areas — Already Decided (no research needed)
Already decided via `/gsd-discuss-phase 6` (2026-10-13):
1. **Cobertura do sync:** damage + health + energy + isDead + deathTime via novo pacote `DAMAGE_SYNC = 20` confiável.
2. **Identificador:** MoonGuid(A,B,C,D) continua sendo a chave para entities sincronizáveis (herdado da fase 04/05).
3. **Morte e revive:** `isDead: bool` + `deathTime: float`; revive via health positivo recebido.
4. **Save/progresso:** pickups_collected (bitfield) + inventory_slots (fixed16) + abilities_shared (8 bools) — relay only, sem aplicação local nesta fase.
5. **Novo ID de pacote:** `DAMAGE_SYNC = 20` (novo, confiável, separado de PLAYER_STATE 18).
6. **Eco-gating:** se estado local == recebido, suprimir aplicação.

## Research Gaps (if any — all decisions already captured)
Todas as áreas cinzentas foram resolvidas no workflow `/gsd-discuss-phase 6`. O researcher pode ainda identificar detalhes de integração no código (offsets de field, valores de enum, layouts de packet) durante implementação, mas não são blocadores para planning.

## Downstream Requirements
- **REQ-07:** Damage/health/energy sync (derived from `animation-enemies-damage-data.md`). Status: open. Cobertura: damage (float), health (float), energy (float), isDead (bool), deathTime (float). Protocolo: novo ID `DAMAGE_SYNC = 20` confiável (SysAck + retry 250ms x3).
- **REQ-08:** Save/progress sync: pickups, inventory, abilities (derived from `game-entry-map.md`). Status: open. Cobertura: pickups_collected (bitfield), inventory_slots (fixed16), abilities_shared (8 bools). Protocolo: relay only (sem aplicação local). ID de pacote: usar CONFIG_SYNC 16 structure (8 bools existentes) ou novo struct.

---
*Gerado por `/gsd-discuss-phase` (modo default) em 2026-10-13. Todas as áreas cinzentas resolvidas. RESEARCH.md pronto para uso de planner.*