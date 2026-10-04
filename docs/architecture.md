# Arquitetura

## Visao geral

O Ori Coop Plus e dividido em tres partes:

```text
OriDE.exe
  └─ BepInEx\plugins\OriCoopBepInEx.dll
       ├─ RemotePlayerManager + Puppets (renderização e animação do clone)
       ├─ RemoteVisualController (watchdog de visibilidade e bypass de culling)
       ├─ AnimationRegistry (catálogo pré-aquecido e fallback de poses)
       └─ Harmony + NetworkService + Patches

OriCoopDedicatedServer.exe
  └─ OriCoopDedicatedServer.Core.dll
       └─ UDP + pacotes + comandos + regras do Ori
```

O projeto `OriCoopDedicatedServer` fornece uma infraestrutura própria: servidor
UDP, clientes conectados, pacotes, eventos e comandos de console. As regras do
Ori são compiladas no executável próprio; não existe dependência de servidor
ou API de multiplayer externa.

## Projetos

| Projeto | Target | Saida | Responsabilidade |
| --- | --- | --- | --- |
| `OriCoopBepInEx` | `.NET Framework 3.5` | `OriCoopBepInEx.dll` | plugin BepInEx 5.x, replicação e blindagem visual de entidades |
| `OriCoopShared` | arquivos compartilhados | incorporado nos dois modulos | enums, dados de sincronização, configuração e contrato comum |
| `OriCoopDedicatedServer.Core` | `.NET 8.0` | `OriCoopDedicatedServer.Core.dll` | transporte UDP, ciclo de vida, API e console |
| `OriCoopDedicatedServer` | `.NET 8.0` | `OriCoopDedicatedServer.exe` | servidor dedicado independente do WW |

O cliente referencia DLLs instaladas pelo jogo em `oriDE_Data\Managed` e o `BepInEx.dll`
de `API\Client\`. A compilação é suportada via script dedicado (`build.ps1`) ou via
MSBuild com caminhos configuráveis de fallback.

A arquitetura do cliente BepInEx está documentada em
[bepinex-architecture.md](bepinex-architecture.md).

## Ciclo de inicializacao

### Cliente

1. O BepInEx encontra `OriCoopBepInEx.dll` em `BepInEx\plugins`.
2. `OriCoopPlugin.Awake` carrega a configuração BepInEx e inicia a rede.
3. Harmony aplica os patches do mod, incluindo o bypass de frustum culling e o
   pré-aquecimento de animações.
4. `SeinCharacterPatch` envia snapshots de posição, velocidade e animação a cada
   FixedUpdate do jogador local.
5. Quando chegam snapshots de outros jogadores, `RemotePlayerManager` instancia
   e atualiza os puppets visuais desacoplados, blindados pelo `RemoteVisualController`.

### Servidor

1. `Program` escolhe máximo de jogadores e porta.
2. `Server.Start` abre o listener UDP e cria os slots de clientes.
3. O servidor registra os comandos de infraestrutura.
4. `OriCoopServerModule.OnEnable` zera as opções cooperativas, registra handlers
   e registra os comandos do Ori.

## Estado e responsabilidades

- O **servidor** é a autoridade para configuração, IDs, nomes recebidos,
  teleporte e distribuição das mensagens.
- O **cliente** cria e gerencia entidades remotas desacopladas, assegura
  visibilidade contínua do corpo/mesh, aplica interpolação de posições e estados
  determinísticos de animação.
- O **código compartilhado** define os identificadores e estruturas de dados que
  precisam ser iguais nos dois lados.

## Compatibilidade

Cliente e servidor devem ser distribuídos como um par. O contrato de
`PacketType`, a ordem dos campos e os recursos de configuração precisam
permanecer compatíveis. Ao mudar um pacote, compile e teste os dois módulos.
