using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спавнер длинных кораллов. Угол наклона держим близким к горизонтали:
    /// вертикальная ветвь поставила бы над пузырьком целую колонну звеньев,
    /// и пробить её за отведённое время было бы нельзя.
    ///
    /// Дополнительно подбирается сдвиг по X, чтобы в коридоре пузырька оказалось
    /// не больше spireMaxCenterSegments звеньев.
    /// </summary>
    public class SpireSpawner : MonoBehaviour
    {
        struct Piece
        {
            public Vector2 Local;
            public float Angle;
            public float Size;
            public float Radius;
        }

        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<CoralSpire> _pool = new Stack<CoralSpire>();
        readonly List<Piece> _pieces = new List<Piece>();
        float _timer;

        public float Interval { get; set; }

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
            ResetState();
        }

        public void ResetState()
        {
            Interval = _cfg.spireInterval;
            _timer = Interval;
        }

        public CoralSpire Tick(float dt, float depth)
        {
            Interval = Mathf.Max(_cfg.spireMinInterval,
                Interval - dt * _cfg.spireIntervalDecay * Difficulty.Current.spawnDecayScale);

            if (depth < _cfg.spireFirstSpawnAt * Mathf.Max(1f, Difficulty.Current.unlockDepthScale)) return null;

            _timer -= dt;
            if (_timer > 0f) return null;

            _timer = Interval * Difficulty.FeatureIntervalFactor(depth) * Random.Range(0.75f, 1.3f);
            return Spawn();
        }

        CoralSpire Spawn()
        {
            float length = Random.Range(_cfg.spireMinLength, _cfg.spireMaxLength);
            float size = _cfg.spireSegmentSize;
            float spacing = size * _cfg.spireSegmentSpacing;
            float angle = Random.Range(-_cfg.spireMaxAngle, _cfg.spireMaxAngle);

            BuildPieces(length, size, spacing);

            float radians = angle * Mathf.Deg2Rad;
            float x = PickOffset(radians);
            float reach = length * 0.5f + size;
            float y = _field.Top + reach + 0.4f;

            float spin = Random.Range(-_cfg.spireSpinMax, _cfg.spireSpinMax);

            var spire = Rent();
            spire.Activate(new Vector2(x, y), angle, spin, reach);
            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                spire.AddSegment(piece.Local, piece.Angle, piece.Size, piece.Radius);
            }
            spire.FinishSetup();
            return spire;
        }

        /// <summary>Ствол ветви плюс один-два боковых отростка.</summary>
        void BuildPieces(float length, float size, float spacing)
        {
            _pieces.Clear();

            int count = Mathf.Max(3, Mathf.RoundToInt(length / spacing));
            float half = (count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                // к концам ветвь тоньше
                float taper = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI);
                float segmentSize = size * taper;

                _pieces.Add(new Piece
                {
                    Local = new Vector2(i * spacing - half, Mathf.Sin(t * Mathf.PI * 1.4f) * 0.10f),
                    Angle = Random.Range(-7f, 7f),
                    Size = segmentSize,
                    Radius = segmentSize * _cfg.spireThickness,
                });
            }

            int branches = Random.Range(1, 3);
            for (int b = 0; b < branches; b++)
            {
                int anchor = Random.Range(1, Mathf.Max(2, count - 1));
                Vector2 basePoint = _pieces[anchor].Local;
                float branchAngle = (Random.value < 0.5f ? 1f : -1f) * Random.Range(45f, 80f);
                Vector2 dir = new Vector2(Mathf.Cos(branchAngle * Mathf.Deg2Rad),
                                          Mathf.Sin(branchAngle * Mathf.Deg2Rad));

                int branchCount = Random.Range(2, 4);
                for (int i = 1; i <= branchCount; i++)
                {
                    float segmentSize = size * (0.62f - i * 0.06f);
                    _pieces.Add(new Piece
                    {
                        Local = basePoint + dir * (spacing * 0.85f * i),
                        Angle = branchAngle,
                        Size = segmentSize,
                        Radius = segmentSize * _cfg.spireThickness,
                    });
                }
            }
        }

        /// <summary>Ищем сдвиг, при котором прямо над пузырьком мало звеньев.</summary>
        float PickOffset(float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float limit = Mathf.Max(0.1f, _field.PlayHalfWidth * 0.8f);

            float best = 0f;
            int bestCount = int.MaxValue;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float candidate = Random.Range(-limit, limit);

                int blocked = 0;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    var piece = _pieces[i];
                    float worldX = candidate + piece.Local.x * cos - piece.Local.y * sin;
                    if (Mathf.Abs(worldX) < piece.Radius + 0.30f) blocked++;
                }

                if (blocked <= Difficulty.MaxCenterBlocks(_cfg.spireMaxCenterSegments)) return candidate;
                if (blocked < bestCount) { bestCount = blocked; best = candidate; }
            }
            return best;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Для дев-панели: заспавнить длинный коралл немедленно.</summary>
        public CoralSpire DevSpawn() => Spawn();
#endif

        CoralSpire Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("CoralSpire");
            go.transform.SetParent(_root, false);
            return go.AddComponent<CoralSpire>();
        }

        public void Recycle(CoralSpire spire)
        {
            if (spire == null || spire.Pooled) return;
            spire.Pooled = true;
            spire.Deactivate();
            _pool.Push(spire);
        }
    }
}
