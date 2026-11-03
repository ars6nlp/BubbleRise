using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Крабы на боковых камнях — редкие гости у края экрана. Остальной декор мира
    /// живёт в слоях фона (<see cref="WorldLayers"/>): один меш на слой вместо
    /// отдельного спрайта на каждую колонну и водоросль.
    /// </summary>
    public class ScenerySpawner : MonoBehaviour
    {
        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<SideCrab> _crabPool = new Stack<SideCrab>();
        float _crabTimer;
        int _lastCrabSide = 1;

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
            ResetState();
        }

        public void ResetState()
        {
            // Первого краба не выпускаем сразу на старте: он редкое событие, и увидеть
            // его в первые же секунды значило бы обесценить редкость.
            _crabTimer = _cfg.sideCrabInterval * Random.Range(0.5f, 1.0f);
        }

        /// <summary>
        /// Краб по краю экрана — редкий гость, примерно раз в минуту, со случайной
        /// стороны. Идёт отдельным потоком от прочих декораций: у него своя анимация
        /// и своя скорость, а разнобой скоростей в общем потоке декора запрещён —
        /// он читается как «предметы падают».
        /// </summary>
        public SideCrab TickCrab(float dt)
        {
            if (_cfg.sideCrabInterval <= 0f) return null;

            _crabTimer -= dt;
            if (_crabTimer > 0f) return null;

            _crabTimer = _cfg.sideCrabInterval * Random.Range(0.7f, 1.3f);
            return SpawnCrab();
        }

        SideCrab SpawnCrab()
        {
            // стороны чередуем, иначе череда крабов может уйти в один край
            _lastCrabSide = -_lastCrabSide;
            int side = _lastCrabSide;

            float size = Random.Range(_cfg.sideCrabSizeMin, _cfg.sideCrabSizeMax);

            // Прижимаем к самому краю: крабу место на камнях, а не в игровой колонке.
            // На широком экране это боковая полоса, на телефоне — кромка экрана.
            float x = side * (_field.HalfWidth - size * Random.Range(0.18f, 0.42f));

            float crawl = Random.Range(_cfg.sideCrabCrawlMin, _cfg.sideCrabCrawlMax);
            if (Random.value >= _cfg.sideCrabUpwardChance) crawl = -crawl;

            float tilt = Random.Range(_cfg.sideCrabTiltMin, _cfg.sideCrabTiltMax);
            float parallax = Random.Range(_cfg.sceneryNearParallaxMin, _cfg.sceneryNearParallaxMax);

            var crab = RentCrab();
            crab.Activate(side, new Vector2(x, _field.Top + size * 0.8f), size, crawl, parallax, tilt);
            return crab;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Выпустить краба немедленно — ждать минуту при отладке незачем.</summary>
        public SideCrab DevSpawnCrab() => SpawnCrab();
#endif

        SideCrab RentCrab()
        {
            if (_crabPool.Count > 0)
            {
                var pooled = _crabPool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("SideCrab");
            go.transform.SetParent(_root, false);
            var crab = go.AddComponent<SideCrab>();
            crab.Setup();
            return crab;
        }

        public void RecycleCrab(SideCrab crab)
        {
            if (crab == null || crab.Pooled) return;
            crab.Pooled = true;
            crab.Deactivate();
            _crabPool.Push(crab);
        }
    }
}
