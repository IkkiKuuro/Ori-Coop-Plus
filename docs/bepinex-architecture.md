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
- **Client (Entidades Remotas e Visibilidade):**
  - `RemotePlayerManager`: Cria, atualiza e descarta instâncias de `RemotePlayerPuppet`
    conforme snapshots são recebidos ou jogadores desconectam.
  - `RemotePuppetFactory`: Clona a hierarquia visual de `Game.Characters.Sein`,
    removendo estritamente scripts de input (`SeinController`, `SeinInput`), física
    concorrente (`Rigidbody`), controladores de morte/inventário e otimizadores de
    frustum nativos. O GameObject resultante é alocado sob `DontDestroyOnLoad`.
  - `RemoteVisualController`: Atua em `LateUpdate()` como watchdog, garantindo que
    `MeshRenderer.enabled` e `gameObject.activeSelf` permaneçam ativos e que o canal
    alpha dos materiais não seja zerado por cutscenes ou gatilhos de cenário.
  - `AnimationRegistry`: Pré-aquece o catálogo de `TextureAnimationWithTransitions`
    carregados em memória e resolve animações por nome ou hash FNV-1a, oferecendo
    heurísticas de fallback de movimento (`Running`, `Falling`, `Jump`, `Idle`) para
    eliminar poses congeladas ou T-pose.
- **Diagnostics:**
  - `ReplicationObservability`: Registra logs estruturados com rate-limiting
    (debounce) de transições de visibilidade e taxas de pacotes de animação recebidos
    versus aplicados.
- **Patches:**
  - `FrustumCullingBypassPatch`: Intercepta `CameraFrustumOptimizer.ProcessFrustumOptimizable`
    e ignora o culling caso o componente pertença a uma entidade remota.
  - `AnimationPrewarmPatch`: Dispara `AnimationRegistry.Prewarm()` assim que o
    `CharacterAnimationSystem.Start` do jogo é executado.
  - `SeinCharacterPatch`: Captura `transform.position`, `Speed`, `FaceLeft` e
    `CurrentAnimation.name` via tipagem direta confirmada em `SeinCharacter`.

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
