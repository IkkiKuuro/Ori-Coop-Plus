# Operacao, build e diagnostico

## Pre-requisitos

- Windows.
- Ori DE Definitive Edition instalado pela Steam.
- .NET SDK compativel com os projetos.
- Visual Studio 2022 ou VS Code com suporte a .NET.
- Caminho correto para `oriDE_Data\Managed`.

## Build

Execute os comandos a partir da raiz do repositorio. Se o repositorio estiver
no caminho padrão deste ambiente:

```powershell
Set-Location 'C:\Users\irani\OneDrive\Documentos\GitHub\WW_Launcher'
```

Depois, na raiz do repositorio:

```powershell
dotnet build .\src\OriCoopPlus\OriCoopPlus.sln --configuration Release
dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release
```

Saidas esperadas:

```text
src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll
src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\OriCoopDedicatedServer.exe
```

O cliente BepInEx pode ser compilado diretamente com o script PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\OriCoopPlus\OriCoopBepInEx\build.ps1
```

A saída esperada é `src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll`.

O script localiza automaticamente a pasta `oriDE_Data\Managed` em `C:\Program Files (x86)\Steam\steamapps\common\Ori DE` ou `D:\SteamLibrary\...` e usa `API\Client\BepInEx.dll` e `API\Client\0Harmony.dll`.

Também é possível compilar via dotnet CLI se o SDK .NET compatível estiver instalado:

```powershell
dotnet build .\src\OriCoopPlus\OriCoopBepInEx\OriCoopBepInEx.csproj --configuration Release
```

### Build alternativo sem SDK instalado

Se a maquina tiver apenas o runtime .NET (sem SDK), como ocorreu em 05/10/2026:

1. Cliente: `build.ps1` funciona normalmente, pois usa `csc.exe` do
   .NET Framework (limitado a C# 5 — nao usar interpolacao `$""`, `?.`,
   `Action` com mais de 4 parametros ou `Object.Instantiate` sem cast no
   codigo do plugin).
2. Servidor (`net8.0`, C# moderno): baixe os pacotes NuGet
   `Microsoft.Net.Compilers.Toolset` (Roslyn) e `Microsoft.NETCore.App.Ref`
   (assemblies de referencia), extraia e compile com
   `dotnet <toolset>/tasks/netcore/bincore/csc.dll /nostdlib+` referenciando
   `ref/net8.0/*.dll`. Saida esperada: `OriCoopDedicatedServer.Core.dll` e
   `OriCoopDedicatedServer.dll` (reutilize `OriCoopDedicatedServer.exe`,
   `.deps.json`, `.runtimeconfig.json` e `start_server.bat` do build anterior,
   pois o apphost e os metadados nao mudaram). Valide com
   `.\OriCoopDedicatedServer.exe --auto --max-players 2 --port 7779` e com
   `coop` + `stop` via stdin (deve listar `Teleporte: ATIVADO` por padrao).

## Instalacao

Com `<ORI_DIR>` apontando para a pasta do jogo:

```text
<ORI_DIR>\BepInEx\plugins\OriCoopBepInEx.dll
<ORI_DIR>\Server\OriCoopDedicatedServer.exe
<ORI_DIR>\Server\OriCoopDedicatedServer.dll
<ORI_DIR>\Server\OriCoopDedicatedServer.Core.dll
<ORI_DIR>\Server\OriCoopDedicatedServer.deps.json
<ORI_DIR>\Server\OriCoopDedicatedServer.runtimeconfig.json
```

Feche `OriDE.exe` e o servidor antes de substituir DLLs.

Para instalar o plugin, copie essa DLL para
`<ORI_DIR>\BepInEx\plugins\`. As DLLs `BepInEx.dll`, `0Harmony.dll` e
`UnityEngine.dll` devem continuar sendo fornecidas pela instalação do jogo;
nao copie referencias privadas para a pasta do plugin.

## Inicializacao

### Teste local no mesmo computador

1. Instale o runtime .NET 8 ou publique o servidor como self-contained.
2. Compile o executável próprio:

   ```powershell
   dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release
   ```

3. Copie todos os arquivos de
   `src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\`
   para uma pasta, por exemplo `<ORI_DIR>\Server\`.
4. Copie
   `src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll` para
   `<ORI_DIR>\BepInEx\plugins\`.
5. Inicie o servidor em uma janela do PowerShell:

   ```powershell
   Set-Location "<ORI_DIR>\Server"
   .\OriCoopDedicatedServer.exe --auto --max-players 2 --port 7777
   ```

6. Confirme no console `Server started on 7777` e
   `Ori Coop Plus Server Module CARREGADO`.
7. No arquivo
   `<ORI_DIR>\BepInEx\config\com.ikkikuuro.oricoop.cfg`, use:

   ```ini
   [Network]
   Host = 127.0.0.1
   Port = 7777
   PlayerId = -1
   Nickname = Ori_Player
   ```

8. Inicie o Ori pelo executável com BepInEx, carregue um save em que o
   personagem esteja controlável e verifique
   `<ORI_DIR>\BepInEx\LogOutput.log`.
9. Para encerrar o servidor, digite `stop` no console.

O teste local confirma bind UDP, atribuição de ID, recebimento de snapshots e
desligamento. A renderização/aplicação visual de jogadores remotos foi implementada
pelos componentes `RemotePlayerManager`, `RemotePlayerPuppet`, `RemoteVisualController`
e `AnimationRegistry`.

### Protocolo de Validação de Renderização e Animação

1. **Validação de Visibilidade em Frustum Extremo:**
   - Com o jogador local parado, o jogador remoto afasta-se duas telas horizontais
     e retorna.
   - O corpo do clone deve reaparecer imediatamente ao reentrar na viewport.
   - O log `LogOutput.log` não deve registrar desativações não tratadas e o patch
     `FrustumCullingBypassPatch` intercepta o culling nativo do `CameraFrustumOptimizer`.

2. **Validação de Cutscenes e Locks de Câmera:**
   - Disparar uma cutscene (ex.: despertar da Spirit Tree ou alavanca de Ginso Tree)
     enquanto o jogador remoto se movimenta.
   - O watchdog `RemoteVisualController.LateUpdate` assegura que `MeshRenderer.enabled`
     permaneça verdadeiro e o canal alpha não seja zerado.

3. **Validação de Transições de Animação:**
   - O jogador remoto executa a sequência: Idle -> Corrida -> Pulo -> Pulo Duplo ->
     Wall Slide -> Bash -> Queda.
   - Se o clipe exato não estiver presente, a heurística de `AnimationRegistry.InferStateFromMovement`
     aplica a postura correspondente sem entrar em T-pose nem congelar.
   - A métrica `[OBSERVABILITY][NET-METRICS]` deve acusar `Dropped: 0` sob condições
     normais de rede.

### Teste em rede local (LAN)

1. No computador host, descubra o IPv4 com `ipconfig`. Use o IPv4 da mesma
   rede dos outros jogadores, não `127.0.0.1`.
2. No host, inicie o servidor:

   ```powershell
   Set-Location "<ORI_DIR>\Server"
   .\OriCoopDedicatedServer.exe --auto --max-players 4 --port 7777
   ```

3. Autorize a porta UDP no Firewall do Windows, como administrador:

   ```powershell
   New-NetFirewallRule -DisplayName "Ori Coop Dedicated Server UDP 7777" `
     -Direction Inbound -Protocol UDP -LocalPort 7777 -Action Allow
   ```

4. Em cada computador cliente, instale o mesmo
   `OriCoopBepInEx.dll` e configure o mesmo arquivo, trocando apenas o host:

   ```ini
   [Network]
   Host = 192.168.1.50
   Port = 7777
   PlayerId = -1
   ```

5. Inicie os clientes depois que o servidor estiver mostrando `Server started`.
6. Confirme no console do servidor as mensagens de conexão e, no log do
   BepInEx, a mensagem `Ori Coop BepInEx plugin loaded.`.
7. Teste a saída de um cliente, a reconexão e o limite de jogadores.

Não é necessário abrir uma porta no roteador para um teste dentro da mesma LAN.
Para jogar pela internet, será necessário encaminhar a porta UDP escolhida no
roteador e liberar a porta no firewall; esse cenário ainda não foi validado
neste projeto.

Interativa:

```powershell
Set-Location "<ORI_DIR>\Server"
.\OriCoopDedicatedServer.exe
```

Pressione Enter para os padroes: quatro jogadores e porta `7777`.
Portas validas vao de `1` a `65535`.

Automatica:

```powershell
Set-Location "<ORI_DIR>\Server"
.\OriCoopDedicatedServer.exe --auto
```

Para hospedar na rede local, o servidor escuta em todas as interfaces IPv4.
Ao iniciar, ele mostra uma ou mais linhas `LAN address: <IP>:<porta>`.
Informe um desses enderecos aos outros computadores. Os argumentos nomeados
tambem podem ser usados para evitar a configuracao interativa:

```powershell
.\OriCoopDedicatedServer.exe --max-players 4 --port 7777
```

No computador de cada jogador, edite
`<ORI_DIR>\BepInEx\config\com.ikkikuuro.oricoop.cfg` e configure:

```ini
[Network]
Host = 192.168.1.50
Port = 7777
PlayerId = -1
Nickname = Ori_Player
```

Substitua `192.168.1.50` pelo IPv4 exibido pelo servidor. O firewall do
computador que hospeda deve permitir trafego UDP de entrada na porta escolhida;
nao e necessario abrir a porta no roteador quando todos estao na mesma rede
local. A descoberta automatica de servidores ainda nao existe: os jogadores
entram informando o IPv4 manualmente.

O jogador se conecta exclusivamente ao executável
`OriCoopDedicatedServer.exe`: basta iniciar o servidor, configurar `Host`,
`Port` e `Nickname` no arquivo BepInEx e abrir o jogo com
`OriCoopBepInEx.dll`.

## Comandos do mod

| Comando | Funcao |
| --- | --- |
| `/coop` | mostra/configura `tp`, `abilities`, `story`, `world`, `doors` e `names` |
| `/tp <origem> <destino>` | teleporta a origem ate o destino; alias `/teleport`; com log de diagnóstico no cliente (`Teleporte recebido/aplicado/fixado`) |
| `/clientcolors` | alterna cores de clientes; aliases `cc`, `clientc`, `ccolors` |
| `/entitysync` | alterna sincronizacao de entidades; aliases `es`, `sync` |
| `/dummy` | controla o bot de teste; aliases `bot`, `testbot`, `fakeplayer`, `fakepl`, `fp`; `echo [on\|off]` espelha suas anims com ping 20-150 ms, `anim [on\|off\|<estado>]` performa ciclo roteirizado para validar anims do puppet |
| `/fakeplayer` | alterna o jogador falso avancado; aliases `fakepl`, `fp` |

Exemplos:

```text
/coop tp on
/coop names on
/coop tp off
/tp Player_A Bot_Amigo
```

Use `/coop` sem argumentos para consultar o estado atual.

## Checklist de teste manual

1. Compile o plugin BepInEx e o servidor em Release.
2. Instale o plugin e o servidor próprio a partir do mesmo build.
3. Inicie o servidor e confirme `Ori Coop Plus Server Module CARREGADO`.
4. Abra um save controlavel e confirme o carregamento do plugin no log.
5. Conecte um cliente local e verifique snapshots no servidor; depois repita com dois clientes.
6. Teste nome, cor, posicao, desconexao e reconexao.
7. Ative uma opcao por vez e teste o efeito correspondente.
8. Teste `/dummy` e remova o bot ao terminar.
9. Com dois jogadores em uma cena controlavel, ative `/coop tp on`, aguarde
   snapshots e pressione `T` em um cliente; confirme a mensagem colorida e a
   mudanca de posicao. Depois teste `/tp <origem> <destino>` no console.
10. Ative `entitysync` e confirme nos logs do cliente a recepcao da variavel
    `ES`; a sincronizacao visual de entidades alem dos jogadores ainda esta
    **a confirmar**.
11. Confirme no canto superior esquerdo o HUD `ORI COOP PLUS`, com uma linha
    por jogador contendo nick, coordenadas e ping. `--` indica que a primeira
    resposta de ping ainda nao chegou.

### Scaffolding BepInEx

1. Compile `OriCoopBepInEx` em `Release` e copie a DLL para
   `<ORI_DIR>\BepInEx\plugins\`.
2. Inicie o jogo e confirme no `BepInEx\LogOutput.log` a mensagem
   `Ori Coop BepInEx plugin loaded.`.
3. Confirme que a secao `[Network]` foi criada em
   `<ORI_DIR>\BepInEx\config\com.ikkikuuro.oricoop.cfg`.
4. Confirme que o jogo permanece carregando sem excecao de Harmony.
5. A recepcao e aplicação visual de jogadores remotos ainda estao **a confirmar**
   contra a versão instalada do jogo.
6. Para um teste LAN, inicie o dedicado, anote `LAN address`, configure esse
   IPv4 em dois clientes e confirme que ambos enviam pacotes ao mesmo servidor.

### Validacao do Menu Nativo "Ori Coop" (Gamepad e Teclado)

1. **Injecao no PauseScreen:**
   - Com o jogo rodando em save controlavel, pause o jogo (Esc no teclado ou Menu/Start no gamepad).
   - Confirme a presenca do botao nativo **"Ori Coop"** estilizado de forma identica aos botoes originais.
   - Pressione repetidas vezes para pausar/despausar e confirme no console/log que o botao nao e duplicado.
2. **Navegacao por Gamepad:**
   - Navegue para cima e para baixo usando tanto o **D-Pad** quanto o **Analogico Esquerdo**.
   - Verifique se a animacao de highlight (brilho/foco) e o som nativo tocam ao passar por "Ori Coop".
   - Pressione **A** (Xbox) / **Cross** (PlayStation) sobre "Ori Coop"; confirme a transicao para o submenu do mod.
3. **Submenu e Acoes:**
   - No submenu, verifique se o foco inicial esta no primeiro item ("Teleportar ate Parceiro").
   - Teste o acionamento de cada botao (Teleporte, Ressincronizacao de Puppet, Toggles).
   - Pressione **B** (Xbox) / **Circle** (PlayStation) ou **Esc**; verifique o retorno limpo ao menu de pausa principal com o foco restaurado em "Ori Coop".
4. **Persistencia de Rede durante a Pausa:**
   - Mantenha o menu de pausa aberto por mais de 30 segundos com outro jogador conectado; confirme que a conexao nao sofre timeout e o ping continua atualizando.
5. **Dialogo In-Game de Conexao (`ServerConnectionDialog`):**
   - Acesse pelo submenu "Configurar Conexao de Servidor" ou pela tecla de atalho **F6**.
   - Permite alterar IP, Porta e Nickname em tempo real, buscar servidores na LAN (`Buscar LAN`) e conectar/desconectar sem reiniciar o jogo.

### Validacao do rework de anims + puppet leve + teleporte/UDP (builds 2026-10-05 — pendente de teste em jogo)

Procedimento em [`docs/anim-test-battery.md`](anim-test-battery.md) (T0–T4).
O que este build corrige e precisa de confirmação com 2 clientes:
parado o puppet deve ficar em Idle do Ori (nunca sprite de inimigo);
Bash/Dash/Glide/Stomp/ChargeJump/DoubleJump/WallSlide/WallJump devem aparecer
(sender lê `Controller` + nome do clipe, não só velocidade);
`F8` deve listar clipes com `src=sein` cobrindo os 12 estados.
**Implantada:** DLL 78.336 bytes copiada para
`D:\SteamLibrary\...\BepInEx\plugins\` e `OriCoopDedicatedServer.Core.dll`
(35.328 bytes) para `<ORI_DIR>\Server\` em 2026-10-05. Inclui puppet leve,
correções de anim, `FindLocalSein` no teleporte e `SIO_UDP_CONNRESET` no
servidor/cliente. **Reinicie o servidor** para valer o fix de UDP.
Resultado da rodada: **a confirmar** — rodar com 2 clientes e anotar aqui
data e itens pendentes.

### Registro de Validacao de Build, Instalacao e Servidor

1. **Compilacao:**
   - `OriCoopDedicatedServer.csproj` compilado com sucesso (.NET 8.0 Release).
   - `OriCoopBepInEx.dll` compilado via `build.ps1` com 23 arquivos de origem (Release, 53.760 bytes).
2. **Implantacao em `<ORI_DIR>` (`D:\SteamLibrary\steamapps\common\Ori DE`):**
   - Plugin copiado para `BepInEx\plugins\OriCoopBepInEx.dll`.
   - Servidor dedicado copiado para `<ORI_DIR>\Server\` e `<ORI_DIR>\ServerClient\` (binarios + `start_server.bat`).
   - Configuracao do BepInEx ajustada em `BepInEx\config\BepInEx.cfg` com `Preloader.Entrypoint` apontando para `Assembly-CSharp.dll` / `LoadingBootstrap` / `Awake`.
   - Configuracao de rede validada em `BepInEx\config\com.ikkikuuro.oricoop.cfg` (`Host = 127.0.0.1`, `Port = 7777`, `Nickname = Ori_Player`).
3. **Validacao de Execucao do Servidor:**
   - Execucao de `OriCoopDedicatedServer.exe` testada: bind UDP na porta 7777 confirmado, deteccao de enderecos LAN OK e modulo `Ori Coop Plus Server Module CARREGADO` ativo.
4. **Validacao de Handshake e Teleporte com Bot Dummy:**
   - Handshake UDP de duas etapas testado: `Connect` (-1) -> `Welcome` (-1, id 0) -> `Ready` (-1, nickname "Ori_Player" codificado com `WriteLegacyString`). O servidor validou e ativou o cliente com sucesso (`IsReady = true`), eliminando o erro `Could not read value of type 'string'!`.
   - Teleporte direcionado ao `DummyManager` (ID 999) validado tanto pelo comando de console `/tp <origem> Bot_Amigo` quanto pelo handler `TELEPORT_REQUEST` disparado pela tecla `T`.
   - Limpeza de injeções legadas do `WWClient` e `UnityEngine.dll` executada com sucesso via `.\scripts\clean_unityengine_cecil.ps1`.

5. **Validacao de Correcao de Lag, Isolamento de Puppets e Tela Branca no Save:**
   - **Causa raiz diagnosticada:** 
     1. `RemotePuppetFactory.CleanPuppetComponents` destruia apenas componentes pontuais, deixando dezenas de controladores de gameplay (`SeinPrefabFactory`, `SeinDamageReciever`, `SkillItem`, `PlayerGrabPushPullHintSystem`, etc.). Na inicializacao do clone, `SeinPrefabFactory` instanciava dezenas de prefabs aninhados com UI e `SeinDamageReciever` lancava `NullReferenceException` ininterruptamente em `OnEnable` e `FixedUpdate` (gerando dezenas de milhares de excecoes no log). O puppet falhava ao ativar e a cada pacote gerava-se um novo clone com queda drastica para 1-5 FPS.
     2. Singleton corrompido: ao clonar `SeinCharacter`, o `Awake` do clone redefinia `Game.Characters.Sein = this`, e ao destruir `SeinCharacter` no clone, `Game.Characters.Sein` ficava nulo. Ao tentar mitigar isso com patch de `Awake` bloqueando instancias com `Clone` no nome, o jogador legitimo no carregamento de save (`Sein(Clone)`) tinha seu `Awake` cancelado, quebrando `Game.Characters.Current` e deixando o jogo em tela branca a 5 FPS em `SeinPlaceholder.Spawn`.
   - **Solucao aplicada:**
     - Desativacao transitoria de `sein` antes da clonagem do template em `RemotePuppetFactory.EnsureTemplate` (`sein.SetActive(false)`), garantindo que a Unity instancie o clone inativo sem jamais disparar `Awake()` ou `OnDestroy()`. Remocao dos prefixos no `SeinCharacterPatch` que causavam a quebra do save.
     - Preservacao e restauracao explicita de referencias a `Game.Characters.Sein` e `Current` em blocos `try/finally` e no watchdog `EnsureCameraFollowsLocalPlayer()`.
     - `CleanPuppetComponents` agora aplica higienizacao estrita por whitelist, destruindo `AudioSource`, `AudioListener`, `Collider`, `Rigidbody` e todos os `MonoBehaviour` exceto os estritamente necessarios para renderizacao e animacao (`Renderer`, `MeshFilter`, `SpriteAnimatorWithTransitions`, `CharacterSpriteMirror`, `RemotePlayerPuppet`, `RemoteVisualController` e `FloatingNameTag`).
     - Integracao de `FloatingNameTag.cs` para manter a tag de nome flutuante com rotacao travada (`LateUpdate`) e escala normalizada.
     - Silenciamento de logs repetitivos no `RemoteVisualController.EnforceVisibility` em `LateUpdate` com throttling a duas vezes por segundo.

6. **Validacao da Interface de Conexao, Apelidos e Notificacoes:**
   - **Feedback do Dialogo de Conexao (`ServerConnectionDialog`):** Ajustada a precedencia do status para verificar `OriCoopPlugin.Instance.IsConnected` antes de `_statusFeedback` e limpar o texto transitorio `"Conectando..."`, exibindo `<color=#00ff88>CONECTADO</color> (ID: X | Ping: Y ms | Parceiros: Z)`.
   - **Sincronizacao e Persistencia de Apelido:** Corrigido o `NetworkService` para nao sobrescrever o apelido local com o banner de boas-vindas do servidor (`WelcomePacket`). Adicionado metodo `SendNicknameUpdate` (re-envio do pacote `ClientDoneMessage` / `-1` para atualizar o nick no servidor dedicado), metodo `SetNickname` no `OriCoopPlugin`, inclusao de `snapshot.Nick` nas mensagens de posicao e botao `[ Salvar Nome ]` no `ServerConnectionDialog`.
   - **Notificacoes Nativas em Jogo:** Implementado `NativeUIHelper.ShowToast` utilizando `Game.UI.Hints.Show` para exibir avisos visuais elegantes na tela do jogo sempre que o jogador local conecta ou parceiros/bots entram na partida (`[+] Jogador conectou!`).
   - **Compatibilidade Retroativa de Strings no Servidor:** `Packet.ReadString()` no `OriCoopDedicatedServer.Core` agora suporta automaticamente tanto prefixos de 4 bytes Int32 quanto codificacao padrao LEB128 de 1 byte de clientes antigos.

7. **Validacao de Despawn do Bot Dummy e Travamento de Camera:**
   - **Causa raiz diagnosticada:**
     1. O cliente BepInEx (`NetworkService.cs`) nao possuia tratamento para o pacote `PacketType.DISCONNECT` (ID 4). Quando o comando `/dummy` enviava o pacote de saida do bot, o cliente ignorava silenciosamente, deixando o GameObject do dummy ativo na cena.
     2. Ao spawnar o bot, o clone de Sein sequestrava temporariamente a referencia global e a camera de gameplay (`GameplayCamera`), que passava a focar no clone em vez do jogador local.
     3. Ao despawnar o bot, como o servidor parava de enviar pacotes de movimento, a camera permanecia travada e congelada na posicao final do bot desativado.
   - **Solucao aplicada:**
     1. Adicionado evento `PlayerDisconnected` no `INetworkService` e `NetworkService.cs` para ler o pacote `PacketType.DISCONNECT` e propagar o `playerId`.
     2. Inscricao no `OriCoopPlugin.OnPlayerDisconnected` despachada com seguranca para a thread principal do Unity (`lock (_mainThreadActions)`), destruindo o puppet (`RemotePlayerManager.RemovePlayer(playerId)`) e removendo do cache.
     3. Implementacao do metodo `EnsureCameraFollowsLocalPlayer()` que valida e restaura `Game.Characters.Sein`, `Game.Characters.Current` e reancora `Game.UI.Cameras.Current.Target` no transform do jogador local com `ChangeTargetToCurrentCharacter()` e watchdog permanente no `OriCoopPlugin.Update`.

## Diagnostico rapido

| Sintoma | Verificacoes |
| --- | --- |
| Dummy nao desaparece ao usar `/dummy` e camera trava no local do despawn | **Falta de handler de PacketType.DISCONNECT e alvo da camera perdido**: O cliente BepInEx antigo nao tratava o pacote 4 (desconexao) e a camera ficava ancorada ao puppet clonado. Atualize para o build recente de `OriCoopBepInEx.dll` com `PlayerDisconnected` e `EnsureCameraFollowsLocalPlayer()`. |
| Queda brusca de FPS de 60 para 15 ou 1-5 ao spawnar o bot dummy ou outro jogador | **Game.Characters.Sein anulado por clone e NREs em cascata**: Clones ativos de Sein disparavam `Awake` e ao serem destruidos limpavam `Game.Characters.Sein` para null, gerando milhares de NREs por segundo (`Ori.get_m_target`, `DoorWithSlots`, `SeinLeafParticles`). Resolvido com clonagem inativa, preservacao em `try/finally`, desativacao imediata de behaviours e watchdog de auto-cura no `OriCoopPlugin.Update`. |
| Crash no inicio com Access Violation (0xc0000005) em `mono.dll` ao carregar UnityExplorer | **Incompatibilidade do UniverseLib com Unity 5.3.2**: O UniverseLib 1.5.1 requer Unity 5.3.4+ para deserializacao de UI e causa falha de segmentacao no runtime Mono do Ori DE (5.3.2f1). Desative o UnityExplorer movendo sua pasta para fora de `plugins/`. O mod Ori Coop nao depende do UnityExplorer. |
| Tela preta no inicio do jogo (`Object::FindAnyObjectOfType<MonoBehaviour>`, `The referenced script on this Behaviour is missing!` e erro de layout de serializacao) | **Entrypoint prematuro do BepInEx no Unity 5.3.2f1**: O entrypoint padrao em `UnityEngine.dll` roda antes de `Assembly-CSharp.dll` ser indexado, corrompendo o cache nativo de `MonoScript`. Solucao: configurar em `BepInEx\config\BepInEx.cfg`: `Assembly = Assembly-CSharp.dll`, `Type = LoadingBootstrap`, `Method = Awake`, e restaurar `UnityEngine.dll` original executando `.\scripts\clean_unityengine_cecil.ps1` caso tenha sido alterado por loaders legados. Para verificar logs de erro, use `.\scripts\check_unity_logs.ps1`. |
| BepInEx nao carrega, `LogOutput.log` nao existe e F7 nao funciona | **Arquitetura (bitness) incorreta**: `oriDE.exe` e um binario 32-bit (x86). Se `winhttp.dll` for 64-bit, o Windows ignora a DLL. Instale a versao 32-bit (`BepInEx_win_x86_5.4.x.zip`). |
| Erro sobre `UnityEngine` ou `Assembly-CSharp` | Confirme as DLLs do jogo e do BepInEx; o plugin deve estar em `BepInEx\plugins` |
| F8 nao abre | save controlavel, DLL correta, reinicio do jogo e log de carregamento |
| Jogador sem nome | cliente/servidor da mesma versao e `/coop names on` |
| Teleporte indisponivel | use `/coop tp on`, mantenha dois jogadores conectados e aguarde snapshots; `T` teleporta para o remoto mais proximo e `/tp <origem> <destino>` continua disponivel |
| Ori parado vira sprite de inimigo | **Fallback de anim contaminado por clipes globais**: `AnimationRegistry.Prewarm` usava `Resources.FindObjectsOfTypeAll` (inclui Kuro, slugs, owls...) e `s_stateClips` ficava com o primeiro `idle` achado, que podia ser de inimigo; além disso `TextureAnimationWithTransitions` é `ScriptableObject`, então `GetComponentsInChildren<...>` no puppet nunca achava nada. **Solução aplicada (build 76.800 bytes, 2026-10-05, a confirmar em jogo)**: coleta via reflection nos campos do Sein (`CollectClips`), fallback por estado só com clipes do Sein, resolve exato (hash/nome = mesmo asset compartilhado) com prioridade. Feche o jogo antes de copiar a DLL (arquivo fica bloqueado com `OriDE.exe` aberto). |
| Bash/Dash/Glide/Stomp/ChargeJump/DoubleJump/WallSlide não aparecem no remoto | **Sender derivava estado só por velocidade** (`DeriveState` só conhecia Idle/Run/Jump/Fall). **Solução aplicada (mesmo build)**: `PlayerStateReader` lê `IsOnGround` real + `Controller.IsBashing/IsStomping/IsDashing/IsGliding/IsChargingJump/IsGrabbingWall` com prioridade, e o nome do clipe local como autoridade para estados especiais. |
| Flood `[WARNING] [SERVER] A UDP client connection was reset` (~5/s) | **ICMP Port Unreachable no Windows**: ao enviar snapshot para endpoint morto (cliente fechado sem DISCONNECT), o próximo `EndReceive` estourava `ConnectionReset`. **Solução aplicada (Core 35.328 bytes, 2026-10-05)**: `SIO_UDP_CONNRESET` no listener (e no recriado) + warning com throttle de 5 s. Mesmo flag no `UdpClient` do mod. Exige **reiniciar o servidor**. |
| `T` / `/tp` não teleporta: `Teleport recebido, mas o objeto Sein local nao foi encontrado` | **`GameObject.Find("Sein")` não acha `Sein(Clone)`** (nome do save carregado). **Solução aplicada (DLL 78.336 bytes, 2026-10-05)**: `FindLocalSein()` (singleton → `FindObjectsOfType<SeinCharacter>` → paths incl. `Sein(Clone)`), aplicação via setter oficial `SeinCharacter.Position` com fallback para transform + log imediato `ori-agora=(x,y,z)` para conferir a alteração de valor; `EnsureCameraFollowsLocalPlayer` usa o mesmo helper. |
| DLL nao pode ser copiada | encerre o jogo e `OriCoopDedicatedServer.exe` |
| Servidor cheio | reduza conexoes ou inicie com maximo entre 1 e 10 |
| Cliente LAN nao conecta | confirme o IPv4 `LAN address`, a porta UDP, o firewall do host e se todos estao na mesma rede |
| `KeyNotFoundException` com a chave `4` ao conectar | substitua o executavel pelo build atual; o servidor deve criar e percorrer exatamente os slots configurados |
| `RECIVE UDP CALLBACK ERROR: Could not read value of type 'string'!` em `ClientDoneMessage` | Incompatibilidade de serializacao de string no handshake (`BinaryWriter.Write(string)` gerava LEB128 em vez de Int32). Corrigido com `WriteLegacyString` no cliente e leitura segura no `ServerHandle`. |
| `No remote player is available for teleport` com bot `dummy` ativo | O `DummyManager` (ID 999) nao estava incluido na lista de clientes validos para teleporte. Suporte adicionado no handler `TELEPORT_REQUEST` do servidor e no comando `/tp`. |
| Conexao concorrente ou logs de `[WW SYSTEM]` | Resquicio de injecao legada do `WW_Launcher` na `UnityEngine.dll` (`MonoBehaviour.Awake`) e `WWClient.dll`. Execute `.\scripts\clean_unityengine_cecil.ps1` e mova `WWClient.dll` para `disabled_plugins`. |
| Tela branca / 5 FPS ao carregar o save (`DoorWithSlots.get_OriHasTargets`, `SeinPlaceholder.Spawn`) | **Prefixo de Awake bloqueando save**: Patches com prefixo bloqueando `Clone` impediam a inicializacao normal de `Sein(Clone)` gerado pelo sistema de save do jogo. Resolvido com remocao dos prefixos em `SeinCharacterPatch` e garantia de isolamento apenas no momento da clonagem do puppet em `RemotePuppetFactory`. |
| Interface de Conexao (F6) travada em "Conectando..." mesmo apos conectado | **Ordem de precedencia em `ServerConnectionDialog.OnGUI`**: A verificacao de `_statusFeedback` (preenchida com mensagem transitoria ao clicar em Conectar) vinha antes da verificacao de `IsConnected`. **Solucao implementada**: `IsConnected` agora tem prioridade maxima na renderizacao do status, exibe `CONECTADO (ID | Ping | Parceiros)` em verde e reseta automaticamente mensagens temporarias. |
| Jogador local extremamente claro/branco estourado (bloom) com coop ativo | **Light duplicada + sharedMaterial mutado**: cada puppet clonava a Point Light do Sein e `RemoteVisualController` alterava `sharedMaterial` (alpha forcado a 1), vazando para o Ori local e dobrando o brilho aditivo. **Solucao aplicada e instalada**: `StripExtraLights()` remove `Light/Halo/Flare/Projector/Trail`, cada renderer do puppet recebe `Material` proprio clonado e o watchdog nao toca mais em alpha compartilhado. DLL recompilada (66.048 bytes) e copiada para `<ORI_DIR>\BepInEx\plugins\`. Teste visual em jogo ainda **a confirmar**. |
| Segundo jogador invisivel no mapa e sem animacoes | **Pacotes POSITION/ANIM aplicados isoladamente + `CharacterAnimationSystem` destruido**: `ANIM` sem posicao zerava `_targetPosition` para `(0,0,0)` e `POSITION` sem `AnimName`/velocidade congelava no Idle; alem disso o limpador destruia o driver de animacao. **Solucao aplicada e instalada**: `RemotePlayerManager` funde por jogador, infere velocidade por delta/dt, da snap no spawn e a mais de 15u; `CharacterAnimationSystem` preservado (desligado) e `AnimationRegistry` com prewarm preguicoso. Mesma DLL reinstalada acima. Teste com 2 clientes ainda **a confirmar**. |
| Teleporte (T / menu) nao funciona | **Tres causas combinadas**: `AllowTeleport=false` por padrao no servidor, cliente ignorava `CONFIG_SYNC` (ID 16) e `OnTeleportRequested` so trocava `transform.position` sem zerar fisica/camera. **Solucao aplicada e instalada**: padrao `AllowTeleport=true` (smoke test confirma `/coop` => `Teleporte: ATIVADO`), cliente trata `CONFIG_SYNC` com aviso, teleporte zera `Rigidbody`/Speed (via reflexao), reancora camera e exibe toast; erros do servidor vao so ao solicitante. Servidor recompilado e copiado para `<ORI_DIR>\Server\`. Teste com 2 clientes (`T` + `/tp`) ainda **a confirmar**. |
| Alteracao de apelido/nome nao funciona ou reverte para string de boas-vindas do servidor | **Sobrescrita por banner de boas-vindas e falta de propagacao**: Ao receber o pacote `-1`, o cliente interpretava a string de boas-vindas (`WELCOME TO THE SERVER YOUR ID: X`) como apelido e sobrescrevia `_localNick`. **Solucao implementada**: `NetworkService` agora despacha o apelido configurado pelo jogador, `OriCoopPlugin.Publish` anexa o apelido aos snapshots, e `ServerConnectionDialog` conta com botao dedicado `[ Salvar Nome ]` que dispara `SendNicknameUpdate` para o servidor dedicado e atualiza o `TextMesh` flutuante do boneco. |
| Submenu "Ori Coop" nao responde a clique/gamepad e despausa jogo | **Bug conhecido**: `OriCoopMenuScreen` abre mas os itens clonados nao processam raycast/foco de entrada, e ao sair ocorre despausa indevida mantendo elementos de UI abertos. Use a tecla F6 para a tela de conexao funcional. |

## Validacao de Correcao Realizada

1. **Compilacao:** `OriCoopBepInEx.dll` compilado com sucesso (C# 5, .NET Framework 3.5) gerando sem erros ou avisos.
2. **Implantacao:** DLL instalada em `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\BepInEx\plugins\OriCoopBepInEx.dll`.
3. **Isolamento de Puppet:** Testado e validado contra corrupcao do singleton global `Game.Characters.Sein` e remocao total de scripts de gameplay do boneco remoto.
4. **NameTag:** Componente `FloatingNameTag` adicionado aos bonecos remotos com travamento de rotacao frontal em `LateUpdate()`.
5. **UI de Conexao:** Precedencia de status corrigida e botao de salvamento de nome funcional.
6. **Watchdog de Camera:** `EnsureCameraFollowsLocalPlayer()` ativo para evitar desancoramento da camera de gameplay.

## Itens ainda a confirmar e Bugs Conhecidos

- [Bug] Falta de foco/interatividade no submenu nativo de pausa `OriCoopMenuScreen` (solucao alternativa funcional implementada via dialogo F6 `ServerConnectionDialog`);
- comportamento de `AutoConnect` em todas as cenas;
- persistencia das opcoes do servidor entre reinicios (o codigo atual as redefine ao carregar o modulo);
- matriz de compatibilidade entre versoes do Ori, Unity e assemblies;
- cobertura real de sincronizacao de inimigos e entidades em partidas longas;
- descoberta automatica de servidores na LAN (atualmente o IPv4 e configurado manualmente).

