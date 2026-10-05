using System;
using OriCoop;
using OriCoopBepInEx.Diagnostics;
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

        private void ApplyAnimation(string animName, uint animHash)
        {
            if (_animator == null)
            {
                return;
            }

            ActionVisualState fallbackState = AnimationRegistry.InferStateFromMovement(_velocity, true);
            TextureAnimationWithTransitions targetClip = AnimationRegistry.Resolve(animName, animHash, fallbackState);

            bool applied = false;
            if (targetClip != null)
            {
                _animator.SetAnimation(targetClip, true);
                applied = true;
            }

            if (_lastAnimName != animName)
            {
                _lastAnimName = animName;
                ReplicationObservability.TrackPacket(PlayerId, animHash, animName ?? fallbackState.ToString(), applied);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, _targetPosition, dt * InterpolationSmoothing);
        }
    }
}
