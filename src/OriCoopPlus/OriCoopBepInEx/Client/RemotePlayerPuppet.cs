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
        private string _lastAnimName;
        private const float InterpolationSmoothing = 18f;
        private const float ConfirmDelaySec = 0.15f;

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

            TextureAnimationWithTransitions targetClip = AnimationRegistry.Resolve(animName, animHash, confirmedState);

            if (targetClip == null)
            {
                try
                {
                    TextureAnimationWithTransitions[] local =
                        GetComponentsInChildren<TextureAnimationWithTransitions>(true);
                    if (local != null && local.Length > 0)
                    {
                        AnimationRegistry.RegisterClips(local);
                        targetClip = AnimationRegistry.Resolve(animName, animHash, confirmedState);
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

            if (_animator.CurrentAnimation != targetClip)
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
            TextureAnimationWithTransitions targetClip = AnimationRegistry.Resolve(animName, animHash, fallbackState);

            // Ultima tentativa: cataloga clipes visiveis no puppet e resolve de novo.
            if (targetClip == null)
            {
                try
                {
                    TextureAnimationWithTransitions[] local =
                        GetComponentsInChildren<TextureAnimationWithTransitions>(true);
                    if (local != null && local.Length > 0)
                    {
                        AnimationRegistry.RegisterClips(local);
                        targetClip = AnimationRegistry.Resolve(animName, animHash, fallbackState);
                    }
                }
                catch { }
            }

            bool applied = false;
            if (targetClip != null)
            {
                if (_animator.CurrentAnimation != targetClip)
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
            // Teleporte/logoff: se o alvo esta muito longe, teleporta em vez de
            // atravessar o mapa voando (que parecia "sumico" do jogador).
            float dist = Vector3.Distance(transform.position, _targetPosition);
            if (dist > 15f)
            {
                transform.position = _targetPosition;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _targetPosition, dt * InterpolationSmoothing);
            }
        }

        public void SnapTo(Vector3 position)
        {
            _targetPosition = position;
            transform.position = position;
        }
    }
}
