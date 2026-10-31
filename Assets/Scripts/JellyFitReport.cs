using System.Text;
using UnityEngine;

namespace BubbleRise
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Помещается ли медуза в кадр на всех форматах экрана.
    ///
    /// **Почему это считается, а не снимается.** Снимок доказывает один формат;
    /// формул же здесь ровно две — ширина игровой колонки и высота зависания, —
    /// и обе чистые. Проверка гоняет их по списку форматов за миллисекунду и
    /// падает на любом, где вид выходит за кромку. Снимки при этом никто не
    /// отменял: они ловят то, чего в формулах нет, — цвет, слои, порядок.
    ///
    /// Зовётся из дев-панели (F1) и из меню редактора.
    /// </summary>
    public static class JellyFitReport
    {
        /// <summary>Форматы: портретные и альбомный, от самого узкого до самого широкого.</summary>
        public static readonly (string Name, float Aspect)[] Formats =
        {
            ("21:9 портрет", 9f / 21f),
            ("19.5:9 портрет", 9f / 19.5f),
            ("16:9 портрет", 9f / 16f),
            ("3:4 портрет", 3f / 4f),
            ("16:9 альбом", 16f / 9f),
            ("21:9 альбом", 21f / 9f),
        };

        /// <summary>Отчёт строкой. Вторым значением — всё ли влезает.</summary>
        public static string Run() => Run(out _);

        public static string Run(out bool allFit)
        {
            // GameConfig — компонент на объекте Game, а не ассет: в сцене он
            // уже есть, а без сцены заводим временный, чтобы взять значения по
            // умолчанию. Проверка обязана работать и без Play Mode.
            var cfg = Object.FindFirstObjectByType<GameConfig>();
            GameObject temp = null;
            if (cfg == null)
            {
                temp = new GameObject("JellyFitProbe");
                temp.hideFlags = HideFlags.HideAndDontSave;
                cfg = temp.AddComponent<GameConfig>();
            }

            try
            {
                return Build(cfg, out allFit);
            }
            finally
            {
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        static string Build(GameConfig cfg, out bool allFit)
        {

            float halfHeight = cfg.worldHeight * 0.5f;
            var sb = new StringBuilder("[медуза] посадка по форматам\n");
            allFit = true;

            foreach (var (name, aspect) in Formats)
            {
                float play = JellyFit.PlayHalfWidth(halfHeight, aspect, cfg.designAspect);
                float column = play * 2f;
                float bell = column * cfg.jellyBellSpan;
                float field = column * cfg.jellyFieldSpan;
                float fieldSpan = bell > 0.01f ? field / bell : 1f;
                float hover = halfHeight - cfg.worldHeight * cfg.jellyHoverFactor;

                bool fits = JellyFit.Fits(halfHeight, bell, JellyBossController.HazeSpanConst,
                                          fieldSpan, hover, JellyTentacles.Links,
                                          out float top, out float bottom);

                JellyFit.Solve(halfHeight, bell, JellyBossController.HazeSpanConst, fieldSpan,
                               hover, JellyTentacles.Links, out float y, out float fit);

                if (!fits) allFit = false;

                sb.AppendFormat(
                    "  {0,-16} купол {1:0.00}  вид {2:0.00}…{3:0.00}  экран ±{4:0.00}  " +
                    "высота {5:0.00}  ужим x{6:0.00}  {7}\n",
                    name, bell, bottom, top, halfHeight, y, fit, fits ? "влезает" : "НЕ ВЛЕЗАЕТ");
            }

            sb.Append(allFit ? "  итог: влезает на всех форматах"
                             : "  итог: ЕСТЬ ФОРМАТЫ, ГДЕ НЕ ВЛЕЗАЕТ");
            return sb.ToString();
        }
    }
#endif
}
