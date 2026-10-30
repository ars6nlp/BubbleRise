using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Фон: вертикальный градиент, который светлеет по мере подъёма,
    /// и декоративные пузырьки, всплывающие вверх (на геймплей не влияют).
    /// </summary>
    public class BackgroundController : MonoBehaviour
    {
        const int GradientSteps = 64;

        class BgBubble
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Parallax;
            public float Drift;
            public float Size;
        }

        GameConfig _cfg;
        Playfield _field;

        SpriteRenderer _gradient;
        Texture2D _gradientTex;
        Color32[] _gradientPixels;
        float _lastT = -1f;
        float _lastBlend = -1f;

        Color _themeTop, _themeBottom;
        float _themeBlend;

        BgBubble[] _bubbles;

        // ----- лучи света с поверхности
        SpriteRenderer[] _shafts;
        float[] _shaftPhase;
        float[] _shaftWidth;

        OceanAmbience _ambience;

        // ----- проплывающая стайка рыб
        SpriteRenderer[] _school;
        float _schoolTimer;
        float _schoolX, _schoolY, _schoolSpeed, _schoolPhase, _schoolSize;
        bool _schoolActive;

        public void Init(GameConfig cfg, Playfield field)
        {
            _cfg = cfg;
            _field = field;

            // ----- градиент
            _gradientTex = SpriteFactory.CreateGradientTexture(GradientSteps);
            _gradientPixels = new Color32[GradientSteps];
            var sprite = Sprite.Create(_gradientTex, new Rect(0, 0, 1, GradientSteps),
                                       new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

            var gradGo = new GameObject("Gradient");
            gradGo.transform.SetParent(transform, false);
            _gradient = gradGo.AddComponent<SpriteRenderer>();
            _gradient.sprite = sprite;
            _gradient.sharedMaterial = SpriteFactory.Unlit;
            _gradient.sortingOrder = SortingOrders.Background;

            // ----- декоративные пузырьки
            int count = Mathf.Max(0, cfg.backgroundBubbleCount);
            _bubbles = new BgBubble[count];
            var dot = SpriteFactory.SoftDot();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BgBubble");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dot;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.BackgroundBubbles;
                sr.color = Palette.Foam.WithAlpha(0.18f);

                _bubbles[i] = new BgBubble { Tr = go.transform, Sr = sr };
                Respawn(_bubbles[i], true);
            }

            BuildLightShafts();
            BuildSchool();

            // Планктон, струи со дна и далёкие силуэты живут в отдельном компоненте,
            // чтобы этот класс не превратился в свалку.
            var ambienceGo = new GameObject("Ambience");
            ambienceGo.transform.SetParent(transform, false);
            _ambience = ambienceGo.AddComponent<OceanAmbience>();
            _ambience.Init(cfg, field);

            ApplyLayout();
            UpdateGradient(0f, true);
        }

        void BuildLightShafts()
        {
            int count = Mathf.Max(0, _cfg.lightShaftCount);
            _shafts = new SpriteRenderer[count];
            _shaftPhase = new float[count];
            _shaftWidth = new float[count];

            var sprite = SpriteFactory.LightShaft();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("LightShaft");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.LightShaft;
                sr.color = Palette.Foam.WithAlpha(0f);

                // лучи наклонены в одну сторону, как настоящие — свет идёт с поверхности
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-16f, -6f));

                _shafts[i] = sr;
                _shaftPhase[i] = Random.Range(0f, 10f);
                _shaftWidth[i] = Random.Range(1.6f, 3.4f);
            }
        }

        void BuildSchool()
        {
            int count = Mathf.Max(0, _cfg.schoolSize);
            _school = new SpriteRenderer[count];
            var sprite = SpriteFactory.FishSprite(FishType.Piranha);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("SchoolFish");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.BackgroundBubbles;
                go.SetActive(false);
                _school[i] = sr;
            }

            _schoolTimer = Random.Range(_cfg.schoolIntervalMin, _cfg.schoolIntervalMax);
        }

        void Respawn(BgBubble b, bool anywhere)
        {
            // крупные частицы «ближе» к камере и проносятся быстрее — простой параллакс
            b.Parallax = Random.Range(_cfg.backgroundParallaxMin, _cfg.backgroundParallaxMax);
            b.Size = Mathf.Lerp(0.045f, 0.20f, Mathf.InverseLerp(_cfg.backgroundParallaxMin,
                                                                 _cfg.backgroundParallaxMax, b.Parallax));
            b.Drift = Random.Range(-0.09f, 0.09f);
            float x = Random.Range(-_field.HalfWidth, _field.HalfWidth);
            // появляются сверху и уходят вниз: мир проносится мимо поднимающегося пузырька
            float y = anywhere ? Random.Range(_field.Bottom, _field.Top) : _field.Top + 0.2f;
            b.Tr.position = new Vector3(x, y, 0f);
            b.Tr.localScale = Vector3.one * b.Size;
            b.Sr.color = Palette.Foam.WithAlpha(Random.Range(0.10f, 0.24f) * Mathf.Lerp(0.6f, 1f, b.Parallax));
        }

        public void ApplyLayout()
        {
            // спрайт градиента в мире 1 x GradientSteps юнитов — растягиваем на весь экран
            _gradient.transform.localScale = new Vector3(
                _field.HalfWidth * 2f + 0.2f,
                (_field.Top - _field.Bottom) / GradientSteps,
                1f);
            _gradient.transform.position = Vector3.zero;
        }

        /// <param name="depth">Пройденная глубина в метрах.</param>
        /// <param name="scrollSpeed">Скорость подъёма пузырька, юнитов/сек.</param>
        public void Tick(float dt, float depth, float scrollSpeed)
        {
            ApplyLayout(); // экран мог измениться (поворот телефона, ресайз окна браузера)
            UpdateGradient(Mathf.Clamp01(depth / Mathf.Max(1f, _cfg.surfaceDepth)), false);

            for (int i = 0; i < _bubbles.Length; i++)
            {
                var b = _bubbles[i];
                var p = b.Tr.position;

                // мы поднимаемся, поэтому вода уходит вниз; собственная плавучесть частиц
                // это движение лишь чуть замедляет
                float vertical = _cfg.backgroundBuoyancy - scrollSpeed * b.Parallax;
                p.y += vertical * dt;
                p.x += Mathf.Sin(p.y * 1.6f) * b.Drift * dt;
                b.Tr.position = p;

                // вытягиваем по вертикали пропорционально скорости — лёгкие линии скорости
                float streak = 1f + Mathf.Clamp01(Mathf.Abs(vertical) / 3f) * 1.1f;
                b.Tr.localScale = new Vector3(b.Size, b.Size * streak, 1f);

                if (p.y < _field.Bottom - 0.3f) Respawn(b, false);
                else if (p.y > _field.Top + 0.6f) Respawn(b, false);
            }

            TickLightShafts(dt, Mathf.Clamp01(depth / Mathf.Max(1f, _cfg.surfaceDepth)));
            TickSchool(dt, scrollSpeed);
            _ambience.Tick(dt, depth, scrollSpeed);
        }

        /// <summary>
        /// Лучи с поверхности: не скроллятся вместе с миром — свет идёт сверху всегда.
        /// Ближе к поверхности разгораются, поэтому подъём читается ещё и по свету.
        /// </summary>
        void TickLightShafts(float dt, float surfaceProgress)
        {
            if (_shafts == null) return;

            float height = (_field.Top - _field.Bottom) * 1.4f;
            float spanX = _field.HalfWidth * 1.05f;

            for (int i = 0; i < _shafts.Length; i++)
            {
                _shaftPhase[i] += dt * 0.11f;

                float slot = _shafts.Length > 1 ? i / (float)(_shafts.Length - 1) : 0.5f;
                float drift = Mathf.Sin(_shaftPhase[i] * 1.3f + i) * (spanX * 0.12f);
                float x = Mathf.Lerp(-spanX * 0.8f, spanX * 0.8f, slot) + drift;

                var sr = _shafts[i];
                var tr = sr.transform;
                tr.position = new Vector3(x, _field.Top - height * 0.42f, 0f);
                // спрайт в мире 0.375 x 1 юнита при масштабе 1
                tr.localScale = new Vector3(_shaftWidth[i] / 0.375f, height, 1f);

                // Размах дыхания срезан с 35 % до 22 % и замедлен: луч должен
                // колыхаться, а не мерцать. Сама альфа и без того меньше десятой.
                float breathe = 0.78f + 0.22f * Mathf.Sin(_shaftPhase[i] * 1.2f + i * 1.7f);
                float alpha = _cfg.lightShaftIntensity * 0.16f * breathe
                              * Mathf.Lerp(0.45f, 1f, surfaceProgress);
                sr.color = Palette.Foam.WithAlpha(alpha);
            }
        }

        /// <summary>Стайка мелких рыбёшек, изредка пересекающая экран. Чистая декорация.</summary>
        void TickSchool(float dt, float scrollSpeed)
        {
            if (_school == null || _school.Length == 0) return;

            if (!_schoolActive)
            {
                _schoolTimer -= dt;
                if (_schoolTimer > 0f) return;
                StartSchool();
                return;
            }

            _schoolPhase += dt;
            _schoolX += _schoolSpeed * dt;
            _schoolY -= scrollSpeed * 0.55f * dt; // сносится вместе с миром, но медленнее

            float span = _field.HalfWidth + 2.5f;
            bool goingRight = _schoolSpeed > 0f;

            for (int i = 0; i < _school.Length; i++)
            {
                // рыбки идут уступом и слегка виляют
                float back = i * 0.42f * (goingRight ? -1f : 1f);
                float lane = ((i % 3) - 1) * 0.34f;
                float wave = Mathf.Sin(_schoolPhase * 4f + i * 0.8f) * 0.10f;

                var tr = _school[i].transform;
                tr.position = new Vector3(_schoolX + back, _schoolY + lane + wave, 0f);
                tr.localScale = Vector3.one * _schoolSize;
                tr.rotation = Quaternion.identity;
                _school[i].flipY = !goingRight;
                if (!goingRight) tr.rotation = Quaternion.Euler(0f, 0f, 180f);
            }

            if (Mathf.Abs(_schoolX) > span || _schoolY < _field.Bottom - 2f)
            {
                _schoolActive = false;
                for (int i = 0; i < _school.Length; i++) _school[i].gameObject.SetActive(false);
                _schoolTimer = Random.Range(_cfg.schoolIntervalMin, _cfg.schoolIntervalMax);
            }
        }

        void StartSchool()
        {
            _schoolActive = true;
            _schoolPhase = 0f;
            _schoolSize = Random.Range(0.20f, 0.32f);

            bool fromLeft = Random.value < 0.5f;
            float span = _field.HalfWidth + 2f;
            _schoolX = fromLeft ? -span : span;
            _schoolSpeed = (fromLeft ? 1f : -1f) * Random.Range(1.6f, 2.8f);
            _schoolY = Random.Range(_field.Bottom + 1.5f, _field.Top - 1.5f);

            // тёмный силуэт: стайка читается как дальний план, а не как угроза
            Color tint = Color.Lerp(Palette.Deep2, Palette.Foam, 0.22f).WithAlpha(0.55f);
            for (int i = 0; i < _school.Length; i++)
            {
                _school[i].color = tint;
                _school[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Подмешать цвет воды локации. blend = 0 — обычный градиент глубины,
        /// 1 — полностью цвет локации.
        /// </summary>
        public void SetTheme(Color top, Color bottom, float blend)
        {
            _themeTop = top;
            _themeBottom = bottom;
            _themeBlend = Mathf.Clamp01(blend);
        }

        public void ClearTheme() => _themeBlend = 0f;

        void UpdateGradient(float t, bool force)
        {
            if (!force && Mathf.Abs(t - _lastT) < 0.002f && Mathf.Abs(_themeBlend - _lastBlend) < 0.002f) return;
            _lastT = t;
            _lastBlend = _themeBlend;

            Color top = Color.Lerp(Palette.GradTopDeep, Palette.GradTopSurface, t);
            Color bottom = Color.Lerp(Palette.GradBotDeep, Palette.GradBotSurface, t);

            if (_themeBlend > 0f)
            {
                top = Color.Lerp(top, _themeTop, _themeBlend);
                bottom = Color.Lerp(bottom, _themeBottom, _themeBlend);
            }

            for (int i = 0; i < GradientSteps; i++)
            {
                float k = i / (GradientSteps - 1f); // 0 — низ текстуры (низ экрана)
                _gradientPixels[i] = Color.Lerp(bottom, top, k);
            }

            _gradientTex.SetPixels32(_gradientPixels);
            _gradientTex.Apply(false);
        }
    }
}
