using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Каменный панцирь» — стая черепах, вставших кольцом вокруг пузырька. Как и у
    /// пираний, полоса HP считает живых: **отбил камень — черепаха, которая его
    /// метнула, выбывает**.
    ///
    /// Проверяемый навык — ритм и круговое ведение. Черепахи стреляют по очереди
    /// вдоль кольца, и порядок читается: пока идёт замах у одной, уже видно, кто
    /// следующий. Играть надо не реакцией на выстрел, а движением по кругу на шаг
    /// вперёд — рука ведёт стража вдоль кольца, как стрелку часов.
    ///
    /// По ходу боя к одиночному броску добавляются ещё два приёма — иначе кольцо
    /// быстро превращается в однообразную карусель:
    ///
    /// * **Веер из трёх камней** (с `turtleFanFromRage`). В пузырёк летит только
    ///   средний, два крайних уходят мимо. Отличить их можно только по линии полёта,
    ///   а перезарядка стража одна: потратил её на обманку — пропустил настоящий.
    ///   Это единственный момент боя, где нужно не успеть, а выбрать.
    /// * **Таран панцирем** (с `turtleChargeFromRage`). Черепаха втягивается,
    ///   раскручивается на месте и катится через всю арену сквозь пузырёк. Отбить
    ///   можно — и тогда она выбывает, — но перекрывать надо не камень, а её саму.
    ///
    /// И ещё три вещи не дают заучить карусель:
    ///
    /// 1. **Кольцо медленно вращается**, поэтому позиции не запоминаются.
    /// 2. **Иногда очередь перескакивает** (`turtleJumpChance`) — ход берёт случайная
    ///    черепаха, и вести стража вслепую по кругу нельзя, надо смотреть на замах.
    /// 3. **Кольцо сжимается к концу боя** (`turtleRingTighten`): камень летит то же
    ///    расстояние меньше времени.
    ///
    /// Самих черепах трогать бесполезно: они прячутся в панцирь и урона от касания
    /// не получают. Единственный способ убрать черепаху — принять её камень.
    /// </summary>
    public class TurtleBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Ready, Aim, Volley, ChargeAim, Charge, Dying, Done }

        class Turtle
        {
            public SpriteRenderer Sr;
            public float Angle;         // положение на кольце
            public bool Alive;
            /// <summary>Сколько попаданий панцирь ещё держит. Ноль — черепаха выбывает.</summary>
            public int Shell;
            public float DyingTimer;
            public float Recoil;        // насколько отведена назад при замахе
            public float Flash;
        }

        class Rock
        {
            public SpriteRenderer Sr;
            public Vector2 Position;
            public Vector2 Velocity;
            /// <summary>Чей камень. −1 у обманок из веера: их можно отбить, но толку не будет.</summary>
            public int Owner;
            public bool Active;
            public float Spin;
        }

        public string DisplayName => Loc.Get("boss.turtles");
        public BossKind Kind => BossKind.Turtle;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;

        Turtle[] _ring;
        Rock[] _rocks;

        Phase _phase = Phase.Done;
        float _timer;
        float _ringPhase;
        float _pulse;
        float _spawnProgress;

        int _shooter = -1;
        int _burstLeft;
        float _burstGap;

        // таран панцирем
        int _charger = -1;
        Vector2 _chargeDirection;
        Vector2 _chargePosition;
        float _chargeSpin;
        bool _chargeHitBubble;

        SpriteRenderer[] _chevrons;
        const int ChevronCount = 9;

        float Rage => BossTuning.Rage(Hp, MaxHp);

        /// <summary>Цвет панциря, который уже держал удар.</summary>
        static readonly Color CrackedShell = new Color(1f, 0.70f, 0.58f, 1f);
        float Size => _field.PlayHalfWidth * 2f * _cfg.turtleSizeSpan;
        float RingRadius => _field.PlayHalfWidth * 2f * _cfg.turtleRingSpan;

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            _ring = new Turtle[Mathf.Max(1, cfg.turtleCount)];
            for (int i = 0; i < _ring.Length; i++)
            {
                var go = new GameObject("Turtle" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Turtle();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Boss;
                _ring[i] = new Turtle { Sr = sr };
            }

            // Камней в воздухе больше двух-трёх не бывает, но запас на залп нужен.
            _rocks = new Rock[Mathf.Max(4, cfg.turtleCount)];
            for (int i = 0; i < _rocks.Length; i++)
            {
                var go = new GameObject("Rock" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Rock();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Projectile;
                sr.enabled = false;
                _rocks[i] = new Rock { Sr = sr };
            }

            // Стрелки нужны только тарану: камень летит сам по себе и виден,
            // а вот катящаяся черепаха обязана заранее показать линию.
            var chevron = SpriteFactory.AimChevron();
            _chevrons = new SpriteRenderer[ChevronCount];
            for (int i = 0; i < ChevronCount; i++)
            {
                var go = new GameObject("AimChevron" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = chevron;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.BossAura;
                sr.enabled = false;
                _chevrons[i] = sr;
            }

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            transform.position = Vector3.zero;

            MaxHp = _ring.Length;
            Hp = MaxHp;
            _pulse = 0f;
            _ringPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _shooter = -1;
            _spawnProgress = 0f;

            for (int i = 0; i < _ring.Length; i++)
            {
                var t = _ring[i];
                t.Alive = true;
                t.Shell = Mathf.Max(1, _cfg.turtleShellHits);
                t.DyingTimer = 0f;
                t.Recoil = 0f;
                t.Flash = 0f;
                t.Angle = _ringPhase + i * (Mathf.PI * 2f / _ring.Length);
                t.Sr.sprite = SpriteFactory.Turtle();
                t.Sr.color = Color.white;
                t.Sr.enabled = true;
            }

            for (int i = 0; i < _rocks.Length; i++) DeactivateRock(_rocks[i]);

            _charger = -1;
            HideChevrons();

            _phase = Phase.Enter;
            _timer = _cfg.turtleEnterTime;
        }

        /// <summary>Камни в полёте гаснут вместе с их хозяевами, отдельного сброса нет.</summary>
        public void Interrupt() { }

        public void Despawn()
        {
            _phase = Phase.Done;
            for (int i = 0; i < _rocks.Length; i++) DeactivateRock(_rocks[i]);
            HideChevrons();
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _pulse += dt;

            // Кольцо крутится здесь и только здесь: угол хранится у самой черепахи,
            // отдельного «фазового» счётчика нет, чтобы они не разъезжались.
            float spin = _cfg.turtleRingSpin * Mathf.Deg2Rad * dt;
            for (int i = 0; i < _ring.Length; i++) _ring[i].Angle += spin;

            TickDeaths(dt);
            TickRocks(dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Ready: TickReady(dt); break;
                case Phase.Aim: TickAim(dt); break;
                case Phase.Volley: TickVolley(dt); break;
                case Phase.ChargeAim: TickChargeAim(dt); break;
                case Phase.Charge: TickCharge(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            ApplyRing();
        }

        /// <summary>Кольцо съезжается к пузырьку — видно, что тебя обступают.</summary>
        void TickEnter(float dt)
        {
            _timer -= dt;
            _spawnProgress = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.turtleEnterTime));
            if (_timer <= 0f)
            {
                _spawnProgress = 1f;
                StartReady(_cfg.turtleFirstPause);
            }
        }

        void StartReady(float pause)
        {
            _phase = Phase.Ready;
            _timer = pause;
        }

        void TickReady(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;

            if (CountAlive() == 0) { StartDying(); return; }

            // Таран решается раньше очереди: он заменяет её целиком.
            if (Rage >= _cfg.turtleChargeFromRage &&
                UnityEngine.Random.value < _cfg.turtleChargeChance)
            {
                StartChargeAim();
                return;
            }

            // Длина очереди растёт к концу боя: под конец стреляют пачками.
            _burstLeft = 1 + Mathf.FloorToInt(Rage * (_cfg.turtleBurstMax - 1) + 0.0001f);
            _burstGap = Mathf.Lerp(_cfg.turtleBurstGapMax, _cfg.turtleBurstGapMin, Rage);
            StartAim(nextInOrder: true);
        }

        // -------------------------------------------------------------- замах

        void StartAim(bool nextInOrder)
        {
            int next = PickShooter(nextInOrder);
            if (next < 0) { StartDying(); return; }

            _shooter = next;
            _phase = Phase.Aim;
            _timer = Mathf.Lerp(_cfg.turtleAimTimeMax, _cfg.turtleAimTimeMin, Rage);
        }

        /// <summary>
        /// Следующий стрелок. Обычно — соседняя живая черепаха по ходу кольца: порядок
        /// должен читаться, иначе ритма не выучить. Но иногда очередь перескакивает,
        /// чтобы игрок всё-таки смотрел на замах, а не крутил стража вслепую.
        /// </summary>
        int PickShooter(bool nextInOrder)
        {
            if (CountAlive() == 0) return -1;

            // Повтор: та же черепаха стреляет ещё раз. Ломает привычку сразу после
            // отбитого камня переводить стража на соседнюю по ходу кольца.
            if (nextInOrder && _shooter >= 0 && _ring[_shooter].Alive &&
                UnityEngine.Random.value < _cfg.turtleRepeatChance)
                return _shooter;

            if (nextInOrder && _shooter >= 0 && UnityEngine.Random.value >= _cfg.turtleJumpChance)
            {
                for (int step = 1; step <= _ring.Length; step++)
                {
                    int index = (_shooter + step) % _ring.Length;
                    if (_ring[index].Alive) return index;
                }
            }

            // случайная живая
            int alive = CountAlive();
            int pick = UnityEngine.Random.Range(0, alive);
            for (int i = 0; i < _ring.Length; i++)
            {
                if (!_ring[i].Alive) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        void TickAim(float dt)
        {
            _timer -= dt;

            var t = _ring[_shooter];
            if (!t.Alive) { StartReady(0.15f); return; }

            // Замах: черепаха отходит назад по радиусу и подсвечивается. Отход наружу
            // читается лучше любой подсветки — видно даже боковым зрением.
            float k = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.turtleAimTimeMax));
            t.Recoil = Mathf.Sin(k * Mathf.PI) * _cfg.turtleRecoil;
            t.Flash = k;

            if (_timer <= 0f) Fire(t);
        }

        void Fire(Turtle t)
        {
            t.Recoil = 0f;
            t.Flash = 0f;

            Vector2 from = PositionOf(t);
            Vector2 delta = _bubble.Center - from;
            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;

            int owner = IndexOf(t);
            float speed = Mathf.Lerp(_cfg.turtleRockSpeedMin, _cfg.turtleRockSpeedMax, Rage);

            // Веер: настоящий камень идёт по центру, обманки — по бокам. Внешне они
            // неотличимы намеренно, разница только в линии полёта.
            bool fan = Rage >= _cfg.turtleFanFromRage &&
                       UnityEngine.Random.value < _cfg.turtleFanChance;

            LaunchRock(from, direction, speed, owner);
            if (fan)
            {
                float spread = _cfg.turtleFanSpread;
                LaunchRock(from, Rotate(direction, spread), speed, -1);
                LaunchRock(from, Rotate(direction, -spread), speed, -1);
            }

            _burstLeft--;
            if (_burstLeft > 0)
            {
                _phase = Phase.Volley;
                _timer = _burstGap;
            }
            else
            {
                StartReady(Mathf.Lerp(_cfg.turtleCycleGapMax, _cfg.turtleCycleGapMin, Rage));
            }
        }

        void LaunchRock(Vector2 from, Vector2 direction, float speed, int owner)
        {
            var rock = RentRock();
            if (rock == null) return;

            rock.Active = true;
            rock.Owner = owner;
            rock.Position = from + direction * (Size * 0.55f);
            rock.Velocity = direction * speed;
            rock.Spin = UnityEngine.Random.Range(-220f, 220f);
            rock.Sr.enabled = true;
            rock.Sr.transform.position = rock.Position;
            rock.Sr.transform.localScale = Vector3.one * (_cfg.turtleRockRadius * 2.2f);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // -------------------------------------------------------------- таран

        /// <summary>
        /// Раскрутка перед тараном. Черепаха остаётся на кольце, но бешено вращается
        /// и тянет стрелки к пузырьку: видно и кто пошёл, и по какой линии.
        /// </summary>
        void StartChargeAim()
        {
            int index = PickShooter(nextInOrder: false);
            if (index < 0) { StartDying(); return; }

            _charger = index;
            _shooter = index;                 // очередь продолжится от него
            _phase = Phase.ChargeAim;
            _timer = _cfg.turtleChargeAimTime;
            _chargeSpin = 0f;
        }

        void TickChargeAim(float dt)
        {
            _timer -= dt;

            var t = _ring[_charger];
            if (!t.Alive) { HideChevrons(); StartReady(0.15f); return; }

            _chargeSpin += 900f * dt;
            t.Flash = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.turtleChargeAimTime));

            Vector2 from = PositionOf(t);
            UpdateChevrons(from, _bubble.Center, t.Flash);

            if (_timer > 0f) return;

            HideChevrons();
            _chargePosition = from;
            Vector2 delta = _bubble.Center - from;
            _chargeDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
            _chargeHitBubble = false;
            t.Flash = 0f;
            _phase = Phase.Charge;
        }

        void TickCharge(float dt)
        {
            var t = _ring[_charger];
            if (!t.Alive) { StartReady(0.2f); return; }

            _chargeSpin += 720f * dt;

            float distance = _cfg.turtleChargeSpeed * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
            float step = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                _chargePosition += _chargeDirection * step;

                if (_guardian.CanDeflect &&
                    Vector2.Distance(_chargePosition, _guardian.Center) < _cfg.turtleChargeRadius + _guardian.Radius)
                {
                    _guardian.NotifyDeflect();
                    GuardianBlocked?.Invoke(_chargePosition);
                    Kill(t);
                    StartReady(0.25f);
                    return;
                }

                if (!_chargeHitBubble &&
                    Vector2.Distance(_chargePosition, _bubble.Center) < _cfg.turtleChargeRadius + _bubble.Radius)
                {
                    _chargeHitBubble = true;   // на подшагах панцирь пройдёт рядом ещё не раз
                    BubbleHit?.Invoke(_chargePosition);
                }
            }

            // Укатился за кадр — возвращается в строй на своё место кольца.
            if (Mathf.Abs(_chargePosition.x) > _field.HalfWidth + 2f ||
                _chargePosition.y > _field.Top + 2f || _chargePosition.y < _field.Bottom - 2f)
            {
                _charger = -1;
                StartReady(Mathf.Lerp(_cfg.turtleCycleGapMax, _cfg.turtleCycleGapMin, Rage));
            }
        }

        void UpdateChevrons(Vector2 from, Vector2 to, float urgency)
        {
            Vector2 delta = to - from;
            if (delta.magnitude < 0.3f) { HideChevrons(); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float head = Mathf.Repeat(_pulse * 2f, 1f);

            for (int i = 0; i < _chevrons.Length; i++)
            {
                float k = (i + 0.5f) / _chevrons.Length;
                var tr = _chevrons[i].transform;
                tr.position = from + delta * k;
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = Vector3.one * 0.6f;

                float wave = 1f - Mathf.Min(Mathf.Abs(k - head), Mathf.Abs(k - head + 1f)) * 4f;
                float bright = Mathf.Clamp01(0.3f + urgency * 0.45f + Mathf.Max(0f, wave) * 0.5f);

                _chevrons[i].enabled = true;
                _chevrons[i].color = new Color(Palette.Coral.r, Palette.Coral.g, Palette.Coral.b, bright);
            }
        }

        void HideChevrons()
        {
            if (_chevrons == null) return;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = false;
        }

        /// <summary>Пауза между выстрелами очереди.</summary>
        void TickVolley(float dt)
        {
            _timer -= dt;
            if (_timer <= 0f) StartAim(nextInOrder: true);
        }

        // -------------------------------------------------------------- камни

        void TickRocks(float dt)
        {
            for (int i = 0; i < _rocks.Length; i++)
            {
                var r = _rocks[i];
                if (!r.Active) continue;

                float distance = r.Velocity.magnitude * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
                Vector2 step = r.Velocity * (dt / steps);

                for (int s = 0; s < steps; s++)
                {
                    r.Position += step;
                    if (ResolveRock(r)) break;
                }

                if (!r.Active) continue;

                r.Sr.transform.position = r.Position;
                r.Sr.transform.Rotate(0f, 0f, r.Spin * dt);

                if (Mathf.Abs(r.Position.x) > _field.HalfWidth + 2f ||
                    r.Position.y > _field.Top + 2f || r.Position.y < _field.Bottom - 2f)
                    DeactivateRock(r);
            }
        }

        /// <summary>Возвращает true, если камень отработал и больше не летит.</summary>
        bool ResolveRock(Rock r)
        {
            if (_guardian.CanDeflect &&
                Vector2.Distance(r.Position, _guardian.Center) < _cfg.turtleRockRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(r.Position);

                // Отбитый камень уходит обратно по своей же линии и достаёт хозяина.
                // Целиться при этом не нужно — в отличие от медузы, где отражение надо
                // ещё и направить. Здесь навык в ритме, а не в углах.
                //
                // У обманок из веера хозяина нет (Owner = −1): отбить их можно, но
                // единственное, что это даст, — потраченная перезарядка стража.
                if (r.Owner >= 0 && r.Owner < _ring.Length && _ring[r.Owner].Alive)
                    Crack(_ring[r.Owner]);

                DeactivateRock(r);
                return true;
            }

            if (Vector2.Distance(r.Position, _bubble.Center) < _cfg.turtleRockRadius + _bubble.Radius)
            {
                BubbleHit?.Invoke(r.Position);
                DeactivateRock(r);
                return true;
            }

            return false;
        }

        Rock RentRock()
        {
            for (int i = 0; i < _rocks.Length; i++)
                if (!_rocks[i].Active) return _rocks[i];
            return null;
        }

        void DeactivateRock(Rock r)
        {
            r.Active = false;
            r.Owner = -1;
            r.Sr.enabled = false;
        }

        // -------------------------------------------------------------- гибель

        /// <summary>
        /// Возвращённый камень попал в хозяина. Панцирь держит несколько ударов, и
        /// выбывает черепаха только когда он кончился: раньше одно попадание убирало
        /// стрелка насовсем, и кольцо разбиралось быстрее, чем игрок успевал понять,
        /// в каком оно ритме.
        /// </summary>
        void Crack(Turtle t)
        {
            t.Flash = 0.25f;

            if (--t.Shell > 0)
            {
                // Цвет ставит отрисовка: она переписывает его каждый кадр, и здесь
                // задавать его бессмысленно. Само событие нужно ради искр и звука —
                // без них отбитый камень выглядел бы потраченным впустую.
                Damaged?.Invoke(PositionOf(t));
                return;
            }

            Kill(t);
        }

        void Kill(Turtle t)
        {
            t.Alive = false;
            t.DyingTimer = 0.45f;
            t.Recoil = 0f;

            Hp = Mathf.Max(0, Hp - 1);
            Damaged?.Invoke(PositionOf(t));

            if (Hp <= 0) StartDying();
        }

        void TickDeaths(float dt)
        {
            for (int i = 0; i < _ring.Length; i++)
            {
                var t = _ring[i];
                if (t.Alive || t.DyingTimer <= 0f) continue;

                t.DyingTimer -= dt;
                // отброшенная черепаха уходит наружу кольца
                t.Recoil += 3.5f * dt;

                float k = Mathf.Clamp01(t.DyingTimer / 0.45f);
                t.Sr.color = new Color(1f, 1f, 1f, k);
                if (t.DyingTimer <= 0f) t.Sr.enabled = false;
            }
        }

        void StartDying()
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;
            _phase = Phase.Dying;
            _timer = 0f;
        }

        void TickDying(float dt)
        {
            _timer += dt;
            if (_timer < 0.6f) return;

            _phase = Phase.Done;
            Defeated?.Invoke(_bubble.Center + Vector2.up * 2f);
            Despawn();
        }

        // ------------------------------------------------------- вспомогательное

        int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < _ring.Length; i++) if (_ring[i].Alive) n++;
            return n;
        }

        int IndexOf(Turtle t)
        {
            for (int i = 0; i < _ring.Length; i++) if (ReferenceEquals(_ring[i], t)) return i;
            return -1;
        }

        /// <summary>
        /// Полуоси кольца. Ровная окружность заданного радиуса не годится: пузырёк
        /// сидит низко (bubbleYFactor = 0.32), и нижние черепахи уезжали бы под кромку
        /// экрана, а на портретной ориентации боковые вылезали бы за края. Поэтому
        /// кольцо ужимается до того, что реально помещается, и остаётся видимым целиком
        /// при любом соотношении сторон.
        ///
        /// Нижняя граница сплющивания не даёт кольцу выродиться в полоску.
        /// </summary>
        Vector2 RingRadii()
        {
            // К концу боя кольцо подступает ближе: то же расстояние камень пролетает
            // быстрее, и времени на реакцию остаётся меньше.
            float wanted = RingRadius * Mathf.Lerp(1f, _cfg.turtleRingTighten, Rage);
            float margin = Size * 0.55f;

            float rx = Mathf.Min(wanted, _field.HalfWidth - margin);
            float ry = Mathf.Min(wanted,
                                 Mathf.Min(_field.Top - _bubble.Center.y,
                                           _bubble.Center.y - _field.Bottom) - margin);

            rx = Mathf.Max(rx, wanted * 0.5f);
            ry = Mathf.Max(ry, wanted * 0.5f);
            return new Vector2(rx, ry);
        }

        /// <summary>
        /// Положение черепахи на кольце. На входе радиус едет от края экрана внутрь —
        /// кольцо съезжается, а не появляется из ниоткуда.
        /// </summary>
        Vector2 PositionOf(Turtle t)
        {
            // Таранящая черепаха живёт вне кольца — у неё своя траектория.
            if (_phase == Phase.Charge && _charger >= 0 && ReferenceEquals(_ring[_charger], t))
                return _chargePosition;

            Vector2 radii = RingRadii();
            float grow = Mathf.Lerp(_field.HalfWidth, 0f, _spawnProgress) + t.Recoil;

            var direction = new Vector2(Mathf.Cos(t.Angle), Mathf.Sin(t.Angle));
            Vector2 point = _bubble.Center
                          + new Vector2(direction.x * (radii.x + grow),
                                        direction.y * (radii.y + grow));

            // Последняя страховка: на входе и на отлёте после смерти черепаха может
            // выйти за кадр, и это нормально, — но живая и стреляющая обязана быть видна.
            return t.Alive && _spawnProgress >= 1f
                ? _field.ClampToScreen(point, Size * 0.45f)
                : point;
        }

        void ApplyRing()
        {
            float size = Size;

            for (int i = 0; i < _ring.Length; i++)
            {
                var t = _ring[i];
                if (!t.Sr.enabled) continue;

                Vector2 position = PositionOf(t);
                t.Sr.transform.position = position;

                // Черепаха всегда развёрнута мордой к пузырьку: сразу понятно, что цель — он.
                Vector2 toBubble = _bubble.Center - position;
                // См. комментарий у пираний: flipX зеркалит до поворота,
                // поэтому угол сдвигается на 180°, а не отражается.
                float angle = Mathf.Atan2(toBubble.y, toBubble.x) * Mathf.Rad2Deg;
                bool mirror = toBubble.x < 0f;
                if (mirror) angle -= 180f;

                t.Sr.flipX = mirror;
                t.Sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                // На таране панцирь крутится волчком — самый заметный сигнал в бою.
                bool charging = (_phase == Phase.Charge || _phase == Phase.ChargeAim) &&
                                _charger >= 0 && ReferenceEquals(_ring[_charger], t);
                if (charging)
                {
                    t.Sr.flipX = false;
                    t.Sr.transform.rotation = Quaternion.Euler(0f, 0f, _chargeSpin);
                }

                // На замахе слегка раздувается — вместе с отходом наружу это читается
                // как «сейчас метнёт».
                float swell = 1f + t.Flash * (charging ? 0.30f : 0.18f);
                t.Sr.transform.localScale = Vector3.one * (size * swell);

                if (t.Alive)
                {
                    // Треснувший панцирь держит свой цвет постоянно, а вспышка замаха
                    // ложится поверх него. Иначе эта строка, выполняясь каждый кадр,
                    // просто стирала бы отметку о попадании.
                    Color shell = t.Shell >= Mathf.Max(1, _cfg.turtleShellHits)
                        ? Color.white : CrackedShell;
                    t.Sr.color = Color.Lerp(shell, Palette.Coral, t.Flash * 0.55f);
                }
            }
        }
    }
}
