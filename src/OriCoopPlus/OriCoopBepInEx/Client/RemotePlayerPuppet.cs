using System;
using OriCoop;
using OriCoopBepInEx.Diagnostics;
using OriCoopBepInEx.Plugin;
using OriCoopBepInEx.UI;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
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
        // SOMENTE o clipe de ataque, resolvido via AnimationRegistry
        // exact-then-state com retorno fail-closed em null (D-15) — nunca
        // chute para Idle generico. Zero efeito gameplay: sem dano, sem
        // colisao, sem mutacao de estado. Particula, SFX transiente e
        // projetil fake sao escopo do plano 02, nao aqui.
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
                if (OriCoopPlugin.IsAnimVerbose())
                {
                    OriCoopPlugin.LogInfo(string.Format("[EVENT] P{0} kind=spiritflame aplicado=manteve-atual motivo=clip-desconhecido",
                        PlayerId));
                }
                return;
            }

            if (!IsPlayingClip(_animator, clip))
            {
                _animator.SetAnimation(clip, true);
            }
            if (OriCoopPlugin.IsAnimVerbose())
            {
                OriCoopPlugin.LogInfo(string.Format("[EVENT] P{0} kind=spiritflame aplicado={1} motivo=recebido",
                    PlayerId, clip.name));
            }
        }
    }
}
