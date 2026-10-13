---
phase: 06
plan: "06"
type: tracer
wave: 1
depends_on: [05]
files_modified:
  - src/OriCoopPlus/OriCoopShared/PacketType.cs
  - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
  - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs
  - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
  - docs/animation-enemies-damage-data.md
  - docs/protocol.md
autonomous: false
requirements: [REQ-07, REQ-08]
---
<objective>
Provar de ponta a ponta que sync de dano, saúde e energia está idêntico entre cliente e servidor; e que pickups/inventory/abilities estão sincronizados via relay. Ao fim, ambos os clientes refletem o mesmo estado de health/energy do host, pickups coletados são visíveis nos dois clientes, e flags de ability shared são consistentes — sem sprite aleatório, sem desvio de dano, sem oscilação de estado em teste com 2 clientes.
</objective>

<tasks>
<task name="Novo ID de pacote DAMAGE_SYNC = 20">
<read_first>
- src/OriCoopPlus/OriCoopShared/PacketType.cs (enum de IDs atuais)
- docs/protocol.md (regra IDs never reused, envelope 24B)
- CONTEXT.md phase 06: novo ID DAMAGE_SYNC = 20 confiável
</read_first>
<action>
Adicionar `DAMAGE_SYNC = 20` ao enum `PacketType` em PacketType.cs. Documentar que esse novo ID usa confiabilidade SysAck + retry 250ms x3 (regra da fase 02) e payload `damage: float + health: float + energy: float + isDead: bool + deathTime: float`. Manter anotação de que esse é um protocolo paralelo ao PLAYER_STATE 18.
</action>
<verify>Build do cliente via build.ps1 sem erros; PacketType.cs compilado com `DAMAGE_SYNC = 20`.</verify>
<acceptance_criteria>
- Source: PacketType.cs contém `DAMAGE_SYNC = 20` enum value
- Behavior: Novo ID não conflita com PLAYER_STATE 18; builds antigos podem ser instalados lado a lado (one-way break)
</acceptance_criteria>
</task>

<task name="NetworkService: writer/reader do pacote DAMAGE_SYNC">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs (ReadServerPacket, WriteServerPacket, WriteLegacyString, EntitySyncChanged event)
- docs/protocol.md (envelope 24B, confiabilidade por pacote)
- CONTEXT.md phase 06: payload damage+health+energy+isDead+deathTime
</read_first>
<action>
Em NetworkService.WriteServerPacket, adicionar escrita do pacote DAMAGE_SYNC: `damage: float` (0 = nenhum, negativo = cura), `health: float` (0.0 = morto, 1.0 = vida cheia), `energy: float` (0.0 = sem energia, 1.0 = energia cheia), `isDead: bool`, `deathTime: float` (ms desde death). Em NetworkService.ReadServerPacket, adicionar leitura no mesmo ordem. Implementar confiabilidade SysAck + retry 250ms x3 (modelo do AckTracker da fase 02). Manter `PLAYER_STATE 18` escrevendo/lerendo inalterado.
</action>
<verify>Build do cliente sem erros; pacote DAMAGE_SYNC desmontado corretamente em LogOutput.log com SysAck + retry.</verify>
<acceptance_criteria>
- Source: ReadServerPacket lê damage (float) + health (float) + energy (float) + isDead (bool) + deathTime (float) no payload DAMAGE_SYNC
- Source: WriteServerPacket escreve no mesmo ordem
- Behavior: SysAck + retry 250ms x3 ativo para esse pacote; build do cliente/sem erros
</acceptance_criteria>
</task>

<task name="NetworkHandler: relay do pacote DAMAGE_SYNC no servidor">
<read_first>
- src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/NetworkHandler.cs (OnPacketRecived, cases de PacketType 18)
- src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Packet.cs (ReadVector3, WriteVector3, ReadByte, WriteByte)
- CONTEXT.md phase 06: relay sem fundir nem inferir
</read_first>
<action>
Em `OnPacketRecved`, adicionar `case (PacketType)20`: ler na ordem `damage: float` (ReadFloat), `health: float` (ReadFloat), `energy: float` (ReadFloat), `isDead: bool` (ReadByte/boolean), `deathTime: float` (ReadFloat). Atualizar `player.Health`, `player.Energy`, `player.IsDead`, `player.DeathTime`. Reescrever pacote 20 com mesmos bytes e `player.Nick` e `SendToAll(player.Id, ...)`. NÃO fundir nem inferir nada — apenas relay dos bytes recebidos.
</action>
<verify>Build do servidor `dotnet build` sem erros; teste `--auto --max-players 2` mostra relay de pacote 20 sem exceção `Could not read value`.</verify>
<acceptance_criteria>
- Source: NetworkHandler.cs contém `case (PacketType)20` com leitura dos 5 campos na ordem correta
- Behavior: segundo cliente recebe pacote 20 com damage/health/energy/isDead do primeiro
</acceptance_criteria>
</task>

<task name="RemotePlayerPuppet: isDead + health/energy + animação de morte/revival">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs (já tem ApplySnapshot/anim authority, fields _confirmedAnimState/_pendingAnimState/_pendingSince 0.15s da fase 05)
- CONTEXT.md phase 06: isDead + deathTime + flags de ability shared
- FASE 01 DECISION: puppets visual-only, fail-closed, sem dano/colisão remoto direto
</read_first>
<action>
Em RemotePlayerPuppet, adicionar campos `health: float`, `energy: float`, `isDead: bool`, `deathTime: float`. Em `OnDamageSync`, ler payload DAMAGE_SYNC e preencher esses campos. `ApplySnapshot` passa a receber `health` e `energy` e atualizar o estado local (sem mutar colisão ou gameplay direto — apenas UI e visual). Quando `isDead` mudar de false para true, disparar animação de morte (`Play("Death")`). Quando `health` chegar > 0 após `isDead` ser true, disparar sequência de revive (`Play("Revive")`, tempo `deathTime`). Fail-closed: se `health` for recebido fora de ordem (ex: deathTime futuro), manter último estado conhecido. Sincronizar flags `abilities_shared: bool[8]` e aplicar apenas via Diagnostics/verbose (não mutar gameplay).
</action>
<verify>Teste 2 clientes: aplicação de dano → health/reflect no outro cliente; death → animação de morte; revive após health > 0 → animação de revive; pickups flags refletidas; 60 s teste sem sprite aleatório.</verify>
<acceptance_criteria>
- Behavior: dano aplicadoreflete no cliente remoto (health decrementa idêntico)
- Behavior: death → animação de morte disparada; isDead = true
- Behavior: revive (health > 0 após death) → animação de revive disparada
- Behavior: flags abilities_shared refletidas no HUD/verbose (não mutam gameplay)
</acceptance_criteria>
</task>

<task name="Pickups/Inventory/Abilities Relay (REQ-08)">
<read_first>
- src/OriCoopDedicatedServer/OriCoopDedicatedServer.Net/Game/PlayerStateRelay.cs (relay de pacotes existentes)
- docs/game-entry-map.md § pickups/inventory/abilities vocabulary
- CONTEXT.md phase 06: REQ-08 — sync pickups/inventory/abilities, relay only
- FASE 04 DECISION: relay without application for world-sync events
</read_first>
<action>
Em PlayerStateRelay, adicionar suporte a ler/escrever pickups_collected (bitfield de IDs de pickup), inventory_slots (fixed16 de item ID/quantidade), abilities_shared (8 bools: AllowTeleport, ShareAbilities, ShareStoryOnly, ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames, ClientColors, EntitySync). Escrever pacote CONFIG_SYNC 16 (já existente no NetProtocol.cs) com esses 8 bools + bitfield de pickups. No cliente, ler esses campos mas APENAS atualizar UI/verbose — NÃO aplicar efeitos de item, dar XP ou alterar abilities locais. Isso mantém consistência visual entre clientes sem quebrar jogos single-player ou modos competitivos.
</action>
<verify>Build do cliente/sem erros; pacote CONFIG_SYNC 16 contém pickups + inventory + abilities_shared; deploy nas pastas do jogo sem erros.</verify>
<acceptance_criteria>
- Source: PlayerStateRelay escreve pacote CONFIG_SYNC 16 com pickups_collected (bitfield) + inventory_slots (fixed16) + abilities_shared (8 bools)
- Behavior: cliente recebe pacote; flags abilities_shared atualizadas no HUD/verbose apenas; pickups_collected listados em log verbose; NENHUM efeito de gameplay (item dado, XP dado, ability desbloqueada)
</acceptance_criteria>
</task>

<task name="Log verboso + docs (Diagnostics/AnimVerbose extensão)">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs (Config.Bind pattern, IsAnimVerbose)
- src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs
- docs/protocol.md
- docs/animation-enemies-damage-data.md
- CONTEXT.md phase 06: next phase (07+) = abilities application
</read_first>
<action>
Estender `Config.Bind("Diagnostics", "DamageVerbose", false, ...)` e método `OriCoopPlugin.IsDamageVerbose()`. Em `NetworkService.OnDamageSync`, quando verbose, `LogInfo("[DAMAGE] P{playerId} recv={damage} health={health} energy={energy} isDead={isDead} deathTime={deathTime}")` via `string.Format` (sem `$""`). Em `RemotePlayerPuppet.OnDamageSync`, quando verbose, `LogInfo("[PUPPET] P{playerId} health={health} isDead={isDead}")`. Atualizar `docs/protocol.md`: nova linha na tabela de pacotes (`20 DAMAGE_SYNC`) + parágrafo de ordem de campos + nota de que PLAYER_STATE 18 permanece inalterado. Atualizar `docs/animation-enemies-damage-data.md`: nota de que coverage agora inclui damage/health/energy sync para players (antes só coverage era enemy animation state).
</action>
<verify>`docs/protocol.md` contém `20 DAMAGE_SYNC`; com `DamageVerbose=true`, `LogOutput.log` mostra linhas `[DAMAGE]`.</verify>
<acceptance_criteria>
- Source: `protocol.md` contém `20 DAMAGE_SYNC` + ordem de campos
- Docs: `animation-enemies-damage-data.md` nota a cobertura damage/health/energy
- Behavior: flag verbose controla logs [DAMAGE] e [PUPPET]; desligada por padrão
</acceptance_criteria>
</task>
</tasks>

<verification>
1. `build.ps1` (cliente) e `dotnet build` (servidor) sem erros.
2. Deploy nas pastas do jogo com processos fechados.
3. Servidor `--auto --max-players 2 --port 7777` + 2 clientes locais:
   dano → health reflete, death → animação, revive → health > 0 animação, pickups flags refletidas, nenhum sprite aleatório em 60 s.
4. [`OBSERVABILITY][NET-METRICS]` com `DAMAGE_SYNC dropped: 0` em rede local; `CONFIG_SYNC 16` flags corretas.
</verification>

<success_criteria>
- DAMAGE_SYNC packet trafega de ponta a ponta com damage + health + energy + isDead + deathTime
- Puppet remote reflete health/energy/isDead com animação de morte/revival adequada
- MoonGuid keyed dedup mantém estado consistente (herdado da fase 04/05)
- Pickups/inventory/abilities sincronizados via relay (CONFIG_SYNC 16) — aplicação apenas via verbose/debug
- Protocolo documentado; build par cliente+servidor instalado
</success_criteria>

<must_haves>
truths:
  - "DAMAGE_SYNC payload: damage (float) + health (float) + energy (float) + isDead (bool) + deathTime (float)"
  - "Novo ID PacketType.DAMAGE_SYNC = 20 (confiável, SysAck + retry 250ms x3)"
  - "PLAYER_STATE 18 permanece inalterado — protocolo paralelo"
  - "CONFIG_SYNC 16 flags + pickups bitfield para REQ-08 (relay only)"
  - "Fail-closed: health fora de ordem mantém último estado conhecido"
  - "Transição logada atrás de flag DamageVerbose desligada por padrão"
</must_haves>

<artifacts this phase produces>
- PacketType.DAMAGE_SYNC = 20 (enum value, OriCoopShared)
- NetworkService writer/reader para pacote 20 (damage+health+energy+isDead+deathTime)
- NetworkHandler case 20 no servidor (relay dos bytes recebidos)
- RemotePlayerPuppet.health + energy + isDead + deathTime + animação de morte/revival
- PlayerStateRelay CONFIG_SYNC 16 com pickups_collected + inventory_slots + abilities_shared
- Diagnostics/DamageVerbose (BepInEx config key) + linhas [DAMAGE] / [PUPPET]
- docs/protocol.md �� 20 DAMAGE_SYNC + ordem de campos
- docs/animation-enemies-damage-data.md �� nota de cobertura damage/health/energy
</artifacts this phase produces>