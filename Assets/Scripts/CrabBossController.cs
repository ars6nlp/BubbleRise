using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Клешнерук» — пятый босс. Висит наверху и бьёт двумя клешнями по очереди.
    ///
    /// Его смысл в том, чтобы сломать выученную привычку «подставь стража и всё будет хорошо».
    ///
    /// ПОКА HP ВЫШЕ ПОЛОВИНЫ — клешня идёт **сжатой**. Всё как обычно: подставил стража,
    /// отбил, нанёс урон. Клешни чередуются.
    ///
    /// НИЖЕ ПОЛОВИНЫ — через раз он бьёт **раскрытой** клешнёй. Тут защита не работает
    /// в принципе: чтобы нанести урон, стража нужно **вывести навстречу клешне** за границу
    /// зоны атаки (её показывает золотое кольцо на линии удара) и попасть по осевой в мишень.
    ///
    /// Отсиживаешься у пузырька — клешня **схватит стража**: игрок теряет управление,
    /// вторая клешня спокойно бьёт по беззащитному пузырьку, и только после этого разжимает.
    ///
    /// Именно расстояние до пузырька и различает «защиту» и «атаку». По одному лишь
    /// боковому смещению они неразличимы: и подставленный, и целящийся страж одинаково
    /// стоят на оси клешни.
    ///
    /// То есть на второй половине боя защита и атака меняются местами, и делать это
    /// приходится через раз — по кругу.
    /// </summary>
    public class CrabBossController : MonoBehaviour, IBoss
    {
        enum BossPhase { Enter, Fight, Dying, Done }
        enum ClawPhase { Idle, Windup, Strike, Recoil, Return, Holding }

        class Claw
        {
            public Transform[] ArmSegments;
            public SpriteRenderer[] ArmRenderers;
            public Transform Root;
            public SpriteRenderer Renderer;

            public float Side;          // -1 левая, +1 правая
            public ClawPhase Phase;
            public float Timer;
            public bool Open;
            public bool Punish;         // добивающий удар по беззащитному пузырьку

            public SpriteRenderer Marker;   // куда выводить стража, чтобы ударить в мишень

            public Vector2 Position;
            public Vector2 Forward = Vector2.down;
            public Vector2 StagePoint;
            public float IdlePhase;
        }

        public string DisplayName => Loc.Get("boss.crab");
        public BossKind Kind => BossKind.Crab;
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

        SpriteRenderer _body;
        Claw[] _claws;

        BossPhase _phase = BossPhase.Done;
        float _timer;
        float _attackTimer;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();
        float _targetY;
        float _sway;

        int _nextClaw;
        bool _nextOpen;

        Claw _grabber;          // клешня, держащая стража
        float _grabTimer;

        // Размеры пересчитываются из долей игровой колонки на спавне: так краб
        // одинаково крупно читается и на портрете, и на широком экране.
        float _shellWidth;
        float _pincerSize;
        float _limbWidth;
        float _strikeRange;
        float _attackReach;

        float Rage => BossTuning.Rage(Hp, MaxHp);
        bool HalfDead => MaxHp > 0 && Hp <= MaxHp * 0.5f;

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            var bodyGo = new GameObject("Carapace");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = SpriteFactory.CrabBody(false);
            _body.sharedMaterial = SpriteFactory.Unlit;
            _body.sortingOrder = SortingOrders.Boss + 2;

            int segments = Mathf.Max(3, _cfg.crabArmSegments);
            _claws = new Claw[2];

            for (int i = 0; i < 2; i++)
            {
                var claw = new Claw
                {
                    Side = i == 0 ? -1f : 1f,
                    ArmSegments = new Transform[segments],
                    ArmRenderers = new SpriteRenderer[segments],
                    IdlePhase = i * 1.7f,
                    Phase = ClawPhase.Idle,
                };

                for (int s = 0; s < segments; s++)
                {
                    var go = new GameObject("Arm" + i + "_" + s);
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.CrabArmSegment();
                    sr.sharedMaterial = SpriteFactory.Unlit;
                    sr.sortingOrder = SortingOrders.Boss;
                    claw.ArmSegments[s] = go.transform;
                    claw.ArmRenderers[s] = sr;
                }

                var clawGo = new GameObject("Claw" + i);
                clawGo.transform.SetParent(transform, false);
                claw.Root = clawGo.transform;
                claw.Renderer = clawGo.AddComponent<SpriteRenderer>();
                claw.Renderer.sprite = SpriteFactory.CrabClaw(false, false);
                claw.Renderer.sharedMaterial = SpriteFactory.Unlit;
                claw.Renderer.sortingOrder = SortingOrders.Boss + 3;

                var markerGo = new GameObject("Marker" + i);
                markerGo.transform.SetParent(transform, false);
                claw.Marker = markerGo.AddComponent<SpriteRenderer>();
                claw.Marker.sprite = SpriteFactory.Ring();
                claw.Marker.sharedMaterial = SpriteFactory.Unlit;
                claw.Marker.sortingOrder = SortingOrders.BossAura;
                claw.Marker.enabled = false;

                _claws[i] = claw;
            }

            // Подмес при попадании — на панцирь и клешни. Суставы лежат на одном
            // порядке и перекрывают друг друга: два силуэта там сложились бы вдвое.
            _tint.Track(_body);
            for (int i = 0; i < _claws.Length; i++) _tint.Track(_claws[i].Renderer);

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);

            MaxHp = BossTuning.Hp(Mathf.Max(2, _cfg.crabHp));
            Hp = MaxHp;
            _phase = BossPhase.Enter;
            _timer = 0f;
            _tint.Reset();
            _sway = UnityEngine.Random.Range(0f, 10f);
            _targetY = _field.YFromTop(_cfg.crabHoverFactor);
            _attackTimer = 1.8f;

            float column = _field.PlayHalfWidth * 2f;
            _shellWidth = column * _cfg.crabShellSpan;
            _pincerSize = column * _cfg.crabPincerSpan;
            _limbWidth = column * _cfg.crabLimbSpan;
            _strikeRange = column * _cfg.crabStrikeSpan;
            _attackReach = column * _cfg.crabAttackReachSpan;
            _nextClaw = UnityEngine.Random.Range(0, 2);
            _nextOpen = true;
            _grabber = null;

            transform.position = new Vector3(0f, _field.Top + _shellWidth * 0.5f, 0f);
            _body.transform.localScale = Vector3.one * _shellWidth;
            _body.color = Color.white;

            RefreshSprites();

            for (int i = 0; i < _claws.Length; i++)
            {
                var claw = _claws[i];
                claw.Phase = ClawPhase.Idle;
                claw.Timer = 0f;
                claw.Open = false;
                claw.Punish = false;
                claw.Position = IdlePoint(claw);
                claw.Forward = Vector2.down;
                claw.Renderer.color = Color.white;
                claw.Marker.enabled = false;
                claw.Root.gameObject.SetActive(true);
                for (int s = 0; s < claw.ArmSegments.Length; s++)
                {
                    claw.ArmRenderers[s].color = Color.white;
                    claw.ArmSegments[s].gameObject.SetActive(true);
                }
            }
        }

        void RefreshSprites()
        {
            _body.sprite = SpriteFactory.CrabBody(false);
            var armSprite = SpriteFactory.CrabArmSegment();

            for (int i = 0; i < _claws.Length; i++)
            {
                var claw = _claws[i];
                claw.Renderer.sprite = SpriteFactory.CrabClaw(claw.Open, false);
                for (int s = 0; s < claw.ArmRenderers.Length; s++)
                    claw.ArmRenderers[s].sprite = armSprite;
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

            for (int i = 0; i < _claws.Length; i++)
            {
                TickClaw(_claws[i], dt);
                DrawClaw(_claws[i]);
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
            _sway += dt * _cfg.crabSwaySpeed;
            float amp = _field.PlayHalfWidth * _cfg.crabSwayAmplitude;
            transform.position = new Vector3(Mathf.Sin(_sway) * amp, _targetY, 0f);

            // пока держит стража — новых атак не назначаем, идёт добивание
            if (_grabber != null)
            {
                _grabTimer -= dt;
                _guardian.Pin(MouthPoint(_grabber));
                if (_grabTimer <= 0f) ReleaseGrab();
                return;
            }

            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                ScheduleAttack();
                _attackTimer = Mathf.Lerp(_cfg.crabAttackIntervalMax, _cfg.crabAttackIntervalMin, Rage)
                               * UnityEngine.Random.Range(0.85f, 1.15f);
            }
        }

        void TickDying(float dt)
        {
            _timer += dt;
            transform.position += Vector3.up * (0.35f * dt);

            float alpha = Mathf.Clamp01(1f - _timer / 1.2f);
            _body.color = new Color(1f, 1f, 1f, alpha);
            for (int i = 0; i < _claws.Length; i++)
            {
                _claws[i].Renderer.color = new Color(1f, 1f, 1f, alpha);
                for (int s = 0; s < _claws[i].ArmRenderers.Length; s++)
                    _claws[i].ArmRenderers[s].color = new Color(1f, 1f, 1f, alpha);
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
        /// Клешни чередуются всегда. Раскрытая появляется только во второй половине боя
        /// и тоже через раз — получается цикл «отбей — ударь в мишень — отбей — ...».
        /// </summary>
        void ScheduleAttack()
        {
            var claw = _claws[_nextClaw];
            _nextClaw = 1 - _nextClaw;
            if (claw.Phase != ClawPhase.Idle) return;

            bool open = HalfDead && _nextOpen;
            if (HalfDead) _nextOpen = !_nextOpen;

            StartWindup(claw, open, false);
        }

        void StartWindup(Claw claw, bool open, bool punish)
        {
            claw.Phase = ClawPhase.Windup;
            claw.Timer = punish
                ? _cfg.crabPunishWindup
                : Mathf.Lerp(_cfg.crabWindupMax, _cfg.crabWindupMin, Rage);
            claw.Open = open;
            claw.Punish = punish;
            claw.Renderer.sprite = SpriteFactory.CrabClaw(open, false);

            // Заход по дуге со своей стороны: левая клешня приходит слева, правая справа.
            // Центр дуги поднят вверх — краб висит сверху, и клешни падают сверху-сбоку.
            // Так у выпада хватает места на разгон даже на узком портретном экране.
            float centre = claw.Side > 0f ? _cfg.crabApproachAngle : 180f - _cfg.crabApproachAngle;
            float spread = _cfg.crabApproachSpread;
            float angle = (centre + UnityEngine.Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            Vector2 stage = _bubble.Center +
                            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _strikeRange;

            stage.x = Mathf.Clamp(stage.x, -_field.HalfWidth + 0.6f, _field.HalfWidth - 0.6f);
            stage.y = Mathf.Clamp(stage.y, _field.Bottom + 0.8f, _field.Top - 0.8f);
            claw.StagePoint = stage;

            // Раскрытая клешня требует выйти навстречу — показываем куда именно.
            // Без этой метки правило «выйди из-за пузырька» было бы неугадываемым.
            claw.Marker.enabled = open;
        }

        void TickClaw(Claw claw, float dt)
        {
            if (_phase == BossPhase.Dying || _phase == BossPhase.Done)
            {
                claw.Position = Vector2.Lerp(claw.Position, IdlePoint(claw), 1f - Mathf.Exp(-3f * dt));
                return;
            }

            switch (claw.Phase)
            {
                case ClawPhase.Idle:
                {
                    Vector2 idle = IdlePoint(claw);
                    claw.Position = Vector2.Lerp(claw.Position, idle, 1f - Mathf.Exp(-4f * dt));
                    FaceTowards(claw, idle + Vector2.down, dt, 6f);
                    break;
                }

                case ClawPhase.Windup:
                {
                    claw.Position = Vector2.Lerp(claw.Position, claw.StagePoint, 1f - Mathf.Exp(-8f * dt));
                    FaceTowards(claw, _bubble.Center, dt, 12f);
                    UpdateMarker(claw, 1f - Mathf.Clamp01(claw.Timer / 0.45f));

                    claw.Timer -= dt;
                    if (claw.Timer <= 0f)
                    {
                        claw.Phase = ClawPhase.Strike;
                        claw.Timer = Mathf.Max(0.05f, _cfg.crabLungeTime);
                    }
                    break;
                }

                case ClawPhase.Strike:
                {
                    // ось доворачиваем до движения, иначе проверка захвата считалась бы
                    // по прошлому кадру и на быстром ударе промахивалась
                    FaceTowards(claw, _bubble.Center, dt, 16f);

                    float speed = Vector2.Distance(claw.StagePoint, _bubble.Center)
                                  / Mathf.Max(0.05f, _cfg.crabLungeTime);
                    float distance = speed * dt;

                    // Клешня идёт под 10 юнитов/сек и за кадр перепрыгивала стража целиком —
                    // отсюда и были пропуски. Дробим шаг и проверяем на каждом подшаге.
                    int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
                    float step = distance / steps;

                    UpdateMarker(claw, 1f);

                    bool resolved = false;
                    for (int i = 0; i < steps; i++)
                    {
                        claw.Position = Vector2.MoveTowards(claw.Position, _bubble.Center, step);
                        if (ResolveStrike(claw)) { resolved = true; break; }
                    }
                    if (resolved) break;

                    claw.Timer -= dt;
                    if (claw.Timer <= 0f)
                    {
                        // последняя проверка в упор: страж мог оказаться ровно в точке остановки
                        if (!ResolveStrike(claw)) StartReturn(claw);
                    }
                    break;
                }

                case ClawPhase.Holding:
                    // положение задаётся в TickFight, клешня стоит там, где схватила
                    break;

                case ClawPhase.Recoil:
                {
                    Vector2 away = (claw.Position - _bubble.Center);
                    if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
                    claw.Position += away.normalized * (4.5f * dt);
                    claw.Timer -= dt;
                    if (claw.Timer <= 0f) StartReturn(claw);
                    break;
                }

                case ClawPhase.Return:
                {
                    claw.Position = Vector2.Lerp(claw.Position, IdlePoint(claw), 1f - Mathf.Exp(-6f * dt));
                    FaceTowards(claw, IdlePoint(claw) + Vector2.down, dt, 7f);
                    claw.Timer -= dt;
                    if (claw.Timer <= 0f)
                    {
                        claw.Phase = ClawPhase.Idle;
                        if (claw.Open)
                        {
                            claw.Open = false;
                            claw.Renderer.sprite = SpriteFactory.CrabClaw(false, false);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Кольцо на линии удара в той точке, куда нужно вывести стража: на границе
        /// зоны атаки. Пульсирует, чтобы читалось как «встань сюда», а не как декор.
        /// </summary>
        void UpdateMarker(Claw claw, float intensity)
        {
            if (!claw.Marker.enabled) return;

            Vector2 fromBubble = claw.StagePoint - _bubble.Center;
            if (fromBubble.sqrMagnitude < 0.0001f) fromBubble = Vector2.up;

            Vector2 spot = _bubble.Center + fromBubble.normalized * (_attackReach + _guardian.Radius * 0.5f);
            claw.Marker.transform.position = spot;

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f);
            claw.Marker.transform.localScale = Vector3.one * (_guardian.Radius * 2.6f * pulse);
            claw.Marker.color = Palette.Gold.WithAlpha(Mathf.Lerp(0.25f, 0.85f, Mathf.Clamp01(intensity)));
        }

        /// <summary>Точка «рта» клешни — центр мишени у раскрытой и остриё у сжатой.</summary>
        Vector2 MouthPoint(Claw claw) => claw.Position + claw.Forward * (_pincerSize * _cfg.crabMouthFraction);

        /// <summary>
        /// Разбор удара. Здесь и живёт главная механика: у раскрытой клешни есть
        /// уязвимая мишень и опасный корпус, и это две совершенно разные реакции.
        /// </summary>
        bool ResolveStrike(Claw claw)
        {
            Vector2 mouth = MouthPoint(claw);
            float clawRadius = _pincerSize * _cfg.crabClawRadius;

            if (claw.Open)
            {
                // Раскрытая клешня — это буква «V», и решает не расстояние, а то,
                // насколько страж отклонился от её осевой линии. Через расстояние
                // проверка не работала: мишень лежит впереди центра клешни, поэтому
                // по ходу удара всегда срабатывала первой, и захват был недостижим.
                Vector2 toGuardian = _guardian.Center - claw.Position;
                float along = Vector2.Dot(toGuardian, claw.Forward);
                Vector2 perpendicular = new Vector2(-claw.Forward.y, claw.Forward.x);
                float lateral = Mathf.Abs(Vector2.Dot(toGuardian, perpendicular));

                float reach = _pincerSize * _cfg.crabJawSpan;
                float jawHalf = _pincerSize * _cfg.crabJawSpread;

                // Страж попал в раскрытые клыки. Окно по оси захватывает и сустав позади
                // центра — иначе клешня «проглатывала» стража без реакции.
                if (along > -reach * 0.6f && along < reach && lateral < jawHalf + _guardian.Radius)
                {
                    // Ключевое различие. По одному лишь боковому смещению «закрыться» и
                    // «попасть в мишень» неразличимы: и то и другое означает «страж на оси
                    // клешни». Отличает их РАССТОЯНИЕ ДО ПУЗЫРЬКА — отсиживаешься рядом
                    // с ним или вышел навстречу клешне.
                    bool reachedOut = Vector2.Distance(_guardian.Center, _bubble.Center) > _attackReach;

                    // 1) вышел навстречу и попал по осевой — удар в мишень
                    if (reachedOut && lateral < _pincerSize * _cfg.crabTargetFraction)
                    {
                        if (!_guardian.CanDeflect) return false; // на перезарядке проносится мимо

                        _guardian.NotifyDeflect();
                        GuardianBlocked?.Invoke(mouth);
                        ApplyDamage(mouth);
                        if (_phase != BossPhase.Dying) StartRecoil(claw);
                        return true;
                    }

                    // 2) отсиживался у пузырька или промахнулся мимо осевой — клешня смыкается
                    StartGrab(claw);
                    return true;
                }
            }
            else
            {
                // сжатая клешня отбивается как обычная угроза
                if (_guardian.CanDeflect &&
                    Vector2.Distance(_guardian.Center, claw.Position) < clawRadius + _guardian.Radius)
                {
                    _guardian.NotifyDeflect();
                    GuardianBlocked?.Invoke(claw.Position);
                    ApplyDamage(claw.Position);
                    if (_phase != BossPhase.Dying) StartRecoil(claw);
                    return true;
                }
            }

            if (Vector2.Distance(mouth, _bubble.Center) < _cfg.crabTipRadius + _bubble.Radius)
            {
                BubbleHit?.Invoke(mouth);
                StartRecoil(claw);
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------- захват

        void StartGrab(Claw claw)
        {
            claw.Phase = ClawPhase.Holding;
            claw.Marker.enabled = false;
            claw.Open = false; // сомкнулась на страже
            claw.Renderer.sprite = SpriteFactory.CrabClaw(false, false);

            _grabber = claw;
            _guardian.Pin(MouthPoint(claw));

            // Вторая клешня немедленно идёт добивать беззащитный пузырёк — независимо
            // от того, чем была занята: иначе захват иногда проходил вообще без наказания.
            var other = claw == _claws[0] ? _claws[1] : _claws[0];
            StartWindup(other, false, true);

            _grabTimer = _cfg.crabPunishWindup + _cfg.crabLungeTime + 0.45f;
        }

        /// <summary>Отпускаем стража и разводим клешни: захват начался до смерти игрока.</summary>
        public void Interrupt()
        {
            ReleaseGrab();
            _guardian.Release();
        }

        void ReleaseGrab()
        {
            if (_grabber == null) return;
            _guardian.Release();
            StartRecoil(_grabber);
            _grabber = null;
        }

        void StartRecoil(Claw claw)
        {
            claw.Marker.enabled = false;
            if (_grabber == claw)
            {
                _guardian.Release();
                _grabber = null;
            }
            claw.Phase = ClawPhase.Recoil;
            claw.Timer = 0.22f;
            claw.Punish = false;
        }

        void StartReturn(Claw claw)
        {
            if (_grabber == claw) return; // держит стража — уходить нельзя
            claw.Marker.enabled = false;
            claw.Phase = ClawPhase.Return;
            claw.Timer = _cfg.crabReturnTime;
            claw.Punish = false;
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
                if (_grabber != null) { _guardian.Release(); _grabber = null; }
            }
        }

        // ---------------------------------------------------------- отрисовка

        Vector2 ShoulderPoint(Claw claw)
        {
            float half = _shellWidth * 0.5f;
            // руки выходят из-под края панциря, а не из его середины
            return (Vector2)transform.position + new Vector2(claw.Side * half * 0.78f, -half * 0.09f);
        }

        Vector2 IdlePoint(Claw claw)
        {
            float bob = Mathf.Sin(Time.time * 1.5f + claw.IdlePhase) * 0.18f;
            Vector2 point = ShoulderPoint(claw) + new Vector2(claw.Side * _pincerSize * 0.22f,
                                                             -_pincerSize * 0.52f + bob);

            // клешня огромная: на узком экране в покое она ушла бы за кадр целиком
            float limit = Mathf.Max(0.5f, _field.HalfWidth - _pincerSize * 0.25f);
            point.x = Mathf.Clamp(point.x, -limit, limit);
            return point;
        }

        void FaceTowards(Claw claw, Vector2 target, float dt, float rate)
        {
            Vector2 desired = target - claw.Position;
            if (desired.sqrMagnitude < 0.0001f) return;
            claw.Forward = Vector2.Lerp(claw.Forward, desired.normalized,
                                        1f - Mathf.Exp(-rate * dt)).normalized;
        }

        /// <summary>Рука — дуга Безье от плеча к клешне, локоть отведён наружу.</summary>
        void DrawClaw(Claw claw)
        {
            Vector2 shoulder = ShoulderPoint(claw);
            Vector2 end = claw.Position;
            Vector2 delta = end - shoulder;
            float length = delta.magnitude;
            if (length < 0.01f) { delta = Vector2.down; length = 1f; }

            Vector2 perpendicular = new Vector2(-delta.y, delta.x) / length;
            Vector2 control = shoulder + delta * 0.5f + perpendicular * (length * 0.30f * claw.Side);

            int count = claw.ArmSegments.Length;
            float segmentSize = Mathf.Clamp(length / count * 1.9f,
                                            _limbWidth * 0.7f, _limbWidth * 1.8f);

            Vector2 previous = shoulder;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (i + 0.5f) / count : 0.5f;
                Vector2 point = Bezier(shoulder, control, end, t);
                Vector2 forward = point - previous;
                if (forward.sqrMagnitude < 0.0001f) forward = delta;
                previous = point;

                var tr = claw.ArmSegments[i];
                tr.position = point;
                tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
                tr.localScale = Vector3.one * (segmentSize * Mathf.Lerp(1f, 0.72f, t));
            }

            claw.Root.position = claw.Position;
            claw.Root.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(claw.Forward.y, claw.Forward.x) * Mathf.Rad2Deg);
            claw.Root.localScale = Vector3.one * _pincerSize;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        public void Despawn()
        {
            _phase = BossPhase.Done;
            if (_grabber != null) { _guardian.Release(); _grabber = null; }
            if (_claws != null)
                for (int i = 0; i < _claws.Length; i++)
                    if (_claws[i].Marker != null) _claws[i].Marker.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
