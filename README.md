# WW Launcher

Um sistema para criar multiplayer em jogos single-player feitos com Unity.

Nem todos os jogos do launcher estao funcionando corretamente no momento.

Discord: https://discord.gg/F2sHzGC3kb

Download: https://github.com/zheka-100500/WW_Launcher/raw/main/Launcher.zip

## Documentacao

A documentacao tecnica e de operacao fica em [`docs/`](docs/README.md). Ela
tambem registra o contexto conhecido do jogo e do mod, o protocolo de
sincronizacao, a organizacao do codigo e os pontos que ainda precisam ser
confirmados em testes.

## Ori Coop Plus

O Ori Coop Plus adiciona multiplayer cooperativo ao **Ori and the Blind Forest:
Definitive Edition**. O mod possui um modulo cliente, carregado pelo jogo, e um
modulo servidor, carregado pelo servidor dedicado. Eles devem ser colocados em
pastas diferentes.

O cliente próprio usa BepInEx 5.x em
[`src/OriCoopPlus/OriCoopBepInEx`](src/OriCoopPlus/OriCoopBepInEx) e target
estrito `.NET Framework 3.5`. O servidor próprio fica em
[`src/OriCoopDedicatedServer`](src/OriCoopDedicatedServer). Consulte
[docs/bepinex-architecture.md](docs/bepinex-architecture.md) antes de integrar
novos pacotes ou patches.

## Requisitos

- Ori and the Blind Forest: Definitive Edition instalado pela Steam.
- Windows.
- .NET SDK para compilar o projeto.
- Visual Studio 2022 ou VS Code com suporte a projetos .NET.
- Acesso a uma pasta do jogo semelhante a:

  ```text
  D:\SteamLibrary\steamapps\common\Ori DE\
  ```

Os caminhos podem ser diferentes conforme a instalacao da Steam.

## 1. Compilar o mod

1. Feche o jogo e qualquer servidor dedicado em execucao.
2. Abra a solucao:

   ```text
   src\OriCoopPlus\OriCoopPlus.sln
   ```

3. Selecione a configuracao **Release**.
4. Use **Build > Build Solution** ou pressione `Ctrl+Shift+B`.
5. Confirme que a compilacao terminou sem erros.

Tambem e possivel compilar pelo PowerShell na raiz do repositorio:

```powershell
Set-Location 'C:\Users\irani\OneDrive\Documentos\GitHub\WW_Launcher'
dotnet build .\src\OriCoopPlus\OriCoopPlus.sln --configuration Release
dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release
```

Os arquivos gerados ficam em:

```text
src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll
src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\OriCoopDedicatedServer.exe
```

## 2. Instalar os modulos

Considere `<ORI_DIR>` como a pasta de instalacao do jogo. Copie os arquivos
para estes destinos:

```text
OriCoopBepInEx.dll
  de: src\OriCoopPlus\OriCoopBepInEx\bin\Release\
  para: <ORI_DIR>\BepInEx\plugins\

OriCoopDedicatedServer.Core.dll
  de: src\OriCoopDedicatedServer\OriCoopDedicatedServer.Core\bin\Release\net8.0\
  para: <ORI_DIR>\Server\
```

Copie tambem o servidor dedicado compilado para `<ORI_DIR>\Server\`. Para
manter os arquivos correspondentes entre si, copie estes arquivos da pasta
`src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\`:

```text
OriCoopDedicatedServer.exe
OriCoopDedicatedServer.dll
OriCoopDedicatedServer.deps.json
OriCoopDedicatedServer.runtimeconfig.json
OriCoopDedicatedServer.pdb
```

Antes de substituir uma DLL, feche o jogo e o servidor. O Windows nao permite
substituir uma DLL que ainda esteja sendo usada pelo processo do jogo.

A estrutura final esperada e:

```text
<ORI_DIR>\
â”œâ”€â”€ BepInEx\
â”‚   â””â”€â”€ plugins\
â”‚       â””â”€â”€ OriCoopBepInEx.dll
â””â”€â”€ Server\
    â”œâ”€â”€ OriCoopDedicatedServer.exe
    â”œâ”€â”€ OriCoopDedicatedServer.dll
    â”œâ”€â”€ OriCoopDedicatedServer.deps.json
    â”œâ”€â”€ OriCoopDedicatedServer.runtimeconfig.json
    â””â”€â”€ OriCoopDedicatedServer.Core.dll
```

## 3. Abrir o servidor

1. Abra:

   ```text
   <ORI_DIR>\Server\
   ```

2. Execute `OriCoopDedicatedServer.exe`.
3. Quando for solicitado o numero maximo de jogadores, digite um valor de `1`
   a `10`, ou pressione `Enter` para usar `4`.
4. Quando for solicitada a porta, digite `7777`, ou pressione `Enter` para
   usar a porta padrao.
5. Aguarde mensagens semelhantes a:

   ```text
   Server started on 7777 maxplayers: 4
   Ori Coop Plus Server Module CARREGADO
   ```

6. Mantenha a janela do servidor aberta enquanto estiver jogando.

Para iniciar automaticamente com 4 jogadores e porta 7777:

```powershell
Set-Location "<ORI_DIR>\Server"
.\OriCoopDedicatedServer.exe --auto
```

Se o arquivo `Iniciar_Servidor.bat` nao estiver configurado para a sua
instalacao, execute o `OriCoopDedicatedServer.exe` diretamente usando os passos
acima.

O servidor escuta em todas as interfaces IPv4 e exibe no console as linhas
`LAN address: <IP>:<porta>`. Para clientes em outros computadores da mesma
rede, use um desses IPv4s no campo `Host` de
`BepInEx\config\com.ikkikuuro.oricoop.cfg` e mantenha `Port = 7777`.
Libere trafego UDP de entrada nessa porta no firewall do computador host.
Nao ha descoberta automatica de servidores; o IPv4 precisa ser informado
manualmente.

## 4. Entrar pelo jogo

1. Inicie o jogo normalmente.
2. Carregue um save em que o Ori ja esteja controlavel. O multiplayer nao pode
   ser testado no menu ou durante o prologo com a Naru.
3. O plugin deve carregar automaticamente a partir de `BepInEx\plugins`.
4. Confirme `Ori Coop BepInEx` no `BepInEx\LogOutput.log`.
5. O IP e a porta sao configurados em
   `BepInEx\config\com.ikkikuuro.oricoop.cfg`. Para um servidor no mesmo computador,
   use:

   ```text
   IP: 127.0.0.1
   Porta: 7777
   ```

6. Carregue um save controlavel para disparar o patch de `SeinCharacter`.
7. Para testar com outro computador, use o IP do computador que esta
   executando o servidor e libere a porta `7777` no firewall.

O plugin BepInEx fornece uma HUD própria no canto superior esquerdo, com nick,
coordenadas e ping dos jogadores conhecidos. O atalho `T` e o comando `/tp`
usam o pacote `TELEPORT_REQUEST`; a atualização de câmera e cenas carregadas
após o teleporte ainda está **a confirmar**.

Por seguranca, todas as opcoes cooperativas iniciam desligadas a cada
inicializacao do servidor. Para usar um recurso, ative-o explicitamente pelos
comandos do servidor.

## 5. Comandos do servidor

Os comandos sao digitados na janela do `OriCoopDedicatedServer.exe`.

```text
/coop
/coop tp on
/coop tp off
/coop names on
/coop names off
/tp <jogador-origem> <jogador-destino>
/clientcolors
/entitysync
/dummy
/fakeplayer
```

Use `/coop` sem argumentos para ver o estado das configuracoes. O comando
`/dummy` ativa ou desativa o bot de teste `Bot_Amigo`. O bot deve aparecer no
painel F8 com o nome recebido do servidor.

O comando `/clientcolors` alterna as cores personalizadas dos clientes e
`/entitysync` alterna a sincronizacao de entidades. Ambos iniciam desligados.

### Teleporte do host

O host pode teleportar um jogador ate outro usando o nick exato:

```text
/tp NickOrigem NickDestino
```

Por exemplo, para teleportar `Player_A` ate `Bot_Amigo`:

```text
/tp Player_A Bot_Amigo
```

Tambem e possivel usar o ID numerico do jogador:

```text
/tp 1 Bot_Amigo
```

O jogador de origem precisa estar conectado e o servidor precisa ter recebido
ao menos uma atualizacao da posicao do jogador de destino. O comando tambem
pode ser escrito como `/teleport`.

## Solucao de problemas

### O plugin nao aparece no BepInEx

Confirme que `OriCoopBepInEx.dll` foi colocado em:

```text
<ORI_DIR>\BepInEx\plugins\
```

O servidor próprio é executado diretamente por `OriCoopDedicatedServer.exe`.

### O patch nao envia snapshots

- Confirme que o jogo esta em um save no qual `SeinCharacter` exista.
- Verifique `Host`, `Port` e `PlayerId = -1` no arquivo
  `BepInEx\config\com.ikkikuuro.oricoop.cfg`.
- Reinicie o jogo depois da substituicao.
- Verifique o `BepInEx\LogOutput.log` e o console do servidor.

### O jogador aparece sem nome

- Confirme que o `OriCoopDedicatedServer.exe` e o
  `OriCoopDedicatedServer.Core.dll` vieram do mesmo build.
- Use `/coop names on` no servidor.
- Reconecte os jogadores depois de alterar a configuracao.

### O teleporte esta desativado

Execute no servidor:

```text
/coop tp on
```

Depois reconecte o cliente ou aguarde a sincronizacao da configuracao e abra o
F8 novamente.

### O Windows nao deixa copiar a DLL

O jogo ou o servidor ainda esta usando o arquivo. Feche `OriDE.exe` e
`OriCoopDedicatedServer.exe` pelo Gerenciador de Tarefas e tente copiar novamente.
