# Arquitetura da UI Nativa e Integracao do Menu "Ori Coop"

Este documento descreve a arquitetura da interface nativa do **Ori and the Blind Forest: Definitive Edition**, o ciclo de vida do `PauseScreen`, a estrutura de componentes (`CleverMenuItem`, `CleverMenuItemSelectionManager`, `MessageBox`), e o plano de integracao do novo submenu nativo **Ori Coop**.

---

## 1. Visao Geral da UI Nativa

A interface do Ori DE nao utiliza o UGUI tradicional (Canvas/EventSystem) nem IMGUI flutuante (`OnGUI`). Ela utiliza uma hierarquia propria de GameObjects alocados na cena com:
- Componentes de texto 3D (`CatlikeCoding.TextBox.TextBox` envelopados por `MessageBox`);
- Botoes reativos (`CleverMenuItem`) com controladores de animacao (`BaseAnimator`), transicao de materiais e disparadores de audio (`PlaySoundAction`);
- Gerenciador de navegacao espacial (`CleverMenuItemSelectionManager`), que suporta controle (D-Pad e analogico com calculo angular vetorial) e teclado/mouse;
- Autolayout vertical (`CleverMenuItemLayout`) que calcula espacamento dinamico.

---

## 2. Telas de Pausa no Ori DE: `InventoryManager` vs `PauseScreen`

O Ori DE possui dois sistemas de pausa distintos controlados por `MenuScreenManager.ShowInventoryOrPauseMenu()`:

```text
MenuScreenManager.ShowInventoryOrPauseMenu()
       │
       ├── Se InventoryManager.Instance != null (Dentro de um save / gameplay ativo):
       │      └── ShowInventory() -> Abre InventoryManager (Screens.Inventory)
       │
       └── Se InventoryManager.Instance == null (Cutscenes / Prólogo inicial):
              └── ShowMenuScreen(false) -> Abre PauseScreen (Screens.Pause)
```

1. **`InventoryManager` (Menu de Pausa no Jogo / Save):**
   - É a tela que o jogador vê ao apertar `Esc` ou `Start`/`Menu` no controle durante a partida.
   - Contém o anel de habilidades (`InventoryAbilityItem`) à esquerda, estatísticas à direita e os botões de menu centrais:
     - `CONTINUAR`
     - `AJUDA E OPÇÕES`
     - `ORI COOP` (injetado pelo mod)
     - `DIFICULDADE`
     - `SAIR`
2. **`PauseScreen` (Menu de Pausa em Cutscenes):**
   - Usado exclusivamente em cutscenes e prólogos para exibir `Pular Cutscene` / `Pular Prólogo`.
   - O botão "Ori Coop" **não** deve ser injetado nesta tela para não sobrepor opções vanilla nem poluir menus de vídeo.

---

## 3. Estrutura de Componentes de Navegação

### `CleverMenuItem`
Representa um botão interativo da UI nativa:
- **`Highlight` / `Unhighlight`**: Disparadores do tipo `ActionMethod` (normalmente configurados com `PlaySoundAction` nativo e gatilhos de partículas).
- **`Pressed` / `PressedCallback`**: Ação executada ao confirmar (botão A no gamepad ou Enter/Espaço/Clique).
- **`HighlightAnimator`**: Componente `BaseAnimator` que interpola escala, cor de materiais e efeitos visuais.
- **`Space`**: Espaçamento vertical usado pelo layout.
- **`IsActivated` / `IsVisible`**: Controlam a elegibilidade do item para navegação.

### `CleverMenuItemSelectionManager`
Controla o foco e a entrada de usuário:
- **`List<CleverMenuItem> MenuItems`**: Lista linear dos itens gerenciados.
- **`List<NavigationData> Navigation`**: Lista de links direcionais `From` -> `To` com condição opcional. O método `ChangeMenuItem()` compara a direção analógica (`Core.Input.Axis`) com o vetor `(To.position - From.position)` via produto escalar (`Vector2.Dot`).
- **`Index`**: Índice do botão atualmente em foco.
- **`MoveSelection(bool forward)`**: Navega circularmente pela lista `MenuItems` quando o direcional é pressionado.
- **`OnBackPressedCallback`**: Invocado ao pressionar o botão de voltar (`Core.Input.Cancel` - botão B no controle ou Esc no teclado).

> [!IMPORTANT]
> **Prevenção de `NullReferenceException` em `RefreshVisible()`:**
> O método `CleverMenuItemSelectionManager.OnEnable()` chama imediatamente `RefreshVisible()`, que itera sobre `this.MenuItems.GetEnumerator()`. Ao criar um `GameObject` via código e chamar `AddComponent<CleverMenuItemSelectionManager>()` com o objeto ativo, o `OnEnable()` dispara antes da atribuição de `MenuItems = new List<CleverMenuItem>()`, gerando falha de referência nula. O contêiner de navegação deve ser criado inativo (`navObj.SetActive(false)`), populado com os botões e só então ativado (`navObj.SetActive(true)`).

---

## 4. Arquitetura da Injeção do Submenu "Ori Coop"

### 4.1 Injeção do Botão no `InventoryManager`
O patch Harmony em `InventoryManager.Awake()` (`InventoryScreenPatch.cs`):
1. **Idempotência**: Verifica se o botão `OriCoopButton` já foi instanciado.
2. **Localização do Template**: Encontra o botão "AJUDA E OPÇÕES" (item central sem `InventoryAbilityItem`).
3. **Clonagem e Desacoplamento**:
   - Clona o botão nativo via `NativeUIHelper.CloneNativeButton`.
   - Remove ações pré-existentes (`ShowOptionsAction`) para não disparar a tela vanilla de opções.
   - Atribui ao `PressedCallback` a chamada de abertura do submenu do mod: `OriCoopMenuScreen.Instance.Open()`.
4. **Alinhamento Espacial e Grafo de Navegação**:
   - Posiciona o botão verticalmente entre "AJUDA E OPÇÕES" e "DIFICULDADE", deslocando para baixo os itens subsequentes para evitar sobreposição visual.
   - Atualiza `NavigationManager.Navigation` adicionando vínculos bidirecionais (`Options` $\leftrightarrow$ `OriCoopButton` $\leftrightarrow$ `NextItem`), **preservando intactos** todos os links espaciais com o anel de habilidades.

### 4.2 Estrutura da Nova Tela `OriCoopMenuScreen`
A tela "Ori Coop" é instanciada como uma subclasse de `MenuScreen` com seu próprio `CleverMenuItemSelectionManager`:

```text
OriCoopMenuScreen (MenuScreen)
   ├── Titulo / Status: "ORI COOP | JOGADORES: 1 | PING: 24 ms" (MessageBox)
   ├── Navigation (CleverMenuItemSelectionManager - Direction: TopToBottom)
   │     ├── BtnTeleport ("Teleportar ate Parceiro")
   │     ├── BtnResync ("Ressincronizar Puppet")
   │     ├── TogglePartnerHp ("Vida do Parceiro: LIGADO/DESLIGADO")
   │     ├── ToggleNetworkLogs ("Logs Verbosos: LIGADO/DESLIGADO")
   │     └── BtnBack ("Voltar")
   └── Layout (CleverMenuItemLayout - VerticalAlignment: Center)
```

### 4.3 Transição e Preservação da Pausa Vanilla
- **Ao abrir o submenu (`Open()`):**
  - Oculta a navegação do inventário (`InventoryManager.Instance.NavigationManager.SetVisible(false)`).
  - O `MenuScreenManager.MainMenuVisible` permanece `true` e o `SuspensionManager` mantém todas as entidades de gameplay suspensas.
  - Ativa `OriCoopMenuScreen.NavigationManager.SetVisible(true)` e foca o primeiro botão.
- **Ao voltar (`OnBackPressed()`, botão B ou Esc):**
  - Oculta `OriCoopMenuScreen`.
  - Reexibe `InventoryManager.Instance.NavigationManager.SetVisible(true)`.
  - Restaura a seleção para o botão `OriCoopButton`.
- **Ao fechar o menu externamente (tecla Start/Menu do controle):**
  - O loop `Update()` detecta `!Game.UI.Menu.MainMenuVisible` e encerra o submenu de forma limpa.

---

## 5. Bugs Conhecidos e Pendências (UI e Conexão)

1. **Submenu 'Ori Coop' sem foco de entrada e despausa indevida da hierarquia:**
   - **Sintoma:** A tela `OriCoopMenuScreen` é renderizada, porém os itens não respondem a cliques do mouse nem à navegação por gamepad (D-Pad/analógico). Ao sair, ocorre quebra de hierarquia e o jogo no fundo é despausado mesmo com a interface de menu ainda visível na tela.
   - **Causas a confirmar / investigar:**
     - `CleverMenuItemSelectionManager` necessita de registro ou sincronização explícita com os delegates de foco globais do `MenuScreenManager`.
     - Falta de colliders/camadas ativas nos botões clonados para interação de mouse/cursor e inicialização de flags de ativação (`CleverMenuItem.IsActivated`).
     - A transição de saída deve sincronizar o estado de `SuspensionManager` para impedir despausa prematura enquanto o `InventoryManager` estiver aberto.
2. **Falta de Interface In-Game para Conexão ao Servidor:**
   - **Sintoma:** O servidor dedicado (`OriCoopDedicatedServer.exe`) pode ser iniciado com sucesso, porém não há campos ou menus no cliente para inserir IP, porta ou reconectar durante a partida.
   - **Contorno temporário:** Configuração manual prévia no arquivo `<ORI_DIR>\BepInEx\config\com.ikkikuuro.oricoop.cfg`.


