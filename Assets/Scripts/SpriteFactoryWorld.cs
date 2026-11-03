using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Рисунки мира в атласе: фон, детали, свет, лёд.</summary>
    public enum WorldPiece
    {
        // дальний план — силуэты, их тонирует туман глубины
        FarRidgeA, FarRidgeB, FarRidgeC, FarRuinsA, FarRuinsB, FarKelp, FarArch,
        // жизнь на дальнем плане: не враги, без насыщенных цветов
        SchoolFish, Ray, Jelly, Whale,
        // средний план
        SpireA, SpireB, SpireC, ColumnA, ColumnB, ArchMid, WallWindow, Stairs,
        KelpTallA, KelpTallB, CanyonStrata,
        // ближний план и мелочи
        RockA, RockB, RockC, RockD, CoralBranchA, CoralBranchB, FanA, FanB, Brain,
        AnemoneA, AnemoneB, TubesA, TubesB, StarA, StarB, StarC, UrchinDecor,
        ShellA, ShellB, ShellC, Pebbles, SandA, SandB, Amphora, ShardA, ShardB,
        DrumA, DrumB, Chain, AnchorOld, RustPlate, Barnacles, GrassA, GrassB, KelpShort,
        CrustRock, CrustCoral, CrustSand,
        // передний план
        FrontLeaf, FrontBubble,
        // свет и частицы
        Shaft, Dot, Flake, BubbleRing, Glow, WindowBeam,
        Count
    }

    /// <summary>Рисунки «Ледяного разлома» — отдельный атлас, нужен только в этой локации.</summary>
    public enum IcePiece
    {
        FloeA, FloeB, FloeC, IcicleA, IcicleB, CrystalA, CrystalB, CrystalC, FrozenColumn,
        ShelfA, ShelfB, Drift, FrozenKelp, BoulderA, BoulderB, IceFish, IceArch, FrostRock,
        Count
    }

    /// <summary>Место рисунка в атласе: UV и соотношение сторон (ширина / высота).</summary>
    public struct AtlasPiece
    {
        public Rect Uv;
        public float Aspect;
        public int Width, Height;
        /// <summary>Средний цвет непрозрачных точек (sRGB) — от него считается тон слоя.</summary>
        public Color Avg;
    }

    public static partial class SpriteFactory
    {
        // ------------------------------------------------------------ атласы
        //
        // Атласы рисуются один раз при загрузке, текстуры дальше не трогаются.
        // Один атлас — один материал — один вызов отрисовки на слой.

        static Texture2D _worldAtlas, _iceAtlas, _causticTex;
        static Material _worldMat, _iceMat, _causticMat;
        static AtlasPiece[] _world, _ice;

        /// <summary>Сколько миллисекунд ушло на атласы мира — для замера загрузки.</summary>
        public static long WorldAtlasMs { get; private set; }

        /// <summary>Самые дорогие рисунки атласов: «имя мс», по убыванию — для замера загрузки.</summary>
        public static string WorldAtlasReport { get; private set; } = "";
        static readonly List<KeyValuePair<string, long>> PieceCost = new List<KeyValuePair<string, long>>();

        public static Material WorldMaterial { get { EnsureWorldAtlas(); return _worldMat; } }
        public static Material IceMaterial { get { EnsureIceAtlas(); return _iceMat; } }
        public static Material CausticMaterial { get { EnsureCaustics(); return _causticMat; } }
        public static Texture2D WorldAtlasTexture { get { EnsureWorldAtlas(); return _worldAtlas; } }
        public static Texture2D IceAtlasTexture { get { EnsureIceAtlas(); return _iceAtlas; } }

        public static AtlasPiece World(WorldPiece piece) { EnsureWorldAtlas(); return _world[(int)piece]; }
        public static AtlasPiece Ice(IcePiece piece) { EnsureIceAtlas(); return _ice[(int)piece]; }

        struct PieceDef
        {
            public int Id, W, H, SS;
            public Func<Vector2, Color> Paint;
        }

        public static void PrewarmWorld()
        {
            EnsureWorldAtlas();
            EnsureIceAtlas();
            EnsureZonesAtlas();
            EnsureCaustics();
            foreach (WallStyle style in Enum.GetValues(typeof(WallStyle))) ZoneWallMaterial(style);
        }

        static void EnsureWorldAtlas()
        {
            if (_worldAtlas != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _world = new AtlasPiece[(int)WorldPiece.Count];
            _worldAtlas = BakedOrPaintAtlas("world_atlas", WorldDefs(), 2048, _world);
            _worldMat = new Material(Unlit) { name = "BubbleRise_World", mainTexture = _worldAtlas };
            WorldAtlasMs += sw.ElapsedMilliseconds;
        }

        static void EnsureIceAtlas()
        {
            if (_iceAtlas != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _ice = new AtlasPiece[(int)IcePiece.Count];
            _iceAtlas = BakedOrPaintAtlas("ice_atlas", IceDefs(), 1024, _ice);
            _iceMat = new Material(Unlit) { name = "BubbleRise_Ice", mainTexture = _iceAtlas };
            WorldAtlasMs += sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// Атлас: готовый из запечённых файлов, если они есть и совпадают с разметкой,
        /// иначе — нарисованный здесь же. Разметка (места и размеры рисунков) в обоих
        /// случаях считается из списка рисунков: это дёшево, рисование — нет.
        /// </summary>
        static Texture2D BakedOrPaintAtlas(string name, List<PieceDef> defs, int width, AtlasPiece[] into)
        {
            var pos = new Vector2Int[defs.Count];
            int height = LayoutAtlas(defs, width, into, pos);
            var baked = LoadBaked(name, TextureWrapMode.Clamp, width, height);
            if (baked != null && ReadBakedAvg(name, into)) return baked;
            if (baked != null) DestroyTexture(baked);
            return MakeTexture(name, width, height, PaintAtlas(name, defs, width, height, pos, into), TextureWrapMode.Clamp);
        }

        /// <summary>
        /// Полочная упаковка: рисунки по убыванию высоты, ряд за рядом. Поле в
        /// 2 точки между рисунками — билинейная выборка не цепляет соседа.
        /// Заполняет места и размеры рисунков (без среднего цвета), возвращает
        /// высоту атласа.
        /// </summary>
        static int LayoutAtlas(List<PieceDef> defs, int width, AtlasPiece[] into, Vector2Int[] pos)
        {
            defs.Sort((a, b) => b.H.CompareTo(a.H));
            const int pad = 2;
            int x = pad, y = pad, shelf = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                if (x + defs[i].W + pad > width) { y += shelf + pad; x = pad; shelf = 0; }
                pos[i] = new Vector2Int(x, y);
                x += defs[i].W + pad;
                shelf = Mathf.Max(shelf, defs[i].H);
            }
            int used = y + shelf + pad;
            int height = 64;
            while (height < used) height *= 2;

            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                // полпикселя внутрь: на краю рисунка выборка не тянет пустоту соседнего поля
                into[d.Id] = new AtlasPiece
                {
                    Uv = new Rect((pos[i].x + 0.5f) / width, (pos[i].y + 0.5f) / height,
                                  (d.W - 1f) / width, (d.H - 1f) / height),
                    Aspect = d.W / (float)d.H,
                    Width = d.W,
                    Height = d.H,
                    Avg = Color.white,
                };
            }
            return height;
        }

        /// <summary>Нарисовать атлас по разметке и посчитать средний цвет каждого рисунка.</summary>
        static Color32[] PaintAtlas(string name, List<PieceDef> defs, int width, int height, Vector2Int[] pos,
                                    AtlasPiece[] into)
        {
            var px = new Color32[width * height];
            var watch = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                watch.Restart();
                // Крупные рисунки — без суперсэмплинга: на экране они всё равно
                // растянуты и сглажены билинейной выборкой, а загрузку SS×SS
                // замедлял вчетверо. Мелочи (до 128×128) рисуются с ним.
                int ss = d.W * d.H > 128 * 128 ? 1 : d.SS;
                var src = RenderRect(d.W, d.H, d.Paint, ss);
                // Без суперсэмплинга кромка жёсткая, а на экране рисунок растянут
                // вчетверо — край шёл лесенкой [ТЕСТ]. Смягчаем: цвет подтягиваем
                // на прозрачную кайму, альфу размываем 3×3.
                if (ss == 1) SoftenEdges(src, d.W, d.H);
                PieceCost.Add(new KeyValuePair<string, long>(name + ":" + d.Id, watch.ElapsedMilliseconds));
                float ar = 0f, ag = 0f, ab = 0f, an = 0f;
                for (int row = 0; row < d.H; row++)
                {
                    int dst = (pos[i].y + row) * width + pos[i].x;
                    int s = row * d.W;
                    for (int col = 0; col < d.W; col++)
                    {
                        var c = src[s + col];
                        px[dst + col] = c;
                        if (c.a > 0.5f) { ar += c.r; ag += c.g; ab += c.b; an += 1f; }
                    }
                }
                into[d.Id].Avg = an > 0f ? new Color(ar / an, ag / an, ab / an, 1f) : Color.white;
            }

            PieceCost.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(12, PieceCost.Count); i++)
                sb.Append(PieceCost[i].Key).Append(' ').Append(PieceCost[i].Value).Append("; ");
            WorldAtlasReport = sb.ToString();
            return px;
        }

        static Texture2D MakeTexture(string name, int width, int height, Color32[] px, TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = wrap,
                name = name,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            AtlasesPainted++;
            return tex;
        }

        // ------------------------------------------------------------ помощники

        static void SoftenEdges(Color[] px, int w, int h)
        {
            var src = (Color[])px.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = 0f, r = 0f, g = 0f, b = 0f, wsum = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            var c = src[yy * w + xx];
                            a += c.a;
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; wsum += c.a;
                        }
                    }
                    a /= 9f;
                    var o = src[y * w + x];
                    if (wsum > 0.0001f && o.a < 0.99f)
                        px[y * w + x] = new Color(r / wsum, g / wsum, b / wsum, a);
                    else
                        px[y * w + x] = new Color(o.r, o.g, o.b, a);
                }
            }
        }

        static float Sat(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float SStep(float a, float b, float x) { float t = Sat((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Целочисленный хеш: в разы дешевле синуса, а атласы рисуются по пикселю.</summary>
        static float Hash(float x, float y)
        {
            unchecked
            {
                uint h = (uint)(int)(x * 1597f) * 0x27d4eb2dU ^ (uint)(int)(y * 2531f) * 0x165667b1U;
                h ^= h >> 15; h *= 0x85ebca6bU; h ^= h >> 13; h *= 0xc2b2ae35U; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Шум значений со сглаженной интерполяцией: дешевле Перлина и без полос.</summary>
        static float VNoise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int octaves = 3)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += VNoise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f; y = y * 2.03f + 9.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Sat(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, ba.sqrMagnitude));
            return (pa - ba * h).magnitude;
        }

        /// <summary>
        /// Общий свет мира для силуэта с полем расстояний: свет сверху и чуть слева,
        /// верхняя кромка светлее, низ темнее, обводка на треть темнее заливки.
        /// Нормаль — по разности поля, поэтому свет ложится по форме, а не по рамке.
        /// </summary>
        static Color Lit(Func<Vector2, float> sdf, Vector2 p, Color fill, float outline, float rim = 1f)
        {
            float d = sdf(p);
            if (d > 0f) return Color.clear;
            float edge = Sat(1f + d / (outline * 4f));
            float facing = 0f;
            // нормаль нужна только у кромки: внутри блик не ложится, и два лишних
            // вызова поля расстояний там — чистая трата времени загрузки
            if (edge > 0f)
            {
                const float e = 0.012f;
                float gx = sdf(new Vector2(p.x + e, p.y)) - d;
                float gy = sdf(new Vector2(p.x, p.y + e)) - d;
                float len = Mathf.Sqrt(gx * gx + gy * gy) + 1e-6f;
                facing = Sat((-0.32f * gx + 0.95f * gy) / len);
            }
            Color c = Color.Lerp(Mul(fill, 0.72f), Mul(fill, 1.08f), SStep(-1f, 1f, p.y));
            c = Color.Lerp(c, Mul(fill, 1.35f), facing * edge * 0.55f * rim);
            if (d > -outline) c = Color.Lerp(c, Mul(fill, 0.62f), 0.85f);
            c.a = 1f;
            return c;
        }

        static void Def(List<PieceDef> list, Enum id, int w, int h, int ss, Func<Vector2, Color> paint)
            => list.Add(new PieceDef { Id = Convert.ToInt32(id), W = w, H = h, SS = ss, Paint = paint });

        // ------------------------------------------------------------ цвета деталей
        //
        // Природные цвета, приглушённые: дальше их ещё темнит туман слоя. Ни один
        // не совпадает с цветами угроз — красно-коричневым кораллом, оранжевым
        // угольком и розово-фиолетовым ежом: декор не имеет права выглядеть опасным.

        static readonly Color RockFill = Palette.Hex("5C6870");
        static readonly Color StoneFill = Palette.Hex("7C7768");
        static readonly Color AlgaeFill = Palette.Hex("3E6A4A");
        static readonly Color SandFill = Palette.Hex("857D66");
        static readonly Color BarnacleFill = Palette.Hex("A8A08A");
        static readonly Color MauveFill = Palette.Hex("8E5C80");
        static readonly Color OchreFill = Palette.Hex("A6843E");
        static readonly Color SeaGreenFill = Palette.Hex("4A8468");
        static readonly Color SlateFill = Palette.Hex("51669C");
        static readonly Color RustFill = Palette.Hex("6A4C3C");
        static readonly Color SilhouetteFill = Palette.Hex("8FA4B4");   // нейтральный: туман слоя даст ему цвет

        // ------------------------------------------------------------ рисунки мира

        static List<PieceDef> WorldDefs()
        {
            var l = new List<PieceDef>();

            // ---- дальний план: хребты, руины, лес ламинарий, арка
            Def(l, WorldPiece.FarRidgeA, 256, 96, 1, p => Ridge(p, 3.1f, 0.55f));
            Def(l, WorldPiece.FarRidgeB, 256, 96, 1, p => Ridge(p, 7.7f, 0.75f));
            Def(l, WorldPiece.FarRidgeC, 256, 96, 1, p => Ridge(p, 12.3f, 0.4f));
            Def(l, WorldPiece.FarRuinsA, 256, 112, 1, p => Skyline(p, 2.0f));
            Def(l, WorldPiece.FarRuinsB, 256, 112, 1, p => Skyline(p, 5.4f));
            Def(l, WorldPiece.FarKelp, 128, 256, 1, FarKelpForest);
            Def(l, WorldPiece.FarArch, 192, 128, 1, p => NaturalArch(p, SilhouetteFill, 0f));

            // ---- жизнь
            Def(l, WorldPiece.SchoolFish, 48, 16, 2, SchoolFishShape);
            Def(l, WorldPiece.Ray, 128, 80, 2, RayShape);
            Def(l, WorldPiece.Jelly, 48, 72, 2, JellyShape);
            Def(l, WorldPiece.Whale, 256, 112, 1, WhaleShape);

            // ---- средний план
            Def(l, WorldPiece.SpireA, 96, 256, 1, p => Spire(p, 1.3f));
            Def(l, WorldPiece.SpireB, 96, 256, 1, p => Spire(p, 4.6f));
            Def(l, WorldPiece.SpireC, 96, 256, 1, p => Spire(p, 8.2f));
            Def(l, WorldPiece.ColumnA, 80, 320, 2, p => Column(p, 0.9f, false));
            Def(l, WorldPiece.ColumnB, 80, 320, 2, p => Column(p, 3.3f, true));
            Def(l, WorldPiece.ArchMid, 256, 192, 1, RuinArchShape);
            Def(l, WorldPiece.WallWindow, 256, 192, 1, WallWindowShape);
            Def(l, WorldPiece.Stairs, 224, 128, 1, StairsShape);
            Def(l, WorldPiece.KelpTallA, 72, 360, 2, p => KelpStrand(p, 2.1f, true));
            Def(l, WorldPiece.KelpTallB, 72, 360, 2, p => KelpStrand(p, 6.7f, true));
            Def(l, WorldPiece.CanyonStrata, 128, 256, 1, CanyonStrataShape);

            // ---- ближний план
            Def(l, WorldPiece.RockA, 192, 144, 2, p => Rock(p, 1.7f, 0.5f, 0.6f, 0f));
            Def(l, WorldPiece.RockB, 192, 144, 2, p => Rock(p, 5.1f, 0.9f, 0.2f, 0f));
            Def(l, WorldPiece.RockC, 192, 144, 2, p => Rock(p, 9.4f, 0.2f, 0.9f, 0f));
            Def(l, WorldPiece.RockD, 192, 144, 2, p => Rock(p, 13.8f, 0.6f, 0.4f, 0f));
            Def(l, WorldPiece.CoralBranchA, 144, 176, 2, p => CoralBranch(p, 2.2f, MauveFill));
            Def(l, WorldPiece.CoralBranchB, 144, 176, 2, p => CoralBranch(p, 7.9f, OchreFill));
            Def(l, WorldPiece.FanA, 160, 160, 2, p => SeaFan(p, 1.1f, MauveFill));
            Def(l, WorldPiece.FanB, 160, 160, 2, p => SeaFan(p, 4.4f, SlateFill));
            Def(l, WorldPiece.Brain, 112, 88, 2, BrainCoral);
            Def(l, WorldPiece.AnemoneA, 64, 80, 2, p => Anemone(p, 1.5f, Palette.Hex("B0789A"), Palette.Hex("CCB8C0")));
            Def(l, WorldPiece.AnemoneB, 64, 80, 2, p => Anemone(p, 3.9f, Palette.Hex("6E9A8A"), Palette.Hex("B4CEC0")));
            Def(l, WorldPiece.TubesA, 64, 80, 2, p => TubeWorms(p, 2.5f, Palette.Hex("C8583E")));
            Def(l, WorldPiece.TubesB, 64, 80, 2, p => TubeWorms(p, 6.1f, Palette.Hex("D89A3E")));
            Def(l, WorldPiece.StarA, 48, 48, 2, p => Starfish(p, 0.3f, SlateFill));
            Def(l, WorldPiece.StarB, 48, 48, 2, p => Starfish(p, 1.1f, OchreFill));
            Def(l, WorldPiece.StarC, 48, 48, 2, p => Starfish(p, 2.0f, MauveFill));
            Def(l, WorldPiece.UrchinDecor, 40, 40, 2, UrchinSmall);
            Def(l, WorldPiece.ShellA, 40, 40, 2, Scallop);
            Def(l, WorldPiece.ShellB, 40, 40, 2, Conch);
            Def(l, WorldPiece.ShellC, 40, 40, 2, Spiral);
            Def(l, WorldPiece.Pebbles, 96, 32, 2, PebbleRow);
            Def(l, WorldPiece.SandA, 256, 48, 1, p => SandFloor(p, 1.3f));
            Def(l, WorldPiece.SandB, 256, 48, 1, p => SandFloor(p, 4.8f));
            Def(l, WorldPiece.Amphora, 56, 80, 2, AmphoraShape);
            Def(l, WorldPiece.ShardA, 48, 36, 2, p => Shard(p, 1.7f));
            Def(l, WorldPiece.ShardB, 48, 36, 2, p => Shard(p, 5.2f));
            Def(l, WorldPiece.DrumA, 80, 56, 2, p => Drum(p, 0.4f));
            Def(l, WorldPiece.DrumB, 80, 56, 2, p => Drum(p, 2.9f));
            Def(l, WorldPiece.Chain, 32, 160, 2, ChainShape);
            Def(l, WorldPiece.AnchorOld, 112, 144, 2, AnchorShape);
            Def(l, WorldPiece.RustPlate, 80, 56, 2, RustPlateShape);
            Def(l, WorldPiece.Barnacles, 64, 40, 2, BarnacleCluster);
            Def(l, WorldPiece.GrassA, 112, 96, 2, p => SeaGrass(p, 1.9f));
            Def(l, WorldPiece.GrassB, 112, 96, 2, p => SeaGrass(p, 5.6f));
            Def(l, WorldPiece.KelpShort, 56, 256, 2, p => KelpStrand(p, 9.3f, false));
            Def(l, WorldPiece.CrustRock, 128, 96, 2, p => Crust(p, 1.2f, RockFill, 0.2f));
            Def(l, WorldPiece.CrustCoral, 128, 96, 2, p => Crust(p, 4.7f, MauveFill, 0.7f));
            Def(l, WorldPiece.CrustSand, 128, 96, 2, p => Crust(p, 8.1f, SandFill, 0f));

            // ---- передний план
            Def(l, WorldPiece.FrontLeaf, 96, 256, 1, FrontLeafShape);
            Def(l, WorldPiece.FrontBubble, 96, 96, 1, p => BubbleShape(p, 0.5f));

            // ---- свет и частицы
            Def(l, WorldPiece.Shaft, 64, 256, 1, ShaftShape);
            Def(l, WorldPiece.Dot, 24, 24, 1, p => SoftDisc(p, 1f));
            Def(l, WorldPiece.Flake, 32, 32, 1, FlakeShape);
            Def(l, WorldPiece.BubbleRing, 40, 40, 2, p => BubbleShape(p, 1f));
            Def(l, WorldPiece.Glow, 64, 64, 1, p => SoftDisc(p, 0.5f));
            Def(l, WorldPiece.WindowBeam, 64, 192, 1, WindowBeamShape);
            return l;
        }

        // ---- дальний план

        /// <summary>Хребет: верхняя кромка из шума, низ уходит в туман.</summary>
        static Color Ridge(Vector2 p, float seed, float rough)
        {
            float u = p.x;                       // −1..1
            float top = 0.05f + (Fbm(u * 2.2f + seed, seed, 4) - 0.5f) * 1.1f * (0.6f + rough)
                        + 0.2f * Mathf.Sin(u * 1.7f + seed);
            top = Mathf.Clamp(top, -0.3f, 0.36f);
            if (p.y > top) return Color.clear;
            float fade = SStep(-0.38f, 0.1f, p.y);              // к низу растворяется
            float rim = SStep(top - 0.05f, top, p.y) * 0.35f;  // свет по гребню
            Color c = Color.Lerp(Mul(SilhouetteFill, 0.8f), Mul(SilhouetteFill, 1.1f), rim + 0.3f * fade);
            c.a = 0.35f + 0.65f * fade;
            return c;
        }

        /// <summary>Силуэт затонувшего города: башни, купола, проломы.</summary>
        static Color Skyline(Vector2 p, float seed)
        {
            float x = p.x;                         // −1..1
            float cell = Mathf.Floor((x + 1f) * 9f);
            float h = Hash(cell, seed);
            float top = -0.3f + h * 0.55f;
            float cx = (cell + 0.5f) / 9f - 1f;
            if (Hash(cell, seed + 1f) > 0.65f)    // купол
            {
                float r = 0.09f;
                if (Vector2.Distance(p, new Vector2(cx, top)) < r) top = p.y + 0.001f;
            }
            if (Hash(cell, seed + 2f) > 0.75f && Mathf.Abs(x - cx) < 0.018f) top += 0.12f;   // шпиль
            if (Hash(cell, seed + 3f) > 0.7f && p.y > top - 0.12f) top -= 0.08f * Hash(cell, 3.3f); // пролом
            if (p.y > top) return Color.clear;
            // окна: тёмные проёмы на светлом силуэте
            float wx = Mathf.Repeat((x + 1f) * 36f, 1f), wy = Mathf.Repeat((p.y + 1f) * 14f, 1f);
            bool window = wx > 0.35f && wx < 0.65f && wy > 0.3f && wy < 0.75f && p.y < top - 0.06f
                          && Hash(Mathf.Floor((x + 1f) * 36f), Mathf.Floor((p.y + 1f) * 14f) + seed) > 0.55f;
            float fade = SStep(-0.5f, 0.05f, p.y);
            Color c = Mul(SilhouetteFill, window ? 0.65f : 0.9f + 0.15f * SStep(top - 0.04f, top, p.y));
            c.a = 0.35f + 0.65f * fade;
            return c;
        }

        static Color FarKelpForest(Vector2 p)
        {
            // p.x в −0.5..0.5, p.y в −1..1
            float a = 0f;
            for (int i = 0; i < 6; i++)
            {
                float x0 = -0.42f + i * 0.17f + 0.04f * Mathf.Sin(i * 2.3f);
                float h = 0.45f + 0.5f * Hash(i, 2.7f);
                float x = x0 + 0.06f * Mathf.Sin(p.y * 3.1f + i * 1.7f) * (p.y + 1f) * 0.6f;
                float w = 0.022f + 0.012f * (1f - (p.y + 1f) * 0.5f);
                if (p.y < -1f + 2f * h && Mathf.Abs(p.x - x) < w) a = Mathf.Max(a, 0.9f);
                // листья — короткие овалы вдоль ствола
                float k = Mathf.Repeat((p.y + 1f) * 9f + i * 0.37f, 1f);
                float side = (Mathf.Floor((p.y + 1f) * 9f + i * 0.37f) % 2f) < 1f ? 1f : -1f;
                Vector2 q = new Vector2((p.x - x) * side - 0.05f, (k - 0.5f) * 0.22f);
                if (p.y < -1f + 2f * h - 0.04f && q.x > -0.03f && (q.x * q.x) / 0.0036f + (q.y * q.y) / 0.0016f < 1f)
                    a = Mathf.Max(a, 0.75f);
            }
            if (a <= 0f) return Color.clear;
            float fade = SStep(-1f, -0.2f, p.y);
            return WithA(Mul(SilhouetteFill, 0.85f), a * (0.3f + 0.7f * fade));
        }

        /// <summary>Природная арка: скала с проёмом посередине.</summary>
        static Color NaturalArch(Vector2 p, Color fill, float detail)
        {
            // p.x −1..1, p.y −0.67..0.67
            float outerTop = 0.55f - 0.25f * p.x * p.x + 0.06f * (Fbm(p.x * 4f, 1.3f) - 0.5f);
            if (p.y > outerTop || Mathf.Abs(p.x) > 0.98f) return Color.clear;
            float hole = (p.x * p.x) / 0.30f + ((p.y + 0.67f) * (p.y + 0.67f)) / 0.62f;
            if (hole < 1f) return Color.clear;
            float fade = SStep(-0.67f, 0f, p.y);
            Color c = Mul(fill, 0.8f + 0.25f * SStep(outerTop - 0.06f, outerTop, p.y));
            c.a = 0.4f + 0.6f * fade;
            return c;
        }

        // ---- жизнь

        static Color SchoolFishShape(Vector2 p)
        {
            // p.x −1..1, p.y −0.33..0.33; голова справа
            float body = (p.x * p.x) / 0.55f + (p.y * p.y) / 0.035f;
            bool tail = p.x < -0.62f && p.x > -0.98f && Mathf.Abs(p.y) < (-0.62f - p.x) * 0.8f + 0.02f;
            if (body > 1f && !tail) return Color.clear;
            float belly = SStep(-0.2f, 0.15f, p.y);
            Color c = Color.Lerp(Palette.Hex("9AAAB4"), Palette.Hex("5A6A78"), belly);
            return c;
        }

        static Color RayShape(Vector2 p)
        {
            // орляк: ромб крыльев, тонкий хвост; p.x −1..1, p.y −0.62..0.62
            float wing = Mathf.Abs(p.x) * 0.75f + Mathf.Abs(p.y - 0.05f + p.x * p.x * 0.25f) * 1.4f;
            bool tail = p.y < -0.05f && Mathf.Abs(p.x) < 0.02f + (p.y + 0.62f) * 0.03f && p.y > -0.62f;
            if (wing > 0.7f && !tail) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("4E5E6C"), Palette.Hex("7A8C98"), SStep(-0.2f, 0.3f, p.y));
            return c;
        }

        static Color JellyShape(Vector2 p)
        {
            // маленькая далёкая медуза: бледный купол и нити; p.x −0.67..0.67
            float bell = (p.x * p.x) / 0.30f + ((p.y - 0.45f) * (p.y - 0.45f)) / 0.16f;
            if (bell < 1f && p.y > 0.32f)
            {
                float rim = SStep(0.6f, 1f, bell);
                return new Color(0.78f, 0.84f, 0.9f, 0.45f + 0.35f * rim);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -0.3f + i * 0.2f + 0.05f * Mathf.Sin(p.y * 6f + i);
                if (p.y < 0.34f && p.y > -0.9f + 0.15f * Hash(i, 1f) && Mathf.Abs(p.x - x) < 0.025f)
                    return new Color(0.75f, 0.82f, 0.88f, 0.35f * SStep(-0.95f, 0.2f, p.y));
            }
            return Color.clear;
        }

        static Color WhaleShape(Vector2 p)
        {
            float u = p.x, v = p.y; // −1..1, −0.44..0.44
            float body = (u * u) / 0.62f + ((v + 0.02f + u * 0.06f) * (v + 0.02f + u * 0.06f)) / 0.055f;
            bool flukes = u < -0.74f && u > -1f && Mathf.Abs(v - (u + 0.74f) * 0.2f) < (-0.74f - u) * 0.9f;
            bool fin = u > -0.1f && u < 0.2f && v < -0.12f && v > -0.3f + (u + 0.1f) * 0.4f;
            if (body > 1f && !flukes && !fin) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("6A7C8A"), Palette.Hex("4A5866"), SStep(-0.1f, 0.2f, v));
            return c;
        }

        // ---- средний план

        static Color Spire(Vector2 p, float seed)
        {
            // p.x −0.375..0.375, p.y −1..1: столб с уступами и трещинами
            Func<Vector2, float> sdf = q =>
            {
                float w = 0.3f - 0.17f * SStep(-1f, 1f, q.y) + 0.05f * (Fbm(q.y * 5f, seed, 3) - 0.5f);
                // уступы
                float ledge = Mathf.Repeat(q.y * 2.3f + seed, 1f);
                if (ledge < 0.12f) w += 0.04f;
                float top = 0.92f - 0.1f * Hash(seed, 1f);
                return Mathf.Max(Mathf.Abs(q.x + 0.03f * Mathf.Sin(q.y * 3f + seed)) - w, q.y - top);
            };
            Color c = Lit(sdf, p, RockFill, 0.02f, 0.8f);
            if (c.a <= 0f) return c;
            float crack = Mathf.Abs(p.x - 0.08f * Mathf.Sin(p.y * 7f + seed) - 0.05f * (Fbm(p.y * 9f, seed) - 0.5f));
            if (crack < 0.008f && Hash(Mathf.Floor(p.y * 6f), seed) > 0.4f) c = Mul(c, 0.7f);
            if (Fbm(p.x * 9f, p.y * 9f + seed) > 0.63f) c = Color.Lerp(c, AlgaeFill, 0.35f);  // налёт
            return c;
        }

        static Color Column(Vector2 p, float seed, bool broken)
        {
            // p.x −0.25..0.25: каннелированная колонна с капителью и сколами
            float topY = broken ? 0.55f + 0.25f * (Fbm(p.x * 20f, seed) - 0.5f) : 0.92f;
            Func<Vector2, float> sdf = q =>
            {
                float w = 0.15f;
                if (q.y > 0.78f && !broken) w = 0.21f;           // капитель
                if (q.y < -0.88f) w = 0.22f;                      // база
                float t = broken ? 0.55f + 0.25f * (Fbm(q.x * 20f, seed) - 0.5f) : 0.92f;
                return Mathf.Max(Mathf.Abs(q.x) - w, Mathf.Max(q.y - t, -1f - q.y));
            };
            Color c = Lit(sdf, p, StoneFill, 0.015f);
            if (c.a <= 0f) return c;
            // каннелюры
            if (p.y < 0.78f && p.y > -0.88f)
            {
                float f = Mathf.Repeat((p.x + 0.15f) * 23f, 1f);
                if (f < 0.25f) c = Mul(c, 0.82f);
            }
            if (Fbm(p.x * 12f + seed, p.y * 6f) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.4f);
            if (p.y > topY - 0.05f && broken) c = Mul(c, 1.12f);
            return c;
        }

        static Color RuinArchShape(Vector2 p)
        {
            // p.x −1..1, p.y −0.75..0.75: каменная арка из блоков с замковым камнем
            float outer = (p.x * p.x) / 0.85f + ((p.y + 0.75f) * (p.y + 0.75f)) / 2.0f;
            float inner = (p.x * p.x) / 0.42f + ((p.y + 0.75f) * (p.y + 0.75f)) / 1.05f;
            bool pier = Mathf.Abs(p.x) > 0.62f && Mathf.Abs(p.x) < 0.9f && p.y < 0.1f;
            bool ring = outer < 1f && inner > 1f;
            if (!ring && !pier) return Color.clear;
            // разлом справа сверху
            if (p.x > 0.25f && p.y > 0.35f + 0.1f * Fbm(p.x * 8f, 2f)) return Color.clear;
            float ang = Mathf.Atan2(p.y + 0.75f, p.x);
            float block = ring ? Mathf.Repeat(ang * 4.8f, 1f) : Mathf.Repeat((p.y + 0.75f) * 6f, 1f);
            Color c = Color.Lerp(Mul(StoneFill, 0.78f), StoneFill, SStep(-0.75f, 0.6f, p.y));
            if (block < 0.06f) c = Mul(c, 0.66f);
            if (ring && Mathf.Abs(ang - Mathf.PI * 0.5f) < 0.12f) c = Mul(c, 1.12f);        // замковый камень
            if (Fbm(p.x * 7f, p.y * 7f) > 0.62f) c = Color.Lerp(c, AlgaeFill, 0.4f);
            if (outer > 0.9f && p.y > 0f) c = Mul(c, 1.15f);                                  // свет по верху
            c.a = 1f;
            return c;
        }

        static Color WallWindowShape(Vector2 p)
        {
            // стена с арочным окном и обвалившимся верхом; p.x −1..1, p.y −0.75..0.75
            float top = 0.55f + 0.18f * (Fbm(p.x * 3f, 4.1f) - 0.5f) - 0.2f * SStep(0.3f, 1f, p.x);
            if (p.y > top || Mathf.Abs(p.x) > 0.96f) return Color.clear;
            float wx = p.x + 0.15f, wy = p.y + 0.05f;
            bool window = Mathf.Abs(wx) < 0.2f && wy > -0.35f && (wy < 0.12f || (wx * wx + (wy - 0.12f) * (wy - 0.12f)) < 0.04f);
            if (window) return Color.clear;
            float bx = Mathf.Repeat(p.x * 5f + (Mathf.Floor(p.y * 7f) % 2f) * 0.5f, 1f);
            float by = Mathf.Repeat(p.y * 7f, 1f);
            Color c = Color.Lerp(Mul(StoneFill, 0.74f), StoneFill, SStep(-0.75f, 0.6f, p.y));
            if (bx < 0.05f || by < 0.07f) c = Mul(c, 0.7f);
            if (Fbm(p.x * 8f, p.y * 8f + 2f) > 0.62f) c = Color.Lerp(c, AlgaeFill, 0.4f);
            if (p.y > top - 0.04f) c = Mul(c, 1.15f);
            c.a = 1f;
            return c;
        }

        static Color StairsShape(Vector2 p)
        {
            // ступени, уходящие вправо вверх; p.x −1..1, p.y −0.57..0.57
            float step = Mathf.Floor((p.x + 1f) * 3.5f);
            float top = -0.5f + step * 0.16f;
            if (p.y > top || p.x > 0.95f) return Color.clear;
            if (step > 4f && Hash(step, 2f) > 0.5f && p.y > top - 0.08f) return Color.clear;     // сколы
            Color c = Color.Lerp(Mul(StoneFill, 0.72f), StoneFill, SStep(-0.57f, 0.5f, p.y));
            if (p.y > top - 0.03f) c = Mul(c, 1.18f);
            if (Mathf.Repeat((p.x + 1f) * 3.5f, 1f) < 0.05f) c = Mul(c, 0.7f);
            if (Fbm(p.x * 9f, p.y * 9f) > 0.64f) c = Color.Lerp(c, AlgaeFill, 0.45f);
            c.a = 1f;
            return c;
        }

        /// <summary>Ламинария: ствол с листьями, у основания шире. Качается от низа.</summary>
        static Color KelpStrand(Vector2 p, float seed, bool tall)
        {
            // p.x −0.2..0.2, p.y −1..1
            float k = (p.y + 1f) * 0.5f;
            float x = 0.035f * Mathf.Sin(p.y * 4.1f + seed) * k;
            float stem = Mathf.Abs(p.x - x) - (0.012f + 0.01f * (1f - k));
            Color dark = Palette.Hex("2E5238"), light = Palette.Hex("5C8A48");
            if (stem < 0f && p.y < 0.96f) return Color.Lerp(dark, light, 0.4f * k);
            // листья
            float n = (p.y + 1f) * (tall ? 7f : 5f) + seed;
            float idx = Mathf.Floor(n), f = n - idx;
            float side = (idx % 2f) < 1f ? 1f : -1f;
            float lx = (p.x - x) * side;
            float len = 0.14f + 0.05f * Hash(idx, seed);
            float wv = (f - 0.5f) * 0.28f + lx * 0.4f;
            if (lx > 0f && lx < len && Mathf.Abs(wv) < 0.035f * Mathf.Sin(Mathf.PI * Sat(lx / len)) + 0.004f && p.y < 0.9f)
            {
                Color c = Color.Lerp(dark, light, 0.3f + 0.5f * (lx / len));
                if (Mathf.Abs(wv) < 0.006f) c = Mul(c, 0.8f);   // жилка
                return c;
            }
            return Color.clear;
        }

        static Color CanyonStrataShape(Vector2 p)
        {
            // слоистая стена каньона, внутренний край справа; p.x −0.5..0.5
            float edge = 0.32f + 0.1f * (Fbm(p.y * 3f, 6.3f) - 0.5f) + 0.06f * Mathf.Sin(p.y * 9f);
            if (p.x > edge) return Color.clear;
            float band = Mathf.Repeat(p.y * 6f + 0.3f * Fbm(p.x * 3f, p.y * 2f), 1f);
            Color c = Color.Lerp(Palette.Hex("4E4A54"), Palette.Hex("6E6470"), band);
            c = Color.Lerp(Mul(c, 0.7f), c, SStep(-0.5f, edge, p.x));         // к краю светлее
            if (p.x > edge - 0.025f) c = Mul(c, 1.2f);
            c.a = 1f;
            return c;
        }

        // ---- ближний план

        /// <summary>
        /// Валун: неровный контур из шума, трещины, сверху налёт водорослей,
        /// по низу ракушечный нарост. Свет сверху и чуть слева.
        /// </summary>
        static Color Rock(Vector2 p, float seed, float algae, float barnacles, float frost)
        {
            // p.x −1..1, p.y −0.75..0.75
            Func<Vector2, float> sdf = q =>
            {
                float ang = Mathf.Atan2(q.y + 0.2f, q.x);
                float r = 0.62f + 0.16f * (Fbm(Mathf.Cos(ang) * 1.8f + seed, Mathf.Sin(ang) * 1.8f, 3) - 0.5f) * 2f;
                Vector2 c = new Vector2(q.x * 0.78f, (q.y + 0.2f) * 1.25f);
                return Mathf.Max(c.magnitude - r, -0.75f - q.y + 0.02f);
            };
            Color col = Lit(sdf, p, RockFill, 0.025f);
            if (col.a <= 0f) return col;
            float n = Fbm(p.x * 6f + seed, p.y * 6f, 3);
            col = Mul(col, 0.9f + 0.2f * n);
            // трещины — сеть по линии уровня шума, тоньше и тише к краю; одна
            // синусоида поперёк камня читалась как «бровь» [ТЕСТ, альбом]
            float crack = Mathf.Abs(Fbm(p.x * 2.6f + seed * 1.7f, p.y * 2.6f, 3) - 0.5f);
            if (crack < 0.011f * (1f - Mathf.Abs(p.x) * 0.6f)) col = Mul(col, 0.8f);
            // водоросли на верхних гранях
            float topness = SStep(-0.1f, 0.45f, p.y);
            if (algae > 0f && Fbm(p.x * 10f, p.y * 10f + seed) * topness > 0.55f - algae * 0.25f)
                col = Color.Lerp(col, Mul(AlgaeFill, 1.1f), 0.65f);
            // ракушечный нарост по низу
            // ракушечный нарост — кучками там, где шум густеет, а не ровным пояском
            if (barnacles > 0f && p.y < -0.1f && Fbm(p.x * 3f + seed, p.y * 3f, 2) > 0.62f - barnacles * 0.08f)
            {
                Vector2 cell = new Vector2(Mathf.Floor(p.x * 16f), Mathf.Floor(p.y * 16f));
                Vector2 f = new Vector2(p.x * 16f - cell.x - 0.5f, p.y * 16f - cell.y - 0.5f);
                float jx = Hash(cell.x + 3f, cell.y) - 0.5f, jy = Hash(cell.x, cell.y + 5f) - 0.5f;
                f -= new Vector2(jx, jy) * 0.4f;
                if (Hash(cell.x, cell.y + seed) < 0.55f && f.magnitude < 0.3f)
                    // бугорок, а не «глаз»: без тёмной сердцевины, светлее сверху
                    col = Color.Lerp(col, Mul(BarnacleFill, 0.8f + 0.25f * Sat(-f.y * 2f + 0.5f)), 0.35f * (1f - f.magnitude / 0.3f) + 0.1f);
            }
            if (frost > 0f && p.y > 0.1f && Fbm(p.x * 8f, p.y * 4f) * topness > 0.5f - frost * 0.3f)
                col = Color.Lerp(col, Palette.Hex("A6C4D2"), 0.7f);
            col.a = 1f;
            return col;
        }

        static Color CoralBranch(Vector2 p, float seed, Color fill)
        {
            // p.x −0.82..0.82, p.y −1..1: ветвистый мягкий коралл из сегментов
            float best = 9f;
            Vector2 root = new Vector2(0f, -0.95f);
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2f) * 0.38f + 0.12f * Mathf.Sin(seed + i);
                Vector2 mid = root + new Vector2(Mathf.Sin(a) * 0.45f, 0.75f);
                Vector2 tip = mid + new Vector2(Mathf.Sin(a * 1.6f) * 0.25f, 0.65f + 0.25f * Hash(i, seed));
                best = Mathf.Min(best, SegDist(p, root, mid) - 0.07f);
                best = Mathf.Min(best, SegDist(p, mid, tip) - 0.05f);
                Vector2 twig = mid + new Vector2(Mathf.Sin(a + 1.2f) * 0.28f, 0.35f);
                best = Mathf.Min(best, SegDist(p, mid, twig) - 0.04f);
            }
            float d = best;
            if (d > 0f) return Color.clear;
            Color c = Color.Lerp(Mul(fill, 0.75f), Mul(fill, 1.15f), SStep(-1f, 1f, p.y));
            if (d > -0.015f) c = Mul(fill, 0.6f);
            // полипы — светлые точки
            if (Hash(Mathf.Floor(p.x * 30f), Mathf.Floor(p.y * 30f) + seed) > 0.88f) c = Mul(c, 1.25f);
            c.a = 1f;
            return c;
        }

        static Color SeaFan(Vector2 p, float seed, Color fill)
        {
            // веер-горгонария: сетка жилок внутри овала; p.x −1..1, p.y −1..1
            float shape = (p.x * p.x) / 0.8f + ((p.y - 0.15f) * (p.y - 0.15f)) / 0.72f;
            bool stalk = Mathf.Abs(p.x) < 0.04f && p.y < -0.4f && p.y > -1f;
            if (shape > 1f && !stalk) return Color.clear;
            if (stalk) return Mul(fill, 0.7f);
            float ang = Mathf.Atan2(p.y + 0.9f, p.x);
            float rad = Vector2.Distance(p, new Vector2(0f, -0.9f));
            bool rib = Mathf.Repeat(ang * 9f + Fbm(rad * 4f, seed) * 0.6f, 1f) < 0.16f;
            bool cross = Mathf.Repeat(rad * 11f + Fbm(ang * 3f, seed) * 0.8f, 1f) < 0.12f;
            if (!rib && !cross) return shape > 0.92f ? WithA(Mul(fill, 0.8f), 0.6f) : Color.clear;
            Color c = Color.Lerp(Mul(fill, 0.75f), Mul(fill, 1.2f), SStep(-0.5f, 1f, p.y));
            c.a = 1f;
            return c;
        }

        static Color BrainCoral(Vector2 p)
        {
            Func<Vector2, float> sdf = q => new Vector2(q.x * 0.9f, (q.y + 0.35f) * 1.4f).magnitude - 0.78f;
            Color c = Lit(sdf, p, Palette.Hex("8A8452"), 0.03f);
            if (c.a <= 0f || p.y < -0.78f) return Color.clear;
            float maze = Mathf.Sin(p.x * 22f + Mathf.Sin(p.y * 9f) * 3f) * Mathf.Sin(p.y * 20f + Mathf.Sin(p.x * 7f) * 2f);
            if (maze > 0.25f) c = Mul(c, 0.78f);
            return c;
        }

        static Color Anemone(Vector2 p, float seed, Color body, Color tips)
        {
            // p.x −0.8..0.8, p.y −1..1: столбик и венчик щупалец
            if (p.y < -0.2f)
            {
                float w = 0.22f + 0.08f * (p.y + 1f);
                if (Mathf.Abs(p.x) < w && p.y > -1f) return Color.Lerp(Mul(body, 0.65f), body, SStep(-1f, -0.2f, p.y));
                return Color.clear;
            }
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-1.25f, 1.25f, i / 8f) + 0.1f * Mathf.Sin(seed + i);
                Vector2 root = new Vector2(Mathf.Sin(a) * 0.12f, -0.2f);
                Vector2 tip = new Vector2(Mathf.Sin(a) * 0.72f, -0.2f + Mathf.Cos(a) * 0.95f + 0.1f * Hash(i, seed));
                float d = SegDist(p, root, tip);
                float t = Sat(Vector2.Dot(p - root, tip - root) / (tip - root).sqrMagnitude);
                if (d < 0.055f * (1f - t * 0.5f)) return Color.Lerp(body, tips, SStep(0.4f, 1f, t));
            }
            return Color.clear;
        }

        static Color TubeWorms(Vector2 p, float seed, Color plume)
        {
            for (int i = 0; i < 5; i++)
            {
                float baseX = -0.5f + i * 0.25f + 0.08f * (Hash(i, seed) - 0.5f);
                float top = -0.25f + 0.55f * Hash(i + 3f, seed);
                float lean = (Hash(i + 7f, seed) - 0.5f) * 0.25f;
                float t = Sat((p.y + 1f) / (top + 1f));
                float x = baseX + lean * t * t;
                float half = Mathf.Lerp(0.06f, 0.04f, t);
                if (Mathf.Abs(p.x - x) < half && p.y < top && p.y > -1f)
                {
                    Color c = Color.Lerp(Palette.Hex("4A4438"), Palette.Hex("8A7F68"), t);
                    if (Mathf.Repeat(p.y * 11f + i * 0.3f, 1f) < 0.18f) c = Mul(c, 0.8f);   // кольца трубки
                    if (p.x - x > half * 0.3f) c = Mul(c, 0.82f);                            // теневая сторона
                    return c;
                }
                // венчик — веер нитей, раскрытый вверх
                Vector2 q = new Vector2(p.x - (baseX + lean), p.y - top);
                float r = q.magnitude;
                float ang = Mathf.Atan2(q.x, q.y);                     // 0 — строго вверх
                if (r < 0.2f && r > 0.01f && Mathf.Abs(ang) < 1.25f)
                {
                    float fil = Mathf.Abs(Mathf.Cos(ang * 6f + seed));
                    if (fil > 0.45f || r < 0.04f)
                    {
                        Color c = Color.Lerp(Mul(plume, 0.6f), plume, r / 0.2f);
                        c.a = 1f - SStep(0.13f, 0.2f, r) * 0.7f;
                        return c;
                    }
                }
            }
            return Color.clear;
        }

        static Color Starfish(Vector2 p, float rot, Color fill)
        {
            float ang = Mathf.Atan2(p.y, p.x) + rot;
            float r = p.magnitude;
            float arm = 0.42f + 0.48f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f)), 3f);
            if (r > arm * 0.95f) return Color.clear;
            Color c = Color.Lerp(Mul(fill, 1.15f), Mul(fill, 0.75f), r / arm);
            if (Hash(Mathf.Floor(p.x * 18f), Mathf.Floor(p.y * 18f)) > 0.75f) c = Mul(c, 1.2f);   // бугорки
            if (r > arm * 0.82f) c = Mul(fill, 0.6f);
            c.a = 1f;
            return c;
        }

        static Color UrchinSmall(Vector2 p)
        {
            // маленький, зелёно-бурый, с короткими иглами — не похож на ежа-угрозу
            float r = p.magnitude;
            if (r < 0.5f) return Color.Lerp(Palette.Hex("5A6A3E"), Palette.Hex("3A4428"), r / 0.5f);
            float ang = Mathf.Atan2(p.y, p.x);
            float spike = Mathf.Abs(Mathf.Sin(ang * 9f));
            if (r < 0.5f + 0.32f * Mathf.Pow(spike, 8f)) return Palette.Hex("4A5432");
            return Color.clear;
        }

        static Color Scallop(Vector2 p)
        {
            float r = Vector2.Distance(p, new Vector2(0f, -0.6f));
            float ang = Mathf.Atan2(p.y + 0.6f, p.x);
            if (r > 1.25f || ang < 0.45f || ang > Mathf.PI - 0.45f || p.y < -0.62f) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("A89480"), Palette.Hex("D2C2A8"), r / 1.25f);
            if (Mathf.Repeat(ang * 6f, 1f) < 0.18f) c = Mul(c, 0.78f);
            return c;
        }

        static Color Conch(Vector2 p)
        {
            Vector2 q = new Vector2(p.x * 0.8f - p.y * 0.3f, p.y);
            float body = (q.x * q.x) / 0.18f + (q.y * q.y) / 0.7f;
            if (body > 1f) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("B89A82"), Palette.Hex("D6C3AE"), SStep(-0.8f, 0.8f, p.y));
            if (Mathf.Repeat(q.y * 5f + q.x * 2f, 1f) < 0.12f) c = Mul(c, 0.8f);
            return c;
        }

        static Color Spiral(Vector2 p)
        {
            float r = p.magnitude;
            if (r > 0.9f) return Color.clear;
            float ang = Mathf.Atan2(p.y, p.x);
            float s = Mathf.Repeat(ang / (Mathf.PI * 2f) + r * 2.2f, 1f);
            Color c = Color.Lerp(Palette.Hex("9A8A78"), Palette.Hex("D0C0AA"), s);
            if (s < 0.1f) c = Mul(c, 0.7f);
            return c;
        }

        static Color PebbleRow(Vector2 p)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = new Vector2(-0.85f + i * 0.34f + 0.05f * Hash(i, 1f), -0.2f + 0.05f * Hash(i, 2f));
                Vector2 r = new Vector2(0.1f + 0.06f * Hash(i, 3f), 0.08f + 0.03f * Hash(i, 4f));
                Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
                if (q.sqrMagnitude < 1f)
                {
                    Color col = Color.Lerp(Palette.Hex("6A6E6E"), Palette.Hex("9A9890"), Hash(i, 5f));
                    return Mul(col, 0.8f + 0.3f * SStep(-1f, 1f, q.y));
                }
            }
            return Color.clear;
        }

        static Color SandFloor(Vector2 p, float seed)
        {
            // полоса дна с песчаной рябью; p.x −1..1, p.y −0.19..0.19
            float top = 0.05f + 0.06f * (Fbm(p.x * 3f, seed) - 0.5f) + 0.02f * Mathf.Sin(p.x * 18f);
            if (p.y > top) return Color.clear;
            float ripple = Mathf.Sin(p.x * 40f + Mathf.Sin(p.y * 30f) * 2f + seed);
            Color c = Color.Lerp(Mul(SandFill, 0.7f), SandFill, SStep(-0.19f, top, p.y));
            if (ripple > 0.6f) c = Mul(c, 1.12f);
            if (Hash(Mathf.Floor(p.x * 60f), Mathf.Floor(p.y * 60f)) > 0.93f) c = Mul(c, 0.8f);
            c.a = SStep(-0.19f, -0.12f, p.y) * 0.6f + 0.4f;
            return c;
        }

        static Color AmphoraShape(Vector2 p)
        {
            // амфора лёжа на боку не читается — стоит, с отбитым горлом; p.x −0.7..0.7
            Func<Vector2, float> sdf = q =>
            {
                float w = 0.38f * Mathf.Sin(Mathf.PI * Sat((q.y + 0.95f) / 1.5f)) + 0.04f;
                if (q.y > 0.5f) w = 0.12f;
                float d = Mathf.Abs(q.x) - w;
                return Mathf.Max(d, Mathf.Max(q.y - 0.78f + 0.08f * Sat(q.x * 3f), -0.95f - q.y));
            };
            Color c = Lit(sdf, p, Palette.Hex("8A5E48"), 0.03f);
            if (c.a <= 0f) return c;
            if (Mathf.Abs(p.y + 0.1f) < 0.03f) c = Mul(c, 0.75f);                  // поясок
            if (Fbm(p.x * 9f, p.y * 9f) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.5f);
            return c;
        }

        static Color Shard(Vector2 p, float seed)
        {
            // обломок черепка: изогнутая пластина
            float bend = p.y - 0.25f * p.x * p.x + 0.1f;
            float edge = 0.45f + 0.1f * (Fbm(p.x * 5f, seed) - 0.5f);
            if (Mathf.Abs(bend) > 0.16f || Mathf.Abs(p.x) > edge * 1.6f) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("6E4A3A"), Palette.Hex("9A6A50"), SStep(-0.16f, 0.16f, bend));
            if (bend > 0.12f) c = Mul(c, 1.15f);
            return c;
        }

        static Color Drum(Vector2 p, float seed)
        {
            // барабан колонны лежит на дне: цилиндр сбоку с каннелюрами и торцом
            bool face = ((p.x - 0.62f) * (p.x - 0.62f)) / 0.04f + (p.y * p.y) / 0.42f < 1f;
            bool body = Mathf.Abs(p.y) < 0.62f && p.x > -0.85f && p.x < 0.62f;
            if (!face && !body) return Color.clear;
            if (face) return Color.Lerp(Mul(StoneFill, 0.85f), Mul(StoneFill, 1.1f), SStep(-0.6f, 0.6f, p.y));
            Color c = Color.Lerp(Mul(StoneFill, 0.65f), Mul(StoneFill, 1.05f), SStep(-0.62f, 0.62f, p.y));
            if (Mathf.Repeat(p.y * 6f, 1f) < 0.18f) c = Mul(c, 0.82f);
            if (Fbm(p.x * 8f + seed, p.y * 8f) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.45f);
            return c;
        }

        static Color ChainShape(Vector2 p)
        {
            // висящая цепь со звеньями поочерёдно анфас и в профиль; p.x −0.2..0.2
            float y = Mathf.Repeat((p.y + 1f) * 5f, 1f);
            bool flat = (Mathf.Floor((p.y + 1f) * 5f) % 2f) < 1f;
            Vector2 q = new Vector2(p.x / (flat ? 0.12f : 0.045f), (y - 0.5f) / 0.55f);
            float r = q.magnitude;
            if (r > 1f || (flat && r < 0.55f)) return Color.clear;
            Color c = Color.Lerp(Palette.Hex("4A4440"), Palette.Hex("7A6A5A"), SStep(-1f, 1f, q.x * -0.5f + q.y * 0.5f));
            if (Fbm(p.x * 20f, p.y * 10f) > 0.62f) c = Color.Lerp(c, AlgaeFill, 0.6f);
            return c;
        }

        static Color AnchorShape(Vector2 p)
        {
            // старый якорь, наполовину в песке, зарос; p.x −0.78..0.78
            float shank = SegDist(p, new Vector2(0f, -0.7f), new Vector2(0.05f, 0.75f)) - 0.07f;
            float stock = SegDist(p, new Vector2(-0.35f, 0.62f), new Vector2(0.42f, 0.66f)) - 0.05f;
            float ringD = Mathf.Abs(Vector2.Distance(p, new Vector2(0.06f, 0.88f)) - 0.09f) - 0.03f;
            float arm = Mathf.Abs(Vector2.Distance(p, new Vector2(0f, -0.25f)) - 0.48f) - 0.06f;
            if (p.y > -0.35f) arm = 9f;
            float d = Mathf.Min(Mathf.Min(shank, stock), Mathf.Min(ringD, arm));
            if (d > 0f || p.y < -0.85f) return Color.clear;
            Color c = Color.Lerp(RustFill, Mul(RustFill, 1.3f), SStep(-0.8f, 0.8f, p.y));
            if (d > -0.015f) c = Mul(RustFill, 0.6f);
            if (Fbm(p.x * 12f, p.y * 12f) > 0.58f) c = Color.Lerp(c, AlgaeFill, 0.55f);
            if (p.y < -0.62f) c = Color.Lerp(c, SandFill, 0.6f);   // утонул в песке
            return c;
        }

        static Color RustPlateShape(Vector2 p)
        {
            float edge = 0.62f + 0.08f * (Fbm(p.x * 4f, 2.2f) - 0.5f);
            if (Mathf.Abs(p.y) > edge || Mathf.Abs(p.x) > 0.92f) return Color.clear;
            Color c = Color.Lerp(Mul(RustFill, 0.8f), Mul(RustFill, 1.2f), Fbm(p.x * 6f, p.y * 6f));
            // заклёпки по краю
            if (Mathf.Abs(Mathf.Abs(p.y) - edge + 0.12f) < 0.06f && Mathf.Repeat(p.x * 5f, 1f) < 0.25f) c = Mul(c, 1.35f);
            if (Fbm(p.x * 10f, p.y * 10f + 3f) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.5f);
            return c;
        }

        static Color BarnacleCluster(Vector2 p)
        {
            for (int i = 0; i < 7; i++)
            {
                Vector2 c = new Vector2(-0.7f + Hash(i, 1f) * 1.4f, -0.3f + Hash(i, 2f) * 0.45f);
                float r = 0.13f + 0.08f * Hash(i, 3f);
                float d = Vector2.Distance(p, c);
                if (d < r)
                {
                    // конусок с узкой щелью сверху — бугорок, а не глаз
                    Vector2 q = (p - c) / r;
                    if (Mathf.Abs(q.x) < 0.35f && Mathf.Abs(q.y - 0.15f) < 0.08f) return Mul(BarnacleFill, 0.6f);
                    return Color.Lerp(Mul(BarnacleFill, 0.72f), Mul(BarnacleFill, 1.05f), SStep(-1f, 1f, q.y));
                }
            }
            return Color.clear;
        }

        static Color SeaGrass(Vector2 p, float seed)
        {
            for (int i = 0; i < 9; i++)
            {
                float x0 = -0.75f + i * 0.19f + 0.05f * Hash(i, seed);
                float h = 0.3f + 0.6f * Hash(i + 1f, seed);
                float t = Sat((p.y + 0.85f) / (h + 0.85f));
                float x = x0 + 0.25f * t * t * (Hash(i, seed + 2f) - 0.5f);
                if (p.y > -0.86f && p.y < h && Mathf.Abs(p.x - x) < 0.03f * (1f - t * 0.7f))
                    return Color.Lerp(Palette.Hex("2F5A3A"), Palette.Hex("6A9A50"), t);
            }
            return Color.clear;
        }

        /// <summary>Корка по внутреннему краю стены локации: бугры, наросты.</summary>
        static Color Crust(Vector2 p, float seed, Color fill, float polyps)
        {
            float top = 0.3f + 0.25f * (Fbm(p.x * 3f, seed) - 0.5f) + 0.08f * Mathf.Sin(p.x * 11f + seed);
            if (p.y > top || p.y < -0.74f) return Color.clear;
            Color c = Color.Lerp(Mul(fill, 0.62f), Mul(fill, 1.05f), SStep(-0.74f, top, p.y));
            c = Mul(c, 0.88f + 0.24f * Fbm(p.x * 8f, p.y * 8f + seed));
            if (p.y > top - 0.04f) c = Mul(c, 1.2f);
            if (polyps > 0f && Hash(Mathf.Floor(p.x * 24f), Mathf.Floor(p.y * 24f) + seed) > 1f - polyps * 0.15f)
                c = Mul(c, 1.3f);
            c.a = SStep(-0.74f, -0.55f, p.y) * 0.7f + 0.3f;
            return c;
        }

        // ---- передний план

        static Color FrontLeafShape(Vector2 p)
        {
            // большой лист ламинарии вне фокуса: мягкий край, тёмный
            float x = 0.12f * Mathf.Sin(p.y * 2.2f);
            float w = 0.28f * Mathf.Sin(Mathf.PI * Sat((p.y + 1f) * 0.5f)) + 0.02f;
            float d = Mathf.Abs(p.x - x) / w;
            if (d > 1f) return Color.clear;
            return new Color(0.16f, 0.26f, 0.22f, 0.85f * SStep(1f, 0.55f, d));
        }

        static Color BubbleShape(Vector2 p, float softness)
        {
            float r = p.magnitude;
            if (r > 0.95f) return Color.clear;
            float ring = SStep(0.62f - 0.2f * softness, 0.9f, r) * SStep(0.97f, 0.88f, r);
            float spec = SStep(0.28f, 0.0f, Vector2.Distance(p, new Vector2(-0.32f, 0.36f)));
            float a = Sat(ring * 0.75f + spec * 0.85f + 0.06f);
            return new Color(0.72f, 0.78f, 0.80f, a * (1f - 0.4f * softness));   // сумма 2.30
        }

        // ---- свет и частицы

        /// <summary>Луч: вертикальная полоса с мягкими краями, гаснет книзу.</summary>
        static Color ShaftShape(Vector2 p)
        {
            // p.x −0.25..0.25, p.y −1..1
            float across = Sat(1f - Mathf.Abs(p.x) / 0.25f);
            float soft = across * across * (3f - 2f * across);
            float down = SStep(-1f, 0.9f, p.y);
            return new Color(1f, 1f, 1f, soft * down);
        }

        static Color SoftDisc(Vector2 p, float hardness)
        {
            float r = p.magnitude;
            if (r >= 1f) return Color.clear;
            float a = Mathf.Pow(1f - r, 1.2f + hardness * 1.5f);
            return new Color(1f, 1f, 1f, a);
        }

        static Color FlakeShape(Vector2 p)
        {
            // морской снег: мягкий неровный комочек — на экране в несколько точек,
            // поэтому форма только чуть «живая», а края размыты
            float ang = Mathf.Atan2(p.y, p.x);
            float r = p.magnitude * (1f + 0.1f * Mathf.Sin(ang * 2f + 0.7f) + 0.06f * Mathf.Sin(ang * 5f));
            if (r >= 0.95f) return Color.clear;
            return new Color(1f, 1f, 1f, SStep(0.95f, 0.0f, r) * 0.9f);
        }

        static Color WindowBeamShape(Vector2 p)
        {
            // косой свет из окна руин: трапеция, расширяется книзу
            float w = 0.06f + 0.14f * SStep(1f, -1f, p.y);
            float d = Mathf.Abs(p.x + p.y * 0.08f) / w;
            if (d >= 1f) return Color.clear;
            return new Color(1f, 1f, 1f, SStep(1f, 0.2f, d) * SStep(-1f, 0.6f, p.y) * SStep(1f, 0.8f, p.y));
        }

        // ------------------------------------------------------------ лёд

        // Лёд — самое светлое в мире, поэтому его палитра сразу под потолком
        // «сумма каналов ≤ 2.3»: кромка AACAD6 — 2.30, светлый A0BED0 — 2.19.
        static readonly Color IceLight = Palette.Hex("A0BED0");
        static readonly Color IceMid = Palette.Hex("7FA4BE");
        static readonly Color IceDeep = Palette.Hex("587E9C");
        static readonly Color IceShadow = Palette.Hex("24425E");
        static readonly Color IceRim = Palette.Hex("AACAD6");

        static List<PieceDef> IceDefs()
        {
            var l = new List<PieceDef>();
            Def(l, IcePiece.FloeA, 320, 112, 2, p => Floe(p, 1.4f));
            Def(l, IcePiece.FloeB, 320, 112, 2, p => Floe(p, 5.8f));
            Def(l, IcePiece.FloeC, 320, 112, 2, p => Floe(p, 9.1f));
            Def(l, IcePiece.IcicleA, 48, 176, 2, p => Icicle(p, 2.3f));
            Def(l, IcePiece.IcicleB, 48, 176, 2, p => Icicle(p, 7.4f));
            Def(l, IcePiece.CrystalA, 112, 144, 2, p => Crystal(p, 1.1f, 4));
            Def(l, IcePiece.CrystalB, 112, 144, 2, p => Crystal(p, 4.6f, 5));
            Def(l, IcePiece.CrystalC, 112, 144, 2, p => Crystal(p, 8.8f, 3));
            Def(l, IcePiece.FrozenColumn, 80, 320, 2, FrozenColumnShape);
            Def(l, IcePiece.ShelfA, 224, 176, 2, p => Shelf(p, 2.6f));
            Def(l, IcePiece.ShelfB, 224, 176, 2, p => Shelf(p, 6.9f));
            Def(l, IcePiece.Drift, 320, 72, 1, DriftShape);
            Def(l, IcePiece.FrozenKelp, 56, 288, 2, FrozenKelpShape);
            Def(l, IcePiece.BoulderA, 160, 112, 2, p => IceBoulder(p, 3.3f));
            Def(l, IcePiece.BoulderB, 160, 112, 2, p => IceBoulder(p, 8.4f));
            Def(l, IcePiece.IceFish, 56, 20, 2, IceFishShape);
            Def(l, IcePiece.IceArch, 320, 224, 1, IceArchShape);
            Def(l, IcePiece.FrostRock, 192, 144, 2, p => Rock(p, 21.7f, 0f, 0.1f, 1f));
            return l;
        }

        /// <summary>Льдина у потолка: плоский верх, рваный низ с наплывами и сосульками.</summary>
        static Color Floe(Vector2 p, float seed)
        {
            // p.x −1..1, p.y −0.35..0.35
            float bottom = -0.05f - 0.12f * Fbm(p.x * 3f, seed, 3) - 0.18f * Mathf.Pow(Mathf.Abs(Mathf.Sin(p.x * 7f + seed)), 6f);
            float top = 0.3f;
            if (p.y < bottom || p.y > top || Mathf.Abs(p.x) > 0.97f - 0.08f * Hash(seed, 1f)) return Color.clear;
            float depthIn = SStep(bottom, top, p.y);
            Color c = Color.Lerp(IceMid, IceLight, depthIn);
            // свет сверху проходит сквозь лёд: прожилки и пузырьки
            if (Fbm(p.x * 7f, p.y * 14f + seed) > 0.62f) c = Color.Lerp(c, IceRim, 0.45f);
            if (Hash(Mathf.Floor(p.x * 40f), Mathf.Floor(p.y * 40f) + seed) > 0.97f) c = IceRim;
            if (p.y < bottom + 0.03f) c = Color.Lerp(c, IceDeep, 0.6f);   // нижняя кромка темнее
            c.a = 1f;
            return c;
        }

        static Color Icicle(Vector2 p, float seed)
        {
            // p.x −0.27..0.27, p.y −1..1: сверху широко, вниз остриём
            float t = Sat((p.y + 1f) * 0.5f);              // 0 — остриё, 1 — основание
            float w = 0.2f * Mathf.Pow(t, 0.8f) + 0.01f;
            float x = 0.03f * Mathf.Sin(p.y * 5f + seed);
            float d = Mathf.Abs(p.x - x);
            if (d > w) return Color.clear;
            Color c = Color.Lerp(IceDeep, IceLight, d / w < 0.4f ? 0.8f : 0.45f);
            if (Mathf.Repeat(p.y * 7f + seed, 1f) < 0.08f) c = Color.Lerp(c, IceRim, 0.5f);
            if (d > w * 0.8f) c = Color.Lerp(c, IceShadow, 0.35f);
            return c;
        }

        static Color Crystal(Vector2 p, float seed, int count)
        {
            // друза: несколько гранёных столбиков из общего основания; p.x −0.78..0.78
            for (int i = 0; i < count; i++)
            {
                float a = (i - (count - 1) * 0.5f) * 0.42f + 0.15f * (Hash(i, seed) - 0.5f);
                float len = 0.9f + 0.7f * Hash(i + 2f, seed);
                Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                Vector2 rel = p - new Vector2(0f, -0.9f);
                float along = Vector2.Dot(rel, dir);
                float across = Vector2.Dot(rel, new Vector2(dir.y, -dir.x));
                float w = 0.1f + 0.05f * Hash(i + 4f, seed);
                // остриё: последние 20 % сужаются в точку
                float taper = along > len * 0.8f ? Sat((len - along) / (len * 0.2f)) : 1f;
                if (along > 0f && along < len && Mathf.Abs(across) < w * taper)
                {
                    bool lit = across < 0f;                 // свет слева
                    Color c = lit ? Color.Lerp(IceMid, IceLight, along / len) : Color.Lerp(IceShadow, IceDeep, along / len);
                    if (Mathf.Abs(across) < 0.012f) c = Color.Lerp(c, IceRim, 0.6f);     // ребро
                    return c;
                }
            }
            return Color.clear;
        }

        static Color FrozenColumnShape(Vector2 p)
        {
            Color c = Column(p, 6.2f, true);
            if (c.a <= 0f) return c;
            // колонна целиком во льду: камень лишь проступает — иначе серо-зелёная
            // колонна с полосами читалась как доска [ТЕСТ, кадр ice f450]
            c = Color.Lerp(c, IceDeep, 0.55f);
            // иней и ледяная корка по верху и краям
            float frost = Fbm(p.x * 10f, p.y * 6f);
            c = Color.Lerp(c, IceLight, 0.35f + 0.35f * frost * SStep(-0.6f, 0.6f, p.y));
            if (Mathf.Abs(p.x) > 0.12f) c = Color.Lerp(c, IceRim, 0.3f);
            return c;
        }

        /// <summary>Ледяной уступ стены: угловатый, грани светлые сверху, тёмные снизу.</summary>
        static Color Shelf(Vector2 p, float seed)
        {
            // p.x −1..1, p.y −0.79..0.79; внутренний край справа
            float edge = 0.55f + 0.25f * (Hash(Mathf.Floor(p.y * 4f + seed), seed) - 0.5f)
                         + 0.15f * Mathf.Abs(Mathf.Repeat(p.y * 4f + seed, 1f) - 0.5f);
            if (p.x > edge || p.x < -1f) return Color.clear;
            float facet = Mathf.Repeat(p.y * 4f + seed, 1f);
            Color c = facet > 0.5f ? Color.Lerp(IceDeep, IceMid, SStep(-1f, edge, p.x))
                                   : Color.Lerp(IceShadow, IceDeep, SStep(-1f, edge, p.x));
            if (p.x > edge - 0.04f) c = Color.Lerp(c, IceRim, 0.55f);
            if (Fbm(p.x * 6f, p.y * 6f + seed) > 0.66f) c = Color.Lerp(c, IceLight, 0.3f);
            c.a = 1f;
            return c;
        }

        static Color DriftShape(Vector2 p)
        {
            float top = 0.08f + 0.08f * Mathf.Sin(p.x * 3f + 1.2f) + 0.05f * (Fbm(p.x * 5f, 3.3f) - 0.5f);
            if (p.y > top) return Color.clear;
            Color c = Color.Lerp(IceDeep, IceLight, SStep(-0.22f, top, p.y));
            if (Hash(Mathf.Floor(p.x * 80f), Mathf.Floor(p.y * 80f)) > 0.95f) c = IceRim;
            c.a = SStep(-0.22f, -0.14f, p.y) * 0.5f + 0.5f;
            return c;
        }

        static Color FrozenKelpShape(Vector2 p)
        {
            Color c = KelpStrand(p, 4.4f, false);
            if (c.a <= 0f) return c;
            // вмёрзла: зелень едва проступает сквозь лёд, по листьям — иней
            c = Color.Lerp(c, IceMid, 0.72f);
            if (Hash(Mathf.Floor(p.x * 40f), Mathf.Floor(p.y * 40f)) > 0.8f) c = Color.Lerp(c, IceRim, 0.5f);
            return c;
        }

        static Color IceBoulder(Vector2 p, float seed)
        {
            // Ледяной валун: неровный контур, внутри — плоские грани (ячейки
            // Вороного), каждая со своим светом сверху-слева. Прежний вариант
            // красил целые сектора от центра — выходила «круговая диаграмма».
            Vector2 q = new Vector2(p.x * 0.8f, (p.y + 0.12f) * 1.15f);
            float ang = Mathf.Atan2(q.y, q.x);
            float r = 0.6f + 0.1f * (Fbm(Mathf.Cos(ang) * 1.5f + seed, Mathf.Sin(ang) * 1.5f, 2) - 0.5f) * 2f;
            if (q.magnitude > r || p.y < -0.68f) return Color.clear;
            Vector2 cell = Vector2.zero;
            float best = 9f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = new Vector2(Hash(i, seed) - 0.5f, Hash(i + 9f, seed) - 0.5f) * 1.1f;
                float d = (q - c).sqrMagnitude;
                if (d < best) { best = d; cell = c; }
            }
            float light = Sat(0.5f + 0.9f * Vector2.Dot(cell.normalized, new Vector2(-0.45f, 0.9f)) * Mathf.Min(1f, cell.magnitude * 2.2f));
            light = 0.25f + 0.55f * light + 0.15f * Hash(cell.x * 7f, seed);
            Color col = Color.Lerp(IceShadow, IceLight, light);
            if (q.magnitude > r - 0.035f) col = Color.Lerp(col, IceRim, 0.35f);
            col.a = 1f;
            return col;
        }

        static Color IceFishShape(Vector2 p)
        {
            Color c = SchoolFishShape(new Vector2(p.x, p.y * 0.9f));
            if (c.a <= 0f) return c;
            return Color.Lerp(c, IceMid, 0.4f);
        }

        /// <summary>Порог «Ледяного разлома»: арка из льда с сосульками по своду.</summary>
        static Color IceArchShape(Vector2 p)
        {
            // p.x −1..1, p.y −0.7..0.7
            float outer = (p.x * p.x) / 0.95f + ((p.y + 0.7f) * (p.y + 0.7f)) / 1.85f;
            float inner = (p.x * p.x) / 0.5f + ((p.y + 0.7f) * (p.y + 0.7f)) / 1.02f;
            if (outer > 1f) return Color.clear;
            if (inner < 1f)
            {
                // сосульки свисают со свода внутрь проёма
                float k = Mathf.Repeat((p.x + 1f) * 7f, 1f);
                float hang = 0.18f * Hash(Mathf.Floor((p.x + 1f) * 7f), 2.2f) * (1f - Mathf.Abs(k - 0.5f) * 2f);
                float inEdge = Mathf.Sqrt(Mathf.Max(0f, 1.02f * (1f - (p.x * p.x) / 0.5f))) - 0.7f;
                if (p.y > inEdge - hang && p.y < inEdge) return Color.Lerp(IceDeep, IceLight, 0.6f);
                return Color.clear;
            }
            float facet = Mathf.Repeat(Mathf.Atan2(p.y + 0.7f, p.x) * 5f, 1f);
            Color c = facet < 0.5f ? Color.Lerp(IceDeep, IceMid, SStep(-0.7f, 0.6f, p.y)) : Color.Lerp(IceMid, IceLight, SStep(-0.7f, 0.6f, p.y));
            if (outer > 0.93f) c = Color.Lerp(c, IceRim, 0.5f);
            if (Fbm(p.x * 6f, p.y * 6f) > 0.65f) c = Color.Lerp(c, IceRim, 0.25f);
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------ каустики
        //
        // Отдельная текстура с повтором: световая сетка на дне — переплетение
        // ярких перемычек. Бесшовная, потому что строится по клеткам на торе.

        /// <summary>Пиксели каустик 128×128, бесшовные по обеим осям.</summary>
        static Color32[] PaintCaustics()
        {
            const int n = CausticSize;
            const int cells = 5;
            var pts = new Vector2[cells * cells];
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                    pts[j * cells + i] = new Vector2((i + 0.15f + 0.7f * Hash(i, j)) / cells, (j + 0.15f + 0.7f * Hash(j + 7f, i)) / cells);

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Vector2 uv = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    float d1 = 9f, d2 = 9f;
                    int cx = (int)(uv.x * cells), cy = (int)(uv.y * cells);
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy;
                            int wx = (gx + cells) % cells, wy = (gy + cells) % cells;
                            // точка соседней клетки, перенесённая на тор
                            Vector2 pt = pts[wy * cells + wx] + new Vector2(gx - wx, gy - wy) / cells;
                            float d = (uv - pt).sqrMagnitude;
                            if (d < d1) { d2 = d1; d1 = d; }
                            else if (d < d2) d2 = d;
                        }
                    }
                    // ярко на границах ячеек (d2 ≈ d1) — сеть перемычек
                    float edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
                    float a = Mathf.Pow(Sat(1f - edge * 9f), 3f);
                    byte v = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, v);
                }
            }
            return px;
        }

        const int CausticSize = 128;

        static void EnsureCaustics()
        {
            if (_causticTex != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _causticTex = LoadBaked("caustics", TextureWrapMode.Repeat, CausticSize, CausticSize)
                          ?? MakeTexture("caustics", CausticSize, CausticSize, PaintCaustics(), TextureWrapMode.Repeat);
            _causticMat = new Material(Unlit) { name = "BubbleRise_Caustics", mainTexture = _causticTex };
            WorldAtlasMs += sw.ElapsedMilliseconds;
        }
    }
}
