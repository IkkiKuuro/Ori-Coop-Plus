# Scaffolding BepInEx do Ori Coop

O novo cliente fica em
[`src/OriCoopPlus/OriCoopBepInEx`](../src/OriCoopPlus/OriCoopBepInEx). Ele e um
plugin BepInEx 5.x para o Ori DE 32-bit e substitui o carregamento legado por
`BaseUnityPlugin`.

## Camadas

```text
Plugin/OriCoopPlugin
    -> Patches/SeinCharacterPatch + Patches/SeinInputPatch
    -> Domain/PlayerSnapshot + contratos
    -> Networking/NetworkService (UDP e serializacao)
```

- **Plugin** registra configuracao, cria as dependencias, inicia a rede e
  aplica/desfaz Harmony.
- **Domain** contem DTOs sem referencias a Unity, BepInEx ou ao assembly do
  jogo.
- **Networking** implementa o transporte UDP e o protocolo binario inicial.
  Nenhum tipo Unity e usado nessa camada.
- **Patches** sao gatilhos finos. `SeinCharacterPatch` captura o estado por
  meio de um leitor isolado; `SeinInputPatch` esta reservado para a captura de
  inputs depois que os campos exatos do assembly da versao instalada forem
  confirmados.

O atalho `T` usa os snapshots remotos recebidos, escolhe o jogador remoto mais
proximo e envia um pedido de teleporte ao servidor. A resposta e aplicada no
thread principal do Unity ao objeto `Characters/Sein` (com fallback para
`Sein`). Se o objeto nao existir na cena atual, o cliente registra um aviso.

O plugin usa `HarmonyPatch` com nomes de tipo/metodo para evitar uma referencia
de compilacao a `Assembly-CSharp.dll` durante o scaffolding. A existencia e os
nomes de `SeinCharacter.FixedUpdate`, `SeinInput.Update`, `Velocity`,
`CurrentAnimation` e `FaceLeft` ainda precisam ser confirmados no runtime da
versao instalada.

O plugin tambem desenha um HUD proprio no canto superior esquerdo usando
`OnGUI`, sem depender de um Canvas do jogo. Cada linha mostra nick,
coordenadas recebidas e ping de ida e volta medido pelo pacote `-7`.

## Contrato inicial

`NetworkService` implementa diretamente o transporte UDP e os pacotes próprios
`POSITION`, `ANIM` e mensagens `-5`, alem do pedido/resposta
`TELEPORT_REQUEST` e da variavel `ES`, mantendo compatibilidade inicial com
`OriCoopShared/PacketType.cs` e `NetworkHandler.cs`. O snapshot de domínio
continua mais rico que o payload atual; velocidade e inputs ainda não são
serializados pelo servidor dedicado e estão **a confirmar** para a próxima
versão do protocolo.

O cliente BepInEx conecta diretamente ao `OriCoopDedicatedServer.exe`. Depois
de receber seu ID, envia o nick configurado em `[Network] Nickname`; essa
confirmação ativa o slot no servidor. O fluxo não instancia componentes externos
de multiplayer.

## Compatibilidade

O projeto aponta para as DLLs locais do jogo e do BepInEx. O target e
estritamente `net35`, sem `async`, `Task` ou APIs posteriores ao CLR 2.0.
Altere os `HintPath` do
[`OriCoopBepInEx.csproj`](../src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj)
se a instalação do jogo estiver em outro caminho.
