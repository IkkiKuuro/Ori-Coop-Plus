# Protocolo e sincronizacao

## Identificadores de pacotes

Os IDs sao definidos em `src/OriCoopPlus/OriCoopShared/PacketType.cs` e devem
ser alterados com extremo cuidado:

| ID | Nome | Finalidade |
| ---: | --- | --- |
| 1 | `POSITION` | REMOVIDO — posicao fragmentada (ver secao de remocao) |
| 2 | `ANIM` | REMOVIDO — animacao fragmentada (ver secao de remocao) |
| 3 | — | REMOVIDO (D-15) — identificacao do handshake legado; nunca reutilizar |
| 4 | `DISCONNECT` | saida |
| 5 | — | REMOVIDO (D-15) — `REQUEST_PLAYERS`, listagem de jogadores; nunca reutilizar |
| 6 | `COLOR` | cor |
| 7 | `SKILL` | habilidade |
| 10 | `SYNC_ABILITY` | evento de habilidade |
| 11 | `SYNC_LEVER` | estado de alavanca |
| 12 | `SYNC_DOOR` | estado de porta |
| 13 | `SYNC_BREAKABLE` | estado de breakable |
| 14 | `SYNC_WORLDEVENT` | evento do mundo |
| 15 | `TELEPORT_REQUEST` | pedido cliente-servidor ou resposta servidor-cliente |
| 16 | `CONFIG_SYNC` | configuracao do servidor |
| 17 | `DUMMY_ACTION` | acao do bot de teste |
| 18 | `PLAYER_STATE` | estado unificado posicao+animacao (substitui `POSITION`+`ANIM` fragmentados) |

`CoopSkillType` atualmente diferencia `NONE`, `Spirit` e `Stomp`.

## IDs removidos no redesenho (D-15, quebra one-way)

> Cliente e servidor precisam ser sempre do mesmo build; nao ha fallback
> para o envelope antigo nem para estes IDs. IDs removidos nunca sao
> reutilizados para outro significado.

| ID | Nome antigo | Destino |
| ---: | --- | --- |
| 1 | `POSITION` | REMOVIDO no rework de anims; `PLAYER_STATE` 18 carrega posicao+anim |
| 2 | `ANIM` | REMOVIDO no rework de anims; `PLAYER_STATE` 18 carrega posicao+anim |
| 3 | `ID` | REMOVIDO — handshake legado; novo handshake e `Hello` 100 / `Welcome` 101 / `Confirm` 102 |
| 5 | `REQUEST_PLAYERS` | REMOVIDO — listagem de jogadores; sem substituto (relay e automatico) |
| -1 | handshake legado | REMOVIDO — substituido por `Hello` 100 / `Welcome` 101 / `Confirm` 102 |
| -2 | `NBMessage` | REMOVIDO — repasse cego; sem substituto |
| -3 | `NetworkVar` (`ES`, `cc`, `Coop_*`) | REMOVIDO — absorvido pelo `CONFIG_SYNC` de 8 bools abaixo |
| -6 | `RPC` | REMOVIDO — repasse cego; sem substituto |
| -7 | ping legado | REMOVIDO — substituido por `MsgPing` 104 / `MsgPong` 105 |

`COLOR` 6, `SKILL` 7, `SYNC_ABILITY` 10, `SYNC_LEVER` 11, `SYNC_DOOR` 12,
`SYNC_BREAKABLE` 13, `SYNC_WORLDEVENT` 14, `TELEPORT_REQUEST` 15,
`CONFIG_SYNC` 16, `DUMMY_ACTION` 17 e `PLAYER_STATE` 18 mantem valores e
ordem de campos.

## Transporte

O servidor usa UDP com um envelope versionado obrigatorio (secao abaixo):
todo datagrama carrega `magic 0x4F43` + versao `2`; divergencia e recusada
com `Reject 106` sem criar sessao. Nao ha envelope antigo nem fallback:
cliente e servidor precisam ser sempre do mesmo build.

A implementação oficial desse transporte pertence ao novo core em
`src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/` (camadas
`Transport`/`Session`/`Game`/`Diagnostics`, sem dependencia do Core antigo,
que saiu do build no cutover). O executável próprio inicializa diretamente
as regras do Ori via `ServerBoot`, sem carregar módulos externos.

O servidor aceita uma porta configuravel, com padrao `7777`, e um maximo
configuravel de jogadores, limitado pelo programa entre `1` e `10`. O cliente
padrão aponta para `127.0.0.1:7777`. O dedicado vincula o listener a
`IPAddress.Any` em IPv4, portanto aceita clientes na mesma rede local pelas
interfaces de rede disponíveis. O endereço LAN nao e descoberto pelo
protocolo; cada cliente deve configurar manualmente o IPv4 do host.

O ingresso usa handshake em 3 vias sobre o envelope: o cliente envia `Hello`
100 com `clientId -1` e `token 0`; o servidor responde `Welcome` 101 com o ID
atribuido e o token da sessao; o cliente confirma com `Confirm` 102 e o
servidor marca `IsReady=true`, enviando `COLOR` inicial + `CONFIG_SYNC` em
unicast confiavel. Só depois dessa confirmação o servidor aceita snapshots.
O fluxo é exclusivo do transporte UDP do Ori Coop Plus.

Strings em todos os pacotes devem obedecer ao formato de 4 bytes de tamanho (`int32`)
seguido por bytes ASCII, evitando o prefixo LEB128 padrão de `BinaryWriter.Write(string)`.

Quando o bot virtual de testes está ativo (`dummy`), o servidor aceita requisições
de `TELEPORT_REQUEST` direcionadas ao ID `999` (`DummyManager.DummyId`), respondendo
com a posição flutuante atual do bot e o nick `Bot_Amigo`.

## Envelope versionado (novo core, build atual)

> Quebra total com builds anteriores (D-02/D-14/D-15, one-way): datagramas sem
> `magic 0x4F43` + versao `2` sao recusados com mensagem (`Reject 106`) e nao
> criam sessao. Cliente e servidor precisam ser sempre do mesmo build; nao ha
> fallback para o envelope antigo `[clientId + len + payload]`.

Todo datagrama UDP carrega um header fixo de 24 bytes little-endian
(`src/OriCoopPlus/OriCoopShared/NetProtocol.cs` e o contrato canonico),
seguido do payload bruto (sem tamanho prefixado: o resto do datagrama):

| Offset | Tamanho | Campo | Descricao |
| ---: | ---: | --- | --- |
| 0 | 2 | `magic` | `0x4F43` ("OC"); divergencia = recusa imediata |
| 2 | 1 | `versao` | `2`; divergencia = recusa imediata |
| 3 | 1 | `flags` | bit 0 `Reliable` (0x01), bit 1 `AckPresent` (0x02) |
| 4 | 4 | `seq` | `uint32` por remetente; comparacao wrap-safe (`(int)(nova - ultima) > 0`) |
| 8 | 4 | `clientId` | remetente (`-1` pre-handshake, `0` servidor); relay preserva o do remetente |
| 12 | 4 | `token` | `uint32` da sessao (`0` pre-handshake) |
| 16 | 4 | `packetId` | mensagem de sistema (100–106) ou pacote de jogo (ex. 18) |
| 20 | 4 | `ackSeq` | `uint32` de confirmacao (usado a partir da confiabilidade 02-02) |

Mensagens de sistema (`packetId`):

| ID | Nome | Direcao | Payload |
| ---: | --- | --- | --- |
| 100 | `Hello` | cliente → servidor | `protoVer` byte + nick (`int32 length` + ASCII); header com `clientId -1`, `token 0` |
| 101 | `Welcome` | servidor → cliente | `assignedId` int + `token` uint + `serverVer` byte |
| 102 | `Confirm` | cliente → servidor | vazio no handshake; marca `IsReady=true` (com token correto). Re-`Confirm` com nick e tolerado e re-dispara o join (`COLOR` + `CONFIG_SYNC`) |
| 103 | `Ack` | ambos | `uint32` LE com a `seq` confirmada; resposta imediata a todo datagrama `Reliable`, antes do dispatch |
| 104 | `Ping` | cliente → servidor | `sendTicks` long; ecoado sem alteracao |
| 105 | `Pong` | servidor → cliente | mesmo `sendTicks`; cliente calcula ida-volta em ms (HUD) |
| 106 | `Reject` | servidor → cliente | motivo (`int32 length` + ASCII); nao cria sessao |

Handshake em 3 vias: `Hello(-1)` → `Welcome+assignedId+token` → `Confirm`.
So apos o `Confirm` o servidor marca `IsReady=true` e aceita snapshots.
Nick vazio vira `Player_<id>`. IDs incrementais nunca reutilizados no run;
`0` (servidor) e `999` (dummy) nunca sao alocados. Servidor cheio
(`count >= MaxPlayers`) recusa o `Hello` com `Reject`.
Reconectar exige novo `Hello` com novo ID; o ID antigo nunca e reaproveitado.

### Confiabilidade e sessao (hardening 02-02)

Pacotes criticos — chat `-5`, `CONFIG_SYNC` 16, `TELEPORT_REQUEST` 15,
`SYNC_ABILITY` 10, `SYNC_LEVER` 11, `SYNC_DOOR` 12, `SYNC_WORLDEVENT` 14,
`SKILL` 7, `COLOR` 6, `DISCONNECT` 4 — sao confiaveis: o remetente marca a
flag `Reliable` (`0x01`), o servidor responde `SysAck` 103 imediato (payload
= `uint32` LE com a `seq` confirmada) antes do dispatch, e o reenvio do
datagrama original acontece a cada 250 ms ate 3 tentativas (`AckRetryMs` /
`MaxRetries` em `NetProtocol.cs`). Sem ACK apos as 3 tentativas o servidor
loga `Warning` com sessao e `seq` e desiste sem derrubar a sessao. ACK
piggybacked (`AckPresent` `0x02` + `ackSeq` no header) vale como `SysAck`.
`PLAYER_STATE` 18 e `Ping` 104 nunca geram pendencia (unreliable, sem retry).

Espelho no cliente (cutover 02-04, `NetworkService.cs`): o cliente responde
`SysAck` 103 imediato a todo datagrama `Reliable` (inclui `CONFIG_SYNC` e
chat do servidor) e reenvia os proprios criticos (chat, `TELEPORT_REQUEST`
15, `SKILL` 7, `COLOR` 6, `DISCONNECT` 4, `SYNC_*`) a cada 250 ms ate 3
tentativas; `PLAYER_STATE` vai on-change + heartbeat 2,5 Hz sem retry, com
drop-old wrap-safe por remetente; `Reject` 106 reseta para `-1` (re-handshake)
e limpa baselines/pendencias, de modo que o re-sync pos-`Confirm` (`CONFIG`
+ snapshots correntes que o servidor reenvia) e sempre aplicado. Heartbeat e
ping rodam na thread de rede com timeout curto — nunca em `FixedUpdate` —
para a conexao sobreviver a pausa com o menu aberto.

Todo datagrama pos-handshake valida o par endpoint fixo + `token` contra a
sessao do `clientId`: divergencia de token ou de endpoint (IP/porta) e
descartada com `Warning` (com `clientId` e motivo), sem atualizar `LastSeen`
e sem resposta; trocar de IP/porta (NAT) exige novo `Hello` com `clientId -1`.
O sweeper (1 s) remove sessoes com mais de 10000 ms sem datagrama valido
(`SessionTimeoutMs`) e transmite `DISCONNECT` 4 confiavel (`marcador int 4` +
`disconnectedId int`) aos restantes; servidor cheio responde `Reject` 106
com motivo `SERVER_FULL`, sem criar sessao.

Chat `-5` (critico, confiavel nos dois sentidos): corpo = `marcador int -5`
+ `string` legada no envio do cliente; o servidor trunca o texto em 350 chars
(`ChatMaxChars`), remove `<`/`>` do texto e do nick, responde `h`/`help`/
`/h`/`/help` com a lista de comandos em unicast confiavel ao solicitante
(remetente `SERVER`), e transmite o restante como `marcador -5` + `sender`
formatado + texto, confiavel, aos `IsReady`.

Regra de corpo legado preservado: pacotes de jogo (`PLAYER_STATE` 18 e
demais) mantem o corpo byte-identico ao formato anterior, incluindo o
`int` inicial com o proprio ID (ex. `18`) e a ordem de campos atual — o
`playerId` do remetente viaja no `clientId` do header e o relay reemite os
bytes originais sem reconstrucao. Strings continuam `int32 length` + ASCII.

## Configuracao distribuida

`CONFIG_SYNC` 16 transmite, nesta ordem exata, 8 booleanos (1 byte cada,
`BinaryWriter.Write(bool)` / `BinaryReader.ReadBoolean` no cliente):

```text
1. AllowTeleport
2. ShareAbilities
3. ShareStoryOnly
4. ShareWorldEvents
5. ShareDoorsAndLevers
6. ShowNicknames
7. ClientColors      (novo; antes via variavel de rede "cc" do -3 morto)
8. EntitySync        (novo; antes via variavel de rede "ES" do -3 morto)
```

Os 6 primeiros preservam ordem e significado; os 2 novos vao ao final. Corpo
com marcador: `int 16` + 8 bools. O servidor e autoridade: transmite
`CONFIG_SYNC` confiavel (ACK + retry) em unicast a cada `Confirm` e em
broadcast a cada mudanca via comando; persiste em `serverconfig.json` ao lado
do exe (nunca zera no boot). O cliente BepInEx le os 8 com leitura tolerante
(bytes ausentes viram `false`), dispara `ConfigSyncReceived` com os 8 valores
e espelha o oitavo em `EntitySyncChanged`.

Ao adicionar um campo, atualize o escritor no servidor, o leitor no cliente e
esta tabela na mesma mudanca. As variaveis de rede `ES`, `cc` e `Coop_*` do
pacote `-3` (morto, ver tabela de removidos) nao existem mais no fio.

`TELEPORT_REQUEST` 15 tem duas formas, ambas com marcador `int 15`. Do cliente
para o servidor, o payload e `int targetPlayerId` (use `999` para o dummy
`Bot_Amigo`); o servidor nega em unicast (chat) quando `AllowTeleport` esta
desligado ou sem snapshot recente do destino. Do servidor para o cliente, e
`Vector3 position` (3 floats) seguido de `string destinationNick` no formato
legado (`int32 length` + ASCII).

`DUMMY_ACTION` 17 e server-local: acoes vindas de cliente sao so `0` (toggle
spawn/despawn) e `1` (ability: `int action` + `int abilityId`); triggers de
alavanca/porta do dummy sao console-only (comando `/dummy`), nunca via fio.

Mensagens `-5` usam o formato de chat `string sender`, `string message` com
marcador `int -5`. O ping legado `-7` nao existe mais: o cliente envia
`MsgPing` 104 com `long sentTicks` e o servidor devolve `MsgPong` 105 com o
mesmo valor; o cliente calcula ida-volta em ms e exibe no HUD.

Regra de corpo legado preservado: pacotes de jogo mantem o corpo
byte-identico ao formato anterior, incluindo o `int` inicial com o proprio
ID (ex. `18`) e a ordem de campos atual — o `playerId` do remetente viaja no
`clientId` do header do envelope e o `packetId` do header espelha o mesmo
valor do marcador para dispatch sem parse. Strings continuam
`int32 length` + ASCII.

## Pacote unificado PLAYER_STATE (18)

`PLAYER_STATE` carrega, nesta ordem: `Vector3 pos` (3 floats),
`byte state` (`ActionVisualState` — autoridade da animacao),
`byte flags` (bit 0 `FacingLeft`, bit 1 `IsGrounded`),
`int animHash` (`uint` FNV1a reinterpretado como `int`),
`float speedX`, `float speedY` (velocidade real do `Sein.Speed`) e
`string nick` no formato legado (`int32` + ASCII), sempre precedidos do
marcador `int 18`. A identidade do remetente viaja no `clientId` do header
do envelope (nunca no corpo) e o relay reemite os bytes originais sem
reconstrucao nem fusao.

`POSITION` (1) + `ANIM` (2) fragmentados foram removidos no rework de
sincronia de anims (ver tabela de removidos); so `PLAYER_STATE` (18) trafega
posicao+anim. IDs 1 e 2 nunca serão reutilizados. Cliente e servidor sempre
do mesmo build.

## Fragmentacao POSITION/ANIM (causa raiz do bug #2 — REMOVIDA)

> Removida no rework de sincronia de anims: sender, relay e receptor do
> fragmentado foram excluídos; só `PLAYER_STATE` (18) trafega posição+anim.
> IDs 1 e 2 nunca serão reutilizados. Cliente e servidor sempre do mesmo build.

`POSITION` (1) carrega `int playerId`, `Vector3 pos`, `Color RGB (3 bytes)`,
`bool facingLeft`, `string nick` — mas **nao** carrega velocidade nem nome de
animacao. `ANIM` (2) carrega apenas `int playerId` + `string animName`.
O servidor retransmite os dois fluxos separadamente.

O cliente **nao** deve aplicar cada pacote como snapshot completo: um `ANIM`
puro aplicado como posicao `(0,0,0)` teleporta o puppet para a origem (jogador
some do mapa), e um `POSITION` puro sem `AnimName` congela no Idle. A regra
atual (ver `RemotePlayerManager`) e fundir por jogador — ultimo `POSITION` +
ultimo `ANIM` — e inferir velocidade pelo delta de posicao dividido por `dt`
(o servidor nunca envia velocidade), com snap imediato no spawn e snap quando
o alvo esta a mais de 15 unidades.

## Regras para mudancas

1. Nunca reutilize um ID existente para outro significado.
2. Mude cliente e servidor juntos.
3. Preserve a ordem dos campos de um pacote existente.
4. Valide comprimento e disponibilidade dos dados antes de ler pacotes novos.
5. Teste cliente local, dois clientes e desconexao/reconexao.

Os campos completos de cada pacote nao estao formalizados aqui porque parte
deles e montada nos handlers. Ao documentar um novo pacote, registre tambem a
ordem dos campos e os tipos (`int`, `bool`, `string`, vetor etc.).
