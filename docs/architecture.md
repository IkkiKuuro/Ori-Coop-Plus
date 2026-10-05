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

OriCoopDedicatedServer.exe (novo core, sem dependencias externas)
  └─ Net/
       ├─ Transport (EnvelopeCodec 24B + UdpTransport com Channel)
       ├─ Session (SessionManager: token, endpoint fixo, sweeper 10 s)
       ├─ Game (ServerBoot, GameHandlers, ConfigStore, DummyBot, Commands)
       └─ Diagnostics (FileConsoleLogger: console + Logs/server.log)
```

O `OriCoopDedicatedServer.Core` antigo foi removido do repositorio em
2026-10-05 (junto com `OriCoopDedicatedServer/Game/` do path antigo): nao ha
dual-stack nem fallback de protocolo. Rodar binario antigo gera o sintoma
`Ignored packet with unknown client ID 151363` (magic `0x4F43`/v2 lido como
`int`), porque o cliente novo so fala envelope 24B.

O projeto `OriCoopDedicatedServer` e autonomo: servidor UDP com envelope
versionado `0x4F43`/v2, sessoes com token, confiabilidade por pacote
(ACK + retry) e regras do Ori compiladas no executável próprio; não existe
dependência de servidor ou API de multiplayer externa, nem do Core antigo.

## Projetos

| Projeto | Target | Saida | Responsabilidade |
| --- | --- | --- | --- |
| `OriCoopBepInEx` | `.NET Framework 3.5` | `OriCoopBepInEx.dll` | plugin BepInEx 5.x, replicação e blindagem visual de entidades |
| `OriCoopShared` | arquivos compartilhados | incorporado nos dois modulos | enums, dados de sincronização, configuração e contrato comum |
| `OriCoopDedicatedServer` | `.NET 8.0` | `OriCoopDedicatedServer.exe` | servidor dedicado: `Net/` (transporte, sessao, jogo, diagnostico) + `SmokeProbe` de validacao |

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

1. `Program` escolhe máximo de jogadores (1–10) e porta (1–65535), via
   argumentos (`--auto`, `--max-players`, `--port`, posicionais) ou prompt
   interativo.
2. `ServerBoot` carrega `serverconfig.json` (sem zerar opcoes), instancia
   logger (`Logs/server.log` + console), sessoes, transporte e camada Game, e
   liga mudancas de config ao broadcast.
3. `NetServerHost` abre o listener UDP em `IPAddress.Any`, loga os enderecos
   LAN, anuncia `Server started on <porta>` + `Module CARREGADO` e processa
   receive (`UdpTransport` + `Channel`) → dispatch (sessao + `GameHandlers`) →
   send, com timers de retry (250 ms) e sweeper (10 s).
4. O loop de console despacha via `CommandRegistry` instanciado
   (`coop`/`tp`/`dummy`/`clientcolors`/`entitysync`/`help`/`stop`); `stop`
   sinaliza o `CancellationToken` e encerra limpo.

## Estado e responsabilidades

- O **servidor** é a autoridade para configuração, IDs, nomes recebidos,
  teleporte e distribuição das mensagens.
- O **cliente** cria e gerencia entidades remotas desacopladas, assegura
  visibilidade contínua do corpo/mesh, aplica interpolação de posições e estados
  determinísticos de animação.
- O **código compartilhado** define os identificadores e estruturas de dados que
  precisam ser iguais nos dois lados.

## Compatibilidade

Cliente e servidor devem ser distribuídos como um par do mesmo build. O
contrato de `PacketType`/`NetProtocol`, a ordem dos campos e os recursos de
configuração precisam permanecer compatíveis — nao ha fallback para builds
antigos (quebra one-way D-02/D-14/D-15). Ao mudar um pacote, compile e teste
os dois módulos.
