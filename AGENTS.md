# Regras de manutencao do repositorio

## Documentacao e contexto

A pasta [`docs/`](docs/README.md) e parte fundamental do projeto e deve ser
mantida atualizada em toda mudanca relevante. Esta regra se aplica a qualquer
conversa, tarefa ou contribuicao que altere este repositorio.

Antes de concluir uma tarefa:

1. verifique se a mudanca afeta arquitetura, comportamento do mod, protocolo,
   comandos, configuracao, build, instalacao, diagnostico ou contexto do jogo;
2. atualize o arquivo correspondente dentro de `docs/`;
3. crie uma nova pagina em `docs/` quando o assunto ainda nao estiver coberto;
4. marque como "a confirmar" qualquer comportamento que nao tenha sido
   confirmado pelo codigo ou por teste;
5. atualize os links e o indice de [`docs/README.md`](docs/README.md) quando
   adicionar uma pagina;
6. registre testes manuais ou automatizados relevantes em
   [`docs/operations.md`](docs/operations.md).

Uma tarefa nao deve ser considerada completa se o codigo mudou e a
documentacao relacionada ficou desatualizada.
