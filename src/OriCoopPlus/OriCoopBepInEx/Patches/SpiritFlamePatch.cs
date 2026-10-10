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
            Vector3Data direction = new Vector3Data(dx, 0f, 0f);
            Vector3Data originData = new Vector3Data(origin.x, origin.y, origin.z);
            long timestamp = DateTime.UtcNow.Ticks;
            // Linha temporaria de deteccao (piloto): confirma no LogOutput.log
            // que o patch disparou no tiro local. Virar verbosa apos validacao.
            OriCoopPlugin.LogInfo(string.Format("[EVENT] SpiritFlame detected dir=({0:F1},{1:F1},{2:F1}) origin=({3:F1},{4:F1},{5:F1})",
                direction.X, direction.Y, direction.Z, originData.X, originData.Y, originData.Z));
            PlayerEventCore.PublishSpiritFlame(direction, originData, timestamp);
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
}
