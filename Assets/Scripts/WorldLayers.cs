using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Четыре слоя параллакса: дальний (хребты, руины, лес), средний (скалы,
    /// колонны, арки, ламинарии), ближний (камни, кораллы и мелочи по краям) и
    /// передний (редкие размытые листья и пузыри у самой кромки).
    ///
    /// **Без повторов.** Слой разбит на слоты по высоте, и содержимое слота
    /// выводится из хеша его номера: рисунок, сторона, размер, отражение, оттенок.
    /// Ни один рисунок не стоит по сетке, длина «повтора» бесконечна — слои не
    /// выглядят штампами, и при этом ничего не нужно хранить или пулить.
    ///
    /// **Биом.** Слот получает биом в момент, когда впервые входит в кадр сверху:
    /// так смена окружения по глубине идёт снизу вверх вместе с миром, без стыка
    /// посреди экрана. У границы ступени новый биом примешивается долей.
    ///
    /// **Колонна тихая.** Всё яркое и детальное — у краёв: ближний план не
    /// заходит глубже 28 % ширины от края, в колонке остаются только дальние
    /// силуэты, утопленные в тумане. Тон каждого рисунка считается от его
    /// среднего цвета: «во что превратится» задаётся прямо, а не подбором тинта.
    /// </summary>
    public sealed class WorldLayers
    {
        enum Kind { Far, Mid, Near, Front, Ice, Zone }

        struct Layer
        {
            public Kind Kind;
            public QuadBatch Batch;
            public float Parallax, Spacing, Bright, Fog, Alpha;
            public int[] CacheK;
            public byte[] CacheBiome;
            public int HideBelow;        // слоты не выше этого не рисуются (после пустой сцены)
        }

        const int CacheSize = 128;

        readonly Playfield _field;
        readonly Layer[] _layers;
        float _scroll;
        float _time;
        float _drift;
        bool _wasEmpty;

        /// <summary>Набор слоёв локации на экране (-1 — набор по глубине).</summary>
        public int ZoneBiome = -1;

        // Обрыв в боковых полосах широкого экрана: один меш на обе стороны.
        const int CliffRows = 40;
        readonly MeshRenderer _cliff;
        readonly Mesh _cliffMesh;
        readonly Vector3[] _cliffV = new Vector3[CliffRows * 3 * 2];
        readonly Color32[] _cliffC = new Color32[CliffRows * 3 * 2];
        readonly Vector2[] _cliffUv = new Vector2[CliffRows * 3 * 2];

        /// <summary>
        /// На экране «Ледяной разлом»: ледяной слой получают слоты, входящие сверху,
        /// пока флаг стоит, — и держат его, пока не уйдут вниз.
        /// </summary>
        public bool IceActive;

        /// <summary>Параллакс ледяного слоя: между дальним и средним.</summary>
        public const float IceParallax = 0.24f;

        /// <summary>
        /// На экране новая локация (ZoneKind; -1 — нет): её дальний набор — корпус,
        /// грудная клетка, острова в дымке — получают слоты, входящие сверху, как у льда.
        /// </summary>
        public int ZoneKit = -1;

        /// <summary>Параллакс слоя новых локаций: дальше льда, ближе дальнего плана.</summary>
        public const float ZoneParallax = 0.2f;

        /// <summary>Множители параллакса слоёв — дальний, средний, ближний, передний.</summary>
        public static readonly float[] Parallax = { 0.12f, 0.35f, 0.75f, 1.3f };

        public WorldLayers(Transform root, Playfield field)
        {
            _field = field;
            var mat = SpriteFactory.WorldMaterial;

            var cliffGo = new GameObject("BandCliff");
            cliffGo.transform.SetParent(root, false);
            _cliff = cliffGo.AddComponent<MeshRenderer>();
            _cliff.sharedMaterial = SpriteFactory.ZoneWallMaterial(WallStyle.Rock);
            _cliff.sortingOrder = SortingOrders.NearLayer - 1;
            _cliff.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cliff.receiveShadows = false;
            _cliffMesh = new Mesh { name = "BandCliffMesh" };
            _cliffMesh.MarkDynamic();
            cliffGo.AddComponent<MeshFilter>().sharedMesh = _cliffMesh;
            var tris = new int[(CliffRows - 1) * 2 * 6 * 2];
            int ti = 0;
            for (int side = 0; side < 2; side++)
            {
                int b0 = side * CliffRows * 3;
                for (int i = 0; i < CliffRows - 1; i++)
                {
                    for (int c = 0; c < 2; c++)
                    {
                        int a = b0 + i * 3 + c, b = b0 + (i + 1) * 3 + c;
                        tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                        tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
                    }
                }
            }
            _cliffMesh.vertices = _cliffV;
            _cliffMesh.triangles = tris;
            _cliffMesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 4000f, 10f));
            _layers = new[]
            {
                Make(Kind.Far, root, "LayerFar", mat, SortingOrders.FarLayer, 12, Parallax[0], 3.4f, 0.5f, 0.82f, 0.9f),
                Make(Kind.Mid, root, "LayerMid", mat, SortingOrders.MidLayer, 28, Parallax[1], 2.3f, 0.55f, 0.55f, 1f),
                Make(Kind.Near, root, "LayerNear", mat, SortingOrders.NearLayer, 220, Parallax[2], 1.25f, 0.7f, 0.28f, 1f),
                Make(Kind.Front, root, "LayerFront", mat, SortingOrders.FrontLayer, 6, Parallax[3], 8.5f, 0.3f, 0.35f, 0.5f),
                // свой атлас и свой материал — один вызов отрисовки, только во льду
                Make(Kind.Ice, root, "LayerIce", SpriteFactory.IceMaterial, SortingOrders.IceLayer, 8, IceParallax, 2.8f, 0.5f, 0.6f, 1f),
                // атлас новых локаций — тоже свой материал и один вызов, только в них
                Make(Kind.Zone, root, "LayerZone", SpriteFactory.ZonesMaterial, SortingOrders.IceLayer, 8, ZoneParallax, 3.0f, 0.5f, 0.62f, 1f),
            };
        }

        static Layer Make(Kind kind, Transform root, string name, Material mat, int order, int capacity,
                          float parallax, float spacing, float bright, float fog, float alpha)
        {
            var l = new Layer
            {
                Kind = kind, Batch = new QuadBatch(root, name, mat, order, capacity),
                Parallax = parallax, Spacing = spacing, Bright = bright, Fog = fog, Alpha = alpha,
                CacheK = new int[CacheSize], CacheBiome = new byte[CacheSize], HideBelow = int.MinValue,
            };
            for (int i = 0; i < CacheSize; i++) l.CacheK[i] = int.MinValue;
            return l;
        }

        /// <param name="menuDrift">0…1: в меню слои медленно плывут вбок — «камера» осматривается.</param>
        public void Tick(float dt, float scrollSpeed, float meters, DepthLook look, bool highDetail,
                         bool empty, float menuDrift)
        {
            _time += dt;
            _scroll += scrollSpeed * dt;
            _drift = Mathf.MoveTowards(_drift, menuDrift, dt * 0.25f);

            Biome biome = DepthLook.BiomeMix(meters, out Biome next, out float mix);
            if (ZoneBiome >= 0) { biome = next = (Biome)ZoneBiome; mix = 0f; }

            for (int li = 0; li < _layers.Length; li++)
            {
                ref var l = ref _layers[li];
                l.Batch.Clear();
                bool off = empty || (l.Kind == Kind.Front && !highDetail);
                float u = _scroll * l.Parallax;
                float margin = 4.5f;
                int k0 = Mathf.CeilToInt((u + _field.Bottom - margin) / l.Spacing);
                int k1 = Mathf.FloorToInt((u + _field.Top + margin) / l.Spacing);

                if (empty) _wasEmpty = true;
                else if (_wasEmpty)
                {
                    // после пустой сцены всё, что сейчас в кадре, остаётся пустым:
                    // декор возвращается только сверху, вместе с миром
                    for (int j = 0; j < _layers.Length; j++)
                    {
                        float uj = _scroll * _layers[j].Parallax;
                        _layers[j].HideBelow = Mathf.FloorToInt((uj + _field.Top + 4.5f) / _layers[j].Spacing);
                    }
                    _wasEmpty = false;
                }

                if (!off)
                {
                    float dx = Mathf.Sin(_time * Mathf.PI * 2f / 32f) * 0.22f * _drift * (1.2f - l.Parallax * 0.5f);
                    for (int k = k0; k <= k1; k++)
                    {
                        if (k <= l.HideBelow) continue;
                        float y = k * l.Spacing - u;
                        if (l.Kind == Kind.Ice)
                        {
                            if (SlotIce(ref l, k)) EmitIce(ref l, li, k, y, dx, look);
                            continue;
                        }
                        if (l.Kind == Kind.Zone)
                        {
                            int kit = SlotKit(ref l, k);
                            if (kit >= 0) EmitZone(ref l, li, k, y, dx, look, kit);
                            continue;
                        }
                        Biome b = SlotBiome(ref l, li, k, biome, next, mix);
                        Emit(ref l, li, k, y, dx, b, look, highDetail);
                    }
                }
                l.Batch.Commit();
            }
            TickCliff(look, empty);
        }

        /// <summary>
        /// Обрыв в боковых полосах: виден только там, где экран шире игрового
        /// поля. Кромка — шум по высоте мира (едет с ближним планом), цвет — из
        /// вида глубины: тёмная глубина у края экрана, тон тумана в теле, свет
        /// кромки у края поля. Без него полосы заполнялись стопками валунов.
        /// </summary>
        void TickCliff(DepthLook look, bool empty)
        {
            float hw = _field.HalfWidth;
            bool band = hw > _field.PlayHalfWidth + 0.6f;
            _cliff.enabled = band && !empty;
            if (!_cliff.enabled) return;

            float u = _scroll * Parallax[2];
            float dx = Mathf.Sin(_time * Mathf.PI * 2f / 32f) * 0.22f * _drift * (1.2f - Parallax[2] * 0.5f);
            float top = _field.Top + 1f, bottom = _field.Bottom - 1f;
            float step = (top - bottom) / (CliffRows - 1);
            Color32 deep = QuadBatch.ToVertex(look.WaterBottom);
            Color32 body = QuadBatch.ToVertex(Color.Lerp(look.Fog, look.EdgeLight, 0.35f));
            Color32 rim = QuadBatch.ToVertex(look.EdgeLight);
            for (int side = 0; side < 2; side++)
            {
                float sd = side == 0 ? -1f : 1f;
                int b0 = side * CliffRows * 3;
                for (int i = 0; i < CliffRows; i++)
                {
                    float y = bottom + i * step;
                    float wy = y + u;
                    float noise = (Mathf.PerlinNoise(side * 13.7f + 3f, wy * 0.21f) - 0.5f) * 0.9f
                                + (Mathf.PerlinNoise(side * 7.1f + 1f, wy * 0.9f) - 0.5f) * 0.25f;
                    float inner = _field.PlayHalfWidth + 0.45f + noise;
                    float x0 = sd * (hw + 1.5f) + dx, x1 = sd * (inner + 1.2f) + dx, x2 = sd * inner + dx;
                    int v = b0 + i * 3;
                    _cliffV[v] = new Vector3(x0, y, 0f);
                    _cliffV[v + 1] = new Vector3(x1, y, 0f);
                    _cliffV[v + 2] = new Vector3(x2, y, 0f);
                    _cliffC[v] = deep; _cliffC[v + 1] = body; _cliffC[v + 2] = rim;
                    _cliffUv[v] = new Vector2(x0 * 0.5f, wy * 0.25f);
                    _cliffUv[v + 1] = new Vector2(x1 * 0.5f, wy * 0.25f);
                    _cliffUv[v + 2] = new Vector2(x2 * 0.5f, wy * 0.25f);
                }
            }
            _cliffMesh.vertices = _cliffV;
            _cliffMesh.colors32 = _cliffC;
            _cliffMesh.uv = _cliffUv;
        }

        /// <summary>Слот ближнего или переднего плана, вошедший в кадр во льду: камни в инее, без живого.</summary>
        const byte IceSlot = 255;

        /// <summary>Иней на камнях ближнего плана во льду (сумма каналов 2.17 — без белого).</summary>
        static readonly Color Frost = Palette.Hex("9CB8CC");

        Biome SlotBiome(ref Layer l, int li, int k, Biome cur, Biome next, float mix)
        {
            int idx = ((k % CacheSize) + CacheSize) % CacheSize;
            if (l.CacheK[idx] == k) return (Biome)l.CacheBiome[idx];
            if (IceActive && (l.Kind == Kind.Near || l.Kind == Kind.Front))
            {
                l.CacheK[idx] = k;
                l.CacheBiome[idx] = IceSlot;
                return (Biome)IceSlot;
            }
            Biome b = H(li, k, 91) < mix ? next : cur;
            l.CacheK[idx] = k;
            l.CacheBiome[idx] = (byte)b;
            return b;
        }

        /// <summary>Лёд ли в слоте: решается, когда слот впервые входит в кадр.</summary>
        bool SlotIce(ref Layer l, int k)
        {
            int idx = ((k % CacheSize) + CacheSize) % CacheSize;
            if (l.CacheK[idx] == k) return l.CacheBiome[idx] != 0;
            l.CacheK[idx] = k;
            l.CacheBiome[idx] = (byte)(IceActive ? 1 : 0);
            return IceActive;
        }

        /// <summary>Набор новой локации в слоте: решается, когда слот впервые входит в кадр.</summary>
        int SlotKit(ref Layer l, int k)
        {
            int idx = ((k % CacheSize) + CacheSize) % CacheSize;
            if (l.CacheK[idx] == k) return l.CacheBiome[idx] - 1;
            l.CacheK[idx] = k;
            l.CacheBiome[idx] = (byte)(ZoneKit + 1);
            return ZoneKit;
        }

        static readonly ZonePiece[][] ZoneKits =
        {
            new[] { ZonePiece.WFarHull, ZonePiece.WMast, ZonePiece.WFarHull, ZonePiece.WFrame },          // корабль
            new[] { ZonePiece.RFarCage, ZonePiece.RRibArc, ZonePiece.RFarCage, ZonePiece.RVertebraA },    // рёбра
            new[] { ZonePiece.GFarIslands, ZonePiece.GIslandB, ZonePiece.GFarIslands, ZonePiece.GIslandC }, // сад
        };

        /// <summary>Дальний слой новой локации: силуэты у краёв, утопленные в тумане локации.</summary>
        void EmitZone(ref Layer l, int li, int k, float y, float dx, DepthLook look, int kit)
        {
            int set = kit - (int)ZoneKind.Wreck;
            if (set < 0 || set >= ZoneKits.Length) return;
            float hw = _field.HalfWidth;
            float side = H(li, k, 1) < 0.5f ? -1f : 1f;
            if ((k & 1) == 0) side = -side;
            var piece = Pick(ZoneKits[set], H(li, k, 2));
            var info = SpriteFactory.Zone(piece);
            float h = piece == ZonePiece.WMast ? Mathf.Lerp(3.5f, 4.5f, H(li, k, 3))
                    : info.Aspect > 1.6f ? Mathf.Lerp(1.6f, 2.2f, H(li, k, 3))
                    : Mathf.Lerp(1.2f, 1.7f, H(li, k, 3));
            float w = h * info.Aspect;
            float x = side * (hw + w * Mathf.Lerp(-0.3f, 0.05f, H(li, k, 4))) + dx;
            float calm = CentreCalm(x, w, 0.35f);
            float jitter = 0.9f + 0.2f * H(7, k, (int)piece);
            Color target = Color.Lerp(Mul(info.Avg, l.Bright * jitter * calm), look.Fog, l.Fog);
            Color tint = TintFor(target, info.Avg);
            tint.a = l.Alpha;
            float tilt = piece == ZonePiece.WMast ? side * 0.3f : 0f;
            l.Batch.Add(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f), tilt, tint, info.Uv, side > 0f);
        }

        static readonly IcePiece[] IceKit =
        {
            IcePiece.ShelfA, IcePiece.FloeA, IcePiece.FrozenColumn, IcePiece.ShelfB,
            IcePiece.FloeB, IcePiece.FloeC, IcePiece.FrozenColumn, IcePiece.IceArch,
        };

        /// <summary>Ледяной слой: дальние уступы, льдины и колонны у краёв, утопленные в синеве.</summary>
        void EmitIce(ref Layer l, int li, int k, float y, float dx, DepthLook look)
        {
            float hw = _field.HalfWidth;
            float side = H(li, k, 1) < 0.5f ? -1f : 1f;
            if ((k & 1) == 0) side = -side;
            var piece = Pick(IceKit, H(li, k, 2));
            var info = SpriteFactory.Ice(piece);
            float h = piece == IcePiece.FrozenColumn ? Mathf.Lerp(4f, 5.5f, H(li, k, 3))
                    : piece == IcePiece.IceArch ? Mathf.Lerp(2.6f, 3.2f, H(li, k, 3))
                    : Mathf.Lerp(1.4f, 2.1f, H(li, k, 3));
            float w = h * info.Aspect;
            float x = side * (hw + w * Mathf.Lerp(-0.12f, 0.2f, H(li, k, 4))) + dx;
            float calm = CentreCalm(x, w, 0.35f);
            float jitter = 0.9f + 0.2f * H(7, k, (int)piece);
            Color target = Color.Lerp(Mul(info.Avg, l.Bright * jitter * calm), look.Fog, l.Fog);
            Color tint = TintFor(target, info.Avg);
            tint.a = l.Alpha;
            l.Batch.Add(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f), 0f, tint, info.Uv, side > 0f);
        }

        // ------------------------------------------------------------ хеш

        static float H(int layer, int k, int salt)
        {
            unchecked
            {
                uint h = (uint)k * 0x9E3779B1U ^ (uint)(layer + 1) * 0x85EBCA77U ^ (uint)salt * 0xC2B2AE3DU;
                h ^= h >> 15; h *= 0x2C1B3C6DU; h ^= h >> 12; h *= 0x297A2D39U; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static T Pick<T>(T[] set, float r) => set[Mathf.Min(set.Length - 1, (int)(r * set.Length))];

        // ------------------------------------------------------------ наборы биомов

        static readonly WorldPiece[][] FarKit =
        {
            new[] { WorldPiece.FarRidgeA, WorldPiece.FarRidgeB, WorldPiece.FarRidgeC, WorldPiece.FarArch },   // бездна
            new[] { WorldPiece.FarRidgeA, WorldPiece.FarRidgeB, WorldPiece.FarRidgeC },                       // впадина
            new[] { WorldPiece.FarRidgeB, WorldPiece.FarArch, WorldPiece.FarRidgeC },                         // каньон
            new[] { WorldPiece.FarRuinsA, WorldPiece.FarRuinsB, WorldPiece.FarRidgeA },                       // руины
            new[] { WorldPiece.FarKelp, WorldPiece.FarKelp, WorldPiece.FarRidgeC },                           // отмель
        };

        static readonly WorldPiece[][] MidKit =
        {
            new[] { WorldPiece.SpireA, WorldPiece.SpireB, WorldPiece.SpireC, WorldPiece.KelpTallB },
            new[] { WorldPiece.SpireA, WorldPiece.CanyonStrata, WorldPiece.SpireC, WorldPiece.ArchMid },
            new[] { WorldPiece.CanyonStrata, WorldPiece.SpireB, WorldPiece.KelpTallA, WorldPiece.CanyonStrata },
            new[] { WorldPiece.ColumnA, WorldPiece.ColumnB, WorldPiece.ArchMid, WorldPiece.WallWindow, WorldPiece.Stairs },
            new[] { WorldPiece.KelpTallA, WorldPiece.KelpTallB, WorldPiece.KelpTallA, WorldPiece.SpireB },
        };

        /// <summary>Особенность на кромке ближнего плана: живое и обломки по биому.</summary>
        static readonly WorldPiece[][] NearKit =
        {
            new[] { WorldPiece.KelpShort, WorldPiece.TubesB, WorldPiece.AnemoneB, WorldPiece.Brain },
            new[] { WorldPiece.Brain, WorldPiece.FanB, WorldPiece.AnemoneA, WorldPiece.KelpShort },
            new[] { WorldPiece.CoralBranchA, WorldPiece.CoralBranchB, WorldPiece.FanA, WorldPiece.Brain, WorldPiece.KelpShort },
            new[] { WorldPiece.DrumA, WorldPiece.DrumB, WorldPiece.Amphora, WorldPiece.AnchorOld, WorldPiece.CoralBranchA, WorldPiece.Chain },
            new[] { WorldPiece.GrassA, WorldPiece.GrassB, WorldPiece.KelpShort, WorldPiece.CoralBranchB, WorldPiece.FanA },
        };

        static readonly WorldPiece[][] DetailKit =
        {
            new[] { WorldPiece.AnemoneA, WorldPiece.TubesA, WorldPiece.TubesB, WorldPiece.UrchinDecor, WorldPiece.Barnacles, WorldPiece.Pebbles },
            new[] { WorldPiece.StarA, WorldPiece.UrchinDecor, WorldPiece.ShellB, WorldPiece.Barnacles, WorldPiece.AnemoneB },
            new[] { WorldPiece.AnemoneA, WorldPiece.AnemoneB, WorldPiece.StarB, WorldPiece.TubesA, WorldPiece.ShellA },
            new[] { WorldPiece.ShardA, WorldPiece.ShardB, WorldPiece.RustPlate, WorldPiece.Barnacles, WorldPiece.StarC, WorldPiece.ShellC },
            new[] { WorldPiece.ShellA, WorldPiece.ShellB, WorldPiece.ShellC, WorldPiece.StarA, WorldPiece.StarB, WorldPiece.Pebbles },
        };

        // ------------------------------------------------------------ размеры и повадки

        /// <summary>Высота рисунка в мире, юнитов, и размах качания (доля высоты).</summary>
        internal static void Size(WorldPiece p, float r, out float height, out float sway)
        {
            sway = 0f;
            switch (p)
            {
                case WorldPiece.FarRidgeA: case WorldPiece.FarRidgeB: case WorldPiece.FarRidgeC: height = Mathf.Lerp(2.6f, 3.6f, r); break;
                case WorldPiece.FarRuinsA: case WorldPiece.FarRuinsB: height = Mathf.Lerp(2.4f, 3.2f, r); break;
                case WorldPiece.FarKelp: height = Mathf.Lerp(5f, 7f, r); sway = 0.03f; break;
                case WorldPiece.FarArch: height = Mathf.Lerp(3f, 4f, r); break;
                case WorldPiece.SpireA: case WorldPiece.SpireB: case WorldPiece.SpireC: height = Mathf.Lerp(4.5f, 7f, r); break;
                case WorldPiece.ColumnA: case WorldPiece.ColumnB: height = Mathf.Lerp(4f, 6f, r); break;
                case WorldPiece.ArchMid: height = Mathf.Lerp(3f, 3.8f, r); break;
                case WorldPiece.WallWindow: height = Mathf.Lerp(2.8f, 3.6f, r); break;
                case WorldPiece.Stairs: height = Mathf.Lerp(1.6f, 2.2f, r); break;
                case WorldPiece.KelpTallA: case WorldPiece.KelpTallB: height = Mathf.Lerp(5f, 7.5f, r); sway = 0.07f; break;
                case WorldPiece.CanyonStrata: height = Mathf.Lerp(6f, 8f, r); break;
                case WorldPiece.RockA: case WorldPiece.RockB: case WorldPiece.RockC: case WorldPiece.RockD: height = Mathf.Lerp(1.4f, 2.0f, r); break;
                case WorldPiece.CoralBranchA: case WorldPiece.CoralBranchB: height = Mathf.Lerp(1.3f, 1.9f, r); sway = 0.03f; break;
                case WorldPiece.FanA: case WorldPiece.FanB: height = Mathf.Lerp(1.3f, 1.8f, r); sway = 0.04f; break;
                case WorldPiece.Brain: height = Mathf.Lerp(0.7f, 1f, r); break;
                case WorldPiece.KelpShort: height = Mathf.Lerp(2.6f, 3.6f, r); sway = 0.09f; break;
                case WorldPiece.GrassA: case WorldPiece.GrassB: height = Mathf.Lerp(0.8f, 1.1f, r); sway = 0.08f; break;
                case WorldPiece.DrumA: case WorldPiece.DrumB: height = Mathf.Lerp(0.6f, 0.8f, r); break;
                case WorldPiece.Amphora: height = Mathf.Lerp(0.6f, 0.8f, r); break;
                case WorldPiece.AnchorOld: height = Mathf.Lerp(1.1f, 1.4f, r); break;
                case WorldPiece.Chain: height = Mathf.Lerp(1.6f, 2.4f, r); sway = 0.03f; break;
                case WorldPiece.AnemoneA: case WorldPiece.AnemoneB: height = Mathf.Lerp(0.45f, 0.6f, r); sway = 0.06f; break;
                case WorldPiece.TubesA: case WorldPiece.TubesB: height = Mathf.Lerp(0.45f, 0.6f, r); sway = 0.04f; break;
                case WorldPiece.StarA: case WorldPiece.StarB: case WorldPiece.StarC: height = Mathf.Lerp(0.28f, 0.38f, r); break;
                case WorldPiece.UrchinDecor: height = Mathf.Lerp(0.2f, 0.26f, r); break;
                case WorldPiece.ShellA: case WorldPiece.ShellB: case WorldPiece.ShellC: height = Mathf.Lerp(0.2f, 0.28f, r); break;
                case WorldPiece.Pebbles: height = Mathf.Lerp(0.22f, 0.28f, r); break;
                case WorldPiece.ShardA: case WorldPiece.ShardB: height = Mathf.Lerp(0.25f, 0.32f, r); break;
                case WorldPiece.RustPlate: height = Mathf.Lerp(0.4f, 0.5f, r); break;
                case WorldPiece.Barnacles: height = Mathf.Lerp(0.28f, 0.36f, r); break;
                case WorldPiece.FrontLeaf: height = Mathf.Lerp(4.5f, 6f, r); sway = 0.06f; break;
                case WorldPiece.FrontBubble: height = Mathf.Lerp(0.9f, 1.4f, r); break;
                default: height = 1f; break;
            }
        }

        // ------------------------------------------------------------ слоты

        void Emit(ref Layer l, int li, int k, float y, float dx, Biome biome, DepthLook look, bool highDetail)
        {
            bool ice = (byte)biome == IceSlot;
            if (ice) biome = Biome.Abyss;
            int bi = (int)biome;
            float hw = _field.HalfWidth;
            bool band = hw > _field.PlayHalfWidth + 0.6f;   // широкий экран: есть полосы вне игры
            float side = H(li, k, 1) < 0.5f ? -1f : 1f;
            if ((k & 1) == 0) side = -side;                  // стороны в основном чередуются

            switch (l.Kind)
            {
                case Kind.Far:
                {
                    var piece = Pick(FarKit[bi], H(li, k, 2));
                    Size(piece, H(li, k, 3), out float h, out float sw);
                    var info = SpriteFactory.World(piece);
                    bool ridge = piece == WorldPiece.FarRidgeA || piece == WorldPiece.FarRidgeB || piece == WorldPiece.FarRidgeC;
                    if (ridge)
                    {
                        // хребет, поставленный на ребро, — дальний утёс у края: мы
                        // поднимаемся вдоль стены, а не смотрим на горизонт
                        float x = side * (hw + h * 0.05f) + dx;
                        QuadRot(ref l, piece, new Vector2(x, y), h, side * Mathf.PI * 0.5f, side < 0f, look, 1f, k);
                    }
                    else
                    {
                        float w = h * info.Aspect;
                        // в колонну дальний план заходит только тонущим в тумане
                        float x = side * Mathf.Lerp(hw * 0.25f, hw - w * 0.3f, H(li, k, 4)) + dx;
                        Quad(ref l, piece, new Vector2(x, y), h, H(li, k, 5) < 0.5f, sw, look, CentreCalm(x, w), k);
                    }
                    break;
                }
                case Kind.Mid:
                {
                    var piece = Pick(MidKit[bi], H(li, k, 2));
                    Size(piece, H(li, k, 3), out float h, out float sw);
                    var info = SpriteFactory.World(piece);
                    float w = h * info.Aspect;
                    // центр рисунка у самого края: в кадре 30–55 % его ширины
                    float inner = band ? _field.PlayHalfWidth + w * 0.3f : hw + w * Mathf.Lerp(-0.05f, 0.2f, H(li, k, 4));
                    float x = side * inner + dx;
                    bool flip = side > 0f;
                    if (piece == WorldPiece.ArchMid || piece == WorldPiece.WallWindow || piece == WorldPiece.Stairs)
                        x = side * (hw + w * 0.1f) + dx;
                    float centreCalm = CentreCalm(x, w, 0.28f);
                    Quad(ref l, piece, new Vector2(x, y), h, flip, sw, look, centreCalm, k);
                    if (piece == WorldPiece.WallWindow && look.Shafts > 0.05f)
                    {
                        // свет из окна руин: косой тусклый луч
                        var beam = SpriteFactory.World(WorldPiece.WindowBeam);
                        Vector2 at = new Vector2(x + (flip ? 1f : -1f) * w * 0.07f - side * 0.2f, y - h * 0.15f);
                        Color c = Palette.SafeLight(Color.Lerp(look.EdgeLight, Palette.Foam, 0.4f))
                                  .WithAlpha(0.07f * look.Shafts * centreCalm);
                        l.Batch.Add(at, new Vector2(h * 0.18f, h * 0.55f), 0f, c, beam.Uv);
                    }
                    break;
                }
                case Kind.Near:
                {
                    // Ближний план — сплошная скальная кромка по ОБОИМ краям: камни
                    // перекрываются (шаг меньше высоты), висящих в воде валунов нет.
                    // На внутренней кромке камня — особенность биома и мелочи.
                    // На широком экране боковую полосу занимает обрыв (TickCliff);
                    // камни — одной колонной на его кромке. Сеткой из нескольких
                    // колонн полоса превращалась в стопки одинаковых валунов [ТЕСТ, альбом].
                    int columns = 1;
                    for (int s = 0; s < 2 * columns; s++)
                    {
                        float sd = (s & 1) == 0 ? -1f : 1f;
                        int col = s >> 1;
                        int salt = s * 100;
                        var rock = Pick(Walls, H(li, k, 2 + salt));
                        Size(rock, H(li, k, 3 + salt), out float rh, out _);
                        var rinfo = SpriteFactory.World(rock);
                        float rw = rh * rinfo.Aspect;
                        // камень почти целиком за краем: в кадре 22–42 % его ширины
                        float rx, calm = 1f;
                        if (band && col == 1)
                        {
                            // дальняя колонна у края экрана: крупнее, темнее, наполовину за краем
                            rh *= 1.5f; rw *= 1.5f; calm = 0.55f;
                            rx = sd * (hw + rw * Mathf.Lerp(-0.22f, 0.05f, H(li, k, 4 + salt)));
                        }
                        else if (band) rx = sd * (_field.PlayHalfWidth + rw * Mathf.Lerp(0.2f, 0.45f, H(li, k, 4 + salt)));
                        else rx = sd * (hw + rw * Mathf.Lerp(0.08f, 0.28f, H(li, k, 4 + salt)));
                        rx += dx;
                        float ry = y + (col > 0 ? (H(li, k, 5 + salt) - 0.5f) * l.Spacing : 0f);
                        float tilt = (H(li, k, 5 + salt) - 0.5f) * 0.3f;
                        // на обрыве широкого экрана камень — не в каждом слоте: кромка
                        // обрыва и так сплошная, а камень на камне вставал башней [ТЕСТ, альбом]
                        if (!band || H(li, k, 11 + salt) < 0.45f)
                            QuadRot(ref l, rock, new Vector2(rx, ry), rh, tilt, sd > 0f, look, calm, k * 3 + s, ice);
                        if (ice) continue;   // во льду у кромки ни кораллов, ни грибов

                        float edgeX = rx - sd * rw * 0.42f;
                        if (col == 0 && H(li, k, 6 + salt) < 0.7f)
                        {
                            var feat = Pick(NearKit[bi], H(li, k, 7 + salt));
                            Size(feat, H(li, k, 8 + salt), out float fh, out float fsw);
                            var finfo = SpriteFactory.World(feat);
                            Vector2 at = new Vector2(edgeX + sd * finfo.Aspect * fh * 0.15f, ry + rh * 0.22f + fh * 0.4f);
                            Quad(ref l, feat, at, fh, sd > 0f, fsw, look, 1f, k * 5 + s);
                        }
                        int details = col == 0 ? Mathf.FloorToInt(H(li, k, 9 + salt) * 2.6f) : 0;
                        if (!highDetail) details /= 2;
                        for (int d = 0; d < details; d++)
                        {
                            var dp = Pick(DetailKit[bi], H(li, k, 10 + d + salt));
                            Size(dp, H(li, k, 20 + d + salt), out float dh, out float dsw);
                            Vector2 at = new Vector2(edgeX + sd * (0.05f + 0.3f * H(li, k, 40 + d + salt)),
                                                     ry + (H(li, k, 30 + d + salt) - 0.4f) * rh * 0.7f);
                            Quad(ref l, dp, at, dh, H(li, k, 50 + d + salt) < 0.5f, dsw, look, 1f, k * 7 + d + s * 3);
                        }
                    }
                    break;
                }
                default:
                {
                    if (H(li, k, 2) < 0.45f) break;            // передний план редок
                    var piece = !ice && H(li, k, 3) < 0.7f ? WorldPiece.FrontLeaf : WorldPiece.FrontBubble;
                    Size(piece, H(li, k, 4), out float h, out float sw);
                    var info = SpriteFactory.World(piece);
                    float x = side * (hw + h * info.Aspect * 0.08f) + dx;
                    Quad(ref l, piece, new Vector2(x, y), h, side > 0f, sw, look, 1f, k);
                    break;
                }
            }
        }

        /// <summary>Камни скальной кромки ближнего плана.</summary>
        static readonly WorldPiece[] Walls = { WorldPiece.RockA, WorldPiece.RockB, WorldPiece.RockC, WorldPiece.RockD };

        /// <summary>
        /// Насколько рисунок, заходящий в колонну, приглушается: 1 — у края,
        /// <paramref name="min"/> — если закрывает центр. Дальний план тонет в
        /// тумане и так (0.45), средний и ледяной крупные — до 0.28–0.35: арки
        /// руин шириной в экран иначе светлили колонну до L 0.033 [ТЕСТ].
        /// </summary>
        float CentreCalm(float x, float w, float min = 0.45f)
        {
            float innerEdge = Mathf.Abs(x) - w * 0.5f;
            return Mathf.Lerp(min, 1f, Mathf.Clamp01(innerEdge / (_field.HalfWidth * 0.55f)));
        }

        /// <summary>
        /// Один рисунок слоя. Цвет считается так: средний цвет рисунка темнеет до
        /// яркости слоя и тонет в тумане глубины; вершинный тон — то, что даёт
        /// именно такой средний цвет. Оттенок слегка гуляет от слота к слоту.
        /// </summary>
        void Quad(ref Layer l, WorldPiece piece, Vector2 at, float height, bool flip, float sway,
                  DepthLook look, float calm, int seed)
        {
            var info = SpriteFactory.World(piece);
            float w = height * info.Aspect;
            float jitter = 0.9f + 0.2f * H(7, seed, (int)piece);
            Color target = Color.Lerp(Mul(info.Avg, l.Bright * jitter * calm), look.Fog, l.Fog);
            Color tint = TintFor(target, info.Avg);
            tint.a = l.Alpha;
            float swayX = sway > 0f ? OceanCurrent.At(at.y, seed * 0.37f) * sway * height : 0f;
            l.Batch.Add(at, new Vector2(w * 0.5f, height * 0.5f), 0f, tint, info.Uv, flip, swayX);
        }

        /// <summary>Как <see cref="Quad"/>, но с поворотом и без качания.</summary>
        void QuadRot(ref Layer l, WorldPiece piece, Vector2 at, float height, float angle, bool flip,
                     DepthLook look, float calm, int seed, bool frost = false)
        {
            var info = SpriteFactory.World(piece);
            float w = height * info.Aspect;
            float jitter = 0.9f + 0.2f * H(7, seed, (int)piece);
            Color target = Color.Lerp(Mul(info.Avg, l.Bright * jitter * calm), look.Fog, l.Fog);
            if (frost) target = Color.Lerp(target, Mul(Frost, l.Bright * 0.55f), 0.55f);
            Color tint = TintFor(target, info.Avg);
            tint.a = l.Alpha;
            l.Batch.Add(at, new Vector2(w * 0.5f, height * 0.5f), angle, tint, info.Uv, flip);
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>
        /// Вершинный тон (sRGB), который превращает средний цвет рисунка в нужный:
        /// по каналам в линейном пространстве — именно там шейдер их умножает.
        /// </summary>
        internal static Color TintFor(Color target, Color avg)
        {
            Color t = target.linear, a = avg.linear;
            float r = Mathf.Clamp01(t.r / Mathf.Max(0.004f, a.r));
            float g = Mathf.Clamp01(t.g / Mathf.Max(0.004f, a.g));
            float b = Mathf.Clamp01(t.b / Mathf.Max(0.004f, a.b));
            return new Color(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b), 1f);
        }
    }
}
