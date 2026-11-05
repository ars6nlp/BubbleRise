using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Змей бездны» — второй босс. Снарядами не стреляет: свободно плавает по всему
    /// экрану и бьёт телом. Два этапа.
    ///
    /// **Первый этап (HP от 100 до 50 %).** ПЛАВАНИЕ -> ЗАХОД В КРУГ -> КРУГ -> ЗАМАХ
    /// -> РЫВОК -> ВОЗВРАТ. Во время замаха загорается пунктирный луч — окно реакции.
    /// Успел подставить стража на линию рывка — змей получает урон и отлетает.
    /// Сторона замаха берётся равномерно по кругу, кроме сектора под пузырьком, где
    /// полоса HP закрывала морду (C-09), а плавание между рывками короче прежнего.
    ///
    /// **Переход на 50 %.** Змей уходит к верхней кромке и полторы-две секунды
    /// меняется у всех на глазах: тело темнеет, по звеньям бежит тусклый
    /// красно-оранжевый ореол, шипы удлиняются, на хвосте вырастает костяное лезвие,
    /// гудит низкий звук. Атак и урона в это время нет.
    ///
    /// **Второй этап (HP от 50 % до нуля).** Чередует удар хвостом и кольцо, раз в
    /// три-четыре атаки — прежний рывок головой. Пузырёк игрок не ведёт (он стоит
    /// по центру), поэтому защита от каждой атаки — страж:
    ///
    /// * **Хвост.** По полу размаха 0.8 с горят пунктирная дуга и тень, затем хвост
    ///   за 0.5 с хлещет по этой дуге — она проходит через пузырёк. Страж на дуге
    ///   принимает удар, хвост отскакивает.
    /// * **Кольцо.** Тело ложится кругом вокруг стража с проёмом не уже 2.2 его
    ///   диаметров и сжимается 1.8 с. Пунктир кольца и подсвеченный проём видны с
    ///   первой секунды. Вывел стража через проём — кольцо смыкается впустую. Не
    ///   вывел — страж зажат, а голова бьёт пузырёк по пунктирному лучу.
    ///
    /// После каждой атаки змей на 0.8 с замирает, голова в мягком контуре: в это
    /// окно её можно ударить стражем — тем же блоком, что и на первом этапе. Паузы
    /// подобраны так, что неуязвимым он не бывает дольше 4 с подряд.
    ///
    /// Звенья лежат на порядках 5–9 — ниже пузырька и стража (C-10).
    /// </summary>
    public class SerpentBossController : MonoBehaviour, IBoss
    {
        enum Phase
        {
            Enter, Roam, Coil, Circle, Windup, Lunge, Recover,
            Retreat, Shift,
            TailAim, TailSwing,
            RingForm, RingSqueeze, RingStrike,
            Stunned,
            Dying, Done
        }

        /// <summary>Звуки, которые просит змей. Воспроизводит GameManager.</summary>
        public enum Noise { Shift, Whoosh, Coil }

        class Segment
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public SpriteRenderer Next;   // второй облик: проявляется поверх на переходе
            public SpriteRenderer Glow;   // тусклый ореол перехода
            public Vector2 Pos;      // положение в цепочке, с ним считаются связи
            public Vector2 Render;   // положение на экране: цепочка плюс волна
            public float Size;
            public float BaseSize;
            public int Variant;
            public bool IsTail;
        }

        public string DisplayName => Loc.Get("boss.serpent");
        public BossKind Kind => BossKind.Serpent;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public Vector2 Center => _segments != null && _segments.Length > 0 ? _segments[0].Pos : (Vector2)transform.position;

        /// <summary>Второй этап уже начался.</summary>
        public bool Stage2 => _stage2;

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;
        /// <summary>Змей просит звук: что и где.</summary>
        public event Action<Noise, Vector2> Noised;

        // ---------------------------------------------------- константы вида

        /// <summary>Порядок головы и тела: ниже пузырька (10) и стража (20), C-10.</summary>
        const int HeadOrder = SortingOrders.Boss + 4;   // 9
        const int BodyOrder = SortingOrders.Boss + 2;   // 7
        /// <summary>Шаг глубины между звеньями: голова ближе всех, у каждого звена своё место.</summary>
        const float ZStep = 0.02f;
        /// <summary>Пунктир телеграфа: проявление и угасание, с.</summary>
        const float TeleRise = 0.2f, TeleFall = 0.35f;
        /// <summary>Контур уязвимой головы: проявление и угасание, с.</summary>
        const float HaloRise = 0.15f, HaloFall = 0.3f;
        /// <summary>Телеграф раньше этого не читается — бот-проверка ждёт столько же.</summary>
        const float Reaction = 0.5f;
        const int DashCount = 24;
        const int ShadeCount = 5;
        const int GapCount = 3;
        /// <summary>Хвост второго этапа крупнее: на нём лезвие.</summary>
        const float TailStage2Size = 0.95f;
        /// <summary>Полуширина горба ореола перехода в долях тела.</summary>
        const float ShiftWaveHalf = 0.3f;
        /// <summary>Центр петли перехода — на столько ниже верхней кромки, юнитов.</summary>
        const float ShiftLoopDepth = 2.3f;
        /// <summary>Волна по телу — не чаще стольких колебаний в секунду.</summary>
        const float WaveHzMax = 0.7f;
        /// <summary>Радиус маятника хвоста, юнитов; на узком экране — по ширине.</summary>
        const float TailArcRadius = 2.4f;
        /// <summary>Сколько градусов дуги до пузырька и после него.</summary>
        const float TailArcLead = 90f, TailArcFollow = 40f;
        /// <summary>Проём кольца не ближе стольких градусов к направлению на пузырёк.</summary>
        const float GapBubbleClear = 50f;

        static readonly Color ShiftGlow = Palette.Hex("B4502A");   // тусклый красно-оранжевый, сумма 1.19
        static readonly Color ShadeColor = Palette.Hex("05080E");

        // ------------------------------------------------------------- поля

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;

        Segment[] _segments;
        SpriteRenderer _aimBeam;
        SpriteRenderer[] _dash;
        SpriteRenderer[] _shade;
        SpriteRenderer[] _gap;
        SpriteRenderer _halo;

        Phase _phase = Phase.Done;
        float _timer;
        float _phaseTime;     // длительность текущей фазы
        float _phaseClock;    // сколько она уже идёт
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
        float _circleTime, _windupTime;
        bool _recoverToStun;
        bool _ringStrike;

        // второй этап
        bool _stage2;
        bool _shiftPending;
        int _attacksDone;
        int _lungeEvery = 3;
        bool _nextTail = true;
        Phase _queued = Phase.TailAim;
        bool _kinematic;      // тело ставится явно (хвост, кольцо, замирание), а не тянется за головой

        // хвост
        Vector2 _pivot;
        float _arcRadius, _arcFrom, _arcTo, _arcSide;
        Vector2 _headAnchor;
        bool _tailHitBubble;

        // кольцо
        Vector2 _ringCenter;
        float _ringGapAngle;
        float _ringGapWidth;
        float _ringR;
        float _ringGapHalf;
        float _ringBlend;

        // телеграфы
        enum Tele { None, Arc, Ring }
        Tele _tele;
        float _teleWant, _teleShown;
        float _haloWant, _haloShown;

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
                // Голова поверх тела. Звенья тела — на одном порядке, а по глубине
                // каждое своё: ближние к голове ложатся сверху, объём не теряется.
                sr.sortingOrder = i == 0 ? HeadOrder : BodyOrder;

                bool isTail = i == count - 1;
                float t = i / (float)(count - 1);
                float size = i == 0
                    ? _cfg.serpentHeadSize
                    : _cfg.serpentHeadSize * Mathf.Lerp(0.86f, 0.42f, t);

                var next = NewChild(go.transform, "Stage2", sr.sortingOrder, -0.004f);
                var glow = NewChild(go.transform, "ShiftGlow", SortingOrders.Boss + 1, 0f);
                glow.sprite = SpriteFactory.SoftDot();
                glow.transform.localScale = Vector3.one * 1.7f;

                _segments[i] = new Segment
                {
                    Tr = go.transform,
                    Sr = sr,
                    Next = next,
                    Glow = glow,
                    Size = size,
                    BaseSize = size,
                    Variant = i % 2,
                    IsTail = isTail,
                };
                // Звенья тела на одном порядке, но каждое на своей глубине: силуэт
                // подмеса ложится сразу на своё звено и уходит под следующее.
                _tint.Track(sr);
            }

            var beamGo = new GameObject("AimBeam");
            beamGo.transform.SetParent(transform, false);
            _aimBeam = beamGo.AddComponent<SpriteRenderer>();
            _aimBeam.sprite = SpriteFactory.AimBeam();
            _aimBeam.sharedMaterial = SpriteFactory.Unlit;
            _aimBeam.sortingOrder = SortingOrders.BossAura;
            _aimBeam.enabled = false;

            // Телеграфы второго этапа — один пул на хвост и кольцо: одновременно
            // они не бывают.
            _dash = new SpriteRenderer[DashCount];
            for (int i = 0; i < DashCount; i++)
                _dash[i] = NewChild(transform, "TeleDash" + i, SortingOrders.BossAura, 0f, SpriteFactory.TelegraphDash());
            _shade = new SpriteRenderer[ShadeCount];
            for (int i = 0; i < ShadeCount; i++)
                _shade[i] = NewChild(transform, "TeleShade" + i, SortingOrders.BossAura - 1, 0f, SpriteFactory.SoftDot());
            _gap = new SpriteRenderer[GapCount];
            for (int i = 0; i < GapCount; i++)
                _gap[i] = NewChild(transform, "RingGap" + i, SortingOrders.BossAura, 0f, SpriteFactory.SoftDot());
            _halo = NewChild(transform, "HeadHalo", HeadOrder - 1, 0f, SpriteFactory.Ring());

            gameObject.SetActive(false);
        }

        static SpriteRenderer NewChild(Transform parent, string childName, int order, float z, Sprite sprite = null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            sr.sprite = sprite;
            sr.enabled = false;
            return sr;
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

            _stage2 = false;
            _shiftPending = false;
            _attacksDone = 0;
            _lungeEvery = RollLungeEvery();
            _nextTail = true;
            _kinematic = false;
            _recoverToStun = false;
            _ringStrike = false;
            _tele = Tele.None;
            _teleWant = _teleShown = 0f;
            _haloWant = _haloShown = 0f;
            HideTelegraphs();

            // вползает сверху единой стопкой, тело расправится само за счёт связей
            Vector2 start = new Vector2(UnityEngine.Random.Range(-1f, 1f), _field.Top + 2f);
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                s.Pos = start + Vector2.up * (i * _cfg.serpentSegmentSpacing);
                s.Render = s.Pos;
                s.Size = s.BaseSize;
                ApplySprite(s, i);
                s.Sr.color = Color.white;
                s.Next.enabled = false;
                s.Glow.enabled = false;
                s.Tr.position = new Vector3(s.Pos.x, s.Pos.y, i * ZStep);
                s.Tr.localScale = Vector3.one * s.Size;
                s.Tr.gameObject.SetActive(true);
            }

            _prevHeadPos = _segments[0].Pos;
            _waypoint = _segments[0].Pos;
        }

        int RollLungeEvery() => UnityEngine.Random.Range(
            Mathf.Max(2, _cfg.serpentLungeEveryMin), Mathf.Max(_cfg.serpentLungeEveryMin, _cfg.serpentLungeEveryMax) + 1);

        Sprite SpriteFor(Segment segment, int index, int stage)
        {
            if (index == 0) return SpriteFactory.SerpentHead(stage, false);
            if (segment.IsTail) return SpriteFactory.SerpentTail(stage, false);
            return SpriteFactory.SerpentBody(segment.Variant, stage, false);
        }

        void ApplySprite(Segment segment, int index)
            => segment.Sr.sprite = SpriteFor(segment, index, _stage2 ? 2 : 1);

        // --------------------------------------------------------------- цикл

        /// <summary>0 при полном здоровье, 1 при последнем — от неё зависят все тайминги.</summary>
        float Rage => BossTuning.Rage(Hp, MaxHp);

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _tint.Tick(dt);
            _phaseClock += dt;

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Roam: TickRoam(dt); break;
                case Phase.Coil: TickCoil(dt); break;
                case Phase.Circle: TickCircle(dt); break;
                case Phase.Windup: TickWindup(dt); break;
                case Phase.Lunge: TickLunge(dt); break;
                case Phase.Recover: TickRecover(dt); break;
                case Phase.Retreat: TickRetreat(dt); break;
                case Phase.Shift: TickShift(dt); break;
                case Phase.TailAim: TickTailAim(dt); break;
                case Phase.TailSwing: TickTailSwing(dt); break;
                case Phase.RingForm: TickRingForm(dt); break;
                case Phase.RingSqueeze: TickRingSqueeze(dt); break;
                case Phase.RingStrike: TickRingStrike(dt); break;
                case Phase.Stunned: TickStunned(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            if (_phase == Phase.Done) return;
            UpdateSwim(dt);
            UpdateBody(dt);
            TickTelegraphs(dt);
        }

        void SetPhase(Phase phase, float time)
        {
            _phase = phase;
            _phaseTime = Mathf.Max(0.01f, time);
            _timer = time;
            _phaseClock = 0f;
        }

        // ------------------------------------------------------------ рулёжка

        float RoamHalfWidth => Mathf.Lerp(_field.PlayHalfWidth, _field.HalfWidth, 0.3f)
                               * Mathf.Max(0.1f, _cfg.serpentRoamSpread);
        float RoamTop => _field.Top - 1.1f;
        float RoamBottom => _field.Bottom + 1.1f;

        /// <summary>Ниже этой линии голова на замахе не опускается: там полоса HP.</summary>
        float ArenaBottom => _field.Bottom + 1.6f;

        Vector2 ClampToArena(Vector2 p, float margin)
            => new Vector2(Mathf.Clamp(p.x, -_field.HalfWidth + margin, _field.HalfWidth - margin),
                           Mathf.Clamp(p.y, ArenaBottom, _field.Top - margin));

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
            if (_kinematic) wanted = 0.2f;   // поза задана атакой — волна почти стихает
            _swimAmount = Mathf.Lerp(_swimAmount, wanted, 1f - Mathf.Exp(-7f * dt));
            // Волна медленная и тяжёлая: не чаще WaveHzMax колебаний в секунду.
            // Прежде на быстром ходу хвост вилял 2.5 раза в секунду, и светлое
            // костяное лезвие мелькало в одной точке экрана 6 раз в секунду. [ТЕСТ]
            float rate = _cfg.serpentWaveSpeed * (0.4f + _swimAmount);
            _swimPhase += dt * Mathf.Min(rate, Mathf.PI * 2f * WaveHzMax);
        }

        Vector2 OrbitPoint(float extraRadius = 0f)
        {
            float wobble = Mathf.Sin(Time.time * 2.1f) * 0.22f;
            float radius = _cfg.serpentOrbitRadius + extraRadius + wobble;
            Vector2 p = _bubble.Center + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * radius;
            // На узком экране точка орбиты сбоку уходила за край, снизу — под полосу
            // HP. Морда на замахе обязана быть видна, поэтому прижимаем её к арене.
            return ClampToArena(p, 0.45f);
        }

        void TickEnter(float dt)
        {
            SwimTo(OrbitPoint(), _cfg.serpentRoamMoveSpeed * 1.4f, _cfg.serpentTurnRate, dt);
            if (Vector2.Distance(_segments[0].Pos, _bubble.Center) < _cfg.serpentOrbitRadius + 0.8f) StartRoam();
        }

        // ---------------------------------------------------------- плавание

        void StartRoam(float time = -1f)
        {
            SetPhase(Phase.Roam, time >= 0f ? time : Mathf.Lerp(_cfg.serpentRoamTimeMax, _cfg.serpentRoamTimeMin, Rage));
            _kinematic = false;
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
            if (_timer > 0f) return;

            if (!_stage2) StartCoil();
            else BeginQueued();
        }

        // ------------------------------------------------------- заход в круг

        /// <summary>
        /// Угол, с которого будет замах: равномерно по кругу, кроме сектора
        /// 240–300° под пузырьком. Прежде угол замаха был там, где оказалась голова,
        /// и рывки шли снизу в 73 % случаев, а морда на замахе в половине случаев
        /// пряталась под полосой HP (C-09).
        /// </summary>
        static float PickStrikeAngle()
        {
            float deg = UnityEngine.Random.Range(0f, 300f);
            if (deg >= 240f) deg += 60f;
            return deg * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Резкое стягивание на орбиту: смена темпа — сигнал, что дальше будет атака.
        /// Начальный угол подобран так, чтобы к концу круга и замаха голова пришла на
        /// выбранный угол замаха.
        /// </summary>
        void StartCoil()
        {
            SetPhase(Phase.Coil, _cfg.serpentCoilTime);
            _kinematic = false;
            if (UnityEngine.Random.value < 0.35f) _orbitDirection = -_orbitDirection;

            _circleTime = Mathf.Lerp(_cfg.serpentCircleTimeMax, _cfg.serpentCircleTimeMin, Rage);
            _windupTime = Mathf.Lerp(_cfg.serpentWindupMax, _cfg.serpentWindupMin, Rage);

            float sweep = _cfg.serpentOrbitSpeed * (2.2f * _cfg.serpentCoilTime
                                                    + (1f + Rage * 0.5f) * _circleTime
                                                    + 0.35f * _windupTime);
            _orbitAngle = PickStrikeAngle() - _orbitDirection * sweep;
        }

        /// <summary>Скорость хода по орбите: ровно такая, чтобы поспевать за точкой.</summary>
        float OrbitSpeed(float multiplier) =>
            Mathf.Max(2f, _cfg.serpentOrbitRadius * _cfg.serpentOrbitSpeed * multiplier);

        void TickCoil(float dt)
        {
            _orbitAngle += _cfg.serpentOrbitSpeed * 2.2f * _orbitDirection * dt;
            SwimTo(OrbitPoint(), OrbitSpeed(3f), _cfg.serpentTurnRate * 1.6f, dt);

            _timer -= dt;
            if (_timer <= 0f) SetPhase(Phase.Circle, _circleTime);
        }

        void TickCircle(float dt)
        {
            _orbitAngle += _cfg.serpentOrbitSpeed * (1f + Rage * 0.5f) * _orbitDirection * dt;
            SwimTo(OrbitPoint(), OrbitSpeed(1.9f + Rage * 0.5f), _cfg.serpentTurnRate * 1.4f, dt);

            _timer -= dt;
            if (_timer <= 0f)
            {
                SetPhase(Phase.Windup, _windupTime);
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

            if (_timer <= 0f) BeginLunge();
        }

        void BeginLunge()
        {
            Vector2 toBubble = _bubble.Center - _segments[0].Pos;
            _lungeDirection = toBubble.sqrMagnitude > 0.0001f ? toBubble.normalized : Vector2.down;
            _heading = _lungeDirection;
            _lungeTravel = 0f;
            SetPhase(Phase.Lunge, 1f);
            _kinematic = false;
            _aimBeam.enabled = false;
        }

        void TickLunge(float dt)
        {
            float speed = Mathf.Lerp(_cfg.serpentLungeSpeedMin, _cfg.serpentLungeSpeedMax, Rage);
            float step = speed * dt;

            var head = _segments[0];
            head.Pos += _lungeDirection * step;
            _lungeTravel += step;

            // 1) страж принял удар (зажатый в кольце — не может)
            if (_guardian.CanDeflect &&
                Vector2.Distance(head.Pos, _guardian.Center) < _cfg.serpentHeadRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(head.Pos);
                ApplyDamage();
                _recoverToStun = false;
                EndLunge();
                return;
            }

            // 2) достал пузырёк
            if (Vector2.Distance(head.Pos, _bubble.Center) < _cfg.serpentHeadRadius + _bubble.Radius)
            {
                BubbleHit?.Invoke(head.Pos);
                _recoverToStun = _stage2;
                EndLunge();
                return;
            }

            // 3) промахнулся и ушёл слишком далеко
            if (_lungeTravel > _cfg.serpentOrbitRadius * 2.2f)
            {
                _recoverToStun = _stage2;
                EndLunge();
            }
        }

        void EndLunge()
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;
            if (_ringStrike)
            {
                // Удар из кольца: страж отпущен. Голова сначала отходит, потом
                // замирает: замри она у пузырька, отпущенный страж задел бы её
                // сам, и удар по змею достался бы даром. [ТЕСТ]
                _ringStrike = false;
                _guardian.Release();
                _recoverToStun = true;
            }
            StartRecover(-_lungeDirection);
        }

        void StartRecover(Vector2 pushDirection)
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;

            SetPhase(Phase.Recover, _cfg.serpentRecoverTime);
            _kinematic = false;
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
            if (_timer > 0f) return;

            if (_shiftPending) StartRetreat();
            else if (!_stage2) StartRoam();
            else if (_recoverToStun) StartStunned();
            else ScheduleStage2();
        }

        // ------------------------------------------------------------ переход

        void StartRetreat()
        {
            _shiftPending = false;
            SetPhase(Phase.Retreat, 1.6f);
            _kinematic = false;
            _aimBeam.enabled = false;
            _teleWant = 0f;
            _haloWant = 0f;
            _waypoint = new Vector2(UnityEngine.Random.Range(-0.6f, 0.6f), _field.Top + 0.4f);
        }

        /// <summary>Уходит к верхней кромке: голова за краем, тело ещё на экране.</summary>
        void TickRetreat(float dt)
        {
            SwimTo(_waypoint, _cfg.serpentRoamMoveSpeed * 1.5f, _cfg.serpentTurnRate * 1.3f, dt);
            _timer -= dt;
            if (_segments[0].Pos.y > _field.Top - 0.3f || _timer <= 0f) StartShift();
        }

        void StartShift()
        {
            SetPhase(Phase.Shift, _cfg.serpentShiftTime);
            _kinematic = false;
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                s.Next.sprite = SpriteFor(s, i, 2);
                s.Next.color = Color.white.WithAlpha(0f);
                s.Next.enabled = true;
                s.Glow.color = ShiftGlow.WithAlpha(0f);
                s.Glow.enabled = true;
            }
            Noised?.Invoke(Noise.Shift, _segments[0].Pos);
        }

        /// <summary>
        /// «Он меняется». Голова возвращается из-за кромки и медленно описывает
        /// петлю в верхней части экрана — тело на виду, перемену видно целиком.
        /// Всё плавное: второй облик проявляется поверх первого, первый темнеет и
        /// уходит под него, по звеньям один раз пробегает тусклый ореол.
        /// </summary>
        void TickShift(float dt)
        {
            _timer -= dt;
            float k = 1f - Mathf.Clamp01(_timer / _phaseTime);

            // Петля высоко над пузырьком: самая нижняя точка на ShiftLoopDepth + радиус
            // от верха, до пузырька больше трёх юнитов.
            float ang = Mathf.PI * 0.5f + _orbitDirection * k * Mathf.PI * 1.5f;
            float radius = Mathf.Min(1.2f, _field.PlayHalfWidth - 0.7f);
            Vector2 loop = new Vector2(0f, _field.Top - ShiftLoopDepth)
                           + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            SwimTo(loop, _cfg.serpentRoamMoveSpeed * 0.7f, _cfg.serpentTurnRate, dt);

            ApplyShiftLook(k);
            if (_timer <= 0f) FinishShift();
        }

        void ApplyShiftLook(float k)
        {
            float fadeNew = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, k));
            float fadeOld = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, k));
            float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, k));
            // ореол: один проход от головы к хвосту за весь переход. Два прохода
            // давали каждому звену цикл 0.9 с — чаще правила «не быстрее раза в
            // 1.5 с». Ширина горба — звено светлеет и гаснет по 0.32 с.
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.1f, k))
                             * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, k)));
            float wavePos = k * 1.7f - 0.35f;

            int n = _segments.Length;
            for (int i = 0; i < n; i++)
            {
                var s = _segments[i];
                float along = i / (float)(n - 1);
                float bump = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(wavePos - along) / ShiftWaveHalf));

                Color old = Color.Lerp(Color.white, new Color(0.8f, 0.76f, 0.78f, 1f), dark);
                old.a = fadeOld;
                s.Sr.color = old;
                s.Next.color = Color.white.WithAlpha(fadeNew);
                s.Glow.color = ShiftGlow.WithAlpha(0.3f * bump * envelope);

                if (s.IsTail) s.Size = Mathf.Lerp(s.BaseSize, _cfg.serpentHeadSize * TailStage2Size, fadeNew);
            }
        }

        void FinishShift()
        {
            _stage2 = true;
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                ApplySprite(s, i);
                s.Sr.color = Color.white;
                s.Next.enabled = false;
                s.Glow.enabled = false;
                if (s.IsTail) s.Size = _cfg.serpentHeadSize * TailStage2Size;
            }
            _attacksDone = 0;
            _lungeEvery = RollLungeEvery();
            _nextTail = true;
            _queued = Phase.TailAim;
            StartRoam(0.9f);
        }

        // ---------------------------------------------------- второй этап

        /// <summary>
        /// Следующая атака второго этапа и пауза перед ней. Хвост и кольцо
        /// чередуются, раз в 3–4 атаки — рывок головой. Перед кольцом пауза короче:
        /// пауза плюс кольцо не дольше 4 с без окна уязвимости.
        /// </summary>
        void ScheduleStage2()
        {
            _attacksDone++;
            if (_attacksDone >= _lungeEvery)
            {
                _queued = Phase.Coil;
                _attacksDone = 0;
                _lungeEvery = RollLungeEvery();
            }
            else
            {
                _queued = _nextTail ? Phase.TailAim : Phase.RingForm;
                _nextTail = !_nextTail;
            }

            float max = _queued == Phase.TailAim ? _cfg.serpentStage2PauseMax : _cfg.serpentRingPauseMax;
            StartRoam(UnityEngine.Random.Range(_cfg.serpentStage2PauseMin, Mathf.Max(_cfg.serpentStage2PauseMin, max)));
        }

        void BeginQueued()
        {
            switch (_queued)
            {
                case Phase.TailAim: StartTailAim(); break;
                case Phase.RingForm: StartRingForm(); break;
                default: StartCoil(); break;
            }
        }

        // ---------------------------------------------------------- хвост

        Vector2 ArcPoint(float angle) => _pivot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _arcRadius;

        /// <summary>
        /// Удар хвостом — маятник. Центр размаха над пузырьком, хвост заходит с
        /// борта напротив головы и проходит пузырёк поперёк. Голова держится у своего
        /// борта: змей не переплывает экран ради замаха.
        ///
        /// Прежде центр стоял у борта и дуга шла сверху вниз — через то место над
        /// пузырьком, где страж стоит, когда его не трогают. Брошенный страж отбивал
        /// хвост сам, и атака была не опасной (тот же изъян, что C-01 у медузы).
        /// Теперь место стража внутри дуги, на 0.95 ближе к центру размаха: лезвие
        /// проходит мимо, и хвост надо встретить самому.
        /// </summary>
        void StartTailAim()
        {
            SetPhase(Phase.TailAim, _cfg.serpentTailTelegraph);
            _kinematic = true;
            _aimBeam.enabled = false;

            float side = _segments[0].Pos.x >= 0f ? 1f : -1f;
            _arcSide = side;
            _arcRadius = Mathf.Min(TailArcRadius, _field.PlayHalfWidth - 0.25f);
            _pivot = _bubble.Center + Vector2.up * _arcRadius;
            float phi = -90f * Mathf.Deg2Rad;
            _arcFrom = phi - side * TailArcLead * Mathf.Deg2Rad;
            _arcTo = phi + side * TailArcFollow * Mathf.Deg2Rad;
            _headAnchor = new Vector2(side * (_field.HalfWidth - 0.55f),
                                      Mathf.Min(_field.Top - 0.8f, _bubble.Center.y + 3.0f));
            _tailHitBubble = false;
            _tele = Tele.Arc;
            _teleWant = 1f;
        }

        void TickTailAim(float dt)
        {
            _timer -= dt;
            var tail = _segments[_segments.Length - 1];
            // хвост поднимается в начало дуги, голова уходит к якорю у борта
            PoseFromTail(Vector2.MoveTowards(tail.Pos, ArcPoint(_arcFrom), 14f * dt), _headAnchor, dt);

            if (_timer <= 0f)
            {
                SetPhase(Phase.TailSwing, _cfg.serpentTailSwingTime);
                Noised?.Invoke(Noise.Whoosh, tail.Pos);
            }
        }

        void TickTailSwing(float dt)
        {
            float kPrev = 1f - Mathf.Clamp01(_timer / _phaseTime);
            _timer -= dt;
            float k = 1f - Mathf.Clamp01(_timer / _phaseTime);

            var tail = _segments[_segments.Length - 1];
            Vector2 from = ArcPoint(Mathf.Lerp(_arcFrom, _arcTo, Mathf.SmoothStep(0f, 1f, kPrev)));
            Vector2 to = ArcPoint(Mathf.Lerp(_arcFrom, _arcTo, Mathf.SmoothStep(0f, 1f, k)));

            // Дробим шаг: на пике размаха лезвие проходит стража за один кадр.
            float length = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.08f));
            for (int i = 1; i <= steps; i++)
            {
                Vector2 tip = Vector2.Lerp(from, to, i / (float)steps);

                if (_guardian.CanDeflect &&
                    Vector2.Distance(tip, _guardian.Center) < _cfg.serpentTailBladeRadius + _guardian.Radius)
                {
                    // Лезвие встретило стража: удар отбит, хвост отскакивает.
                    _guardian.NotifyDeflect();
                    GuardianBlocked?.Invoke(tip);
                    PoseFromTail(tip, _headAnchor, dt);
                    StartStunned();
                    return;
                }

                if (!_tailHitBubble &&
                    Vector2.Distance(tip, _bubble.Center) < _cfg.serpentTailBladeRadius + _bubble.Radius)
                {
                    _tailHitBubble = true;
                    BubbleHit?.Invoke(tip);
                    if (_phase == Phase.Done) return;
                }
            }

            PoseFromTail(to, _headAnchor, dt);
            if (_timer <= 0f) StartStunned();
        }

        /// <summary>
        /// Поза «ведёт хвост»: кончик стоит в заданной точке, голова тянется к
        /// якорю, длина звеньев сохраняется (два прохода FABRIK). Последний проход —
        /// от хвоста: точность удара важнее точности у головы.
        /// </summary>
        void PoseFromTail(Vector2 tip, Vector2 headTarget, float dt)
        {
            int n = _segments.Length;
            float spacing = _cfg.serpentSegmentSpacing;
            var head = _segments[0];
            Vector2 headPos = Vector2.Lerp(head.Pos, headTarget, 1f - Mathf.Exp(-4f * dt));

            for (int it = 0; it < 2; it++)
            {
                _segments[n - 1].Pos = tip;
                for (int i = n - 2; i >= 0; i--) Constrain(i, i + 1, spacing);
                _segments[0].Pos = headPos;
                for (int i = 1; i < n; i++) Constrain(i, i - 1, spacing);
            }
            _segments[n - 1].Pos = tip;
            for (int i = n - 2; i >= 0; i--) Constrain(i, i + 1, spacing);

            Vector2 face = _segments[0].Pos - _segments[1].Pos;
            if (face.sqrMagnitude > 0.0001f) _heading = face.normalized;
        }

        void Constrain(int i, int anchor, float spacing)
        {
            Vector2 d = _segments[i].Pos - _segments[anchor].Pos;
            if (d.sqrMagnitude < 0.000001f) d = Vector2.up;
            _segments[i].Pos = _segments[anchor].Pos + d.normalized * spacing;
        }

        // ---------------------------------------------------------- кольцо

        float GuardianDiameter => _guardian != null ? _guardian.Radius * 2f : 0.6f;

        static float GapHalf(float radius, float width)
            => Mathf.Asin(Mathf.Clamp01(width * 0.5f / Mathf.Max(radius, 0.01f)));

        /// <summary>Угол места звена i на кольце: голова у одного края проёма, хвост у другого.</summary>
        float SlotAngle(int i, float gapHalf)
        {
            float t = i / (float)(_segments.Length - 1);
            return _ringGapAngle + gapHalf + (Mathf.PI * 2f - gapHalf * 2f) * t;
        }

        Vector2 RingPoint(float angle, float radius)
            => _ringCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        /// <summary>
        /// Кольцо вокруг стража. Центр — там, где страж сейчас, но так, чтобы кольцо
        /// и проём поместились на экране; проём смотрит туда, где за ним есть место.
        /// </summary>
        void StartRingForm()
        {
            SetPhase(Phase.RingForm, _cfg.serpentRingFormTime);
            _kinematic = false;
            _aimBeam.enabled = false;

            float r0 = _cfg.serpentRingRadius;
            float mx = Mathf.Max(0.05f, _field.HalfWidth - r0 - 0.45f);
            Vector2 g = _guardian.Center;
            _ringCenter = new Vector2(Mathf.Clamp(g.x, -mx, mx),
                                      Mathf.Clamp(g.y, ArenaBottom + r0, _field.Top - r0 - 0.5f));
            _ringR = r0;
            _ringGapWidth = GuardianDiameter * Mathf.Max(2.2f, _cfg.serpentRingGapDiameters);
            _ringGapHalf = GapHalf(r0, _ringGapWidth);
            _ringGapAngle = PickGapAngle(r0);
            _ringBlend = 0f;
            _tele = Tele.Ring;
            _teleWant = 1f;
            Noised?.Invoke(Noise.Coil, _ringCenter);
        }

        float PickGapAngle(float radius)
        {
            // Пузырёк обычно внутри кольца (страж стоит над ним). Проём в его
            // сторону заставил бы вести стража через пузырёк — такие углы
            // берутся, только если других нет.
            Vector2 toBubble = _bubble.Center - _ringCenter;
            bool bubbleNear = toBubble.magnitude < radius + 0.9f;
            float bubbleDeg = Mathf.Atan2(toBubble.y, toBubble.x) * Mathf.Rad2Deg;

            float best = 0f, bestRoom = -1f;
            for (int i = 0; i < 10; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                Vector2 outside = RingPoint(a, radius + 0.9f);
                float room = Mathf.Min(_field.HalfWidth - Mathf.Abs(outside.x),
                                       Mathf.Min(_field.Top - outside.y, outside.y - ArenaBottom));
                if (bubbleNear && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, bubbleDeg)) < GapBubbleClear)
                    room -= 10f;
                if (room > 0.25f) return a;
                if (room > bestRoom) { bestRoom = room; best = a; }
            }
            return best;
        }

        /// <summary>Голова обходит круг, тело ложится следом: укладка видна, а не возникает.</summary>
        void TickRingForm(float dt)
        {
            _timer -= dt;
            float k = 1f - Mathf.Clamp01(_timer / _phaseTime);
            // голова идёт от хвостового края проёма к своему, против счёта мест
            float angle = Mathf.Lerp(_ringGapAngle - _ringGapHalf + Mathf.PI * 2f,
                                     _ringGapAngle + _ringGapHalf, Mathf.SmoothStep(0f, 1f, k));
            SwimTo(RingPoint(angle, _ringR), 15f, 900f, dt);
            UpdateRingConfine(_ringR, _ringGapHalf);

            if (_timer <= 0f)
            {
                SetPhase(Phase.RingSqueeze, _cfg.serpentRingSqueezeTime);
                _kinematic = true;
                _ringBlend = 0f;
            }
        }

        void TickRingSqueeze(float dt)
        {
            _timer -= dt;
            float k = 1f - Mathf.Clamp01(_timer / _phaseTime);

            _ringR = Mathf.Lerp(_cfg.serpentRingRadius, _cfg.serpentRingEndRadius, Mathf.SmoothStep(0f, 1f, k));
            // Проём держит ширину до последних 14 % — и только тогда смыкается.
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, k));
            _ringGapHalf = GapHalf(_ringR, _ringGapWidth * (1f - close));

            _ringBlend = Mathf.MoveTowards(_ringBlend, 1f, dt / 0.25f);
            float follow = _ringBlend >= 1f ? 1f : 1f - Mathf.Exp(-14f * dt);
            for (int i = 0; i < _segments.Length; i++)
            {
                float wobble = Mathf.Sin(_swimPhase * 0.6f + i * 0.9f) * 0.04f;
                Vector2 slot = RingPoint(SlotAngle(i, _ringGapHalf), _ringR + wobble);
                _segments[i].Pos = Vector2.Lerp(_segments[i].Pos, slot, follow);
            }
            Vector2 face = _segments[0].Pos - _segments[1].Pos;
            if (face.sqrMagnitude > 0.0001f) _heading = face.normalized;

            UpdateRingConfine(_ringR, _ringGapHalf);
            if (_timer <= 0f) ResolveRing();
        }

        /// <summary>Тело — стенка для стража: изнутри наружу только через проём.</summary>
        void UpdateRingConfine(float radius, float gapHalf)
        {
            float wall = Mathf.Max(0.05f, radius - 0.22f);
            _guardian.SetConfine(_ringCenter, wall, _ringGapAngle, gapHalf);
        }

        void ResolveRing()
        {
            bool caught = _guardian.ConfinedInside &&
                          Vector2.Distance(_guardian.Center, _ringCenter) < _cfg.serpentRingEndRadius + 0.3f;
            _guardian.ClearConfine();
            _teleWant = 0f;

            if (!caught)
            {
                // Кольцо сомкнулось впустую — окно для удара по голове.
                StartStunned();
                return;
            }

            // Страж зажат в кольце, голова бьёт пузырёк по пунктирному лучу.
            _guardian.Pin(_ringCenter);
            _ringStrike = true;
            SetPhase(Phase.RingStrike, 0.6f);
            _aimBeam.enabled = true;
        }

        void TickRingStrike(float dt)
        {
            _timer -= dt;
            _guardian.Pin(_ringCenter);
            TurnTowards(_bubble.Center - _segments[0].Pos, _cfg.serpentTurnRate * 2f, dt);
            UpdateAimBeam(1f - Mathf.Clamp01(_timer / 0.35f));
            if (_timer <= 0f) BeginLunge();
        }

        // ---------------------------------------------------- окно уязвимости

        /// <summary>
        /// После атаки змей замирает, голова в мягком контуре. Страж, коснувшийся
        /// головы в это окно, бьёт её — тот же блок, что и на первом этапе.
        /// </summary>
        void StartStunned()
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;
            SetPhase(Phase.Stunned, _cfg.serpentStunTime);
            _kinematic = true;
            _aimBeam.enabled = false;
            _teleWant = 0f;
            _haloWant = 1f;
        }

        void TickStunned(float dt)
        {
            _timer -= dt;
            var head = _segments[0];

            if (_guardian.CanDeflect &&
                Vector2.Distance(head.Pos, _guardian.Center) < _cfg.serpentHeadRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(head.Pos);
                _haloWant = 0f;
                ApplyDamage();
                if (_phase == Phase.Dying) return;
                Vector2 away = head.Pos - _guardian.Center;
                _recoverToStun = false;
                StartRecover(away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up);
                return;
            }

            if (_timer <= 0f)
            {
                _haloWant = 0f;
                ScheduleStage2();
            }
        }

        // ------------------------------------------------------------- смерть

        void TickDying(float dt)
        {
            _timer += dt;

            // агония: голова мечется, тело тянется следом
            var head = _segments[0];
            head.Pos += new Vector2(Mathf.Sin(_timer * 9f), Mathf.Cos(_timer * 7f)) * (2.0f * dt);
            head.Pos += Vector2.up * (0.5f * dt);

            const float fade = 1.6f;
            float alpha = Mathf.Clamp01(1f - _timer / fade);
            for (int i = 0; i < _segments.Length; i++)
            {
                var col = _segments[i].Sr.color;
                col.a = alpha;
                _segments[i].Sr.color = col;
            }

            if (_timer >= fade)
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
                StartDying();
                return;
            }

            if (!_stage2 && !_shiftPending &&
                Hp <= Mathf.CeilToInt(MaxHp * Mathf.Clamp01(_cfg.serpentStage2At)))
                _shiftPending = true;
        }

        void StartDying()
        {
            ReleaseGuardian();
            _phase = Phase.Dying;
            _timer = 0f;
            _kinematic = false;
            _aimBeam.enabled = false;
            _teleWant = 0f;
            _haloWant = 0f;
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].Next.enabled = false;
                _segments[i].Glow.enabled = false;
            }
        }

        void ReleaseGuardian()
        {
            if (_guardian == null) return;
            _guardian.ClearConfine();
            if (_ringStrike || _phase == Phase.RingStrike) _guardian.Release();
            _ringStrike = false;
        }

        // --------------------------------------------------------------- тело

        /// <summary>
        /// Дистанционные связи: каждое звено подтягивается к предыдущему на фиксированное
        /// расстояние. Это и даёт «змеиность» — тело запаздывает и хлещет на рывке.
        /// Поверх связей бежит волна: она влияет только на рисунок, в сами связи не
        /// подмешивается, иначе изгиб копился бы от звена к звену и змей сворачивался
        /// бы в спираль. Когда позу задаёт атака (хвост, кольцо), связи не тянут.
        /// </summary>
        void UpdateBody(float dt)
        {
            float spacing = _cfg.serpentSegmentSpacing;
            var head = _segments[0];

            if (!_kinematic)
            {
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
            }

            float amplitude = _cfg.serpentWaveAmplitude * Mathf.Min(1.4f, _swimAmount) * (_kinematic ? 0.35f : 1f);
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
            ApplyTransform(head, 0, head.Render - _segments[1].Render);
            for (int i = 1; i < _segments.Length; i++)
                ApplyTransform(_segments[i], i, _segments[i - 1].Render - _segments[i].Render);
        }

        static void ApplyTransform(Segment segment, int index, Vector2 forward)
        {
            segment.Tr.position = new Vector3(segment.Render.x, segment.Render.y, index * ZStep);
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

            _aimBeam.enabled = true;
            _aimBeam.transform.position = from + delta * 0.5f;
            _aimBeam.transform.rotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // спрайт луча в мире 1 x 0.125 юнита при масштабе 1
            _aimBeam.transform.localScale = new Vector3(distance, 1.4f, 1f);

            float alpha = Mathf.Lerp(0.35f, 0.95f, Mathf.Clamp01(intensity));
            _aimBeam.color = Palette.Coral.WithAlpha(alpha);
        }

        // ---------------------------------------------------------- телеграфы

        /// <summary>
        /// Пунктир дуги или кольца, тень у пузырька, подсветка проёма и контур
        /// уязвимой головы. Всё проявляется за 0.15–0.2 с и гаснет за 0.3–0.35 с:
        /// включений за кадр нет. Опасное — коралловое (пунктир), безопасное —
        /// бирюзовое (проём), уязвимое — бирюзовый контур головы.
        /// </summary>
        void TickTelegraphs(float dt)
        {
            _teleShown = Mathf.MoveTowards(_teleShown, _teleWant,
                                           dt / (_teleWant > _teleShown ? TeleRise : TeleFall));
            _haloShown = Mathf.MoveTowards(_haloShown, _haloWant,
                                           dt / (_haloWant > _haloShown ? HaloRise : HaloFall));

            float a = _teleShown;
            int used = 0, shades = 0, gaps = 0;

            if (a > 0.004f && _tele == Tele.Arc)
            {
                // пунктир по дуге размаха
                float span = Mathf.Abs(_arcTo - _arcFrom) * _arcRadius;
                int n = Mathf.Clamp(Mathf.RoundToInt(span / 0.3f), 4, DashCount);
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    float ang = Mathf.Lerp(_arcFrom, _arcTo, t);
                    PlaceDash(used++, ArcPoint(ang), ang + Mathf.PI * 0.5f, Palette.Coral.WithAlpha(0.5f * a));
                }
                // тень по полу размаха у пузырька — где лезвие пройдёт ближе всего
                Vector2 toBubble = _bubble.Center - _pivot;
                float phi = Mathf.Atan2(toBubble.y, toBubble.x);
                for (int i = 0; i < ShadeCount; i++)
                {
                    float t = i / (float)(ShadeCount - 1) - 0.5f;
                    var sr = _shade[shades++];
                    sr.enabled = true;
                    sr.transform.position = new Vector3(ArcPoint(phi - _arcSide * t * 0.7f).x,
                                                        ArcPoint(phi - _arcSide * t * 0.7f).y, 0f);
                    sr.transform.localScale = Vector3.one * 0.9f;
                    sr.color = ShadeColor.WithAlpha(0.28f * a);
                }
            }
            else if (a > 0.004f && _tele == Tele.Ring)
            {
                float body = Mathf.PI * 2f - _ringGapHalf * 2f;
                float span = body * _ringR;
                int n = Mathf.Clamp(Mathf.RoundToInt(span / 0.3f), 6, DashCount);
                for (int i = 0; i < n; i++)
                {
                    float ang = _ringGapAngle + _ringGapHalf + body * ((i + 0.5f) / n);
                    PlaceDash(used++, RingPoint(ang, _ringR - 0.28f), ang + Mathf.PI * 0.5f,
                              Palette.Coral.WithAlpha(0.42f * a));
                }
                // проём: три тусклые бирюзовые точки поперёк выхода — сюда выводить стража
                for (int i = 0; i < GapCount; i++)
                {
                    float t = GapCount > 1 ? i / (GapCount - 1f) * 2f - 1f : 0f;
                    var sr = _gap[gaps++];
                    sr.enabled = true;
                    Vector2 at = RingPoint(_ringGapAngle + t * _ringGapHalf * 0.6f, _ringR);
                    sr.transform.position = new Vector3(at.x, at.y, 0f);
                    sr.transform.localScale = Vector3.one * 0.3f;
                    sr.color = Palette.Aqua.WithAlpha(0.3f * a);
                }
            }

            for (int i = used; i < DashCount; i++) if (_dash[i].enabled) _dash[i].enabled = false;
            for (int i = shades; i < ShadeCount; i++) if (_shade[i].enabled) _shade[i].enabled = false;
            for (int i = gaps; i < GapCount; i++) if (_gap[i].enabled) _gap[i].enabled = false;
            if (a <= 0.004f && _teleWant <= 0f) _tele = Tele.None;

            // контур уязвимой головы
            bool halo = _haloShown > 0.004f;
            _halo.enabled = halo;
            if (halo)
            {
                var head = _segments[0];
                _halo.transform.position = new Vector3(head.Render.x, head.Render.y, -0.01f);
                _halo.transform.localScale = Vector3.one * head.Size * 1.45f;
                _halo.color = Palette.Aqua.WithAlpha(0.42f * _haloShown);
            }
        }

        void PlaceDash(int index, Vector2 at, float angle, Color color)
        {
            if (index >= DashCount) return;
            var sr = _dash[index];
            sr.enabled = true;
            sr.transform.position = new Vector3(at.x, at.y, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            sr.color = color;
        }

        void HideTelegraphs()
        {
            if (_dash != null) foreach (var d in _dash) d.enabled = false;
            if (_shade != null) foreach (var d in _shade) d.enabled = false;
            if (_gap != null) foreach (var d in _gap) d.enabled = false;
            if (_halo != null) _halo.enabled = false;
        }

        // ------------------------------------------------------------ сервис

        /// <summary>
        /// Змей бросает начатую атаку. Вызывается при возрождении игрока: атака,
        /// начатая до его смерти, к нему уже не относится. Переход не прерывается.
        /// </summary>
        public void Interrupt()
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;

            ReleaseGuardian();
            _aimBeam.enabled = false;
            _lungeTravel = 0f;
            _teleWant = 0f;
            _haloWant = 0f;
            _kinematic = false;
            if (_phase == Phase.Retreat || _phase == Phase.Shift) return;
            if (_stage2) ScheduleStage2();
            else StartRoam();
        }

        public void Despawn()
        {
            ReleaseGuardian();
            _phase = Phase.Done;
            if (_aimBeam != null) _aimBeam.enabled = false;
            HideTelegraphs();
            gameObject.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Куда поставил бы стража игрок с реакцией 0.5 с от начала телеграфа.
        /// Нужно проверке проходимости в Unity: бот ведёт стража сюда так же, как
        /// палец, — с той же скоростью следования, без телепорта.
        /// </summary>
        public bool DevBotTarget(out Vector2 target)
        {
            target = default;
            if (_segments == null || _bubble == null) return false;
            Vector2 b = _bubble.Center;
            var head = _segments[0];

            switch (_phase)
            {
                case Phase.Windup:
                    target = b + (head.Pos - b).normalized * 1.0f;
                    return _phaseClock >= Mathf.Min(Reaction, _phaseTime * 0.6f);
                case Phase.Lunge:
                    if (_ringStrike) return false;
                    target = b - _lungeDirection * 1.0f;
                    return true;
                case Phase.TailAim:
                case Phase.TailSwing:
                {
                    if (_phase == Phase.TailAim && _phaseClock < Reaction) return false;
                    Vector2 toBubble = b - _pivot;
                    float phi = Mathf.Atan2(toBubble.y, toBubble.x);
                    target = ArcPoint(phi - _arcSide * 28f * Mathf.Deg2Rad);
                    return true;
                }
                case Phase.RingForm:
                case Phase.RingSqueeze:
                    if (_phase == Phase.RingForm && _phaseClock < Reaction) return false;
                    target = RingPoint(_ringGapAngle, _ringR + 1.0f);
                    return true;
                case Phase.Stunned:
                    if (_phaseClock < 0.25f) return false;
                    target = head.Pos;
                    return true;
            }
            return false;
        }

        /// <summary>Отладка: сразу второй этап (без перехода) или сразу переход.</summary>
        public void DevGoStage2(bool withShift)
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;
            Hp = Mathf.Min(Hp, Mathf.CeilToInt(MaxHp * Mathf.Clamp01(_cfg.serpentStage2At)));
            if (withShift) { StartRetreat(); return; }
            _shiftPending = false;
            FinishShift();
        }

        /// <summary>Отладка: следующая атака второго этапа — эта.</summary>
        public void DevQueue(int attack)
        {
            _queued = attack == 0 ? Phase.TailAim : attack == 1 ? Phase.RingForm : Phase.Coil;
            if (_phase == Phase.Roam) _timer = Mathf.Min(_timer, 0.3f);
        }

        public string DevState => _phase + (_stage2 ? " (этап 2)" : " (этап 1)");
#endif
    }
}
