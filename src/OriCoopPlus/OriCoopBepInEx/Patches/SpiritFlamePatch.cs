using System;
using System.Reflection;
using HarmonyLib;
using OriCoopBepInEx.Client;
using OriCoopBepInEx.Domain;
using OriCoopBepInEx.Events;
using OriCoopBepInEx.Plugin;
using UnityEngine;

namespace OriCoopBepInEx.Patches
{
    // Detector do disparo de Spirit Flame (fase 3, D-06/D-07). Alvo confirmado
    // por leitura direta dos metadados do Assembly-CSharp instalado (Wave 0):
    // SeinSpiritFlameAbility declara ThrowSpiritFlames(SpiritFlame), instância,
    // void; campo m_sein : SeinCharacter presente nas 3 classes de flame.
    // A hipotese OnShoot foi refutada (existe so em classes inimigas).
    // Direcao por fallback de facing (Sein.FaceLeft -> +-X) e origem na
    // posicao do Sein, conforme fallback autorizado no gate. Zero logica de
    // rede aqui (D-05): so detecta, filtra e delega ao PlayerEventCore.
    [HarmonyPatch(typeof(SeinSpiritFlameAbility), "ThrowSpiritFlames")]
    internal static class SpiritFlamePatch
    {
        private static void Postfix(SeinSpiritFlameAbility __instance, SeinCharacter ___m_sein)
        {
            if (OriCoopPlugin.Instance == null)
            {
                return;
            }
            SeinCharacter owner = ___m_sein;
            if (owner == null)
            {
                owner = ResolveOwner(__instance);
            }
            // So o jogador local publica (D-07): puppet/remoto nunca publica.
            if (owner == null || owner != Game.Characters.Sein)
            {
                return;
            }
            Vector3 origin;
            bool faceLeft;
            try
            {
                origin = owner.transform.position;
                faceLeft = owner.FaceLeft;
            }
            catch
            {
                return;
            }
            // Origem no ORBE (R3): o tiro real parte de Ori.get_Position
            // (StartPosition), nao do corpo — com a origem no corpo o bolt
            // nascia ~1 unidade afastado da bola. Fallback: corpo.
            try
            {
                Ori orb = Game.Characters.Ori;
                if (orb != null && orb.gameObject != null)
                {
                    origin = orb.transform.position;
                }
            }
            catch { }
            // Cache do prefab real (R3): esta instancia e a habilidade exata
            // que disparou — entrega o prefab sem busca. O espelho remoto
            // (SeinVisualMirror.ResolveProjectilePrefab) usa esse cache.
            // Reflexao no tipo real: as subclasses de CharacterState sao
            // irmas (cast direto nao compila — CS0039); CurrentSpiritFlame
            // vive na subclasse concreta (padrao ResolveOwner abaixo).
            try
            {
                PropertyInfo curProp = __instance.GetType().GetProperty(
                    "CurrentSpiritFlame",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (curProp != null)
                {
                    object flameObj = null;
                    try { flameObj = curProp.GetValue(__instance, null); }
                    catch { flameObj = null; }
                    SpiritFlame flame = flameObj as SpiritFlame;
                    if (flame != null && flame.Projectile != null)
                    {
                        SeinVisualMirror.NoteLocalPrefabs(flame.Projectile);
                    }
                }
            }
            catch { }
            float dx = faceLeft ? -1f : 1f;
            // Mira real (R3-beam): o tiro do jogo trava em ClosestAttackables
            // (posicao do alvo), nao no facing. Le a mira exata no Postfix;
            // sem alvo: facing*5 (TempTarget real ~4.5 a frente). A direcao
            // e a normalizada (mira-origem); a mira viaja no payload v2.
            Vector3 aimPoint = origin + new Vector3(dx * 5f, 0f, 0f);
            try
            {
                aimPoint = ReadAimPoint(___m_sein, owner, origin, aimPoint);
            }
            catch { }
            Vector3 diff = aimPoint - origin;
            if (diff.sqrMagnitude > 0.0001f)
            {
                diff.Normalize();
                dx = diff.x;
            }
            else
            {
                diff = new Vector3(dx, 0f, 0f);
            }
            Vector3Data direction = new Vector3Data(diff.x, diff.y, diff.z);
            Vector3Data originData = new Vector3Data(origin.x, origin.y, origin.z);
            Vector3Data aimData = new Vector3Data(aimPoint.x, aimPoint.y, aimPoint.z);
            long timestamp = DateTime.UtcNow.Ticks;
            // Linha temporaria de deteccao (piloto): confirma no LogOutput.log
            // que o patch disparou no tiro local. Virar verbosa apos validacao.
            OriCoopPlugin.LogInfo(string.Format("[EVENT] SpiritFlame detected dir=({0:F1},{1:F1},{2:F1}) origin=({3:F1},{4:F1},{5:F1})",
                direction.X, direction.Y, direction.Z, originData.X, originData.Y, originData.Z));
            PlayerEventCore.PublishSpiritFlame(direction, originData, aimData, timestamp);
        }

        // Mira travada do disparo: primeiro alvo de ClosestAttackables
        // (IAttackable.Position). Tudo defensivo: tipo da lista/elemento
        // pode variar; qualquer falha cai no fallback de facing do chamador.
        private static Vector3 ReadAimPoint(SeinCharacter injected, SeinCharacter owner, Vector3 origin, Vector3 fallback)
        {
            try
            {
                SeinCharacter sein = injected != null ? injected : owner;
                if (sein == null)
                {
                    return fallback;
                }
                object abilities = null;
                try
                {
                    PropertyInfo abProp = sein.GetType().GetProperty(
                        "Abilities", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (abProp != null)
                    {
                        abilities = abProp.GetValue(sein, null);
                    }
                    if (abilities == null)
                    {
                        FieldInfo abField = sein.GetType().GetField(
                            "Abilities", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (abField != null)
                        {
                            abilities = abField.GetValue(sein);
                        }
                    }
                }
                catch { abilities = null; }
                if (abilities == null)
                {
                    return fallback;
                }
                object targetting = null;
                try
                {
                    PropertyInfo tgProp = abilities.GetType().GetProperty(
                        "SpiritFlameTargetting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tgProp != null)
                    {
                        targetting = tgProp.GetValue(abilities, null);
                    }
                    if (targetting == null)
                    {
                        FieldInfo tgField = abilities.GetType().GetField(
                            "SpiritFlameTargetting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (tgField != null)
                        {
                            targetting = tgField.GetValue(abilities);
                        }
                    }
                }
                catch { targetting = null; }
                if (targetting == null)
                {
                    return fallback;
                }
                object listObj = null;
                try
                {
                    PropertyInfo liProp = targetting.GetType().GetProperty(
                        "ClosestAttackables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (liProp != null)
                    {
                        listObj = liProp.GetValue(targetting, null);
                    }
                    if (listObj == null)
                    {
                        FieldInfo liField = targetting.GetType().GetField(
                            "ClosestAttackables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (liField != null)
                        {
                            listObj = liField.GetValue(targetting);
                        }
                    }
                }
                catch { listObj = null; }
                System.Collections.IList list = listObj as System.Collections.IList;
                if (list == null || list.Count == 0)
                {
                    return fallback;
                }
                object first = null;
                try { first = list[0]; }
                catch { first = null; }
                if (first == null)
                {
                    return fallback;
                }
                try
                {
                    PropertyInfo posProp = first.GetType().GetProperty(
                        "Position", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (posProp != null)
                    {
                        object posObj = posProp.GetValue(first, null);
                        if (posObj is Vector3)
                        {
                            return (Vector3)posObj;
                        }
                    }
                }
                catch { }
                try
                {
                    Component c = first as Component;
                    if (c != null)
                    {
                        return c.transform.position;
                    }
                }
                catch { }
            }
            catch { }
            return fallback;
        }

        // Reserva caso a injecao ___m_sein venha nula (ex.: rename futuro do
        // campo): procura o primeiro campo SeinCharacter na hierarquia.
        private static SeinCharacter ResolveOwner(SeinSpiritFlameAbility instance)
        {
            try
            {
                if (instance == null)
                {
                    return null;
                }
                Type type = instance.GetType();
                while (type != null && type != typeof(object))
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (fields[i] != null && fields[i].FieldType == typeof(SeinCharacter))
                        {
                            object value = fields[i].GetValue(instance);
                            SeinCharacter sein = value as SeinCharacter;
                            if (sein != null)
                            {
                                return sein;
                            }
                        }
                    }
                    type = type.BaseType;
                }
            }
            catch
            {
            }
            return null;
        }
    }

    // Detector da rajada carregada (R3, fecha G-03-6): caminho separado do
    // tiro normal — SeinChargeFlameAbility.ReleaseChargeBurst() instancia um
    // dos ChargeFlameBurstA/B/C em Ori.get_Position (radial, sem mira).
    // Publica kind 2 com aim=origin (visual remoto radial, nao beam).
    [HarmonyPatch(typeof(SeinChargeFlameAbility), "ReleaseChargeBurst")]
    internal static class ChargeFlamePatch
    {
        private static void Postfix(SeinChargeFlameAbility __instance)
        {
            if (OriCoopPlugin.Instance == null || __instance == null)
            {
                return;
            }
            SeinCharacter owner = ResolveChargeOwner(__instance);
            if (owner == null || owner != Game.Characters.Sein)
            {
                return;
            }
            Vector3 origin;
            try
            {
                Ori orb = Game.Characters.Ori;
                origin = (orb != null && orb.gameObject != null)
                    ? orb.transform.position
                    : owner.transform.position;
            }
            catch
            {
                return;
            }
            // Prefab da rajada + provedor de som: varredura reflexiva dos
            // campos (nomes observados: ChargeFlameBurstA/B/C, SoundProvider).
            try { NoteChargeAssets(__instance); }
            catch { }
            Vector3Data originData = new Vector3Data(origin.x, origin.y, origin.z);
            OriCoopPlugin.LogInfo(string.Format("[EVENT] ChargedFlame detected origin=({0:F1},{1:F1},{2:F1})",
                originData.X, originData.Y, originData.Z));
            PlayerEventCore.PublishChargedFlame(originData, DateTime.UtcNow.Ticks);
        }

        private static SeinCharacter ResolveChargeOwner(SeinChargeFlameAbility instance)
        {
            try
            {
                if (instance == null)
                {
                    return null;
                }
                Type type = instance.GetType();
                while (type != null && type != typeof(object))
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (fields[i] != null && fields[i].FieldType == typeof(SeinCharacter))
                        {
                            object value = null;
                            try { value = fields[i].GetValue(instance); }
                            catch { value = null; }
                            SeinCharacter sein = value as SeinCharacter;
                            if (sein != null)
                            {
                                return sein;
                            }
                        }
                    }
                    type = type.BaseType;
                }
            }
            catch
            {
            }
            return null;
        }

        private static void NoteChargeAssets(SeinChargeFlameAbility instance)
        {
            try
            {
                Type type = instance.GetType();
                while (type != null && type != typeof(object))
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        FieldInfo f = fields[i];
                        if (f == null)
                        {
                            continue;
                        }
                        object value = null;
                        try { value = f.GetValue(instance); }
                        catch { value = null; }
                        if (value == null)
                        {
                            continue;
                        }
                        try
                        {
                            string tn = f.FieldType != null ? f.FieldType.Name : string.Empty;
                            string fn = f.Name != null ? f.Name.ToLower() : string.Empty;
                            // Prefab da rajada: campo GameObject com "burst".
                            if (value is GameObject && fn.Contains("burst"))
                            {
                                SeinVisualMirror.NoteChargePrefabs((GameObject)value);
                                continue;
                            }
                            // Definitions: ChargeFlameBurstA/B/C sao fields.
                            if (tn == "ChargeFlameDefinitions")
                            {
                                try
                                {
                                    FieldInfo[] df = f.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    for (int j = 0; j < df.Length; j++)
                                    {
                                        if (df[j] == null || df[j].FieldType != typeof(GameObject))
                                        {
                                            continue;
                                        }
                                        object dv = null;
                                        try { dv = df[j].GetValue(value); }
                                        catch { dv = null; }
                                        GameObject burst = dv as GameObject;
                                        if (burst != null)
                                        {
                                            SeinVisualMirror.NoteChargePrefabs(burst);
                                            break;
                                        }
                                    }
                                }
                                catch { }
                                continue;
                            }
                            // Som: primeiro SoundProvider achado.
                            if (tn == "SoundProvider")
                            {
                                try { SeinVisualMirror.NoteChargeSound(value); }
                                catch { }
                            }
                        }
                        catch { }
                    }
                    type = type.BaseType;
                }
            }
            catch { }
        }
    }
}
