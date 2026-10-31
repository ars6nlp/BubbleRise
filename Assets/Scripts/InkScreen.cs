using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Чернила на «объективе камеры».
    ///
    /// Живёт не в мире, а на камере — поэтому заливка не уезжает вниз вместе с
    /// прокруткой, а остаётся на стекле, как ей и положено.
    ///
    /// Работает в два такта:
    ///
    /// 1. **Глухая заливка.** Экран закрыт целиком, сцена едва угадывается. Ничего
    ///    искать в этот момент нельзя и не нужно — это пауза вслепую.
    /// 2. **Растворение.** Чернила сходят, картинка возвращается. Вот тогда игрок и
    ///    ищет стража, которого зашвырнуло в угол.
    ///
    /// Два слоя, а не один: сплошная подложка гарантирует, что между кляксами не
    /// останется просветов, а кляксы поверх неё дают неровный край. Подложка уходит
    /// вдвое быстрее, поэтому хвост растворения — это уже рваные пятна, а не
    /// равномерно светлеющий прямоугольник.
    /// </summary>
    public class InkScreen : MonoBehaviour
    {
        // Сетка 4x5 покрывает экран при любом соотношении сторон и стоит два десятка
        // спрайтов — заметно дешевле, чем одна огромная клякса с большим овердро.
        const int Columns = 4;
        const int Rows = 5;
        const int Count = Columns * Rows;

        readonly SpriteRenderer[] _splats = new SpriteRenderer[Count];
        readonly float[] _delay = new float[Count];
        SpriteRenderer _base;

        Playfield _field;
        float _peakAlpha;

        bool _active;
        float _hold;
        float _fade = 1f;
        float _elapsed;
        float _rise = 1f;     // нарастание заливки, 0…1

        /// <summary>
        /// За сколько чернила закрывают экран, с. Раньше — за один кадр:
        /// средняя яркость падала на 72 % ступенькой на весь экран (E-11).
        /// Время входит в удержание: экран закрыт столько же, сколько раньше.
        /// </summary>
        public const float RiseTime = 0.2f;

        /// <summary>Насколько плотно закрыт экран: 0 — чисто, 1 — глухая заливка.</summary>
        public float Coverage { get; private set; }

        public void Init(GameConfig cfg, Playfield field, Transform cameraTransform)
        {
            _field = field;
            _peakAlpha = Mathf.Clamp01(cfg.inkBlackoutAlpha);

            // Родитель — камера: чернила должны остаться на стекле, а не уплыть с миром.
            transform.SetParent(cameraTransform, false);
            transform.localPosition = new Vector3(0f, 0f, 1f);

            _base = NewLayer("InkBase", SortingOrders.Ink);
            _base.sprite = SpriteFactory.SolidBlock();

            for (int i = 0; i < Count; i++)
                _splats[i] = NewLayer("Splat" + i, SortingOrders.Ink + 1);

            Clear();
        }

        SpriteRenderer NewLayer(string layerName, int order)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <param name="holdTime">Сколько держать экран закрытым наглухо.</param>
        /// <param name="fadeTime">За сколько чернила сходят после этого.</param>
        public void Splash(float holdTime, float fadeTime)
        {
            _active = true;
            _hold = Mathf.Max(0f, holdTime);
            _fade = Mathf.Max(0.1f, fadeTime);
            _elapsed = 0f;
            _rise = 0f;

            float screenW = _field.HalfWidth * 2f;
            float screenH = _field.Top - _field.Bottom;

            // Подложка с запасом: при смене ориентации экрана в середине боя
            // просветов по краям быть не должно.
            _base.transform.localPosition = new Vector3(0f, (_field.Top + _field.Bottom) * 0.5f, 0f);
            _base.transform.localScale = new Vector3(screenW * 1.3f, screenH * 1.3f, 1f);
            _base.enabled = true;

            float cellW = screenW / Columns;
            float cellH = screenH / Rows;
            float size = Mathf.Max(cellW, cellH) * 1.7f;

            for (int i = 0; i < Count; i++)
            {
                int col = i % Columns;
                int row = i / Columns;

                // Джиттер небольшой: сетка не должна читаться сеткой, но и заметных
                // прорех к концу растворения появиться не должно.
                float x = -_field.HalfWidth + cellW * (col + 0.5f) + Random.Range(-cellW * 0.22f, cellW * 0.22f);
                float y = _field.Bottom + cellH * (row + 0.5f) + Random.Range(-cellH * 0.22f, cellH * 0.22f);

                var sr = _splats[i];
                sr.sprite = SpriteFactory.InkSplat(Random.Range(0, 3));
                sr.transform.localPosition = new Vector2(x, y);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                sr.transform.localScale = Vector3.one * (size * Random.Range(0.95f, 1.2f));
                sr.enabled = true;

                // Разброс момента исчезновения: кляксы сходят вразнобой, а не разом.
                _delay[i] = Random.Range(0f, 0.45f);
            }

            Apply(0f, 0f);
        }

        public void Tick(float dt)
        {
            if (!_active) return;

            if (_rise < 1f)
            {
                _rise = Mathf.Min(1f, _rise + dt / RiseTime);
                _hold -= dt;
                float r = Mathf.SmoothStep(0f, 1f, _rise);
                Apply(r, r);
                return;
            }

            if (_hold > 0f)
            {
                _hold -= dt;
                Apply(1f, 1f);
                return;
            }

            _elapsed += dt;

            // Подложка уходит вдвое быстрее клякс — хвост растворения получается рваным.
            float baseK = 1f - Mathf.Clamp01(_elapsed / (_fade * 0.5f));
            float splatK = 1f - Mathf.Clamp01(_elapsed / _fade);

            if (baseK <= 0f && splatK <= 0f) { Clear(); return; }

            Apply(baseK, splatK);
        }

        void Apply(float baseK, float splatK)
        {
            _base.enabled = baseK > 0f;
            if (_base.enabled) _base.color = InkColor(_peakAlpha * baseK);

            float splatSum = 0f;
            for (int i = 0; i < Count; i++)
            {
                // Своя задержка у каждой кляксы: одни держатся дольше других.
                float k = Mathf.Clamp01((splatK - _delay[i]) / Mathf.Max(0.01f, 1f - _delay[i]));
                splatSum += k;

                var sr = _splats[i];
                if (k <= 0f) sr.enabled = false;
                else { sr.enabled = true; sr.color = InkColor(_peakAlpha * k); }
            }

            Coverage = Mathf.Max(baseK, splatSum / Count);
        }

        public void Clear()
        {
            _active = false;
            _hold = 0f;
            _elapsed = 0f;
            Coverage = 0f;

            if (_base != null) _base.enabled = false;
            for (int i = 0; i < Count; i++)
                if (_splats[i] != null) _splats[i].enabled = false;
        }

        static Color InkColor(float alpha)
        {
            var c = Palette.InkBody;
            c.a = alpha;
            return c;
        }
    }
}
