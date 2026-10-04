using System;
using System.Collections.Generic;
using HarmonyLib;
using OriCoopBepInEx.Plugin;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    [HarmonyPatch(typeof(InventoryManager), "Awake")]
    public static class InventoryScreenPatch
    {
        public static void Postfix(InventoryManager __instance)
        {
            try
            {
                if (__instance == null || __instance.NavigationManager == null)
                {
                    return;
                }

                CleverMenuItemSelectionManager nav = __instance.NavigationManager;
                if (nav.MenuItems == null || nav.MenuItems.Count == 0)
                {
                    return;
                }

                // Idempotência: impede inserção duplicada
                for (int i = 0; i < nav.MenuItems.Count; i++)
                {
                    if (nav.MenuItems[i] != null && nav.MenuItems[i].name == "OriCoopButton")
                    {
                        return;
                    }
                }

                if (nav.transform.Find("OriCoopButton") != null)
                {
                    return;
                }

                // Inicializa a tela secundária OriCoopMenuScreen com o InventoryManager como base
                OriCoopMenuScreen.Initialize(__instance);

                // Localiza os botões centrais (não-habilidades): "Options" e o botão imediatamente seguinte ("Difficulty" ou "Exit")
                CleverMenuItem optionsItem = null;
                CleverMenuItem nextItem = null;
                int optionsIndex = -1;

                for (int i = 0; i < nav.MenuItems.Count; i++)
                {
                    CleverMenuItem it = nav.MenuItems[i];
                    if (it == null) continue;
                    if (it.GetComponent<InventoryAbilityItem>() != null) continue;

                    string lower = it.name.ToLower();
                    if (optionsItem == null && (lower.Contains("option") || lower.Contains("setting") || lower.Contains("ajuda") || it.GetComponent<ShowOptionsAction>() != null))
                    {
                        optionsItem = it;
                        optionsIndex = i;
                    }
                    else if (optionsItem != null && nextItem == null)
                    {
                        nextItem = it;
                    }
                }

                // Fallback caso não encontre pelo nome
                if (optionsItem == null)
                {
                    for (int i = 0; i < nav.MenuItems.Count; i++)
                    {
                        CleverMenuItem it = nav.MenuItems[i];
                        if (it != null && it.GetComponent<InventoryAbilityItem>() == null)
                        {
                            optionsItem = it;
                            optionsIndex = i;
                            break;
                        }
                    }
                }

                if (optionsItem == null)
                {
                    optionsItem = nav.MenuItems[0];
                    optionsIndex = 0;
                }

                CleverMenuItem coopBtn = NativeUIHelper.CloneNativeButton(
                    optionsItem,
                    optionsItem.transform.parent,
                    "OriCoopButton",
                    "Ori Coop",
                    new Action(OnCoopButtonClicked)
                );

                if (coopBtn == null)
                {
                    return;
                }

                // Insere na lista logo após o botão Options
                int insertIndex = optionsIndex + 1;
                if (insertIndex > nav.MenuItems.Count)
                {
                    insertIndex = nav.MenuItems.Count;
                }
                nav.MenuItems.Insert(insertIndex, coopBtn);

                // Ajuste de posicionamento vertical
                CleverMenuItemLayout layout = optionsItem.transform.parent != null ? 
                    optionsItem.transform.parent.GetComponent<CleverMenuItemLayout>() : null;
                if (layout == null)
                {
                    layout = nav.GetComponent<CleverMenuItemLayout>();
                }

                if (layout != null)
                {
                    if (layout.MenuItems != null && !layout.MenuItems.Contains(coopBtn))
                    {
                        int layoutIdx = layout.MenuItems.IndexOf(optionsItem);
                        if (layoutIdx >= 0)
                        {
                            layout.MenuItems.Insert(layoutIdx + 1, coopBtn);
                        }
                        else
                        {
                            layout.MenuItems.Add(coopBtn);
                        }
                    }
                    coopBtn.Space = optionsItem.Space > 0f ? optionsItem.Space : 0.5f;
                    layout.Sort();
                }
                else
                {
                    // Sem CleverMenuItemLayout: calcula o espaçamento geométrico a partir de optionsItem e nextItem
                    Vector3 delta = Vector3.down * 0.45f;
                    if (nextItem != null)
                    {
                        delta = nextItem.transform.localPosition - optionsItem.transform.localPosition;
                        if (delta.magnitude < 0.1f)
                        {
                            delta = Vector3.down * 0.45f;
                        }
                    }

                    coopBtn.transform.localPosition = optionsItem.transform.localPosition + delta;

                    // Desloca para baixo todos os itens centrais posteriores
                    bool isAfter = false;
                    for (int i = 0; i < nav.MenuItems.Count; i++)
                    {
                        CleverMenuItem it = nav.MenuItems[i];
                        if (it == null || it.GetComponent<InventoryAbilityItem>() != null) continue;
                        if (it == coopBtn) { isAfter = true; continue; }
                        if (isAfter)
                        {
                            it.transform.localPosition += delta;
                        }
                    }
                }

                // Atualiza o grafo de navegação espacial sem destruir os vínculos nativos existentes
                NativeUIHelper.InsertItemWithSpatialLinks(nav, optionsItem, coopBtn, nextItem);

                OriCoopPlugin.LogInfo("[UI-Hook] Botao 'Ori Coop' injetado com sucesso no InventoryManager (menu de pausa do save).");
            }
            catch (Exception ex)
            {
                OriCoopPlugin.LogError("[UI-Hook] Falha ao injetar botao no InventoryManager: " + ex.Message);
            }
        }

        private static void OnCoopButtonClicked()
        {
            if (OriCoopMenuScreen.Instance != null)
            {
                OriCoopMenuScreen.Instance.Open();
            }
        }
    }
}
