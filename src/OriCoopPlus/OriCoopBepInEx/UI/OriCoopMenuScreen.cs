using System;
using System.Collections.Generic;
using OriCoopBepInEx.Plugin;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    public sealed class OriCoopMenuScreen : MenuScreen
    {
        public static OriCoopMenuScreen Instance { get; private set; }

        private CleverMenuItemSelectionManager _navManager;
        private MessageBox _statusText;
        private CleverMenuItem _btnConnection;
        private CleverMenuItem _btnTeleport;
        private CleverMenuItem _btnResync;
        private CleverMenuItem _btnToggleHp;
        private CleverMenuItem _btnToggleLogs;
        private CleverMenuItem _btnBack;

        private bool _showPartnerHp = true;
        private bool _showNetworkLogs = false;
        private bool _isOpen = false;
        private float _statusTimer = 0f;

        public static void Initialize(InventoryManager invTemplate)
        {
            if (invTemplate == null)
            {
                return;
            }

            if (Instance != null && Instance.gameObject != null)
            {
                UnityEngine.Object.Destroy(Instance.gameObject);
                Instance = null;
            }

            // Anexa diretamente ao GameObject do InventoryManager para pertencer a hierarquia do MenuScreenManager
            // Isso garante que o SuspensionManager considere o submenu como tela de pausa ativa e nao suspenda o CleverMenuItemSelectionManager
            GameObject screenObj = new GameObject("OriCoopMenuScreen");
            screenObj.SetActive(false);
            screenObj.transform.SetParent(invTemplate.transform, false);
            screenObj.transform.localPosition = Vector3.zero;
            screenObj.transform.localRotation = Quaternion.identity;
            screenObj.transform.localScale = Vector3.one;

            Instance = screenObj.AddComponent<OriCoopMenuScreen>();
            Instance.BuildScreen(invTemplate);
            screenObj.SetActive(true);
            Instance.HideImmediate();
        }

        private void BuildScreen(InventoryManager template)
        {
            if (template == null || template.NavigationManager == null || template.NavigationManager.MenuItems == null || template.NavigationManager.MenuItems.Count == 0)
            {
                return;
            }

            // Localiza botão de template sem InventoryAbilityItem (botão de coluna central)
            CleverMenuItem btnTemplate = null;
            for (int i = 0; i < template.NavigationManager.MenuItems.Count; i++)
            {
                CleverMenuItem it = template.NavigationManager.MenuItems[i];
                if (it != null && it.GetComponent<InventoryAbilityItem>() == null)
                {
                    btnTemplate = it;
                    break;
                }
            }

            if (btnTemplate == null)
            {
                btnTemplate = template.NavigationManager.MenuItems[0];
            }

            // Cria o contêiner de navegação inicialmente INATIVO para configurar os itens com seguranca
            GameObject navObj = new GameObject("Navigation");
            navObj.SetActive(false);
            navObj.transform.SetParent(transform, false);
            navObj.transform.localPosition = new Vector3(0f, -0.2f, 0f);

            _navManager = navObj.AddComponent<CleverMenuItemSelectionManager>();
            _navManager.MenuItems = new List<CleverMenuItem>();
            _navManager.Navigation = new List<CleverMenuItemSelectionManager.NavigationData>();
            _navManager.ItemDirection = CleverMenuItemSelectionManager.Direction.TopToBottom;
            _navManager.OnBackPressedCallback = new Action(OnBackPressed);
            _navManager.HighlightOnMouseOver = true;
            _navManager.UnhighlightOnMouseLeave = false;
            _navManager.IsActive = true;
            _navManager.IsSuspended = false;

            // 1. Cabeçalho de Status
            MessageBox textTemplate = template.Difficulty;
            if (textTemplate == null)
            {
                textTemplate = template.AbilityNameText;
            }

            if (textTemplate != null)
            {
                GameObject statusObj = UnityEngine.Object.Instantiate(textTemplate.gameObject) as GameObject;
                if (statusObj != null)
                {
                    statusObj.name = "CoopStatusText";
                    statusObj.transform.SetParent(transform, false);
                    statusObj.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                    _statusText = statusObj.GetComponent<MessageBox>();
                    if (_statusText != null)
                    {
                        _statusText.SetMessage(new MessageDescriptor("ORI COOP PLUS | STATUS"));
                        _statusText.RefreshText();
                    }
                }
            }

            // 2. Botões do Submenu
            _btnConnection = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnConnection", "Configurar Conexao de Servidor", new Action(OnConnectionClicked));
            if (_btnConnection != null)
            {
                _btnConnection.Space = 0.55f;
                _navManager.MenuItems.Add(_btnConnection);
            }

            _btnTeleport = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnTeleport", "Teleportar ate Parceiro", new Action(OnTeleportClicked));
            if (_btnTeleport != null)
            {
                _btnTeleport.Space = 0.55f;
                _navManager.MenuItems.Add(_btnTeleport);
            }

            _btnResync = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnResync", "Ressincronizar Puppet", new Action(OnResyncClicked));
            if (_btnResync != null)
            {
                _btnResync.Space = 0.55f;
                _navManager.MenuItems.Add(_btnResync);
            }

            _btnToggleHp = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnToggleHp", "Vida do Parceiro: LIGADO", new Action(OnToggleHpClicked));
            if (_btnToggleHp != null)
            {
                _btnToggleHp.Space = 0.55f;
                _navManager.MenuItems.Add(_btnToggleHp);
            }

            _btnToggleLogs = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnToggleLogs", "Logs Verbosos: DESLIGADO", new Action(OnToggleLogsClicked));
            if (_btnToggleLogs != null)
            {
                _btnToggleLogs.Space = 0.55f;
                _navManager.MenuItems.Add(_btnToggleLogs);
            }

            _btnBack = NativeUIHelper.CloneNativeButton(btnTemplate, navObj.transform, "BtnBack", "Voltar", new Action(OnBackPressed));
            if (_btnBack != null)
            {
                _btnBack.Space = 0.55f;
                _navManager.MenuItems.Add(_btnBack);
            }

            // Layout vertical
            CleverMenuItemLayout layout = navObj.AddComponent<CleverMenuItemLayout>();
            layout.MenuItems = _navManager.MenuItems;
            layout.VerticalAlignment = CleverMenuItemLayout.Alignment.Center;
            layout.Sort();

            // Grafo de navegação vertical
            NativeUIHelper.BuildVerticalNavigationCage(_navManager);

            // Ativa o contêiner de navegação agora que MenuItems e Navigation estão completamente configurados
            navObj.SetActive(true);
        }

        public void Open()
        {
            _isOpen = true;

            // 1. Oculta os botões centrais do InventoryManager (Continuar, Opções, Ori Coop, Dificuldade, Sair)
            SetCentralMenuButtonsVisible(false);

            // 2. Trava e desativa a navegação do InventoryManager sem despausar nem acionar fade out
            if (InventoryManager.Instance != null && InventoryManager.Instance.NavigationManager != null)
            {
                CleverMenuItemSelectionManager invNav = InventoryManager.Instance.NavigationManager;
                if (invNav.CurrentMenuItem != null)
                {
                    invNav.CurrentMenuItem.OnUnhighlight();
                }
                invNav.IsLocked = true;
                invNav.IsActive = false;
            }

            // 3. Atualiza dados e ativa os elementos visuais do submenu
            UpdateStatusDisplay();
            Show();

            // 4. Habilita o gerenciador de seleção do submenu e posiciona no primeiro item
            if (_navManager != null)
            {
                _navManager.gameObject.SetActive(true);
                _navManager.IsSuspended = false;
                _navManager.IsLocked = false;
                _navManager.IsActive = true;
                _navManager.SetVisible(true);
                _navManager.RefreshVisible();
                _navManager.SetIndexToFirst();
            }
        }

        public void SetCentralMenuButtonsVisible(bool visible)
        {
            if (InventoryManager.Instance == null || InventoryManager.Instance.NavigationManager == null)
            {
                return;
            }

            CleverMenuItemSelectionManager nav = InventoryManager.Instance.NavigationManager;
            if (nav.MenuItems != null)
            {
                for (int i = 0; i < nav.MenuItems.Count; i++)
                {
                    CleverMenuItem it = nav.MenuItems[i];
                    if (it == null) continue;

                    // Ignora itens de habilidade na roda esquerda
                    if (it.GetComponent<InventoryAbilityItem>() != null) continue;

                    // Ignora itens com texto de ajuda/estatísticas na direita
                    if (it.GetComponent<InventoryItemHelpText>() != null) continue;

                    // Oculta/exibe o botão da coluna central
                    it.gameObject.SetActive(visible);
                }
            }

            // Oculta/exibe o texto de dificuldade da coluna central
            if (InventoryManager.Instance.Difficulty != null && InventoryManager.Instance.Difficulty.gameObject != null)
            {
                InventoryManager.Instance.Difficulty.gameObject.SetActive(visible);
                if (visible)
                {
                    InventoryManager.Instance.Difficulty.RefreshText();
                }
            }
        }

        public void CloseSubmenuAndRestoreCentralButtons()
        {
            _isOpen = false;

            // 1. Oculta imediatamente os itens do submenu Ori Coop
            HideImmediate();

            // 2. Reativa os botões centrais do InventoryManager
            SetCentralMenuButtonsVisible(true);

            // 3. Destrava e reativa a navegação do InventoryManager
            if (InventoryManager.Instance != null && InventoryManager.Instance.NavigationManager != null)
            {
                CleverMenuItemSelectionManager nav = InventoryManager.Instance.NavigationManager;
                nav.IsLocked = false;
                nav.IsActive = true;
                nav.IsSuspended = false;
                nav.RefreshVisible();

                // Posiciona a seleção de volta no botão Ori Coop
                int coopIdx = nav.MenuItems.FindIndex(delegate (CleverMenuItem m)
                {
                    return m != null && m.name == "OriCoopButton";
                });
                if (coopIdx >= 0)
                {
                    nav.SetCurrentItem(coopIdx);
                }
                else
                {
                    nav.SetIndexToFirst();
                }
            }
        }

        public override void Show()
        {
            if (_navManager != null)
            {
                _navManager.gameObject.SetActive(true);
                _navManager.IsSuspended = false;
                _navManager.IsLocked = false;
                _navManager.IsActive = true;
                _navManager.SetVisible(true);
            }
            if (_statusText != null && _statusText.gameObject != null)
            {
                _statusText.gameObject.SetActive(true);
            }
        }

        public override void Hide()
        {
            _isOpen = false;
            if (_navManager != null)
            {
                _navManager.SetVisible(false);
                _navManager.IsActive = false;
                _navManager.IsLocked = true;
                _navManager.gameObject.SetActive(false);
            }
            if (_statusText != null && _statusText.gameObject != null)
            {
                _statusText.gameObject.SetActive(false);
            }
        }

        public override void ShowImmediate()
        {
            if (_navManager != null)
            {
                _navManager.gameObject.SetActive(true);
                _navManager.IsSuspended = false;
                _navManager.IsLocked = false;
                _navManager.IsActive = true;
                _navManager.SetVisibleImmediate(true);
            }
            if (_statusText != null && _statusText.gameObject != null)
            {
                _statusText.gameObject.SetActive(true);
            }
        }

        public override void HideImmediate()
        {
            _isOpen = false;
            if (_navManager != null)
            {
                _navManager.SetVisibleImmediate(false);
                _navManager.IsActive = false;
                _navManager.IsLocked = true;
                _navManager.gameObject.SetActive(false);
            }
            if (_statusText != null && _statusText.gameObject != null)
            {
                _statusText.gameObject.SetActive(false);
            }
        }

        public void OnBackPressed()
        {
            bool menuStillOpen = Game.UI.Menu != null && Game.UI.Menu.MainMenuVisible;
            CloseSubmenuAndRestoreCentralButtons();

            if (!menuStillOpen && Game.UI.Menu != null)
            {
                Game.UI.Menu.HideMenuScreen(false);
            }
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            // Se o diálogo de conexão do servidor estiver aberto sobre a tela, delega o input a ele
            if (ServerConnectionDialog.Instance != null && ServerConnectionDialog.Instance.IsOpen)
            {
                return;
            }

            // Se o menu de pausa foi fechado externamente (ex: tecla Menu/Start do controle ou unpause pelo jogo)
            if (Game.UI.Menu != null && !Game.UI.Menu.MainMenuVisible)
            {
                CloseSubmenuAndRestoreCentralButtons();
                return;
            }

            // Resiliência de foco: garante que o selection manager mantenha estado operacional
            if (_navManager != null)
            {
                if (_navManager.IsSuspended)
                {
                    _navManager.IsSuspended = false;
                }
                if (_navManager.IsLocked)
                {
                    _navManager.IsLocked = false;
                }
                if (!_navManager.IsActive)
                {
                    _navManager.IsActive = true;
                }
            }

            // Tecla Start / Esc para fechar o menu completo de pausa e retornar ao jogo
            if (Core.Input.Start != null && Core.Input.Start.OnPressed && !Core.Input.Start.Used)
            {
                Core.Input.Start.Used = true;
                CloseSubmenuAndRestoreCentralButtons();
                if (Game.UI.Menu != null)
                {
                    Game.UI.Menu.HideMenuScreen(false);
                }
                return;
            }

            // Tecla Cancel (B do controle ou ESC) para retornar ao InventoryManager (caso nao consumida pelo _navManager)
            if (Core.Input.Cancel != null && Core.Input.Cancel.OnPressed && !Core.Input.Cancel.Used)
            {
                Core.Input.Cancel.Used = true;
                OnBackPressed();
                return;
            }

            _statusTimer += Time.unscaledDeltaTime;
            if (_statusTimer >= 0.8f)
            {
                _statusTimer = 0f;
                UpdateStatusDisplay();
            }
        }

        public void UpdateStatusDisplay()
        {
            if (_statusText == null || OriCoopPlugin.Instance == null)
            {
                return;
            }

            string status;
            if (OriCoopPlugin.Instance.IsConnected)
            {
                int ping = OriCoopPlugin.Instance.CurrentPing;
                string pingStr = ping < 0 ? "--" : ping.ToString() + " ms";
                int count = OriCoopPlugin.Instance.ConnectedPlayerCount + 1;
                status = string.Format("ORI COOP | CONECTADO: {0}:{1} | JOGADORES: {2} | PING: {3}",
                    OriCoopPlugin.Instance.ServerHost,
                    OriCoopPlugin.Instance.ServerPort,
                    count,
                    pingStr);
            }
            else
            {
                status = string.Format("ORI COOP | DESCONECTADO ({0}:{1}) | CLIQUE EM CONFIGURAR CONEXAO",
                    OriCoopPlugin.Instance.ServerHost,
                    OriCoopPlugin.Instance.ServerPort);
            }

            _statusText.SetMessage(new MessageDescriptor(status));
            _statusText.RefreshText();
        }

        private void OnConnectionClicked()
        {
            if (ServerConnectionDialog.Instance != null)
            {
                ServerConnectionDialog.Instance.Open();
            }
        }

        private void OnTeleportClicked()
        {
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.TeleportToNearestPartner();
            }
            OnBackPressed();
        }

        private void OnResyncClicked()
        {
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.ForceResyncPuppets();
            }
            OnBackPressed();
        }

        private void OnToggleHpClicked()
        {
            _showPartnerHp = !_showPartnerHp;
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.ShowPartnerHp = _showPartnerHp;
            }
            if (_btnToggleHp != null)
            {
                NativeUIHelper.SetItemText(_btnToggleHp.gameObject, "Vida do Parceiro: " + (_showPartnerHp ? "LIGADO" : "DESLIGADO"));
            }
        }

        private void OnToggleLogsClicked()
        {
            _showNetworkLogs = !_showNetworkLogs;
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.SetVerboseLogging(_showNetworkLogs);
            }
            if (_btnToggleLogs != null)
            {
                NativeUIHelper.SetItemText(_btnToggleLogs.gameObject, "Logs Verbosos: " + (_showNetworkLogs ? "LIGADO" : "DESLIGADO"));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
