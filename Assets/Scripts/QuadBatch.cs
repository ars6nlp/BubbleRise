using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Слой из прямоугольников в одном меше: один материал, один вызов отрисовки
    /// на весь слой, сколько бы деталей в нём ни было.
    ///
    /// Зачем: в URP 2D каждый SpriteRenderer — отдельный вызов отрисовки, и
    /// динамический батчинг этого не меняет [ТЕСТ]: 110 спрайтов давали 118
    /// вызовов и с ним, и без него. Фону нужны сотни мелочей — снег, лучи,
    /// ракушки, — и по одному спрайту на каждую бюджет кадра не выдержал бы.
    ///
    /// Меш пересобирается каждый кадр из массивов, выделенных один раз под
    /// <c>capacity</c>; мусора нет. Цвет вершины умножается на текстуру —
    /// так один и тот же рисунок атласа тонет в тумане или темнеет к краю.
    /// </summary>
    public sealed class QuadBatch
    {
        readonly Mesh _mesh;
        readonly MeshRenderer _renderer;
        readonly Vector3[] _vertices;
        readonly Color32[] _colors;
        readonly Vector2[] _uvs;
        readonly int[] _triangles;
        readonly int _capacity;
        int _count;
        int _committed = -1;

        /// <summary>
        /// Проект в линейном цветовом пространстве, а цвет вершины шейдер берёт как
        /// есть. Цвета в коде записаны в sRGB (как в палитре), поэтому без перевода
        /// тёмный 0x1A ложился на экран как заметно светлый [ТЕСТ]: вода выходила
        /// на L 0.06–0.19 вместо 0.01–0.02. Переводим здесь, в одном месте.
        /// </summary>
        static readonly bool Linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>Цвет sRGB → цвет вершины (с переводом в линейное пространство, если оно включено).</summary>
        public static Color32 ToVertex(Color c) => Linear ? (Color32)c.linear : (Color32)c;

        public int Count => _count;
        public int Capacity => _capacity;
        public GameObject GameObject => _renderer.gameObject;

        public bool Visible
        {
            get => _renderer.enabled;
            set => _renderer.enabled = value;
        }

        public QuadBatch(Transform parent, string name, Material material, int sortingOrder, int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.sortingOrder = sortingOrder;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _mesh = new Mesh { name = name + "Mesh" };
            _mesh.MarkDynamic();
            // Слой может занимать весь экран и уходить за край: границы с запасом,
            // иначе камера отсекала бы его по старому положению.
            _mesh.bounds = Bounds;
            filter.sharedMesh = _mesh;

            _vertices = new Vector3[_capacity * 4];
            _colors = new Color32[_capacity * 4];
            _uvs = new Vector2[_capacity * 4];
            _triangles = new int[_capacity * 6];
            for (int i = 0; i < _capacity; i++)
            {
                int v = i * 4, t = i * 6;
                _triangles[t + 0] = v; _triangles[t + 1] = v + 1; _triangles[t + 2] = v + 2;
                _triangles[t + 3] = v; _triangles[t + 4] = v + 2; _triangles[t + 5] = v + 3;
            }
        }

        public void Clear() => _count = 0;

        /// <summary>
        /// Прямоугольник с центром <paramref name="center"/>, полуразмерами
        /// <paramref name="half"/>, поворотом и сдвигом верха вбок (<paramref name="sway"/>,
        /// в юнитах) — так водоросль качается от основания, а не вертится целиком.
        /// </summary>
        public void Add(Vector2 center, Vector2 half, float angle, Color color, Rect uv,
                        bool flipX = false, float sway = 0f, float pivotY = 0f)
        {
            if (_count >= _capacity) return;
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            Vector2 ax = new Vector2(cs, sn) * half.x;
            Vector2 ay = new Vector2(-sn, cs) * half.y;
            // pivotY: доля полувысоты, где стоит основание (−1 — низ), качается только верх
            Vector2 baseShift = ay * pivotY;
            Vector2 c = center - baseShift;
            Vector2 top = new Vector2(cs, sn) * sway;

            int v = _count * 4;
            _vertices[v + 0] = c - ax - ay;
            _vertices[v + 1] = c - ax + ay + top;
            _vertices[v + 2] = c + ax + ay + top;
            _vertices[v + 3] = c + ax - ay;

            float u0 = flipX ? uv.xMax : uv.xMin, u1 = flipX ? uv.xMin : uv.xMax;
            _uvs[v + 0] = new Vector2(u0, uv.yMin);
            _uvs[v + 1] = new Vector2(u0, uv.yMax);
            _uvs[v + 2] = new Vector2(u1, uv.yMax);
            _uvs[v + 3] = new Vector2(u1, uv.yMin);

            Color32 c32 = ToVertex(color);
            _colors[v + 0] = c32; _colors[v + 1] = c32; _colors[v + 2] = c32; _colors[v + 3] = c32;
            _count++;
        }

        /// <summary>Четырёхугольник по углам с цветом в каждом углу — для градиентов и лучей.</summary>
        public void AddCorners(Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
                               Color cbl, Color ctl, Color ctr, Color cbr, Rect uv)
        {
            if (_count >= _capacity) return;
            int v = _count * 4;
            _vertices[v + 0] = bl; _vertices[v + 1] = tl; _vertices[v + 2] = tr; _vertices[v + 3] = br;
            _uvs[v + 0] = new Vector2(uv.xMin, uv.yMin);
            _uvs[v + 1] = new Vector2(uv.xMin, uv.yMax);
            _uvs[v + 2] = new Vector2(uv.xMax, uv.yMax);
            _uvs[v + 3] = new Vector2(uv.xMax, uv.yMin);
            _colors[v + 0] = ToVertex(cbl); _colors[v + 1] = ToVertex(ctl);
            _colors[v + 2] = ToVertex(ctr); _colors[v + 3] = ToVertex(cbr);
            _count++;
        }

        /// <summary>Залить накопленное в меш. Неиспользованная часть массивов не уходит.</summary>
        public void Commit()
        {
            int verts = _count * 4;
            if (_committed != _count)
            {
                // Число прямоугольников сменилось — меш очищается целиком: иначе
                // старые индексы ссылались бы на вершины, которых уже нет.
                _mesh.Clear(true);
                _committed = _count;
            }
            _mesh.SetVertices(_vertices, 0, verts);
            _mesh.SetColors(_colors, 0, verts);
            _mesh.SetUVs(0, _uvs, 0, verts);
            _mesh.SetIndices(_triangles, 0, _count * 6, MeshTopology.Triangles, 0, false);
            _mesh.bounds = Bounds;
        }

        static readonly Bounds Bounds = new Bounds(Vector3.zero, new Vector3(4000f, 4000f, 10f));
    }
}
