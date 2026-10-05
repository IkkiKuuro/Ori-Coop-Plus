using System;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    public sealed class FloatingNameTag : MonoBehaviour
    {
        private TextMesh _textMesh;
        private TextMesh _shadowMesh;
        private string _nickname = "Player";
        private Transform _targetTransform;

        public static FloatingNameTag Attach(GameObject parent, string nickname)
        {
            if (parent == null)
            {
                return null;
            }

            // If already attached, reuse existing
            FloatingNameTag existing = parent.GetComponentInChildren<FloatingNameTag>();
            if (existing != null)
            {
                existing.SetNickname(nickname);
                return existing;
            }

            GameObject tagObj = new GameObject("CoopNameTag");
            tagObj.transform.SetParent(parent.transform, false);
            tagObj.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            FloatingNameTag tag = tagObj.AddComponent<FloatingNameTag>();
            tag._targetTransform = parent.transform;
            tag.Init(nickname);
            return tag;
        }

        private void Init(string nickname)
        {
            _nickname = string.IsNullOrEmpty(nickname) ? "Player" : nickname;

            // Shadow mesh for legibility against bright backgrounds
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(transform, false);
            shadowObj.transform.localPosition = new Vector3(0.04f, -0.04f, 0.01f);
            _shadowMesh = shadowObj.AddComponent<TextMesh>();
            _shadowMesh.text = _nickname;
            _shadowMesh.fontSize = 28;
            _shadowMesh.characterSize = 0.075f;
            _shadowMesh.anchor = TextAnchor.MiddleCenter;
            _shadowMesh.alignment = TextAlignment.Center;
            _shadowMesh.color = new Color(0f, 0f, 0f, 0.85f);

            // Foreground text mesh
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = Vector3.zero;
            _textMesh = textObj.AddComponent<TextMesh>();
            _textMesh.text = _nickname;
            _textMesh.fontSize = 28;
            _textMesh.characterSize = 0.075f;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.color = Color.white;
        }

        public void SetNickname(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            _nickname = name;
            if (_textMesh != null)
            {
                _textMesh.text = name;
            }
            if (_shadowMesh != null)
            {
                _shadowMesh.text = name;
            }
        }

        public void SetColor(Color color)
        {
            if (_textMesh != null)
            {
                _textMesh.color = color;
            }
        }

        private void LateUpdate()
        {
            // Lock rotation so name tag stays upright regardless of character flipping/rotation
            transform.rotation = Quaternion.identity;

            // Counteract any X-axis flip on parent transform
            if (_targetTransform != null)
            {
                float parentSign = Mathf.Sign(_targetTransform.lossyScale.x);
                transform.localScale = new Vector3(parentSign >= 0f ? 1f : -1f, 1f, 1f);
            }
        }
    }
}
