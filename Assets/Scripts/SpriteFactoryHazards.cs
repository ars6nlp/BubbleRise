using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Препятствия: объём, фактура, вариации.
    ///
    /// **Что было.** Каждое препятствие — силуэт, залитый одним цветом, с одной
    /// тенью снизу и одним бликом. Повторяющиеся камни выглядели копиями друг
    /// друга, а разные типы — вырезанными из одного картона.
    ///
    /// **Что стало.** У каждого типа три-четыре вариации силуэта и оттенка,
    /// выбираемые при появлении; объём по общему источнику света; фактура
    /// материала шумом; налёт ракушек и водорослей на твёрдом; подводная дымка к
    /// нижнему краю. Приёмы общие — см. <see cref="Volume"/> и соседей, — поэтому
    /// предметы остаются из одного мира.
    ///
    /// **Разрешение.** 128×128 у мелких и 192×192 у крупных: при игровом размере
    /// 0.21–0.48 юнита и высоте мира 10 юнитов на экране 1920 точек это даёт
    /// 86–197 экранных точек на предмет — текстура не растягивается даже на
    /// плотном экране. 44 текстуры по 192×192 в RGBA32 — это 6.2 МБ: много для
    /// одной подсистемы, но приемлемо даже для среднего телефона, где под
    /// текстуры обычно уходит сотня-другая мегабайт. 256×256 дали бы 11 МБ ради
    /// разницы, которой на таком размере не видно.
    ///
    /// **Хитбоксы не тронуты.** Все формы вписаны в прежний единичный круг;
    /// шипы и выступы торчат только на глаз. Радиус столкновения живёт в
    /// <see cref="GameConfig"/> и о рисунке ничего не знает.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>Сколько вариаций у каждого типа препятствия.</summary>
        public const int HazardVariants = 4;

        /// <summary>
        /// Сколько вариаций у упрощённых типов — камня, ежа, нароста и уголька.
        ///
        /// Меньше, и это осознанно: когда с рисунка убраны поры, трещины и налёт,
        /// четвёртая вариация отличается от третьей уже только оттенком, и разницу
        /// никто не замечает. Три формы, которые видно, лучше четырёх, которые
        /// сливаются.
        /// </summary>
        public const int SimpleVariants = 3;

        /// <summary>Крупным типам текстура побольше: их видно во весь размер.</summary>
        const int BigSize = 192;

        static Color Shift(Color c, float h, float s, float v)
        {
            Color.RGBToHSV(c, out float hh, out float ss, out float vv);
            return Color.HSVToRGB(Mathf.Repeat(hh + h, 1f),
                                  Mathf.Clamp01(ss + s), Mathf.Clamp01(vv + v));
        }

        // ------------------------------------------------------------- камень
        /// <summary>
        /// Камень: чистый силуэт, три тона, один крупный скол, один мягкий блик.
        ///
        /// Прошлая версия несла поры, трещины по шуму и налёт водорослей. На
        /// игровом размере — восемьдесят с небольшим точек — всё это слипается в
        /// рябь: камень выглядит грязным, а не фактурным. Убрано всё мелкое,
        /// оставлено то, что читается с одного взгляда.
        /// </summary>
        public static Sprite RockV(int variant)
        {
            int v = Mathf.Abs(variant) % SimpleVariants;

            // Шесть точек вместо семи-восьми и вдвое меньше неровности: силуэт
            // должен быть узнаваемым куском камня, а не кляксой.
            var poly = RockOutline(6 + v % 2, v * 5 + 1, 0.16f);
            Color body = Shift(Palette.Rock, 0f, -0.04f + v * 0.03f, -0.05f + v * 0.05f);

            // Скол: прямая грань под своим углом у каждой вариации.
            float a = 0.6f + v * 1.9f;
            Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 through = n * (0.28f + v * 0.06f);

            return BuildOutlined("rockv" + v, p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;

                float edge = PolyEdge(p, poly);
                Color c = SimpleVolume(p, edge, body);
                c = Chip(c, p, through, n, 0.26f);
                c = Specular(c, p, new Vector2(-0.3f, 0.34f), 0.42f, 0.22f);
                return Underwater(c, p, 0.1f);
            }, size: BigSize);
        }

        // ---------------------------------------------------------------- ёж
        /// <summary>
        /// Морской ёж: крупные редкие иглы с градиентом от основания к кончику.
        ///
        /// Было от десяти до двадцати пяти игл плюс поры и зернистость по телу. На
        /// экране это превращалось в мохнатый ком: опасность читалась как «что-то
        /// колючее», но форму было не разобрать. Теперь игл шесть-восемь, они
        /// толще и длиннее, а тело гладкое — остриё видно сразу.
        /// </summary>
        public static Sprite UrchinV(int variant)
        {
            int v = Mathf.Abs(variant) % SimpleVariants;

            int spines = 6 + v;                       // 6, 7, 8 — крупные и редкие
            float spineLen = 0.46f - v * 0.03f;
            // Тело светлее прежнего UrchinBody (3A2540, контраст с водой 1.3:1, D-22):
            // 7A5A8A даёт ≥ 2.5:1 к фону колонны на любой глубине (95-й перцентиль
            // L ≤ 0.024 [ТЕСТ]), и ёж читается силуэтом, а не только иглами.
            Color body = Shift(Palette.Hex("7A5A8A"), 0f, -0.05f + v * 0.05f, v * 0.05f);
            Color tip = Shift(Palette.Hex("E8A0C0"), 0f, -0.05f, 0.04f);

            return BuildOutlined("urchinv" + v, p =>
            {
                float d = p.magnitude;
                float a = Mathf.Atan2(p.y, p.x);

                // Игла — конус: ширина убывает к кончику, поэтому степень высокая.
                float wave = Mathf.Cos(a * spines + v * 0.7f);
                float reach = 0.52f + spineLen * Mathf.Pow(Mathf.Max(0f, wave), 1.6f);
                if (d > reach) return Color.clear;

                if (d > 0.52f)
                {
                    // Градиент от основания к кончику — и больше ничего.
                    float t = Mathf.InverseLerp(0.52f, reach, d);
                    Color spine = Color.Lerp(body * 1.3f, tip, Mathf.SmoothStep(0f, 1f, t));
                    spine.a = 1f;
                    return Underwater(spine, p, 0.12f);
                }

                Color c = SimpleVolume(p, d / 0.52f, body);
                c = Specular(c, p, new Vector2(-0.18f, 0.2f), 0.3f, 0.26f);
                return Underwater(c, p, 0.1f);
            }, size: BigSize);
        }

        // ------------------------------------------------------------- коралл

        /// <summary>Коралл: бугристый обломок с сегментами разной яркости.</summary>
        public static Sprite CoralChunkV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            var poly = RockOutline(9, v + 11, 0.24f);
            // Сдвиг тона втрое меньше: при 0.015 на вариацию четвёртый коралл
            // уходил в жёлтый и переставал читаться как коралл. Разнообразие
            // теперь в яркости и насыщенности, а не в цвете.
            Color body = Shift(Palette.CoralBody, v * 0.005f, 0.06f - v * 0.03f, -0.04f + v * 0.045f);
            Color tipc = Shift(Palette.CoralTip, v * 0.005f, 0f, 0.05f);

            return BuildOutlined("coralv" + v, p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;

                float edge = PolyEdge(p, poly);
                Color c = Volume(p, edge, body, 0.9f);

                // Сегменты: бугорки разной яркости, как у настоящего коралла.
                float seg = Wisp(p, 5.5f + v, 7f * v, 3f * v);
                c = Color.Lerp(c, tipc, Mathf.Clamp01(seg) * 0.45f);
                c = Color.Lerp(c, body * 0.72f, Mathf.Clamp01(-seg) * 0.4f);

                c = Grain(c, p, 14f, 0.35f, v + 2);
                c = Crust(c, p, Palette.Hex("A8C8A0"), 6f, 0.22f, v);
                // Блик сверху-слева был запечён в рисунок и крутился вместе с
                // обломком (D-17); блик рисует невращающийся слой Obstacle.
                // Вместо него — пара ракушек: деталь, которой поворот не мешает.
                c = Shell(c, p, new Vector2(0.28f - v * 0.12f, -0.34f + v * 0.08f), 0.1f);
                if (v % 2 == 0) c = Shell(c, p, new Vector2(-0.36f, 0.12f + v * 0.05f), 0.07f);
                return Underwater(c, p);
            }, size: BigSize);
        }

        // ----------------------------------------------------------- вертушка

        /// <summary>
        /// Коралловая вертушка: лопасти с рёбрами и ступицей. Металл не подходит —
        /// это коралл, поэтому фактура пористая, а не полированная.
        /// </summary>
        public static Sprite SpinnerV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            int blades = 3 + v % 3;
            Color body = Shift(Palette.Hex("8FE3C8"), v * 0.02f, 0.05f, -0.05f + v * 0.03f);

            return BuildOutlined("spinnerv" + v, p =>
            {
                float d = p.magnitude;
                if (d > 0.98f) return Color.clear;

                float a = Mathf.Atan2(p.y, p.x) + d * 1.1f;      // изгиб лопасти
                float blade = Mathf.Abs(Mathf.Cos(a * blades * 0.5f));
                float reach = 0.34f + 0.64f * Mathf.Pow(blade, 0.6f);
                bool hub = d < 0.3f;
                if (!hub && d > reach) return Color.clear;

                float edge = hub ? d / 0.3f : Mathf.InverseLerp(0.3f, reach, d);
                Color c = Volume(p, edge, hub ? body * 0.8f : body, 1.1f);

                // Ребро по центру лопасти: по нему читается, что она изогнута.
                if (!hub)
                {
                    float rib = Mathf.Abs(blade - 1f);
                    c = Color.Lerp(c, Color.Lerp(body, Palette.Foam, 0.4f),
                                   Mathf.Clamp01(1f - rib * 9f) * 0.3f);
                }

                c = Grain(c, p, 12f, 0.3f, v + 7);
                c = Crust(c, p, Palette.Hex("B8D8A8"), 5f, 0.2f, v);
                c = Specular(c, p, new Vector2(-0.2f, 0.36f), 0.3f, 0.22f);
                return Underwater(c, p);
            }, size: BigSize);
        }

        // ------------------------------------------------------------ нарост
        /// <summary>
        /// Шипастый нарост: два-три крупных конуса на гладком основании.
        ///
        /// Было три-четыре конуса плюс зернистость, налёт и обводка у каждого —
        /// на размере мелкой угрозы сливалось в пятно. Конусов стало меньше, они
        /// крупнее, основание чистое.
        /// </summary>
        public static Sprite BarnacleV(int variant)
        {
            int v = Mathf.Abs(variant) % SimpleVariants;
            var poly = RockOutline(6, v * 7 + 23, 0.1f);
            Color body = Shift(Palette.Hex("C9B89A"), 0f, -0.05f, -0.03f + v * 0.04f);
            Color inner = Palette.Hex("8F7F66");

            int cones = 2 + v % 2;

            return BuildOutlined("barnaclev" + v, p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;

                float edge = PolyEdge(p, poly);
                Color c = SimpleVolume(p, edge, body);

                // Конус — светлое кольцо с тёмным устьем. Две линии, не десяток.
                for (int i = 0; i < cones; i++)
                {
                    float a = i / (float)cones * Mathf.PI * 2f + v * 1.1f;
                    Vector2 at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.26f;
                    float r = 0.34f;
                    float dd = Vector2.Distance(p, at);
                    if (dd > r) continue;

                    float t = dd / r;
                    Color shell = Color.Lerp(Color.Lerp(body, Palette.Foam, 0.26f), body * 0.7f, t);
                    if (t < 0.28f) shell = Color.Lerp(shell, inner, 1f - t / 0.28f);
                    c = Color.Lerp(c, shell, 0.9f);
                }

                c = Specular(c, p, new Vector2(-0.3f, 0.3f), 0.34f, 0.2f);
                return Underwater(c, p, 0.1f);
            }, size: BigSize);
        }

        // ------------------------------------------------------------- уголёк
        /// <summary>
        /// Раскалённый камень: тёмная корка и ОДНА широкая светящаяся жила.
        ///
        /// Прожилки шли по шуму и давали сетку из десятков тонких линий — издали
        /// просто оранжевое пятно. Жила теперь одна, широкая, с ровным свечением:
        /// видно и что камень, и что раскалённый.
        /// </summary>
        public static Sprite CinderV(int variant)
        {
            int v = Mathf.Abs(variant) % SimpleVariants;
            var poly = RockOutline(6, v * 11 + 31, 0.13f);
            Color crustCol = Shift(Palette.Hex("3A2B26"), 0f, 0f, v * 0.03f);
            Color glow = Shift(Palette.Hex("FF8A4A"), 0f, 0f, -0.04f);

            // Жила: полоса вдоль своего направления у каждой вариации.
            float a = 0.4f + v * 1.1f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 nrm = new Vector2(-dir.y, dir.x);

            return BuildOutlined("cinderv" + v, p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;

                float edge = PolyEdge(p, poly);
                Color c = SimpleVolume(p, edge, crustCol);

                // Расстояние до оси жилы. Ширина плавает вдоль неё, чтобы полоса
                // не выглядела прочерченной по линейке.
                float along = Vector2.Dot(p, dir);
                float across = Mathf.Abs(Vector2.Dot(p, nrm));
                float width = 0.13f + 0.05f * Mathf.Sin(along * 3.1f);

                if (across < width)
                {
                    float k = Mathf.SmoothStep(1f, 0f, across / width);
                    c = Color.Lerp(c, glow, k * 0.85f);
                    if (k > 0.72f) c = Color.Lerp(c, Color.Lerp(glow, Palette.Gold, 0.35f), (k - 0.72f) * 2f);
                }

                return Underwater(c, p, 0.06f);
            }, size: BigSize);
        }

        // -------------------------------------------------------------- якорь

        /// <summary>Якорь: ржавый чугун с заклёпками и потёками.</summary>
        public static Sprite AnchorWeightV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            Color iron = Shift(Palette.Hex("9AA7B4"), v * 0.01f, 0.02f, -0.08f + v * 0.03f);
            Color rust = Palette.Hex("8F5A32");

            return BuildOutlined("anchorv" + v, p =>
            {
                // Трапеция-груз: широкое основание, скошенные бока.
                float half = Mathf.Lerp(0.82f, 0.5f, Mathf.InverseLerp(-0.75f, 0.75f, p.y));
                if (Mathf.Abs(p.x) > half || p.y < -0.78f || p.y > 0.86f) return Color.clear;

                float edge = Mathf.Max(Mathf.Abs(p.x) / half,
                                       Mathf.InverseLerp(0.86f, -0.78f, p.y) * 0.0f
                                       + Mathf.Abs(p.y + 0.0f) / 0.86f);
                Color c = Volume(p, Mathf.Clamp01(edge), iron, 1.05f);

                // Кольцо наверху.
                float ring = Mathf.Abs(new Vector2(p.x, (p.y - 0.72f) * 1.4f).magnitude - 0.16f);
                if (p.y > 0.55f && ring < 0.06f) c = Color.Lerp(iron * 1.2f, Palette.Foam, 0.2f);

                // Заклёпки по бокам.
                for (int i = 0; i < 3; i++)
                {
                    float yy = -0.45f + i * 0.42f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        float dd = Vector2.Distance(p, new Vector2(sx * (half - 0.14f), yy));
                        if (dd < 0.07f)
                            c = Color.Lerp(c, Color.Lerp(iron, Palette.Foam, 0.35f),
                                           Mathf.SmoothStep(1f, 0.2f, dd / 0.07f));
                    }
                }

                c = Grain(c, p, 8f, 0.3f, v + 19);

                // Ржавчина: потёки сверху вниз.
                float n = Wisp(new Vector2(p.x * 2.4f, p.y * 0.7f), 4f, v * 9f, v * 2f);
                c = Color.Lerp(c, rust, Mathf.Clamp01(n - 0.1f) * 0.55f);

                c = Crust(c, p, Palette.Hex("7F9B78"), 6f, 0.25f, v);
                c = Specular(c, p, new Vector2(-0.35f, 0.4f), 0.34f, 0.24f);
                return Underwater(c, p);
            }, size: BigSize);
        }

        // --------------------------------------------------------------- мина

        /// <summary>Глубинная мина: корпус с болтами, шипы-детонаторы и пузырьки.</summary>
        public static Sprite MineV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            int spikes = 5 + v % 3;
            Color shell = Shift(Palette.Hex("3E4A55"), v * 0.01f, 0.02f, v * 0.02f);
            Color warn = Shift(Palette.Hex("E05A4A"), v * 0.012f, 0f, -0.04f);

            return BuildOutlined("minev" + v, p =>
            {
                float d = p.magnitude;
                float a = Mathf.Atan2(p.y, p.x);

                // Шипы-детонаторы наружу.
                // Степень ниже — шип толще у основания и читается как конус,
                // а не как игла.
                float spike = Mathf.Cos(a * spikes + v * 0.6f);
                float reach = 0.7f + 0.3f * Mathf.Pow(Mathf.Max(0f, spike), 1.8f);
                if (d > reach) return Color.clear;

                if (d > 0.7f)
                {
                    float t = Mathf.InverseLerp(0.7f, reach, d);
                    Color sp = Color.Lerp(shell * 1.15f, warn, t);
                    sp.a = 1f;
                    return Underwater(sp, p, 0.1f);
                }

                float edge = d / 0.7f;
                Color c = Volume(p, edge, shell, 1.2f);

                // Пояс и болты по экватору — главная примета мины.
                if (Mathf.Abs(p.y) < 0.1f) c = Color.Lerp(c, warn, 0.5f);
                for (int i = 0; i < 6; i++)
                {
                    float aa = i / 6f * Mathf.PI * 2f;
                    Vector2 at = new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * 0.46f;
                    float dd = Vector2.Distance(p, at);
                    if (dd < 0.08f)
                        c = Color.Lerp(c, Color.Lerp(shell, Palette.Foam, 0.4f),
                                       Mathf.SmoothStep(1f, 0.2f, dd / 0.08f));
                }

                c = Specular(c, p, new Vector2(-0.26f, 0.3f), 0.3f, 0.26f);
                return Underwater(c, p, 0.1f);
            }, size: BigSize);
        }

        // ------------------------------------------------------------ ловушка

        /// <summary>
        /// Биолюминесцентная ловушка: полупрозрачный купол с жилками.
        /// Яркость постоянная — фазой «горит / погасла» управляет Obstacle.
        /// </summary>
        public static Sprite GlowtrapV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            Color bell = Shift(Palette.Hex("7BE8FF"), v * 0.02f, -0.05f, -0.1f + v * 0.04f);

            return BuildOutlined("glowtrapv" + v, p =>
            {
                float d = p.magnitude;
                if (d > 0.96f) return Color.clear;

                // Купол сверху, щупальца снизу.
                bool dome = p.y > -0.15f;
                float bodyR = dome ? 0.9f : 0.9f - 0.3f * Mathf.Abs(p.y + 0.15f);
                if (d > bodyR && dome) return Color.clear;

                if (!dome)
                {
                    // Щупальца: редкие нити.
                    float t = Mathf.Repeat((p.x + 1f) * (3f + v), 1f);
                    if (t > 0.3f || p.y < -0.9f) return Color.clear;
                    float fade = Mathf.InverseLerp(-0.9f, -0.15f, p.y);
                    return Underwater(bell.WithAlpha(0.3f + fade * 0.4f), p, 0.1f);
                }

                float edge = d / bodyR;
                Color c = Volume(p, edge, bell, 0.6f);

                // Жилки: дуги от макушки вниз.
                float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y + 0.4f, p.x) * (4f + v)));
                if (vein > 0.93f) c = Color.Lerp(c, Color.Lerp(bell, Palette.Foam, 0.5f), 0.4f);

                c = Specular(c, p, new Vector2(-0.3f, 0.42f), 0.36f, 0.3f);

                // Купол полупрозрачный: сквозь него видно воду.
                float alpha = Mathf.Lerp(0.95f, 0.65f, Mathf.InverseLerp(0.3f, 1f, edge));
                c.a = alpha;
                return c;
            }, size: BigSize);
        }

        // ------------------------------------------------------------ воронка

        /// <summary>Водоворот: спиральные полосы и тёмное жерло.</summary>
        public static Sprite VortexV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            Color swirl = Shift(Palette.Hex("6E7BE8"), v * 0.02f, 0f, v * 0.03f);

            return BuildOutlined("vortexv" + v, p =>
            {
                float d = p.magnitude;
                if (d > 1f) return Color.clear;

                float a = Mathf.Atan2(p.y, p.x);
                // Спираль: угол сдвигается с радиусом.
                float arm = Mathf.Sin((a + d * (6f + v)) * (2f + v % 2));

                Color c = Color.Lerp(swirl * 0.45f, swirl, Mathf.Clamp01(arm * 0.5f + 0.5f));

                // Жерло: к центру темнеет до почти чёрного.
                c = Color.Lerp(Palette.Deep, c, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.5f, d)));

                // Светлый ободок по краю — вода закручивается.
                if (d > 0.84f)
                    c = Color.Lerp(c, Color.Lerp(swirl, Palette.Foam, 0.45f),
                                   Mathf.InverseLerp(0.84f, 0.97f, d) * 0.6f);

                c = Grain(c, p, 8f, 0.2f, v + 29);
                c.a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.93f, 1f, d));
                return c;
            }, size: BigSize);
        }

        // ------------------------------------------------------------ створки

        /// <summary>Створки раковины: ребристые половины с перламутром внутри.</summary>
        public static Sprite JawV(int variant)
        {
            int v = Mathf.Abs(variant) % HazardVariants;
            Color shell = Shift(Palette.Hex("D8C7A8"), v * 0.008f, -0.02f + v * 0.03f, -0.05f + v * 0.035f);
            Color pearl = Palette.Hex("F0E8DF");

            // Створки на петле слева, открываются вправо. Две симметричные
            // половинки, как было раньше, читались как два одинаковых диска с
            // щелью: не хватало именно петли — того, чем раковина и отличается
            // от двух блюдец.
            Vector2 hinge = new Vector2(-0.82f, 0f);
            float open = 0.16f + v * 0.03f;       // насколько разведены створки
            float span = 1.0f;                    // угловая ширина створки

            return BuildOutlined("jawv" + v, p =>
            {
                Vector2 from = p - hinge;
                float d = from.magnitude;
                if (d > 1.72f) return Color.clear;

                float a = Mathf.Atan2(from.y, from.x);
                float absA = Mathf.Abs(a);
                if (absA < open || absA > open + span) return Color.clear;

                // Край створки: дальше к середине её угла, ближе к концам.
                float t = Mathf.InverseLerp(open, open + span, absA);
                float reach = 1.72f * (0.55f + 0.45f * Mathf.Sin(t * Mathf.PI));
                if (d > reach) return Color.clear;

                float edge = Mathf.Max(d / reach,
                                       1f - Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                Color c = Volume(p, Mathf.Clamp01(edge), shell, 1.05f);

                // Рёбра: веером от петли, как у настоящей раковины.
                float rib = Mathf.Abs(Mathf.Sin((absA - open) / span * Mathf.PI * (6f + v)));
                c = Color.Lerp(c, c * 0.8f, Mathf.Clamp01(rib - 0.5f) * 1.8f);

                // Кольца роста: дуги постоянного радиуса от петли.
                float ring = Mathf.Repeat(d * (4.5f + v * 0.5f), 1f);
                c = Color.Lerp(c, c * 0.9f, Mathf.Clamp01(0.12f - ring) * 4f);

                // Перламутр у самой щели — внутренняя поверхность створки.
                float inner = Mathf.InverseLerp(open + 0.3f, open, absA);
                c = Color.Lerp(c, pearl, inner * 0.55f);

                // Утолщение у петли: там створка толще всего.
                if (d < 0.4f) c = Color.Lerp(c, shell * 0.75f, Mathf.InverseLerp(0.4f, 0.1f, d) * 0.5f);

                c = Grain(c, p, 11f, 0.25f, v + 37);
                c = Crust(c, p, Palette.Hex("8FA87F"), 5f, 0.22f, v);
                c = Specular(c, p, new Vector2(-0.1f, 0.42f), 0.34f, 0.22f);
                return Underwater(c, p);
            }, size: BigSize);
        }

        // --------------------------------------------------------------- пул

        /// <summary>
        /// Прогреть все вариации: 11 видов по 4 штуки плюс две стадии трещин.
        /// Рисуются один раз на загрузке и живут в кэше <see cref="Build"/>,
        /// поэтому ни одного кадра на отрисовку текстуры в бою не тратится.
        /// </summary>
        public static void PrewarmHazards()
        {
            for (int v = 0; v < HazardVariants; v++)
            {
                RockV(v); UrchinV(v); CoralChunkV(v); SpinnerV(v); BarnacleV(v);
                CinderV(v); AnchorWeightV(v); MineV(v); GlowtrapV(v); VortexV(v); JawV(v);
            }
            CracksStage(1); CracksStage(2);
        }

        /// <summary>
        /// Сколько вариаций у этого типа.
        ///
        /// У упрощённых — три, у остальных четыре. Без этого Obstacle тянул бы
        /// номер из четырёх для всех, и у камня нулевая вариация выпадала бы
        /// вдвое чаще прочих: 0 и 3 оба дают ноль после остатка.
        /// </summary>
        public static int VariantsFor(ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Rock:
                case ObstacleType.Urchin:
                case ObstacleType.Barnacle:
                case ObstacleType.Cinder:
                    return SimpleVariants;
                default:
                    return HazardVariants;
            }
        }

        /// <summary>Спрайт препятствия по типу и вариации.</summary>
        public static Sprite Hazard(ObstacleType type, int variant)
        {
            switch (type)
            {
                case ObstacleType.Urchin: return UrchinV(variant);
                case ObstacleType.Coral: return CoralChunkV(variant);
                case ObstacleType.Spinner: return SpinnerV(variant);
                case ObstacleType.Barnacle: return BarnacleV(variant);
                case ObstacleType.Cinder: return CinderV(variant);
                case ObstacleType.Anchor: return AnchorWeightV(variant);
                case ObstacleType.Mine: return MineV(variant);
                case ObstacleType.Glowtrap: return GlowtrapV(variant);
                case ObstacleType.Vortex: return VortexV(variant);
                case ObstacleType.Jaw: return JawV(variant);
                default: return RockV(variant);
            }
        }
    }
}
