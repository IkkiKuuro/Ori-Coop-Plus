using System;
using System.Reflection;
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
        private static GameObject s_chargeBurstPrefab;
        private static object s_chargeSoundProvider;

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

        public static void NoteChargePrefabs(GameObject burstPrefab)
        {
            try
            {
                if (burstPrefab != null)
                {
                    s_chargeBurstPrefab = burstPrefab;
                }
            }
            catch { }
        }

        public static void NoteChargeSound(object soundProvider)
        {
            try
            {
                if (soundProvider != null)
                {
                    s_chargeSoundProvider = soundProvider;
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

        public void PlayShot(Vector3 origin, Vector3 direction, Vector3 aim)
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

                // Parametros do voo lidos do prefab (reflexao defensiva).
                float travelTime = 0.45f;
                int vertexCount = 8;
                GameObject impactPrefab = null;
                GameObject throwPrefab = null;
                try
                {
                    if (prefabComp != null)
                    {
                        try
                        {
                            FieldInfo durField = prefabComp.GetType().GetField(
                                "Duration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (durField != null)
                            {
                                object dv = durField.GetValue(prefabComp);
                                if (dv is float && (float)dv > 0.01f && (float)dv < 5f)
                                {
                                    travelTime = (float)dv;
                                }
                            }
                        }
                        catch { }
                        try
                        {
                            FieldInfo vcField = prefabComp.GetType().GetField(
                                "LineVertexCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (vcField != null)
                            {
                                object vv = vcField.GetValue(prefabComp);
                                if (vv is int && (int)vv >= 2 && (int)vv <= 32)
                                {
                                    vertexCount = (int)vv;
                                }
                            }
                        }
                        catch { }
                        try { impactPrefab = prefabComp.ImpactEffectGameObject; }
                        catch { impactPrefab = null; }
                        try { throwPrefab = prefabComp.ThrowEffectGameObject; }
                        catch { throwPrefab = null; }
                    }
                }
                catch { }

                // (3) Beam orbe→mira com arco (geometria real do tiro).
                try { SpawnBeamShot(prefab, origin, aim, direction, travelTime, vertexCount, impactPrefab); }
                catch { }

                // (4) Efeito de disparo do prefab (visual + fade rapido).
                try
                {
                    if (throwPrefab != null)
                    {
                        SpawnThrowEffect(throwPrefab, origin);
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

        // Rajada carregada (kind 2, radial no orbe): prefab do burst
        // visual-only + fade + som do charge. Fallback honesto com motivo
        // se o prefab ainda nao foi anotado pelo patch local.
        public void PlayChargedShot(Vector3 origin)
        {
            try
            {
                try
                {
                    if (_shootAnim != null)
                    {
                        try { _shootAnim.Restart(); }
                        catch { }
                    }
                }
                catch { }
                GameObject burstPrefab = null;
                try { burstPrefab = s_chargeBurstPrefab; }
                catch { burstPrefab = null; }
                if (burstPrefab == null)
                {
                    string noBurstLine = string.Format("[EVENT] P{0} kind=chargedflame aplicado=manteve-atual motivo=sem-prefab-charge", _playerId);
                    try { ReplicationObservability.Record(noBurstLine); }
                    catch { }
                    try { OriCoopPlugin.LogInfo(noBurstLine); }
                    catch { }
                    return;
                }
                GameObject burst = null;
                try { burst = UnityEngine.Object.Instantiate(burstPrefab) as GameObject; }
                catch { burst = null; }
                if (burst == null)
                {
                    return;
                }
                try { burst.SetActive(false); }
                catch { }
                try { burst.transform.parent = null; }
                catch { }
                StripVisualClone(burst, true);
                try { IsolateMaterials(burst); }
                catch { }
                try { burst.transform.position = origin; }
                catch { }
                try { burst.SetActive(true); }
                catch { }
                try
                {
                    ParticleSystem[] systems = burst.GetComponentsInChildren<ParticleSystem>();
                    if (systems != null)
                    {
                        for (int i = 0; i < systems.Length; i++)
                        {
                            if (systems[i] == null)
                            {
                                continue;
                            }
                            try { systems[i].Play(); }
                            catch { }
                        }
                    }
                }
                catch { }
                try { FadeAndDie.Attach(burst, 0.6f); }
                catch { }
                try { TransientJanitor.Attach(burst, 1.5f); }
                catch { }
                try
                {
                    object sp = null;
                    try { sp = s_chargeSoundProvider; }
                    catch { sp = null; }
                    SoundProvider provider = sp as SoundProvider;
                    if (provider != null)
                    {
                        try { Core.Sound.Play(provider.GetSound(null), origin, null); }
                        catch { }
                    }
                }
                catch { }
                string appliedLine = string.Format("[EVENT] P{0} kind=chargedflame aplicado=espelho-real motivo=recebido", _playerId);
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

        // Beam orbe→mira (geometria real do tiro): o LineRenderer do jogo
        // interpola StartPosition→cabeca com arco; aqui o driver redesenha
        // os pontos a cada frame (start fixo, cabeca viajando start→aim em
        // travelTime, arco lateral senoidal). Sem alvo (aim≈origin), o beam
        // vai para origin+direction*6 (TempTarget real ~4.5 a frente).
        // Tudo desativado durante o strip; zero dano/colisao/publicacao.
        private static void SpawnBeamShot(GameObject prefab, Vector3 origin, Vector3 aim, Vector3 fallbackDir, float travelTime, int vertexCount, GameObject impactPrefab)
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
            Vector3 target = aim;
            try
            {
                Vector3 gap = aim - origin;
                if (gap.sqrMagnitude < 0.04f)
                {
                    Vector3 d = fallbackDir;
                    if (d.sqrMagnitude < 0.0001f)
                    {
                        d = new Vector3(1f, 0f, 0f);
                    }
                    d.Normalize();
                    target = origin + d * 6f;
                }
            }
            catch { target = aim; }
            LineRenderer beam = null;
            try
            {
                LineRenderer[] lines = shot.GetComponentsInChildren<LineRenderer>(true);
                if (lines != null)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i] == null)
                        {
                            continue;
                        }
                        if (beam == null)
                        {
                            beam = lines[i];
                        }
                        else
                        {
                            try { lines[i].enabled = false; }
                            catch { }
                        }
                    }
                }
            }
            catch { beam = null; }
            if (beam == null)
            {
                try { UnityEngine.Object.Destroy(shot); }
                catch { }
                return;
            }
            try
            {
                beam.enabled = true;
                beam.useWorldSpace = true;
                try
                {
                    if (beam.sharedMaterial != null)
                    {
                        beam.material = new Material(beam.sharedMaterial);
                    }
                }
                catch { }
                SetVertexCount(beam, vertexCount);
            }
            catch { }
            try { shot.transform.position = origin; }
            catch { }
            try { shot.SetActive(true); }
            catch { }
            try
            {
                RealBeamDriver driver = shot.AddComponent<RealBeamDriver>();
                if (driver != null)
                {
                    driver.Launch(beam, origin, target, travelTime, vertexCount, impactPrefab);
                }
            }
            catch { }
            // Segunda rede: teto absoluto de vida mesmo se o driver falhar.
            try { TransientJanitor.Attach(shot, travelTime + 2f); }
            catch { }
        }

        private static int ReadVertexCount(LineRenderer line)
        {
            try
            {
                try
                {
                    PropertyInfo p = line.GetType().GetProperty("positionCount");
                    if (p != null)
                    {
                        object v = p.GetValue(line, null);
                        if (v is int)
                        {
                            return (int)v;
                        }
                    }
                }
                catch { }
                try
                {
                    PropertyInfo n = line.GetType().GetProperty("numPositions");
                    if (n != null)
                    {
                        object v = n.GetValue(line, null);
                        if (v is int)
                        {
                            return (int)v;
                        }
                    }
                }
                catch { }
            }
            catch { }
            return -1;
        }

        private static void SetVertexCount(LineRenderer line, int count)
        {
            try
            {
                if (count < 2)
                {
                    count = 2;
                }
                if (count > 32)
                {
                    count = 32;
                }
                try
                {
                    PropertyInfo p = line.GetType().GetProperty("positionCount");
                    if (p != null)
                    {
                        p.SetValue(line, count, null);
                        return;
                    }
                }
                catch { }
                try
                {
                    PropertyInfo n = line.GetType().GetProperty("numPositions");
                    if (n != null)
                    {
                        n.SetValue(line, count, null);
                        return;
                    }
                }
                catch { }
                try
                {
                    MethodInfo m = line.GetType().GetMethod("SetVertexCount");
                    if (m != null)
                    {
                        m.Invoke(line, new object[] { count });
                    }
                }
                catch { }
            }
            catch { }
        }

        // Driver do beam: redesenha start→cabeca a cada frame com arco
        // lateral, scroll na textura, flash de impacto ao chegar e destroy.
        // Segunda rede de ciclo de vida: teto absoluto; destroi mesmo se o
        // driver/fade principal falhar (morte dos flashes era dirigida por
        // script — sem ela, qualquer falha vira blob eterno).
        private sealed class TransientJanitor : MonoBehaviour
        {
            private float _life = 1f;
            private float _age;

            public static void Attach(GameObject go, float life)
            {
                try
                {
                    if (go == null)
                    {
                        return;
                    }
                    TransientJanitor j = go.AddComponent<TransientJanitor>();
                    if (j != null && life > 0f && life < 30f)
                    {
                        j._life = life;
                    }
                }
                catch { }
            }

            private void Update()
            {
                try
                {
                    _age += Time.deltaTime;
                    if (_age >= _life)
                    {
                        try { UnityEngine.Object.Destroy(gameObject); }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private sealed class RealBeamDriver : MonoBehaviour
        {
            private LineRenderer _line;
            private Vector3 _start;
            private Vector3 _target;
            private float _travel = 0.45f;
            private float _age;
            private int _count = 8;
            private Material _mat;
            private GameObject _impactPrefab;
            private bool _done;

            public void Launch(LineRenderer line, Vector3 start, Vector3 target, float travelTime, int vertexCount, GameObject impactPrefab)
            {
                try
                {
                    _line = line;
                    _start = start;
                    _target = target;
                    if (travelTime > 0.05f && travelTime < 5f)
                    {
                        _travel = travelTime;
                    }
                    if (vertexCount >= 2 && vertexCount <= 32)
                    {
                        _count = vertexCount;
                    }
                    try
                    {
                        if (_line != null)
                        {
                            _mat = _line.material;
                        }
                    }
                    catch { _mat = null; }
                    _impactPrefab = impactPrefab;
                    _age = 0f;
                    // Limpa pontos assados do prefab ATE 32: se o set de count
                    // falhar no runtime, pontos alem do count ficariam fixos
                    // no mundo (rastro gigante = "animacao bugada" do R3-C1).
                    try
                    {
                        int actual = ReadVertexCount(_line);
                        if (actual >= 2 && actual <= 64)
                        {
                            _count = actual;
                        }
                    }
                    catch { }
                    try
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            try { _line.SetPosition(i, start); }
                            catch { break; }
                        }
                    }
                    catch { }
                    Draw(0f);
                    try
                    {
                        string diag = string.Format("[EVENT] diag-beam n={0}", _count);
                        try { ReplicationObservability.Record(diag); }
                        catch { }
                        try { OriCoopPlugin.LogInfo(diag); }
                        catch { }
                    }
                    catch { }
                }
                catch { }
            }

            private void Update()
            {
                try
                {
                    if (_done)
                    {
                        return;
                    }
                    _age += Time.deltaTime;
                    float t = _age / _travel;
                    if (t >= 1f)
                    {
                        t = 1f;
                        _done = true;
                    }
                    Draw(t);
                    if (_mat != null)
                    {
                        try
                        {
                            Vector2 off = _mat.mainTextureOffset;
                            off.x -= Time.deltaTime * 3f;
                            _mat.mainTextureOffset = off;
                        }
                        catch { }
                    }
                    if (_done)
                    {
                        try
                        {
                            if (_impactPrefab != null)
                            {
                                SpawnImpact(_impactPrefab, _target);
                            }
                        }
                        catch { }
                        try { UnityEngine.Object.Destroy(gameObject); }
                        catch { }
                    }
                }
                catch { }
            }

            private void Draw(float t)
            {
                try
                {
                    if (_line == null)
                    {
                        return;
                    }
                    Vector3 head = _start + (_target - _start) * t;
                    Vector3 dir = _target - _start;
                    Vector3 side = new Vector3(0f, 0f, 0f);
                    try
                    {
                        if (dir.sqrMagnitude > 0.0001f)
                        {
                            dir.Normalize();
                            side = new Vector3(-dir.y, dir.x, 0f);
                        }
                    }
                    catch { }
                    float span = 0.8f;
                    try
                    {
                        float dist = Vector3.Distance(_start, _target);
                        if (dist > 0.01f && dist < 3f)
                        {
                            span = dist * 0.3f;
                        }
                    }
                    catch { }
                    for (int i = 0; i < _count; i++)
                    {
                        float f = (_count <= 1) ? 0f : ((float)i / (float)(_count - 1));
                        Vector3 p = _start + (head - _start) * f;
                        try
                        {
                            float bow = Mathf.Sin(f * Mathf.PI) * span;
                            p += side * bow;
                        }
                        catch { }
                        try { _line.SetPosition(i, p); }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static void SpawnImpact(GameObject fxPrefab, Vector3 at)
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
            try { IsolateMaterials(fx); }
            catch { }
            try { fx.transform.position = at; }
            catch { }
            try { fx.SetActive(true); }
            catch { }
            try
            {
                ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>();
                if (systems != null)
                {
                    for (int i = 0; i < systems.Length; i++)
                    {
                        if (systems[i] == null)
                        {
                            continue;
                        }
                        try { systems[i].Play(); }
                        catch { }
                    }
                }
            }
            catch { }
            try { FadeAndDie.Attach(fx, 0.4f); }
            catch { }
            try { TransientJanitor.Attach(fx, 1.0f); }
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
            // Particulas do prefab podem nascer pausadas fora do Start
            // original (destruido no strip): forca o play de todas.
            try
            {
                ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>();
                if (systems != null)
                {
                    for (int i = 0; i < systems.Length; i++)
                    {
                        if (systems[i] == null)
                        {
                            continue;
                        }
                        try { systems[i].Play(); }
                        catch { }
                    }
                }
            }
            catch { }
            // Fade rapido com materiais isolados: sem o fade dirigido pelo
            // driver, o flash congelava no brilho maximo pela vida toda —
            // o "bloom piscando" do R3-C1. 0.35s e some.
            try { IsolateMaterials(fx); }
            catch { }
            try { FadeAndDie.Attach(fx, 0.35f); }
            catch { }
            try { TransientJanitor.Attach(fx, 0.8f); }
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

        // Some o flash (materiais ja isolados): fade em _Color E _TintColor
        // (particulas Unity 4 usam _TintColor — so _Color era no-op) e
        // destroy no fim. Sem o fade do driver original, o flash congelava
        // no brilho maximo ("bloom piscando" do R3-C1).
        private sealed class FadeAndDie : MonoBehaviour
        {
            private Material[] _mats;
            private float _life = 0.35f;
            private float _age;

            public static void Attach(GameObject go, float life)
            {
                try
                {
                    FadeAndDie f = go.AddComponent<FadeAndDie>();
                    if (f == null)
                    {
                        return;
                    }
                    if (life > 0f)
                    {
                        f._life = life;
                    }
                    try
                    {
                        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                        if (rs != null)
                        {
                            Material[] mats = new Material[rs.Length];
                            for (int i = 0; i < rs.Length; i++)
                            {
                                if (rs[i] == null || rs[i] is ParticleSystemRenderer)
                                {
                                    continue;
                                }
                                Material m = null;
                                try { m = rs[i].material; }
                                catch { m = null; }
                                mats[i] = m;
                            }
                            f._mats = mats;
                        }
                    }
                    catch { }
                }
                catch { }
            }

            private void Update()
            {
                try
                {
                    _age += Time.deltaTime;
                    float t = _age / _life;
                    if (t >= 1f)
                    {
                        try { UnityEngine.Object.Destroy(gameObject); }
                        catch { }
                        return;
                    }
                    float a = 1f - t;
                    if (_mats != null)
                    {
                        for (int i = 0; i < _mats.Length; i++)
                        {
                            Material m = _mats[i];
                            if (m == null)
                            {
                                continue;
                            }
                            try
                            {
                                if (m.HasProperty("_Color"))
                                {
                                    Color c = m.color;
                                    c.a = a;
                                    m.color = c;
                                }
                            }
                            catch { }
                            try
                            {
                                if (m.HasProperty("_TintColor"))
                                {
                                    Color tc = m.GetColor("_TintColor");
                                    tc.a = a;
                                    m.SetColor("_TintColor", tc);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
