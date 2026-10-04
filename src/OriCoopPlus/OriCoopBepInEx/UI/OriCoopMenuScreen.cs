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
            if (Instance != null || invTemplate == null)
            {
                return;
            }

            GameObject screenObj = new GameObject("OriCoopMenuScreen");
            screenObj.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(screenObj);
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

            // Cria o contêiner de navegação inicialmente INATIVO para evitar NullReferenceException no OnEnable
            GameObject navObj = new GameObject("Navigation");
            navObj.SetActive(false);
            navObj.transform.SetParent(transform, false);
            navObj.transform.localPosition = new Vector3(0f, -0.3f, 0f);

            _navManager = navObj.AddComponent<CleverMenuItemSelectionManager>();
            _navManager.MenuItems = new List<CleverMenuItem>();
            _navManager.Navigation = new List<CleverMenuItemSelectionManager.NavigationData>();
            _navManager.ItemDirection = CleverMenuItemSelectionManager.Direction.TopToBottom;
            _navManager.OnBackPressedCallback = new Action(OnBackPressed);
            _navManager.HighlightOnMouseOver = true;

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
                    statusObj.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                    _statusText = statusObj.GetComponent<MessageBox>();
                    if (_statusText != null)
                    {
                        _statusText.SetMessage(new MessageDescriptor("ORI COOP PLUS | STATUS"));
                        _statusText.RefreshText();
                    }
                }
            }

            // 2. Botões
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

            // Oculta a navegação do InventoryManager sem despausar o jogo
            if (InventoryManager.Instance != null && InventoryManager.Instance.NavigationManager != null)
            {
                InventoryManager.Instance.NavigationManager.SetVisible(false);
            }

            UpdateStatusDisplay();
            Show();

            if (_navManager != null)
            {
                _navManager.RefreshVisible();
                _navManager.SetVisible(true);
                _navManager.SetIndexToFirst();
            }
        }

        public override void Show()
        {
            if (_navManager != null)
            {
                _navManager.SetVisible(true);
            }
            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(true);
            }
        }

        public override void Hide()
        {
            if (_navManager != null)
            {
                _navManager.SetVisible(false);
            }
            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(false);
            }
        }

        public override void ShowImmediate()
        {
            if (_navManager != null)
            {
                _navManager.SetVisibleImmediate(true);
            }
            if (_statusText != null)
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
            }
            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(false);
            }
        }

        public void OnBackPressed()
        {
            _isOpen = false;
            Hide();

            // Restaura o menu do save (InventoryManager) mantendo a pausa do jogo
            if (InventoryManager.Instance != null && InventoryManager.Instance.NavigationManager != null)
            {
                InventoryManager.Instance.NavigationManager.SetVisible(true);
                int coopIdx = InventoryManager.Instance.NavigationManager.MenuItems.FindIndex(delegate (CleverMenuItem m)
                {
                    return m != null && m.name == "OriCoopButton";
                });
                if (coopIdx >= 0)
                {
                    InventoryManager.Instance.NavigationManager.SetCurrentItem(coopIdx);
                }
            }
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            // Se o menu de pausa foi fechado externamente (ex: tecla Menu do controle), fecha o submenu
            if (Game.UI.Menu != null && !Game.UI.Menu.MainMenuVisible)
            {
                OnBackPressed();
                return;
            }

            _statusTimer += Time.unscaledDeltaTime;
            if (_statusTimer >= 1.0f)
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

            int ping = OriCoopPlugin.Instance.CurrentPing;
            string pingStr = ping < 0 ? "--" : ping.ToString() + " ms";
            int count = OriCoopPlugin.Instance.ConnectedPlayerCount + 1;
            string status = string.Format("ORI COOP | JOGADORES: {0} | PING: {1}", count, pingStr);

            _statusText.SetMessage(new MessageDescriptor(status));
            _statusText.RefreshText();
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
    }
}
