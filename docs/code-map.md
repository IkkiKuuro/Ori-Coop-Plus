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

## Servidor dedicado próprio

| Caminho | Papel |
| --- | --- |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` | argumentos e ciclo de vida do servidor |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Server.cs` | listener UDP e slots |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Client.cs` | estado de cada cliente |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/Network/Packet.cs` | serialização de pacotes |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/CommandSystem/` | parser e registro de comandos |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/API/` | API própria, eventos e tipos comuns |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Game/` | regras, comandos e handlers do Ori compilados no servidor |

## Fontes de catalogo

- `AllGames.txt`: jogos reconhecidos pelo launcher.
- `AllMods.txt`: identificadores e pastas de modulos.
- `WWGames.txt`: jogos com status adicional no catalogo.
- `CHANGELOGS/`: historico parcial por jogo.
