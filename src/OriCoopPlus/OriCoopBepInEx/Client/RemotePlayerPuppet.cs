using System;
using System.Collections.Generic;
using Game;
using OriCoop;
using OriCoopBepInEx.Diagnostics;
using OriCoopBepInEx.Plugin;
using OriCoopBepInEx.UI;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    // Projetil fake do Spirit Flame remoto (fase 3, plano 02, D-14).
    // Puramente visual desde o nascimento: MeshFilter + MeshRenderer +
    // este mover — sem colisao, sem corpo rigido, sem dano, sem gatilhos.
    // Move-se em linha reta a partir da origem + direcao do evento (mesma
    // idioma de extrapolacao do puppet Update: posicao += v * dt), e se
    // desativa apos alcance ou tempo de vida, voltando ao pool.
    // Ponto de spawn + forma do payload (origem + direcao do evento) sao
    // identicos ao que a fisica real futura vai precisar (D-14).
    public sealed class FakeFlameMover : MonoBehaviour
    {
        private Vector3 _velocity;
        private Vector3 _start;
        private float _age;

        public void Launch(Vector3 direction)
        {
            _start = transform.position;
            Vector3 dir = direction;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = new Vector3(1f, 0f, 0f);
            }
            dir.Normalize();
            _velocity = dir * RemotePlayerPuppet.FakeShotSpeed;
            _age = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position += _velocity * dt;
            _age += dt;
            if (_age >= RemotePlayerPuppet.FakeShotLifetime
                || Vector3.Distance(transform.position, _start) >= RemotePlayerPuppet.FakeShotRange)
            {
                RemotePlayerPuppet.ReturnPooledFake(gameObject);
            }
        }
    }

    // Limpeza de transientes de evento (particula do disparo, audio
    // transitorio): destroi o GameObject apos o tempo de vida. Roda na
    // main thread do Unity (criado pelo caminho de recepcao, ja em main).
    public sealed class TransientEventCleanup : MonoBehaviour
    {
        public float Lifetime = 1f;
        private float _age;

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime)
            {
                try { UnityEngine.Object.Destroy(gameObject); }
                catch { }
            }
        }
    }

    public sealed class RemotePlayerPuppet : MonoBehaviour
    {
        private RemoteVisualController _visualController;
        private SpriteAnimatorWithTransitions _animator;
        private CharacterSpriteMirror _spriteMirror;
        private FloatingNameTag _nameTag;

        private Vector3 _targetPosition;
        private Vector3 _velocity;
        private float _lastSnapshotTime;
        private string _lastAnimName;
        private const float InterpolationSmoothing = 24f;
        private const float ConfirmDelaySec = 0.15f;
        private const float MaxExtrapolationSec = 0.25f;

        private ActionVisualState _confirmedState = ActionVisualState.Idle;
        private ActionVisualState _pendingState = ActionVisualState.Idle;
        private float _pendingSince;

        public int PlayerId { get; private set; }
        public string Nickname { get; private set; }

        // Visual do Spirit Flame remoto (fase 3, plano 02, D-13/D-14):
        // velocidade/alcance/tempo do fake ajustados no piloto (comparaveis
        // ao tiro real proximo; fisicas reais chegam na fase de entidades).
        public const float FakeShotSpeed = 18f;
        public const float FakeShotLifetime = 0.8f;
        public const float FakeShotRange = 14f;
        private const int FakePoolCap = 8;
        private const float MuzzleParticleLifetime = 0.9f;

        private static readonly List<GameObject> s_fakePool = new List<GameObject>();
        private static Mesh s_fakeQuadMesh;
        private static Material s_fakeBoltMaterial;
        private static AudioClip s_cachedShotClip;

        public void Setup(int id, string nickname)
        {
            PlayerId = id;
            Nickname = string.IsNullOrEmpty(nickname) ? ("Player " + id) : nickname;

            _visualController = GetComponent<RemoteVisualController>() ?? gameObject.AddComponent<RemoteVisualController>();
            _visualController.InitializeHierarchy(PlayerId);

            _animator = GetComponentInChildren<SpriteAnimatorWithTransitions>();
            _spriteMirror = GetComponentInChildren<CharacterSpriteMirror>();

            _targetPosition = transform.position;
            _lastSnapshotTime = Time.time;

            if (_nameTag == null)
            {
                _nameTag = FloatingNameTag.Attach(gameObject, Nickname);
                if (id == 999 && _nameTag != null)
                {
                    _nameTag.SetColor(new Color(0.2f, 0.85f, 1f)); // Distinct cyan color for test dummy bot
                }
            }
        }

        public void UpdateNickname(string newNick)
        {
            if (string.IsNullOrEmpty(newNick) || newNick == Nickname)
            {
                return;
            }

            Nickname = newNick;
            gameObject.name = string.Format("RemotePlayer_{0}_{1}", PlayerId, Nickname);
            if (_nameTag != null)
            {
                _nameTag.SetNickname(newNick);
            }
        }

        public void ApplySnapshot(Vector3 position, Vector3 velocity, bool facingLeft, string animName, uint animHash, string nick)
        {
            if (!string.IsNullOrEmpty(nick) && nick != Nickname)
            {
                UpdateNickname(nick);
            }

            _targetPosition = position;
            _velocity = velocity;
            _lastSnapshotTime = Time.time;

            if (_spriteMirror != null)
            {
                _spriteMirror.FaceLeft = facingLeft;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, facingLeft ? 180f : 0f, 0f);
            }

            ApplyAnimation(animName, animHash);
        }

        public void ApplySnapshotDirect(Vector3 position, Vector3 velocity, bool facingLeft, ActionVisualState state, string animName, uint animHash, string nick)
        {
            if (!string.IsNullOrEmpty(nick) && nick != Nickname)
            {
                UpdateNickname(nick);
            }

            _targetPosition = position;
            _velocity = velocity;
            _lastSnapshotTime = Time.time;

            if (_spriteMirror != null)
            {
                _spriteMirror.FaceLeft = facingLeft;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, facingLeft ? 180f : 0f, 0f);
            }

            if (state == _confirmedState)
            {
                _pendingState = _confirmedState;
                ApplyConfirmedAnimation(animName, animHash, _confirmedState);
                LogAnimTransition(state, "manteve");
                return;
            }

            if (state != _pendingState)
            {
                _pendingState = state;
                _pendingSince = Time.time;
                LogAnimTransition(state, "histerese-aguarda");
                return;
            }

            if (Time.time - _pendingSince >= ConfirmDelaySec)
            {
                _confirmedState = state;
                ApplyConfirmedAnimation(animName, animHash, _confirmedState);
                LogAnimTransition(state, "trocou");
            }
        }

        private void ApplyConfirmedAnimation(string animName, uint animHash, ActionVisualState confirmedState)
        {
            if (_animator == null)
            {
                return;
            }

            if (!AnimationRegistry.IsPrewarmed)
            {
                try { AnimationRegistry.Prewarm(); } catch { }
            }

            TextureAnimationWithTransitions targetClip;
            // Exato primeiro: hash/nome do sender apontam para o MESMO asset
            // compartilhado — sempre correto. Fallback por estado só usa
            // clipes comprovadamente do Sein (nunca inimigo).
            if (!AnimationRegistry.TryResolveExact(animName, animHash, out targetClip))
            {
                AnimationRegistry.TryResolveState(confirmedState, out targetClip);
            }

            if (targetClip == null)
            {
                // O proprio puppet e visual despojado (sem os MonoBehaviours
                // do Sein), entao re-coletar dele nao adianta: atualiza do
                // Sein vivo.
                try
                {
                    AnimationRegistry.RefreshFromSein();
                    if (!AnimationRegistry.TryResolveExact(animName, animHash, out targetClip))
                    {
                        AnimationRegistry.TryResolveState(confirmedState, out targetClip);
                    }
                }
                catch { }
            }

            // Clipe desconhecido: mantém a anim atual (fail-closed, sem
            // fallback para Idle genérico que virava sprite aleatório).
            if (targetClip == null)
            {
                if (OriCoopPlugin.IsAnimVerbose())
                {
                    OriCoopPlugin.LogInfo(string.Format("[ANIM] P{0} recv={1} aplicado=manteve-atual motivo=desconhecido",
                        PlayerId, confirmedState));
                }
                return;
            }

            if (!IsPlayingClip(_animator, targetClip))
            {
                _animator.SetAnimation(targetClip, true);
                if (OriCoopPlugin.IsAnimVerbose())
                {
                    OriCoopPlugin.LogInfo(string.Format("[ANIM] P{0} recv={1} aplicado={2} motivo=trocou",
                        PlayerId, confirmedState, targetClip.name));
                }
            }

            if (_lastAnimName != animName)
            {
                _lastAnimName = animName;
                ReplicationObservability.TrackPacket(PlayerId, animHash, animName ?? confirmedState.ToString(), true);
            }
        }

        private void ApplyAnimation(string animName, uint animHash)
        {
            if (_animator == null)
            {
                return;
            }

            // Se o registro ainda esta vazio (Prewarm correu cedo demais), tenta de novo
            // de forma preguicosa antes de desistir — sem isso o puppet ficava invisivel/T-pose.
            if (!AnimationRegistry.IsPrewarmed)
            {
                try { AnimationRegistry.Prewarm(); } catch { }
            }

            // Velocidade agora e inferida no RemotePlayerManager (o servidor nao envia).
            // isGrounded e heuristico: queda/subida forte indica arco aereo.
            bool isGrounded = Mathf.Abs(_velocity.y) < 1.0f;
            ActionVisualState fallbackState = AnimationRegistry.InferStateFromMovement(_velocity, isGrounded);
            TextureAnimationWithTransitions targetClip;
            if (!AnimationRegistry.TryResolveExact(animName, animHash, out targetClip))
            {
                AnimationRegistry.TryResolveState(fallbackState, out targetClip);
            }

            // Ultima tentativa: atualiza do Sein vivo (o proprio puppet e
            // visual despojado, re-coletar dele nao adianta) e resolve de novo.
            if (targetClip == null)
            {
                try
                {
                    AnimationRegistry.RefreshFromSein();
                    if (!AnimationRegistry.TryResolveExact(animName, animHash, out targetClip))
                    {
                        AnimationRegistry.TryResolveState(fallbackState, out targetClip);
                    }
                }
                catch { }
            }

            bool applied = false;
            if (targetClip != null)
            {
                if (!IsPlayingClip(_animator, targetClip))
                {
                    _animator.SetAnimation(targetClip, true);
                }
                applied = true;
            }

            if (_lastAnimName != animName)
            {
                _lastAnimName = animName;
                ReplicationObservability.TrackPacket(PlayerId, animHash, animName ?? fallbackState.ToString(), applied);
            }
        }

        // CurrentAnimation e a TextureAnimation INTERNA enquanto o alvo e o
        // wrapper (WithTransitions): comparar direto daria sempre diferente
        // e re-setaria a anim a cada pacote. Compara pelo wrapper atual.
        private static bool IsPlayingClip(SpriteAnimatorWithTransitions animator, TextureAnimationWithTransitions clip)
        {
            if (animator == null || clip == null)
            {
                return false;
            }
            try
            {
                return animator.CurrentTextureAnimationTransitions == clip;
            }
            catch
            {
                return false;
            }
        }

        private void LogAnimTransition(ActionVisualState receivedState, string reason)
        {
            if (!OriCoopPlugin.IsAnimVerbose())
            {
                return;
            }
            string line = string.Format("[ANIM] P{0} recv={1} confirmado={2} motivo={3}",
                PlayerId, receivedState, _confirmedState, reason);
            ReplicationObservability.Record(line);
            OriCoopPlugin.LogInfo(line);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // Extrapola o alvo pela velocidade real do snapshot (D-06 fase 1):
            // entre dois pacotes o puppet segue andando em vez de esperar
            // parado, o que corta o atraso percebido quase pela metade.
            // Sem snapshot recente, segura no ultimo alvo (sem deriva).
            Vector3 goal = _targetPosition;
            float age = Time.time - _lastSnapshotTime;
            if (age >= 0f && age <= MaxExtrapolationSec)
            {
                goal += _velocity * age;
            }
            // Teleporte/logoff: se o alvo esta muito longe, teleporta em vez de
            // atravessar o mapa voando (que parecia "sumico" do jogador).
            float dist = Vector3.Distance(transform.position, goal);
            if (dist > 15f)
            {
                transform.position = _targetPosition;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, goal, dt * InterpolationSmoothing);
            }
        }

        public void SnapTo(Vector3 position)
        {
            _targetPosition = position;
            transform.position = position;
        }

        // Reproducao visual do Spirit Flame remoto (fase 3, piloto, D-13):
        // o MESMO clipe de ataque (AimThrow, exact-then-state, fail-closed),
        // mais particula do disparo + som transiente + projetil fake em linha
        // reta. Tudo visual: zero dano, zero colisao, zero mutacao de estado
        // do jogo local (D-14). So o kind SpiritFlame chega aqui — o
        // PlayerEventCore filtra desconhecidos fail-closed antes (D-15, D-01,
        // D-04: nenhum outro kind tem codigo visual).
        // Roda na main thread do Unity (via EnqueueMainThread).
        public void PlaySpiritFlameVisual(Vector3 origin, Vector3 direction)
        {
            if (_animator == null)
            {
                return;
            }

            if (!AnimationRegistry.IsPrewarmed)
            {
                try { AnimationRegistry.Prewarm(); } catch { }
            }

            TextureAnimationWithTransitions clip;
            // Efeitos primeiro, sempre (G-03-1/G-03-2, D-13): particula +
            // som + fake independem do resolve do clipe — cada spawn ja e
            // individualmente try/catch-guardado. O clipe abaixo e
            // best-effort e NUNCA suprime os efeitos.
            try { SpawnMuzzleParticle(origin); }
            catch { }
            try { PlayTransientShotSound(origin); }
            catch { }
            try { SpawnFakeProjectile(origin, direction); }
            catch { }

            if (!AnimationRegistry.TryResolveState(ActionVisualState.AimThrow, out clip) || clip == null)
            {
                try
                {
                    AnimationRegistry.RefreshFromSein();
                    AnimationRegistry.TryResolveState(ActionVisualState.AimThrow, out clip);
                }
                catch { }
            }

            if (clip == null)
            {
                // Clip desconhecido: log-and-continue SEMPRE visivel
                // (fora de qualquer gate IsAnimVerbose) + mantem a ultima
                // anim (fail-closed D-15: sem SetAnimation, sem Idle).
                string missLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado=manteve-atual motivo=clip-desconhecido",
                    PlayerId);
                ReplicationObservability.Record(missLine);
                OriCoopPlugin.LogInfo(missLine);
                return;
            }

            if (!IsPlayingClip(_animator, clip))
            {
                _animator.SetAnimation(clip, true);
            }
            string appliedLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado={1} motivo=recebido",
                PlayerId, clip.name);
            ReplicationObservability.Record(appliedLine);
            OriCoopPlugin.LogInfo(appliedLine);
        }

        // Particula do disparo: clone visual-only de um sistema do Sein
        // local, desativado logo apos instanciar e despojado para
        // renderizadores + animator antes da primeira ativacao (mesma
        // disciplina inactive-clone da factory). Nome fora da skip-list do
        // watchdog (RemoteVisualController 31-38) para continuar visivel;
        // de todo modo o watchdog so impõe os renderers rastreados no Setup,
        // nao os filhos dinamicos. Auto-destruido apos o tempo de vida.
        private void SpawnMuzzleParticle(Vector3 origin)
        {
            GameObject seinGo = FindLocalSeinObject();
            if (seinGo == null)
            {
                VerboseEvent("sem-sein");
                return;
            }
            ParticleSystem[] systems = null;
            try { systems = seinGo.GetComponentsInChildren<ParticleSystem>(true); }
            catch { systems = null; }
            if (systems == null || systems.Length == 0)
            {
                VerboseEvent("sem-particula");
                return;
            }
            ParticleSystem chosen = PickFlameParticle(systems);
            if (chosen == null || chosen.gameObject == null)
            {
                VerboseEvent("sem-efeito");
                return;
            }
            GameObject clone = UnityEngine.Object.Instantiate(chosen.gameObject) as GameObject;
            if (clone == null)
            {
                return;
            }
            try { clone.SetActive(false); }
            catch { }
            // Solta do pai original ANTES de posicionar (o clone herda o pai).
            try { clone.transform.parent = null; }
            catch { }
            StripToVisualOnly(clone);
            clone.name = "RemoteFlameMuzzle_P" + PlayerId;
            try
            {
                clone.transform.position = origin;
                clone.SetActive(true);
                ParticleSystem fx = clone.GetComponentInChildren<ParticleSystem>();
                if (fx != null)
                {
                    try { fx.Play(); }
                    catch { }
                }
                TransientEventCleanup cleanup = clone.AddComponent<TransientEventCleanup>();
                cleanup.Lifetime = MuzzleParticleLifetime;
            }
            catch
            {
                try { UnityEngine.Object.Destroy(clone); }
                catch { }
            }
        }

        // Som do disparo: AudioSource transiente em GameObject de cena,
        // NUNCA sob o puppet (a factory destroi todo AudioSource do puppet
        // por desenho). Reusa o clipe do jogo (ShootEffect/ShootingSound do
        // Sein local, resolvido em Wave 0) quando encontrado; sem clipe,
        // silencio fail-closed.
        private void PlayTransientShotSound(Vector3 origin)
        {
            AudioClip clip = ResolveShotClip();
            if (clip == null)
            {
                VerboseEvent("sem-clip");
                return;
            }
            GameObject go = new GameObject("RemoteFlameShotAudio_P" + PlayerId);
            go.transform.position = origin;
            AudioSource src = go.AddComponent<AudioSource>();
            if (src == null)
            {
                try { UnityEngine.Object.Destroy(go); }
                catch { }
                return;
            }
            try
            {
                src.PlayOneShot(clip);
                TransientEventCleanup cleanup = go.AddComponent<TransientEventCleanup>();
                cleanup.Lifetime = clip.length + 0.2f;
            }
            catch
            {
                try { UnityEngine.Object.Destroy(go); }
                catch { }
            }
        }

        // Projetil fake (D-14): GameObject puramente visual do pool
        // (malha + renderizador apenas), lançado da origem + direcao do
        // evento — ponto de spawn e forma do payload identicos ao que a
        // fisica real futura vai precisar. Sem eco: nunca publica (D-07).
        private void SpawnFakeProjectile(Vector3 origin, Vector3 direction)
        {
            Vector3 dir = direction;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = new Vector3(1f, 0f, 0f);
            }
            dir.Normalize();
            GameObject shot = TakePooledFake();
            if (shot == null)
            {
                VerboseEvent("pool-cheio");
                return;
            }
            try
            {
                shot.transform.position = origin;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                shot.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                shot.SetActive(true);
                FakeFlameMover mover = shot.GetComponent<FakeFlameMover>();
                if (mover == null)
                {
                    mover = shot.AddComponent<FakeFlameMover>();
                }
                mover.Launch(dir);
            }
            catch
            {
                ReturnPooledFake(shot);
            }
        }

        private GameObject TakePooledFake()
        {
            Renderer donor = null;
            try { donor = GetComponentInChildren<Renderer>(); }
            catch { donor = null; }
            for (int i = s_fakePool.Count - 1; i >= 0; i--)
            {
                GameObject pooled = s_fakePool[i];
                s_fakePool.RemoveAt(i);
                if (pooled == null)
                {
                    continue;
                }
                MeshFilter filter = pooled.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    try
                    {
                        if (filter == null)
                        {
                            filter = pooled.AddComponent<MeshFilter>();
                        }
                        filter.sharedMesh = GetFakeQuadMesh();
                    }
                    catch { }
                }
                MeshRenderer renderer = pooled.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    try
                    {
                        if (renderer == null)
                        {
                            renderer = pooled.AddComponent<MeshRenderer>();
                        }
                        renderer.sharedMaterial = GetFakeBoltMaterial(donor);
                    }
                    catch { }
                }
                return pooled;
            }
            if (s_fakePool.Count >= FakePoolCap)
            {
                return null;
            }
            GameObject fresh = BuildFakeProjectile(donor);
            return fresh;
        }

        public static void ReturnPooledFake(GameObject shot)
        {
            if (shot == null)
            {
                return;
            }
            try { shot.SetActive(false); }
            catch { }
            for (int i = 0; i < s_fakePool.Count; i++)
            {
                if (s_fakePool[i] == shot)
                {
                    return;
                }
            }
            if (s_fakePool.Count >= FakePoolCap)
            {
                try { UnityEngine.Object.Destroy(shot); }
                catch { }
                return;
            }
            s_fakePool.Add(shot);
        }

        // Constroi o fake desativado e despojado antes da primeira ativacao:
        // SOMENTE malha + renderizador (+ Transform implicito). Nenhum
        // colisor, corpo, dano ou gatilho nasce aqui — visual-only desde o
        // nascimento (T-03-05). O fallback de material clona o sprite do
        // proprio puppet quando o shader padrao nao resolve.
        private static GameObject BuildFakeProjectile(Renderer materialDonor)
        {
            GameObject go = new GameObject("RemoteFlameShot");
            try { go.SetActive(false); }
            catch { }
            try
            {
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = GetFakeQuadMesh();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GetFakeBoltMaterial(materialDonor);
            }
            catch { }
            StripToVisualOnly(go);
            try { go.transform.localScale = new Vector3(1.2f, 0.35f, 1f); }
            catch { }
            return go;
        }

        private static Mesh GetFakeQuadMesh()
        {
            if (s_fakeQuadMesh != null)
            {
                return s_fakeQuadMesh;
            }
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            try { mesh.RecalculateNormals(); }
            catch { }
            mesh.name = "RemoteFlameShotQuad";
            s_fakeQuadMesh = mesh;
            return mesh;
        }

        private static Material GetFakeBoltMaterial(Renderer materialDonor)
        {
            if (s_fakeBoltMaterial != null)
            {
                return s_fakeBoltMaterial;
            }
            Material mat = null;
            try
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    mat = new Material(shader);
                    mat.color = new Color(1f, 0.62f, 0.15f, 1f);
                    mat.name = "RemoteFlameShotBolt";
                }
            }
            catch { mat = null; }
            if (mat == null && materialDonor != null)
            {
                try
                {
                    if (materialDonor.sharedMaterial != null)
                    {
                        mat = new Material(materialDonor.sharedMaterial);
                        mat.color = new Color(1f, 0.62f, 0.15f, 1f);
                        mat.name = "RemoteFlameShotBolt";
                    }
                }
                catch { mat = null; }
            }
            s_fakeBoltMaterial = mat;
            return mat;
        }

        // Despoja para visual-only: mantem Transform, sistemas de
        // particula, renderizadores e malhas; destroi todo o resto de forma
        // generica (sem nomear tipos de gameplay — o keep-list positivo e a
        // garantia). Mesma disciplina da factory.
        private static void StripToVisualOnly(GameObject root)
        {
            if (root == null)
            {
                return;
            }
            Component[] parts = null;
            try { parts = root.GetComponentsInChildren<Component>(true); }
            catch { parts = null; }
            if (parts == null)
            {
                return;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                Component c = parts[i];
                if (c == null)
                {
                    continue;
                }
                if (c is Transform)
                {
                    continue;
                }
                if (c is ParticleSystem)
                {
                    continue;
                }
                if (c is Renderer)
                {
                    continue;
                }
                if (c is MeshFilter)
                {
                    continue;
                }
                if (c is FakeFlameMover)
                {
                    continue;
                }
                if (c is TransientEventCleanup)
                {
                    continue;
                }
                try { UnityEngine.Object.DestroyImmediate(c); }
                catch { }
            }
        }

        private static GameObject FindLocalSeinObject()
        {
            try
            {
                if (Game.Characters.Sein != null && Game.Characters.Sein.gameObject != null)
                {
                    return Game.Characters.Sein.gameObject;
                }
            }
            catch { }
            return null;
        }

        private static ParticleSystem PickFlameParticle(ParticleSystem[] systems)
        {
            string[] keywords = new string[] { "shoot", "flame", "spirit", "muzzle", "projectile", "fire", "shot" };
            for (int k = 0; k < keywords.Length; k++)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] == null || systems[i].gameObject == null)
                    {
                        continue;
                    }
                    string name = systems[i].gameObject.name;
                    if (!string.IsNullOrEmpty(name)
                        && name.ToLower().Contains(keywords[k]))
                    {
                        return systems[i];
                    }
                }
            }
            return null;
        }

        private static AudioClip ResolveShotClip()
        {
            if (s_cachedShotClip != null)
            {
                return s_cachedShotClip;
            }
            GameObject seinGo = FindLocalSeinObject();
            if (seinGo == null)
            {
                return null;
            }
            AudioSource[] sources = null;
            try { sources = seinGo.GetComponentsInChildren<AudioSource>(true); }
            catch { sources = null; }
            if (sources == null)
            {
                return null;
            }
            string[] keywords = new string[] { "shoot", "flame", "spirit", "shot", "fire" };
            for (int k = 0; k < keywords.Length; k++)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] == null || sources[i].clip == null)
                    {
                        continue;
                    }
                    string name = sources[i].clip.name;
                    if (!string.IsNullOrEmpty(name)
                        && name.ToLower().Contains(keywords[k]))
                    {
                        s_cachedShotClip = sources[i].clip;
                        return s_cachedShotClip;
                    }
                }
            }
            return null;
        }

        private void VerboseEvent(string reason)
        {
            if (!OriCoopPlugin.IsAnimVerbose())
            {
                return;
            }
            OriCoopPlugin.LogInfo(string.Format("[EVENT] P{0} kind=spiritflame aplicado=parcial motivo={1}",
                PlayerId, reason));
        }
    }
}
