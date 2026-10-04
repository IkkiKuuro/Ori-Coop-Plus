# Arquitetura

## Visao geral

O Ori Coop Plus e dividido em tres partes:

```text
OriDE.exe
  └─ BepInEx\plugins\OriCoopBepInEx.dll
       └─ Harmony + NetworkService + patches

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
| `OriCoopBepInEx` | `.NET Framework 3.5` | `OriCoopBepInEx.dll` | scaffolding do plugin BepInEx 5.x |
| `OriCoopShared` | arquivos compartilhados | incorporado nos dois modulos | enums, configuracao e contrato comum |
| `OriCoopDedicatedServer.Core` | `.NET 8.0` | `OriCoopDedicatedServer.Core.dll` | transporte UDP, ciclo de vida, API e console |
| `OriCoopDedicatedServer` | `.NET 8.0` | `OriCoopDedicatedServer.exe` | servidor dedicado independente do WW |

O cliente referencia DLLs instaladas pelo jogo em `oriDE_Data\Managed`. Esses
caminhos sao configurados atualmente no `.csproj` e precisam ser ajustados
quando a instalacao do jogo estiver em outro local.

O novo scaffolding BepInEx esta documentado em
[bepinex-architecture.md](bepinex-architecture.md). Ele e um projeto separado,
com target estrito `net35`, conectado diretamente ao servidor dedicado próprio.

## Ciclo de inicializacao

### Cliente

1. O BepInEx encontra `OriCoopBepInEx.dll` em `BepInEx\plugins`.
2. `OriCoopPlugin.Awake` carrega a configuracao BepInEx.
3. `NetworkService` abre o UDP próprio e tenta conectar diretamente ao
   `OriCoopDedicatedServer.exe`.
4. Harmony aplica os patches do mod.
5. `SeinCharacterPatch` envia snapshots de posicao e animacao.

### Servidor

1. `Program` escolhe maximo de jogadores e porta.
2. `Server.Start` abre o listener UDP e cria os slots de clientes.
3. O servidor registra os comandos de infraestrutura.
4. `OriCoopServerModule.OnEnable` zera as opcoes cooperativas, registra handlers
   e registra os comandos do Ori.

## Estado e responsabilidades

- O **servidor** e a autoridade para configuracao, IDs, nomes recebidos,
  teleporte e distribuicao das mensagens.
- O **cliente** cria/atualiza representacoes remotas, aplica efeitos no mundo
  Unity e controla a interface F8.
- O **codigo compartilhado** define os identificadores que precisam ser iguais
  nos dois lados.
- A camada de jogo e acessada por assemblies externos; alteracoes nesses
  assemblies podem quebrar compilacao ou comportamento em runtime.

## Compatibilidade

Cliente e servidor devem ser distribuidos como um par. O contrato de
`PacketType`, a ordem dos campos e os recursos de configuracao precisam
permanecer compatíveis. Ao mudar um pacote, compile e teste os dois modulos.
