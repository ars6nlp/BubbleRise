using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Расставляет декорации над верхней границей экрана. Колонны, уступы и водоросли
    /// жмутся к краям, чтобы не мешать читать игровую зону; постройки и арки уходят
    /// в дальний план с меньшим параллаксом и заметно темнее.
    /// </summary>
    public class ScenerySpawner : MonoBehaviour
    {
        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<Scenery> _pool = new Stack<Scenery>();
        readonly Stack<SideCrab> _crabPool = new Stack<SideCrab>();
        float _timer;
        float _sideTimer;
        float _crabTimer;
        int _lastSide = 1;
        int _lastCrabSide = 1;

        /// <summary>
        /// На широких экранах игровая зона занимает только центральную колонку,
        /// а по бокам остаётся пустота. Заполняем её отдельным, более плотным потоком.
        /// На телефоне эта полоса нулевой ширины, и ничего лишнего не спавнится.
        /// </summary>
        bool HasSideBand => _field.HalfWidth > _field.PlayHalfWidth + 1.2f;

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
            ResetState();
        }

        public void ResetState()
        {
            _timer = _cfg.sceneryInterval * 0.4f;
            _sideTimer = _cfg.sceneryEdgeInterval * 0.3f;
            // Первого краба не выпускаем сразу на старте: он редкое событие, и увидеть
            // его в первые же секунды значило бы обесценить редкость.
            _crabTimer = _cfg.sideCrabInterval * Random.Range(0.5f, 1.0f);
        }

        public Scenery Tick(float dt)
        {
            // боковые заросли идут своим, более частым потоком
            if (HasSideBand)
            {
                _sideTimer -= dt;
                if (_sideTimer <= 0f)
                {
                    _sideTimer = _cfg.sceneryEdgeInterval * Random.Range(0.6f, 1.4f);
                    return SpawnSide(_field.Top, false);
                }
            }

            _timer -= dt;
            if (_timer > 0f) return null;

            _timer = _cfg.sceneryInterval * Random.Range(0.65f, 1.45f);
            return Spawn(_field.Top, false);
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

        /// <summary>Заполнить экран декорациями при старте, чтобы он не был пустым.</summary>
        public void Prefill(List<Scenery> into)
        {
            for (int i = 0; i < _cfg.sceneryPrefillCount; i++)
            {
                float y = Random.Range(_field.Bottom, _field.Top);
                into.Add(Spawn(y, true));
            }

            if (!HasSideBand) return;

            // боков на широком экране много — заполняем их гуще
            int sideCount = _cfg.sceneryPrefillCount * 3;
            for (int i = 0; i < sideCount; i++)
            {
                float y = Random.Range(_field.Bottom, _field.Top);
                into.Add(SpawnSide(y, true));
            }
        }

        /// <summary>Заросли в полосе между игровой зоной и краем экрана.</summary>
        Scenery SpawnSide(float topY, bool prefill)
        {
            _lastSide = -_lastSide;

            float roll = Random.value;
            SceneryKind kind = roll < 0.42f ? SceneryKind.Kelp
                             : roll < 0.74f ? SceneryKind.CoralPillar
                             : roll < 0.90f ? SceneryKind.RockLedge
                             : SceneryKind.RuinBlocks;

            float size = kind == SceneryKind.Kelp ? Random.Range(2.6f, 5.0f)
                       : kind == SceneryKind.CoralPillar ? Random.Range(2.2f, 4.4f)
                       : Random.Range(2.0f, 3.4f);

            // чем дальше от игровой зоны, тем медленнее и темнее — глубина кадра
            float inner = _field.PlayHalfWidth + 0.2f;
            float outer = _field.HalfWidth + 0.8f;
            float across = Random.value;
            float x = (_lastSide > 0 ? 1f : -1f) * Mathf.Lerp(inner, outer, across);

            float parallax = Mathf.Lerp(_cfg.sceneryNearParallaxMax, _cfg.sceneryFarParallaxMin, across);
            float sway = kind == SceneryKind.Kelp ? 0.12f : kind == SceneryKind.CoralPillar ? 0.03f : 0f;
            float y = prefill ? topY : topY + size * 0.6f;

            var scenery = Rent();
            scenery.Activate(kind, new Vector2(x, y), size, _lastSide > 0, parallax, sway);
            return scenery;
        }

        Scenery Spawn(float topY, bool prefill)
        {
            var kind = PickKind();
            bool farLayer = kind == SceneryKind.RuinBlocks || kind == SceneryKind.RuinArch;

            float parallax = farLayer
                ? Random.Range(_cfg.sceneryFarParallaxMin, _cfg.sceneryFarParallaxMax)
                : Random.Range(_cfg.sceneryNearParallaxMin, _cfg.sceneryNearParallaxMax);

            float size;
            float x;
            bool flip = _lastSide > 0; // стороны чередуются, см. PickKind
            float sway = 0f;

            switch (kind)
            {
                case SceneryKind.RockLedge:
                    size = Random.Range(2.4f, 3.6f);
                    // висит на краю экрана и торчит внутрь лишь частью
                    x = (flip ? 1f : -1f) * (_field.HalfWidth + size * 0.18f);
                    break;

                case SceneryKind.Kelp:
                    size = Random.Range(3.0f, 4.6f);
                    x = (flip ? 1f : -1f) * _field.PlayHalfWidth * Random.Range(0.72f, 1.15f);
                    sway = 0.10f;
                    break;

                case SceneryKind.CoralPillar:
                    size = Random.Range(2.6f, 4.2f);
                    x = (flip ? 1f : -1f) * _field.PlayHalfWidth * Random.Range(0.78f, 1.18f);
                    sway = 0.03f;
                    break;

                default: // постройки и арки — дальний план, можно ближе к центру
                    size = kind == SceneryKind.RuinArch ? Random.Range(3.4f, 4.8f) : Random.Range(2.2f, 3.4f);
                    x = Random.Range(-_field.PlayHalfWidth * 0.75f, _field.PlayHalfWidth * 0.75f);
                    break;
            }

            float y = prefill ? topY : topY + size * 0.6f;

            var scenery = Rent();
            scenery.Activate(kind, new Vector2(x, y), size, flip, parallax, sway);
            return scenery;
        }

        SceneryKind PickKind()
        {
            // чередуем стороны, чтобы декорации не сбивались в кучу с одного края
            _lastSide = -_lastSide;

            float roll = Random.value;
            if (roll < 0.30f) return SceneryKind.CoralPillar;
            if (roll < 0.52f) return SceneryKind.Kelp;
            if (roll < 0.72f) return SceneryKind.RockLedge;
            if (roll < 0.90f) return SceneryKind.RuinBlocks;
            return SceneryKind.RuinArch;
        }

        Scenery Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("Scenery");
            go.transform.SetParent(_root, false);
            var scenery = go.AddComponent<Scenery>();
            scenery.Setup();
            return scenery;
        }

        public void Recycle(Scenery scenery)
        {
            if (scenery == null || scenery.Pooled) return;
            scenery.Pooled = true;
            scenery.Deactivate();
            _pool.Push(scenery);
        }
    }
}
