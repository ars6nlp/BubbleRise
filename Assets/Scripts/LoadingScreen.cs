using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Экран загрузки: ровная тёмная вода и тонкая полоса прогресса.
    ///
    /// Без текста — язык площадки на этот момент ещё не известен, и шрифт не
    /// нужен. Без вспышек: фон одного цвета с водой на старте, полоса растёт
    /// плавно (не скачет за шагами прогрева), а в конце экран тает за 0.4 с
    /// поверх уже собранного меню.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        const float BarWidth = 260f, BarHeight = 6f;
        const float FadeOut = 0.4f;

        RectTransform _fill;
        CanvasGroup _group;
        float _shown, _target;
        bool _hiding;

        public static LoadingScreen Create(Transform parent)
        {
            var go = new GameObject("LoadingScreen");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 1f;
            var screen = go.AddComponent<LoadingScreen>();
            screen._group = go.AddComponent<CanvasGroup>();
            screen._group.blocksRaycasts = true;

            // фон — тон воды на старте подъёма: меню проявляется из того же цвета
            var bg = NewImage("Water", go.transform, DepthLook.At(0f).WaterMid);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var track = NewImage("Track", go.transform, Palette.Foam.WithAlpha(0.12f));
            var trackRt = track.rectTransform;
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0.42f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(BarWidth, BarHeight);

            var fill = NewImage("Fill", track.transform, Palette.Aqua.WithAlpha(0.7f));
            screen._fill = fill.rectTransform;
            screen._fill.anchorMin = new Vector2(0f, 0f);
            screen._fill.anchorMax = new Vector2(0f, 1f);
            screen._fill.pivot = new Vector2(0f, 0.5f);
            screen._fill.anchoredPosition = Vector2.zero;
            screen._fill.sizeDelta = new Vector2(0f, 0f);
            return screen;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Доля готового 0…1. Полоса догоняет её плавно.</summary>
        public void SetProgress(float done) => _target = Mathf.Clamp01(done);

        /// <summary>Растаять поверх собранной игры и убрать себя.</summary>
        public void Hide()
        {
            _hiding = true;
            _group.blocksRaycasts = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _shown = Mathf.MoveTowards(_shown, _target, dt * 3f);
            _fill.sizeDelta = new Vector2(BarWidth * _shown, 0f);
            if (!_hiding) return;
            _group.alpha -= dt / FadeOut;
            if (_group.alpha <= 0f) Destroy(gameObject);
        }
    }
}
