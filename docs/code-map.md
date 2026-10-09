# Mapa do codigo

## Ori Coop Plus

| Caminho | Papel |
| --- | --- |
| `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` | ponto de entrada BepInEx, configuração e ciclo de vida |
| `src/OriCoopPlus/OriCoopBepInEx/Client/` | gerenciamento de puppets remotos, blindagem visual e catálogo de animação |
| `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/` | observabilidade, métricas e alertas de visibilidade |
| `src/OriCoopPlus/OriCoopBepInEx/Domain/` | DTOs e contratos sem dependência de Unity |
| `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` | transporte UDP do protocolo próprio |
| `src/OriCoopPlus/OriCoopBepInEx/Patches/` | gatilhos Harmony, bypass de culling frustum e leitura de Sein |
| `src/OriCoopPlus/OriCoopShared/` | contrato cliente-servidor e dados de sincronização |

## Servidor dedicado (novo core, unico path)

| Caminho | Papel |
| --- | --- |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` | argumentos e ciclo de vida (sempre `ServerBoot`; `--net2` aceito como no-op) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/` | `EnvelopeCodec` (header 24B `0x4F43`/v2) + `UdpTransport` (receive com `Channel`) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/` | `SessionManager` (token, endpoint fixo, allocator, timer 1 s / timeout 10 s) + `Session` |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/` | `AckTracker` (SysAck 103 + retry 250 ms x3 dos criticos) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/` | `ServerBoot`, `GameHandlers`, `ConfigStore` (`serverconfig.json`), `DummyBot`, `PlayerStateRelay` |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/` | `ConsoleCommand` + `CommandRegistry` + `OriCommands` (`coop`/`tp`/`dummy`/`clientcolors`/`entitysync`/`help`/`stop`) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Diagnostics/` | `ILogger` + `FileConsoleLogger` (console + `Logs/server.log` com niveis) |
| `src/OriCoopDedicatedServer/SmokeProbe/` | validacao automatizada do protocolo (`--test all`, `SMOKE_OK`) |

Legado removido em 2026-10-05: `OriCoopDedicatedServer.Core/` e
`OriCoopDedicatedServer/Game/` (path antigo) foram excluidos; ver
`operations.md`. Nao recriar `Server.cs`/`Client.cs`/`Packet.cs` legados.

## Fontes de catalogo

- `AllGames.txt`: jogos reconhecidos pelo launcher.
- `AllMods.txt`: identificadores e pastas de modulos.
- `WWGames.txt`: jogos com status adicional no catalogo.
- `CHANGELOGS/`: historico parcial por jogo.
