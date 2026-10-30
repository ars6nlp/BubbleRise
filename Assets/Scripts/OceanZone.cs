using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Локация — крупный участок дна со своим цветом воды, стенами-каньоном
    /// и композициями из коралловых шаров. Занимает две с лишним высоты экрана
    /// и проплывает мимо целиком, как уровень.
    ///
    /// Стены рисуются двумя процедурными мешами с цветами вершин: один draw call
    /// на стену, профиль получается гладким, а не ступенчатым из спрайтов.
    /// Стены декоративные — пузырёк зафиксирован по центру и до них не достаёт;
    /// их задача сузить проход и задать силуэт места. Опасны шары внутри.
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

        public bool Pooled { get; set; }
        public string DisplayName { get; private set; }
        public Color BannerColor { get; private set; }
        public Color WaterTop { get; private set; }
        public Color WaterBottom { get; private set; }
        public IReadOnlyList<Blob> Blobs => _blobs;

        // Рядов достаточно, чтобы профиль оставался гладким и на длинных локациях.
        const int WallRows = 80;

        readonly List<Blob> _blobs = new List<Blob>();
        int _usedBlobs;

        readonly List<SpriteRenderer> _decor = new List<SpriteRenderer>();
        int _usedDecor;

        MeshFilter _leftWall, _rightWall;
        Mesh _leftMesh, _rightMesh;

        float _height;

        public float TopEdge => transform.position.y + _height * 0.5f;
        public float BottomEdge => transform.position.y - _height * 0.5f;

        // ---------------------------------------------------------- построение

        public void Activate(ZoneTheme theme, System.Func<float, float> profile, float height,
                             Playfield field, int seed)
        {
            Pooled = false;
            _height = height;
            DisplayName = Loc.Get(theme.NameKey);
            BannerColor = theme.Crust;
            WaterTop = theme.WaterTop;
            WaterBottom = theme.WaterBottom;

            transform.position = new Vector3(0f, field.Top + height * 0.5f + 0.5f, 0f);

            EnsureWalls();
            BuildWall(_leftMesh, profile, height, field, theme, seed, mirrored: false);
            BuildWall(_rightMesh, profile, height, field, theme, seed + 977, mirrored: true);

            _usedBlobs = 0;
            _usedDecor = 0;

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
            for (int i = _usedDecor; i < _decor.Count; i++)
                _decor[i].gameObject.SetActive(false);
        }

        void EnsureWalls()
        {
            if (_leftWall != null) return;

            _leftWall = CreateWall("WallLeft", out _leftMesh);
            _rightWall = CreateWall("WallRight", out _rightMesh);
        }

        MeshFilter CreateWall(string wallName, out Mesh mesh)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SpriteFactory.VertexColorUnlit;
            renderer.sortingOrder = SortingOrders.ZoneWall;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            mesh = new Mesh { name = wallName + "Mesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return filter;
        }

        /// <summary>
        /// Полоса из WallRows рядов. Внутренний край шевелится шумом Перлина —
        /// стена получается неровной, как настоящий риф, а не как линейка.
        /// </summary>
        void BuildWall(Mesh mesh, System.Func<float, float> profile, float height,
                       Playfield field, ZoneTheme theme, int seed, bool mirrored)
        {
            float sign = mirrored ? 1f : -1f;
            float outer = (field.HalfWidth + 3f) * sign;
            float crustWidth = 0.55f;

            var vertices = new Vector3[WallRows * 3];
            var colors = new Color[WallRows * 3];
            var uvs = new Vector2[WallRows * 3];
            var triangles = new int[(WallRows - 1) * 12];

            for (int i = 0; i < WallRows; i++)
            {
                float t = i / (float)(WallRows - 1);
                float y = -height * 0.5f + t * height;

                float noise = (Mathf.PerlinNoise(seed * 0.37f, t * 9f) - 0.5f) * 0.6f;
                float inner = (profile(t) * field.PlayHalfWidth + noise) * sign;

                // корка лежит с внутренней стороны края, то есть в сторону тела стены
                int v = i * 3;
                vertices[v + 0] = new Vector3(outer, y, 0f);
                vertices[v + 1] = new Vector3(inner + crustWidth * sign, y, 0f);
                vertices[v + 2] = new Vector3(inner, y, 0f);

                colors[v + 0] = theme.WallDeep;
                colors[v + 1] = theme.Wall;
                colors[v + 2] = theme.Crust;

                uvs[v + 0] = new Vector2(0f, t);
                uvs[v + 1] = new Vector2(0.7f, t);
                uvs[v + 2] = new Vector2(1f, t);
            }

            int tri = 0;
            for (int i = 0; i < WallRows - 1; i++)
            {
                int a = i * 3;
                int b = (i + 1) * 3;
                // тело стены
                triangles[tri++] = a + 0; triangles[tri++] = b + 0; triangles[tri++] = a + 1;
                triangles[tri++] = a + 1; triangles[tri++] = b + 0; triangles[tri++] = b + 1;
                // коралловая корка вдоль внутреннего края
                triangles[tri++] = a + 1; triangles[tri++] = b + 1; triangles[tri++] = a + 2;
                triangles[tri++] = a + 2; triangles[tri++] = b + 1; triangles[tri++] = b + 2;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
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

        public void AddDecor(Sprite sprite, Vector2 localPosition, float size, bool flip, Color tint)
        {
            var sr = RentDecor();
            sr.sprite = sprite;
            sr.flipX = flip;
            sr.color = tint;
            sr.transform.localPosition = localPosition;
            sr.transform.localScale = Vector3.one * size;
            sr.gameObject.SetActive(true);
        }

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

        SpriteRenderer RentDecor()
        {
            if (_usedDecor < _decor.Count) return _decor[_usedDecor++];

            var go = new GameObject("Decor");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.ZoneDecor;

            _decor.Add(sr);
            _usedDecor++;
            return sr;
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
            var p = transform.position;
            p.y -= scrollSpeed * dt;
            transform.position = p;

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
            for (int i = 0; i < _decor.Count; i++) _decor[i].gameObject.SetActive(false);
            _usedBlobs = 0;
            _usedDecor = 0;
            gameObject.SetActive(false);
        }
    }
}
