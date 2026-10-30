using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Атмосферные слои океана, не влияющие на геймплей. Живут отдельно от
    /// BackgroundController, чтобы тот не превратился в свалку.
    ///
    /// Три системы, и каждая работает на своей глубине:
    /// — планктон светится тем ярче, чем глубже (противовес лучам света, которые,
    ///   наоборот, разгораются у поверхности);
    /// — пузырьковые струи бьют со дна и проносятся мимо вместе с миром;
    /// — далёкие силуэты китов и скатов изредка пересекают дальний план.
    /// </summary>
    public class OceanAmbience : MonoBehaviour
    {
        class Mote
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Parallax;
            public float Size;
            public float Phase;
            public float TwinkleSpeed;
            public float Drift;
            public Color Tint;
        }

        class Vent
        {
            public Transform Root;
            public SpriteRenderer[] Bubbles;
            public bool Active;
            public float X, Y;
            public float Height;
            public float Phase;
            public float Spread;
        }

        GameConfig _cfg;
        Playfield _field;

        Mote[] _motes;
        Vent[] _vents;
        float _ventTimer;

        SpriteRenderer _silhouette;
        bool _silActive;
        float _silTimer, _silX, _silY, _silSpeed, _silPhase, _silSize;

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field)
        {
            _cfg = cfg;
            _field = field;

            BuildPlankton();
            BuildVents();
            BuildSilhouette();
        }

        void BuildPlankton()
        {
            int count = Mathf.Max(0, _cfg.planktonCount);
            _motes = new Mote[count];
            var sprite = SpriteFactory.PlanktonMote();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Plankton");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Plankton;

                _motes[i] = new Mote { Tr = go.transform, Sr = sr };
                RespawnMote(_motes[i], true);
            }
        }

        void BuildVents()
        {
            int ventCount = 3;
            int bubbleCount = Mathf.Max(3, _cfg.ventBubbleCount);
            _vents = new Vent[ventCount];
            var dot = SpriteFactory.SoftDot();

            for (int v = 0; v < ventCount; v++)
            {
                var root = new GameObject("BubbleVent").transform;
                root.SetParent(transform, false);

                var bubbles = new SpriteRenderer[bubbleCount];
                for (int b = 0; b < bubbleCount; b++)
                {
                    var go = new GameObject("VentBubble");
                    go.transform.SetParent(root, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = dot;
                    sr.sharedMaterial = SpriteFactory.Unlit;
                    sr.sortingOrder = SortingOrders.BubbleVent;
                    bubbles[b] = sr;
                }

                root.gameObject.SetActive(false);
                _vents[v] = new Vent { Root = root, Bubbles = bubbles };
            }

            _ventTimer = Random.Range(_cfg.ventIntervalMin, _cfg.ventIntervalMax) * 0.4f;
        }

        void BuildSilhouette()
        {
            var go = new GameObject("Silhouette");
            go.transform.SetParent(transform, false);
            _silhouette = go.AddComponent<SpriteRenderer>();
            _silhouette.sharedMaterial = SpriteFactory.Unlit;
            _silhouette.sortingOrder = SortingOrders.Silhouette;
            go.SetActive(false);

            _silTimer = Random.Range(_cfg.silhouetteIntervalMin, _cfg.silhouetteIntervalMax) * 0.5f;
        }

        // --------------------------------------------------------------- цикл

        /// <param name="depth">Глубина в метрах — от неё зависит яркость планктона.</param>
        public void Tick(float dt, float depth, float scrollSpeed)
        {
            float surfaceProgress = Mathf.Clamp01(depth / Mathf.Max(1f, _cfg.surfaceDepth));

            TickPlankton(dt, scrollSpeed, surfaceProgress);
            TickVents(dt, scrollSpeed);
            TickSilhouette(dt, scrollSpeed);
        }

        // ----------------------------------------------------------- планктон

        void RespawnMote(Mote mote, bool anywhere)
        {
            mote.Parallax = Random.Range(0.35f, 1.0f);
            mote.Size = Mathf.Lerp(0.10f, 0.30f, mote.Parallax);
            mote.Phase = Random.Range(0f, 10f);
            mote.TwinkleSpeed = Random.Range(0.8f, 2.4f);
            mote.Drift = Random.Range(-0.12f, 0.12f);

            // холодная биолюминесценция: от аквамарина до сиреневого
            mote.Tint = Color.Lerp(Palette.Aqua, Palette.JellyBell, Random.value);

            float x = Random.Range(-_field.HalfWidth, _field.HalfWidth);
            float y = anywhere ? Random.Range(_field.Bottom, _field.Top) : _field.Top + 0.3f;
            mote.Tr.position = new Vector3(x, y, 0f);
        }

        void TickPlankton(float dt, float scrollSpeed, float surfaceProgress)
        {
            // чем глубже, тем ярче — на глубине это единственный источник света
            float depthBoost = Mathf.Lerp(1f, 0.25f, surfaceProgress);

            for (int i = 0; i < _motes.Length; i++)
            {
                var mote = _motes[i];
                mote.Phase += dt * mote.TwinkleSpeed;

                var p = mote.Tr.position;
                p.y -= scrollSpeed * mote.Parallax * dt;
                p.x += Mathf.Sin(mote.Phase * 0.5f) * mote.Drift * dt;
                mote.Tr.position = p;

                float twinkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(mote.Phase));
                float alpha = _cfg.planktonIntensity * 0.55f * twinkle * depthBoost * mote.Parallax;
                mote.Sr.color = mote.Tint.WithAlpha(alpha);
                mote.Tr.localScale = Vector3.one * (mote.Size * (0.75f + 0.35f * twinkle));

                if (p.y < _field.Bottom - 0.4f) RespawnMote(mote, false);
            }
        }

        // ------------------------------------------------------ струи со дна

        void TickVents(float dt, float scrollSpeed)
        {
            _ventTimer -= dt;
            if (_ventTimer <= 0f)
            {
                _ventTimer = Random.Range(_cfg.ventIntervalMin, _cfg.ventIntervalMax);
                StartVent();
            }

            for (int v = 0; v < _vents.Length; v++)
            {
                var vent = _vents[v];
                if (!vent.Active) continue;

                vent.Y -= scrollSpeed * dt;
                vent.Phase += dt * 0.55f;

                int count = vent.Bubbles.Length;
                for (int b = 0; b < count; b++)
                {
                    // каждый пузырёк идёт по своей фазе вдоль столба и всплывает
                    float t = Mathf.Repeat(vent.Phase + b / (float)count, 1f);
                    float y = vent.Y + t * vent.Height;
                    float x = vent.X + Mathf.Sin(t * 7f + b) * vent.Spread;

                    var tr = vent.Bubbles[b].transform;
                    tr.position = new Vector3(x, y, 0f);
                    tr.localScale = Vector3.one * Mathf.Lerp(0.07f, 0.19f, t);

                    // появляется у устья и тает к вершине струи
                    float fade = Mathf.Min(Mathf.InverseLerp(0f, 0.12f, t), Mathf.InverseLerp(1f, 0.72f, t));
                    vent.Bubbles[b].color = Palette.Foam.WithAlpha(0.30f * fade);
                }

                if (vent.Y < _field.Bottom - vent.Height - 1f)
                {
                    vent.Active = false;
                    vent.Root.gameObject.SetActive(false);
                }
            }
        }

        void StartVent()
        {
            Vent vent = null;
            for (int v = 0; v < _vents.Length; v++)
                if (!_vents[v].Active) { vent = _vents[v]; break; }
            if (vent == null) return;

            // струи бьют по краям, чтобы не мешать читать игровую зону
            float inner = _field.PlayHalfWidth * 0.78f;
            float outer = _field.HalfWidth - 0.4f;
            float x = Random.Range(inner, Mathf.Max(inner + 0.2f, outer));

            vent.Active = true;
            vent.X = Random.value < 0.5f ? -x : x;
            vent.Y = _field.Top + 0.5f;
            vent.Height = Random.Range(2.6f, 5.0f);
            vent.Spread = Random.Range(0.10f, 0.26f);
            vent.Phase = Random.Range(0f, 1f);
            vent.Root.gameObject.SetActive(true);
        }

        // ------------------------------------------------- далёкие силуэты

        void TickSilhouette(float dt, float scrollSpeed)
        {
            if (!_silActive)
            {
                _silTimer -= dt;
                if (_silTimer <= 0f) StartSilhouette();
                return;
            }

            _silPhase += dt;
            _silX += _silSpeed * dt;
            _silY -= scrollSpeed * 0.25f * dt; // дальний план сносится заметно медленнее

            var tr = _silhouette.transform;
            tr.position = new Vector3(_silX, _silY + Mathf.Sin(_silPhase * 0.6f) * 0.35f, 0f);
            // лёгкое качание вместо анимации плавников
            tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_silPhase * 0.75f) * 4f);
            tr.localScale = new Vector3(_silSpeed > 0f ? _silSize : -_silSize, _silSize, 1f);

            float span = _field.HalfWidth + _silSize * 1.6f;
            if (Mathf.Abs(_silX) > span || _silY < _field.Bottom - _silSize)
            {
                _silActive = false;
                _silhouette.gameObject.SetActive(false);
                _silTimer = Random.Range(_cfg.silhouetteIntervalMin, _cfg.silhouetteIntervalMax);
            }
        }

        void StartSilhouette()
        {
            bool whale = Random.value < 0.55f;
            _silhouette.sprite = whale ? SpriteFactory.WhaleSilhouette() : SpriteFactory.MantaSilhouette();
            _silSize = whale ? Random.Range(5.5f, 8.5f) : Random.Range(3.4f, 5.0f);

            bool fromLeft = Random.value < 0.5f;
            _silSpeed = (fromLeft ? 1f : -1f) * Random.Range(0.55f, 1.05f);
            _silX = (fromLeft ? -1f : 1f) * (_field.HalfWidth + _silSize * 1.2f);
            _silY = Random.Range(_field.Bottom + 1.5f, _field.Top - 1.5f);
            _silPhase = 0f;

            // почти растворён в толще воды — это далёкий силуэт, а не персонаж
            _silhouette.color = Palette.Deep2.WithAlpha(Random.Range(0.16f, 0.28f));
            _silhouette.gameObject.SetActive(true);
            _silActive = true;
        }
    }
}
