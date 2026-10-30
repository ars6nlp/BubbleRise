using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Выпускает рыб с боков и снизу. Во время боя с боссом продолжает работать,
    /// но реже — чтобы игрок не мог полностью сосредоточиться на отражении снарядов.
    /// </summary>
    public class FishSpawner : MonoBehaviour
    {
        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<Fish> _pool = new Stack<Fish>();
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
            Interval = _cfg.fishBaseInterval;
            _timer = Interval;
        }

        /// <param name="depth">Текущая глубина в метрах — рыбы включаются не сразу.</param>
        /// <param name="bossFight">Во время боя интервал умножается.</param>
        public Fish Tick(float dt, float depth, bool bossFight, Vector2 bubbleCenter, float scrollSpeed)
        {
            Interval = Mathf.Max(_cfg.fishMinInterval,
                Interval - dt * _cfg.fishIntervalDecay * Difficulty.Current.spawnDecayScale);

            if (depth < _cfg.fishFirstSpawnAt * Mathf.Max(1f, Difficulty.Current.unlockDepthScale)) return null;

            _timer -= dt;
            if (_timer > 0f) return null;

            float multiplier = bossFight ? Mathf.Max(1f, _cfg.fishBossIntervalMultiplier) : 1f;
            _timer = Interval * multiplier * Difficulty.FeatureIntervalFactor(depth)
                     * Random.Range(0.8f, 1.25f);

            // медузы не охотятся — они просто висят в толще воды на пути наверх
            if (!bossFight && Random.value < _cfg.jellyfishChance) return SpawnJellyfish(scrollSpeed);
            return Spawn(bubbleCenter);
        }

        Fish SpawnJellyfish(float scrollSpeed)
        {
            float radius = _cfg.jellyfishRadius * Random.Range(0.85f, 1.25f);
            float limit = Mathf.Max(0.1f, _field.PlayHalfWidth - radius - 0.2f);
            var position = new Vector2(Random.Range(-limit, limit), _field.Top + radius + 0.4f);

            var fish = Rent();
            fish.Activate(FishType.Jellyfish, radius, position, Vector2.down,
                          scrollSpeed * Random.Range(0.88f, 1.05f),
                          0f, Random.Range(-_cfg.jellyfishDriftMax, _cfg.jellyfishDriftMax), 999f);
            return fish;
        }

        Fish Spawn(Vector2 bubbleCenter)
        {
            // Lurker'ы редкие: крупные, медленные и живучие в поворотах
            bool lurker = Random.value < _cfg.lurkerChance;
            var type = lurker ? FishType.Lurker : FishType.Piranha;

            float radius = lurker ? _cfg.fishRadius * 1.45f : _cfg.fishRadius;
            float speed = lurker
                ? Random.Range(_cfg.fishSpeedMin, _cfg.fishSpeedMax) * 0.75f
                : Random.Range(_cfg.fishSpeedMin, _cfg.fishSpeedMax);
            float turn = lurker ? _cfg.fishTurnRate * 0.7f : _cfg.fishTurnRate;

            Vector2 position;
            switch (Random.Range(0, 3))
            {
                case 0: // слева
                    position = new Vector2(-_field.HalfWidth - radius - 0.3f,
                                           Random.Range(_field.Bottom + 1f, _field.Top - 1.5f));
                    break;
                case 1: // справа
                    position = new Vector2(_field.HalfWidth + radius + 0.3f,
                                           Random.Range(_field.Bottom + 1f, _field.Top - 1.5f));
                    break;
                default: // снизу
                    position = new Vector2(Random.Range(-_field.PlayHalfWidth, _field.PlayHalfWidth),
                                           _field.Bottom - radius - 0.3f);
                    break;
            }

            // изначальный курс — примерно на пузырёк, но с приличным разбросом
            Vector2 dir = bubbleCenter - position;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f)) * dir.normalized;

            var fish = Rent();
            fish.Activate(type, radius, position, dir, speed, turn, _cfg.fishWanderStrength, _cfg.fishLifetime);
            return fish;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Для дев-панели: заспавнить рыбу или медузу немедленно.</summary>
        public Fish DevSpawn(Vector2 bubbleCenter) => Spawn(bubbleCenter);
        public Fish DevSpawnJellyfish(float scrollSpeed) => SpawnJellyfish(scrollSpeed);
#endif

        Fish Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("Fish");
            go.transform.SetParent(_root, false);
            var fish = go.AddComponent<Fish>();
            fish.Setup();
            return fish;
        }

        public void Recycle(Fish fish)
        {
            if (fish == null || fish.Pooled) return;
            fish.Pooled = true;
            fish.Deactivate();
            _pool.Push(fish);
        }
    }
}
