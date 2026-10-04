# Contexto do jogo e do mod

## Jogo alvo

O alvo documentado e **Ori and the Blind Forest: Definitive Edition** para
Windows, instalado pela Steam. O jogo e Unity e fornece ao mod assemblies como
`UnityEngine`, `UnityEngine.UI` e `Assembly-CSharp`.

O multiplayer nao e uma conversao geral do jogo para multiplayer. Ele injeta um
modulo no cliente existente e usa um servidor dedicado para distribuir estado.
Por isso, o teste deve ocorrer dentro de um save em que Ori ja esteja
controlavel; menu e prologo com Naru nao representam o fluxo normal do mod.

## Vocabulário do jogo usado pelo codigo

| Conceito | Uso no mod |
| --- | --- |
| Ori/player local | personagem controlado pelo cliente atual |
| jogador remoto | representacao visual e estado recebido de outro cliente |
| habilidade | eventos de habilidades, incluindo `Spirit` e `Stomp` |
| pickup | itens coletaveis como keystone, skill point, vida, energia, map stone e orbs |
| porta/alavanca | objetos de mundo sincronizaveis quando a opcao esta ativa |
| world event | evento global do mundo tratado pelo `WorldSyncManager` |
| breakable | entidade destrutivel que pode ser distribuida pelo recurso de sincronizacao |
| entity sync | sincronizacao adicional de entidades Unity; desativada por padrao |

O significado exato de cada entidade depende dos tipos presentes nas DLLs do
jogo. Os nomes acima sao o vocabulario observado nas classes
`SeinPickupMPProcessor`, `SeinSpiritMP`, `SeinStompMP`, `WorldSyncManager` e
`EntitySync`.

## Recursos cooperativos

As opcoes sao controladas pelo servidor e enviadas para os clientes:

- **Teleporte**: `/tp <origem> <destino>` e a tecla `T` usam o mesmo fluxo
  servidor-autoritativo. A tecla `T` escolhe o jogador remoto mais proximo
  conhecido pelo cliente e o servidor devolve a ultima posicao recebida.
- **Habilidades**: distribui eventos de habilidades suportadas.
- **Story only**: subopcao de compartilhamento relacionada ao progresso de
  historia; o efeito exato deve ser validado no fluxo de patches.
- **World events**: compartilha eventos globais do mundo.
- **Doors and levers**: compartilha portas e alavancas.
- **Nomes**: habilita floating name tags dos jogadores.
- **Client colors**: alterna cores personalizadas no servidor.
- **Entity sync**: alterna a sincronizacao de entidades adicionais e entrega
  o estado ao cliente pela variavel de rede `ES`; a cobertura concreta de
  inimigos e objetos do mundo ainda esta **a confirmar**.

Todas as opcoes cooperativas sao inicializadas desligadas em
`ORIDEServerModule.OnEnable`. Isso e intencional para evitar que um servidor
novo altere o estado da partida sem uma escolha explicita do operador.

## Configuracao local

O cliente grava `MPSettings.json` na pasta raiz do jogo. O formato observado e:

```json
{
  "LocalColor": "...",
  "NickName": "Ori_123",
  "ServerIP": "127.0.0.1",
  "ServerPort": 7777,
  "AutoConnect": true
}
```

`LocalColor` e serializado pelo `JsonUtility` do Unity. O valor concreto pode
variar conforme a versao do Unity; nao edite esse campo manualmente sem testar.

## Limites conhecidos

- O mod depende de uma versao especifica dos assemblies do jogo.
- A documentacao nao afirma que todos os jogos catalogados no launcher estao
  funcionando; o README registra que alguns ainda nao estao.
- Nao ha, nesta base, uma especificacao formal do estado completo de cada
  entidade do Ori. Antes de adicionar sincronizacao, capture o fluxo existente
  em `NetworkService` e `ServerHandle`.
