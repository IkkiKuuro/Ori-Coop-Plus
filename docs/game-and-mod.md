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
| jogador remoto | representacao visual e estado recebido de outro cliente; visual do Spirit Flame = espelho real do Sein (orbe + prefab real visual-only, sem dano/colisao — em jogo ainda **a confirmar**) |
| orbe seguidor (visual do Sein) | `Game.Characters.Ori` (estatico, auto-registrado; nunca busca por string) — `Game.Characters.Sein` e o avatar do jogador |
| habilidade | eventos de habilidades, incluindo `Spirit` e `Stomp` |
| pickup | itens coletaveis como keystone, skill point, vida, energia, map stone e orbs |
| porta/alavanca | objetos de mundo sincronizaveis quando a opcao esta ativa |
| world event | evento global do mundo tratado pelo `WorldSyncManager` |
| breakable | entidade destrutivel que pode ser distribuida pelo recurso de sincronizacao |
| entity sync | sincronizacao adicional de entidades Unity; desativada por padrao |

O significado exato de cada entidade depende dos tipos presentes nas DLLs do
jogo. Auditoria vanilla 2026-10-10 (REA `ev_b8a19444...`, `Assembly-CSharp`
2539 tipos): `MoonGuid`, `PickupBase`, `IAttackable`,
`ISpiritFlameAttackable`, `Game.Targets`, `SeinStomp`,
`SeinSpiritFlameAbility` existem no vanilla; `SeinPickupMPProcessor`,
`SeinSpiritMP`, `SeinStompMP`, `WorldSyncManager`, `EntitySync` são nomes do
mod/legado, não do vanilla. `Boss`, `SpiritWell` e `Breakable` têm zero tipos
no vanilla (ver `game-entry-map.md` para os substitutos reais:
sequências `Kuro*`, `SavePedestal`+`SoulFlame`, sem alvo para 13).

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
  o estado ao cliente pelo oitavo bool do `CONFIG_SYNC` 16 (antes via
  variavel de rede `ES` do pacote `-3`, removido — ver `protocol.md`);
  a cobertura concreta de
  inimigos e objetos do mundo ainda esta **a confirmar**.

Todas as opcoes cooperativas seguem os padroes de `ConfigStore`
(`serverconfig.json`): `AllowTeleport=true`, demais `false`.
O `ORIDEServerModule.OnEnable` legado (que zerava tudo) foi removido do
repositorio em 2026-10-05; nao usar como referencia.

## Configuracao local

O cliente BepInEx grava `com.ikkikuuro.oricoop.cfg` em
`<ORI_DIR>\BepInEx\config` (plugin `com.ikkikuuro.oricoop`). O formato observado e:

```ini
[Network]
Host = 127.0.0.1
Port = 7777
PlayerId = -1
Nickname = Ori_Player
```

O `MPSettings.json` legado na raiz do jogo nao e mais usado; nao usar como
referencia. `ServerHandle` legado tambem foi removido com o Core em
2026-10-05; o fluxo atual esta em `NetworkService` + `Net/Game/GameHandlers`.

## Limites conhecidos

- O mod depende de uma versao especifica dos assemblies do jogo.
- A documentacao nao afirma que todos os jogos catalogados no launcher estao
  funcionando; o README registra que alguns ainda nao estao.
- Nao ha, nesta base, uma especificacao formal do estado completo de cada
  entidade do Ori. Antes de adicionar sincronizacao, capture o fluxo existente
  em `NetworkService` e `ServerHandle`.
