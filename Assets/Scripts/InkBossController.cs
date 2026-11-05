using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Чернильный морок» — каракатица, которая бьёт не по пузырьку, а по обзору игрока.
    ///
    /// Цикл: ЗАМАХ -> ПЛЕВОК В КАМЕРУ -> ТЕМНОТА -> ПОИСК -> УДАР -> ПЕРЕДЫШКА -> ЗАМАХ.
    /// Удар чередуется: струя по линии стрелок или три чернильных шара.
    ///
    /// Ключевой такт — разделение темноты и поиска. Пока экран залит, искать
    /// нечего: стража в этот момент и зашвыривает в угол. Потом чернила сходят — и
    /// начинается собственно поиск, уже под тикающие часы. Проверяемый навык —
    /// **ориентация в потерянной картинке**: страж не там, где его оставили, а
    /// через пару секунд прилетит удар по линии, которую надо успеть перекрыть.
    ///
    /// **Вид.** Спрут, как прежний морок (до 29.10): гладкая голова-купол светлее
    /// воды (контраст не ниже 2.2:1), два крупных бледных глаза на её середине под
    /// хмурыми складками — левый крупнее, правый прищурен веком; зрачки —
    /// горизонтальные щели, сужающиеся к концам. Клюв — тёмный крюк без зубов.
    /// Восемь щупалец пучком из-под головы одной линией, концы загнуты наружу,
    /// идут волной с запаздыванием. Глаза разгораются за 0.35 с и следят за игроком.
    ///
    /// **Темнота.** Чернила нарастают за inkRiseTime, потолок затемнения —
    /// inkBlackoutAlpha (0.97: почти чёрный экран), сходят за inkFadeTime — оба
    /// фронта по SmoothStep. Пузырёк и страж обведены мягким светлым контуром
    /// поверх чернил (inkOutlineAlpha), интерфейс — поверх всего. В темноте тускло
    /// светят глаза морока: они медленно смещаются туда, откуда придёт удар.
    /// Новых угроз в темноте нет: рыбы с замаха не появляются, а живые уплывают
    /// (C-13, GameManager).
    ///
    /// **Три шара.** Каждый второй удар — три шара по расходящимся путям. Путь,
    /// ведущий в пузырёк, отмечен на поиске коралловыми точками, ложные — пустыми
    /// фиолетовыми кольцами. Перекрыть надо настоящий; ложные лопаются на капли.
    /// </summary>
    public class InkBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Charge, Spit, Blackout, Search, Strike, Balls, Recover, Dying, Done }

        /// <summary>Звуки морока. Воспроизводит GameManager.</summary>
        public enum Noise { Charge, Balls }

        const int ChevronCount = 12;
        const int ArmCount = 8;
        const int ArmLinks = 2;
        const int EyeCount = 2;
        const int BallCount = 3;
        const int LaneMarks = 3;

        /// <summary>Разгорание и угасание глаз, с. Не быстрее 0.3.</summary>
        const float EyeRise = 0.35f;
        /// <summary>Пути шаров расходятся на этот угол, градусы.</summary>
        const float LaneSpread = 28f;
        /// <summary>В темноте морок дрейфует медленнее: глаза-точки читаются.</summary>
        const float DarkDriftScale = 0.55f;
        /// <summary>Волна по стрелкам струи: один проход за столько секунд.</summary>
        const float ChevronWavePeriod = 1.6f;
        /// <summary>Смерть: расползание чернил и всплывающие пузыри, с.</summary>
        const float DeathTime = 1.8f;
        const float Reaction = 0.5f;
        // Глаза шире, чем выше (как у прежнего морока, 1.5:1), чуть повёрнуты —
        // каждый по-своему: лёгкая асимметрия вместо зеркального лица.
        static readonly float[] EyeStretch = { 0.2f, 0.18f };
        static readonly float[] EyeTilt = { -6f, 9f };

        public string DisplayName => Loc.Get("boss.ink");
        public BossKind Kind => BossKind.Ink;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        /// <summary>Чернила накрыли экран — момент для звука.</summary>
        public event Action InkSplashed;
        /// <summary>Морок просит звук: что и где.</summary>
        public event Action<Noise, Vector2> Noised;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;
        FxManager _fx;

        InkScreen _ink;
        Transform _look;           // носитель вида: масштаб замаха и покачивание
        SpriteRenderer _mantle;
        SpriteRenderer _beak;
        SpriteRenderer _lid;
        SpriteRenderer[] _iris;
        SpriteRenderer[] _pupil;
        SpriteRenderer[] _arm;
        SpriteRenderer[] _darkEyes; // глаза, светящие поверх чернил
        SpriteRenderer _bubbleOutline, _guardianOutline;   // контуры поверх чернил
        SpriteRenderer _blob;      // сгусток в полёте к камере
        SpriteRenderer _jet;       // струя, которой он бьёт по линии
        SpriteRenderer[] _chevrons;
        SpriteRenderer[] _balls;
        SpriteRenderer[] _lane;

        Phase _phase = Phase.Done;
        float _timer;
        float _phaseLength;
        float _phaseClock;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();
        float _pulse;

        Vector2 _position;
        float _targetX;
        int _lastCorner = -1;
        int _strikes;
        bool _ballsNext;

        Vector2 _jetPosition;
        Vector2 _jetDirection;
        bool _jetHitBubble;

        readonly Vector2[] _ballPos = new Vector2[BallCount];
        readonly Vector2[] _ballDir = new Vector2[BallCount];
        readonly float[] _ballTravel = new float[BallCount];
        readonly float[] _ballPop = new float[BallCount];   // 1 — летит; падает к нулю, пока лопается
        bool _ballsHitBubble;
        float _laneShown;
        Vector2 _laneAim = Vector2.down;

        // вид
        float _glow, _glowWant;       // свет глаз
        float _squint, _squintWant;   // сужение зрачков
        float _reach, _reachWant;     // щупальца тянутся к цели
        Vector2 _gaze;                // взгляд, в долях радиуса радужки
        float _armPhase;
        float _bodyAlpha = 1f;
        float _darkShown;
        float _dripClock;

        /// <summary>Насколько плотно залит экран прямо сейчас. Нужно снаружи: пока
        /// обзора нет, новых рыб выпускать нечестно.</summary>
        public float InkCoverage => _ink != null ? _ink.Coverage : 0f;

        /// <summary>
        /// С замаха и до конца темноты рыбы не появляются (C-13): та, что выплыла на
        /// замахе, добиралась до пузырька уже в темноте.
        /// </summary>
        public bool HoldsFish => _phase == Phase.Charge || _phase == Phase.Spit || _phase == Phase.Blackout;

        float Rage => BossTuning.Rage(Hp, MaxHp);

        float BodySize => _field.PlayHalfWidth * 2f * _cfg.inkBodySpan;

        /// <summary>Клюв — из него летят и сгусток, и струя, и шары.</summary>
        Vector2 Muzzle => _position + Vector2.down * (BodySize * 0.12f);

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian, FxManager fx)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;
            _fx = fx;

            var lookGo = new GameObject("Look");
            lookGo.transform.SetParent(transform, false);
            _look = lookGo.transform;

            // щупальца — под мантией
            _arm = new SpriteRenderer[ArmCount * ArmLinks];
            for (int i = 0; i < _arm.Length; i++)
                _arm[i] = NewRenderer("Arm" + i, SortingOrders.Boss - 1, _look, SpriteFactory.InkArm(i % ArmLinks));

            _mantle = NewRenderer("Mantle", SortingOrders.Boss, _look, SpriteFactory.InkMantle(false));
            _tint.Track(_mantle);

            // клюв — под головой, между корнями щупалец
            _beak = NewRenderer("Beak", SortingOrders.Boss + 1, _look, SpriteFactory.InkBeak());
            _beak.transform.localPosition = new Vector3(0f, -0.085f, -0.01f);
            _beak.transform.localScale = new Vector3(0.13f, 0.13f, 1f);

            _iris = new SpriteRenderer[EyeCount];
            _pupil = new SpriteRenderer[EyeCount];
            for (int i = 0; i < EyeCount; i++)
            {
                var spot = SpriteFactory.InkEyeSpots[i];
                _iris[i] = NewRenderer("Eye" + i, SortingOrders.Boss + 1, _look, SpriteFactory.InkEyeIris(0));
                _iris[i].transform.localPosition = new Vector3(spot.x * 0.5f, spot.y * 0.5f, -0.01f);
                // глаза неправильные: каждый вытянут и повёрнут по-своему
                float stretch = EyeStretch[i];
                _iris[i].transform.localScale = new Vector3(spot.z * 1.05f * (1f + stretch), spot.z * 1.05f * (1f - stretch), 1f);
                _iris[i].transform.localRotation = Quaternion.Euler(0f, 0f, EyeTilt[i]);
                _pupil[i] = NewRenderer("Pupil" + i, SortingOrders.Boss + 2, _iris[i].transform, SpriteFactory.InkPupil());
                _pupil[i].transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            }
            // тяжёлое веко на правом глазу: прищур
            _lid = NewRenderer("Lid", SortingOrders.Boss + 3, _iris[1].transform, SpriteFactory.InkLid());
            _lid.transform.localScale = Vector3.one * 1.08f;
            _lid.transform.localPosition = new Vector3(0f, 0.12f, -0.01f);

            _blob = NewRenderer("InkBlob", SortingOrders.Projectile, transform, SpriteFactory.InkBlob());
            _blob.enabled = false;

            _jet = NewRenderer("InkJet", SortingOrders.Projectile, transform, SpriteFactory.InkBlob());
            _jet.enabled = false;

            // Стрелки, отметки путей и глаза в темноте живут поверх чернил — иначе
            // их не было бы видно ровно тогда, когда они и нужны. Страж в этот слой
            // не попадает намеренно: подсвеченный, он не оставил бы никакого поиска.
            var chevron = SpriteFactory.AimChevron();
            _chevrons = new SpriteRenderer[ChevronCount];
            for (int i = 0; i < ChevronCount; i++)
            {
                _chevrons[i] = NewRenderer("AimChevron" + i, SortingOrders.InkMarker, transform, chevron);
                _chevrons[i].enabled = false;
            }

            _balls = new SpriteRenderer[BallCount];
            for (int i = 0; i < BallCount; i++)
            {
                _balls[i] = NewRenderer("InkBall" + i, SortingOrders.Projectile, transform, SpriteFactory.InkBall());
                _balls[i].enabled = false;
            }
            _lane = new SpriteRenderer[BallCount * LaneMarks];
            for (int i = 0; i < _lane.Length; i++)
            {
                bool danger = i / LaneMarks == 1;
                _lane[i] = NewRenderer("Lane" + i, SortingOrders.InkMarker, transform,
                                       danger ? SpriteFactory.SoftDot() : SpriteFactory.Ring());
                _lane[i].enabled = false;
            }

            _darkEyes = new SpriteRenderer[EyeCount];
            for (int i = 0; i < EyeCount; i++)
            {
                _darkEyes[i] = NewRenderer("DarkEye" + i, SortingOrders.InkMarker, transform, SpriteFactory.SoftDot());
                _darkEyes[i].enabled = false;
            }

            // Контуры пузырька и стража — поверх чернил, под отметками атак: играть
            // в темноте можно, а подсказки направления остаются главными.
            _bubbleOutline = NewRenderer("BubbleOutline", SortingOrders.Ink + 3, transform, SpriteFactory.InkOutline());
            _guardianOutline = NewRenderer("GuardianOutline", SortingOrders.Ink + 3, transform, SpriteFactory.InkOutline());
            _bubbleOutline.enabled = _guardianOutline.enabled = false;

            // Экран чернил висит на камере, а не на боссе: он не должен ездить с миром.
            var inkGo = new GameObject("InkScreen");
            _ink = inkGo.AddComponent<InkScreen>();
            _ink.Init(cfg, field, field.Cam.transform);

            gameObject.SetActive(false);
        }

        static SpriteRenderer NewRenderer(string childName, int order, Transform parent, Sprite sprite)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            sr.sprite = sprite;
            return sr;
        }

        public void Spawn()
        {
            gameObject.SetActive(true);

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.inkHp));
            Hp = MaxHp;
            _tint.Reset();
            _pulse = 0f;
            _lastCorner = -1;
            _strikes = 0;
            _ballsNext = false;

            // Заходит сверху, из-за кромки: видно, кто пришёл.
            _position = new Vector2(0f, _field.Top + BodySize * 0.8f);
            _targetX = 0f;
            SetPhase(Phase.Enter, 0f);

            _glow = _glowWant = 0.3f;
            _squint = _squintWant = 0f;
            _reach = _reachWant = 0f;
            _gaze = Vector2.zero;
            _armPhase = UnityEngine.Random.value * 10f;
            _bodyAlpha = 1f;
            _darkShown = 0f;
            _laneShown = 0f;

            _blob.enabled = false;
            _jet.enabled = false;
            HideChevrons();
            HideBalls();
            foreach (var d in _darkEyes) d.enabled = false;
            _bubbleOutline.enabled = _guardianOutline.enabled = false;
            _ink.Clear();
            _guardian.SetVisible(true);

            ApplyTransform();
            ApplyLook(0f);
        }

        void SetPhase(Phase phase, float length)
        {
            _phase = phase;
            _phaseLength = length;
            _timer = length;
            _phaseClock = 0f;
        }

        /// <summary>Чернила на экране гаснут сами по таймеру, отдельного сброса не требуют.</summary>
        public void Interrupt() { }

        public void Despawn()
        {
            _phase = Phase.Done;
            _ink.Clear();
            _guardian.SetVisible(true);      // на случай смерти игрока прямо в темноте
            _guardian.ClearDisplacement();   // иначе страж остался бы лежать в углу навсегда
            HideChevrons();
            HideBalls();
            if (_darkEyes != null) foreach (var d in _darkEyes) d.enabled = false;
            if (_bubbleOutline != null) _bubbleOutline.enabled = _guardianOutline.enabled = false;
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _pulse += dt;
            _phaseClock += dt;

            _tint.Tick(dt);

            // При гибели темнота не держится: удержание снимается, и чернила сходят
            // за обычные inkFadeTime. Прежде они смывались вшестеро быстрее —
            // экран светлел целиком за 0.1 с, фронт короче правила 0.3–0.6 с. [ТЕСТ]
            _ink.Tick(dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Charge: TickCharge(dt); break;
                case Phase.Spit: TickSpit(dt); break;
                case Phase.Blackout: TickBlackout(dt); break;
                case Phase.Search: TickSearch(dt); break;
                case Phase.Strike: TickStrike(dt); break;
                case Phase.Balls: TickBalls(dt); break;
                case Phase.Recover: TickRecover(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }
            if (_phase == Phase.Done) return;

            ApplyLook(dt);
            TickDarkEyes(dt);
            TickOutlines();
            TickLanes(dt);
        }

        Vector2 HoverPoint => new Vector2(_targetX, _field.YFromTop(_cfg.inkHoverFactor));

        void TickEnter(float dt)
        {
            _position = Vector2.MoveTowards(_position, HoverPoint, 6f * dt);
            ApplyTransform();

            if (Vector2.Distance(_position, HoverPoint) < 0.05f) StartCharge(0.6f);
        }

        // ------------------------------------------------------------- замах

        void StartCharge(float extra = 0f)
        {
            SetPhase(Phase.Charge, _cfg.inkChargeTime + extra);
            _blob.enabled = false;
            _jet.enabled = false;
            HideChevrons();
            Noised?.Invoke(Noise.Charge, _position);
        }

        /// <summary>
        /// Замах: мантия набухает, глаза сужаются и разгораются, щупальца тянутся
        /// вперёд, из клюва стекает тёмная струйка. Всё плавно и одновременно.
        /// </summary>
        void TickCharge(float dt)
        {
            _timer -= dt;
            Drift(dt, 1f);

            float t = 1f - Mathf.Clamp01(_timer / _phaseLength);
            float swell = 1f + 0.16f * Mathf.Sin(t * Mathf.PI);   // одно медленное набухание, без дрожи
            ApplyTransform(swell, 1f / swell);

            _glowWant = 1f;
            _squintWant = 1f;
            _reachWant = 1f;

            _dripClock += dt;
            if (_fx != null && _dripClock >= 0.08f)
            {
                _dripClock = 0f;
                _fx.Emit(Muzzle + new Vector2(UnityEngine.Random.Range(-0.05f, 0.05f), -0.05f),
                         new Vector2(UnityEngine.Random.Range(-0.05f, 0.05f), -0.7f),
                         SpriteFactory.InkMantleLow, 0.5f, 0.06f, 0.8f, SortingOrders.Boss + 1);
            }

            if (_timer <= 0f) StartSpit();
        }

        // ------------------------------------------------------------ плевок

        void StartSpit()
        {
            SetPhase(Phase.Spit, Mathf.Max(0.1f, _cfg.inkSpitTime));

            _blob.enabled = true;
            _blob.transform.position = Muzzle;
            _blob.transform.localScale = Vector3.one * (BodySize * 0.25f);
            _blob.color = Color.white;
        }

        /// <summary>
        /// Сгусток летит «в камеру»: разрастается и выцветает к центру экрана, за ним
        /// тянется вязкий хвост капель. Настоящей глубины у нас нет, поэтому иллюзию
        /// даёт разгон масштаба — растёт он не линейно, а с ускорением к концу.
        /// </summary>
        void TickSpit(float dt)
        {
            _timer -= dt;
            float t = 1f - Mathf.Clamp01(_timer / _phaseLength);

            Vector2 from = Muzzle;
            Vector2 to = new Vector2(0f, (_field.Top + _field.Bottom) * 0.5f);
            Vector2 at = Vector2.Lerp(from, to, t * t);
            _blob.transform.position = at;

            float screen = Mathf.Max(_field.HalfWidth * 2f, _field.Top - _field.Bottom);
            _blob.transform.localScale = Vector3.one * Mathf.Lerp(BodySize * 0.25f, screen * 1.4f, t * t * t);
            _blob.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.25f, t));

            _dripClock += dt;
            if (_fx != null && _dripClock >= 0.03f && t < 0.7f)
            {
                _dripClock = 0f;
                Vector2 back = (from - at).sqrMagnitude > 0.0001f ? (from - at).normalized : Vector2.up;
                _fx.Emit(at + back * BodySize * 0.12f + UnityEngine.Random.insideUnitCircle * 0.08f,
                         back * 0.6f, SpriteFactory.InkMantleLow, 0.45f, 0.08f, 0.85f, SortingOrders.Projectile);
            }

            ApplyTransform();
            _glowWant = 0.6f;
            _squintWant = 0.5f;
            _reachWant = 0.3f;

            if (_timer <= 0f) Blind();
        }

        // ------------------------------------------------------------- поиск

        /// <summary>
        /// Попадание: экран закрывается чернилами, стража зашвыривает в угол.
        ///
        /// Страж на это время снимается с показа: заливка не абсолютно непрозрачна,
        /// и яркий шарик просвечивал бы сквозь неё, выдавая угол раньше времени.
        /// </summary>
        void Blind()
        {
            // Клякса гаснет не здесь, а когда чернила закроют экран целиком
            // (TickBlackout): иначе за время нарастания экран сначала светлел бы.
            SetPhase(Phase.Blackout, Mathf.Lerp(_cfg.inkBlackoutTimeMin, _cfg.inkBlackoutTimeMax, Rage));

            _ink.Splash(_phaseLength, _cfg.inkFadeTime);
            _guardian.Displace(PickCorner());
            _guardian.SetVisible(false);
            HideChevrons();
            InkSplashed?.Invoke();

            // Новая позиция по горизонтали: линия удара каждый раз другая, иначе
            // игрок заучил бы одно место и перестал смотреть на стрелки.
            _targetX = UnityEngine.Random.Range(-_field.PlayHalfWidth * 0.8f, _field.PlayHalfWidth * 0.8f);
            _ballsNext = _strikes % 2 == 1;
        }

        /// <summary>Пауза вслепую. Босс медленно дрейфует, его глаза тускло светят сквозь чернила.</summary>
        void TickBlackout(float dt)
        {
            if (_blob.enabled && _ink.Coverage >= 0.999f) _blob.enabled = false;
            _timer -= dt;
            Drift(dt, DarkDriftScale);
            ApplyTransform();
            _glowWant = 0.55f;
            _squintWant = 0.3f;
            _reachWant = 0f;

            if (_timer > 0f) return;

            // Картинка возвращается — вот теперь ищем.
            _guardian.SetVisible(true);
            SetPhase(Phase.Search, Mathf.Lerp(_cfg.inkSearchTimeMax, _cfg.inkSearchTimeMin, Rage));
        }

        /// <summary>Угол экрана, обязательно не тот же, что в прошлый раз.</summary>
        Vector2 PickCorner()
        {
            int corner = UnityEngine.Random.Range(0, 4);
            if (corner == _lastCorner) corner = (corner + 1 + UnityEngine.Random.Range(0, 3)) % 4;
            _lastCorner = corner;

            float marginX = _field.HalfWidth * 0.22f;
            float marginY = (_field.Top - _field.Bottom) * 0.18f;

            float x = (corner == 0 || corner == 3) ? -_field.HalfWidth + marginX
                                                   : _field.HalfWidth - marginX;
            float y = (corner < 2) ? _field.Top - marginY : _field.Bottom + marginY;
            return new Vector2(x, y);
        }

        void TickSearch(float dt)
        {
            _timer -= dt;
            Drift(dt, 1f);
            ApplyTransform();
            _glowWant = 0.8f;
            _squintWant = 0.7f;
            _reachWant = 0.4f;

            // Что перекрывать — видно с первого взгляда: стрелки для струи,
            // отметки путей для шаров.
            Vector2 d = _bubble.Center - Muzzle;
            _laneAim = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
            if (_ballsNext) HideChevrons();
            else UpdateChevrons();

            if (_timer > 0f) return;
            _strikes++;
            if (_ballsNext) StartBalls();
            else StartStrike();
        }

        // -------------------------------------------------------------- удар

        void StartStrike()
        {
            SetPhase(Phase.Strike, 0f);
            HideChevrons();

            Vector2 delta = _bubble.Center - Muzzle;
            _jetDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
            _jetPosition = Muzzle;
            _jetHitBubble = false;

            _jet.enabled = true;
            _jet.transform.localScale = Vector3.one * (_cfg.inkJetRadius * 2.4f);
            _jet.color = Color.white;
        }

        void TickStrike(float dt)
        {
            float distance = _cfg.inkStrikeSpeed * dt;

            // Дробим шаг: на полной скорости струя перепрыгнула бы стража за кадр.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
            float step = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                _jetPosition += _jetDirection * step;
                if (ResolveJet()) return;
            }

            _jet.transform.position = _jetPosition;
            ApplyTransform();

            if (Mathf.Abs(_jetPosition.x) > _field.HalfWidth + 1.5f ||
                _jetPosition.y < _field.Bottom - 1.5f || _jetPosition.y > _field.Top + 1.5f)
                StartRecover();
        }

        bool ResolveJet()
        {
            if (_guardian.CanDeflect &&
                Vector2.Distance(_jetPosition, _guardian.Center) < _cfg.inkJetRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(_jetPosition);
                ApplyDamage(_jetPosition);

                _jet.enabled = false;
                if (_phase != Phase.Dying) StartRecover();
                return true;
            }

            if (!_jetHitBubble &&
                Vector2.Distance(_jetPosition, _bubble.Center) < _cfg.inkJetRadius + _bubble.Radius)
            {
                _jetHitBubble = true;   // на подшагах струя пройдёт рядом ещё не раз
                BubbleHit?.Invoke(_jetPosition);
            }
            return false;
        }

        // ------------------------------------------------------- три шара

        Vector2 LaneDirection(int lane)
            => Quaternion.Euler(0f, 0f, (lane - 1) * LaneSpread) * _laneAim;

        /// <summary>
        /// Три шара по расходящимся путям. Средний — в пузырёк, крайние уходят мимо
        /// и лопаются на капли. Скорость и радиус — как у струи: сложность та же,
        /// добавлено чтение «какой путь настоящий».
        /// </summary>
        void StartBalls()
        {
            SetPhase(Phase.Balls, 0f);
            HideChevrons();
            _ballsHitBubble = false;
            for (int i = 0; i < BallCount; i++)
            {
                _ballPos[i] = Muzzle;
                _ballDir[i] = LaneDirection(i);
                _ballTravel[i] = 0f;
                _ballPop[i] = 1f;
                _balls[i].enabled = true;
                _balls[i].color = Color.white;
            }
            Noised?.Invoke(Noise.Balls, Muzzle);
        }

        void TickBalls(float dt)
        {
            float distance = _cfg.inkStrikeSpeed * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
            float step = distance / steps;
            float reachBubble = Vector2.Distance(Muzzle, _bubble.Center);
            bool any = false;

            for (int b = 0; b < BallCount; b++)
            {
                if (_ballPop[b] < 1f)
                {
                    // лопается: капли уже разлетелись, сам шар тает за 0.2 с
                    _ballPop[b] = Mathf.Max(0f, _ballPop[b] - dt / 0.2f);
                    _balls[b].enabled = _ballPop[b] > 0f;
                    if (_balls[b].enabled)
                    {
                        _balls[b].color = Color.white.WithAlpha(_ballPop[b]);
                        _balls[b].transform.localScale = Vector3.one * (_cfg.inkJetRadius * 2.1f * (1.3f - 0.3f * _ballPop[b]));
                        any = true;
                    }
                    continue;
                }
                if (!_balls[b].enabled) continue;
                any = true;

                for (int s = 0; s < steps && _ballPop[b] >= 1f; s++)
                {
                    _ballPos[b] += _ballDir[b] * step;
                    _ballTravel[b] += step;

                    if (_guardian.CanDeflect &&
                        Vector2.Distance(_ballPos[b], _guardian.Center) < _cfg.inkJetRadius + _guardian.Radius)
                    {
                        _guardian.NotifyDeflect();
                        GuardianBlocked?.Invoke(_ballPos[b]);
                        PopBall(b);
                        ApplyDamage(_ballPos[b]);
                        if (_phase == Phase.Dying) return;
                        break;
                    }

                    if (!_ballsHitBubble &&
                        Vector2.Distance(_ballPos[b], _bubble.Center) < _cfg.inkJetRadius + _bubble.Radius)
                    {
                        _ballsHitBubble = true;
                        BubbleHit?.Invoke(_ballPos[b]);
                        PopBall(b);
                        if (_phase == Phase.Done) return;
                        break;
                    }

                    // ложный путь: лопается чуть дальше расстояния до пузырька
                    if (_ballTravel[b] > reachBubble * 1.15f || OffScreen(_ballPos[b])) { PopBall(b); break; }
                }

                if (_ballPop[b] >= 1f)
                {
                    _balls[b].transform.position = new Vector3(_ballPos[b].x, _ballPos[b].y, 0f);
                    _balls[b].transform.localScale = Vector3.one * (_cfg.inkJetRadius * 2.1f);
                }
            }

            ApplyTransform();
            if (!any) StartRecover();
        }

        bool OffScreen(Vector2 p)
            => Mathf.Abs(p.x) > _field.HalfWidth + 1f || p.y < _field.Bottom - 1f || p.y > _field.Top + 1f;

        /// <summary>Шар лопается на мелкие капли — без вспышки, тёмные брызги.</summary>
        void PopBall(int b)
        {
            _ballPop[b] = 0.999f;
            _fx?.Burst(_ballPos[b], SpriteFactory.InkMantleLow, 5, 0.4f, 1.5f, 0.04f, 0.08f);
        }

        void HideBalls()
        {
            if (_balls != null) foreach (var b in _balls) b.enabled = false;
            if (_lane != null) foreach (var l in _lane) l.enabled = false;
            _laneShown = 0f;
        }

        /// <summary>
        /// Отметки путей шаров: настоящий путь — коралловые точки, ложные — пустые
        /// фиолетовые кольца. Проявляются за 0.2 с, гаснут за 0.3 с; лежат поверх
        /// чернил, как и стрелки.
        /// </summary>
        void TickLanes(float dt)
        {
            bool want = _phase == Phase.Search && _ballsNext;
            _laneShown = Mathf.MoveTowards(_laneShown, want ? 1f : 0f, dt / (want ? 0.2f : 0.3f));
            bool on = _laneShown > 0.004f;
            float length = Vector2.Distance(Muzzle, _bubble.Center);

            for (int lane = 0; lane < BallCount; lane++)
            {
                Vector2 dir = LaneDirection(lane);
                bool danger = lane == 1;
                for (int k = 0; k < LaneMarks; k++)
                {
                    var sr = _lane[lane * LaneMarks + k];
                    sr.enabled = on;
                    if (!on) continue;
                    float t = (k + 1f) / (LaneMarks + 1f);
                    Vector2 at = Muzzle + dir * (length * t);
                    sr.transform.position = new Vector3(at.x, at.y, 0f);
                    sr.transform.localScale = Vector3.one * (danger ? 0.42f : 0.5f);
                    sr.color = danger ? Palette.Coral.WithAlpha(0.5f * _laneShown)
                                      : Palette.InkGlow.WithAlpha(0.35f * _laneShown);
                }
            }
        }

        // ------------------------------------------------------------ передышка

        void StartRecover()
        {
            SetPhase(Phase.Recover, _cfg.inkRestTime);
            _jet.enabled = false;
            for (int i = 0; i < BallCount; i++) _balls[i].enabled = false;
        }

        void TickRecover(float dt)
        {
            _timer -= dt;
            Drift(dt, 1f);
            ApplyTransform();
            _glowWant = 0.3f;
            _squintWant = 0f;
            _reachWant = 0f;
            if (_timer <= 0f) StartCharge();
        }

        /// <summary>
        /// Смерть: тело медленно тает и всплывает, чернила расползаются в стороны,
        /// вверх уходят пузыри. 1.8 с, без вспышки.
        /// </summary>
        void TickDying(float dt)
        {
            _timer += dt;
            _position += Vector2.up * (0.35f * dt);
            ApplyTransform(1f + _timer * 0.06f, 1f - _timer * 0.08f);

            float k = Mathf.Clamp01(_timer / DeathTime);
            _bodyAlpha = 1f - Mathf.SmoothStep(0f, 1f, k);
            _glowWant = 0f;

            _dripClock += dt;
            if (_fx != null && _dripClock >= 0.06f && k < 0.85f)
            {
                _dripClock = 0f;
                Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                _fx.Emit(_position + dir * BodySize * 0.2f, dir * 0.6f, SpriteFactory.InkMantleLow,
                         1.1f, 0.12f, 0.6f, SortingOrders.Boss + 1);
                _fx.Emit(_position + new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f) * BodySize, 0f),
                         new Vector2(0f, UnityEngine.Random.Range(0.6f, 1.1f)), Palette.Foam,
                         0.9f, 0.05f, 0.45f, SortingOrders.Boss + 2);
            }

            if (_timer >= DeathTime)
            {
                _phase = Phase.Done;
                Defeated?.Invoke(_position);
                Despawn();
            }
        }

        void ApplyDamage(Vector2 at)
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(at);

            if (Hp <= 0)
            {
                _phase = Phase.Dying;
                _timer = 0f;
                _ink.EndHold();
                _jet.enabled = false;
                _blob.enabled = false;
                HideChevrons();
                HideBalls();
                _guardian.SetVisible(true);
                _guardian.ClearDisplacement();
            }
        }

        // ------------------------------------------------------- вспомогательное

        /// <summary>Неспешный дрейф к выбранной точке — босс не телепортируется.</summary>
        void Drift(float dt, float scale)
        {
            _position = Vector2.MoveTowards(_position, HoverPoint, _cfg.inkDriftSpeed * scale * dt);
        }

        void ApplyTransform(float scaleX = 1f, float scaleY = 1f)
        {
            transform.position = _position;

            // Мантию качает на воде — без этого босс выглядит приклеенным.
            float sway = Mathf.Sin(_pulse * 1.6f) * 3.5f;
            _look.localRotation = Quaternion.Euler(0f, 0f, sway);
            _look.localScale = new Vector3(BodySize * scaleX, BodySize * scaleY, 1f);
        }

        /// <summary>
        /// Живые части: свет и взгляд глаз, сужение зрачков, щупальца волной с
        /// запаздыванием по звеньям. Всё через ограниченную скорость — ни одна
        /// величина не меняется скачком.
        /// </summary>
        void ApplyLook(float dt)
        {
            _glow = Mathf.MoveTowards(_glow, _glowWant, dt / EyeRise);
            _squint = Mathf.MoveTowards(_squint, _squintWant, dt / 0.4f);
            _reach = Mathf.MoveTowards(_reach, _reachWant, dt / 0.5f);
            _armPhase += dt * 2.6f;   // одна волна за 2.4 с

            Vector2 toBubble = _bubble != null ? _bubble.Center - _position : Vector2.down;
            Vector2 wantGaze = toBubble.sqrMagnitude > 0.0001f ? toBubble.normalized : Vector2.down;
            _gaze = Vector2.MoveTowards(_gaze, wantGaze, dt * 0.8f);

            float a = _bodyAlpha;
            _mantle.color = Color.white.WithAlpha(a);
            _beak.color = Color.white.WithAlpha(a);
            _lid.color = Color.white.WithAlpha(a);

            for (int i = 0; i < EyeCount; i++)
            {
                Color eye = SpriteFactory.InkEyeColors[i];
                Color c = Palette.SafeLight(Color.Lerp(eye * 0.55f, eye, _glow));
                c.a = a;
                _iris[i].color = c;

                // зрачок-щель сужается по высоте; у прищуренного правого — ещё уже
                var pt = _pupil[i].transform;
                float squint = Mathf.Lerp(1f, 0.45f, _squint) * (i == 1 ? 0.85f : 1f);
                pt.localScale = new Vector3(0.7f, 0.7f * squint, 1f);
                pt.localPosition = new Vector3(_gaze.x * 0.16f, _gaze.y * 0.1f, -0.01f);
                _pupil[i].color = Color.white.WithAlpha(a);
            }

            // Щупальца: восемь корней пучком под головой одной линией, веером
            // вниз; второе звено загибается наружу, как у спрута. Волна идёт с
            // запаздыванием от корня к концу и от щупальца к щупальцу. На замахе
            // тянутся к цели.
            Vector2 aimLocal = (Vector2)(Quaternion.Inverse(_look.localRotation) * (Vector3)wantGaze);
            float aimAngle = Mathf.Atan2(aimLocal.y, aimLocal.x) * Mathf.Rad2Deg;
            for (int i = 0; i < ArmCount; i++)
            {
                float u = i / (float)(ArmCount - 1);
                // корни — под низом головы (InkDome: низ на −0.10, корни чуть выше кромки)
                float side = u * 2f - 1f;
                float rx = 0.15f * side;
                Vector2 root = new Vector2(rx, -0.07f + 0.03f * side * side);
                float rest = -90f + 46f * side;
                float wave = Mathf.Sin(_armPhase + i * 0.7f) * 9f;
                float a0 = Mathf.LerpAngle(rest + wave, aimAngle + side * 15f, _reach * 0.5f);
                float curl = 24f * side * (1f - _reach);
                float a1 = a0 + curl + Mathf.Sin(_armPhase + i * 0.7f - 0.8f) * 14f
                           + Mathf.DeltaAngle(a0, aimAngle) * _reach * 0.25f;

                Vector2 d0 = new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad));
                Vector2 d1 = new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad));
                const float l0 = 0.12f, l1 = 0.11f;
                PlaceArm(_arm[i * ArmLinks], root + d0 * (l0 * 0.5f), a0, l0, 0.12f, a);
                PlaceArm(_arm[i * ArmLinks + 1], root + d0 * l0 + d1 * (l1 * 0.5f), a1, l1, 0.085f, a);

                // редкие капли чернил с кончиков
                if (_fx != null && dt > 0f && UnityEngine.Random.value < dt * 0.5f && a > 0.5f)
                {
                    Vector2 tipLocal = root + d0 * l0 + d1 * l1;
                    Vector2 tip = _look.TransformPoint(tipLocal);
                    _fx.Emit(tip, new Vector2(0f, -0.35f), SpriteFactory.InkMantleLow, 0.9f, 0.05f, 0.7f, SortingOrders.Boss - 1);
                }
            }
        }

        static void PlaceArm(SpriteRenderer sr, Vector2 center, float angle, float length, float width, float alpha)
        {
            sr.transform.localPosition = new Vector3(center.x, center.y, 0.01f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            sr.transform.localScale = new Vector3(width, length / 0.95f, 1f);
            sr.color = Color.white.WithAlpha(alpha);
        }

        /// <summary>
        /// Контуры пузырька и стража поверх чернил: мягкое светлое кольцо, яркость —
        /// inkOutlineAlpha в доле того, насколько закрыт экран. Появляются и уходят
        /// вместе с чернилами — без своего фронта.
        /// </summary>
        void TickOutlines()
        {
            float a = Mathf.Clamp01(_ink.Coverage) * _cfg.inkOutlineAlpha;
            bool on = a > 0.004f;
            _bubbleOutline.enabled = _guardianOutline.enabled = on;
            if (!on) return;
            Color c = OutlineColor.WithAlpha(a);
            _bubbleOutline.transform.position = new Vector3(_bubble.Center.x, _bubble.Center.y, 0f);
            _bubbleOutline.transform.localScale = Vector3.one * (_bubble.Radius * 2.5f);
            _bubbleOutline.color = c;
            _guardianOutline.transform.position = new Vector3(_guardian.Center.x, _guardian.Center.y, 0f);
            _guardianOutline.transform.localScale = Vector3.one * (_guardian.Radius * 2.5f);
            _guardianOutline.color = c;
        }

        /// <summary>Холодный светлый контур, сумма каналов 2.27 — без белого.</summary>
        static readonly Color OutlineColor = Palette.Hex("B0C4D0");

        /// <summary>
        /// Глаза в темноте: две тусклые холодные точки поверх чернил, ровно там,
        /// где глаза морока. Он медленно дрейфует к месту следующего удара — точки
        /// подсказывают, откуда придёт атака. Проявляются вместе с чернилами.
        /// </summary>
        void TickDarkEyes(float dt)
        {
            float want = _phase == Phase.Blackout ? Mathf.Clamp01(_ink.Coverage) : 0f;
            _darkShown = Mathf.MoveTowards(_darkShown, want, dt / 0.3f);
            bool on = _darkShown > 0.004f;
            for (int i = 0; i < EyeCount; i++)
            {
                var sr = _darkEyes[i];
                sr.enabled = on;
                if (!on) continue;
                Vector3 at = _iris[i].transform.position;
                sr.transform.position = new Vector3(at.x, at.y, 0f);
                sr.transform.localScale = Vector3.one * Mathf.Max(0.18f, SpriteFactory.InkEyeSpots[i].z * BodySize * 0.9f);
                sr.color = (SpriteFactory.InkEyeColors[i] * 0.75f).WithAlpha(0.34f * _darkShown);
            }
        }

        void UpdateChevrons()
        {
            Vector2 from = Muzzle;
            Vector2 delta = _bubble.Center - from;
            float distance = delta.magnitude;
            if (distance < 0.3f) { HideChevrons(); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            // Один проход волны за ChevronWavePeriod: прежде 1.6 прохода в секунду,
            // и каждая стрелка мигала чаще правила «не быстрее раза в 1.5 с».
            float head = Mathf.Repeat(_pulse / ChevronWavePeriod, 1f);

            // К концу окна стрелки разгораются — видно, что время выходит.
            float urgency = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _phaseLength));
            // Появление за 0.2 с от начала поиска, а не за кадр.
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_phaseClock / 0.2f));

            for (int i = 0; i < _chevrons.Length; i++)
            {
                float t = (i + 0.5f) / _chevrons.Length;
                var tr = _chevrons[i].transform;
                tr.position = from + delta * t;
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = Vector3.one * 0.6f;

                // Волна — по кругу: расстояние до «головы» считается с обоих
                // концов. Прежде учитывался только один, и на стыке стрелка
                // гасла за кадр.
                float dist = Mathf.Abs(t - head);
                dist = Mathf.Min(dist, 1f - dist);
                float wave = Mathf.Max(0f, 1f - dist * 4f);
                float bright = Mathf.Clamp01(0.35f + urgency * 0.4f + wave * 0.3f) * appear;

                _chevrons[i].enabled = true;
                _chevrons[i].color = new Color(Palette.InkGlow.r, Palette.InkGlow.g, Palette.InkGlow.b, bright);
            }
        }

        void HideChevrons()
        {
            if (_chevrons == null) return;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Куда поставил бы стража игрок с реакцией 0.5 с от начала поиска: на линию
        /// струи или на путь настоящего шара. Для проверки проходимости в Unity.
        /// </summary>
        public bool DevBotTarget(out Vector2 target)
        {
            target = default;
            Vector2 b = _bubble.Center;
            switch (_phase)
            {
                case Phase.Search:
                    if (_phaseClock < Reaction) return false;
                    target = b + (Muzzle - b).normalized * 1.2f;
                    return true;
                case Phase.Strike:
                case Phase.Balls:
                    target = b + (Muzzle - b).normalized * 1.2f;
                    return true;
            }
            return false;
        }

        /// <summary>Отладка: следующий удар — шары.</summary>
        public void DevBallsNext() => _strikes = 1;

        public string DevState => _phase + (_ballsNext ? " (шары)" : " (струя)");
#endif
    }
}
