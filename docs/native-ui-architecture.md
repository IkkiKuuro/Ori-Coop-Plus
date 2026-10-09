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

### 4.2 Estrutura da Tela `OriCoopMenuScreen`
A tela "Ori Coop" é instanciada como uma subclasse de `MenuScreen` diretamente sob a hierarquia de `InventoryManager` com seu próprio `CleverMenuItemSelectionManager`:

```text
OriCoopMenuScreen (MenuScreen, filho de InventoryManager)
   ├── Titulo / Status: "ORI COOP | CONECTADO: 127.0.0.1:7777 | JOGADORES: 1 | PING: 24 ms" (MessageBox)
   ├── Navigation (CleverMenuItemSelectionManager - Direction: TopToBottom)
   │     ├── BtnConnection ("Configurar Conexao de Servidor") -> Abre ServerConnectionDialog
   │     ├── BtnTeleport ("Teleportar ate Parceiro")
   │     ├── BtnResync ("Ressincronizar Puppet")
   │     ├── TogglePartnerHp ("Vida do Parceiro: LIGADO/DESLIGADO")
   │     ├── ToggleNetworkLogs ("Logs Verbosos: LIGADO/DESLIGADO")
   │     └── BtnBack ("Voltar")
   └── Layout (CleverMenuItemLayout - VerticalAlignment: Center)
```

### 4.3 Transição e Preservação da Pausa Vanilla
- **Ao abrir o submenu (`Open()`):**
  - **Não** chama `InventoryManager.Instance.NavigationManager.SetVisible(false)` (o que dispararia o `TransparencyAnimator` e apagaria toda a tela do inventário).
  - Em vez disso, chama `SetCentralMenuButtonsVisible(false)`, ocultando cirurgicamente apenas os itens da coluna central (`CONTINUAR`, `AJUDA E OPÇÕES`, `Ori Coop`, `DIFICULDADE`, `SAIR`).
  - O anel de habilidades à esquerda, estatísticas à direita e os prompts inferiores permanecem perfeitamente visíveis e ambientados.
  - Trava a navegação vanilla (`IsLocked = true`, `IsActive = false`) e remove highlight do botão selecionado.
  - Ativa o contêiner do submenu e seu `CleverMenuItemSelectionManager`: define `IsSuspended = false`, `IsLocked = false`, `IsActive = true`, exibe o cabeçalho de status e foca o primeiro item (`SetIndexToFirst()`).
- **Ao voltar (`OnBackPressed()`, botão B ou Cancel):**
  - Executa `CloseSubmenuAndRestoreCentralButtons()`.
  - Oculta o contêiner do submenu e seus botões (`HideImmediate()`).
  - Reativa a visibilidade dos botões da coluna central (`SetCentralMenuButtonsVisible(true)`).
  - Destrava e reativa `InventoryManager.Instance.NavigationManager` (`IsLocked = false`, `IsActive = true`, `RefreshVisible()`), restaurando a seleção imediatamente para o botão `OriCoopButton`.
- **Ao fechar o menu externamente (tecla Start/Esc no controle ou teclado, ou unpause):**
  - O loop `Update()` detecta `Core.Input.Start.OnPressed` ou `!Game.UI.Menu.MainMenuVisible`, consome o input, chama `CloseSubmenuAndRestoreCentralButtons()` e solicita o encerramento seguro via `Game.UI.Menu.HideMenuScreen(false)`.
  - Patches Harmony em `InventoryManager.Show()` e `InventoryManager.ShowImmediate()` garantem que, caso o jogador abra o menu de pausa novamente após despausar, o estado comece 100% limpo, sem elementos residuais do submenu.

---

## 5. Resolução dos Problemas Conhecidos e Nova Interface de Conexão

### 5.1 Resolução do Bug 1: Falta de foco/interatividade e despausa indevida
(Correção aplicada no código; validação em jogo com gamepad + teclado ainda
**a confirmar** — ver `operations.md` § Itens ainda a confirmar.)
- **Causas Raiz Identificadas:**
  1. **Suspensão pelo `SuspensionManager`:** Ao instanciar `OriCoopMenuScreen` como raiz (`new GameObject("OriCoopMenuScreen")`), o `CleverMenuItemSelectionManager` se registrava no `SuspensionManager`. Durante a pausa do jogo, o `MenuScreenManager` chamava `SuspensionManager.SuspendExcluding(...)` apenas para componentes filhos de sua própria hierarquia, marcando `_navManager.IsSuspended = true`. Na IL de `CleverMenuItemSelectionManager.FixedUpdate()`, o método aborta na instrução `IL_0001: if (IsSuspended) return;`.
  2. **Flag `IsActive`:** `m_isActive` inicializa como `false` por padrão. A instrução `IL_0164: if (!IsActive) return;` abortava a verificação de D-Pad, analógico e botões de ação caso `IsActive` não fosse explicitamente ligado.
  3. **Condições Residuais nos Botões Clonados:** A clonagem de botões nativos copiava instâncias de `Condition Activated` e `Condition Visible`, fazendo com que `get_IsActivated()` e `get_Bounds()` falhassem na validação geométrica de raycast.
  4. **Despausa Indevida ao Sair:** No `Update()`, ao detectar `!MainMenuVisible` decorrente de tecla Start/Esc, a chamada a `OnBackPressed()` religava `InventoryManager.NavigationManager.SetVisible(true)` enquanto o jogo já havia sido despausado pelo motor vanilla.
- **Correções Aplicadas:**
  - `OriCoopMenuScreen` agora é filho direto do `InventoryManager.transform` (sendo automaticamente excluído de suspensão pelo `MenuScreenManager`);
  - `_navManager.IsSuspended = false` e `_navManager.IsActive = true` são garantidos tanto na inicialização quanto no `Open()` e `Update()`;
  - `NativeUIHelper.CloneNativeButton` neutraliza `item.Activated = null` e `item.Visible = null`, atribui explicitamente `item.Transform = cloneObj.transform`, calcula `item.Size`/`Center` e instancia um `BoxCollider` compatível;
  - O fechamento via Start/Esc encerra a tela sem reexibir o inventário.

### 5.2 Resolução do Bug 2: Interface In-Game para Conexão ao Servidor (`ServerConnectionDialog`)
- **Implementação:** Foi criado o diálogo modal nativo `ServerConnectionDialog` (`UI/ServerConnectionDialog.cs`), renderizado via `OnGUI` estilizado com tema ciano escuro idêntico à identidade visual do Ori DE.
- **Formas de Acesso:**
  1. Pelo botão **"Configurar Conexao de Servidor"** no submenu Ori Coop do menu de pausa;
  2. Pela tecla de atalho global **F6** a qualquer momento durante a gameplay.
- **Funcionalidades:**
  - Campo de texto interativo para **IP/Host do Servidor**;
  - Campo de texto interativo para **Porta UDP** (com validação numérica 1–65535);
  - Campo de texto interativo para **Apelido / Nickname**;
  - Botão **[ Conectar ]**: fecha conexão anterior e conecta ao novo endereço em tempo real, persistindo as opções no `com.ikkikuuro.oricoop.cfg`;
  - Botão **[ Desconectar ]**: encerra a sincronização de forma limpa;
  - Botão **[ Buscar LAN ]**: envia sonda UDP broadcast (`255.255.255.255` e `127.0.0.1`) na porta selecionada, detectando automaticamente instâncias ativas do `OriCoopDedicatedServer.exe` e preenchendo o IP detectado;
  - Indicador de status em tempo real com código de cores (Conectado / Conectando / Desconectado / Ping).

### 5.3 Resolução do Bug 3: Tela em branco ao clicar em "Ori Coop" e sobreposição ao reabrir
- **Causas Raiz Identificadas:**
  1. **Tela em Branco (Imagem 1):** `OriCoopMenuScreen.Open()` invocava `InventoryManager.Instance.NavigationManager.SetVisible(false)`. Como `InventoryManager.NavigationManager` possui um `TransparencyAnimator` com `AnimateChildren = true`, a chamada a `SetVisible(false)` iniciava `FadeAnimator.AnimatorDriver.ContinueBackwards()`, animando a opacidade de todo o inventário e de seus filhos (inclusive `OriCoopMenuScreen`) para `0.0f` (invisível).
  2. **Sobreposição de Menus (Imagem 2):** Ao reabrir o menu de pausa pelo atalho de pausa do jogo, o motor do Ori chamava `InventoryManager.Instance.Show()`, que executava `NavigationManager.SetVisible(true)` e restaurava a opacidade para `1.0f`. Como `OriCoopMenuScreen` continuava com `_isOpen = true` e seus itens ativos, ambos os grupos de botões (os 5 da coluna central vanilla e os 6 do submenu Ori Coop) eram renderizados simultaneamente no mesmo espaço central.
- **Correções Aplicadas:**
  1. **Eliminação do `SetVisible(false)` no inventário:** `OriCoopMenuScreen.Open()` agora preserva a visibilidade global do `InventoryManager`, impedindo qualquer disparo do `TransparencyAnimator`.
  2. **Ocultação Cirúrgica da Coluna Central (`SetCentralMenuButtonsVisible`):** O mod localiza os botões centrais (itens de `nav.MenuItems` sem `InventoryAbilityItem` e sem `InventoryItemHelpText`, além de `InventoryManager.Difficulty.gameObject`) e desativa especificamente esses objetos via `SetActive(false)`. O anel de habilidades à esquerda e estatísticas à direita continuam renderizados, servindo de moldura estética para o submenu central.
  3. **Trava Segura de Entrada (`IsLocked` e `IsActive`):** `InventoryManager.NavigationManager` tem `IsLocked = true` e `IsActive = false`, impedindo que cliques ou direcionais interfiram no inventário vanilla enquanto o submenu estiver aberto.
  4. **Restauração Limpa e Idempotência:** `CloseSubmenuAndRestoreCentralButtons()` reativa os botões centrais com `SetActive(true)`, destrava o `NavigationManager` e posiciona a seleção de volta no botão `OriCoopButton`. Além disso, patches em `InventoryManager.Show()` e `ShowImmediate()` garantem que qualquer nova abertura do menu de pausa comece sempre no estado vanilla padrão.


