using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Мини-босс в игре: рисунок босса на пузырьке или страже, который живёт —
    /// щупальца колышутся, хвост качается, крылья взмахивают.
    ///
    /// Это дочерний объект с сеткой (<see cref="MiniBossMotion"/>), а не замена тела:
    /// пузырёк и страж остаются собой — тот же круг столкновений, тот же размер, то
    /// же управление. Их собственный спрайт перестаёт рисоваться
    /// (<c>forceRenderingOff</c>), но живёт как прежде: его видимость и прозрачность
    /// (неуязвимость, модальные экраны) сетка повторяет каждый кадр.
    ///
    /// Витрина рисует тот же рисунок той же сеткой (<see cref="MiniBossGraphic"/>):
    /// сетка, текстура и изгиб берутся из одних функций.
    /// </summary>
    public sealed class MiniBossView : MonoBehaviour
    {
        SpriteRenderer _host;
        MeshRenderer _renderer;
        Mesh _mesh;
        MaterialPropertyBlock _block;
        Vector3[] _verts;
        Color32[] _colors;
        Vector2[] _pos;
        bool _on;
        MiniBossKey _key;
        float _alpha = -1f;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>Сетка мини-босса на теле (пузырёк или страж); создаётся один раз.</summary>
        public static MiniBossView On(Component body)
        {
            var t = body.transform.Find("MiniBoss");
            var view = t != null ? t.GetComponent<MiniBossView>() : null;
            if (view != null) return view;
            var go = new GameObject("MiniBoss");
            go.transform.SetParent(body.transform, false);
            view = go.AddComponent<MiniBossView>();
            view.Init(body.GetComponent<SpriteRenderer>());
            return view;
        }

        void Init(SpriteRenderer host)
        {
            _host = host;
            var filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = SpriteFactory.Unlit;
            _renderer.sortingOrder = host != null ? host.sortingOrder : 0;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;

            int n = MiniBossMotion.VertexCount;
            _verts = new Vector3[n];
            _colors = new Color32[n];
            _pos = new Vector2[n];
            _mesh = new Mesh { name = "MiniBossMesh" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = MiniBossMotion.Uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = MiniBossMotion.Triangles;
            // рисунок не выходит за круг игрока, а изгиб — за 1.2 его радиуса
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.3f);
            filter.sharedMesh = _mesh;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Надеть вид половины: у трофея — мини-босс, у остальных — обычный спрайт тела.</summary>
        public void Show(SkinLook look)
        {
            _on = look.IsBoss;
            if (_host != null) _host.forceRenderingOff = _on;
            _renderer.enabled = false;
            if (!_on) return;

            _key = look.MiniBoss;
            _block.SetTexture(MainTex, SpriteFactory.MiniBoss(_key).texture);
            _renderer.SetPropertyBlock(_block);
            _alpha = -1f;
            LateUpdate();
        }

        public bool Active => _on;
        public MiniBossKey Key => _key;
        /// <summary>Вершины последнего кадра — для проверки «игра = витрина».</summary>
        public Vector2[] LastPositions => _pos;

        void LateUpdate()
        {
            if (!_on) return;
            bool visible = _host == null || _host.enabled;
            _renderer.enabled = visible;
            if (!visible) return;

            MiniBossMotion.Fill(_key, MiniBossMotion.Now, MiniBossMotion.Amplitude, _pos);
            for (int i = 0; i < _pos.Length; i++) _verts[i] = new Vector3(_pos[i].x, _pos[i].y, 0f);
            _mesh.vertices = _verts;

            // прозрачность — от тела: неуязвимость и плавные появления те же, что у любого скина
            float a = _host != null ? _host.color.a : 1f;
            if (!Mathf.Approximately(a, _alpha))
            {
                _alpha = a;
                var c = QuadBatch.ToVertex(new Color(1f, 1f, 1f, a));
                for (int i = 0; i < _colors.Length; i++) _colors[i] = c;
                _mesh.colors32 = _colors;
            }
        }

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
    }
}
