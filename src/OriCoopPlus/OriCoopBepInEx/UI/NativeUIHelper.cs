using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    public static class NativeUIHelper
    {
        public static CleverMenuItem CloneNativeButton(CleverMenuItem sourceButton, Transform parent, string name, string labelText, Action onPressed)
        {
            if (sourceButton == null)
            {
                return null;
            }

            GameObject cloneObj = UnityEngine.Object.Instantiate(sourceButton.gameObject) as GameObject;
            if (cloneObj == null)
            {
                return null;
            }

            cloneObj.name = name;
            cloneObj.transform.SetParent(parent, false);

            // Remove quaisquer ações pré-existentes do botão clonado (ex: ShowOptionsAction, ExitGameAction)
            MonoBehaviour[] scripts = cloneObj.GetComponents<MonoBehaviour>();
            for (int i = 0; i < scripts.Length; i++)
            {
                MonoBehaviour mb = scripts[i];
                if (mb != null && mb != sourceButton)
                {
                    string typeName = mb.GetType().Name;
                    if (typeName.EndsWith("Action") || typeName.EndsWith("Toggler"))
                    {
                        UnityEngine.Object.Destroy(mb);
                    }
                }
            }

            CleverMenuItem item = cloneObj.GetComponent<CleverMenuItem>();
            if (item == null)
            {
                item = cloneObj.AddComponent<CleverMenuItem>();
            }

            // Desacopla qualquer ação nativa existente no botão clonado
            item.Pressed = null;
            if (onPressed != null)
            {
                item.PressedCallback += onPressed;
            }

            SetItemText(cloneObj, labelText);
            return item;
        }

        public static void SetItemText(GameObject itemObj, string text)
        {
            if (itemObj == null)
            {
                return;
            }

            MessageBox msgBox = itemObj.GetComponentInChildren<MessageBox>();
            if (msgBox != null)
            {
                msgBox.SetMessage(new MessageDescriptor(text));
                msgBox.RefreshText();
            }
        }

        public static void BuildVerticalNavigationCage(CleverMenuItemSelectionManager nav)
        {
            if (nav == null || nav.MenuItems == null || nav.MenuItems.Count == 0)
            {
                return;
            }

            if (nav.Navigation == null)
            {
                nav.Navigation = new List<CleverMenuItemSelectionManager.NavigationData>();
            }
            else
            {
                nav.Navigation.Clear();
            }

            int count = nav.MenuItems.Count;
            for (int i = 0; i < count; i++)
            {
                CleverMenuItem current = nav.MenuItems[i];
                if (current == null)
                {
                    continue;
                }

                CleverMenuItem next = nav.MenuItems[(i + 1) % count];
                CleverMenuItem prev = nav.MenuItems[(i - 1 + count) % count];

                // Link para baixo
                CleverMenuItemSelectionManager.NavigationData nextNav = new CleverMenuItemSelectionManager.NavigationData();
                nextNav.From = current;
                nextNav.To = next;
                nav.Navigation.Add(nextNav);

                // Link para cima
                CleverMenuItemSelectionManager.NavigationData prevNav = new CleverMenuItemSelectionManager.NavigationData();
                prevNav.From = current;
                prevNav.To = prev;
                nav.Navigation.Add(prevNav);
            }
        }

        public static void InsertItemWithSpatialLinks(CleverMenuItemSelectionManager nav, CleverMenuItem prevItem, CleverMenuItem newItem, CleverMenuItem nextItem)
        {
            if (nav == null || newItem == null)
            {
                return;
            }

            if (nav.Navigation == null)
            {
                nav.Navigation = new List<CleverMenuItemSelectionManager.NavigationData>();
            }

            // Remove vínculos diretos anteriores entre prevItem e nextItem
            if (prevItem != null && nextItem != null)
            {
                nav.Navigation.RemoveAll(delegate(CleverMenuItemSelectionManager.NavigationData d)
                {
                    return (d.From == prevItem && d.To == nextItem) || (d.From == nextItem && d.To == prevItem);
                });
            }

            // Insere vínculo bidirecional entre prevItem e newItem
            if (prevItem != null)
            {
                nav.Navigation.Add(new CleverMenuItemSelectionManager.NavigationData { From = prevItem, To = newItem });
                nav.Navigation.Add(new CleverMenuItemSelectionManager.NavigationData { From = newItem, To = prevItem });

                // Replica vínculos horizontais do prevItem (ex: navegação esquerda para o anel de habilidades)
                List<CleverMenuItemSelectionManager.NavigationData> existing = new List<CleverMenuItemSelectionManager.NavigationData>(nav.Navigation);
                for (int i = 0; i < existing.Count; i++)
                {
                    CleverMenuItemSelectionManager.NavigationData d = existing[i];
                    if (d.From == prevItem && d.To != nextItem && d.To != newItem)
                    {
                        nav.Navigation.Add(new CleverMenuItemSelectionManager.NavigationData { From = newItem, To = d.To, Condition = d.Condition });
                    }
                }
            }

            // Insere vínculo bidirecional entre newItem e nextItem
            if (nextItem != null)
            {
                nav.Navigation.Add(new CleverMenuItemSelectionManager.NavigationData { From = newItem, To = nextItem });
                nav.Navigation.Add(new CleverMenuItemSelectionManager.NavigationData { From = nextItem, To = newItem });
            }
        }
    }
}
