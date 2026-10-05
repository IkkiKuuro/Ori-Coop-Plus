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
| `/tp <origem> <destino>` | teleporta a origem ate o destino; alias `/teleport` |
| `/clientcolors` | alterna cores de clientes; aliases `cc`, `clientc`, `ccolors` |
| `/entitysync` | alterna sincronizacao de entidades; aliases `es`, `sync` |
| `/dummy` | controla o bot de teste; aliases `bot`, `testbot` |
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

318: 
319: 5. **Validacao de Correcao de Lag e Nomes do Bot Dummy / Puppets:**
320:    - **Causa raiz diagnosticada:** `RemotePuppetFactory.CleanPuppetComponents` destruía apenas 13 componentes pontuais, deixando dezenas de controladores de gameplay (`SeinPrefabFactory`, `SeinDamageReciever`, `SkillItem`, `PlayerGrabPushPullHintSystem`, etc.). Na inicialização do clone, `SeinPrefabFactory` instanciava dezenas de prefabs aninhados com UI e `SeinDamageReciever` lançava `NullReferenceException` ininterruptamente em `OnEnable` e `FixedUpdate` (gerando mais de 27.000 exceções no log). Como o `SetActive(true)` falhava com exceção, o puppet não era retornado para o cache `_puppets[999]` do `RemotePlayerManager`, fazendo com que a cada pacote do dummy (a 10 Hz) um novo clone de Sein fosse gerado, acumulando dezenas de GameObjects e nomes na mesma posição com queda drástica de FPS.
321:    - **Solucao aplicada:**
322:      - `CleanPuppetComponents` agora elimina com segurança todos os GameObjects e componentes não-visuais, preservando estritamente `Transform`, `Renderer`, `MeshFilter`, `SpriteAnimatorWithTransitions`, `CharacterSpriteMirror`, `RemotePlayerPuppet` e `RemoteVisualController`.
323:      - Criação do componente `FloatingNameTag.cs`: anexa um único TextMesh flutuante e sombra acima da cabeça do puppet (offset Y 1.35f, rotação travada em `Quaternion.identity` e escala X normalizada), com suporte a atualização de apelido e cor customizada (Cyan para o bot).
324:      - Silenciamento do log em `RemoteVisualController.EnforceVisibility` em `LateUpdate`, eliminando mais de 37.000 operações de escrita de I/O em disco por minuto causadas pelo watchdog.
325: 6. **Validacao da Interface de Conexao e Notificacoes:**
326:    - **Feedback do Dialogo de Conexao (`ServerConnectionDialog`):** Ajustada a precedência do status para verificar `OriCoopPlugin.Instance.IsConnected` antes de `_statusFeedback` e limpar o texto transitório `"Conectando..."`, exibindo imediatamente `<color=#00ff88>CONECTADO</color> (ID: X | Ping: Y ms | Parceiros: Z)`.
327:    - **Notificacoes Nativas em Jogo:** Implementado `NativeUIHelper.ShowToast` utilizando `Game.UI.Hints.Show` para exibir avisos visuais elegantes na tela do jogo sempre que o jogador local conecta ou parceiros/bots entram na partida (`[+] Jogador conectou!`).
328:    - **Compatibilidade Retroativa de Strings no Servidor:** `Packet.ReadString()` no `OriCoopDedicatedServer.Core` agora suporta automaticamente tanto prefixos de 4 bytes Int32 quanto codificação padrão LEB128 de 1 byte de clientes antigos, eliminando o erro `Could not read player nickname from ...: Could not read value of type 'string'!`.
329: 
330: 
331: ## Diagnostico rapido
332: 
333: | Sintoma | Verificacoes |
334: | --- | --- |
335: | Crash no inicio com Access Violation (0xc0000005) em `mono.dll` ao carregar UnityExplorer | **Incompatibilidade do UniverseLib com Unity 5.3.2**: O UniverseLib 1.5.1 requer Unity 5.3.4+ para deserialização de UI e causa falha de segmentação no runtime Mono do Ori DE (5.3.2f1). Desative o UnityExplorer movendo sua pasta para fora de `plugins/`. O mod Ori Coop não depende do UnityExplorer. |
336: | Tela preta no inicio do jogo (`Object::FindAnyObjectOfType<MonoBehaviour>`, `The referenced script on this Behaviour is missing!` e erro de layout de serializacao) | **Entrypoint prematuro do BepInEx no Unity 5.3.2f1**: O entrypoint padrao em `UnityEngine.dll` roda antes de `Assembly-CSharp.dll` ser indexado, corrompendo o cache nativo de `MonoScript`. Solucao: configurar em `BepInEx\config\BepInEx.cfg`: `Assembly = Assembly-CSharp.dll`, `Type = LoadingBootstrap`, `Method = Awake`, e restaurar `UnityEngine.dll` original executando `.\scripts\clean_unityengine_cecil.ps1` caso tenha sido alterado por loaders legados. Para verificar logs de erro, use `.\scripts\check_unity_logs.ps1`. |
337: | BepInEx nao carrega, `LogOutput.log` nao existe e F7 nao funciona | **Arquitetura (bitness) incorreta**: `oriDE.exe` e um binario 32-bit (x86). Se `winhttp.dll` for 64-bit, o Windows ignora a DLL. Instale a versao 32-bit (`BepInEx_win_x86_5.4.x.zip`). |
338: | Erro sobre `UnityEngine` ou `Assembly-CSharp` | Confirme as DLLs do jogo e do BepInEx; o plugin deve estar em `BepInEx\plugins` |
339: | F8 nao abre | save controlavel, DLL correta, reinicio do jogo e log de carregamento |
340: | Jogador sem nome | cliente/servidor da mesma versao e `/coop names on` |
341: | Teleporte indisponivel | use `/coop tp on`, mantenha dois jogadores conectados e aguarde snapshots; `T` teleporta para o remoto mais proximo e `/tp <origem> <destino>` continua disponivel |
342: | DLL nao pode ser copiada | encerre o jogo e `OriCoopDedicatedServer.exe` |
343: | Servidor cheio | reduza conexoes ou inicie com maximo entre 1 e 10 |
344: | Cliente LAN nao conecta | confirme o IPv4 `LAN address`, a porta UDP, o firewall do host e se todos estao na mesma rede |
345: | `KeyNotFoundException` com a chave `4` ao conectar | substitua o executável pelo build atual; o servidor deve criar e percorrer exatamente os slots configurados |
346: | `Could not read player nickname from ...: Could not read value of type 'string'!` | Incompatibilidade de serialização de string de clientes legados com LEB128 corrigida no `Packet.ReadString()` dual-mode do servidor. |
347: | Bot Dummy / Jogador com múltiplos nomes empilhados e travamento grave (lag) | Clones de Sein acumulavam controladores de gameplay não limpos gerando exceções repetitivas no `SetActive` e recriando instâncias a cada snapshot. Resolvido com limpeza profunda de componentes no `RemotePuppetFactory` e anexação única do `FloatingNameTag`. |
348: | Diálogo de conexão F6 fica preso em "Conectando..." mesmo após conectar | `_statusFeedback` tinha precedência indevida sobre `IsConnected`. Corrigido no `ServerConnectionDialog`. |
349: | Submenu "Ori Coop" não responde a clique/gamepad e despausa jogo | **Bug conhecido**: `OriCoopMenuScreen` abre mas os itens clonados não processam raycast/foco de entrada, e ao sair ocorre despausa indevida mantendo elementos de UI abertos. Use a tecla F6 para a tela de conexão funcional. |
350: 
351: ## Itens ainda a confirmar e Bugs Conhecidos
352: 
353: - [Bug] Falta de foco/interatividade no submenu nativo de pausa `OriCoopMenuScreen` (solução alternativa funcional implementada via diálogo F6 `ServerConnectionDialog`);
354: - comportamento de `AutoConnect` em todas as cenas;
355: - persistencia das opcoes do servidor entre reinicios (o codigo atual as redefine ao carregar o modulo);
356: - matriz de compatibilidade entre versoes do Ori, Unity e assemblies;
357: - cobertura real de sincronizacao de inimigos e entidades em partidas longas;
358: - descoberta automatica de servidores na LAN (atualmente o IPv4 e configurado manualmente ou via busca UDP na porta 7777).

