# Documentacao do WW Launcher

Esta pasta concentra o contexto tecnico e operacional do repositorio. O foco
atual e o **Ori Coop Plus**, mas o launcher tambem possui catalogos e modulos
para outros jogos.

## Como usar esta documentacao

- Comece por [arquitetura](architecture.md) para entender os projetos e o
  fluxo entre jogo, cliente e servidor.
- Consulte [jogo e mod](game-and-mod.md) para o vocabulario do Ori e o que cada
  recurso cooperativo significa.
- Consulte [protocolo](protocol.md) antes de alterar pacotes, sincronizacao ou
  compatibilidade cliente-servidor.
- Consulte [operacao](operations.md) para compilar, instalar, testar e
  diagnosticar uma instancia.
- Use [mapa do codigo](code-map.md) para localizar rapidamente as classes
  principais.
- Use [mapa de entradas do jogo](game-entry-map.md) para consultar habilidades,
  teleporte, pickups, alvos de combate, portas, alavancas e lacunas de
  cobertura.
- Consulte [scaffolding BepInEx](bepinex-architecture.md) para a arquitetura do
  novo plugin, suas camadas e os pontos ainda dependentes de confirmação no
  `Assembly-CSharp`.
- Consulte [arquitetura](architecture.md) para a separação do
  `OriCoopDedicatedServer` e a ausência de dependência do WW.

## Escopo e confiabilidade

Os fatos desta documentacao foram extraidos do README, dos projetos .NET e do
codigo-fonte versionado. Quando um comportamento depende de assemblies do jogo
(`UnityEngine` ou `Assembly-CSharp`) ou de teste manual, ele esta
marcado como **a confirmar** em vez de ser apresentado como garantia.

## Projetos e jogos conhecidos

| Identificador | Jogo/modulo | Estado registrado |
| --- | --- | --- |
| `ORIDE` | Ori and the Blind Forest: Definitive Edition / Ori Coop Plus | Mod cliente e servidor implementados neste repositorio |
| `HK` | Hollow Knight | Catalogado; o README do modulo referencia HKMP |
| `DD` | Death's Door | Catalogado |
| `FARLS` | FAR Lone Sails | Catalogado como modulo existente |
| `ORIWOTW` | Ori and the Will of the Wisps | Catalogado |
| `COTL` | Cult of the Lamb | Catalogado |
| `MG` | Music Game | Catalogado |
| `CUBICS` | Cubics | Catalogado |

Os numeros de modulos e o estado de funcionamento podem mudar; mantenha esta
tabela sincronizada com `AllGames.txt`, `AllMods.txt` e `WWGames.txt`.

## Manutencao da documentacao

Esta pasta e parte fundamental do projeto. A manutencao dela e uma regra do
repositorio para todas as conversas e contribuicoes: qualquer mudanca
relevante deve atualizar a documentacao relacionada antes de ser considerada
concluida. A regra completa esta em [`AGENTS.md`](../AGENTS.md).

Ao alterar codigo, pacote, comando, campo de configuracao ou caminho de
instalacao:

1. atualize a pagina correspondente;
2. registre se a mudanca exige atualizar cliente e servidor juntos;
3. registre um teste manual reproduzivel quando nao houver teste automatizado;
4. diferencie comportamento observado de comportamento apenas pretendido.
5. se o assunto for novo, crie uma pagina em `docs/` e adicione-a a este indice.
