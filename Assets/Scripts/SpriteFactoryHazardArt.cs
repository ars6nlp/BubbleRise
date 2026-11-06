using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Объём, свет и фактура препятствий.
    ///
    /// **Зачем отдельный набор.** Препятствия рисовались каждое по-своему: заливка
    /// одним цветом, одна тень снизу, один блик. Читалось это как плоские фигурки,
    /// и главное — разные предметы выглядели сделанными из разного ничего. Здесь
    /// собраны общие приёмы, чтобы камень, якорь и мина были из одного мира:
    ///
    /// * **Один источник света — в мире, а не в рисунке (D-17).** Препятствия
    ///   поворачиваются и вращаются, и свет, запечённый «сверху-слева» в спрайт,
    ///   приходил у каждого со своей стороны — обломки выглядели наклейками.
    ///   Поэтому заливка здесь одинакова при любом повороте (купол: середина
    ///   светлее, край темнее), а блик сверху-слева рисует Obstacle отдельным
    ///   невращающимся слоем.
    /// * **Отражённый свет по краю.** Вода подсвечивает кромку со всех сторон:
    ///   без этого край читается как грязь, а не как объём.
    /// * **Обводка чуть темнее своего цвета, а не чёрная.** Чёрный контур на
    ///   тёмной воде превращает предмет в дырку.
    /// * **Фактура шумом.** Тот же value-noise, что у материалов скинов, — поры,
    ///   трещины, налёт. Без неё градиент выглядит пластиковым.
    /// * **Подводная дымка.** Лёгкая бирюза к нижнему краю: предмет в воде, а не
    ///   на столе.
    ///
    /// Всё это функции от точки, как и остальная графика в проекте. Текстуры
    /// рисуются один раз на загрузке и кэшируются — см. <see cref="Prewarm"/>.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Где на препятствии лежит блик — в мире, сверху-слева, в долях радиуса.
        /// Его рисует невращающийся слой Obstacle; рисунки угрозы света не несут.
        /// </summary>
        public static readonly Vector2 HazardGlintOffset = new Vector2(-0.22f, 0.25f);

        /// <summary>Цвет подводной дымки — ею объекты «садятся» в воду.</summary>
        static readonly Color Haze = Palette.Hex("4E8FA0");

        /// <summary>
        /// Объёмная заливка без направления: купол — середина светлее, край
        /// темнее, отражённый свет воды по всей кромке и тёмная обводка. При
        /// любом повороте рисунок одинаков (D-17).
        /// </summary>
        /// <param name="p">Точка в нормализованных координатах.</param>
        /// <param name="edge">Расстояние до края формы: 0 в центре, 1 на контуре.</param>
        /// <param name="baseColor">Собственный цвет материала.</param>
        /// <param name="relief">Насколько выражен объём. 1 — обычный камень.</param>
        static Color Volume(Vector2 p, float edge, Color baseColor, float relief = 1f)
        {
            // Купол: высота полусферы — от неё и свет, без направления.
            float h = Mathf.Sqrt(Mathf.Max(0f, 1f - edge * edge));
            float lambert = Mathf.Clamp01(0.3f + h * 0.55f);

            Color lit = Color.Lerp(baseColor * (1f - 0.42f * relief),
                                   Color.Lerp(baseColor, Palette.Foam, 0.3f * relief),
                                   lambert);

            // Отражённый свет: вода подсвечивает кромку со всех сторон.
            float bounce = edge * edge * edge;
            lit = Color.Lerp(lit, Color.Lerp(baseColor, Haze, 0.55f), bounce * 0.18f * relief);

            // Обводка: темнее своего цвета, но не чёрная.
            if (edge > 0.88f)
            {
                float k = Mathf.InverseLerp(0.88f, 1f, edge);
                lit = Color.Lerp(lit, baseColor * 0.45f, k * 0.75f);
            }

            lit.a = 1f;
            return lit;
        }

        /// <summary>
        /// Плоско-объёмная заливка: мягкий градиент сверху-слева вниз-направо,
        /// тень снизу-справа, обводка чуть темнее основного цвета. Без шума, без
        /// отражённого света, без мелких деталей.
        ///
        /// Отдельно от <see cref="Volume"/> намеренно. Та версия даёт настоящий
        /// объём с подсветкой нижнего края и просит сверху фактуру — на камне и
        /// шипах всё это складывалось в рябь, которая на игровом размере читается
        /// как грязь, а не как материал. Здесь ровно три тона и один блик: силуэт
        /// остаётся чистым, а предмет всё равно не плоский.
        /// </summary>
        static Color SimpleVolume(Vector2 p, float edge, Color baseColor)
        {
            // Купол без направления: середина светлее, к краю темнее (D-17).
            float lit = Mathf.Clamp01(1f - edge * 0.85f);

            Color dark = baseColor * 0.62f;
            Color light = Color.Lerp(baseColor, Palette.Foam, 0.22f);
            Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, lit));

            // Обводка: темнее своего цвета, но не чёрная.
            if (edge > 0.9f)
                c = Color.Lerp(c, baseColor * 0.4f, Mathf.InverseLerp(0.9f, 1f, edge) * 0.8f);

            c.a = 1f;
            return c;
        }

        /// <summary>
        /// Скол: прямая грань, отсекающая часть формы и подсвеченная иначе.
        ///
        /// Один крупный скол читается как «камень откололся», десяток мелких
        /// трещин — как шум. Поэтому не шум, а прямая: у настоящего скола край
        /// ровный, его сделал удар.
        /// </summary>
        static Color Chip(Color c, Vector2 p, Vector2 through, Vector2 normal, float lighten)
        {
            float side = Vector2.Dot(p - through, normal);
            if (side < 0f) return c;

            // Сам край скола — тонкая тёмная линия, дальше плоская грань.
            if (side < 0.05f) return Color.Lerp(c, c * 0.55f, Mathf.SmoothStep(1f, 0f, side / 0.05f));
            return Color.Lerp(c, Color.Lerp(c, Palette.Foam, 0.3f), lighten);
        }

        /// <summary>
        /// Подводная дымка: к краю предмет слегка уходит в бирюзу — со всех сторон,
        /// иначе дымка «снизу» вращалась бы вместе с обломком (D-17). Слабая —
        /// иначе препятствие перестаёт отличаться от фона.
        /// </summary>
        static Color Underwater(Color c, Vector2 p, float strength = 0.12f)
            => Color.Lerp(c, Haze, Mathf.InverseLerp(0.45f, 1f, p.magnitude) * strength);

        /// <summary>
        /// Мягкий отсвет в середине формы. Раньше здесь был блик в точке
        /// <paramref name="at"/> сверху-слева — он вращался вместе с обломком
        /// (D-17). Теперь блик рисует невращающийся слой Obstacle, а в рисунке
        /// остаётся только центральный отсвет: при любом повороте одинаковый.
        /// Место <paramref name="at"/> задаёт лишь его размах.
        /// </summary>
        static Color Specular(Color c, Vector2 p, Vector2 at, float radius, float power = 0.22f)
        {
            float r = radius + at.magnitude * 0.5f;
            float d = p.magnitude;
            if (d > r) return c;
            float k = Mathf.SmoothStep(1f, 0f, d / r);
            return Color.Lerp(c, Color.Lerp(c, Palette.Foam, 0.6f), k * power * 0.5f);
        }

        /// <summary>Поры и зернистость: мелкий шум по поверхности.</summary>
        static Color Grain(Color c, Vector2 p, float scale, float amount, int seed = 0)
        {
            float n = Wisp(p, scale, seed * 3.1f, seed * 7.7f);
            return Color.Lerp(c, n > 0f ? c * 1.16f : c * 0.84f, Mathf.Abs(n) * amount);
        }

        /// <summary>
        /// Трещина: тёмная линия по шуму. Не отрезок — отрезок читается как
        /// царапина от инструмента, а не как разлом камня.
        /// </summary>
        static Color Crack(Color c, Vector2 p, float scale, float width, int seed = 0)
        {
            float n = Wisp(p, scale, seed * 11.3f, seed * 5.9f);
            float d = Mathf.Abs(n);
            if (d > width) return c;
            return Color.Lerp(c, c * 0.42f, Mathf.SmoothStep(1f, 0f, d / width));
        }

        /// <summary>
        /// Налёт: ракушки и водоросли пятнами. Садится сверху, как и положено
        /// тому, что оседает из воды.
        /// </summary>
        static Color Crust(Color c, Vector2 p, Color crust, float scale, float amount, int seed = 0)
        {
            float n = Wisp(p, scale, 17f + seed * 2.3f, 29f + seed * 4.1f);
            // пятнами по всей форме: «сверху» у вращающегося обломка нет (D-17)
            float k = Mathf.Clamp01((n - 0.18f) * 2.4f) * 0.7f * amount;
            return Color.Lerp(c, crust, k);
        }

        /// <summary>
        /// Расстояние до края многоугольника в долях: 0 в центре, 1 на контуре.
        /// Нужно <see cref="Volume"/>, чтобы знать, где гнуть нормаль.
        /// </summary>
        static float PolyEdge(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 ab = b - a;
                float len = ab.sqrMagnitude;
                float t = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }

            // Нормируем на «средний радиус» формы: точный путь дорог, а разница
            // на глаз неразличима.
            float reach = 0f;
            for (int i = 0; i < poly.Length; i++) reach += poly[i].magnitude;
            reach /= poly.Length;

            return Mathf.Clamp01(1f - best / Mathf.Max(0.05f, reach));
        }

        /// <summary>
        /// Случайный, но устойчивый многоугольник — силуэт камня или обломка.
        /// Вариация задаётся номером: один и тот же номер даёт одну и ту же форму.
        /// </summary>
        static Vector2[] RockOutline(int points, int variant, float rough = 0.2f)
        {
            var poly = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float a = i / (float)points * Mathf.PI * 2f;
                float r = 0.78f + rough * Mathf.Sin(i * 2.1f + variant * 1.7f)
                          + rough * 0.4f * Mathf.Sin(i * 3.7f + variant * 2.9f);
                poly[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return poly;
        }

        /// <summary>
        /// Трещины по стадии повреждения.
        ///
        /// Две стадии вместо одной: по прочному препятствию страж бьёт дважды или
        /// трижды, и «целое / разбито» не отвечает на вопрос, сколько осталось.
        /// </summary>
        public static Sprite CracksStage(int stage)
        {
            int count = stage <= 1 ? 4 : 8;
            float width = stage <= 1 ? 0.055f : 0.075f;

            return Build("cracks" + stage, p =>
            {
                float d = p.magnitude;
                if (d > 0.95f) return Color.clear;

                float best = 1f;
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count * Mathf.PI * 2f + i * 0.37f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    // Ломаная: середина луча отведена вбок, иначе выходит трафарет.
                    Vector2 mid = dir * 0.45f + new Vector2(-dir.y, dir.x) * (0.12f * Mathf.Sin(i * 2.3f));
                    best = Mathf.Min(best, SegmentDistance(p, Vector2.zero, mid));
                    best = Mathf.Min(best, SegmentDistance(p, mid, dir * 0.92f));
                }

                if (best > width) return Color.clear;
                float k = Mathf.SmoothStep(1f, 0f, best / width);
                return new Color(0.04f, 0.05f, 0.07f, k * (stage <= 1 ? 0.6f : 0.82f));
            });
        }
    }
}
