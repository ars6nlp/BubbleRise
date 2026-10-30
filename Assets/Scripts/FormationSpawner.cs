using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Каталог коралловых фигур и их спавн. Фигуры задаются текстовой сеткой —
    /// добавить новую можно одной строкой, ничего не программируя.
    ///
    /// Перед спавном подбирается горизонтальное смещение так, чтобы в колонке
    /// над пузырьком стояло не больше formationMaxCenterBlocks блоков: иначе
    /// пробить проход стражем физически не хватило бы времени.
    /// </summary>
    public class FormationSpawner : MonoBehaviour
    {
        class ShapeDef
        {
            public string[] Rows;
            public bool CanSpin;
        }

        static readonly ShapeDef[] Shapes =
        {
            // большой квадрат
            new ShapeDef { CanSpin = true, Rows = new[]
            {
                "XXXXX",
                "X...X",
                "X...X",
                "X...X",
                "XXXXX",
            }},
            // ромб
            new ShapeDef { CanSpin = true, Rows = new[]
            {
                "..X..",
                ".X.X.",
                "X...X",
                ".X.X.",
                "..X..",
            }},
            // малое кольцо
            new ShapeDef { CanSpin = true, Rows = new[]
            {
                "XXX",
                "X.X",
                "XXX",
            }},
            // крест
            new ShapeDef { CanSpin = true, Rows = new[]
            {
                ".X.",
                "XXX",
                ".X.",
            }},
            // маленький блок
            new ShapeDef { CanSpin = true, Rows = new[]
            {
                "XX",
                "XX",
            }},
            // стена с проёмом
            new ShapeDef { CanSpin = false, Rows = new[]
            {
                "XXX.XX",
                "XXX.XX",
            }},
            // лестница
            new ShapeDef { CanSpin = false, Rows = new[]
            {
                "XX...",
                ".XX..",
                "..XX.",
                "...XX",
            }},
            // арка
            new ShapeDef { CanSpin = false, Rows = new[]
            {
                ".XXX.",
                "X...X",
                "X...X",
            }},
            // две колонны
            new ShapeDef { CanSpin = false, Rows = new[]
            {
                "X..X",
                "X..X",
                "X..X",
            }},
        };

        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<CoralFormation> _pool = new Stack<CoralFormation>();
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
            Interval = _cfg.formationInterval;
            _timer = Interval;
        }

        public CoralFormation Tick(float dt, float depth)
        {
            Interval = Mathf.Max(_cfg.formationMinInterval,
                Interval - dt * _cfg.formationIntervalDecay * Difficulty.Current.spawnDecayScale);

            // Постройки — самое плотное, что есть в игре: на мягком старте их
            // просто не бывает, иначе первая же стена кораллов учит новичка тому,
            // что сквозь игру не пробиться.
            if (depth < _cfg.formationFirstSpawnAt * Mathf.Max(1f, Difficulty.Current.unlockDepthScale)) return null;

            _timer -= dt;
            if (_timer > 0f) return null;

            _timer = Interval * Difficulty.FeatureIntervalFactor(depth) * Random.Range(0.8f, 1.25f);
            return Spawn();
        }

        CoralFormation Spawn()
        {
            var shape = Shapes[Random.Range(0, Shapes.Length)];
            float cell = _cfg.formationCellSize * Random.Range(0.9f, 1.25f);

            int cols = 0;
            for (int r = 0; r < shape.Rows.Length; r++) cols = Mathf.Max(cols, shape.Rows[r].Length);

            float width = cols * cell;
            float limit = Mathf.Max(0f, _field.PlayHalfWidth - width * 0.5f);
            float x = PickOffset(shape, cols, cell, limit);

            float height = shape.Rows.Length * cell;
            float y = _field.Top + height * 0.5f + 0.4f;

            float spin = shape.CanSpin ? Random.Range(-_cfg.formationSpinMax, _cfg.formationSpinMax) : 0f;

            var formation = Rent();
            formation.Activate(shape.Rows, cell, _cfg.formationBlockRadius, new Vector2(x, y), spin);
            return formation;
        }

        /// <summary>Ищем смещение, при котором пузырьку есть что пробивать, но не слишком много.</summary>
        float PickOffset(ShapeDef shape, int cols, float cell, float limit)
        {
            float best = 0f;
            int bestCount = int.MaxValue;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float candidate = limit > 0.01f ? Random.Range(-limit, limit) : 0f;
                int blocked = CountCentreColumn(shape, cols, cell, candidate);

                if (blocked <= Difficulty.MaxCenterBlocks(_cfg.formationMaxCenterBlocks)) return candidate;
                if (blocked < bestCount)
                {
                    bestCount = blocked;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>Сколько блоков фигуры окажется прямо над пузырьком (он всегда на x = 0).</summary>
        int CountCentreColumn(ShapeDef shape, int cols, float cell, float offset)
        {
            int count = 0;
            float reach = _cfg.formationBlockRadius + 0.30f; // радиус пузырька с запасом

            for (int r = 0; r < shape.Rows.Length; r++)
            {
                string row = shape.Rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != 'X') continue;
                    float x = offset + (c - (cols - 1) * 0.5f) * cell;
                    if (Mathf.Abs(x) < reach) count++;
                }
            }
            return count;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Для дев-панели: заспавнить постройку немедленно.</summary>
        public CoralFormation DevSpawn() => Spawn();
#endif

        CoralFormation Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("CoralFormation");
            go.transform.SetParent(_root, false);
            return go.AddComponent<CoralFormation>();
        }

        public void Recycle(CoralFormation formation)
        {
            if (formation == null || formation.Pooled) return;
            formation.Pooled = true;
            formation.Deactivate();
            _pool.Push(formation);
        }
    }
}
