using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оформление трёх новых локаций — из их атласа (SpriteFactoryZones). Только
    /// вид: на шары, их места и столкновения не влияет.
    ///
    /// Главное отличие от руин: оформление стоит не только по стенам, но и под
    /// самими композициями. Шары палубы лежат на досках, шары ребра — на кости,
    /// сгусток в саду — на теле острова; поэтому место читается формой, а не цветом.
    /// Под шарами рисунок тёмный и утоплен в воде — шар к нему остаётся ярким.
    ///
    /// Случайность — своя (System.Random от зерна локации), UnityEngine.Random
    /// не трогается: поток угроз тот же.
    /// </summary>
    public static class ZoneDressingShaped
    {
        // яркость и доля цвета воды по слоям
        const float EdgeBright = 0.74f, EdgeFog = 0.24f;
        const float DetailBright = 0.66f, DetailFog = 0.32f;
        const float CrustBright = 0.62f, CrustFog = 0.3f;
        /// <summary>
        /// Под шарами: виден (палуба, ребро, остров — это и есть форма места), но шар к
        /// рисунку под ним ≥ 3:1 [ТЕСТ]. Кость светлее дерева и камня — её глушим сильнее.
        /// </summary>
        const float UnderBright = 0.68f, UnderFog = 0.28f;
        const float UnderBoneBright = 0.5f, UnderBoneFog = 0.36f;
        const float BackBright = 0.34f, BackFog = 0.72f;
        const float GateBright = 0.56f, GateFog = 0.38f;

        const float GateInset = ZoneDressing.GateInset;

        public static void Dress(OceanZone zone, ZoneDef def, Playfield field, int seed,
                                 List<Vector3> blobs, List<OceanZone.Slot> slots)
        {
            var rng = new System.Random(seed * 7919 + 29);
            bool bone = def.Kind == ZoneKind.Ribs;
            var ctx = new Ctx
            {
                Zone = zone, Def = def, Field = field, Rng = rng, Blobs = blobs,
                Water = Color.Lerp(def.Theme.WaterTop, def.Theme.WaterBottom, 0.5f),
                High = Visuals.HighDetail, H = zone.Height,
                UnderB = bone ? UnderBoneBright : UnderBright, UnderF = bone ? UnderBoneFog : UnderFog,
            };
            switch (def.Kind)
            {
                case ZoneKind.Wreck: Wreck(ctx, slots); break;
                case ZoneKind.Ribs: Ribs(ctx, slots); break;
                case ZoneKind.Garden: Garden(ctx, slots); break;
            }
        }

        sealed class Ctx
        {
            public OceanZone Zone;
            public ZoneDef Def;
            public Playfield Field;
            public System.Random Rng;
            public List<Vector3> Blobs;
            public Color Water;
            public bool High;
            public float H;
            /// <summary>Тон рисунков под шарами (у кости — глуше).</summary>
            public float UnderB, UnderF;
            public float R() => (float)Rng.NextDouble();
            public float T(float y) => Mathf.Clamp01((y + H * 0.5f) / H);
        }

        // ================================================================ корабль

        static readonly ZonePiece[] WreckEdge =
            { ZonePiece.WFrame, ZonePiece.WPorthole, ZonePiece.WRigging, ZonePiece.WSail, ZonePiece.WFrame,
              ZonePiece.WLantern, ZonePiece.WBarrel, ZonePiece.WCrateA, ZonePiece.WPorthole, ZonePiece.WCrateB };

        /// <summary>
        /// Корпус наискось: обшивка досками по кромке, шпангоуты, иллюминаторы с
        /// ровным тусклым светом, такелаж и парус; палубы — доски под рядами шаров,
        /// мачта — под косой цепочкой, груз — ящики и бочки. Вход — пробоина в борту,
        /// выход — люк палубы над проходом.
        /// </summary>
        static void Wreck(Ctx c, List<OceanZone.Slot> slots)
        {
            WallEdges(c, ZonePiece.WPlankA, ZonePiece.WPlankB, 0.36f, 1.25f, WreckEdge);

            foreach (var s in slots)
            {
                switch (s.Pattern)
                {
                    case "deck": case "planks": RowsOf(c, s, ZonePiece.WPlankA, 0.36f, 0.26f); break;
                    case "mast": MastUnder(c, s); break;
                    case "cargo": UnderEach(c, s, c.R() < 0.5f ? ZonePiece.WCrateA : ZonePiece.WBarrel, 2.3f); break;
                }
            }

            // в глубине: обломки мачт и паруса, утопленные в воде
            for (float y = -c.H * 0.5f + 4f; y < c.H * 0.5f - 4f; y += 4.2f + 2.6f * c.R())
            {
                float t = c.T(y), r = c.R(), r2 = c.R();
                if (!c.High && r2 < 0.4f) continue;
                var piece = r < 0.4f ? ZonePiece.WMast : r < 0.75f ? ZonePiece.WSail : ZonePiece.WFrame;
                float h = piece == ZonePiece.WMast ? 4.5f + 1.5f * r2 : 2.2f + 0.8f * r2;
                float side = r2 < 0.5f ? -1f : 1f;
                float x = c.Zone.Centre(t) + side * Mathf.Lerp(0.4f, 0.9f, r) * Mathf.Min(c.Zone.Passage(t), c.Field.HalfWidth);
                var item = Item(Z(piece), new Vector2(x, y), h, side * (0.25f + 0.3f * r), side > 0f, 0f,
                                Tone(Z(piece), BackBright * Calm(c, x, t), c.Water, BackFog), OceanZone.ZoneBackLayer, false);
                item.FadeBottom = piece == ZonePiece.WMast;
                c.Zone.AddItem(item);
            }

            // вход: пробоина — рваные доски с обоих бортов и шпангоуты
            float yIn = -c.H * 0.5f + GateInset;
            float tIn = c.T(yIn);
            for (int s = 0; s < 2; s++)
            {
                float sd = s == 0 ? -1f : 1f;
                float edge = Edge(c, tIn, sd);
                var info = Z(ZonePiece.WBreach);
                c.Zone.AddItem(Item(info, new Vector2(edge - sd * 0.5f, yIn), 4.2f, 0f, s == 1, 0f,
                                    Tone(info, GateBright, c.Water, GateFog), OceanZone.ZoneBackLayer, false));
                var frame = Z(ZonePiece.WFrame);
                c.Zone.AddItem(Item(frame, new Vector2(edge + sd * 0.2f, yIn - 0.6f), 3.6f, sd * 0.12f, s == 1, 0f,
                                    Tone(frame, GateBright, c.Water, GateFog), OceanZone.ZoneLayer, true));
            }

            // выход: люк палубы во весь проход
            float yOut = c.H * 0.5f - GateInset;
            float tOut = c.T(yOut);
            float l = Edge(c, tOut, -1f), rr = Edge(c, tOut, 1f);
            var hatch = Z(ZonePiece.WHatch);
            float hw = (rr - l) * 0.5f + 0.4f;
            c.Zone.AddItem(Item(hatch, new Vector2((l + rr) * 0.5f, yOut), 2f * hw / hatch.Aspect, 0f, false, 0f,
                                Tone(hatch, GateBright * 0.8f, c.Water, GateFog + 0.15f), OceanZone.ZoneBackLayer, false));

            // свет в иллюминаторах и фонарях — ровный, «дышит» медленно
            Glows(c, Palette.Hex("B09A62"), 0.16f, 2.6f);
        }

        /// <summary>Мачта под косой цепочкой: столб от первого шара до последнего, рея поперёк, обрывок паруса.</summary>
        static void MastUnder(Ctx c, OceanZone.Slot s)
        {
            if (s.Count < 2) return;
            Vector3 lo = c.Blobs[s.First], hi = lo;
            for (int i = s.First; i < s.First + s.Count; i++)
            {
                var b = c.Blobs[i];
                if (b.y < lo.y) lo = b;
                if (b.y > hi.y) hi = b;
            }
            Vector2 a = lo, b2 = hi;
            Vector2 dir = (b2 - a).normalized;
            if (dir.sqrMagnitude < 0.5f) return;
            float len = Vector2.Distance(a, b2) + 1.6f;
            var mast = Z(ZonePiece.WMast);
            float angle = Mathf.Atan2(dir.x, dir.y);
            c.Zone.AddItem(Item(mast, (a + b2) * 0.5f, len, -angle, false, 0f,
                                Tone(mast, c.UnderB, c.Water, c.UnderF), OceanZone.ZoneLayer, false));
            // рея — на трёх четвертях, поперёк мачты; с неё свисает обрывок паруса
            Vector2 yard = Vector2.Lerp(a, b2, 0.78f);
            c.Zone.AddItem(Item(mast, yard, 1.8f, -angle + Mathf.PI * 0.5f, false, 0f,
                                Tone(mast, c.UnderB, c.Water, c.UnderF), OceanZone.ZoneLayer, false));
            var sail = Z(ZonePiece.WSail);
            c.Zone.AddItem(Item(sail, yard + new Vector2(0f, -0.9f), 1.9f, -angle * 0.3f, c.R() < 0.5f, 0.02f,
                                Tone(sail, BackBright * 1.2f, c.Water, BackFog - 0.1f), OceanZone.ZoneBackLayer, false));
        }

        // ================================================================ рёбра

        static readonly ZonePiece[] RibsEdge =
            { ZonePiece.RRibArc, ZonePiece.RVertebraA, ZonePiece.RWorms, ZonePiece.RRibArc, ZonePiece.RCucumber,
              ZonePiece.RBarnacle, ZonePiece.RVertebraB, ZonePiece.RBones };

        /// <summary>
        /// Скелет кита: кромка — кость, из стен растут рёбра; каждый ряд шаров
        /// сидит на дуге ребра (окно — перелом кости), позвонки — под столбиком.
        /// В глубине — огромные рёбра в тумане. Вход — череп с бахромой уса над
        /// проходом, выход — хвостовые позвонки и плавник.
        /// </summary>
        static void Ribs(Ctx c, List<OceanZone.Slot> slots)
        {
            WallEdges(c, ZonePiece.RBoneSeg, ZonePiece.RBoneSeg, 0.3f, 1.0f, RibsEdge);

            foreach (var s in slots)
            {
                switch (s.Pattern)
                {
                    case "rib": case "narrow": case "double": RowsOf(c, s, ZonePiece.RBoneSeg, 0.32f, 0.12f); break;
                    case "spine": UnderEach(c, s, ZonePiece.RVertebraA, 2.4f); break;
                }
            }

            // в глубине: большие рёбра от стен сходятся над проходом аркой — грудная
            // клетка изнутри; концы встречаются, а не перекрещиваются
            for (float y = -c.H * 0.5f + 3.5f; y < c.H * 0.5f - 3.5f; y += 2.6f + 1.6f * c.R())
            {
                float t = c.T(y), r = c.R();
                if (!c.High && r < 0.4f) continue;
                for (int s = 0; s < 2; s++)
                {
                    float sd = s == 0 ? -1f : 1f;
                    var info = Z(ZonePiece.RRibArc);
                    float edge = Edge(c, t, sd);
                    float w = Mathf.Abs(edge) * 1.02f;
                    c.Zone.AddItem(Item(info, new Vector2(edge - sd * w * 0.5f, y), w / info.Aspect, 0f, s == 1, 0f,
                                        Tone(info, BackBright, c.Water, BackFog), OceanZone.ZoneBackLayer, false));
                }
            }

            // вход: череп над проходом, под ним — бахрома уса
            float yIn = -c.H * 0.5f + GateInset;
            // череп сверху поперёк прохода, рылом в сторону; кость светлая — глушим, как всё под шарами
            float wIn = Mathf.Min(c.Zone.Passage(c.T(yIn)), c.Field.HalfWidth) * 1.7f;
            var skull = Z(ZonePiece.RSkull);
            c.Zone.AddItem(Item(skull, new Vector2(0f, yIn + 1.3f), wIn / skull.Aspect, 0f, c.R() < 0.5f, 0f,
                                Tone(skull, GateBright * 0.7f, c.Water, GateFog + 0.22f), OceanZone.ZoneBackLayer, false));
            var baleen = Z(ZonePiece.RBaleen);
            c.Zone.AddItem(Item(baleen, new Vector2(0f, yIn + 0.15f), wIn * 0.95f / baleen.Aspect, 0f, false, 0f,
                                Tone(baleen, GateBright * 0.75f, c.Water, GateFog + 0.15f), OceanZone.ZoneBackLayer, false));

            // выход: позвонки хвоста — к плавнику всё мельче
            float yOut = c.H * 0.5f - GateInset;
            for (int i = 0; i < 5; i++)
            {
                var v = Z(i % 2 == 0 ? ZonePiece.RVertebraA : ZonePiece.RVertebraB);
                float k = i / 4f;
                c.Zone.AddItem(Item(v, new Vector2(0.15f * Mathf.Sin(i * 1.3f), yOut - 2.4f + i * 0.75f), 0.9f - 0.4f * k, 0f,
                                    false, 0f, Tone(v, GateBright * 0.85f, c.Water, GateFog + 0.1f), OceanZone.ZoneBackLayer, false));
            }
            var fluke = Z(ZonePiece.RFluke);
            float wOut = Mathf.Min(c.Zone.Passage(c.T(yOut)), c.Field.HalfWidth) * 1.4f;
            c.Zone.AddItem(Item(fluke, new Vector2(0f, yOut + 1.5f), wOut / fluke.Aspect, 0f, false, 0f,
                                Tone(fluke, GateBright * 0.7f, c.Water, GateFog + 0.22f), OceanZone.ZoneBackLayer, false));
        }

        // ================================================================ сад

        /// <summary>
        /// Открытая вода: стен нет. Сгустки шаров — на плавучих островах (камень,
        /// мох, корни, свисающие лианы, актинии и веера сверху), камни-ступени —
        /// на мелких камнях. В глубине — острова в дымке, в толще — редкие тусклые
        /// огоньки планктона. Вход — два камня с висячим мостом, выход — арка-остров.
        /// </summary>
        static void Garden(Ctx c, List<OceanZone.Slot> slots)
        {
            float hw = c.Field.HalfWidth;
            foreach (var s in slots)
            {
                switch (s.Pattern)
                {
                    case "stones": UnderEach(c, s, ZonePiece.GIslandC, 2.4f); break;
                    default: IslandsUnder(c, s); break;
                }
            }

            // по краям поля — склоны больших островов, уходящие за экран: место не пустое
            for (float y = -c.H * 0.5f + 2f; y < c.H * 0.5f - 2f; y += 3.2f + 2.2f * c.R())
            {
                float r = c.R(), r2 = c.R();
                float sd = r < 0.5f ? -1f : 1f;
                var piece = r2 < 0.5f ? ZonePiece.GIslandA : ZonePiece.GIslandB;
                var info = Z(piece);
                float h = 1.6f + 0.6f * r2, w = h * info.Aspect;
                c.Zone.AddItem(Item(info, new Vector2(sd * (hw + w * 0.18f), y), h, 0f, sd > 0f, 0f,
                                    Tone(info, EdgeBright * 0.85f, c.Water, EdgeFog), OceanZone.ZoneLayer, false));
                var vine = Z(r < 0.25f || r > 0.75f ? ZonePiece.GVineA : ZonePiece.GVineB);
                var item = Item(vine, new Vector2(sd * (hw - w * 0.08f), y - h * 0.15f), 1.6f + 0.8f * r, 0f, false, 0.02f,
                                Tone(vine, EdgeBright * 0.8f, c.Water, EdgeFog), OceanZone.ZoneLayer, false);
                item.PivotY = 1f;
                c.Zone.AddItem(item);
            }

            // острова в дымке за проходом
            for (float y = -c.H * 0.5f + 3f; y < c.H * 0.5f - 3f; y += 3.6f + 2.4f * c.R())
            {
                float r = c.R(), r2 = c.R();
                if (!c.High && r2 < 0.4f) continue;
                var info = Z(r < 0.5f ? ZonePiece.GIslandB : ZonePiece.GIslandC);
                float x = (r2 < 0.5f ? -1f : 1f) * Mathf.Lerp(0.3f, 0.85f, r) * hw;
                c.Zone.AddItem(Item(info, new Vector2(x, y), 0.9f + 0.5f * r, 0f, r2 < 0.5f, 0f,
                                    Tone(info, BackBright * Calm(c, x, c.T(y)), c.Water, BackFog), OceanZone.ZoneBackLayer, false));
            }

            // вход: два камня по краям прохода и висячий мост между ними
            float yIn = -c.H * 0.5f + GateInset;
            float span = Mathf.Min(c.Field.PlayHalfWidth * 0.82f, hw - 0.4f);
            for (int s = 0; s < 2; s++)
            {
                float sd = s == 0 ? -1f : 1f;
                var rock = Z(ZonePiece.GIslandC);
                c.Zone.AddItem(Item(rock, new Vector2(sd * (span + 0.4f), yIn), 1.5f, 0f, s == 1, 0f,
                                    Tone(rock, GateBright, c.Water, GateFog), OceanZone.ZoneBackLayer, false));
            }
            var bridge = Z(ZonePiece.GBridge);
            c.Zone.AddItem(Item(bridge, new Vector2(0f, yIn + 0.25f), 2f * span / bridge.Aspect, 0f, false, 0.01f,
                                Tone(bridge, GateBright, c.Water, GateFog), OceanZone.ZoneBackLayer, false));

            // выход: арка-остров над проходом
            float yOut = c.H * 0.5f - GateInset;
            var arch = Z(ZonePiece.GArch);
            float aw = 2f * Mathf.Min(c.Field.PlayHalfWidth, hw) + 0.6f;
            c.Zone.AddItem(Item(arch, new Vector2(0f, yOut + 0.8f), aw / arch.Aspect, 0f, c.R() < 0.5f, 0f,
                                Tone(arch, GateBright * 0.9f, c.Water, GateFog + 0.1f), OceanZone.ZoneBackLayer, false));

            // огоньки планктона: редкие, тусклые, дышат не быстрее 9 с
            Glows(c, Palette.Hex("8ECCB8"), 0.14f, c.High ? 1.6f : 3.2f);
        }

        /// <summary>Сгустки шаров слота — на островах: один остров на каждую кучку.</summary>
        static void IslandsUnder(Ctx c, OceanZone.Slot s)
        {
            if (s.Count == 0) return;
            // кучки: шары ближе 1.4 юн. друг к другу — одна кучка
            var group = new int[s.Count];
            for (int i = 0; i < s.Count; i++) group[i] = i;
            for (int i = 0; i < s.Count; i++)
                for (int j = i + 1; j < s.Count; j++)
                    if (Vector2.Distance(c.Blobs[s.First + i], c.Blobs[s.First + j]) < 1.4f)
                    {
                        int gi = Root(group, i), gj = Root(group, j);
                        if (gi != gj) group[gj] = gi;
                    }
            for (int g = 0; g < s.Count; g++)
            {
                if (Root(group, g) != g) continue;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                int n = 0;
                for (int i = 0; i < s.Count; i++)
                {
                    if (Root(group, i) != g) continue;
                    var b = c.Blobs[s.First + i];
                    minX = Mathf.Min(minX, b.x - b.z); maxX = Mathf.Max(maxX, b.x + b.z);
                    minY = Mathf.Min(minY, b.y - b.z); maxY = Mathf.Max(maxY, b.y + b.z);
                    n++;
                }
                float w = Mathf.Max(1.2f, (maxX - minX) * 1.15f);
                var piece = n >= 6 ? ZonePiece.GIslandA : n >= 3 ? ZonePiece.GIslandB : ZonePiece.GIslandC;
                var info = Z(piece);
                float h = w / info.Aspect;
                Vector2 at = new Vector2((minX + maxX) * 0.5f, minY + h * 0.42f);
                c.Zone.AddItem(Item(info, at, h, 0f, c.R() < 0.5f, 0f,
                                    Tone(info, c.UnderB, c.Water, c.UnderF), OceanZone.ZoneLayer, false));
                // лианы из-под острова
                int vines = c.High ? 2 : 1;
                for (int v = 0; v < vines; v++)
                {
                    var vine = Z(c.R() < 0.5f ? ZonePiece.GVineA : ZonePiece.GVineB);
                    var item = Item(vine, new Vector2(at.x + (c.R() - 0.5f) * w * 0.6f, at.y - h * 0.15f), 1.1f + 0.7f * c.R(), 0f,
                                    false, 0.02f, Tone(vine, c.UnderB, c.Water, c.UnderF), OceanZone.ZoneBackLayer, false);
                    item.PivotY = 1f;
                    c.Zone.AddItem(item);
                }
                // сверху — актиния или водоросль у края острова, мимо шаров
                if (c.High || c.R() < 0.5f)
                {
                    var top = Z(c.R() < 0.5f ? ZonePiece.GFlower : ZonePiece.GKelp);
                    float sx = c.R() < 0.5f ? minX - 0.1f : maxX + 0.1f;
                    c.Zone.AddItem(Item(top, new Vector2(sx, at.y + h * 0.18f), 0.55f + 0.2f * c.R(), 0f, c.R() < 0.5f, 0.015f,
                                        Tone(top, DetailBright, c.Water, DetailFog), OceanZone.ZoneLayer, true));
                }
            }
        }

        static int Root(int[] g, int i)
        {
            while (g[i] != i) i = g[i];
            return i;
        }

        // ================================================================ общее

        /// <summary>
        /// Кромка стен: корка из рисунков вдоль края по его наклону, крупное на
        /// кромке, мелочи на лице стены.
        /// </summary>
        static void WallEdges(Ctx c, ZonePiece crustA, ZonePiece crustB, float thick, float step, ZonePiece[] kit)
        {
            float hw = c.Field.HalfWidth;
            for (int s = 0; s < 2; s++)
            {
                bool right = s == 1;
                float sd = right ? 1f : -1f;
                for (float y = -c.H * 0.5f + 0.4f; y < c.H * 0.5f - 0.4f; y += step * (0.85f + 0.3f * c.R()))
                {
                    float t = c.T(y);
                    float inner = c.Zone.InnerEdge(t, right);
                    if (inner > hw + 0.6f) continue;
                    var info = Z(c.R() < 0.5f ? crustA : crustB);
                    float angle = sd * (Mathf.PI * 0.5f - Mathf.Atan(Slope(c, t, right)));
                    c.Zone.AddItem(Item(info, new Vector2(sd * (inner + thick * 0.4f), y), thick, angle, c.R() < 0.5f, 0f,
                                        Tone(info, CrustBright, c.Water, CrustFog), OceanZone.ZoneLayer, false));
                }
                for (float y = -c.H * 0.5f + OceanZone.Taper; y < c.H * 0.5f - OceanZone.Taper; y += 1.6f + 1.4f * c.R())
                {
                    float t = c.T(y), r = c.R(), r2 = c.R();
                    float inner = c.Zone.InnerEdge(t, right);
                    if (inner > hw) continue;
                    var piece = kit[c.Rng.Next(kit.Length)];
                    if (!c.High && r2 < 0.35f) continue;
                    var info = Z(piece);
                    float h = Size(piece, r);
                    float w = h * info.Aspect;
                    bool hangs = piece == ZonePiece.WLantern || piece == ZonePiece.WRigging || piece == ZonePiece.WSail;
                    // основание — в стене; растущее из стены (ребро) — от стены в проход
                    float x = sd * (inner + (piece == ZonePiece.RRibArc ? -w * 0.18f : w * 0.22f + 0.05f));
                    var item = Item(info, new Vector2(x, y), h, 0f, right, hangs ? 0.015f : 0f,
                                    Tone(info, EdgeBright * (0.9f + 0.2f * r2), c.Water, EdgeFog), OceanZone.ZoneLayer, !hangs);
                    if (hangs) item.PivotY = 1f;
                    c.Zone.AddItem(item);
                }
            }
        }

        /// <summary>
        /// Ряды шаров слота лежат на рисунке: доски палубы, кость ребра. Настил — от
        /// стены до стены через все шары ряда; самый широкий просвет (от стены до шара
        /// или между шарами) — окно ряда, там перелом. Настил ниже середины шаров на
        /// drop: шары стоят на нём, и он виден под ними, а не прячется за ними.
        /// Двойной ряд делится по высоте слота.
        /// </summary>
        static void RowsOf(Ctx c, OceanZone.Slot s, ZonePiece piece, float thick, float drop)
        {
            if (s.Count == 0) return;
            var rowA = new List<Vector3>();
            var rowB = new List<Vector3>();
            for (int i = s.First; i < s.First + s.Count; i++)
                (c.Blobs[i].y < s.Y ? rowA : rowB).Add(c.Blobs[i]);
            foreach (var row in new[] { rowA, rowB })
            {
                if (row.Count == 0) continue;
                row.Sort((a, b) => a.x.CompareTo(b.x));
                float lo = s.Centre - s.Half - 0.3f, hi = s.Centre + s.Half + 0.3f;
                // точки настила: стена, шары, стена (радиус стены — ноль)
                var pts = new List<Vector3> { new Vector3(lo, EdgeY(row, lo), 0f) };
                pts.AddRange(row);
                pts.Add(new Vector3(hi, EdgeY(row, hi), 0f));
                int window = -1;
                float widest = 0f;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    float gap = pts[i + 1].x - pts[i].x - pts[i].z - pts[i + 1].z;
                    if (gap > widest) { widest = gap; window = i; }
                }
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    if (i == window) continue;   // окно — перелом
                    Vector2 a = new Vector2(pts[i].x, pts[i].y - drop), b = new Vector2(pts[i + 1].x, pts[i + 1].y - drop);
                    // длинный пролёт — несколькими досками/костями, без растяжки рисунка
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.6f));
                    for (int k = 0; k < n; k++)
                        Segment(c, Vector2.Lerp(a, b, k / (float)n), Vector2.Lerp(a, b, (k + 1) / (float)n), piece, thick, ((i + k) & 1) == 1);
                }
            }
        }

        /// <summary>Высота ряда у стены: продолжение по двум крайним шарам.</summary>
        static float EdgeY(List<Vector3> row, float x)
        {
            if (row.Count == 1) return row[0].y;
            Vector3 a, b;
            if (Mathf.Abs(x - row[0].x) < Mathf.Abs(x - row[row.Count - 1].x)) { a = row[0]; b = row[1]; }
            else { a = row[row.Count - 1]; b = row[row.Count - 2]; }
            float slope = (b.y - a.y) / Mathf.Max(0.1f, Mathf.Abs(b.x - a.x)) * Mathf.Sign(b.x - a.x);
            return a.y + slope * (x - a.x) * 0.5f;   // к стене — положе: кость не задирается
        }

        /// <summary>Рисунок-отрезок от a до b: длина — по расстоянию с нахлёстом, угол — по направлению.</summary>
        static void Segment(Ctx c, Vector2 a, Vector2 b, ZonePiece piece, float thick, bool alt)
        {
            if (alt && piece == ZonePiece.WPlankA) piece = ZonePiece.WPlankB;
            var info = Z(piece);
            Vector2 d = b - a;
            float len = d.magnitude + 0.2f;
            if (len < 0.4f) return;
            float angle = Mathf.Atan2(d.y, d.x);
            // рисунок лежит вдоль x: высота — толщина, ширина — длина
            var item = new OceanZone.Item
            {
                Uv = info.Uv, Pos = (a + b) * 0.5f,
                Half = new Vector2(len * 0.5f, thick * 0.5f), Angle = angle, Flip = c.R() < 0.5f,
                Color = Tone(info, c.UnderB, c.Water, c.UnderF), Layer = OceanZone.ZoneLayer,
            };
            c.Zone.AddItem(item);
        }

        /// <summary>Под каждым шаром слота — свой рисунок (ящик, позвонок, камень).</summary>
        static void UnderEach(Ctx c, OceanZone.Slot s, ZonePiece piece, float scale)
        {
            for (int i = s.First; i < s.First + s.Count; i++)
            {
                var b = c.Blobs[i];
                var p = piece;
                if (piece == ZonePiece.WCrateA) p = c.R() < 0.5f ? ZonePiece.WCrateA : ZonePiece.WCrateB;
                if (piece == ZonePiece.RVertebraA && c.R() < 0.5f) p = ZonePiece.RVertebraB;
                var info = Z(p);
                c.Zone.AddItem(Item(info, new Vector2(b.x, b.y - b.z * 0.25f), b.z * scale, (c.R() - 0.5f) * 0.4f, c.R() < 0.5f, 0f,
                                    Tone(info, c.UnderB, c.Water, c.UnderF), OceanZone.ZoneLayer, false));
            }
        }

        /// <summary>Мягкие пятна света: ровные, дышат 9–12 с с размахом 8 % (OceanZone.RebuildDecor).</summary>
        static void Glows(Ctx c, Color tint, float alpha, float step)
        {
            float hw = c.Field.HalfWidth;
            for (float y = -c.H * 0.5f + 2f; y < c.H * 0.5f - 2f; y += step + step * c.R())
            {
                float t = c.T(y), r = c.R();
                float x;
                if (c.Def.Walls)
                {
                    float sd = r < 0.5f ? -1f : 1f;
                    float inner = c.Zone.InnerEdge(t, sd > 0f);
                    if (inner > hw) continue;
                    x = sd * (inner + 0.3f + 0.6f * c.R());
                }
                else x = (r - 0.5f) * 2f * hw * 0.95f;
                var info = Z(ZonePiece.Glow);
                var item = Item(info, new Vector2(x, y), 0.35f + 0.3f * c.R(), 0f, false, 0f,
                                tint.WithAlpha(alpha), OceanZone.ZoneLayer, false);
                item.Breath = 0.08f;
                c.Zone.AddItem(item);
            }
        }

        static float Size(ZonePiece p, float r)
        {
            switch (p)
            {
                case ZonePiece.WFrame: return Mathf.Lerp(2.4f, 3.4f, r);
                case ZonePiece.WRigging: case ZonePiece.WSail: return Mathf.Lerp(1.6f, 2.3f, r);
                case ZonePiece.WLantern: return Mathf.Lerp(0.7f, 0.9f, r);
                case ZonePiece.WPorthole: return Mathf.Lerp(0.55f, 0.75f, r);
                case ZonePiece.WBarrel: case ZonePiece.WCrateA: case ZonePiece.WCrateB: return Mathf.Lerp(0.6f, 0.85f, r);
                case ZonePiece.WWheel: return Mathf.Lerp(0.9f, 1.2f, r);
                case ZonePiece.RRibArc: return Mathf.Lerp(1.3f, 1.8f, r);
                case ZonePiece.RVertebraA: case ZonePiece.RVertebraB: return Mathf.Lerp(0.6f, 0.85f, r);
                case ZonePiece.RWorms: return Mathf.Lerp(0.5f, 0.75f, r);
                case ZonePiece.RCucumber: case ZonePiece.RBones: return Mathf.Lerp(0.3f, 0.42f, r);
                case ZonePiece.RBarnacle: return Mathf.Lerp(0.3f, 0.45f, r);
                default: return 1f;
            }
        }

        /// <summary>Край прохода с этой стороны (x), по стене — или по полю, если стен нет.</summary>
        static float Edge(Ctx c, float t, float sd)
        {
            float half = Mathf.Min(c.Zone.Passage(t), c.Field.HalfWidth);
            return c.Zone.Centre(t) + sd * half;
        }

        /// <summary>Ближе к пути пузырька — тише: силуэты в глубине не спорят с шарами.</summary>
        static float Calm(Ctx c, float x, float t)
        {
            float half = Mathf.Max(0.5f, Mathf.Min(c.Zone.Passage(t), c.Field.HalfWidth));
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Mathf.Abs(x) / half));
        }

        static float Slope(Ctx c, float t, bool right)
        {
            const float e = 0.004f;
            float a = c.Zone.InnerEdge(Mathf.Clamp01(t - e), right), b = c.Zone.InnerEdge(Mathf.Clamp01(t + e), right);
            return (b - a) / (2f * e * c.H);
        }

        static AtlasPiece Z(ZonePiece p) => SpriteFactory.Zone(p);

        static OceanZone.Item Item(AtlasPiece info, Vector2 at, float height, float angle, bool flip, float sway,
                                   Color tint, byte layer, bool pivotBottom)
        {
            return new OceanZone.Item
            {
                Uv = info.Uv,
                Pos = at,
                Half = new Vector2(height * info.Aspect * 0.5f, height * 0.5f),
                Angle = angle,
                Flip = flip,
                Sway = sway,
                PivotY = pivotBottom ? -1f : 0f,
                Color = tint,
                Layer = layer,
            };
        }

        static Color Tone(AtlasPiece info, float bright, Color water, float fog)
        {
            Color target = Color.Lerp(new Color(info.Avg.r * bright, info.Avg.g * bright, info.Avg.b * bright), water, fog);
            return WorldLayers.TintFor(target, info.Avg);
        }
    }
}
