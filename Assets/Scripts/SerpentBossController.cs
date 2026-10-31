using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Змей бездны» — второй босс. Снарядами не стреляет: он свободно плавает по всему
    /// экрану и время от времени бьёт рывком головой.
    ///
    /// Цикл: ПЛАВАНИЕ -> ЗАХОД В КРУГ -> КРУГ -> ЗАМАХ -> РЫВОК -> ВОЗВРАТ -> ПЛАВАНИЕ.
    /// Сначала он вальяжно режет экран из угла в угол, потом резко стягивается на орбиту
    /// вокруг пузырька — это и есть предупреждение, что сейчас будет атака.
    /// Во время замаха загорается пунктирный луч — окно реакции.
    /// Успел подставить стража на линию рывка — змей получает урон и отлетает.
    /// Не успел — пузырёк теряет жизнь. Урон боссу наносится только блоком,
    /// так что бой целиком про позиционирование, а не про отражение.
    ///
    /// Движение головы намеренно сделано «рулёжкой»: у неё есть курс и предельная
    /// скорость поворота, поэтому она закладывает дуги, а не телепортируется к цели.
    /// Тело — цепочка звеньев на дистанционных связях плюс бегущая по ней волна:
    /// связи дают запаздывание и хлёст на рывке, волна — само ощущение плавания.
    /// </summary>
    public class SerpentBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Roam, Coil, Circle, Windup, Lunge, Recover, Dying, Done }

        class Segment
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public Vector2 Pos;      // положение в цепочке, с ним считаются связи
            public Vector2 Render;   // положение на экране: цепочка плюс волна
            public float Size;
            public int Variant;
            public bool IsTail;
        }

        public string DisplayName => Loc.Get("boss.serpent");
        public BossKind Kind => BossKind.Serpent;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public Vector2 Center => _segments != null && _segments.Length > 0 ? _segments[0].Pos : (Vector2)transform.position;

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;

        Segment[] _segments;
        SpriteRenderer _aimBeam;

        Phase _phase = Phase.Done;
        float _timer;
        float _orbitAngle;
        float _orbitDirection = 1f;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();

        Vector2 _heading = Vector2.down;   // куда смотрит голова; поворачивается плавно
        Vector2 _waypoint;                 // текущая путевая точка свободного плавания
        Vector2 _prevHeadPos;
        float _headSpeed;
        float _swimAmount;                 // сглаженная скорость: от неё зависит размах волны
        float _swimPhase;

        Vector2 _lungeDirection;
        float _lungeTravel;
        Vector2 _recoverTarget;

        // ------------------------------------------------------------- запуск

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            int count = Mathf.Max(4, _cfg.serpentSegmentCount);
            _segments = new Segment[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(i == 0 ? "SerpentHead" : "SerpentSegment" + i);
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteFactory.Unlit;
                // голова поверх тела, дальние звенья ниже — получается объём
                sr.sortingOrder = SortingOrders.Boss + (count - i);

                bool isTail = i == count - 1;
                float t = i / (float)(count - 1);
                float size = i == 0
                    ? _cfg.serpentHeadSize
                    : _cfg.serpentHeadSize * Mathf.Lerp(0.86f, 0.42f, t);

                _segments[i] = new Segment
                {
                    Tr = go.transform,
                    Sr = sr,
                    Size = size,
                    Variant = i % 2,
                    IsTail = isTail,
                };
                // У каждого звена свой порядок, поэтому силуэты не складываются.
                _tint.Track(sr);
            }

            var beamGo = new GameObject("AimBeam");
            beamGo.transform.SetParent(transform, false);
            _aimBeam = beamGo.AddComponent<SpriteRenderer>();
            _aimBeam.sprite = SpriteFactory.AimBeam();
            _aimBeam.sharedMaterial = SpriteFactory.Unlit;
            _aimBeam.sortingOrder = SortingOrders.BossAura;
            _aimBeam.enabled = false;

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            transform.position = Vector3.zero; // звенья живут в мировых координатах

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.serpentHp));
            Hp = MaxHp;
            _phase = Phase.Enter;
            _timer = 0f;
            _tint.Reset();
            _orbitDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _orbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _swimPhase = UnityEngine.Random.Range(0f, 20f);
            _swimAmount = 1f;
            _headSpeed = 0f;
            _heading = Vector2.down;
            _aimBeam.enabled = false;

            // вползает сверху единой стопкой, тело расправится само за счёт связей
            Vector2 start = new Vector2(UnityEngine.Random.Range(-1f, 1f), _field.Top + 2f);
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                s.Pos = start + Vector2.up * (i * _cfg.serpentSegmentSpacing);
                s.Render = s.Pos;
                ApplySprite(s, i);
                s.Sr.color = Color.white;
                s.Tr.position = s.Pos;
                s.Tr.localScale = Vector3.one * s.Size;
                s.Tr.gameObject.SetActive(true);
            }

            _prevHeadPos = _segments[0].Pos;
            _waypoint = _segments[0].Pos;
        }

        void ApplySprite(Segment segment, int index)
        {
            if (index == 0) segment.Sr.sprite = SpriteFactory.SerpentHead(false);
            else if (segment.IsTail) segment.Sr.sprite = SpriteFactory.SerpentTail(false);
            else segment.Sr.sprite = SpriteFactory.SerpentBody(segment.Variant, false);
        }

        // --------------------------------------------------------------- цикл

        /// <summary>0 при полном здоровье, 1 при последнем — от неё зависят все тайминги.</summary>
        float Rage => BossTuning.Rage(Hp, MaxHp);

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _tint.Tick(dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Roam: TickRoam(dt); break;
                case Phase.Coil: TickCoil(dt); break;
                case Phase.Circle: TickCircle(dt); break;
                case Phase.Windup: TickWindup(dt); break;
                case Phase.Lunge: TickLunge(dt); break;
                case Phase.Recover: TickRecover(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            UpdateSwim(dt);
            UpdateBody(dt);
        }

        // ------------------------------------------------------------ рулёжка

        float RoamHalfWidth => Mathf.Lerp(_field.PlayHalfWidth, _field.HalfWidth, 0.3f)
                               * Mathf.Max(0.1f, _cfg.serpentRoamSpread);
        float RoamTop => _field.Top - 1.1f;
        float RoamBottom => _field.Bottom + 1.1f;

        /// <summary>Разворачивает курс к нужному направлению не быстрее предельной скорости.</summary>
        void TurnTowards(Vector2 direction, float degreesPerSecond, float dt)
        {
            if (direction.sqrMagnitude < 0.000001f) return;

            float target = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float current = Mathf.Atan2(_heading.y, _heading.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, degreesPerSecond * dt) * Mathf.Deg2Rad;
            _heading = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
        }

        /// <summary>
        /// Плывёт в сторону точки: поворачивает курс и идёт вперёд. Вплотную к цели
        /// притормаживает, издалека наоборот разгоняется — иначе змей либо мечется
        /// у самой точки, либо еле ползёт через весь экран.
        /// </summary>
        void SwimTo(Vector2 target, float speed, float turnRate, float dt)
        {
            var head = _segments[0];
            Vector2 delta = target - head.Pos;
            TurnTowards(delta, turnRate, dt);

            float boost = Mathf.Clamp(delta.magnitude / 2.2f, 0.55f, 1.5f);
            head.Pos += _heading * (speed * boost * dt);
        }

        /// <summary>
        /// Мягкие борта: у края голова отворачивается внутрь, а не утыкается в него.
        /// Без этого змей, промахнувшись мимо точки, уплывал бы за экран.
        /// </summary>
        void KeepInside()
        {
            var head = _segments[0];
            float bx = RoamHalfWidth;

            if (head.Pos.x < -bx) { head.Pos.x = -bx; _heading.x = Mathf.Abs(_heading.x); }
            else if (head.Pos.x > bx) { head.Pos.x = bx; _heading.x = -Mathf.Abs(_heading.x); }

            if (head.Pos.y < RoamBottom) { head.Pos.y = RoamBottom; _heading.y = Mathf.Abs(_heading.y); }
            else if (head.Pos.y > RoamTop) { head.Pos.y = RoamTop; _heading.y = -Mathf.Abs(_heading.y); }

            if (_heading.sqrMagnitude > 0.000001f) _heading = _heading.normalized;
        }

        void UpdateSwim(float dt)
        {
            if (dt <= 0f) return;

            var head = _segments[0];
            _headSpeed = Vector2.Distance(head.Pos, _prevHeadPos) / dt;
            _prevHeadPos = head.Pos;

            float wanted = Mathf.Clamp(_headSpeed / Mathf.Max(0.5f, _cfg.serpentRoamMoveSpeed), 0f, 1.7f);
            _swimAmount = Mathf.Lerp(_swimAmount, wanted, 1f - Mathf.Exp(-7f * dt));
            _swimPhase += dt * _cfg.serpentWaveSpeed * (0.4f + _swimAmount);
        }

        Vector2 OrbitPoint(float extraRadius = 0f)
        {
            float wobble = Mathf.Sin(Time.time * 2.1f) * 0.22f;
            float radius = _cfg.serpentOrbitRadius + extraRadius + wobble;
            return _bubble.Center + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * radius;
        }

        void TickEnter(float dt)
        {
            SwimTo(OrbitPoint(), _cfg.serpentRoamMoveSpeed * 1.4f, _cfg.serpentTurnRate, dt);
            if (Vector2.Distance(_segments[0].Pos, _bubble.Center) < _cfg.serpentOrbitRadius + 0.8f) StartRoam();
        }

        // ---------------------------------------------------------- плавание

        void StartRoam()
        {
            _phase = Phase.Roam;
            _timer = Mathf.Lerp(_cfg.serpentRoamTimeMax, _cfg.serpentRoamTimeMin, Rage);
            _aimBeam.enabled = false;
            PickWaypoint();
        }

        /// <summary>
        /// Следующая точка маршрута. Требований два: она далеко от головы (иначе змей
        /// топчется в одном углу вместо того, чтобы резать экран из конца в конец) и не
        /// ближе serpentRoamMinDistance к пузырьку — достать он должен рывком, а не
        /// случайно проплывая мимо.
        /// </summary>
        void PickWaypoint()
        {
            Vector2 from = _segments[0].Pos;
            float bx = RoamHalfWidth;
            float diagonal = new Vector2(bx * 2f, RoamTop - RoamBottom).magnitude;
            float wantHop = diagonal * Mathf.Clamp01(_cfg.serpentRoamHopFactor);

            Vector2 best = from;
            float bestScore = -1f;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 candidate = new Vector2(
                    UnityEngine.Random.Range(-bx, bx),
                    UnityEngine.Random.Range(RoamBottom, RoamTop));

                // отодвигаем от пузырька, не выпуская за борта
                Vector2 fromBubble = candidate - _bubble.Center;
                if (fromBubble.magnitude < _cfg.serpentRoamMinDistance)
                {
                    if (fromBubble.sqrMagnitude < 0.0001f) fromBubble = Vector2.up;
                    candidate = _bubble.Center + fromBubble.normalized * _cfg.serpentRoamMinDistance;
                    candidate.x = Mathf.Clamp(candidate.x, -bx, bx);
                    candidate.y = Mathf.Clamp(candidate.y, RoamBottom, RoamTop);
                }

                float hop = Vector2.Distance(candidate, from);
                float score = hop >= wantHop ? 1000f + UnityEngine.Random.value : hop;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
                if (score > 1000f) break;
            }

            _waypoint = best;
        }

        void TickRoam(float dt)
        {
            SwimTo(_waypoint, _cfg.serpentRoamMoveSpeed, _cfg.serpentTurnRate, dt);
            KeepInside();

            if (Vector2.Distance(_segments[0].Pos, _waypoint) < 0.8f) PickWaypoint();

            _timer -= dt;
            if (_timer <= 0f) StartCoil();
        }

        // ------------------------------------------------------- заход в круг

        /// <summary>
        /// Резкое стягивание на орбиту: змей набирает ход и заходит в круг с той стороны,
        /// где сейчас голова. Смена темпа здесь — сигнал игроку, что дальше будет атака.
        /// </summary>
        void StartCoil()
        {
            _phase = Phase.Coil;
            _timer = _cfg.serpentCoilTime;

            Vector2 delta = _segments[0].Pos - _bubble.Center;
            _orbitAngle = delta.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(delta.y, delta.x)
                : UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            if (UnityEngine.Random.value < 0.35f) _orbitDirection = -_orbitDirection;
        }

        /// <summary>Скорость хода по орбите: ровно такая, чтобы поспевать за точкой.</summary>
        float OrbitSpeed(float multiplier) =>
            Mathf.Max(2f, _cfg.serpentOrbitRadius * _cfg.serpentOrbitSpeed * multiplier);

        void TickCoil(float dt)
        {
            _orbitAngle += _cfg.serpentOrbitSpeed * 2.2f * _orbitDirection * dt;
            SwimTo(OrbitPoint(), OrbitSpeed(3f), _cfg.serpentTurnRate * 1.6f, dt);

            _timer -= dt;
            if (_timer <= 0f)
            {
                _phase = Phase.Circle;
                _timer = Mathf.Lerp(_cfg.serpentCircleTimeMax, _cfg.serpentCircleTimeMin, Rage);
            }
        }

        void TickCircle(float dt)
        {
            _orbitAngle += _cfg.serpentOrbitSpeed * (1f + Rage * 0.5f) * _orbitDirection * dt;
            SwimTo(OrbitPoint(), OrbitSpeed(1.9f + Rage * 0.5f), _cfg.serpentTurnRate * 1.4f, dt);

            _timer -= dt;
            if (_timer <= 0f)
            {
                _phase = Phase.Windup;
                _timer = Mathf.Lerp(_cfg.serpentWindupMax, _cfg.serpentWindupMin, Rage);
                _aimBeam.enabled = true;
            }
        }

        /// <summary>
        /// Замах: змей отходит наружу и продолжает медленно дрейфовать по орбите,
        /// поэтому заранее «приклеить» стража к линии не выйдет — целиться надо в конце.
        /// </summary>
        void TickWindup(float dt)
        {
            _orbitAngle += _cfg.serpentOrbitSpeed * 0.35f * _orbitDirection * dt;
            SwimTo(OrbitPoint(0.55f), OrbitSpeed(1.1f), _cfg.serpentTurnRate, dt);

            // морда уже развёрнута на пузырёк: игрок должен читать направление удара
            TurnTowards(_bubble.Center - _segments[0].Pos, _cfg.serpentTurnRate * 0.8f, dt);

            _timer -= dt;
            UpdateAimBeam(1f - Mathf.Clamp01(_timer / 0.35f));

            if (_timer <= 0f)
            {
                Vector2 toBubble = _bubble.Center - _segments[0].Pos;
                _lungeDirection = toBubble.sqrMagnitude > 0.0001f ? toBubble.normalized : Vector2.down;
                _heading = _lungeDirection;
                _lungeTravel = 0f;
                _phase = Phase.Lunge;
                _aimBeam.enabled = false;
            }
        }

        void TickLunge(float dt)
        {
            float speed = Mathf.Lerp(_cfg.serpentLungeSpeedMin, _cfg.serpentLungeSpeedMax, Rage);
            float step = speed * dt;

            var head = _segments[0];
            head.Pos += _lungeDirection * step;
            _lungeTravel += step;

            // 1) страж принял удар
            if (_guardian.CanDeflect &&
                Vector2.Distance(head.Pos, _guardian.Center) < _cfg.serpentHeadRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(head.Pos);
                ApplyDamage();
                StartRecover(-_lungeDirection);
                return;
            }

            // 2) достал пузырёк
            if (Vector2.Distance(head.Pos, _bubble.Center) < _cfg.serpentHeadRadius + _bubble.Radius)
            {
                BubbleHit?.Invoke(head.Pos);
                StartRecover(-_lungeDirection);
                return;
            }

            // 3) промахнулся и ушёл слишком далеко
            if (_lungeTravel > _cfg.serpentOrbitRadius * 2.2f) StartRecover(-_lungeDirection);
        }

        void StartRecover(Vector2 pushDirection)
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;

            _phase = Phase.Recover;
            _timer = _cfg.serpentRecoverTime;
            // отскок наружу, к новой точке орбиты чуть дальше по кругу
            _orbitAngle += _orbitDirection * 0.5f;
            _recoverTarget = OrbitPoint(0.4f) + pushDirection * 0.3f;
            _heading = pushDirection.sqrMagnitude > 0.0001f ? pushDirection.normalized : _heading;
        }

        void TickRecover(float dt)
        {
            SwimTo(_recoverTarget, OrbitSpeed(2.2f), _cfg.serpentTurnRate * 1.5f, dt);
            KeepInside();

            _timer -= dt;
            // после отскока снова уходит в свободное плавание
            if (_timer <= 0f) StartRoam();
        }

        void TickDying(float dt)
        {
            _timer += dt;

            // агония: голова мечется, тело тянется следом
            var head = _segments[0];
            head.Pos += new Vector2(Mathf.Sin(_timer * 17f), Mathf.Cos(_timer * 13f)) * (2.4f * dt);
            head.Pos += Vector2.up * (0.5f * dt);

            float alpha = Mathf.Clamp01(1f - _timer / 1.1f);
            for (int i = 0; i < _segments.Length; i++)
            {
                var col = _segments[i].Sr.color;
                col.a = alpha;
                _segments[i].Sr.color = col;
            }

            if (_timer >= 1.1f)
            {
                _phase = Phase.Done;
                Defeated?.Invoke(head.Pos);
                Despawn();
            }
        }

        void ApplyDamage()
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(_segments[0].Pos);

            if (Hp <= 0)
            {
                _phase = Phase.Dying;
                _timer = 0f;
                _aimBeam.enabled = false;
            }
        }

        // --------------------------------------------------------------- тело

        /// <summary>
        /// Дистанционные связи: каждое звено подтягивается к предыдущему на фиксированное
        /// расстояние. Это и даёт «змеиность» — тело запаздывает и хлещет на рывке.
        /// Поверх связей бежит волна: она влияет только на рисунок, в сами связи не
        /// подмешивается, иначе изгиб копился бы от звена к звену и змей сворачивался
        /// бы в спираль.
        /// </summary>
        void UpdateBody(float dt)
        {
            float spacing = _cfg.serpentSegmentSpacing;
            var head = _segments[0];

            for (int i = 1; i < _segments.Length; i++)
            {
                var prev = _segments[i - 1];
                var seg = _segments[i];

                Vector2 delta = seg.Pos - prev.Pos;
                float distance = delta.magnitude;
                if (distance < 0.0001f)
                {
                    delta = Vector2.up;
                    distance = 1f;
                }

                Vector2 constrained = prev.Pos + delta / distance * spacing;
                // лёгкое сглаживание, иначе цепочка выглядит слишком «жёсткой»
                seg.Pos = Vector2.Lerp(seg.Pos, constrained, 1f - Mathf.Exp(-28f * dt));
            }

            float amplitude = _cfg.serpentWaveAmplitude * Mathf.Min(1.4f, _swimAmount);
            float step = Mathf.PI * 2f / Mathf.Max(2f, _cfg.serpentWaveLength);

            head.Render = head.Pos;
            for (int i = 1; i < _segments.Length; i++)
            {
                var seg = _segments[i];
                Vector2 along = _segments[i - 1].Pos - seg.Pos;
                Vector2 perpendicular = along.sqrMagnitude > 0.0001f
                    ? new Vector2(-along.y, along.x).normalized
                    : Vector2.right;

                // у шеи волны почти нет, к хвосту размах растёт — так виляет живая рыба
                float taper = Mathf.Min(1f, i / 3f) * Mathf.Lerp(0.6f, 1.25f, i / (float)(_segments.Length - 1));
                float offset = Mathf.Sin(_swimPhase - i * step);
                seg.Render = seg.Pos + perpendicular * (offset * amplitude * taper);
            }

            // голова смотрит по движению, звенья — на предыдущее звено
            ApplyTransform(head, head.Render - _segments[1].Render);
            for (int i = 1; i < _segments.Length; i++)
                ApplyTransform(_segments[i], _segments[i - 1].Render - _segments[i].Render);
        }

        static void ApplyTransform(Segment segment, Vector2 forward)
        {
            segment.Tr.position = segment.Render;
            if (forward.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
                segment.Tr.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            segment.Tr.localScale = Vector3.one * segment.Size;
        }

        void UpdateAimBeam(float intensity)
        {
            Vector2 from = _segments[0].Pos;
            Vector2 to = _bubble.Center;
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.05f) { _aimBeam.enabled = false; return; }

            _aimBeam.transform.position = from + delta * 0.5f;
            _aimBeam.transform.rotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // спрайт луча в мире 1 x 0.125 юнита при масштабе 1
            _aimBeam.transform.localScale = new Vector3(distance, 1.4f, 1f);

            float alpha = Mathf.Lerp(0.35f, 0.95f, Mathf.Clamp01(intensity));
            _aimBeam.color = Palette.Coral.WithAlpha(alpha);
        }

        /// <summary>
        /// Змей бросает начатую атаку и уходит в свободное плавание. Вызывается при
        /// возрождении игрока: рывок, начатый до его смерти, к нему уже не относится.
        /// </summary>
        public void Interrupt()
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;

            _aimBeam.enabled = false;
            _lungeTravel = 0f;
            StartRoam();
        }

        public void Despawn()
        {
            _phase = Phase.Done;
            if (_aimBeam != null) _aimBeam.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
