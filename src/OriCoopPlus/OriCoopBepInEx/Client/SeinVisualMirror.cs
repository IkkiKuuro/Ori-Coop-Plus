using System;
using Game;
using OriCoopBepInEx.Diagnostics;
using OriCoopBepInEx.Plugin;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    // Espelho visual do Sein real (orbe seguidor + tiro verdadeiro).
    // O orbe e um clone visual do Game.Characters.Ori (global, sem
    // MonoBehaviours de gameplay) que segue o puppet com o mesmo offset
    // local do orbe real. O tiro instancia o prefab verdadeiro do
    // SpiritFlame (SpiritFlame.Projectile) com os scripts destruidos
    // ainda desativado: zero dano, zero colisao, zero publicacao.
    public sealed class SeinVisualMirror : MonoBehaviour
    {
        private int _playerId;
        private GameObject _orbClone;
        private LegacyAnimator _shootAnim;

        private static GameObject s_cachedProjectilePrefab;

        // Prefab anotado pelo patch local (R3): o Postfix de
        // ThrowSpiritFlames roda na instancia exata que disparou e entrega
        // o prefab sem busca. Tem precedencia sobre a resolucao por
        // componentes. Limitacao conhecida: e o visual do nivel do jogador
        // local; o remoto pode ter nivel (e visual) distinto — nuance
        // aceita, muito acima do fake anterior.
        private static GameObject s_localShotPrefab;

        public static void NoteLocalPrefabs(GameObject projectilePrefab)
        {
            try
            {
                if (projectilePrefab != null)
                {
                    s_localShotPrefab = projectilePrefab;
                }
            }
            catch { }
        }

        public static SeinVisualMirror AttachTo(GameObject puppetRoot, int playerId)
        {
            if (puppetRoot == null)
            {
                return null;
            }
            GameObject clone = null;
            try
            {
                Ori realOrb = null;
                try { realOrb = Game.Characters.Ori; }
                catch { realOrb = null; }
                if (realOrb == null || realOrb.gameObject == null)
                {
                    return null;
                }

                LegacyAnimator realShoot = null;
                try { realShoot = realOrb.ShootAnimation; }
                catch { realShoot = null; }
                string shootName = null;
                try
                {
                    if (realShoot != null && realShoot.gameObject != null)
                    {
                        shootName = realShoot.gameObject.name;
                    }
                }
                catch { shootName = null; }

                // Clona desativado para que nenhum Start de gameplay rode no
                // clone; restaura o singleton + ativo do orbe real em seguida
                // (o Awake do clone pode re-registrar Game.Characters.Ori).
                bool wasActive = true;
                try { wasActive = realOrb.gameObject.activeSelf; }
                catch { wasActive = true; }
                try { realOrb.gameObject.SetActive(false); }
                catch { }

                try
                {
                    try
                    {
                        clone = UnityEngine.Object.Instantiate(realOrb.gameObject) as GameObject;
                    }
                    catch
                    {
                        clone = null;
                    }
                }
                finally
                {
                    try { Game.Characters.Ori = realOrb; }
                    catch { }
                    try
                    {
                        if (wasActive)
                        {
                            realOrb.gameObject.SetActive(true);
                        }
                    }
                    catch { }
                }

                if (clone == null)
                {
                    return null;
                }

                clone.name = string.Format("RemoteSeinOrb_P{0}", playerId);
                try { clone.transform.SetParent(puppetRoot.transform, false); }
                catch { }

                StripOrbClone(clone);
                IsolateMaterials(clone);

                LegacyAnimator shootAnim = null;
                try
                {
                    if (!string.IsNullOrEmpty(shootName))
                    {
                        Transform[] all = clone.GetComponentsInChildren<Transform>(true);
                        if (all != null)
                        {
                            for (int i = 0; i < all.Length; i++)
                            {
                                if (all[i] != null && all[i].name == shootName)
                                {
                                    try { shootAnim = all[i].GetComponent<LegacyAnimator>(); }
                                    catch { shootAnim = null; }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { shootAnim = null; }
                if (shootAnim == null)
                {
                    try { shootAnim = clone.GetComponentInChildren<LegacyAnimator>(true); }
                    catch { shootAnim = null; }
                }

                try { clone.SetActive(true); }
                catch { }

                SeinVisualMirror mirror = null;
                try { mirror = puppetRoot.AddComponent<SeinVisualMirror>(); }
                catch { mirror = null; }
                if (mirror == null)
                {
                    try { UnityEngine.Object.Destroy(clone); }
                    catch { }
                    return null;
                }
                mirror._playerId = playerId;
                mirror._orbClone = clone;
                mirror._shootAnim = shootAnim;

                try { mirror.SnapOrbToPuppet(); }
                catch { }

                return mirror;
            }
            catch
            {
                try
                {
                    if (clone != null)
                    {
                        UnityEngine.Object.Destroy(clone);
                    }
                }
                catch { }
                return null;
            }
        }

        public void PlayShot(Vector3 origin, Vector3 direction)
        {
            try
            {
                // (1) Anima o disparo no orbe clonado (best-effort).
                try
                {
                    if (_shootAnim != null)
                    {
                        try { _shootAnim.Restart(); }
                        catch { }
                    }
                    else
                    {
                        string noAnimLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado=espelho-parcial motivo=sem-shootanim", _playerId);
                        try { ReplicationObservability.Record(noAnimLine); }
                        catch { }
                        try { OriCoopPlugin.LogInfo(noAnimLine); }
                        catch { }
                    }
                }
                catch { }

                // (2) Prefab verdadeiro do projetil (lazy + cache).
                GameObject prefab = ResolveProjectilePrefab();
                if (prefab == null)
                {
                    string noPrefabLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado=manteve-atual motivo=sem-prefab", _playerId);
                    try { ReplicationObservability.Record(noPrefabLine); }
                    catch { }
                    try { OriCoopPlugin.LogInfo(noPrefabLine); }
                    catch { }
                    return;
                }

                SpiritFlameProjectile prefabComp = null;
                try { prefabComp = prefab.GetComponent<SpiritFlameProjectile>(); }
                catch { prefabComp = null; }

                // (3) Projetil visual em linha reta.
                try { SpawnRealShot(prefab, origin, direction); }
                catch { }

                // (4) Efeito de disparo do prefab (visual + auto-destroy).
                try
                {
                    if (prefabComp != null)
                    {
                        GameObject fxPrefab = null;
                        try { fxPrefab = prefabComp.ThrowEffectGameObject; }
                        catch { fxPrefab = null; }
                        if (fxPrefab != null)
                        {
                            SpawnThrowEffect(fxPrefab, origin);
                        }
                    }
                }
                catch { }

                // (5) Som de disparo do prefab (best-effort).
                try
                {
                    SoundProvider provider = null;
                    try
                    {
                        if (prefabComp != null)
                        {
                            provider = prefabComp.ThrowSound;
                        }
                    }
                    catch { provider = null; }
                    if (provider == null)
                    {
                        string noSoundLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado=espelho-parcial motivo=sem-som", _playerId);
                        try { ReplicationObservability.Record(noSoundLine); }
                        catch { }
                        try { OriCoopPlugin.LogInfo(noSoundLine); }
                        catch { }
                    }
                    else
                    {
                        try { Core.Sound.Play(provider.GetSound(null), origin, null); }
                        catch { }
                    }
                }
                catch { }

                string appliedLine = string.Format("[EVENT] P{0} kind=spiritflame aplicado=espelho-real motivo=recebido", _playerId);
                try { ReplicationObservability.Record(appliedLine); }
                catch { }
                try { OriCoopPlugin.LogInfo(appliedLine); }
                catch { }
            }
            catch { }
        }

        private void Update()
        {
            try { SnapOrbToPuppet(); }
            catch { }
        }

        private void SnapOrbToPuppet()
        {
            if (_orbClone == null)
            {
                return;
            }
            Ori realOrb = null;
            SeinCharacter sein = null;
            try { realOrb = Game.Characters.Ori; }
            catch { realOrb = null; }
            try { sein = Game.Characters.Sein; }
            catch { sein = null; }
            if (realOrb == null || sein == null)
            {
                return;
            }
            Transform orbT = null;
            Transform seinT = null;
            try { orbT = realOrb.transform; }
            catch { orbT = null; }
            try { seinT = sein.transform; }
            catch { seinT = null; }
            if (orbT == null || seinT == null)
            {
                return;
            }
            Vector3 offset = orbT.position - seinT.position;
            try { _orbClone.transform.position = transform.position + offset; }
            catch { }
        }

        private static GameObject ResolveProjectilePrefab()
        {
            try
            {
                // 1. Prefab anotado pelo tiro local (exato, sem busca).
                try
                {
                    if (s_localShotPrefab != null)
                    {
                        return s_localShotPrefab;
                    }
                }
                catch { }
                if (s_cachedProjectilePrefab != null)
                {
                    return s_cachedProjectilePrefab;
                }
                SeinCharacter sein = null;
                try { sein = Game.Characters.Sein; }
                catch { sein = null; }
                if (sein == null || sein.gameObject == null)
                {
                    return null;
                }
                SeinStandardSpiritFlameAbility ability = null;
                try { ability = sein.GetComponent<SeinStandardSpiritFlameAbility>(); }
                catch { ability = null; }
                if (ability == null)
                {
                    // Habilidades sao CharacterState (nao MonoBehaviour de
                    // cena): varre os filhos antes de desistir.
                    try
                    {
                        SeinStandardSpiritFlameAbility[] all = sein.GetComponentsInChildren<SeinStandardSpiritFlameAbility>(true);
                        if (all != null)
                        {
                            for (int i = 0; i < all.Length; i++)
                            {
                                if (all[i] != null)
                                {
                                    ability = all[i];
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
                if (ability == null)
                {
                    return null;
                }
                SpiritFlame flame = null;
                try { flame = ability.CurrentSpiritFlame; }
                catch { flame = null; }
                if (flame == null)
                {
                    return null;
                }
                GameObject prefab = null;
                try { prefab = flame.Projectile; }
                catch { prefab = null; }
                if (prefab == null)
                {
                    return null;
                }
                s_cachedProjectilePrefab = prefab;
                return prefab;
            }
            catch
            {
                return null;
            }
        }

        private static void SpawnRealShot(GameObject prefab, Vector3 origin, Vector3 direction)
        {
            GameObject shot = null;
            try { shot = UnityEngine.Object.Instantiate(prefab) as GameObject; }
            catch { shot = null; }
            if (shot == null)
            {
                return;
            }
            try { shot.SetActive(false); }
            catch { }
            try { shot.transform.parent = null; }
            catch { }
            StripVisualClone(shot, false);
            try
            {
                shot.transform.position = origin;
                Vector3 dir = direction;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = new Vector3(1f, 0f, 0f);
                }
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                shot.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            }
            catch { }
            try { shot.SetActive(true); }
            catch { }
            try
            {
                RealShotMover mover = shot.AddComponent<RealShotMover>();
                if (mover != null)
                {
                    mover.Launch(direction, RemotePlayerPuppet.FakeShotSpeed, RemotePlayerPuppet.FakeShotLifetime, RemotePlayerPuppet.FakeShotRange);
                }
            }
            catch { }
        }

        private static void SpawnThrowEffect(GameObject fxPrefab, Vector3 origin)
        {
            GameObject fx = null;
            try { fx = UnityEngine.Object.Instantiate(fxPrefab) as GameObject; }
            catch { fx = null; }
            if (fx == null)
            {
                return;
            }
            try { fx.SetActive(false); }
            catch { }
            try { fx.transform.parent = null; }
            catch { }
            StripVisualClone(fx, true);
            try { fx.transform.position = origin; }
            catch { }
            try { fx.SetActive(true); }
            catch { }
            try
            {
                TransientEventCleanup cleanup = fx.AddComponent<TransientEventCleanup>();
                if (cleanup != null)
                {
                    cleanup.Lifetime = 1.0f;
                }
            }
            catch { }
        }

        private static void StripOrbClone(GameObject clone)
        {
            if (clone == null)
            {
                return;
            }
            try
            {
                Light[] lights = clone.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    try
                    {
                        if (lights[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(lights[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            DestroyCommonJunk(clone);
            try
            {
                FollowPositionRotation[] follows = clone.GetComponentsInChildren<FollowPositionRotation>(true);
                for (int i = 0; i < follows.Length; i++)
                {
                    try
                    {
                        if (follows[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(follows[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                MonoBehaviour[] mbs = clone.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < mbs.Length; i++)
                {
                    MonoBehaviour mb = mbs[i];
                    if (mb == null)
                    {
                        continue;
                    }
                    if (mb is LegacyAnimator || mb is ScaleAnimator || mb is SeinVisualMirror)
                    {
                        continue;
                    }
                    try { UnityEngine.Object.DestroyImmediate(mb); }
                    catch { }
                }
            }
            catch { }
            try
            {
                Component[] all = clone.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Component c = all[i];
                    if (c == null)
                    {
                        continue;
                    }
                    if (c is Transform || c is Renderer || c is MeshFilter || c is ParticleSystem)
                    {
                        continue;
                    }
                    if (c is LegacyAnimator || c is ScaleAnimator || c is SeinVisualMirror)
                    {
                        continue;
                    }
                    try { UnityEngine.Object.DestroyImmediate(c); }
                    catch { }
                }
            }
            catch { }
        }

        private static void StripVisualClone(GameObject go, bool keepParticles)
        {
            if (go == null)
            {
                return;
            }
            DestroyCommonJunk(go);
            try
            {
                MonoBehaviour[] mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < mbs.Length; i++)
                {
                    try
                    {
                        if (mbs[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(mbs[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                Component[] all = go.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Component c = all[i];
                    if (c == null)
                    {
                        continue;
                    }
                    if (c is Transform || c is Renderer || c is MeshFilter)
                    {
                        continue;
                    }
                    if (keepParticles && c is ParticleSystem)
                    {
                        continue;
                    }
                    try { UnityEngine.Object.DestroyImmediate(c); }
                    catch { }
                }
            }
            catch { }
        }

        private static void DestroyCommonJunk(GameObject go)
        {
            try
            {
                Collider[] cols = go.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    try
                    {
                        if (cols[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(cols[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                Collider2D[] cols2 = go.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols2.Length; i++)
                {
                    try
                    {
                        if (cols2[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(cols2[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < rbs.Length; i++)
                {
                    try
                    {
                        if (rbs[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(rbs[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                Rigidbody2D[] rbs2 = go.GetComponentsInChildren<Rigidbody2D>(true);
                for (int i = 0; i < rbs2.Length; i++)
                {
                    try
                    {
                        if (rbs2[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(rbs2[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                AudioSource[] sources = go.GetComponentsInChildren<AudioSource>(true);
                for (int i = 0; i < sources.Length; i++)
                {
                    try
                    {
                        if (sources[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(sources[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            try
            {
                AudioListener[] listeners = go.GetComponentsInChildren<AudioListener>(true);
                for (int i = 0; i < listeners.Length; i++)
                {
                    try
                    {
                        if (listeners[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(listeners[i]);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void IsolateMaterials(GameObject clone)
        {
            try
            {
                Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r is ParticleSystemRenderer)
                    {
                        continue;
                    }
                    try
                    {
                        if (r.sharedMaterial != null)
                        {
                            r.material = new Material(r.sharedMaterial);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private sealed class RealShotMover : MonoBehaviour
        {
            private Vector3 _velocity;
            private Vector3 _start;
            private float _age;
            private float _lifetime = 0.8f;
            private float _range = 14f;

            public void Launch(Vector3 direction, float speed, float lifetime, float range)
            {
                try
                {
                    _start = transform.position;
                    Vector3 dir = direction;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = new Vector3(1f, 0f, 0f);
                    }
                    dir.Normalize();
                    _velocity = dir * speed;
                    _age = 0f;
                    if (lifetime > 0f)
                    {
                        _lifetime = lifetime;
                    }
                    if (range > 0f)
                    {
                        _range = range;
                    }
                }
                catch { }
            }

            private void Update()
            {
                try
                {
                    float dt = Time.deltaTime;
                    transform.position += _velocity * dt;
                    _age += dt;
                    if (_age >= _lifetime || Vector3.Distance(transform.position, _start) >= _range)
                    {
                        try { UnityEngine.Object.Destroy(gameObject); }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
