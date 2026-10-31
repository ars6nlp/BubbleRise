using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Спрут глубин» — третий босс. Наверху висит только голова; щупальца к ней
    /// не крепятся — они торчат из-за краёв экрана по всему периметру, так что спрут
    /// окружает арену целиком, а его настоящий размер остаётся за кадром.
    ///
    /// Каждый из первых двух боссов проверял одну линию: «Страж» — отражение снаряда,
    /// «Змей» — позицию на одной линии рывка. Спрут проверяет умение расставлять
    /// приоритеты: он бьёт сериями по два-три щупальца подряд с разных сторон,
    /// и закрыть все физически нельзя — надо выбирать.
    ///
    /// Цикл щупальца: ПОКОЙ -> ЗАМАХ -> УДАР -> ФИКСАЦИЯ -> ОТХОД.
    /// На замахе кончик заранее встаёт на позицию удара и загорается пунктирный луч
    /// до пузырька — видно, откуда придёт удар. Каждое щупальце бьёт со своей стороны
    /// периметра, поэтому угрозы приходят отовсюду и держать стража над пузырьком
    /// бесполезно.
    ///
    /// Щупальце рисуется квадратичной кривой Безье от основания к кончику:
    /// в покое оно закручено, к удару выпрямляется. Урон боссу — только за блок.
    /// </summary>
    public class OctopusBossController : MonoBehaviour, IBoss
    {
        enum BossPhase { Enter, Fight, Dying, Done }
        enum ArmPhase { Idle, Windup, Strike, Hold, Retract }

        class Arm
        {
            public Transform[] Segments;
            public SpriteRenderer[] Renderers;
            public SpriteRenderer Beam;

            public float BaseAngle;      // куда щупальце смотрит из мантии
            public float IdlePhase;
            public float CurlSide;
            public float IdleReach;   // насколько глубоко это щупальце заходит в покое

            public ArmPhase Phase;
            public float Timer;
            public float Delay;          // задержка перед началом замаха (серийная атака)

            public Vector2 Tip;
            public Vector2 StagePoint;   // точка, с которой пойдёт удар
            public float Curl;           // насколько щупальце закручено: 1 — дуга, 0 — прямая
        }

        public string DisplayName => Loc.Get("boss.octopus");
        public BossKind Kind => BossKind.Octopus;
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

        SpriteRenderer _head;
        Arm[] _arms;

        BossPhase _phase = BossPhase.Done;
        float _timer;
        float _attackTimer;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();
        float _targetY;
        float _sway;

        // Пересчитывается из долей игровой колонки на спавне.
        float _headRadius;
        float _idleRing;
        float _strikeRange;

        float Rage => BossTuning.Rage(Hp, MaxHp);
        Vector2 HeadCentre => transform.position;

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            // Мантия — отдельный дочерний объект: корень обязан остаться с масштабом 1,
            // иначе он множил бы масштаб звеньев щупалец.
            var mantle = new GameObject("Mantle");
            mantle.transform.SetParent(transform, false);
            _head = mantle.AddComponent<SpriteRenderer>();
            _head.sprite = SpriteFactory.OctopusHead(false);
            _head.sharedMaterial = SpriteFactory.Unlit;
            _head.sortingOrder = SortingOrders.Boss + 2;

            int armCount = Mathf.Max(4, _cfg.octopusArmsTotal);
            int segCount = Mathf.Max(8, _cfg.octopusArmSegments);
            _arms = new Arm[armCount];

            for (int a = 0; a < armCount; a++)
            {
                var arm = new Arm
                {
                    Segments = new Transform[segCount],
                    Renderers = new SpriteRenderer[segCount],
                    IdlePhase = UnityEngine.Random.Range(0f, 10f),
                    CurlSide = a % 2 == 0 ? 1f : -1f,
                    IdleReach = 1f,
                };

                // Основания раскиданы по ВСЕЙ окружности, а не только по низу: спрут
                // держит арену целиком. Полшага смещения — чтобы ни одно щупальце
                // не оказалось ровно за головой.
                float step = 360f / armCount;
                arm.BaseAngle = (step * a + step * 0.5f) * Mathf.Deg2Rad;

                for (int s = 0; s < segCount; s++)
                {
                    var go = new GameObject("Tentacle" + a + "_" + s);
                    go.transform.SetParent(transform, false);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sharedMaterial = SpriteFactory.Unlit;
                    // ближние к основанию звенья под мантией, кончик — поверх
                    sr.sortingOrder = SortingOrders.Boss + (s == segCount - 1 ? 3 : 1);

                    arm.Segments[s] = go.transform;
                    arm.Renderers[s] = sr;
                }

                var beamGo = new GameObject("ArmBeam" + a);
                beamGo.transform.SetParent(transform, false);
                arm.Beam = beamGo.AddComponent<SpriteRenderer>();
                arm.Beam.sprite = SpriteFactory.AimBeam();
                arm.Beam.sharedMaterial = SpriteFactory.Unlit;
                arm.Beam.sortingOrder = SortingOrders.BossAura;
                arm.Beam.enabled = false;

                _arms[a] = arm;
            }

            // Подмес при попадании — на мантию и кончики. Звенья щупалец лежат на
            // одном порядке и перекрывают друг друга: два силуэта там сложились бы вдвое.
            _tint.Track(_head);
            for (int a = 0; a < _arms.Length; a++)
                _tint.Track(_arms[a].Renderers[_arms[a].Renderers.Length - 1]);

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.octopusHp));
            Hp = MaxHp;
            _phase = BossPhase.Enter;
            _timer = 0f;
            _tint.Reset();
            _sway = UnityEngine.Random.Range(0f, 10f);
            _targetY = _field.YFromTop(_cfg.octopusHeadHover);
            _attackTimer = 1.6f;

            float column = _field.PlayHalfWidth * 2f;
            _headRadius = column * _cfg.octopusHeadRadiusSpan;
            _idleRing = column * _cfg.octopusIdleRingSpan;
            _strikeRange = column * _cfg.octopusStrikeSpan;

            transform.position = new Vector3(0f, _field.Top + _headRadius * 2f, 0f);
            transform.localScale = Vector3.one;
            _head.transform.localScale = Vector3.one * (_headRadius * 2f);
            _head.color = Color.white;

            RefreshSprites();

            for (int a = 0; a < _arms.Length; a++)
            {
                var arm = _arms[a];
                arm.Phase = ArmPhase.Idle;
                arm.Timer = 0f;
                arm.Delay = 0f;
                arm.Curl = 1f;
                // часть щупалец подбирается вплотную, часть остаётся у краёв — так они
                // не висят все на одной окружности и картинка выглядит живой
                arm.IdleReach = 1f + UnityEngine.Random.Range(-1f, 1f) * _cfg.octopusIdleRingSpread;
                arm.Tip = IdleTip(arm);
                arm.Beam.enabled = false;

                for (int s = 0; s < arm.Segments.Length; s++)
                {
                    arm.Renderers[s].color = Color.white;
                    arm.Segments[s].gameObject.SetActive(true);
                }
            }
        }

        void RefreshSprites()
        {
            _head.sprite = SpriteFactory.OctopusHead(false);

            var segment = SpriteFactory.TentacleSegment();
            var tip = SpriteFactory.TentacleTip(false);

            for (int a = 0; a < _arms.Length; a++)
            {
                var arm = _arms[a];
                int last = arm.Renderers.Length - 1;
                for (int s = 0; s <= last; s++)
                    arm.Renderers[s].sprite = s == last ? tip : segment;
            }
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == BossPhase.Done) return;

            _tint.Tick(dt);

            switch (_phase)
            {
                case BossPhase.Enter: TickEnter(dt); break;
                case BossPhase.Fight: TickFight(dt); break;
                case BossPhase.Dying: TickDying(dt); break;
            }

            for (int a = 0; a < _arms.Length; a++)
            {
                TickArm(_arms[a], dt);
                DrawArm(_arms[a]);
            }
        }

        void TickEnter(float dt)
        {
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, _targetY, 1f - Mathf.Exp(-2.4f * dt));
            transform.position = p;
            if (Mathf.Abs(p.y - _targetY) < 0.06f) _phase = BossPhase.Fight;
        }

        void TickFight(float dt)
        {
            _sway += dt * _cfg.octopusSwaySpeed;
            // Голова живёт сама по себе: щупальца к ней не привязаны, поэтому её движение
            // ни на что не влияет и может быть спокойным.
            float amp = _field.PlayHalfWidth * _cfg.octopusSwayAmplitude;
            transform.position = new Vector3(Mathf.Sin(_sway) * amp, _targetY, 0f);

            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                LaunchVolley();
                _attackTimer = Mathf.Lerp(_cfg.octopusAttackIntervalMax, _cfg.octopusAttackIntervalMin, Rage)
                               * UnityEngine.Random.Range(0.85f, 1.15f);
            }
        }

        void TickDying(float dt)
        {
            _timer += dt;
            transform.position += Vector3.up * (0.4f * dt);

            float alpha = Mathf.Clamp01(1f - _timer / 1.2f);
            _head.color = new Color(1f, 1f, 1f, alpha);
            for (int a = 0; a < _arms.Length; a++)
            {
                _arms[a].Beam.enabled = false;
                for (int s = 0; s < _arms[a].Renderers.Length; s++)
                    _arms[a].Renderers[s].color = new Color(1f, 1f, 1f, alpha);
            }

            if (_timer >= 1.2f)
            {
                _phase = BossPhase.Done;
                Defeated?.Invoke(transform.position);
                Despawn();
            }
        }

        // -------------------------------------------------------------- атаки

        /// <summary>
        /// Серия: одно щупальце при полном здоровье, два в середине боя, три под конец.
        /// Задержка между ними больше перезарядки стража, иначе серию нечем отбить.
        /// </summary>
        void LaunchVolley()
        {
            int count = 1;
            if (Rage > 0.30f) count = 2;
            if (Rage > 0.62f) count = 3;
            count = Mathf.Min(count, _arms.Length);

            int launched = 0;
            // сначала пробуем набрать нужное число из свободных щупалец
            for (int attempt = 0; attempt < 24 && launched < count; attempt++)
            {
                var arm = _arms[UnityEngine.Random.Range(0, _arms.Length)];
                if (arm.Phase != ArmPhase.Idle || arm.Delay > 0f) continue;

                // не ноль: нулевая задержка не попала бы в проверку `Delay > 0` и первое
                // щупальце в серии просто не запустилось бы
                arm.Delay = 0.01f + launched * _cfg.octopusSerialDelay;
                launched++;
            }
        }

        void StartWindup(Arm arm)
        {
            arm.Phase = ArmPhase.Windup;
            arm.Timer = Mathf.Lerp(_cfg.octopusWindupMax, _cfg.octopusWindupMin, Rage);

            // Каждое щупальце бьёт со своей стороны периметра — так читается, какое именно
            // замахнулось, а вместе они всё равно покрывают все направления.
            float arc = _cfg.octopusStrikeArc * Mathf.Deg2Rad;
            float angle = arm.BaseAngle + UnityEngine.Random.Range(-arc, arc);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _strikeRange;
            Vector2 stage = _bubble.Center + offset;

            stage.x = Mathf.Clamp(stage.x, -_field.HalfWidth + 0.5f, _field.HalfWidth - 0.5f);
            stage.y = Mathf.Clamp(stage.y, _field.Bottom + 0.6f, _field.Top - 0.6f);
            arm.StagePoint = stage;

            // Изгибаем щупальце в ту сторону, где управляющая точка дальше от пузырька:
            // тогда оно заходит на позицию по широкой дуге вокруг игрока, а не протыкает
            // его насквозь по дороге. Особенно заметно, когда удар готовится снизу.
            Vector2 origin = ArmBase(arm);
            Vector2 span = stage - origin;
            if (span.sqrMagnitude > 0.0001f)
            {
                Vector2 perpendicular = new Vector2(-span.y, span.x).normalized;
                Vector2 middle = origin + span * 0.5f;
                float bend = span.magnitude * 0.42f;

                float outward = Vector2.Distance(middle + perpendicular * bend, _bubble.Center);
                float inward = Vector2.Distance(middle - perpendicular * bend, _bubble.Center);
                arm.CurlSide = outward >= inward ? 1f : -1f;
            }

            arm.Beam.enabled = true;
        }

        void TickArm(Arm arm, float dt)
        {
            if (_phase == BossPhase.Dying || _phase == BossPhase.Done)
            {
                arm.Tip = Vector2.Lerp(arm.Tip, IdleTip(arm), 1f - Mathf.Exp(-3f * dt));
                return;
            }

            if (arm.Delay > 0f)
            {
                arm.Delay -= dt;
                if (arm.Delay <= 0f && arm.Phase == ArmPhase.Idle) StartWindup(arm);
            }

            switch (arm.Phase)
            {
                case ArmPhase.Idle:
                    arm.Curl = Mathf.Lerp(arm.Curl, 1f, 1f - Mathf.Exp(-4f * dt));
                    arm.Tip = Vector2.Lerp(arm.Tip, IdleTip(arm), 1f - Mathf.Exp(-3.5f * dt));
                    break;

                case ArmPhase.Windup:
                {
                    // на замахе дуга остаётся выраженной — щупальце заходит сбоку
                    arm.Curl = Mathf.Lerp(arm.Curl, 0.78f, 1f - Mathf.Exp(-6f * dt));
                    arm.Tip = Vector2.Lerp(arm.Tip, arm.StagePoint, 1f - Mathf.Exp(-9f * dt));
                    UpdateBeam(arm, 1f - Mathf.Clamp01(arm.Timer / 0.4f));

                    arm.Timer -= dt;
                    if (arm.Timer <= 0f)
                    {
                        arm.Phase = ArmPhase.Strike;
                        arm.Timer = Mathf.Max(0.05f, _cfg.octopusStrikeTime);
                        arm.Beam.enabled = false;
                    }
                    break;
                }

                case ArmPhase.Strike:
                {
                    // кончик идёт по прямой в пузырёк, щупальце выпрямляется
                    arm.Curl = Mathf.Lerp(arm.Curl, 0f, 1f - Mathf.Exp(-14f * dt));
                    float speed = Vector2.Distance(arm.StagePoint, _bubble.Center)
                                  / Mathf.Max(0.05f, _cfg.octopusStrikeTime);
                    arm.Tip = Vector2.MoveTowards(arm.Tip, _bubble.Center, speed * dt);

                    if (ResolveStrike(arm)) break;

                    arm.Timer -= dt;
                    if (arm.Timer <= 0f)
                    {
                        arm.Phase = ArmPhase.Hold;
                        arm.Timer = _cfg.octopusHoldTime;
                    }
                    break;
                }

                case ArmPhase.Hold:
                    if (ResolveStrike(arm)) break;
                    arm.Timer -= dt;
                    if (arm.Timer <= 0f) StartRetract(arm);
                    break;

                case ArmPhase.Retract:
                    arm.Curl = Mathf.Lerp(arm.Curl, 1f, 1f - Mathf.Exp(-5f * dt));
                    arm.Tip = Vector2.Lerp(arm.Tip, IdleTip(arm), 1f - Mathf.Exp(-6f * dt));
                    arm.Timer -= dt;
                    if (arm.Timer <= 0f) arm.Phase = ArmPhase.Idle;
                    break;
            }
        }

        /// <summary>Проверка удара. true — щупальце отработало и ушло на отход.</summary>
        bool ResolveStrike(Arm arm)
        {
            float tipRadius = _cfg.octopusTipRadius;

            if (_guardian.CanDeflect &&
                Vector2.Distance(arm.Tip, _guardian.Center) < tipRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(arm.Tip);
                ApplyDamage(arm.Tip);
                StartRetract(arm);
                return true;
            }

            if (Vector2.Distance(arm.Tip, _bubble.Center) < tipRadius + _bubble.Radius)
            {
                BubbleHit?.Invoke(arm.Tip);
                StartRetract(arm);
                return true;
            }

            return false;
        }

        void StartRetract(Arm arm)
        {
            arm.Phase = ArmPhase.Retract;
            arm.Timer = _cfg.octopusRetractTime;
            arm.Beam.enabled = false;
        }

        void ApplyDamage(Vector2 at)
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(at);

            if (Hp <= 0)
            {
                _phase = BossPhase.Dying;
                _timer = 0f;
                for (int a = 0; a < _arms.Length; a++)
                {
                    _arms[a].Beam.enabled = false;
                    _arms[a].Delay = 0f;
                    _arms[a].Phase = ArmPhase.Idle;
                }
            }
        }

        // ------------------------------------------------------- отрисовка рук

        /// <summary>
        /// Основание щупальца — точка за краем экрана в его направлении. Игрок видит
        /// только уходящую за кадр руку, а насколько спрут велик на самом деле — не знает.
        /// </summary>
        Vector2 ArmBase(Arm arm)
        {
            Vector2 centre = new Vector2(0f, (_field.Top + _field.Bottom) * 0.5f);
            Vector2 dir = new Vector2(Mathf.Cos(arm.BaseAngle), Mathf.Sin(arm.BaseAngle));

            // сколько идти до края экрана именно в этом направлении
            float toSide = Mathf.Abs(dir.x) > 0.001f ? _field.HalfWidth / Mathf.Abs(dir.x) : 999f;
            float toCap = Mathf.Abs(dir.y) > 0.001f ? _field.HalfHeight / Mathf.Abs(dir.y) : 999f;
            float distance = Mathf.Min(toSide, toCap) + _cfg.octopusAnchorMargin;

            return centre + dir * distance;
        }

        /// <summary>В покое кончики смыкаются в кольцо вокруг пузырька и шевелятся.</summary>
        Vector2 IdleTip(Arm arm)
        {
            float wave = Mathf.Sin(Time.time * 1.4f + arm.IdlePhase);
            Vector2 dir = new Vector2(Mathf.Cos(arm.BaseAngle), Mathf.Sin(arm.BaseAngle));
            Vector2 side = new Vector2(-dir.y, dir.x);
            return _bubble.Center + dir * (_idleRing * arm.IdleReach + wave * 0.35f)
                   + side * (wave * 0.8f);
        }

        /// <summary>
        /// Щупальце — квадратичная кривая Безье. Управляющая точка отведена вбок
        /// пропорционально Curl: в покое дуга, на ударе почти прямая линия.
        /// </summary>
        void DrawArm(Arm arm)
        {
            Vector2 start = ArmBase(arm);
            Vector2 end = arm.Tip;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.01f) { delta = Vector2.down; length = 1f; }

            Vector2 perpendicular = new Vector2(-delta.y, delta.x) / length;
            float bend = length * 0.42f * arm.Curl * arm.CurlSide;
            Vector2 control = start + delta * 0.5f + perpendicular * bend;

            int count = arm.Segments.Length;
            // масштаб звена подгоняем под фактическую длину, чтобы не рвалось на растяжении
            float span = (Vector2.Distance(start, control) + Vector2.Distance(control, end)) * 0.5f
                         + length * 0.5f;
            float baseSize = Mathf.Clamp(span / count * 2.1f, _cfg.octopusArmWidth * 0.7f,
                                                              _cfg.octopusArmWidth * 1.9f);

            Vector2 previous = start;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                Vector2 point = Bezier(start, control, end, t);
                Vector2 forward = point - previous;
                if (forward.sqrMagnitude < 0.0001f) forward = delta;
                previous = point;

                var tr = arm.Segments[i];
                tr.position = point;
                tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
                tr.localScale = Vector3.one * (baseSize * Mathf.Lerp(1f, 0.38f, t));
            }
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        void UpdateBeam(Arm arm, float intensity)
        {
            Vector2 from = arm.StagePoint;
            Vector2 to = _bubble.Center;
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.05f) { arm.Beam.enabled = false; return; }

            arm.Beam.transform.position = from + delta * 0.5f;
            arm.Beam.transform.rotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            arm.Beam.transform.localScale = new Vector3(distance, 1.2f, 1f);
            arm.Beam.color = Palette.Coral.WithAlpha(Mathf.Lerp(0.30f, 0.90f, Mathf.Clamp01(intensity)));
        }

        /// <summary>Щупальца досчитают свой замах сами, снарядов спрут не оставляет.</summary>
        public void Interrupt() { }

        public void Despawn()
        {
            _phase = BossPhase.Done;
            if (_arms != null)
                for (int a = 0; a < _arms.Length; a++)
                    if (_arms[a].Beam != null) _arms[a].Beam.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
