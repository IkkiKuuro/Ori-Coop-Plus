# Graphify (grafo de conhecimento do repo)

Guia operacional do time. A regra obrigatoria esta em
[`AGENTS.md`](../AGENTS.md) ("Graphify obrigatorio").

## O que commitar

- Commitar: `.planning/config.json` (`graphify.enabled: true`) e
  `.planning/graphs/` (`graph.json`, `graph.html`, `GRAPH_REPORT.md`,
  `.last-build-snapshot.json`).
- Nunca commitar: `graphify-out/` (diretorio de trabalho com `cache/`;
  ja ignorado no `.gitignore` via `/graphify-out/`).

## Setup por maquina do time (agente executa)

1. Verifique o harness/IDE em uso (opencode, claude, cursor, antigravity,
   codex, etc.).
2. Instale a skill para esse harness:
   ```sh
   graphify install --platform <harness>
   ```
   Exemplos: `graphify install --platform opencode`,
   `graphify install --platform claude`.
   Harness desconhecido: `graphify install` (todos os detectados).
   Recarregue o runtime apos instalar.
3. Se o binario `graphify` nao existir ou o build reclamar
   "graphify is not installed":
   ```sh
   python -m pip install graphifyy
   graphify install
   ```
   No Windows garanta o `Scripts/` do Python no PATH.
4. Ativacao (ja commitada neste repo; so refaca se o config sumir):
   ```sh
   node <runtime-home>/gsd-core/bin/gsd-tools.cjs config-set graphify.enabled true
   ```
   `<runtime-home>` e o diretorio de config do runtime
   (ex.: `C:/Users/<voce>/.config/opencode`).

## Quando rebuildar

- `/gsd-graphify status` = STALE ou `Source commit: X (N commits behind HEAD)`.
- Na pratica: apos merge/PR grande, refatoracao, novo modulo/pasta, ou antes
  de planejar fase. Nao rebuildar a cada commit pequeno.
- Quem rebuilda: `/gsd-graphify build` e commita `.planning/graphs/` atualizado.

## Como o agente usa

- Pergunta de arquitetura/dependencias ("onde X e usado?", "o que depende
  de Y?"): consultar o grafo primeiro (`/gsd-graphify query <termo>`),
  depois confirmar no arquivo-fonte.
- Implementacao/edicao: leitura convencional do arquivo (o grafo tem
  nos/arestas, nao o conteudo completo).
- Comportamento confirmado neste repo: build inicial com 1674 nos,
  3930 arestas (a confirmar apos proximos rebuilds).
