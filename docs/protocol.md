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
`string nickname`. Só depois dessa confirmação o servidor marca o cliente
como pronto e aceita snapshots. O fluxo é exclusivo do transporte UDP do Ori Coop Plus.

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

## Regras para mudancas

1. Nunca reutilize um ID existente para outro significado.
2. Mude cliente e servidor juntos.
3. Preserve a ordem dos campos de um pacote existente.
4. Valide comprimento e disponibilidade dos dados antes de ler pacotes novos.
5. Teste cliente local, dois clientes e desconexao/reconexao.

Os campos completos de cada pacote nao estao formalizados aqui porque parte
deles e montada nos handlers. Ao documentar um novo pacote, registre tambem a
ordem dos campos e os tipos (`int`, `bool`, `string`, vetor etc.).
