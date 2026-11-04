using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Локация — крупный участок дна со своим цветом воды, стенами-каньоном
    /// и композициями из коралловых шаров. Занимает три с лишним высоты экрана
    /// и проплывает мимо целиком, как уровень.
    ///
    /// Стены — два процедурных меша с фактурой (камень, кладка, песок, лёд) и
    /// цветом вершин: один вызов отрисовки на стену. Стены декоративные —
    /// пузырёк зафиксирован по центру и до них не достаёт; их задача сузить
    /// проход и задать силуэт места. Опасны только шары внутри.
    ///
    /// Оформление — корка по кромке, кораллы, колонны, мелочи на стене, ворота
    /// входа и выхода, тусклые силуэты в глубине — лежит в слоях
    /// <see cref="QuadBatch"/>: передний перед стенами, задний за ними, плюс
    /// слой ледяного атласа. Всё это только вид: шары, их места и столкновения
    /// оно не трогает.
    /// </summary>
    public class OceanZone : MonoBehaviour
    {
        public class Blob
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public bool Alive;
            public float DyingTimer;
            public float Radius;
            public Vector2 Center => Tr.position;
        }

        /// <summary>Рисунок оформления (координаты локальные, цвет sRGB).</summary>
        public struct Item
        {
            public Rect Uv;
            public Vector2 Pos, Half;
            public float Angle, Sway, PivotY, Breath;
            public bool Flip;
            /// <summary>Тает книзу: низ прозрачный — у столбов и силуэтов нет ровного среза.</summary>
            public bool FadeBottom;
            public Color Color;
            public byte Layer;
        }

        /// <summary>
        /// Слои оформления: перед стенами, за стенами; то же из ледяного атласа и из
        /// атласа новых локаций (корабль, рёбра, сад).
        /// </summary>
        public const byte FrontLayer = 0, BackLayer = 1, IceLayer = 2, IceBackLayer = 3, ZoneLayer = 4, ZoneBackLayer = 5;

        /// <summary>
        /// Слот композиции новой локации: какой шаблон, где, и какие шары его
        /// (номера в раскладке). По нему оформление кладёт доски палубы, кость ребра,
        /// тело острова прямо под шары.
        /// </summary>
        public struct Slot
        {
            public string Pattern;
            public float Y, Centre, Half;
            public int First, Count;
        }

        public bool Pooled { get; set; }
        public string DisplayName { get; private set; }
        public Color BannerColor { get; private set; }
        public Color WaterTop { get; private set; }
        public Color WaterBottom { get; private set; }
        /// <summary>Набор дальних слоёв фона, пока локация на экране.</summary>
        public Biome BackBiome { get; private set; }
        /// <summary>Это ледяная локация — для звука и фона.</summary>
        public bool IsIce { get; private set; }
        /// <summary>Вид локации (ZoneCatalog).</summary>
        public ZoneKind Kind { get; private set; }
        public float Height => _height;
        public IReadOnlyList<Blob> Blobs => _blobs;

        // Рядов с запасом: внутренний край неровный в двух масштабах.
        const int WallRows = 120;
        const int WallCols = 4;

        /// <summary>
        /// У входа и выхода стена на этом отрезке уходит за край экрана: локация
        /// начинается сходом скал, а не ступенькой поперёк экрана.
        /// </summary>
        public const float Taper = 4.5f;

        readonly List<Blob> _blobs = new List<Blob>();
        int _usedBlobs;

        readonly List<Item> _items = new List<Item>();
        QuadBatch _front, _back, _ice, _iceBack, _zone, _zoneBack;

        MeshRenderer _leftRenderer, _rightRenderer;
        Mesh _leftMesh, _rightMesh;

        float _height;
        System.Func<float, float> _profile;
        /// <summary>Середина прохода в долях PlayHalfWidth (null — посередине экрана).</summary>
        System.Func<float, float> _shift;
        Playfield _field;
        int _seed;
        float _time;

        public float TopEdge => transform.position.y + _height * 0.5f;
        public float BottomEdge => transform.position.y - _height * 0.5f;

        // ---------------------------------------------------------- построение

        public void Activate(ZoneTheme theme, System.Func<float, float> profile, float height,
                             Playfield field, int seed)
            => Activate(theme, profile, null, true, theme.Style == WallStyle.Ice ? ZoneKind.Ice : ZoneKind.Ruins,
                        height, field, seed);

        /// <summary>Локация нового вида: форма прохода, сдвиг и стены — из записи каталога.</summary>
        public void Activate(ZoneDef def, float height, Playfield field, int seed)
            => Activate(def.Theme, def.HalfWidth, def.Shift, def.Walls, def.Kind, height, field, seed);

        void Activate(ZoneTheme theme, System.Func<float, float> profile, System.Func<float, float> shift,
                      bool walls, ZoneKind kind, float height, Playfield field, int seed)
        {
            Pooled = false;
            _height = height;
            _profile = profile;
            _shift = shift;
            Kind = kind;
            _field = field;
            _seed = seed;
            _time = 0f;
            DisplayName = Loc.Get(theme.NameKey);
            BannerColor = theme.Banner;
            WaterTop = theme.WaterTop;
            WaterBottom = theme.WaterBottom;
            BackBiome = theme.Back;
            IsIce = theme.Style == WallStyle.Ice;

            transform.position = new Vector3(0f, field.Top + height * 0.5f + 0.5f, 0f);

            EnsureParts();
            var wallMat = SpriteFactory.ZoneWallMaterial(theme.Style);
            _leftRenderer.sharedMaterial = wallMat;
            _rightRenderer.sharedMaterial = wallMat;
            BuildWall(_leftMesh, theme, right: false);
            BuildWall(_rightMesh, theme, right: true);
            // у открытой воды (сад) стен нет вовсе
            _leftRenderer.enabled = walls;
            _rightRenderer.enabled = walls;

            _usedBlobs = 0;
            _items.Clear();

            gameObject.SetActive(true);
        }

        /// <summary>Спрятать неиспользованные объекты — вызывается после расстановки содержимого.</summary>
        public void FinishSetup()
        {
            for (int i = _usedBlobs; i < _blobs.Count; i++)
            {
                _blobs[i].Alive = false;
                _blobs[i].DyingTimer = 0f;
                _blobs[i].Tr.gameObject.SetActive(false);
            }
            RebuildDecor();
        }

        void EnsureParts()
        {
            if (_leftMesh != null) return;
            _leftRenderer = CreateWall("WallLeft", out _leftMesh);
            _rightRenderer = CreateWall("WallRight", out _rightMesh);
            _front = new QuadBatch(transform, "ZoneDecor", SpriteFactory.WorldMaterial, SortingOrders.ZoneDecor, 320);
            _back = new QuadBatch(transform, "ZoneBack", SpriteFactory.WorldMaterial, SortingOrders.ZoneBack, 48);
            _ice = new QuadBatch(transform, "ZoneIce", SpriteFactory.IceMaterial, SortingOrders.ZoneDecor + 1, 240);
            _iceBack = new QuadBatch(transform, "ZoneIceBack", SpriteFactory.IceMaterial, SortingOrders.ZoneBack + 1, 48);
            _zone = new QuadBatch(transform, "ZoneKit", SpriteFactory.ZonesMaterial, SortingOrders.ZoneDecor + 2, 320);
            _zoneBack = new QuadBatch(transform, "ZoneKitBack", SpriteFactory.ZonesMaterial, SortingOrders.ZoneBack + 2, 96);
        }

        MeshRenderer CreateWall(string wallName, out Mesh mesh)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sortingOrder = SortingOrders.ZoneWall;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            mesh = new Mesh { name = wallName + "Mesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return renderer;
        }

        /// <summary>
        /// Внутренний край стены (модуль x) на доле высоты t: профиль локации,
        /// неровность в двух масштабах и сход за край экрана у входа и выхода.
        /// Проход для шаров считается по одному профилю, без неровности и схода, —
        /// как и раньше; стена только шире его у концов.
        /// </summary>
        public float InnerEdge(float t, bool right)
        {
            int seed = right ? _seed + 977 : _seed;
            float noise = (Mathf.PerlinNoise(seed * 0.37f, t * 9f) - 0.5f) * 0.6f
                        + (Mathf.PerlinNoise(seed * 0.53f + 7f, t * 33f) - 0.5f) * 0.2f;
            // сдвиг прохода: правая кромка дальше от центра на столько же, на сколько левая ближе
            float shift = _shift != null ? _shift(t) * (right ? 1f : -1f) : 0f;
            float inner = (_profile(t) + shift) * _field.PlayHalfWidth + noise;
            float fromEnd = Mathf.Min(t, 1f - t) * _height;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fromEnd / Taper));
            return Mathf.Lerp(Mathf.Max(inner, _field.HalfWidth + 0.8f), inner, k);
        }

        /// <summary>Проход по профилю (без неровности) — по нему стоят ворота.</summary>
        public float Passage(float t) => _profile(t) * _field.PlayHalfWidth;

        /// <summary>Середина прохода по x (у корабля гуляет, у прочих — 0).</summary>
        public float Centre(float t) => _shift != null ? _shift(t) * _field.PlayHalfWidth : 0f;

        /// <summary>
        /// Полоса из WallRows рядов и четырёх колонок: глубина стены, тело,
        /// корка, кромка. Цвета в sRGB переводятся в линейные — иначе тёмная
        /// стена светлела бы втрое (см. <see cref="QuadBatch"/>).
        /// </summary>
        void BuildWall(Mesh mesh, ZoneTheme theme, bool right)
        {
            float sign = right ? 1f : -1f;
            float outer = _field.HalfWidth + 3f;

            var vertices = new Vector3[WallRows * WallCols];
            var colors = new Color32[WallRows * WallCols];
            var uvs = new Vector2[WallRows * WallCols];
            var triangles = new int[(WallRows - 1) * (WallCols - 1) * 6];

            Color32 deep = QuadBatch.ToVertex(theme.WallDeep);
            Color32 body = QuadBatch.ToVertex(theme.Wall);
            Color32 crust = QuadBatch.ToVertex(theme.Crust);

            for (int i = 0; i < WallRows; i++)
            {
                float t = i / (float)(WallRows - 1);
                float y = -_height * 0.5f + t * _height;
                float inner = InnerEdge(t, right);

                float[] xs = { outer, Mathf.Min(outer - 0.2f, inner + 1.8f), inner + 0.5f, inner };
                Color32[] cs = { deep, body, Color32.Lerp(body, crust, 0.35f), crust };
                for (int c = 0; c < WallCols; c++)
                {
                    int v = i * WallCols + c;
                    float x = xs[c] * sign;
                    vertices[v] = new Vector3(x, y, 0f);
                    colors[v] = cs[c];
                    // фактура привязана к локации и едет вместе с ней
                    uvs[v] = new Vector2(x * 0.5f, y * 0.25f);
                }
            }

            int tri = 0;
            for (int i = 0; i < WallRows - 1; i++)
            {
                for (int c = 0; c < WallCols - 1; c++)
                {
                    int a = i * WallCols + c, b = (i + 1) * WallCols + c;
                    triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = a + 1;
                    triangles[tri++] = a + 1; triangles[tri++] = b; triangles[tri++] = b + 1;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        // ---------------------------------------------------------- содержимое

        public void AddBlob(Vector2 localPosition, float radius, Color tint)
        {
            var blob = RentBlob();
            blob.Alive = true;
            blob.DyingTimer = 0f;
            blob.Radius = radius;

            blob.Sr.sprite = SpriteFactory.CoralBlob(Random.value < 0.5f ? 0 : 1);
            blob.Sr.color = tint;
            blob.Sr.enabled = true;

            blob.Tr.localPosition = localPosition;
            blob.Tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            blob.Tr.localScale = Vector3.one * (radius * 2f);
            blob.Tr.gameObject.SetActive(true);
        }

        /// <summary>Рисунок оформления. Только вид — на шары и столкновения не влияет.</summary>
        public void AddItem(Item item) => _items.Add(item);

        Blob RentBlob()
        {
            if (_usedBlobs < _blobs.Count) return _blobs[_usedBlobs++];

            var go = new GameObject("Blob");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.Formation;

            var blob = new Blob { Tr = go.transform, Sr = sr };
            _blobs.Add(blob);
            _usedBlobs++;
            return blob;
        }

        /// <summary>
        /// Пересобрать слои оформления: только то, что сейчас в кадре, с качанием
        /// от общего течения. Сотня-другая прямоугольников — дешевле, чем по
        /// спрайту на каждый.
        /// </summary>
        void RebuildDecor()
        {
            _front.Clear(); _back.Clear(); _ice.Clear(); _iceBack.Clear(); _zone.Clear(); _zoneBack.Clear();
            float baseY = transform.position.y;
            float top = _field.Top + 1f, bottom = _field.Bottom - 1f;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                float y = baseY + it.Pos.y;
                // у рисунков с основанием внизу центр — это основание: запас на всю высоту
                float reach = Mathf.Max(it.Half.x, it.Half.y) * 2.2f;
                if (y - reach > top || y + reach < bottom) continue;
                float sway = it.Sway > 0f ? OceanCurrent.At(y, i * 0.37f) * it.Sway * it.Half.y * 2f : 0f;
                Color c = it.Color;
                if (it.Breath > 0f)
                {
                    // светящиеся точки «дышат»: цикл 9–12 с, размах не больше 10 %
                    float period = 9f + 3f * Mathf.Repeat(i * 0.618f, 1f);
                    c.a *= 1f - it.Breath * (0.5f + 0.5f * Mathf.Sin(_time * Mathf.PI * 2f / period + i));
                }
                var batch = it.Layer == BackLayer ? _back : it.Layer == IceLayer ? _ice
                          : it.Layer == IceBackLayer ? _iceBack : it.Layer == ZoneLayer ? _zone
                          : it.Layer == ZoneBackLayer ? _zoneBack : _front;
                if (it.FadeBottom) AddFaded(batch, it, c, sway);
                else batch.Add(it.Pos, it.Half, it.Angle, c, it.Uv, it.Flip, sway, it.PivotY);
            }
            _front.Commit(); _back.Commit(); _ice.Commit(); _iceBack.Commit(); _zone.Commit(); _zoneBack.Commit();
            // пустые слои не рисуются вовсе — у прежней локации ледяных нет
            _front.Visible = _front.Count > 0;
            _back.Visible = _back.Count > 0;
            _ice.Visible = _ice.Count > 0;
            _iceBack.Visible = _iceBack.Count > 0;
            _zone.Visible = _zone.Count > 0;
            _zoneBack.Visible = _zoneBack.Count > 0;
        }

        /// <summary>Прямой рисунок с прозрачным низом (столбы ворот, силуэты в глубине).</summary>
        static void AddFaded(QuadBatch batch, Item it, Color c, float sway)
        {
            Vector2 centre = it.Pos + new Vector2(0f, -it.PivotY * it.Half.y);
            Vector2 bl = centre - it.Half, tr = centre + it.Half;
            Vector2 tl = new Vector2(bl.x + sway, tr.y), trs = new Vector2(tr.x + sway, tr.y), br = new Vector2(tr.x, bl.y);
            Color bottom = c.WithAlpha(0f);
            var uv = it.Flip ? new Rect(it.Uv.xMax, it.Uv.yMin, -it.Uv.width, it.Uv.height) : it.Uv;
            batch.AddCorners(bl, tl, trs, br, bottom, c, c, bottom, uv);
        }

        // ------------------------------------------------------------- логика

        public void Shatter(Blob blob)
        {
            if (!blob.Alive) return;
            blob.Alive = false;
            blob.DyingTimer = 0.28f;
        }

        public void ShatterAll()
        {
            for (int i = 0; i < _usedBlobs; i++)
                if (_blobs[i].Alive) Shatter(_blobs[i]);
        }

        public bool Tick(float dt, float scrollSpeed, Playfield field)
        {
            _time += dt;
            var p = transform.position;
            p.y -= scrollSpeed * dt;
            transform.position = p;

            // Шестерёнки медленно вращаются: 9–15 °/с, у соседних в разные стороны,
            // скорость плавно гуляет ±30 % за ~14 с — с инерцией, без рывков. Свет на
            // рисунке без направления, так что при вращении ничего не мигает (D-17).
            // Скорость — от номера шара, а не от Random: AddBlob и порядок случайных
            // вызовов локации не тронуты (Tools/check_world.py).
            var gear = SpriteFactory.CoralBlob(1);
            for (int i = 0; i < _usedBlobs; i++)
            {
                var b = _blobs[i];
                if (!b.Alive || b.Sr.sprite != gear) continue;
                float spin = (9f + (i * 37 % 7)) * ((i & 1) == 0 ? 1f : -1f);
                b.Tr.Rotate(0f, 0f, spin * (1f + 0.3f * Mathf.Sin(_time * 0.45f + i)) * dt);
            }

            for (int i = 0; i < _usedBlobs; i++)
            {
                var b = _blobs[i];
                if (b.Alive || b.DyingTimer <= 0f) continue;

                b.DyingTimer -= dt;
                float k = Mathf.Clamp01(b.DyingTimer / 0.28f);
                b.Tr.localScale = Vector3.one * (b.Radius * 2f * (0.35f + 0.65f * k));
                var col = b.Sr.color;
                col.a = k;
                b.Sr.color = col;
                if (b.DyingTimer <= 0f) b.Tr.gameObject.SetActive(false);
            }

            RebuildDecor();
            return TopEdge > field.Bottom - 0.5f;
        }

        /// <summary>
        /// Насколько локация закрывает экран: по этой доле подмешивается цвет её воды.
        /// Получается плавный въезд в локацию и такой же выезд.
        /// </summary>
        public float ScreenCoverage(Playfield field)
        {
            float overlap = Mathf.Min(TopEdge, field.Top) - Mathf.Max(BottomEdge, field.Bottom);
            float screenHeight = field.Top - field.Bottom;
            if (screenHeight < 0.01f) return 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(overlap / (screenHeight * 0.65f)));
        }

        public void Deactivate()
        {
            for (int i = 0; i < _blobs.Count; i++)
            {
                _blobs[i].Alive = false;
                _blobs[i].DyingTimer = 0f;
                _blobs[i].Tr.gameObject.SetActive(false);
            }
            _usedBlobs = 0;
            _items.Clear();
            gameObject.SetActive(false);
        }
    }
}
