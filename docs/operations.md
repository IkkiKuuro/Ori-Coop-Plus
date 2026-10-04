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

O scaffolding BepInEx tambem pode ser compilado isoladamente:

```powershell
dotnet build .\src\OriCoopPlus\OriCoopBepInEx\OriCoopBepInEx.csproj --configuration Release
```

A saida esperada e
`src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll`.

Se o PowerShell estiver em outra pasta, use o caminho absoluto do projeto:

```powershell
dotnet build 'C:\Users\irani\OneDrive\Documentos\GitHub\WW_Launcher\src\OriCoopPlus\OriCoopBepInEx\OriCoopBepInEx.csproj' --configuration Release
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
desligamento. A renderização/aplicação visual completa de jogadores remotos
ainda está **a confirmar** no cliente BepInEx atual.

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

## Diagnostico rapido

| Sintoma | Verificacoes |
| --- | --- |
| Erro sobre `UnityEngine` ou `Assembly-CSharp` | Confirme as DLLs do jogo e do BepInEx; o plugin deve estar em `BepInEx\plugins` |
| F8 nao abre | save controlavel, DLL correta, reinicio do jogo e log de carregamento |
| Jogador sem nome | cliente/servidor da mesma versao e `/coop names on` |
| Teleporte indisponivel | use `/coop tp on`, mantenha dois jogadores conectados e aguarde snapshots; `T` teleporta para o remoto mais proximo e `/tp <origem> <destino>` continua disponivel |
| DLL nao pode ser copiada | encerre o jogo e `OriCoopDedicatedServer.exe` |
| Servidor cheio | reduza conexoes ou inicie com maximo entre 1 e 10 |
| Cliente LAN nao conecta | confirme o IPv4 `LAN address`, a porta UDP, o firewall do host e se todos estao na mesma rede |
| `KeyNotFoundException` com a chave `4` ao conectar | substitua o executável pelo build atual; o servidor deve criar e percorrer exatamente os slots configurados |

## Itens ainda a confirmar

- comportamento de `AutoConnect` em todas as cenas;
- persistencia das opcoes do servidor entre reinicios (o codigo atual as
  redefine ao carregar o modulo);
- matriz de compatibilidade entre versoes do Ori, Unity e assemblies;
- cobertura real de sincronizacao de inimigos e entidades em partidas longas.
- descoberta automatica de servidores na LAN (atualmente o IPv4 e configurado manualmente).
- aplicacao visual completa de todos os jogadores remotos e sincronizacao de
  inimigos/entidades em partidas longas.
