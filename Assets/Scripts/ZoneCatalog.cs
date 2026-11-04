using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Вид локации. Порядок — номер в каталоге и в DevSpawnZone.</summary>
    public enum ZoneKind { Ruins, Ice, Wreck, Ribs, Garden }

    /// <summary>
    /// Свет и туман локации: к ним плавно (ThemeFade, ≥ 1.5 с) уходит вид воды глубины,
    /// пока локация на экране. Weight 0 — свет глубины не трогается (руины, лёд).
    /// </summary>
    public struct ZoneLight
    {
        public float Weight;
        public Color EdgeLight, Fog, SnowTint;
        public float Shafts, Caustics, Snow;
    }

    /// <summary>
    /// Шаблон расстановки шаров. Точки — x относительно середины прохода, y
    /// относительно слота, радиус в z. lineX — где в этих координатах линия
    /// пузырька: шаблон может нарочно оставить над ней окно.
    /// </summary>
    public delegate void ZonePatternFn(List<Vector3> points, ZoneShapeCtx ctx);

    public struct ZonePattern
    {
        public string Name;
        public int Weight;
        /// <summary>
        /// Шаблон сам встаёт по ширине прохода (ряды, груз у борта) — сдвиг не
        /// подбирается. Иначе подбирается, как у прежних композиций (PickOffset).
        /// </summary>
        public bool Anchored;
        public ZonePatternFn Build;
    }

    /// <summary>Что знает шаблон о месте, где встаёт.</summary>
    public struct ZoneShapeCtx
    {
        /// <summary>Полуширина прохода и где в нём линия пузырька.</summary>
        public float HalfWidth, LineX;
        /// <summary>Радиусы шаров из конфига.</summary>
        public float Small, Big;
        /// <summary>Самое узкое окно в ряду: 1.5 диаметра стража.</summary>
        public float MinWindow;
        /// <summary>Номер слота: чётные и нечётные ребра чередуют изгиб.</summary>
        public int Slot;
        /// <summary>Доля рядов, у которых окно приходится на путь пузырька (ZoneDef.OpenChance).</summary>
        public float OpenChance;
    }

    /// <summary>
    /// Запись о локации — всё в одном месте: вид, длина, вес в очереди, защита перед
    /// боссом, форма прохода, шаблоны угроз, свет. Новая локация — новая запись (и
    /// её рисунки в SpriteFactoryZones / ZoneDressingShaped).
    /// </summary>
    public sealed class ZoneDef
    {
        public ZoneKind Kind;
        /// <summary>Для логов и дев-панели.</summary>
        public string Key;
        /// <summary>Руины и лёд — прежний путь ZoneSpawner (FillContent, профили, темы руин).</summary>
        public bool Legacy;
        /// <summary>Длина, юн. при zoneHeight = 38 (масштабируется вместе с ним).</summary>
        public float Length = 38f;
        /// <summary>Сколько раз вид входит в круг очереди (круг — все виды по весу, без повторов подряд).</summary>
        public int Weight = 1;
        /// <summary>Не начинать локацию ближе, чем за столько метров до отметки босса.</summary>
        public float BossGuardMeters = 80f;
        public ZoneTheme Theme;
        /// <summary>Полуширина прохода в долях PlayHalfWidth по высоте t (0 — вход, 1 — выход).</summary>
        public Func<float, float> HalfWidth;
        /// <summary>Середина прохода в долях PlayHalfWidth: несимметричные стены (диагональ корабля).</summary>
        public Func<float, float> Shift;
        /// <summary>Стены: false — открытая вода, стен нет (сад).</summary>
        public bool Walls = true;
        /// <summary>Шаг слотов композиций, юн.</summary>
        public float SlotSpacing = 5.5f;
        /// <summary>
        /// Ручки нагрузки на линию пузырька. OpenChance — доля рядов (палубы, рёбра),
        /// у которых окно ложится прямо на путь: передышка. LineBias — доля свободных
        /// шаблонов (острова, мачты), которые ставятся так, чтобы задеть путь хотя бы
        /// одним шаром (но не больше MaxCenterBlocks). Подобраны прогоном ZoneSim:
        /// нагрузка — 60–100 % от прежних локаций (docs/ZONES_REPORT.md).
        /// </summary>
        public float OpenChance = 0.25f, LineBias = 0.7f;
        public ZonePattern[] Patterns;
        public ZoneLight Light;
    }

    public static class ZoneCatalog
    {
        static Color Hex(string h) => Palette.Hex(h);

        public static readonly ZoneDef[] All =
        {
            new ZoneDef { Kind = ZoneKind.Ruins, Key = "ruins", Legacy = true },
            new ZoneDef { Kind = ZoneKind.Ice, Key = "ice", Legacy = true },

            // ---------------------------------------------------------- корабль
            new ZoneDef
            {
                Kind = ZoneKind.Wreck, Key = "wreck", Length = 34f, SlotSpacing = 5.0f, OpenChance = 0.2f, LineBias = 0.8f,
                Theme = new ZoneTheme
                {
                    NameKey = "zone.wreck",
                    WaterTop = Hex("0E2420"), WaterBottom = Hex("07150F"),
                    WallDeep = Hex("12110C"), Wall = Hex("2C281C"), Crust = Hex("4C4430"),
                    BlobTint = Hex("F2E6D6"), Banner = Hex("A89A6A"),
                    Style = WallStyle.Wood, Back = Biome.Ruins, CrustPiece = WorldPiece.CrustRock,
                    GatePost = WorldPiece.ColumnA, GateArch = false,
                },
                // корпус лежит наискось: середина прохода уходит волной на ±0.25 поля
                HalfWidth = t => 0.78f,
                Shift = t => 0.25f * Mathf.Sin(Mathf.PI * (1.6f * t - 0.3f)),
                Patterns = new[]
                {
                    new ZonePattern { Name = "deck", Weight = 3, Anchored = true, Build = ZoneShapes.Deck },
                    new ZonePattern { Name = "mast", Weight = 2, Build = ZoneShapes.Mast },
                    new ZonePattern { Name = "planks", Weight = 2, Anchored = true, Build = ZoneShapes.Planks },
                    new ZonePattern { Name = "cargo", Weight = 1, Anchored = true, Build = ZoneShapes.Cargo },
                },
                Light = new ZoneLight
                {
                    // мутная оливковая вода, лучи через пробоины, снега мало
                    Weight = 1f, EdgeLight = Hex("2A3E32"), Fog = Hex("0E1A14"), SnowTint = Hex("8A8A72"),
                    Shafts = 0.35f, Caustics = 0.08f, Snow = 0.35f,
                },
            },

            // ---------------------------------------------------------- рёбра кита
            new ZoneDef
            {
                Kind = ZoneKind.Ribs, Key = "ribs", Length = 36f, SlotSpacing = 4.8f, OpenChance = 0.45f,
                Theme = new ZoneTheme
                {
                    NameKey = "zone.ribs",
                    WaterTop = Hex("101C2A"), WaterBottom = Hex("070C16"),
                    WallDeep = Hex("141820"), Wall = Hex("2A3038"), Crust = Hex("7E7868"),
                    BlobTint = Hex("ECE4D6"), Banner = Hex("B8B0A0"),
                    Style = WallStyle.Bone, Back = Biome.Abyss, CrustPiece = WorldPiece.CrustSand,
                    GatePost = WorldPiece.ColumnA, GateArch = false,
                },
                // туша: узко у пасти и хвоста, шире всего посередине
                HalfWidth = t => 0.78f + 0.22f * Mathf.Sin(Mathf.PI * t),
                Shift = t => 0f,
                Patterns = new[]
                {
                    new ZonePattern { Name = "rib", Weight = 4, Anchored = true, Build = ZoneShapes.Rib },
                    new ZonePattern { Name = "double", Weight = 2, Anchored = true, Build = ZoneShapes.DoubleRib },
                    new ZonePattern { Name = "narrow", Weight = 2, Anchored = true, Build = ZoneShapes.NarrowRib },
                    new ZonePattern { Name = "spine", Weight = 1, Build = ZoneShapes.Spine },
                },
                Light = new ZoneLight
                {
                    // глухая серо-синяя вода, свет сверху едва заметен, густой «китопад»
                    Weight = 1f, EdgeLight = Hex("1E2C40"), Fog = Hex("0A111A"), SnowTint = Hex("9AA2AA"),
                    Shafts = 0.12f, Caustics = 0f, Snow = 0.95f,
                },
            },

            // ---------------------------------------------------------- парящий сад
            new ZoneDef
            {
                Kind = ZoneKind.Garden, Key = "garden", Length = 32f, SlotSpacing = 5.6f, Walls = false, LineBias = 0.5f,
                Theme = new ZoneTheme
                {
                    NameKey = "zone.garden",
                    WaterTop = Hex("0B2626"), WaterBottom = Hex("061616"),
                    WallDeep = Hex("0A1414"), Wall = Hex("1A2A28"), Crust = Hex("3D6A5A"),
                    BlobTint = Hex("E6F4EC"), Banner = Hex("6FC0A8"),
                    Style = WallStyle.Rock, Back = Biome.Shallows, CrustPiece = WorldPiece.CrustRock,
                    GatePost = WorldPiece.ColumnA, GateArch = false,
                },
                // открытая вода во весь столбец, стен нет
                HalfWidth = t => 1f,
                Shift = t => 0f,
                Patterns = new[]
                {
                    new ZonePattern { Name = "island", Weight = 3, Build = ZoneShapes.Island },
                    new ZonePattern { Name = "twins", Weight = 2, Build = ZoneShapes.Twins },
                    new ZonePattern { Name = "stones", Weight = 2, Build = ZoneShapes.Stones },
                    new ZonePattern { Name = "atoll", Weight = 2, Build = ZoneShapes.Atoll },
                },
                Light = new ZoneLight
                {
                    // тёплая тёмная бирюза, мягкие лучи и каустики, снега почти нет
                    Weight = 1f, EdgeLight = Hex("24584E"), Fog = Hex("0C1E1C"), SnowTint = Hex("8EB4A6"),
                    Shafts = 0.5f, Caustics = 0.3f, Snow = 0.2f,
                },
            },
        };

        public static ZoneDef Get(ZoneKind kind)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Kind == kind) return All[i];
            return All[0];
        }

        public static int Count => All.Length;
    }

    /// <summary>
    /// Шаблоны угроз новых локаций — из тех же шаров локации. Ни один не ставит
    /// «две стены по бокам и проход посередине»: палубы и рёбра — ряды во всю ширину
    /// с окном, мачты — косые цепочки, острова — сгустки сбоку от пути.
    /// Случайность — UnityEngine.Random, как у прежних композиций.
    /// </summary>
    public static class ZoneShapes
    {
        static float R(float a, float b) => UnityEngine.Random.Range(a, b);

        /// <summary>
        /// Ряд во всю ширину прохода с окном: шары по дуге (bend > 0 — горбом вверх)
        /// с шагом 2.1 r, не дальше reach от пути (дальше ряд несёт только кость или
        /// доска оформления). Сетка привязана к пути пузырька: если окно не на пути, над
        /// пузырьком ровно один шар ряда — соседи дальше полосы r + 0.3, и на любом
        /// аспекте ряд задевает путь одинаково. Окно шириной window с серединой
        /// windowX: шары, задевающие окно, не ставятся.
        /// </summary>
        static void Row(List<Vector3> pts, float half, float lineX, float y, float r, float windowX, float window, float bend,
                        float reach = 99f)
        {
            float step = r * 2.1f;
            int k0 = Mathf.CeilToInt((-half + r - lineX) / step), k1 = Mathf.FloorToInt((half - r - lineX) / step);
            for (int k = k0; k <= k1; k++)
            {
                float x = lineX + k * step;
                if (Mathf.Abs(x - lineX) > reach) continue;
                if (x + r > windowX - window * 0.5f && x - r < windowX + window * 0.5f) continue;
                float u = half > 0.01f ? x / half : 0f;
                pts.Add(new Vector3(x, y + bend * (1f - u * u), r));
            }
        }

        /// <summary>
        /// Где поставить окно ряда. С долей OpenChance — прямо на пути пузырька
        /// (передышка: ряд не задевает пузырёк). Иначе — в стороне, не ближе шара от
        /// пути: над пузырьком остаётся шар ряда, и его надо пробить стражем.
        /// </summary>
        static float WindowAt(ZoneShapeCtx c, float window, float r)
        {
            float lim = Mathf.Max(0f, c.HalfWidth - window * 0.5f - 0.1f);
            if (UnityEngine.Random.value < c.OpenChance)
                return Mathf.Clamp(c.LineX + R(-0.2f, 0.2f), -lim, lim);
            // окно не задевает шар на пути и целиком внутри прохода (вылезшее за стену
            // окно оставляло в проходе меньше 1.5 диаметра стража [ТЕСТ, ZoneSim])
            float keep = window * 0.5f + r + 0.05f;
            float edge = c.HalfWidth - window * 0.5f;
            float left = Mathf.Max(0f, c.LineX - keep + edge), right = Mathf.Max(0f, edge - c.LineX - keep);
            if (left + right < 0.01f)   // проход узок — окно у дальнего борта, хоть и заденет путь
                return Mathf.Clamp(c.LineX + (c.LineX > 0f ? -keep : keep), -edge, edge);
            float u = R(0f, left + right);
            return u < left ? -edge + u : c.LineX + keep + (u - left);
        }

        // ------------------------------------------------------------ корабль

        /// <summary>Палуба: ряд досок-шаров во всю ширину с пробоиной.</summary>
        public static void Deck(List<Vector3> pts, ZoneShapeCtx c)
        {
            float r = Mathf.Lerp(c.Small, c.Big, 0.15f);
            float window = Mathf.Max(c.MinWindow, R(1.0f, 1.6f));
            Row(pts, c.HalfWidth, c.LineX, 0f, r, WindowAt(c, window, r), window, 0f);
        }

        /// <summary>Мачта: косая цепочка через весь проход, на двух третях — рея поперёк.</summary>
        public static void Mast(List<Vector3> pts, ZoneShapeCtx c)
        {
            float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float r = c.Small;
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                pts.Add(new Vector3(dir * (k - 0.5f) * c.HalfWidth * 1.7f, (k - 0.5f) * 3.6f, r));
            }
            // рея: три шара поперёк мачты, по ту сторону от линии пузырька
            float yardY = 0.75f;
            float yardX = dir * (0.75f - 0.5f) * c.HalfWidth * 1.7f;
            for (int i = -1; i <= 1; i++)
            {
                if (i == 0) continue;
                pts.Add(new Vector3(yardX + i * r * 2.4f, yardY, r));
            }
        }

        /// <summary>
        /// Доски: два обломка палубы от разных бортов вразбежку — проход змейкой.
        /// Нижний — от ближнего к пути борта, заходит за путь пузырька на полшага;
        /// верхний — от дальнего, обрывается, не доходя до пути на окно. Пробить
        /// нижний — и дальше свободно; просвет у обоих — к дальнему борту.
        /// </summary>
        public static void Planks(List<Vector3> pts, ZoneShapeCtx c)
        {
            float r = Mathf.Lerp(c.Small, c.Big, 0.1f);
            float step = r * 2.1f;
            float side = Mathf.Abs(c.LineX) > 0.05f ? Mathf.Sign(c.LineX)
                       : UnityEngine.Random.value < 0.5f ? -1f : 1f;
            // нижний: от борта side до пути и на шаг за него
            float wall = side * (c.HalfWidth - r);
            float reach = c.LineX - side * step * 0.6f;
            for (float x = wall; side * (x - reach) >= 0f; x -= side * step)
                pts.Add(new Vector3(x, -0.85f, r));
            // верхний: от другого борта, конец — за окно до пути
            float wall2 = -side * (c.HalfWidth - r);
            float stop = c.LineX - side * (c.MinWindow * 0.5f + r);
            for (float x = wall2; -side * (x - stop) >= 0f; x += side * step)
                pts.Add(new Vector3(x, 0.85f, r));
        }

        /// <summary>
        /// Груз в трюме: до трёх ящиков-шаров в ряд у борта подальше от пути, чуть
        /// вразнобой; до пути не доходит на окно.
        /// Шары — вширь, не стопкой: шар, проезжающий плитку экрана, даёт перепад яркости ~0.14,
        /// и два-три шара в одном столбце за секунду — уже ≥ 3 перепада в секунду
        /// (flash_analyze, «на грани» [ТЕСТ, nzf_thr]).
        /// </summary>
        public static void Cargo(List<Vector3> pts, ZoneShapeCtx c)
        {
            float side = c.LineX > 0f ? -1f : 1f;   // к борту подальше от линии пузырька
            float edge = side * c.HalfWidth;          // внешний край следующего ящика
            for (int i = 0; i < 3; i++)
            {
                float r = R(c.Big * 0.75f, c.Big);
                float x = edge - side * r;
                // до пути — не ближе окна: груз — передышка, а не стена поперёк прохода
                if (i > 0 && side * (x - c.LineX) - r < c.MinWindow) break;
                pts.Add(new Vector3(x, R(-0.2f, 0.2f), r));
                edge = x - side * (r + R(0.05f, 0.3f));
            }
        }

        // ------------------------------------------------------------ рёбра кита

        /// <summary>Ребро: дуга во весь проход с окном; изгиб чередуется от слота к слоту.</summary>
        public static void Rib(List<Vector3> pts, ZoneShapeCtx c)
        {
            float r = Mathf.Lerp(c.Small, c.Big, 0.2f);
            float window = Mathf.Max(c.MinWindow, R(1.1f, 1.8f));
            float bend = (c.Slot % 2 == 0 ? 1f : -1f) * R(0.5f, 0.9f);
            Row(pts, c.HalfWidth, c.LineX, 0f, r, WindowAt(c, window, r), window, bend, RibReach);
        }

        /// <summary>
        /// Шары на ребре — не дальше этого от пути: дальше ребро — одна кость. Ряд шаров
        /// во всю ширину шёл по экрану яркой полосой (7 % плиток с ≥ 3 перепадами в
        /// секунду, «на грани» [ТЕСТ, nzf_thr_Ribs]); на нагрузку на линии не влияет.
        /// </summary>
        const float RibReach = 1.7f;

        /// <summary>
        /// Двойное ребро: два ряда в 1.8 юн., у каждого своё окно; у нижнего шары слева
        /// от пути, у верхнего — справа (путь — у обоих).
        /// Шары — вширь, не стопкой: шар, проезжающий плитку экрана, даёт перепад яркости ~0.14,
        /// и два-три шара в одном столбце за секунду — уже ≥ 3 перепада в секунду
        /// (flash_analyze, «на грани» [ТЕСТ, nzf_thr]).
        /// </summary>
        public static void DoubleRib(List<Vector3> pts, ZoneShapeCtx c)
        {
            float r = c.Small;
            float window = Mathf.Max(c.MinWindow, R(1.1f, 1.5f));
            float w1 = WindowAt(c, window, r);
            float w2 = WindowAt(c, window, r);
            int first = pts.Count;
            Row(pts, c.HalfWidth, c.LineX, -0.9f, r, w1, window, 0.4f, RibReach);
            for (int i = pts.Count - 1; i >= first; i--)
                if (pts[i].x > c.LineX + 0.05f) pts.RemoveAt(i);
            first = pts.Count;
            Row(pts, c.HalfWidth, c.LineX, 0.9f, r, w2, window, 0.4f, RibReach);
            for (int i = pts.Count - 1; i >= first; i--)
                if (pts[i].x < c.LineX - 0.05f) pts.RemoveAt(i);
        }

        /// <summary>Узкое ребро: окно у самого бока — линию пузырька придётся пробивать.</summary>
        public static void NarrowRib(List<Vector3> pts, ZoneShapeCtx c)
        {
            float r = Mathf.Lerp(c.Small, c.Big, 0.3f);
            float window = Mathf.Max(c.MinWindow, R(1.0f, 1.2f));
            float lim = Mathf.Max(0f, c.HalfWidth - window * 0.5f - 0.1f);
            float x = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * lim;
            Row(pts, c.HalfWidth, c.LineX, 0f, r, x, window, (c.Slot % 2 == 0 ? 1f : -1f) * 0.6f, RibReach);
        }

        /// <summary>
        /// Позвонки: три шара сбоку от оси, через 1.7 юн. по высоте — столбик позвоночника.
        /// Через 1.0 юн. три шара в одном столбце экрана давали три вспышки в секунду.
        /// </summary>
        public static void Spine(List<Vector3> pts, ZoneShapeCtx c)
        {
            float side = c.LineX > 0f ? -1f : 1f;
            float x = side * c.HalfWidth * R(0.45f, 0.7f);
            for (int i = 0; i < 3; i++) pts.Add(new Vector3(x + R(-0.1f, 0.1f), (i - 1) * 1.7f, c.Small));
        }

        // ------------------------------------------------------------ сад

        /// <summary>
        /// Остров: три-четыре шара наростами по верху плавучего острова — пологой дугой
        /// (тело острова под ними рисует оформление).
        /// Шары — вширь, не стопкой: шар, проезжающий плитку экрана, даёт перепад яркости ~0.14,
        /// и два-три шара в одном столбце за секунду — уже ≥ 3 перепада в секунду
        /// (flash_analyze, «на грани» [ТЕСТ, nzf_thr]).
        /// </summary>
        public static void Island(List<Vector3> pts, ZoneShapeCtx c)
        {
            // где встать, решает подбор сдвига: то сбоку от пути, то задевая его краем
            int n = UnityEngine.Random.Range(3, 5);
            float step = 0.86f;
            // стен нет, но на узком поле с одной стороны должно остаться окно: ряд
            // не длиннее ширины поля без двух окон (тогда одна сторона ≥ окна)
            int fit = Mathf.FloorToInt((2f * c.HalfWidth - 2f * c.MinWindow - 2f * c.Big) / step) + 1;
            n = Mathf.Clamp(n, 2, Mathf.Max(2, fit));
            float cx = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = n > 1 ? i / (float)(n - 1) * 2f - 1f : 0f;
                float r = R(c.Small, Mathf.Lerp(c.Small, c.Big, 0.7f));
                pts.Add(new Vector3(cx + (i - (n - 1) * 0.5f) * step, 0.3f * (1f - u * u) + R(-0.08f, 0.08f), r));
            }
        }

        /// <summary>
        /// Два рифа разного размера — короткими рядами — с протокой между ними, не
        /// посередине: где протока, решает подбор сдвига (то на пути, то путь упирается
        /// в риф).
        /// Шары — вширь, не стопкой: шар, проезжающий плитку экрана, даёт перепад яркости ~0.14,
        /// и два-три шара в одном столбце за секунду — уже ≥ 3 перепада в секунду
        /// (flash_analyze, «на грани» [ТЕСТ, nzf_thr]).
        /// </summary>
        public static void Twins(List<Vector3> pts, ZoneShapeCtx c)
        {
            float channel = R(c.MinWindow + 0.05f, c.MinWindow + 0.6f);
            // шаров столько, чтобы у протоки был ход ±0.5 юн. и на узком поле: иначе она
            // всегда стояла ровно на пути и риф не задевал его никогда [РАСЧЁТ, ZoneSim]
            int total = Mathf.Clamp(Mathf.FloorToInt((2f * c.HalfWidth - 1f - channel) / 0.94f), 2, 5);
            int left = UnityEngine.Random.Range(1, total), right = total - left;
            for (int s = -1; s <= 1; s += 2)
            {
                int n = s < 0 ? left : right;
                float cy = R(-0.4f, 0.4f);
                float x = s * channel * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float rr = R(c.Small, c.Big * 0.85f);
                    x += s * rr;
                    pts.Add(new Vector3(x, cy + R(-0.12f, 0.12f), rr));
                    x += s * (rr + R(0.02f, 0.12f));
                }
            }
        }

        /// <summary>
        /// Камни-ступени: три мелких шара наискось, через 1.1 юн. по ширине и 1.5 по
        /// высоте — каждый в своём столбце экрана (ступени через 0.5 юн. шли одним
        /// столбцом и мигали в нём трижды в секунду [ТЕСТ, nzf_thr_Garden]).
        /// </summary>
        public static void Stones(List<Vector3> pts, ZoneShapeCtx c)
        {
            float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < 3; i++)
                pts.Add(new Vector3(dir * (i - 1) * 1.1f, (i - 1) * 1.5f + R(-0.15f, 0.15f), c.Small));
        }

        /// <summary>
        /// Атолл: полумесяц из пяти мелких шаров — верхняя дуга кольца, сбоку от пути.
        /// Шары — вширь, не стопкой: шар, проезжающий плитку экрана, даёт перепад яркости ~0.14,
        /// и два-три шара в одном столбце за секунду — уже ≥ 3 перепада в секунду
        /// (flash_analyze, «на грани» [ТЕСТ, nzf_thr]).
        /// </summary>
        public static void Atoll(List<Vector3> pts, ZoneShapeCtx c)
        {
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Vector2 centre = new Vector2(side * c.HalfWidth * R(0.35f, 0.5f), -0.5f);
            // на узком поле — меньше: с одной стороны полумесяца остаётся окно
            float ring = Mathf.Clamp(c.HalfWidth - c.MinWindow - c.Small, 0.8f, 1.25f);
            bool up = UnityEngine.Random.value < 0.5f;
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(20f, 160f, i / 4f) * Mathf.Deg2Rad;
                float y = Mathf.Sin(a) * ring * 0.55f;
                pts.Add(new Vector3(centre.x + Mathf.Cos(a) * ring, up ? centre.y + y : -centre.y - y, c.Small));
            }
        }
    }
}
