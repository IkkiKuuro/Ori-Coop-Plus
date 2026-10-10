# Mapa de entradas e entidades do jogo

Este documento registra as entradas do **Ori and the Blind Forest: Definitive
Edition** que o Ori Coop Plus já intercepta, sincroniza ou usa como alvo de
ações. Ele separa o que foi confirmado pelo código do que ainda precisa de
inspeção das DLLs do jogo ou de teste manual.

A cobertura atualmente ativa está limitada aos patches em
`src/OriCoopPlus/OriCoopBepInEx/Patches` e aos handlers do servidor dedicado.

> Auditoria 2026-10-10: o `src/` atual só tem 6 patches
> (`SeinCharacterPatch`, `PlayerStateReader`, `SpiritFlamePatch`,
> `SeinInputPatch`, `FrustumCullingBypassPatch`, `AnimationPrewarmPatch`) e
> a recepção cliente de `SKILL`/`SYNC_*` valida e descarta sem aplicar
> (`Networking/NetworkService.cs:990-1036`). Seções abaixo marcadas como
> "Implementado" para habilidades, portas/alavancas, world events e pickups
> valem como **relay sem aplicação** até o handler voltar. Detalhes em
> [game-assembly-audit.md](game-assembly-audit.md).
>
> Auditoria vanilla 2026-10-10 (REA `inspect_managed_members`
> `ev_b8a19444de607e2f2e70b01b728fa75e5f4d8751b8af89db15853e94dfbf4f3b`,
> `Assembly-CSharp.dll` SHA-256
> `18e70a4c96f093b6dc0ece83ad2842fd45a130196f17a13008d202e7b69c1d88`,
> 2539 tipos / 14123 métodos): enum `AbilityType` completo com 44 valores,
> `PlayerAbilities.SetAbility(AbilityType,bool)`, `SeinCharacter` (17 campos),
> `SeinController` (flags abaixo), `PickupBase` + `SeinPickupProcessor`
> (8 `OnCollect*`), `Lever.OnPushLeverLeft/Right/Middle` +
> `SetLeverDirection`, `DoorWithSlots.FixedUpdate` + `CurrentState`,
> `Door.OnTriggerStay` (transição de cena), `EnergyDoor` (orbes),
> `SetWorldEventAction.Perform` + `WorldEventsRuntime(Value)` +
> `WorldEventsManager` + `WorldEvents(UniqueID/Items)` +
> `SeinWorldState(WaterCleansed,WindReleased)`, `MoonGuid(A,B,C,D)`,
> `IAttackable` (+ `IBashAttackable`), inimigos concretos sem classe `Boss`
> (chefes = sequências `Kuro*`/`Ginso`/`Forlorn`/`Horu`), sem classe
> `SpiritWell` (save/teleporte = `SavePedestal` + `SoulFlame`/`Checkpoint` +
> `SaveGameController`) e sem classe `Breakable`. Seção
> "O que o jogo espera do jogador" ao final consolida o loop vanilla.

## Legenda de cobertura

- **Implementado**: existe um ponto de entrada no código e um fluxo de
  sincronização ou aplicação correspondente.
- **Reconhecido**: o tipo ou contrato aparece no código, mas não há catálogo
  nominal completo nem garantia de que todos os casos são sincronizados.
- **A confirmar**: o comportamento depende de `Assembly-CSharp` ou
  de uma execução dentro do jogo.
- **Não implementado**: não há entrada específica localizada no código atual.

## Habilidades

### Desbloqueio de habilidades — Implementado

O patch de `PlayerAbilities.SetAbility` captura qualquer habilidade que passe
pelo setter com `value == true`. Quando `ShareAbilities` está ativo, o cliente
envia `SYNC_ABILITY`; os outros clientes aplicam a habilidade usando o mesmo
`PlayerAbilities.SetAbility`.

Quando `ShareStoryOnly` está ativo, o filtro atual considera como habilidades
de história:

| `AbilityType` |
| --- |
| `Bash` |
| `ChargeFlame` |
| `WallJump` |
| `Stomp` |
| `DoubleJump` |
| `ChargeJump` |
| `Magnet` |
| `Climb` |
| `Glide` |
| `SpiritFlame` |
| `WaterBreath` |
| `Dash` |
| `Grenade` |
| `ChargeDash` |
| `AirDash` |

Enum vanilla completo (REA, `AbilityType`, 44 valores — corrigido em
2026-10-10; a lista acima é só o filtro `ShareStoryOnly`, não o jogo):
`Bash`, `ChargeFlame`, `WallJump`, `Stomp`, `DoubleJump`, `ChargeJump`,
`Magnet`, `UltraMagnet`, `Climb`, `Glide`, `SpiritFlame`, `RapidFlame`,
`SplitFlameUpgrade`, `SoulEfficiency`, `WaterBreath`, `ChargeFlameBlast`,
`ChargeFlameBurn`, `DoubleJumpUpgrade`, `BashBuff`, `UltraDefense`,
`HealthEfficiency`, `Sense`, `UltraStomp`, `SparkFlame`, `QuickFlame`,
`MapMarkers`, `EnergyEfficiency`, `HealthMarkers`, `EnergyMarkers`,
`AbilityMarkers`, `Rekindle`, `Regroup`, `ChargeFlameEfficiency`,
`UltraSoulFlame`, `SoulFlameEfficiency`, `CinderFlame`, `UltraSplitFlame`,
`Dash`, `Grenade`, `GrenadeUpgrade`, `ChargeDash`, `AirDash`,
`GrenadeEfficiency`. `PlayerAbilities` tem um campo por habilidade +
`SetAbility(AbilityType,bool)` / `HasAbility(AbilityType)` /
`SetAllAbilitys(bool)` — confirmado no vanilla.

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`
- `SYNC_ABILITY` em `src/OriCoopPlus/OriCoopShared/PacketType.cs`

### Uso de habilidades em jogadores remotos — Parcial

O código trata diretamente dois ataques:

| Entrada | Alvos usados | Regra observada |
| --- | --- | --- |
| `Spirit` / Spirit Flame | `Targets.Attackables` que implementam `ISpiritFlameAttackable` | Pode atingir alvos que aceitam `CanBeSpiritFlamed()` em até 10 unidades; seleciona até cinco por prioridade/distância |
| `Stomp` | `Targets.Attackables` que aceitam `CanBeStomped()` | Usa raio configurado em `SeinStomp.StompBlashRadius` e dano `StompDamage` |

O evento visual de Spirit Flame é detectado pelo patch de
`SpiritFlameProjectile.Start`. A execução remota de Stomp usa
`SeinStompMP.Attack()`, que cria o efeito e aplica `DamageType.StompBlast`.
Não existe, neste repositório, uma tabela com os nomes concretos dos inimigos
ou bosses aceitos por essas interfaces.

Evidências (classes MP legadas citadas pelo vocabulario; fluxo novo do piloto
em `Patches/SpiritFlamePatch.cs` + `Events/PlayerEventCore.cs`):

- `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs`
- `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs`

## Teleportes

### Teleporte até outro jogador — Implementado

Há três entradas para o mesmo recurso:

| Entrada | Comportamento |
| --- | --- |
| Tecla `T` | Escolhe o jogador remoto conhecido mais próximo e envia o pedido ao servidor |
| Botão no HUD | **A confirmar**; não há HUD de teleporte no cliente BepInEx atual |
| Comando `/tp <origem> <destino>` ou `/teleport` | O servidor envia ao jogador de origem a última posição conhecida do destino |

O cliente posiciona `Characters.Sein` (ou `Sein` como fallback) e registra uma
mensagem colorida. O recurso só funciona quando `AllowTeleport` está habilitado
pelo servidor e há um snapshot recente do destino. A atualização de câmera e
cenas carregadas ainda está **a confirmar**.

Esse é um teleporte do mod entre jogadores. Não foi localizada uma entrada
para pontos de teleporte, Spirit Wells, portais ou fast travel nativos do jogo.
Esses elementos ficam **a confirmar** e não devem ser tratados como suportados
até que sejam identificados nas assemblies ou em teste.

Correção vanilla 2026-10-10 (REA): não existe classe `SpiritWell`
(grep `spirit+well` = zero tipos). O fast travel/save vanilla é
`SavePedestal` (`TeleportOnPedestal`, `SaveOnPedestal`, `MarkAsUsed`,
`CanTeleport`, `CloneOfSeinForPortals`) + `SoulFlame`/`SeinSoulFlame`
(`CastSoulFlame`, `SpawnSoulFlame`, custo/cooldown) + `Checkpoint`/
`SaveGameController` (`PerformSave`/`PerformLoad`, `SaveSlotsManager`).
`Door` vanilla (`OtherDoorName`, `OnTriggerStay`) é transição de cena/porta,
distinto de `DoorWithSlots`/`EnergyDoor` (orbes). `GameMapTeleporter`/
`Teleporter`/`TeleporterController` existem como tipos de mapa/cena —
efeito em coop segue **a confirmar** em run com 2 clientes.

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs` (`TeleportCommand`)
- `src/OriCoopPlus/OriCoopShared/CoopConfig.cs`

## Inimigos e bosses

### Alvos de combate — Reconhecido (contratos vanilla confirmados)

O código não procura por nomes como `Enemy`, `Boss` ou por uma lista de
classes de bosses. Ele usa os registros globais `Targets.Attackables` e
contratos de combate:

- `IAttackable`: posição, estado de ataque e elegibilidade para Stomp;
- `ISpiritFlameAttackable`: prioridade, offset do projétil e elegibilidade
  para Spirit Flame;
- `CanBeStomped()` e `CanBeSpiritFlamed()`: filtros fornecidos pelo jogo.

Vanilla (REA 2026-10-10): `IAttackable` declara `CanBeChargeFlamed`,
`CanBeChargeDashed`, `CanBeGrenaded`, `CanBeStomped`, `CanBeBashed`,
`CanBeSpiritFlamed`, `IsStompBouncable`, `CanBeLevelUpBlasted`, `IsDead`;
há ainda `IBashAttackable` (`OnEnterBash`, `BashPriority`),
`IChargeDashAttackable`, `IChargeFlameAttackable`, `IStompAttackable`.
Inimigos concretos (sem classe `Boss` — grep `*boss*` retorna zero tipos):
`AcidSlugEnemy`, `RammingEnemy`/`ArmouredRammingEnemy`, `DashOwlEnemy`,
`DropSlugEnemy`, `FishEnemy`, `FloatingRockTurretEnemy`/`FloatingRockLaserEnemy`,
`JumperEnemy` (+ estados `Charging/Fall/Idle/Stomped/Stunned/Thrown`),
`SpitterEnemy` (+ `Charging/Idle/RunBack/Shooting/Stomped/Stunned/Thrown/Walk`),
`SwarmEnemy` (+ `SwarmEnemyManager`), `WormEnemy`/`MortarWormEnemy`,
`KamikazeSootEnemy`, `SlugEnemy`, `StarSlugEnemy`, `OwlEnemy`, `GroundEnemy`.
"Chefes"/gates de progressão são sequências scriptadas, não `Boss`:
`MistyWoodsKuroController`, `ValleyOfTheWindKuroGameplayController`,
`CatAndMouseKuroKillController`, chaves `GinsoTreeKey`/`ForlornRuinsKey`/
`MounthoruKey` em `InventoryManager`, `SeinWorldState(WaterCleansed,
WindReleased)`. Sincronizar vida/morte/fases de chefe continua
**não implementado**.

Assim, um inimigo ou boss só participa do fluxo se o próprio objeto do jogo
estiver registrado em `Targets.Attackables` e implementar o contrato esperado.
O código não confirma se todos os bosses usam esses contratos, nem sincroniza
vida, morte, fases, padrões de ataque, drops ou arena.

### Entidades Unity genéricas — Reconhecido / A confirmar

Quando o oitavo bool do `CONFIG_SYNC` 16 (`EntitySync`, antes variavel `ES`
do pacote `-3` removido) está habilitado, `EntityController.Awake`
procura uma entidade com um objeto de sprite e registra o controlador em
`EntitySync`. O pacote de entidade contém apenas:

- posição (`Vector3`);
- nome da animação atual (`string`);
- direção (`bool`).

Esse fluxo pode cobrir entidades animadas, mas não define que elas são
inimigos ou bosses e não sincroniza explicitamente dano, morte ou estado de
combate. A confirmação deve ser feita em jogo com `EntitySync` ativado.

Evidências (fluxo legado de entidades; cobertura real **a confirmar**):

- `src/OriCoopPlus/OriCoopBepInEx/Patches/` (varredura `EntityController`/`EntitySync`)

## Pickups e progresso

### Pickups detectados — Implementado (tipos vanilla confirmados)

`SeinPickupMPProcessor` varre `PickupBase` no cliente local. Quando um jogador
remoto está a até 2,5 unidades de um pickup ainda não coletado, o processador
chama o método correspondente de `SeinPickupProcessor`.

| Tipo de pickup |
| --- |
| `KeystonePickup` |
| `SkillPointPickup` |
| `RestoreHealthPickup` |
| `MaxHealthContainerPickup` |
| `MaxEnergyContainerPickup` |
| `MapStonePickup` |
| `ExpOrbPickup` |
| `EnergyOrbPickup` |

Esse inventário é uma entrada de progresso/coleta, não um catálogo de
inimigos. O significado exato de cada pickup é o fornecido pelo jogo.

Vanilla (REA 2026-10-10): `PickupBase` (`IsCollected`, `Collected()`,
`Radius`, `DestroyOnCollect`, `Serialize`) + `SeinPickupProcessor` com
8 coletores: `OnCollectSkillPointPickup`, `OnCollectEnergyOrbPickup`,
`OnCollectMaxEnergyContainerPickup`, `OnCollectExpOrbPickup`,
`OnCollectKeystonePickup`, `OnCollectMaxHealthContainerPickup`,
`OnCollectRestoreHealthPickup`, `OnCollectMapStonePickup`.
`SeinInventory` (`Keystones`, `MapStones`, `SkillPointsCollected`,
`Collect/Spend`) + `SeinLevel` (XP/level/skill point) + `SeinEnergy`/
`SeinHealthController` completam o que o jogo espera coletar/gastar.

Evidência: `src/OriCoopPlus/OriCoopBepInEx/Patches/`.

## Mundo, portas e eventos

### Portas e alavancas — Implementado

O patch de mundo intercepta:

- `Lever.OnPushLeverLeft`;
- `Lever.OnPushLeverRight`;
- `Lever.OnPushLeverMiddle`;
- `DoorWithSlots.FixedUpdate`, quando o estado chega a `Opened`.

Os objetos são identificados por `MoonGuid`. A alavanca transmite a direção;
a porta transmite o GUID uma única vez por cliente. O recurso é controlado por
`ShareDoorsAndLevers`.

### Eventos globais — Implementado (tipos vanilla confirmados)

`SetWorldEventAction.Perform` transmite o `MoonGuid` e o estado do evento.
No recebimento, `WorldSyncManager` atualiza o runtime existente ou registra um
novo `WorldEventsRuntime`. O recurso é controlado por `ShareWorldEvents`.

Vanilla (REA 2026-10-10): `SetWorldEventAction(WorldEvents, State)` +
`WorldEventsRuntime(Value)` + `WorldEventsManager(Instance, Find)` +
`WorldEvents(UniqueID, Items, GetIDFromName/GetNameFromID)` +
`SeinWorldState(WaterCleansed, WindReleased)`. `MoonGuid(A,B,C,D)` é o
identificador estável de portas/alavancas/eventos. `Lever` declara
`OnPushLeverLeft/Right/Middle` + `SetLeverDirection(LeverDirections)` +
`LeverMode/LeverType`; `DoorWithSlots` declara `CurrentState` +
`NumberOfOrbsRequired/Used` + `FixedUpdate`; `EnergyDoor` varia por orbes.

### Breakables — Contrato sem fluxo localizado (sem classe vanilla)

`SYNC_BREAKABLE` existe em `PacketType`, mas não foi localizado um patch ou
handler específico para objetos destrutíveis neste código. Breakables devem
ser considerados **não implementados** até que o fluxo seja encontrado ou
criado.

Correção vanilla 2026-10-10 (REA): grep `breakable` = zero tipos no
`Assembly-CSharp.dll`. Não há classe `Breakable` para sincronizar; o ID 13
é contrato do mod sem alvo vanilla mapeado.

Evidências (fluxo de portas/alavancas/mundo; breakable sem fluxo):

- `src/OriCoopPlus/OriCoopBepInEx/Patches/` (varredura `Lever`/`DoorWithSlots`/`SetWorldEventAction`)
- `src/OriCoopPlus/OriCoopShared/PacketType.cs`

## O que o jogo espera do jogador (loop vanilla, REA 2026-10-10)

Ori DE é metroidvania single-player: explorar, desbloquear habilidades em
`PlayerAbilities`, coletar e gastar recursos, abrir caminho e sobreviver.

1. **Mover o Ori**: `SeinCharacter` (`Position`, `Speed`, `FaceLeft`,
   `IsOnGround`) + `SeinController` (flags `IsBashing`, `IsStomping`,
   `IsDashing`, `IsGliding`, `IsChargingJump`, `IsGrabbingWall/Block/Lever`,
   `IsSwimming`, `IsAimingGrenade`, `IsCarrying`, `IsCrouching`,
   `IsPushPulling`, `IsStandingOnEdge`, `IsInsideSoulFlame`, `IsCharging`).
   Isso é o que o mod lê em `PlayerStateReader` e envia em `PLAYER_STATE` 18.
2. **Desbloquear e usar habilidades**: `GetAbilityPedestal`/`GetAbilityAction`
   → `PlayerAbilities.SetAbility` → `CharacterAbility.HasAbility`. Ataques e
   mobilidade: `SeinSpiritFlameAbility.ThrowSpiritFlames`,
   `SeinStomp.DoBlastRadius`, `SeinBashAttack`, `SeinChargeJump`,
   `SeinDashAttack`, `SeinGlide`, `SeinDoubleJump`, `SeinGrenadeAttack`,
   `SeinChargeFlameAbility`, `SeinSoulFlame.CastSoulFlame`.
3. **Coletar progredir**: `PickupBase.Collected()` → `SeinPickupProcessor`
   (8 coletores), `SeinInventory` (keystones/mapstones),
   `SeinLevel` (XP/level/skill point), `SeinEnergy`/`SeinHealthController`,
   `InventoryManager` (chaves `GinsoTreeKey`/`ForlornRuinsKey`/`MounthoruKey`).
4. **Abrir o mundo**: `Lever` (3 direções), `DoorWithSlots`/`EnergyDoor`
   (orbes), `Door` (troca de cena), `SavePedestal` (save + teleporte),
   `SetWorldEventAction`/`WorldEventsRuntime`/`SeinWorldState`
   (`WaterCleansed`, `WindReleased`) para Ginso/vento/água e sequências Kuro.
5. **Sobreviver**: inimigos listados acima + `SeinMortality`/`SeinDamageReciever`,
   `Checkpoint`/`SaveGameController` para respawn. Sem classe `Boss` nem
   `SpiritWell`/`Breakable` — não esperar esses nomes no código.

## Lacunas para a próxima etapa

1. ~~Inspecionar `Assembly-CSharp` para enumerar classes concretas de inimigos,
   bosses, Spirit Wells, portais e outros pontos de teleporte.~~ Feito em
   2026-10-10 (REA `ev_b8a19444...`): ver listas acima; sem `Boss`,
   `SpiritWell` ou `Breakable` como classes.
2. Executar uma sessão com `EntitySync` habilitado e registrar quais entidades
   realmente são criadas, movidas e destruídas.
3. Testar cada inimigo/`Kuro*` com Spirit Flame e Stomp para verificar os contratos
   `ISpiritFlameAttackable` e `CanBeStomped()` (lista de alvos acima).
4. Formalizar, se necessário, um identificador estável para inimigos e bosses;
   posição e animação não são suficientes para sincronizar vida, morte ou
   fases.
5. Investigar o pacote `SYNC_BREAKABLE`, pois o identificador existe mas o
   produtor/consumidor não aparece no código localizado — e não há classe
   `Breakable` no vanilla para ancorar o recurso.
