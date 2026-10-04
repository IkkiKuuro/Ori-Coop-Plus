# Mapa de entradas e entidades do jogo

Este documento registra as entradas do **Ori and the Blind Forest: Definitive
Edition** que o Ori Coop Plus já intercepta, sincroniza ou usa como alvo de
ações. Ele separa o que foi confirmado pelo código do que ainda precisa de
inspeção das DLLs do jogo ou de teste manual.

A cobertura atualmente ativa está limitada aos patches em
`src/OriCoopPlus/OriCoopBepInEx/Patches` e aos handlers do servidor dedicado.

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

O enum completo de `AbilityType` não é declarado neste repositório. Portanto,
habilidades fora dessa lista são reconhecidas pelo setter, mas podem ser
filtradas por `ShareStoryOnly` e devem ser confirmadas contra a DLL do jogo.

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

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopBepInEx/Patches/`

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

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs`
- `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/Commands/TeleportCmd.cs`
- `src/OriCoopPlus/OriCoopShared/CoopConfig.cs`

## Inimigos e bosses

### Alvos de combate — Reconhecido

O código não procura por nomes como `Enemy`, `Boss` ou por uma lista de
classes de bosses. Ele usa os registros globais `Targets.Attackables` e
contratos de combate:

- `IAttackable`: posição, estado de ataque e elegibilidade para Stomp;
- `ISpiritFlameAttackable`: prioridade, offset do projétil e elegibilidade
  para Spirit Flame;
- `CanBeStomped()` e `CanBeSpiritFlamed()`: filtros fornecidos pelo jogo.

Assim, um inimigo ou boss só participa do fluxo se o próprio objeto do jogo
estiver registrado em `Targets.Attackables` e implementar o contrato esperado.
O código não confirma se todos os bosses usam esses contratos, nem sincroniza
vida, morte, fases, padrões de ataque, drops ou arena.

### Entidades Unity genéricas — Reconhecido / A confirmar

Quando a variável de rede `ES` está habilitada, `EntityController.Awake`
procura uma entidade com um objeto de sprite e registra o controlador em
`EntitySync`. O pacote de entidade contém apenas:

- posição (`Vector3`);
- nome da animação atual (`string`);
- direção (`bool`).

Esse fluxo pode cobrir entidades animadas, mas não define que elas são
inimigos ou bosses e não sincroniza explicitamente dano, morte ou estado de
combate. A confirmação deve ser feita em jogo com `EntitySync` ativado.

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopBepInEx/Patches/`

## Pickups e progresso

### Pickups detectados — Implementado

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

### Eventos globais — Implementado

`SetWorldEventAction.Perform` transmite o `MoonGuid` e o estado do evento.
No recebimento, `WorldSyncManager` atualiza o runtime existente ou registra um
novo `WorldEventsRuntime`. O recurso é controlado por `ShareWorldEvents`.

### Breakables — Contrato sem fluxo localizado

`SYNC_BREAKABLE` existe em `PacketType`, mas não foi localizado um patch ou
handler específico para objetos destrutíveis neste código. Breakables devem
ser considerados **não implementados** até que o fluxo seja encontrado ou
criado.

Evidências:

- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopBepInEx/Patches/`
- `src/OriCoopPlus/OriCoopShared/PacketType.cs`

## Lacunas para a próxima etapa

1. Inspecionar `Assembly-CSharp` para enumerar classes concretas de inimigos,
   bosses, Spirit Wells, portais e outros pontos de teleporte.
2. Executar uma sessão com `EntitySync` habilitado e registrar quais entidades
   realmente são criadas, movidas e destruídas.
3. Testar cada boss com Spirit Flame e Stomp para verificar os contratos
   `ISpiritFlameAttackable` e `CanBeStomped()`.
4. Formalizar, se necessário, um identificador estável para inimigos e bosses;
   posição e animação não são suficientes para sincronizar vida, morte ou
   fases.
5. Investigar o pacote `SYNC_BREAKABLE`, pois o identificador existe mas o
   produtor/consumidor não aparece no código localizado.
