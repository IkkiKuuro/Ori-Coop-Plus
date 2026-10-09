# Arquitetura do Cliente BepInEx do Ori Coop

O cliente fica em [`src/OriCoopPlus/OriCoopBepInEx`](../src/OriCoopPlus/OriCoopBepInEx).
Ele é um plugin BepInEx 5.x para o Ori DE 32-bit (`net35`) e gerencia tanto a captura
do jogador local quanto a instanciação, blindagem visual e animação determinística
dos jogadores remotos.

## Camadas

```text
Plugin/OriCoopPlugin
    ├── Client/
    │    ├── RemotePlayerManager (ciclo de vida e roteamento de snapshots)
    │    ├── RemotePuppetFactory (instanciação limpa a partir de Game.Characters.Sein)
    │    ├── RemotePlayerPuppet (interpolação de transform e controle visual)
    │    ├── RemoteVisualController (watchdog LateUpdate contra desativação/culling)
    │    └── AnimationRegistry (pré-aquecimento e resolução de clipes com fallback)
    ├── UI/
    │    ├── InventoryScreenPatch (injeção do botão 'Ori Coop' no InventoryManager do save)
    │    ├── OriCoopMenuScreen (submenu nativo MenuScreen para resgate/debug/status)
    │    └── NativeUIHelper (clonagem de CleverMenuItem, MessageBox e links espaciais)
    ├── Diagnostics/
    │    └── ReplicationObservability (telemetria de pacotes e alertas de visibilidade)
    ├── Domain/ (DTOs de snapshot, posições e vetores sem dependência de engine)
    ├── Networking/NetworkService (transporte UDP e decodificação do protocolo)
    └── Patches/
         ├── SeinCharacterPatch (captura de estado local de Game.Characters.Sein)
         ├── SeinInputPatch (ponto de interceptação de input)
         ├── FrustumCullingBypassPatch (bloqueio de culling do CameraFrustumOptimizer)
         └── AnimationPrewarmPatch (pré-carregamento no CharacterAnimationSystem.Start)
```

- **Plugin:** Registra configuração via BepInEx, inicializa a rede, orquestra o
  `RemotePlayerManager` no thread principal do Unity e aplica/desfaz patches Harmony.
- **UI (Interface Nativa):**
  - Gerencia a injeção do botão "Ori Coop" na coluna central do menu de pausa do save (`InventoryManager.NavigationManager`).
  - Instancia o `OriCoopMenuScreen` clonando elementos visuais nativos e mantendo
    compatibilidade integral com gamepad (D-Pad e analógico) e teclado, preservando a pausa.
  - Documentação detalhada em [`docs/native-ui-architecture.md`](native-ui-architecture.md).
- **Client (Entidades Remotas e Visibilidade):**
  - `RemotePlayerManager`: Cria, atualiza e descarta instâncias de `RemotePlayerPuppet`
    conforme snapshots são recebidos ou jogadores desconectam.
  - `RemotePuppetFactory`: Puppet leve — instancia SÓ a subárvore visual de
    `Game.Characters.Sein` (o `GameObject` que contém cada
    `SpriteAnimatorWithTransitions`), sem clonar a raiz do Sein. Nenhum
    `Awake` de gameplay roda e nenhum singleton (`Game.Characters.Sein` /
    `Current`) é tocado, então não há mais desativação transitória nem
    `try/finally` de preservação (mantido só `EnsureCameraFollowsLocalPlayer()`
    como segurança). A limpeza por *whitelist* elimina filhos sem renderizadores/animadores, desativa imediatamente
    todos os `Behaviour` restantes e destroi colisores, rigidbodies, audios e MonoBehaviours que
    nao sejam puramente visuais (`SpriteAnimatorWithTransitions`, `CharacterSpriteMirror`,
    `RemotePlayerPuppet`, `RemoteVisualController`), prevenindo a execucao concorrente de dezenas
    de scripts de gameplay nativos e eliminando quedas de FPS causadas por loops de `NullReferenceException`.
  - `RemotePlayerPuppet`: Gerencia interpolacao de posicoes e espelhamento, contendo
    um componente `FloatingNameTag` flutuante sobre o boneco. Em `LateUpdate()`, a rotacao da NameTag
    e travada em `Quaternion.identity` para que o texto nao seja invertido quando o boneco virar para a esquerda.
  - `RemoteVisualController`: Atua em `LateUpdate()` como watchdog com taxa limitada (2x/s)
    e cache de propriedades de shader (`Shader.PropertyToID`), garantindo que
    `MeshRenderer.enabled` e `gameObject.activeSelf` permaneçam ativos e que o canal
    alpha dos materiais não seja zerado sem onerar a CPU a cada frame.
  - `AnimationRegistry`: Pré-aquece o catálogo de `TextureAnimationWithTransitions`
    carregados em memória e resolve animações por nome ou hash FNV-1a, oferecendo
    heurísticas de fallback de movimento (`Running`, `Falling`, `Jump`, `Idle`) para
    eliminar poses congeladas ou T-pose.
- **Diagnostics:**
  - `ReplicationObservability`: Registra logs estruturados com rate-limiting
    (debounce) de transições de visibilidade e taxas de pacotes de animação recebidos
    versus aplicados.
- **Patches:**
  - `SeinCharacterPatch`: Captura `transform.position`, `Speed`, `FaceLeft` e
    `CurrentAnimation.name` via tipagem direta de `SeinCharacter`, filtrando estritamente
    para o jogador local (`__instance == Game.Characters.Sein`). O puppet leve
    atual (ver `Client` acima) clona só a subárvore visual e nunca toca nos
    singletons, então não há desativação transitória nem `try/finally` de
    preservação na clonagem (mantido só `EnsureCameraFollowsLocalPlayer()`
    como segurança).
  - `FrustumCullingBypassPatch`: Intercepta `CameraFrustumOptimizer.ProcessFrustumOptimizable`
    e ignora o culling caso o componente pertença a uma entidade remota.
  - `AnimationPrewarmPatch`: Dispara `AnimationRegistry.Prewarm()` assim que o
    `CharacterAnimationSystem.Start` do jogo é executado.

## Build e Compatibilidade

O projeto compila para `.NET Framework 3.5` e referencia:
- `UnityEngine.dll` (`oriDE_Data\Managed\UnityEngine.dll`);
- `Assembly-CSharp.dll` (`oriDE_Data\Managed\Assembly-CSharp.dll`);
- `0Harmony.dll` (Harmony 2.x em `oriDE_Data\Managed\` ou `API\Client\`);
- `BepInEx.dll` (BepInEx 5.x em `API\Client\`).

Para compilar sem depender do Visual Studio IDE completo:
```powershell
powershell -ExecutionPolicy Bypass -File .\src\OriCoopPlus\OriCoopBepInEx\build.ps1
```
O script compila via `csc.exe` do .NET Framework com `/noconfig` e gera
`src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll`.

## Configuração do Entrypoint do BepInEx no Unity 5.3.2f1

No Unity 5.3.2f1 (32-bit), o carregamento de assemblies pelo `MonoManager::ReloadAssembly` ocorre
em estágios sequenciais (`UnityEngine.dll` -> `Assembly-CSharp-firstpass.dll` -> `Assembly-CSharp.dll`).
Se o BepInEx utilizar seu entrypoint padrão (`UnityEngine.dll` / `Application..cctor`), ele é
inicializado antes de `Assembly-CSharp.dll` existir no domínio Mono. Ao criar GameObjects ou
consultar tipos neste estágio, a engine nativa congela a tabela de `MonoScript` vazia, quebrando
a serialização de componentes (`Read 32 bytes but expected 48 bytes` em `LoadingBootstrap` e
`UberPoolGroupWarmer`), resultando em tela preta permanente no início do jogo.

A configuração obrigatória em `BepInEx\config\BepInEx.cfg` é:

```ini
[Preloader.Entrypoint]
Assembly = Assembly-CSharp.dll
Type = LoadingBootstrap
Method = Awake
```

Desta forma, o BepInEx aguarda a finalização completa do reload de assemblies e só inicializa
quando o primeiro script de boot do jogo (`LoadingBootstrap.Awake`) for executado.

