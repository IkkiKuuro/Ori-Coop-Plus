using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game;
using OriCoop;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public static class AnimationRegistry
    {
        private static readonly Dictionary<uint, TextureAnimationWithTransitions> s_animHashCache =
            new Dictionary<uint, TextureAnimationWithTransitions>();
        private static readonly Dictionary<string, TextureAnimationWithTransitions> s_animNameCache =
            new Dictionary<string, TextureAnimationWithTransitions>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ActionVisualState, TextureAnimationWithTransitions> s_stateClips =
            new Dictionary<ActionVisualState, TextureAnimationWithTransitions>();
        // Clipes comprovadamente vindos da hierarquia do Sein (via reflection
        // nos campos dos MonoBehaviours). Resolve exato (hash/nome) confia em
        // qualquer cache porque assets são compartilhados; o fallback por
        // estado SÓ usa este conjunto — nunca clipe de inimigo.
        private static readonly Dictionary<TextureAnimationWithTransitions, bool> s_seinClipRefs =
            new Dictionary<TextureAnimationWithTransitions, bool>();

        // SEED: aliases best-effort — calibrar nomes exatos via dump F8
        // (plano 02). Miss aqui nunca vira sprite aleatório: resolve
        // desconhecido retorna null e o puppet mantém a última anim.
        private static readonly Dictionary<string, ActionVisualState> s_nameToState =
            new Dictionary<string, ActionVisualState>(StringComparer.OrdinalIgnoreCase)
            {
                { "idle", ActionVisualState.Idle },
                { "oriidle", ActionVisualState.Idle },
                { "seinidle", ActionVisualState.Idle },
                { "stand", ActionVisualState.Idle },
                { "oristand", ActionVisualState.Idle },
                { "run", ActionVisualState.Running },
                { "running", ActionVisualState.Running },
                { "orirun", ActionVisualState.Running },
                { "seinrun", ActionVisualState.Running },
                { "jump", ActionVisualState.Jump },
                { "orijump", ActionVisualState.Jump },
                { "seinjump", ActionVisualState.Jump },
                { "jumpup", ActionVisualState.Jump },
                { "rise", ActionVisualState.Jump },
                { "doublejump", ActionVisualState.DoubleJump },
                { "oridoublejump", ActionVisualState.DoubleJump },
                { "flip", ActionVisualState.DoubleJump },
                { "fall", ActionVisualState.Falling },
                { "falling", ActionVisualState.Falling },
                { "orifall", ActionVisualState.Falling },
                { "seinfall", ActionVisualState.Falling },
                { "drop", ActionVisualState.Falling },
                { "wallslide", ActionVisualState.WallSlide },
                { "oriwallslide", ActionVisualState.WallSlide },
                { "slide", ActionVisualState.WallSlide },
                { "walljump", ActionVisualState.WallJump },
                { "oriwalljump", ActionVisualState.WallJump },
                { "bash", ActionVisualState.Bash },
                { "oribash", ActionVisualState.Bash },
                { "seinbash", ActionVisualState.Bash },
                { "glide", ActionVisualState.Glide },
                { "origlide", ActionVisualState.Glide },
                { "seinglide", ActionVisualState.Glide },
                { "feather", ActionVisualState.Glide },
                { "slowfall", ActionVisualState.Glide },
                { "chargejump", ActionVisualState.ChargeJump },
                { "orichargejump", ActionVisualState.ChargeJump },
                { "charge", ActionVisualState.ChargeJump },
                { "stomp", ActionVisualState.Stomp },
                { "oristomp", ActionVisualState.Stomp },
                { "seinstomp", ActionVisualState.Stomp },
                { "groundpound", ActionVisualState.Stomp },
                { "dash", ActionVisualState.Dash },
                { "oridash", ActionVisualState.Dash },
                { "seindash", ActionVisualState.Dash },
                { "airdash", ActionVisualState.Dash },
                { "chargedash", ActionVisualState.Dash },
                { "swim", ActionVisualState.Swim },
                { "oriswim", ActionVisualState.Swim },
                { "seinswim", ActionVisualState.Swim },
                { "swimsurface", ActionVisualState.Swim },
                { "swimidle", ActionVisualState.Swim },
                { "jumpoutofwater", ActionVisualState.Swim },
                { "carry", ActionVisualState.Carry },
                { "oricarry", ActionVisualState.Carry },
                { "seincarry", ActionVisualState.Carry },
                { "pickup", ActionVisualState.Carry },
                { "grabwall", ActionVisualState.GrabWall },
                { "origrabwall", ActionVisualState.GrabWall },
                { "climb", ActionVisualState.GrabWall },
                { "climbup", ActionVisualState.GrabWall },
                { "climbdown", ActionVisualState.GrabWall },
                { "edgeclimb", ActionVisualState.GrabWall },
                { "edgeclamber", ActionVisualState.GrabWall },
                { "grabblock", ActionVisualState.GrabBlock },
                { "push", ActionVisualState.GrabBlock },
                { "pull", ActionVisualState.GrabBlock },
                { "pushagainstwall", ActionVisualState.PushAgainstWall },
                { "hurt", ActionVisualState.Hurt },
                { "orihurt", ActionVisualState.Hurt },
                { "seinhurt", ActionVisualState.Hurt },
                { "crouch", ActionVisualState.Crouch },
                { "oricrouch", ActionVisualState.Crouch },
                { "seicrouch", ActionVisualState.Crouch },
                { "lookup", ActionVisualState.LookUp },
                { "orilookup", ActionVisualState.LookUp },
                { "aim", ActionVisualState.AimThrow },
                { "oriaim", ActionVisualState.AimThrow },
                { "throw", ActionVisualState.AimThrow },
                { "orithrow", ActionVisualState.AimThrow },
                { "lever", ActionVisualState.Lever },
                { "standingonedge", ActionVisualState.StandingOnEdge },
                { "facingedge", ActionVisualState.StandingOnEdge },
            };

        public static bool IsPrewarmed { get; private set; }

        // Re-coleta idempotente do Sein vivo: garante que o puppet remoto
        // tenha acesso a TODAS as animacoes mesmo se o Prewarm correu cedo
        // demais (habilidades ainda nao inicializadas). So adiciona, nunca
        // remove — seguro chamar a qualquer momento.
        public static int RefreshFromSein()
        {
            int added = 0;
            try
            {
                GameObject seinGo = FindSeinObject();
                if (seinGo != null)
                {
                    List<TextureAnimationWithTransitions> seinClips = CollectClips(seinGo);
                    if (seinClips != null && seinClips.Count > 0)
                    {
                        int before = s_animNameCache.Count;
                        RegisterSeinClips(seinClips);
                        added = s_animNameCache.Count - before;
                        IsPrewarmed = true;
                    }
                }
            }
            catch { }
            return added;
        }

        public static void Prewarm()
        {
            if (IsPrewarmed)
            {
                return;
            }

            // Caminho autoritativo: coleta via reflection nos campos do Sein.
            // (TextureAnimationWithTransitions é ScriptableObject — não adianta
            // GetComponentsInChildren, e o scan global antigo catalogava
            // inimigos e o primeiro "idle" de inimigo virava o Idle do Ori.)
            try
            {
                GameObject seinGo = FindSeinObject();
                if (seinGo != null)
                {
                    List<TextureAnimationWithTransitions> seinClips = CollectClips(seinGo);
                    if (seinClips != null && seinClips.Count > 0)
                    {
                        RegisterSeinClips(seinClips);
                        IsPrewarmed = true;
                    }
                }
            }
            catch { }

            // Merge global SOMENTE para resolve exato (populateStateClips=false):
            // hash/nome apontam para o MESMO asset compartilhado, entao e
            // seguro mesmo vindo de inimigos. Garante que nenhum clipe do
            // Sein fique de fora por ter sido perdido no reflection.
            MergeGlobalForExact();
        }

        private static void MergeGlobalForExact()
        {
            try
            {
                TextureAnimationWithTransitions[] allClips = Resources.FindObjectsOfTypeAll<TextureAnimationWithTransitions>();
                if (allClips != null && allClips.Length > 0)
                {
                    RegisterClips(allClips, false);
                    IsPrewarmed = true;
                    Debug.Log(string.Format("[OriCoop] AnimationRegistry: {0} clipes (Sein + global-exato).", s_animNameCache.Count));
                }
            }
            catch { }
        }

        private static GameObject FindSeinObject()
        {
            try
            {
                if (Game.Characters.Sein != null && Game.Characters.Sein.gameObject != null)
                {
                    return Game.Characters.Sein.gameObject;
                }
            }
            catch { }
            GameObject go = GameObject.Find("Characters/Sein");
            if (go == null)
            {
                go = GameObject.Find("Sein");
            }
            return go;
        }

        // Coleta todos os clipes referenciados pelos MonoBehaviours da
        // hierarquia (SeinIdle.IdleAnimation, SeinRun.RunAnimation, arrays de
        // SeinJump/SeinDoubleJump/SeinWallJump, DirectionalAnimationSets do
        // Bash, containers CarryAnimations/SwimmingAnimations...).
        // Inclui os drivers de animacao (CharacterAnimationSystem.m_states,
        // SpriteAnimatorWithTransitions.Default/Current/Previous) para que o
        // puppet tenha acesso a TODOS os clipes, nao so aos campos logicos.
        public static List<TextureAnimationWithTransitions> CollectClips(GameObject root)
        {
            List<TextureAnimationWithTransitions> result = new List<TextureAnimationWithTransitions>();
            if (root == null)
            {
                return result;
            }
            try
            {
                MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour mb = behaviours[i];
                    if (mb == null)
                    {
                        continue;
                    }
                    List<object> visited = new List<object>();
                    CollectFromObject(mb, result, visited, 0);
                }
                CollectAnimationSystemClips(root, result);
            }
            catch { }
            return result;
        }

        private static void CollectAnimationSystemClips(GameObject root, List<TextureAnimationWithTransitions> result)
        {
            try
            {
                SpriteAnimatorWithTransitions[] animators = root.GetComponentsInChildren<SpriteAnimatorWithTransitions>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    SpriteAnimatorWithTransitions animator = animators[i];
                    if (animator == null)
                    {
                        continue;
                    }
                    AddClip(result, animator.DefaultAnimation);
                    AddClip(result, animator.CurrentTextureAnimationTransitions);
                    AddClip(result, animator.PreviousTextureAnimationTransitions);
                }
            }
            catch { }
        }

        private static void AddClip(List<TextureAnimationWithTransitions> result, TextureAnimationWithTransitions clip)
        {
            if (clip != null && !result.Contains(clip))
            {
                result.Add(clip);
            }
        }

        private static void CollectFromObject(object obj, List<TextureAnimationWithTransitions> result, List<object> visited, int depth)
        {
            if (obj == null || depth > 5)
            {
                return;
            }
            for (int i = 0; i < visited.Count; i++)
            {
                if (visited[i] == obj)
                {
                    return;
                }
            }
            visited.Add(obj);

            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null)
                {
                    continue;
                }
                Type fieldType = field.FieldType;
                object value;
                try
                {
                    value = field.GetValue(obj);
                }
                catch
                {
                    continue;
                }
                if (value == null)
                {
                    continue;
                }
                TextureAnimationWithTransitions clip = value as TextureAnimationWithTransitions;
                if (clip != null)
                {
                    if (!result.Contains(clip))
                    {
                        result.Add(clip);
                    }
                    continue;
                }
                // List<T> (ex.: SeinGrenadeAttack.FastThrowAnimations,
                // CharacterAnimationSystem.m_states): o scan antigo so lia
                // arrays e perdia esses clipes — o puppet ficava sem acesso.
                System.Collections.IList list = value as System.Collections.IList;
                if (list != null && !(value is UnityEngine.Object) && !(value is string))
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        object item;
                        try
                        {
                            item = list[j];
                        }
                        catch
                        {
                            continue;
                        }
                        TextureAnimationWithTransitions itemClip = item as TextureAnimationWithTransitions;
                        if (itemClip != null)
                        {
                            if (!result.Contains(itemClip))
                            {
                                result.Add(itemClip);
                            }
                        }
                        else if (item != null && depth < 5 && !(item is UnityEngine.Object) && !(item is string))
                        {
                            CollectFromObject(item, result, visited, depth + 1);
                        }
                    }
                    continue;
                }
                if (fieldType.IsArray)
                {
                    Array arr = value as Array;
                    if (arr != null)
                    {
                        for (int j = 0; j < arr.Length; j++)
                        {
                            object item;
                            try
                            {
                                item = arr.GetValue(j);
                            }
                            catch
                            {
                                continue;
                            }
                            TextureAnimationWithTransitions itemClip = item as TextureAnimationWithTransitions;
                            if (itemClip != null)
                            {
                                if (!result.Contains(itemClip))
                                {
                                    result.Add(itemClip);
                                }
                            }
                            else if (item != null && depth < 5 && !(item is UnityEngine.Object) && !(item is string))
                            {
                                CollectFromObject(item, result, visited, depth + 1);
                            }
                        }
                    }
                    continue;
                }
                if (value is UnityEngine.Object || value is string)
                {
                    continue;
                }
                if (fieldType.IsPrimitive || fieldType.IsEnum)
                {
                    continue;
                }
                if (fieldType == typeof(Type) || fieldType == typeof(FieldInfo))
                {
                    continue;
                }
                CollectFromObject(value, result, visited, depth + 1);
            }
        }

        public static bool IsSeinClip(TextureAnimationWithTransitions clip)
        {
            if (clip == null)
            {
                return false;
            }
            return s_seinClipRefs.ContainsKey(clip);
        }

        public static void RegisterSeinClips(IEnumerable<TextureAnimationWithTransitions> clips)
        {
            RegisterClips(clips, true);
        }

        public static void RegisterClips(IEnumerable<TextureAnimationWithTransitions> clips)
        {
            RegisterClips(clips, false);
        }

        public static void RegisterClips(IEnumerable<TextureAnimationWithTransitions> clips, bool populateStateClips)
        {
            if (clips == null)
            {
                return;
            }

            foreach (TextureAnimationWithTransitions clip in clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.name))
                {
                    continue;
                }

                IndexClip(clip.name, clip);
                // O sender (PlayerStateReader) pode enviar o nome da animacao
                // INTERNA (TextureAnimation.name) enquanto aqui a chave e o
                // nome do wrapper (ScriptableObject.name) — ou vice-versa.
                // Indexa os dois apontando para o mesmo wrapper para o
                // resolve exato por hash/nome nunca errar.
                try
                {
                    TextureAnimation inner = clip.Animation;
                    if (inner != null && !string.IsNullOrEmpty(inner.name) && inner.name != clip.name)
                    {
                        IndexClip(inner.name, clip);
                    }
                }
                catch { }

                if (!populateStateClips)
                {
                    continue;
                }

                if (!s_seinClipRefs.ContainsKey(clip))
                {
                    s_seinClipRefs[clip] = true;
                }

                ActionVisualState mappedState;
                if (s_nameToState.TryGetValue(clip.name, out mappedState)
                    && !s_stateClips.ContainsKey(mappedState))
                {
                    s_stateClips[mappedState] = clip;
                }
                else
                {
                    // Tenta tambem pelo nome interno (ex.: wrapper generico
                    // com inner "swimIdle"): fallback por estado do puppet.
                    try
                    {
                        TextureAnimation innerForState = clip.Animation;
                        if (innerForState != null && !string.IsNullOrEmpty(innerForState.name)
                            && s_nameToState.TryGetValue(innerForState.name, out mappedState)
                            && !s_stateClips.ContainsKey(mappedState))
                        {
                            s_stateClips[mappedState] = clip;
                        }
                    }
                    catch { }
                }

                // Substring fallback estilo PlayerStateReader (G-03-1/G-03-2):
                // o nome real do clipe de ataque do jogo nao esta na tabela
                // SEED exata; Contains aim/throw resolve para AimThrow.
                // So clipes do Sein (proveniencia acima), first-wins (nunca
                // sobrescreve entrada calibrada via F8). Aplica a ambos os
                // niveis outer/inner como a estrutura exata acima.
                // R3: exclui granada — "grenadeThrowDown" contem "throw" mas
                // e de outra habilidade; sem o filtro o corpo toca anim
                // errada (D-13 exige o mesmo clipe). Sem match honesto, o
                // corpo segura a pose (fail-closed D-15); o ataque visivel
                // e o ShootAnimation do orbe.
                if (!s_stateClips.ContainsKey(ActionVisualState.AimThrow))
                {
                    try
                    {
                        string outerLower = clip.name != null ? clip.name.ToLower() : string.Empty;
                        bool outerMatch = (outerLower.Contains("aim") || outerLower.Contains("throw"))
                            && !outerLower.Contains("grenade");
                        string innerLower = string.Empty;
                        try
                        {
                            TextureAnimation innerForSub = clip.Animation;
                            if (innerForSub != null && !string.IsNullOrEmpty(innerForSub.name))
                            {
                                innerLower = innerForSub.name.ToLower();
                            }
                        }
                        catch { }
                        bool innerMatch = (innerLower.Contains("aim") || innerLower.Contains("throw"))
                            && !innerLower.Contains("grenade");
                        if ((outerMatch || innerMatch) && !s_stateClips.ContainsKey(ActionVisualState.AimThrow))
                        {
                            s_stateClips[ActionVisualState.AimThrow] = clip;
                        }
                    }
                    catch { }
                }
            }
        }

        private static void IndexClip(string name, TextureAnimationWithTransitions clip)
        {
            uint hash = AnimationSyncData.ComputeFnv1aHash(name);
            if (!s_animHashCache.ContainsKey(hash))
            {
                s_animHashCache[hash] = clip;
            }
            if (!s_animNameCache.ContainsKey(name))
            {
                s_animNameCache[name] = clip;
            }
        }

        public static void DumpCatalog()
        {
            foreach (KeyValuePair<string, TextureAnimationWithTransitions> entry in s_animNameCache)
            {
                ActionVisualState mapped;
                string stateText = s_nameToState.TryGetValue(entry.Key, out mapped)
                    ? mapped.ToString() : "UNKNOWN";
                string origin = (entry.Value != null && s_seinClipRefs.ContainsKey(entry.Value)) ? "sein" : "global";
                Debug.Log(string.Format("[ANIM-DUMP] name={0} hash=0x{1:X8} estado?={2} src={3}",
                    entry.Key, AnimationSyncData.ComputeFnv1aHash(entry.Key), stateText, origin));
            }
            Debug.Log(string.Format("[OriCoop] ANIM-DUMP concluído: {0} clipes.", s_animNameCache.Count));
        }

        // Resolve exato: hash/nome do sender referenciam o MESMO asset
        // compartilhado — seguro mesmo vindo do cache global.
        public static bool TryResolveExact(string animName, uint animHash, out TextureAnimationWithTransitions clip)
        {
            if (animHash != 0 && s_animHashCache.TryGetValue(animHash, out clip) && clip != null)
            {
                return true;
            }
            if (!string.IsNullOrEmpty(animName) && s_animNameCache.TryGetValue(animName, out clip) && clip != null)
            {
                return true;
            }
            clip = null;
            return false;
        }

        // Fallback por estado: SÓ clipes comprovadamente do Sein.
        public static bool TryResolveState(ActionVisualState state, out TextureAnimationWithTransitions clip)
        {
            if (s_stateClips.TryGetValue(state, out clip) && clip != null)
            {
                return true;
            }
            clip = null;
            return false;
        }

        public static TextureAnimationWithTransitions Resolve(string animName, uint animHash, ActionVisualState fallbackState)
        {
            TextureAnimationWithTransitions result;
            if (TryResolveExact(animName, animHash, out result))
            {
                return result;
            }
            if (TryResolveState(fallbackState, out result))
            {
                return result;
            }

            // Desconhecido: retorna null e o chamador mantém a última anim
            // (fail-closed). Sem chute para Idle genérico.
            return null;
        }

        public static ActionVisualState InferStateFromMovement(Vector3 velocity, bool isGrounded)
        {
            if (!isGrounded)
            {
                if (velocity.y < -1.0f)
                {
                    return ActionVisualState.Falling;
                }
                if (velocity.y > 1.0f)
                {
                    return ActionVisualState.Jump;
                }
            }

            float horizontalSpeed = Mathf.Abs(velocity.x);
            if (horizontalSpeed > 0.4f)
            {
                return ActionVisualState.Running;
            }

            return ActionVisualState.Idle;
        }
    }
}
