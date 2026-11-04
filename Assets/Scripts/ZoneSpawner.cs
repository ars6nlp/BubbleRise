using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оформление локации: цвет воды, цвет стен и корки, тон шаров, набор
    /// рисунков. Всё здесь — только вид: места шаров и их число задают профиль
    /// и композиции, а не тема.
    /// </summary>
    public struct ZoneTheme
    {
        /// <summary>Ключ локализации названия локации.</summary>
        public string NameKey;
        public Color WaterTop, WaterBottom;
        public Color WallDeep, Wall, Crust;
        public Color BlobTint;
        /// <summary>Цвет надписи с названием локации.</summary>
        public Color Banner;
        public WallStyle Style;
        /// <summary>Набор дальних слоёв фона внутри локации.</summary>
        public Biome Back;
        /// <summary>Корка по кромке стены.</summary>
        public WorldPiece CrustPiece;
        /// <summary>Крупное на кромке, мелочи на стене, тусклые силуэты в глубине прохода.</summary>
        public WorldPiece[] Edge, Details, Interior;
        /// <summary>Столбы ворот входа и выхода; GateArch — перемычка-арка над проходом.</summary>
        public WorldPiece GatePost;
        public bool GateArch;
        /// <summary>Светящиеся точки на стенах (впадина).</summary>
        public bool Glow;
    }

    /// <summary>
    /// Каталог локаций и их наполнение. Локация = тема + профиль стен + несколько
    /// композиций из коралловых шаров.
    ///
    /// Как и у построек, для каждой композиции подбирается смещение так, чтобы прямо
    /// над пузырьком стояло не больше zoneMaxCenterBlobs шаров — иначе пробить проход
    /// стражем было бы невозможно.
    /// </summary>
    public class ZoneSpawner : MonoBehaviour
    {
        // Вода темнее, чем была: верх не ярче L 0.018, иначе коралл (L 0.17) давал
        // к воде меньше 3:1, а ёж (L 0.10) — меньше 2.2:1 [РАСЧЁТ, Tools/check_world.py]. Стены и
        // корка — фон: ярче воды, но заметно темнее шаров (у шара L ≈ 0.7).
        static readonly ZoneTheme[] Themes =
        {
            new ZoneTheme
            {
                NameKey = "zone.canyon",
                WaterTop = Hex("10262A"), WaterBottom = Hex("08191B"),
                WallDeep = Hex("1E0E0A"), Wall = Hex("3A1D16"), Crust = Hex("7A3A2A"),
                BlobTint = Hex("FFF3E6"), Banner = Hex("B5573F"),
                Style = WallStyle.Rock, Back = Biome.Canyon, CrustPiece = WorldPiece.CrustCoral,
                Edge = new[] { WorldPiece.CoralBranchA, WorldPiece.CoralBranchB, WorldPiece.FanA, WorldPiece.CoralBranchA, WorldPiece.FanB, WorldPiece.AnemoneA },
                Details = new[] { WorldPiece.StarA, WorldPiece.StarB, WorldPiece.ShellA, WorldPiece.Barnacles, WorldPiece.AnemoneB },
                Interior = new[] { WorldPiece.SpireA, WorldPiece.SpireB, WorldPiece.CanyonStrata },
                GatePost = WorldPiece.SpireA, GateArch = true,
            },
            new ZoneTheme
            {
                NameKey = "zone.kelp",
                WaterTop = Hex("0E2619"), WaterBottom = Hex("06170F"),
                WallDeep = Hex("0A140C"), Wall = Hex("1A2A1A"), Crust = Hex("3D6A3A"),
                BlobTint = Hex("EAF6E6"), Banner = Hex("2E7A4A"),
                Style = WallStyle.Rock, Back = Biome.Shallows, CrustPiece = WorldPiece.CrustRock,
                Edge = new[] { WorldPiece.KelpTallA, WorldPiece.KelpTallB, WorldPiece.KelpShort, WorldPiece.KelpShort, WorldPiece.GrassA, WorldPiece.GrassB },
                Details = new[] { WorldPiece.ShellB, WorldPiece.Pebbles, WorldPiece.StarC, WorldPiece.GrassB },
                Interior = new[] { WorldPiece.KelpTallA, WorldPiece.KelpTallB, WorldPiece.FarKelp },
                GatePost = WorldPiece.KelpTallA, GateArch = false,
            },
            new ZoneTheme
            {
                NameKey = "zone.city",
                WaterTop = Hex("122636"), WaterBottom = Hex("08131E"),
                WallDeep = Hex("141A20"), Wall = Hex("2A333C"), Crust = Hex("56626C"),
                BlobTint = Hex("EFF3F7"), Banner = Hex("8FA3B4"),
                Style = WallStyle.Masonry, Back = Biome.Ruins, CrustPiece = WorldPiece.CrustRock,
                Edge = new[] { WorldPiece.ColumnA, WorldPiece.ColumnB, WorldPiece.Amphora, WorldPiece.ColumnA, WorldPiece.AnchorOld, WorldPiece.Chain },
                Details = new[] { WorldPiece.ShardA, WorldPiece.ShardB, WorldPiece.Amphora, WorldPiece.Barnacles, WorldPiece.ShellC },
                Interior = new[] { WorldPiece.WallWindow, WorldPiece.ArchMid, WorldPiece.Stairs, WorldPiece.ColumnA, WorldPiece.FarRuinsA },
                GatePost = WorldPiece.ColumnB, GateArch = true,
            },
            new ZoneTheme
            {
                NameKey = "zone.trench",
                WaterTop = Hex("1C1433"), WaterBottom = Hex("0A0716"),
                WallDeep = Hex("0E0818"), Wall = Hex("1E1430"), Crust = Hex("4E3478"),
                BlobTint = Hex("D9CBFF"), Banner = Hex("9A74D0"),
                Style = WallStyle.Rock, Back = Biome.Trench, CrustPiece = WorldPiece.CrustCoral,
                Edge = new[] { WorldPiece.TubesA, WorldPiece.TubesB, WorldPiece.AnemoneB, WorldPiece.TubesB, WorldPiece.AnemoneA },
                Details = new[] { WorldPiece.Barnacles, WorldPiece.ShellC, WorldPiece.TubesA },
                Interior = new[] { WorldPiece.SpireC, WorldPiece.SpireB, WorldPiece.FarRidgeB },
                GatePost = WorldPiece.SpireC, GateArch = true, Glow = true,
            },
            new ZoneTheme
            {
                NameKey = "zone.shoal",
                WaterTop = Hex("112628"), WaterBottom = Hex("0A1A1D"),
                WallDeep = Hex("3A2E1C"), Wall = Hex("5A4A30"), Crust = Hex("8A744C"),
                BlobTint = Hex("FFF6E0"), Banner = Hex("C9A45E"),
                Style = WallStyle.Sand, Back = Biome.Shallows, CrustPiece = WorldPiece.CrustSand,
                Edge = new[] { WorldPiece.GrassA, WorldPiece.GrassB, WorldPiece.CoralBranchB, WorldPiece.FanA, WorldPiece.GrassA },
                Details = new[] { WorldPiece.StarA, WorldPiece.StarB, WorldPiece.StarC, WorldPiece.ShellA, WorldPiece.ShellB, WorldPiece.ShellC, WorldPiece.Pebbles },
                Interior = new[] { WorldPiece.FarArch, WorldPiece.ColumnA, WorldPiece.KelpTallB },
                GatePost = WorldPiece.ColumnA, GateArch = true,
            },
        };

        /// <summary>
        /// «Ледяной разлом» — вторая локация, совсем другая: холодная синяя вода,
        /// ледяные стены, кристаллы, сосульки, льдины-потолок. Угрозы — те же
        /// композиции шаров, что и в первой; шар — ледяной глыбой.
        /// Вода холоднее и бирюзовее бездны, верх L 0.016 (коралл 3.3:1, ёж 2.3:1).
        /// Стены светлые, голубые — лёд светлее воды; корка L 0.18 — шар к ней 3:1
        /// (самая светлая, какую шары ещё позволяют). Прежние тёмные стены были
        /// тоном воды и пропадали [ТЕСТ].
        /// </summary>
        static readonly ZoneTheme IceTheme = new ZoneTheme
        {
            NameKey = "zone.ice",
            WaterTop = Hex("0B2535"), WaterBottom = Hex("061521"),
            WallDeep = Hex("1E3A52"), Wall = Hex("4A7290"), Crust = Hex("52789A"),
            BlobTint = Hex("E4F2FF"), Banner = Hex("9CC8E8"),
            Style = WallStyle.Ice, Back = Biome.Abyss, CrustPiece = WorldPiece.CrustRock,
            // наборы из атласа мира льду не нужны: у него свои (ZoneDressing.IceEdge и DressIce)
            Edge = null, Details = null, Interior = null,
            GatePost = WorldPiece.ColumnA, GateArch = true,
        };

        // Профиль: где проходит внутренний край стены, в долях половины игровой зоны.
        // t = 0 — вход в локацию (низ), t = 1 — выход (верх). Больше 1 — стена за экраном.
        static readonly Func<float, float>[] Profiles =
        {
            t => 0.60f,                                         // прямой коридор
            t => Mathf.Lerp(0.45f, 1.20f, t),                   // воронка, расширяется вверх
            t => Mathf.Lerp(1.20f, 0.48f, t),                   // воронка, сужается вверх
            t => 0.50f + 0.78f * Mathf.Sin(t * Mathf.PI),       // зал: узкие вход и выход
            t => 1.25f - 0.75f * Mathf.Sin(t * Mathf.PI),       // перешеек: узко посередине
        };

        enum Pattern { Pyramid, Arc, Ridge, Cluster, Gate }

        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<OceanZone> _pool = new Stack<OceanZone>();
        int _lastTheme = -1;

        // Вид локации — по очереди ZoneRotation: круг из всех видов каталога в
        // случайном порядке, без двух одинаковых подряд. Зерно очереди — своим
        // генератором: UnityEngine.Random задаёт угрозы, и лишний вызов сдвинул бы
        // весь их поток.
        readonly System.Random _kindRng = new System.Random();
        ZoneRotation _rotation = new ZoneRotation(0);

        /// <summary>Новый забег: очередь видов — с новым случайным зерном.</summary>
        public void ResetRun() => ResetRun(_kindRng.Next());

        /// <summary>Новый забег с заданным зерном очереди (проверки).</summary>
        public void ResetRun(int seed) => _rotation = new ZoneRotation(seed);

        /// <summary>Какой вид локации будет следующим.</summary>
        public ZoneKind NextKind => _rotation.Peek();

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
        }

        /// <param name="force">null — по очереди; иначе этот вид (отладка).</param>
        public OceanZone Spawn(ZoneKind? force = null)
        {
            var kind = force ?? _rotation.Next();
            if (force.HasValue) _rotation.Played(kind);
            var def = ZoneCatalog.Get(kind);
            if (!def.Legacy) return SpawnShaped(def);

            // Руины и лёд — прежним путём, слово в слово.
            // тему не повторяем подряд
            int themeIndex = UnityEngine.Random.Range(0, Themes.Length);
            if (themeIndex == _lastTheme) themeIndex = (themeIndex + 1) % Themes.Length;
            _lastTheme = themeIndex;

            bool ice = kind == ZoneKind.Ice;
            var theme = ice ? IceTheme : Themes[themeIndex];
            var profile = Profiles[UnityEngine.Random.Range(0, Profiles.Length)];
            float height = _cfg.zoneHeight;

            // Порядок вызовов UnityEngine.Random тот же, что до этапа 4: тема,
            // профиль, зерно, шары, кромка. Иначе с тем же зерном забега шары
            // и весь дальнейший поток угроз встали бы по-другому.
            int seed = UnityEngine.Random.Range(0, 10000);
            var zone = Rent();
            zone.Activate(theme, profile, height, _field, seed);

            FillContent(zone, theme, profile, height);
            AddWallDecor(zone, theme, profile, height);
            ZoneDressing.Dress(zone, theme, _field, seed);
            zone.FinishSetup();
            return zone;
        }

        // ------------------------------------------------- локации новых видов

        readonly List<Vector3> _layout = new List<Vector3>();
        readonly List<OceanZone.Slot> _slots = new List<OceanZone.Slot>();

        /// <summary>Корабль, рёбра, сад: форма и шаблоны — из записи каталога.</summary>
        OceanZone SpawnShaped(ZoneDef def)
        {
            float height = def.Length * _cfg.zoneHeight / 38f;
            int seed = UnityEngine.Random.Range(0, 10000);
            var zone = Rent();
            zone.Activate(def, height, _field, seed);

            Layout(def, height, _field.PlayHalfWidth, _cfg, _layout, _slots);
            for (int i = 0; i < _layout.Count; i++)
                zone.AddBlob(new Vector2(_layout[i].x, _layout[i].y), _layout[i].z, def.Theme.BlobTint);
            ZoneDressingShaped.Dress(zone, def, _field, seed, _layout, _slots);
            zone.FinishSetup();
            return zone;
        }

        static readonly List<Vector3> s_points = new List<Vector3>();

        /// <summary>
        /// Шары локации нового вида: x, y (от середины локации) и радиус в z.
        ///
        /// Слоты — через SlotSpacing записи, от 10 до 90 % длины, как у прежних
        /// композиций. Шаблон строится от середины прохода (у корабля она гуляет),
        /// затем — те же правила, что у прежних: на линии пузырька (|x| < r + 0.3) не
        /// больше MaxCenterBlocks шаров на шаблон (лишние снимаются — ближние к линии,
        /// пробоина ложится на путь), шар, не влезающий в проход, выбрасывается.
        /// Случайность — UnityEngine.Random, как у прежних композиций.
        /// </summary>
        public static void Layout(ZoneDef def, float height, float playHalfWidth, GameConfig cfg, List<Vector3> output,
                                  List<OceanZone.Slot> slots = null)
        {
            output.Clear();
            slots?.Clear();
            int slotCount = Mathf.Clamp(Mathf.RoundToInt(height * 0.8f / def.SlotSpacing) + 1, 3, 12);
            int maxCenter = Difficulty.MaxCenterBlocks(cfg.zoneMaxCenterBlobs);
            float bubbleHit = PlayerSize.Bubble(cfg) * Difficulty.Current.bubbleHitboxScale;
            var ctx = new ZoneShapeCtx
            {
                Small = cfg.zoneBlobMinRadius,
                Big = cfg.zoneBlobMaxRadius,
                // самое узкое окно в ряду — полтора диаметра стража
                MinWindow = 1.5f * 2f * cfg.guardianRadius * PlayerSize.Scale,
            };
            int totalWeight = 0;
            for (int p = 0; p < def.Patterns.Length; p++) totalWeight += def.Patterns[p].Weight;

            for (int i = 0; i < slotCount; i++)
            {
                float t = Mathf.Lerp(0.10f, 0.90f, i / (float)(slotCount - 1)) + UnityEngine.Random.Range(-0.02f, 0.02f);
                float y = -height * 0.5f + t * height;
                float half = Mathf.Min(def.HalfWidth(t), 1f) * playHalfWidth;
                float centre = def.Shift(t) * playHalfWidth;
                ctx.HalfWidth = half;
                ctx.LineX = -centre;
                ctx.Slot = i;
                ctx.OpenChance = def.OpenChance;

                int roll = UnityEngine.Random.Range(0, totalWeight);
                var pattern = def.Patterns[0];
                for (int p = 0; p < def.Patterns.Length; p++)
                {
                    if (roll < def.Patterns[p].Weight) { pattern = def.Patterns[p]; break; }
                    roll -= def.Patterns[p].Weight;
                }

                s_points.Clear();
                pattern.Build(s_points, ctx);
                if (s_points.Count == 0) continue;

                bool touch = UnityEngine.Random.value < def.LineBias;
                float dx = pattern.Anchored ? 0f : FreeOffset(s_points, half, centre, maxCenter, touch ? bubbleHit : -1f);
                int first = output.Count;
                for (int j = 0; j < s_points.Count; j++)
                {
                    var p = s_points[j];
                    float radius = Mathf.Min(p.z, half * 0.34f);
                    float px = p.x + dx;
                    if (Mathf.Abs(px) > half - radius) continue;   // не влезает в проход
                    if (Mathf.Abs(centre + px) > playHalfWidth - radius) continue;   // и в поле
                    output.Add(new Vector3(centre + px, y + p.y, radius));
                }

                // лишние шары над пузырьком снимаем — с краю линии: самый близкий к пути остаётся
                while (true)
                {
                    int blocked = 0, farthest = -1;
                    for (int j = first; j < output.Count; j++)
                    {
                        var b = output[j];
                        if (Mathf.Abs(b.x) >= b.z + 0.30f) continue;
                        blocked++;
                        if (farthest < 0 || Mathf.Abs(b.x) > Mathf.Abs(output[farthest].x)) farthest = j;
                    }
                    if (blocked <= maxCenter) break;
                    output.RemoveAt(farthest);
                }
                slots?.Add(new OceanZone.Slot
                {
                    Pattern = pattern.Name, Y = y, Centre = centre, Half = half, First = first, Count = output.Count - first,
                });
            }
        }

        /// <summary>
        /// Сдвиг шаблона внутри прохода — тот же подбор, что у PickOffset: на линии не
        /// больше maxCenter шаров. touchHit ≥ 0 — ещё и задеть пузырёк хотя бы одним
        /// шаром (ZoneLoad.OnLine с хитбоксом пузырька touchHit); иначе — обойти путь
        /// совсем, если место есть. Так нагрузка не зависит от ширины экрана: на узком
        /// шаблон иначе задевал бы путь сам собой, на широком — почти никогда.
        /// </summary>
        static float FreeOffset(List<Vector3> points, float half, float centre, int maxCenter, float touchHit)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                minX = Mathf.Min(minX, points[i].x - points[i].z);
                maxX = Mathf.Max(maxX, points[i].x + points[i].z);
            }
            float lo = -half - minX, hi = half - maxX;
            if (lo > hi) lo = hi = 0f;

            float best = 0f;
            int bestCount = int.MaxValue;
            // Задеть путь: сперва — сдвиги, ставящие на путь один из шаров (с какого
            // начать — наугад), потом наугад, как у PickOffset. Обойти — наугад, края
            // и исходное место шаблона (у рифов это протока ровно на пути).
            int n = points.Count;
            int from = UnityEngine.Random.Range(0, n);
            int tries = touchHit >= 0f ? n + 10 : 13;
            for (int attempt = 0; attempt < tries; attempt++)
            {
                float candidate;
                if (touchHit >= 0f && attempt < n)
                    candidate = Mathf.Clamp(-centre - points[(from + attempt) % n].x + UnityEngine.Random.Range(-0.15f, 0.15f), lo, hi);
                else if (touchHit < 0f && attempt >= 10)
                    candidate = attempt == 10 ? lo : attempt == 11 ? hi : Mathf.Clamp(0f, lo, hi);
                else
                    candidate = hi - lo > 0.01f ? UnityEngine.Random.Range(lo, hi) : lo;
                int blocked = 0, hits = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    float x = centre + points[i].x + candidate;
                    if (Mathf.Abs(x) < points[i].z + 0.30f) blocked++;
                    if (touchHit >= 0f && ZoneLoad.OnLine(x, points[i].z, touchHit)) hits++;
                }
                // Оценка, 0 — то, что нужно. Задеть путь: одним или двумя шарами (в пределах
                // нормы), на крайний случай — с лишними: их снимет Layout (у острова — откол
                // у самого пути). Обойти: чем меньше шаров на пути, тем лучше, больше нормы —
                // только если иначе никак.
                int miss = touchHit >= 0f
                    ? (hits == 0 ? 1000 : blocked <= maxCenter ? 0 : blocked - maxCenter)
                    : (blocked > maxCenter ? 1000 + blocked : blocked);
                if (miss == 0) return candidate;
                if (miss < bestCount) { bestCount = miss; best = candidate; }
            }
            return best;
        }

        // -------------------------------------------------------- наполнение

        void FillContent(OceanZone zone, ZoneTheme theme, Func<float, float> profile, float height)
        {
            // Композиции расставляем примерно через каждые 5.5 юнита, поэтому длину
            // локации можно менять в инспекторе — наполнение подстроится само.
            int slotCount = Mathf.Clamp(Mathf.RoundToInt(height / 5.5f), 3, 10);

            for (int i = 0; i < slotCount; i++)
            {
                float t = Mathf.Lerp(0.10f, 0.90f, slotCount > 1 ? i / (float)(slotCount - 1) : 0.5f)
                          + UnityEngine.Random.Range(-0.02f, 0.02f);
                float y = -height * 0.5f + t * height;
                float passage = Mathf.Min(profile(t) * _field.PlayHalfWidth, _field.PlayHalfWidth);

                // В самом узком месте каньона композицию не ставим: там и без шаров
                // тесно, а зажатые в проход шары не оставили бы шанса пробиться.
                if (passage < 1.1f) continue;

                var pattern = (Pattern)UnityEngine.Random.Range(0, 5);
                var points = BuildPattern(pattern, passage);
                if (points.Count == 0) continue;

                float offset = PickOffset(points, passage);
                for (int j = 0; j < points.Count; j++)
                {
                    var point = points[j];
                    float x = point.x + offset;
                    float radius = Mathf.Min(point.z, passage * 0.34f);

                    // Не влезает в проход — просто выбрасываем шар, а не вжимаем его
                    // к центру: иначе узкие места становились бы смертельными.
                    if (Mathf.Abs(x) > passage - radius) continue;

                    zone.AddBlob(new Vector2(x, y + point.y), radius, theme.BlobTint);
                }
            }
        }

        /// <summary>Точки композиции: x, y и радиус в компоненте z.</summary>
        List<Vector3> BuildPattern(Pattern pattern, float passage)
        {
            var points = new List<Vector3>();
            float small = _cfg.zoneBlobMinRadius;
            float big = _cfg.zoneBlobMaxRadius;
            float step = Mathf.Min(big * 2.15f, Mathf.Max(0.8f, passage * 0.62f));

            switch (pattern)
            {
                case Pattern.Pyramid:
                {
                    // 1-2-3 сверху вниз, как на референсе
                    int rows = passage > 2.2f ? 3 : 2;
                    float r = Mathf.Lerp(small, big, 0.45f);
                    for (int row = 0; row < rows; row++)
                    {
                        int count = row + 1;
                        for (int c = 0; c < count; c++)
                        {
                            float x = (c - (count - 1) * 0.5f) * step;
                            points.Add(new Vector3(x, -row * step * 0.92f, r));
                        }
                    }
                    break;
                }

                case Pattern.Arc:
                {
                    int count = passage > 2.4f ? 7 : 5;
                    float radius = Mathf.Min(passage * 0.85f, 2.6f);
                    float r = Mathf.Lerp(small, big, 0.30f);
                    for (int i = 0; i < count; i++)
                    {
                        float a = Mathf.Lerp(165f, 15f, i / (float)(count - 1)) * Mathf.Deg2Rad;
                        points.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.55f, r));
                    }
                    break;
                }

                case Pattern.Ridge:
                {
                    // коралловая гряда по диагонали, шары мельчают к хвосту
                    int count = 5;
                    int dir = UnityEngine.Random.value < 0.5f ? -1 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        float k = i / (float)(count - 1);
                        float r = Mathf.Lerp(big, small, k);
                        points.Add(new Vector3(dir * (k - 0.5f) * passage * 1.5f,
                                               (k - 0.5f) * 2.2f, r));
                    }
                    break;
                }

                case Pattern.Cluster:
                {
                    int count = 3;
                    for (int i = 0; i < count; i++)
                    {
                        float r = UnityEngine.Random.Range(big * 0.8f, big);
                        points.Add(new Vector3(
                            UnityEngine.Random.Range(-passage * 0.6f, passage * 0.6f),
                            UnityEngine.Random.Range(-1.3f, 1.3f), r));
                    }
                    break;
                }

                default: // Gate — две колонны с проходом посередине
                {
                    float r = Mathf.Lerp(small, big, 0.5f);
                    float side = Mathf.Max(0.9f, passage * 0.62f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = (i - 1) * r * 2.1f;
                        points.Add(new Vector3(-side, y, r));
                        points.Add(new Vector3(side, y, r));
                    }
                    break;
                }
            }
            return points;
        }

        /// <summary>Смещаем композицию так, чтобы над пузырьком было мало шаров.</summary>
        float PickOffset(List<Vector3> points, float passage)
        {
            float limit = Mathf.Max(0f, passage * 0.55f);
            float best = 0f;
            int bestCount = int.MaxValue;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float candidate = limit > 0.01f ? UnityEngine.Random.Range(-limit, limit) : 0f;

                int blocked = 0;
                for (int i = 0; i < points.Count; i++)
                    if (Mathf.Abs(points[i].x + candidate) < points[i].z + 0.30f) blocked++;

                if (blocked <= Difficulty.MaxCenterBlocks(_cfg.zoneMaxCenterBlobs)) return candidate;
                if (blocked < bestCount) { bestCount = blocked; best = candidate; }
            }
            return best;
        }

        /// <summary>
        /// Крупное на кромке стены. Вызовы UnityEngine.Random — ровно те же и в
        /// том же порядке, что у прежних водорослей и столбиков: на шары и поток
        /// угроз после локации оформление не влияет. Что именно стоит, выбирает
        /// набор темы.
        /// </summary>
        void AddWallDecor(OceanZone zone, ZoneTheme theme, Func<float, float> profile, float height)
        {
            bool ice = theme.Style == WallStyle.Ice;
            int decorCount = Mathf.Clamp(Mathf.RoundToInt(height / 3f), 6, 20);
            for (int i = 0; i < decorCount; i++)
            {
                float t = UnityEngine.Random.Range(0.03f, 0.97f);
                float inner = profile(t) * _field.PlayHalfWidth;
                if (inner > _field.HalfWidth) continue; // стена за экраном — украшать нечего

                bool right = i % 2 == 0;
                bool first = UnityEngine.Random.value < 0.55f;
                float size = first ? UnityEngine.Random.Range(2.4f, 3.6f)
                                   : UnityEngine.Random.Range(1.8f, 2.8f);
                float jitter = UnityEngine.Random.Range(-0.15f, 0.35f);

                float sizeK = Mathf.InverseLerp(1.8f, 3.6f, size);
                if (ice)
                    ZoneDressing.IceFeature(zone, theme, ZoneDressing.IceEdge[(first ? i : i + 3) % ZoneDressing.IceEdge.Length],
                                            t, right, sizeK, jitter);
                else
                    ZoneDressing.EdgeFeature(zone, theme, theme.Edge[(first ? i : i + 3) % theme.Edge.Length],
                                             t, right, sizeK, jitter);
            }
        }

        // ------------------------------------------------------------- пул

        OceanZone Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("OceanZone");
            go.transform.SetParent(_root, false);
            return go.AddComponent<OceanZone>();
        }

        public void Recycle(OceanZone zone)
        {
            if (zone == null || zone.Pooled) return;
            zone.Pooled = true;
            zone.Deactivate();
            _pool.Push(zone);
        }

        static Color Hex(string hex) => Palette.Hex(hex);
    }
}
