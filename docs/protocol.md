# Protocolo e sincronizacao

## Identificadores de pacotes

Os IDs sao definidos em `src/OriCoopPlus/OriCoopShared/PacketType.cs` e devem
ser alterados com extremo cuidado:

| ID | Nome | Finalidade |
| ---: | --- | --- |
| 1 | `POSITION` | posicao de jogador |
| 2 | `ANIM` | animacao |
| 3 | `ID` | identificacao |
| 4 | `DISCONNECT` | saida |
| 5 | `REQUEST_PLAYERS` | pedido de jogadores |
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

## Transporte

O servidor usa UDP. O primeiro inteiro do pacote identifica o cliente; valores
negativos iniciam tentativa de conexao. O servidor valida o endpoint UDP antes
de encaminhar dados ao cliente associado.

A implementação oficial desse transporte pertence ao
`OriCoopDedicatedServer.Core`. O protocolo permanece compatível com o cliente
BepInEx atual, mas não depende de infraestrutura, namespaces ou assemblies do
WW. O executável próprio inicializa diretamente as regras do Ori, sem carregar
módulos externos.

O servidor aceita uma porta configuravel, com padrao `7777`, e um maximo
configuravel de jogadores, limitado pelo programa entre `1` e `10`. O cliente
padrão aponta para `127.0.0.1:7777`. O dedicado vincula o listener a
`IPAddress.Any` em IPv4, portanto aceita clientes na mesma rede local pelas
interfaces de rede disponíveis. O endereço LAN nao e descoberto pelo
protocolo; cada cliente deve configurar manualmente o IPv4 do host.

O ingresso usa duas etapas próprias: o cliente envia `-1` para solicitar um
slot; o servidor responde `-1`, com uma mensagem de boas-vindas e o ID; então
o cliente envia um envelope autenticado pelo ID contendo o pacote `-1` e seu
`string nickname` (codificado no formato de rede: `int32 length` em 4 bytes little-endian
seguido pelos bytes ASCII, compatível com `Packet.ReadString()` e `WriteLegacyString`).
Só depois dessa confirmação o servidor marca o cliente como pronto e aceita snapshots.
O fluxo é exclusivo do transporte UDP do Ori Coop Plus.

Strings em todos os pacotes devem obedecer ao formato de 4 bytes de tamanho (`int32`)
seguido por bytes ASCII, evitando o prefixo LEB128 padrão de `BinaryWriter.Write(string)`.

Quando o bot virtual de testes está ativo (`dummy`), o servidor aceita requisições
de `TELEPORT_REQUEST` direcionadas ao ID `999` (`DummyManager.DummyId`), respondendo
com a posição flutuante atual do bot e o nick `Bot_Amigo`.

## Configuracao distribuida

`CONFIG_SYNC` transmite, nesta ordem, os booleanos:

```text
AllowTeleport
ShareAbilities
ShareStoryOnly
ShareWorldEvents
ShareDoorsAndLevers
ShowNicknames
```

Ao adicionar um campo, atualize o escritor no servidor, o leitor no cliente e
esta tabela na mesma mudanca. `ClientColors` e `EntitySync` tambem possuem
variaveis de rede proprias, mas nao fazem parte do payload de `CONFIG_SYNC`
listado acima.

`TELEPORT_REQUEST` tem duas formas. Do cliente para o servidor, o payload e
`int targetPlayerId`; do servidor para o cliente, e `Vector3 position` seguido
de `string destinationNick`. Mensagens `-5` usam o formato de chat
`string sender`, `string message`. A variavel `ES` (`-3`) informa se a
sincronizacao adicional de entidades esta habilitada.

O pacote negativo `-7` e reservado para ping. O cliente envia um envelope com
`long sentTicks`; o servidor devolve o mesmo valor e o cliente calcula o
tempo de ida e volta em milissegundos. Esse valor e exibido para cada jogador
no HUD do cliente.

## Pacote unificado PLAYER_STATE (18)

`PLAYER_STATE` carrega, nesta ordem: `int playerId`, `Vector3 pos` (3 floats),
`byte state` (`ActionVisualState` — autoridade da animacao),
`byte flags` (bit 0 `FacingLeft`, bit 1 `IsGrounded`),
`int animHash` (`uint` FNV1a reinterpretado como `int`, pois o `Packet` do
servidor nao possui `Write(uint)`), `float speedX`, `float speedY` (velocidade
real do `Sein.Speed`) e `string nick` no formato legado (`int32` + ASCII).
O servidor retransmite os bytes como recebeu, reescrevendo apenas `playerId`
e `nick`, sem fundir nem inferir nada.

`POSITION` (1) + `ANIM` (2) fragmentados estão depreciados e serão removidos
no passo seguinte do rework (sem compatibilidade retroativa: cliente e
servidor sempre do mesmo build).

## Fragmentacao POSITION/ANIM (causa raiz do bug #2 — legado em remocao)

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
