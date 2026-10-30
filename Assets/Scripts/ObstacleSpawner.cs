using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спавнер препятствий с пулом объектов (в WebGL лишний GC — заметные фризы).
    /// Интервал спавна уменьшается по мере роста сложности.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<Obstacle> _pool = new Stack<Obstacle>();
        float _timer;

        public float SpawnInterval { get; set; }

        /// <summary>
        /// Цвет воды текущей локации. Препятствия подкрашиваются им, чтобы впадина
        /// и ламинариевый лес отличались не только фоном: прежде одни и те же камни
        /// выглядели одинаково в любом биоме и выдавали, что фон — просто картинка.
        /// </summary>
        public Color BiomeTint { get; set; } = Color.white;

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
            ResetState();
        }

        public void ResetState()
        {
            SpawnInterval = _cfg.baseSpawnInterval;
            _timer = SpawnInterval;
        }

        /// <summary>
        /// Тикает таймер; когда пора — возвращает новое препятствие, иначе null.
        ///
        /// Глубина нужна для выбора вида: прочные и злые угрозы открываются не сразу
        /// (см. <see cref="HazardTable.Roll"/>). На первых метрах игрок ещё учится
        /// подставлять стража, и трёхударная створка в этот момент научила бы его
        /// только тому, что страж не работает.
        /// </summary>
        public Obstacle Tick(float dt, float scrollSpeed, int depth)
        {
            // Интервал сокращается медленнее на лёгкой сложности: кривую просили
            // пологую, а самое заметное в ней — именно скорость, с которой экран
            // наполняется.
            SpawnInterval = Mathf.Max(_cfg.minSpawnInterval,
                SpawnInterval - dt * _cfg.spawnIntervalDecay * Difficulty.Current.spawnDecayScale);

            _timer -= dt;
            if (_timer > 0f) return null;

            // Мягкий старт и подстройка под игрока живут здесь, а не в самом
            // интервале: интервал — это разгон сложности, а множитель — поправка
            // на то, где игрок сейчас и как у него идут дела.
            float factor = Difficulty.SpawnIntervalFactor(depth);
            _timer = SpawnInterval * factor * Random.Range(0.8f, 1.2f);
            return Spawn(scrollSpeed, depth);
        }

        Obstacle Spawn(float scrollSpeed, int depth)
        {
            var type = HazardTable.Roll(depth);
            float radius = Random.Range(_cfg.obstacleMinRadius, _cfg.obstacleMaxRadius);
            float limit = Mathf.Max(0.05f, _field.PlayHalfWidth - radius - 0.15f);
            float x = Random.Range(-limit, limit);

            // Проход у края: препятствие вплотную к стенке не оставляет пузырьку
            // места, и обойти его можно только идеальным движением. Отодвигаем от
            // края на половину требуемого прохода.
            float edgeKeep = Difficulty.MinGap(PlayerSize.Bubble(_cfg)) * 0.5f;
            if (limit > edgeKeep)
                x = Mathf.Clamp(x, -limit + edgeKeep * 0.5f, limit - edgeKeep * 0.5f);
            float y = _field.Top + radius + 0.3f;

            // Вертикальная скорость почти совпадает со скоростью прокрутки: разнобой
            // скоростей — как раз то, что читалось как «предметы падают».
            float vy = -scrollSpeed * Random.Range(_cfg.obstacleSpeedMin, _cfg.obstacleSpeedMax);
            float spin = Random.Range(-_cfg.obstacleSpinMax, _cfg.obstacleSpinMax);
            float swayAmplitude = Random.Range(_cfg.obstacleSwayMin, _cfg.obstacleSwayMax);
            float swaySpeed = Random.Range(0.5f, 1.2f);

            var o = Rent();
            o.Activate(type, radius, new Vector2(x, y), new Vector2(0f, vy), spin, swayAmplitude, swaySpeed,
                       BiomeTint);
            return o;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Для дев-панели: заспавнить препятствие немедленно.</summary>
        public Obstacle DevSpawn(float scrollSpeed, int depth) => Spawn(scrollSpeed, depth);
#endif

        Obstacle Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("Obstacle");
            go.transform.SetParent(_root, false);
            var obstacle = go.AddComponent<Obstacle>();
            obstacle.Setup();
            return obstacle;
        }

        public void Recycle(Obstacle o)
        {
            if (o == null || o.Pooled) return;
            o.Pooled = true;
            o.Deactivate();
            _pool.Push(o);
        }
    }
}
