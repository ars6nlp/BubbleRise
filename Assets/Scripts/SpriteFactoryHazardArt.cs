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
    /// * **Один источник света.** Сверху-слева, у всех. Это единственное, что
    ///   превращает набор силуэтов в объёмные предметы на одной сцене.
    /// * **Тень снизу-справа и отражённый свет по нижнему краю.** Второе важнее,
    ///   чем кажется: без него тень читается как грязь, а не как объём.
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
        /// <summary>Направление на источник света. Сверху-слева, одно на всю игру.</summary>
        static readonly Vector2 LightDir = new Vector2(-0.55f, 0.84f).normalized;

        /// <summary>Цвет подводной дымки — ею объекты «садятся» в воду.</summary>
        static readonly Color Haze = Palette.Hex("4E8FA0");

        /// <summary>
        /// Объёмная заливка: свет сверху-слева, тень снизу-справа, отражённый свет
        /// по нижнему краю и тёмная обводка по контуру.
        /// </summary>
        /// <param name="p">Точка в нормализованных координатах.</param>
        /// <param name="edge">Расстояние до края формы: 0 в центре, 1 на контуре.</param>
        /// <param name="baseColor">Собственный цвет материала.</param>
        /// <param name="relief">Насколько выражен объём. 1 — обычный камень.</param>
        static Color Volume(Vector2 p, float edge, Color baseColor, float relief = 1f)
        {
            // Нормаль полусферы: в центре смотрит на зрителя, у края — наружу.
            float h = Mathf.Sqrt(Mathf.Max(0f, 1f - edge * edge));
            Vector2 n = p.sqrMagnitude > 0.0001f ? p.normalized : Vector2.up;

            float lambert = Vector2.Dot(n, LightDir) * (1f - h) + h * 0.55f;
            lambert = Mathf.Clamp01(0.5f + lambert * 0.5f);

            Color lit = Color.Lerp(baseColor * (1f - 0.42f * relief),
                                   Color.Lerp(baseColor, Palette.Foam, 0.3f * relief),
                                   lambert);

            // Отражённый свет: вода подсвечивает нижний край снизу-справа.
            float bounce = Mathf.Clamp01(Vector2.Dot(n, -LightDir)) * edge * edge;
            lit = Color.Lerp(lit, Color.Lerp(baseColor, Haze, 0.55f), bounce * 0.3f * relief);

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
            // Градиент по направлению на свет: одна величина, один переход.
            float lit = Mathf.Clamp01(0.5f + Vector2.Dot(p.normalized, LightDir) * 0.5f);
            if (p.sqrMagnitude < 0.0001f) lit = 0.75f;

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
        /// Подводная дымка: к нижнему краю предмет слегка уходит в бирюзу.
        /// Слабая — иначе препятствие перестаёт отличаться от фона.
        /// </summary>
        static Color Underwater(Color c, Vector2 p, float strength = 0.12f)
            => Color.Lerp(c, Haze, Mathf.InverseLerp(0.6f, -1f, p.y) * strength);

        /// <summary>
        /// Мягкий блик. Статичный и неяркий: просили без пересвета, а движущийся
        /// блик на падающем камне читался бы как мерцание.
        /// </summary>
        static Color Specular(Color c, Vector2 p, Vector2 at, float radius, float power = 0.22f)
        {
            float d = Vector2.Distance(p, at);
            if (d > radius) return c;
            float k = Mathf.SmoothStep(1f, 0f, d / radius);
            return Color.Lerp(c, Color.Lerp(c, Palette.Foam, 0.85f), k * power);
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
            float top = Mathf.InverseLerp(-0.4f, 0.9f, p.y);
            float k = Mathf.Clamp01((n - 0.18f) * 2.4f) * top * amount;
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
