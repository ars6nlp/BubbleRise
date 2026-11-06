using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Фирменные эффекты боссового скина в витрине: аура, след, удар и разрушение.
    ///
    /// Частицы описывает тот же <see cref="BossFx"/>, что и в бою, — форма, цвет,
    /// скорость, появление и угасание одни и те же. Отличается только холст: точки
    /// интерфейса вместо мировых частиц, поэтому движение повторяет
    /// <see cref="FxManager"/> (всплытие, вязкость, вращение, проявление за FadeIn),
    /// а мировые единицы пересчитываются в точки по радиусу шарика.
    ///
    /// Один пул на показ: в панели выбранного и в большом показе по своему.
    /// </summary>
    public sealed class BossFxShowcase
    {
        /// <summary>Радиус пузырька в бою, в юнитах: по нему мир пересчитывается в точки.</summary>
        public const float GameRadius = 0.22f;

        /// <summary>
        /// Скорости в витрине вдвое ниже игровых: поле показа — пара диаметров шарика,
        /// и на полной скорости разрушение улетало бы за край панели.
        /// </summary>
        const float VelocityScale = 0.5f;

        readonly Image[] _dots;
        readonly FxSpec[] _spec;
        readonly float[] _age;
        readonly Vector2[] _velocity;
        readonly List<FxSpec> _burst = new List<FxSpec>();
        float _auraEmit, _trailEmit, _phase;

        public BossFxShowcase(RectTransform parent, int count)
        {
            _dots = new Image[count];
            _spec = new FxSpec[count];
            _age = new float[count];
            _velocity = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BossFx" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.enabled = false;
                _dots[i] = img;
                _age[i] = float.MaxValue;
            }
        }

        /// <summary>Сколько точек живо сейчас — для проверок бюджета.</summary>
        public int Alive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _dots.Length; i++) if (_dots[i].enabled) n++;
                return n;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i].enabled = false;
                _age[i] = float.MaxValue;
            }
            _auraEmit = _trailEmit = 0f;
        }

        /// <summary>
        /// Аура и след вокруг шарика — с той же частотой, что в бою (след, как и
        /// прежний показ следа, вдвое реже: витрина маленькая).
        /// </summary>
        public void TickLook(SkinLook look, bool guardian, float dt, Vector2 center, float radiusPts)
        {
            if (!look.IsBoss) return;
            var def = look.BossDef;
            _phase += dt;

            float auraRate = Mathf.Min(def.AuraRate, BossFx.MaxAuraRate) * (guardian ? 0.5f : 1f) * Visuals.Particles;
            _auraEmit += dt * auraRate;
            while (_auraEmit >= 1f)
            {
                _auraEmit -= 1f;
                Spawn(BossFx.Aura(def), center, radiusPts, inRadii: true);
            }

            _trailEmit += dt * look.Fx.Rate * 0.5f * Visuals.Particles;
            while (_trailEmit >= 1f)
            {
                _trailEmit -= 1f;
                Spawn(SkinFxRuntime.SampleTrail(look, _phase).Spec, center, radiusPts, inRadii: true);
            }
        }

        /// <summary>Фирменный удар стража: в той же точке касания, той же порцией, что в бою.</summary>
        public void PlayStrike(BossSkinDef def, Vector2 contact, Vector2 normal, float radiusPts)
        {
            BossFx.Strike(def, normal, BossFx.MaxStrike, _burst);
            for (int i = 0; i < _burst.Count; i++) Spawn(_burst[i], contact, radiusPts, inRadii: false, burst: true);
        }

        /// <summary>Фирменное разрушение: порция урезается «Тихо» так же, как в бою.</summary>
        public void PlayBreak(BossSkinDef def, Vector2 at, float radiusPts)
        {
            BossFx.Break(def, Mathf.RoundToInt(BossFx.MaxBreak * Visuals.Particles), _burst);
            for (int i = 0; i < _burst.Count; i++) Spawn(_burst[i], at, radiusPts, inRadii: false, burst: true);
        }

        void Spawn(FxSpec t, Vector2 at, float radiusPts, bool inRadii, bool burst = false)
        {
            if (burst) t.Alpha *= BossFx.BurstAlpha;
            int slot = Free();
            if (slot < 0) return;

            float pointsPerUnit = radiusPts / GameRadius;
            var img = _dots[slot];
            img.sprite = SpriteFactory.FxShapeSprite(t.Shape);
            var rt = img.rectTransform;
            rt.anchoredPosition = at + (inRadii ? t.Offset * radiusPts : t.Offset * pointsPerUnit);
            rt.sizeDelta = Vector2.one * Mathf.Max(3f, t.Size * pointsPerUnit);
            rt.localRotation = Quaternion.Euler(0f, 0f, t.Angle);
            rt.localScale = Vector3.zero;

            t.Color = Palette.SafeLight(t.Color);
            t.Alpha *= Visuals.ParticleAlpha;
            t.Stretch = Mathf.Max(0.2f, t.Stretch);
            t.Gravity *= pointsPerUnit * VelocityScale;
            _spec[slot] = t;
            _velocity[slot] = t.Velocity * pointsPerUnit * VelocityScale;
            _age[slot] = 0f;
            img.color = t.Color.WithAlpha(0f);
            img.enabled = true;
        }

        int Free()
        {
            for (int i = 0; i < _dots.Length; i++)
                if (!_dots[i].enabled) return i;
            return -1;
        }

        /// <summary>Тот же закон, что у фирменных частиц <see cref="FxManager"/>.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                var img = _dots[i];
                if (!img.enabled) continue;

                var t = _spec[i];
                _age[i] += dt;
                if (_age[i] >= t.Life)
                {
                    img.enabled = false;
                    continue;
                }

                _velocity[i].y += t.Gravity * dt;
                if (t.Drag > 0f) _velocity[i] *= Mathf.Exp(-t.Drag * dt);
                var rt = img.rectTransform;
                rt.anchoredPosition += _velocity[i] * dt;
                if (t.Spin != 0f)
                {
                    t.Angle += t.Spin * dt;
                    _spec[i] = t;
                    rt.localRotation = Quaternion.Euler(0f, 0f, t.Angle);
                }

                float a = Mathf.Clamp01((t.Life - _age[i]) / Mathf.Max(0.05f, t.Life - t.FadeIn));
                if (_age[i] < t.FadeIn) a *= _age[i] / t.FadeIn;
                img.color = t.Color.WithAlpha(a * t.Alpha);

                float k = 0.6f + 0.4f * a;
                rt.localScale = new Vector3(k * t.Stretch, k, 1f);
            }
        }
    }
}
