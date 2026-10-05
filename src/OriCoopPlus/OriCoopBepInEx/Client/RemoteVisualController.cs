using System;
using System.Collections.Generic;
using OriCoopBepInEx.Diagnostics;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public sealed class RemoteVisualController : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _materials = new List<Material>();
        private bool _isInitialized;
        private int _playerId;

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private float _lastWatchdogTime;

        public void InitializeHierarchy(int playerId)
        {
            _playerId = playerId;
            StripFrustumOptimizers(gameObject);
            StripExtraLights(gameObject);

            _renderers.Clear();
            _materials.Clear();

            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null || r is ParticleSystemRenderer ||
                    r.name.Contains("NameTag") || r.name.Contains("Shadow") ||
                    r.name.Contains("bentBar") || r.name.Contains("radialEnemyHighlight") ||
                    r.name.Contains("ghostTrail") || r.name.Contains("mistErase") ||
                    r.name.Contains("CoopNameTag"))
                {
                    continue;
                }

                // BUG #1 (brilho extremo): o puppet e clonado do Sein e antes
                // compartilhava o MESMO Material (sharedMaterial) com o jogador local.
                // Qualquer ajuste de alpha/cor no puppet vazava para o Ori local e,
                // somado a Light duplicada + dois sprites aditivos sobrepostos,
                // estourava o bloom. Aqui isolamos: cada renderer do puppet ganha
                // sua propria instancia de Material.
                try
                {
                    if (r.sharedMaterial != null)
                    {
                        r.material = new Material(r.sharedMaterial);
                    }
                }
                catch { }

                _renderers.Add(r);
                if (r.material != null)
                {
                    _materials.Add(r.material);
                }
            }

            _isInitialized = true;
            EnforceVisibility("InitializeHierarchy");
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            // Throttle watchdog check to 2x per second to prevent per-frame CPU overhead
            if (Time.time - _lastWatchdogTime < 0.5f)
            {
                return;
            }
            _lastWatchdogTime = Time.time;

            EnforceVisibility("LateUpdate-Watchdog");
        }

        public void EnforceVisibility(string callerContext)
        {
            bool isInit = string.Equals(callerContext, "InitializeHierarchy", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!r.enabled)
                {
                    r.enabled = true;
                    if (isInit)
                    {
                        ReplicationObservability.LogVisibilityEvent(_playerId, string.Format("Renderer '{0}' reenabled by [{1}]", r.name, callerContext), true);
                    }
                }

                if (!r.gameObject.activeSelf)
                {
                    r.gameObject.SetActive(true);
                    if (isInit)
                    {
                        ReplicationObservability.LogVisibilityEvent(_playerId, string.Format("GameObject '{0}' reenabled by [{1}]", r.gameObject.name, callerContext), true);
                    }
                }
            }

            // NOTA bug #1: NAO tocar mais em sharedMaterial e NAO forcar alpha=1.
            // O Ori usa fade/transparencia legitimo (ex.: dash, bash, cutscenes) e o
            // watchdog antigo corrompia o material compartilhado com o jogador local,
            // deixando o personagem estourado/branco. Apenas garante que a instancia
            // propria do puppet nao fique totalmente invisivel.
            for (int i = 0; i < _materials.Count; i++)
            {
                Material mat = _materials[i];
                if (mat != null && mat.HasProperty(ColorPropId))
                {
                    try
                    {
                        Color c = mat.color;
                        if (c.a < 0.01f)
                        {
                            c.a = 1.0f;
                            mat.color = c;
                        }
                    }
                    catch { }
                }
            }
        }

        public static void StripExtraLights(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            // BUG #1: cada clone do Sein trazia sua Point Light / halo / flare.
            // Duas lights reais sobrepostas + bloom = personagem branco estourado.
            // Remove explicitamente, sem depender do passo generico.
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    DestroyImmediate(lights[i]);
                }
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    continue;
                }

                string tn = c.GetType().Name;
                if (tn == "Halo" || tn == "LensFlare" || tn == "FlareLayer" ||
                    tn == "Projector" || tn == "TrailRenderer")
                {
                    try { DestroyImmediate(c); } catch { }
                }
            }
        }

        public static void StripFrustumOptimizers(GameObject root)
        {
            // Componentes que desligam renderers quando fora do frustum da camera.
            // NOTA: CharacterAnimationSystem NAO e culling — e o driver das animacoes
            // do Sein. Destrui-lo congelava o puppet (sem animacoes). Por isso ele foi
            // removido desta lista (correcao bug #2).
            string[] cullingComponents = new string[]
            {
                "CameraFrustumOptimizer",
                "MeshRendererFrustrumOptimiser",
                "DisableRendererWhenOutOfFrustrum",
                "DisableGameObjectWhenOutOfFrustrum",
                "SuspendWhenOutOfFrustrum"
            };

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp == null)
                {
                    continue;
                }

                string typeName = comp.GetType().Name;
                for (int j = 0; j < cullingComponents.Length; j++)
                {
                    if (typeName == cullingComponents[j])
                    {
                        DestroyImmediate(comp);
                        break;
                    }
                }
            }
        }
    }
}
