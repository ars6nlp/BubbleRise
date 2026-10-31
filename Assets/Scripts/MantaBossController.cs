using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Великая тень» — финальный босс. Гигантский скат, который не выходит на арену,
    /// а уже висит над ней: сначала в фоне, за кораллами, потом всё ближе.
    ///
    /// Он собирает то, чему учили остальные девять, поэтому и появляется только после
    /// них — пока хоть один босс не побеждён, его нет в ротации (см. GameManager).
    ///
    /// **Урон по нему проходит единственным способом — отражением его же разрядов.**
    /// Это механика грозовой медузы, самого первого босса игры: круг замыкается, а
    /// заодно снимается вопрос, чем бить того, кто занимает весь экран и висит в фоне.
    /// Дотянуться до него нельзя — можно только вернуть ему его оружие.
    ///
    /// Три акта, по трети полосы HP на каждый:
    ///
    /// 1. **В фоне.** Висит полупрозрачной тенью и бьёт молниями рывками — короткая
    ///    очередь, пауза, снова очередь. Приём грозовой медузы, но с двумя поправками:
    ///    молния отскакивает от стража **как от зеркала**, а урон проходит только в
    ///    **ядра на крыльях** — два огонька, левый и правый, которые ходят по своей
    ///    половине тела и гаснут после каждого попадания. Мало отразить: надо
    ///    отразить туда, а крылья разнесены на весь размах.
    /// 2. **Вышел.** Растворяется и начинает **таранить**: уходит за кадр и идёт
    ///    прямо через пузырёк, каждый раз с новой стороны. Единственная подсказка —
    ///    полоса у края экрана перед заходом. Принял удар стражем — ранил его.
    /// 3. **В упор.** Нависает брюхом и работает двумя приёмами, оба нарочно крупные
    ///    и медленные — на туше в полкадра мелкое просто теряется. **Размах хвостом:**
    ///    плеть наливается светом и проходит дугой через пузырёк, страж обязан
    ///    встретить жало. **Град шаров:** медленные сферы сходят с брюха, гасятся
    ///    касанием, и часть из них заряжена — заряженный уходит обратно в ската.
    ///    Пока страж разбирает град, хвост уже замахивается.
    /// </summary>
    public class MantaBossController : MonoBehaviour, IBoss
    {
        enum Phase { Arrival, Idle, Volley, Vanish, Lurk, Pass, Turn,
                     LashWindup, LashSweep, OrbRain, TailAim, Fan, Eclipse, Pups, Dying, Done }

        public string DisplayName => Loc.Get("boss.manta");
        public BossKind Kind => BossKind.Manta;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        /// <summary>Чем именно скат сейчас шумит. Сам он про звук ничего не знает —
        /// подписывается GameManager, у которого и лежит AudioManager.</summary>
        public enum Noise { Bolt, Slam, Gill, Shock }

        /// <summary>Повод дать звук.</summary>
        public event Action<Noise> Noised;
        /// <summary>Сменился акт: 0, 1, 2. Для музыки и баннера.</summary>
        public event Action<int> ActChanged;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;
        Transform _projectileRoot;
        FxManager _fx;

        SpriteRenderer _body;
        SpriteRenderer[] _eyes;
        SpriteRenderer _eclipse;
        SpriteRenderer _aimBeam;
        SpriteRenderer _lash;        // хвост-плеть третьего акта
        readonly List<Pup> _pups = new List<Pup>();

        readonly List<BossProjectile> _live = new List<BossProjectile>();
        readonly Stack<BossProjectile> _pool = new Stack<BossProjectile>();
        readonly List<BossProjectile> _spikes = new List<BossProjectile>();

        Phase _phase = Phase.Done;
        int _act;
        float _timer;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();
        float _flap;
        float _boltTimer;
        float _actFlashTimer;

        Vector2 _position;
        Vector2 _drift;

        Vector2 _tailTarget;
        int _burstLeft;

        float _eclipseLevel;
        float _eclipseHold;
        float _lastFlapPhase;

        // Акт 1: очереди молний и два ядра на крыльях.
        int _volleyLeft;
        float _volleyTimer;
        float _volleySide = 1f;

        // Два ядра — по одному на крыло. Бить нужно оба, и они далеко друг от
        // друга: одной позицией стража не обойтись.
        readonly SpriteRenderer[] _cores = new SpriteRenderer[2];
        readonly Vector2[] _coreLocal = new Vector2[2];
        readonly float[] _coreTimer = new float[2];
        readonly bool[] _coreLit = new bool[2];

        bool _turnFlipped;           // в перевороте: спрайт уже сменён на брюхо
        float _turnSquash = 1f;      // 1 — обычная туша, 0 — сплющена в полоску

        // Акт 2: заход из-за кадра и таран.
        Vector2 _passFrom, _passTo;
        Vector2 _passDirection;
        float _passProgress;
        bool _ramResolved;           // за один заход он бьёт ровно один раз
        bool _tailShotDone;          // и ровно одну молнию вдогонку

        /// <summary>Куда развёрнута туша. Спрайт нарисован смотрящим вверх, поэтому
        /// в фоновых актах угол нулевой, а в проходах — по курсу.</summary>
        float _bodyAngle;
        float _vanish = 1f;          // 1 — виден полностью, 0 — растворился

        // Акт 3: размах хвоста и град шаровых разрядов.
        float _lashFrom, _lashTo;    // углы дуги размаха, градусы
        float _lashAngle;
        float _lashProgress;         // сколько дуги пройдено, 0..1
        float _lashLength;           // длина плети, посчитанная под расстояние до цели
        bool _lashResolved;          // за один размах он задевает ровно один раз
        int _orbsLeft;
        float _orbTimer;
        readonly List<BossProjectile> _charged = new List<BossProjectile>();
        readonly List<BossProjectile> _splitters = new List<BossProjectile>();

        /// <summary>Мелкий скат из выводка второго акта.</summary>
        class Pup
        {
            public SpriteRenderer Sr;
            public Vector2 Position;
            public Vector2 Velocity;
            public bool Alive;
        }

        /// <summary>
        /// Размах крыльев. Считается не от игровой колонки, а между ней и реальной
        /// шириной экрана: привязанный к одной колонке, финальный босс на широком
        /// мониторе занимал едва треть кадра и терялся в пустых полях по бокам,
        /// хотя на телефоне смотрелся ровно как задумано.
        /// </summary>
        float Span => Mathf.Lerp(_field.PlayHalfWidth, _field.HalfWidth, 0.55f)
                      * 2f * _cfg.mantaSpanFactor;
        float BodyScale => _act == 0 ? _cfg.mantaBackgroundScale
                        : _act == 1 ? _cfg.mantaPassScale : _cfg.mantaCloseScale;

        /// <summary>Кончик хвоста — из него вылетают шипы.</summary>
        Vector2 TailTip => BodyPoint(new Vector2(0f, -0.62f));

        /// <summary>Голова: ею он и таранит, когда идёт через арену.</summary>
        Vector2 Snout => BodyPoint(new Vector2(0f, 0.46f));

        /// <summary>
        /// Точка на теле по её месту в спрайте (доли полуразмаха) с учётом разворота.
        /// Без поворота смещений глаза и хвост оставались бы на прежних местах, пока
        /// сама туша идёт боком, — и отрывались бы от неё.
        /// </summary>
        Vector2 BodyPoint(Vector2 local)
        {
            float half = Span * BodyScale * 0.5f;
            Vector2 scaled = local * half;

            if (Mathf.Abs(_bodyAngle) < 0.01f) return _position + scaled;

            float a = _bodyAngle * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return _position + new Vector2(scaled.x * c - scaled.y * sn,
                                           scaled.x * sn + scaled.y * c);
        }

        int ActFromHp => Hp > _cfg.mantaHpPerAct * 2 ? 0 : (Hp > _cfg.mantaHpPerAct ? 1 : 2);

        /// <summary>
        /// Интервал между угрозами, который страж физически способен отработать.
        ///
        /// После каждого блока страж уходит на перезарядку, и серия, идущая чаще неё,
        /// непробиваема в принципе: часть ударов проходит не потому, что игрок ошибся,
        /// а потому что ему нечем было ответить. Такие «честные» промахи хуже любой
        /// сложности — поэтому нижняя граница берётся из самой перезарядки, а не из
        /// числа в инспекторе, которое легко разойдётся с ней при первой же правке.
        ///
        /// Полуторный запас — на реакцию и на то, чтобы довести стража до нужной точки.
        /// </summary>
        float Spaced(float wanted) => Mathf.Max(wanted, _cfg.guardianDeflectCooldown * 1.5f);

        /// <summary>Насколько выбран текущий акт: 0 в начале, 1 на последнем его HP.</summary>
        float ActProgress
        {
            get
            {
                int perAct = Mathf.Max(1, _cfg.mantaHpPerAct);
                int within = Hp - (2 - _act) * perAct;      // сколько HP осталось в этом акте
                return Mathf.Clamp01((perAct - within) / (float)Mathf.Max(1, perAct - 1));
            }
        }

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble,
                         GuardianOrb guardian, Transform projectileRoot, FxManager fx)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;
            _projectileRoot = projectileRoot;
            _fx = fx;

            // Затемнение лежит поверх мира, но под интерфейсом: полоса жизней и метры
            // обязаны оставаться читаемыми, даже когда крыло накрыло всё остальное.
            var eclipseGo = new GameObject("Eclipse");
            eclipseGo.transform.SetParent(transform, false);
            _eclipse = eclipseGo.AddComponent<SpriteRenderer>();
            _eclipse.sprite = SpriteFactory.SolidBlock();
            _eclipse.sharedMaterial = SpriteFactory.Unlit;
            _eclipse.sortingOrder = SortingOrders.Ink;
            _eclipse.enabled = false;

            var beamGo = new GameObject("TailAim");
            beamGo.transform.SetParent(transform, false);
            _aimBeam = beamGo.AddComponent<SpriteRenderer>();
            _aimBeam.sprite = SpriteFactory.AimBeam();
            _aimBeam.sharedMaterial = SpriteFactory.Unlit;
            _aimBeam.sortingOrder = SortingOrders.BossAura;
            _aimBeam.enabled = false;

            // Ядра первого акта: холодные огоньки на крыльях, куда и нужно возвращать
            // молнии. Цвет намеренно не такой, как у глаз, — их нельзя путать.
            for (int i = 0; i < _cores.Length; i++)
            {
                var coreGo = new GameObject("Core" + i);
                coreGo.transform.SetParent(transform, false);
                var sr = coreGo.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.MantaGill(true);
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.InkMarker;
                sr.enabled = false;
                _cores[i] = sr;
            }

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = SpriteFactory.MantaBack(false);
            _body.sharedMaterial = SpriteFactory.Unlit;
            _tint.Track(_body);

            // Глаза рисуются поверх тела и не тускнеют вместе с ним: тело в фоне
            // полупрозрачное, а огоньки должны гореть в полную силу. Именно они
            // превращают тень на заднем плане в того, кто на тебя смотрит.
            _eyes = new SpriteRenderer[2];
            for (int i = 0; i < _eyes.Length; i++)
            {
                var go = new GameObject("Eye" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.MantaEyeGlow();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.InkMarker;   // видны и сквозь затмение
                sr.enabled = false;
                _eyes[i] = sr;
            }

            // Хвост-плеть: отдельный спрайт, который тянется от туши к жалу. В
            // третьем акте это главное оружие, и видеть его надо раньше, чем оно дойдёт.
            var lashGo = new GameObject("Lash");
            lashGo.transform.SetParent(transform, false);
            _lash = lashGo.AddComponent<SpriteRenderer>();
            _lash.sprite = SpriteFactory.MantaLash(false);
            _lash.sharedMaterial = SpriteFactory.Unlit;
            _lash.sortingOrder = SortingOrders.Boss + 3;
            _lash.enabled = false;

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            transform.position = Vector3.zero;

            MaxHp = BossTuning.Hp(Mathf.Max(3, _cfg.mantaHpPerAct * 3));
            Hp = MaxHp;
            _act = 0;

            _phase = Phase.Arrival;
            _timer = _cfg.mantaArrivalTime;
            _tint.Reset();
            _flap = 0f;
            _boltTimer = _cfg.mantaBoltGapMax;
            _actFlashTimer = 0f;

            // Выплывает сверху: сначала видно только тень, наползающую на свет.
            _position = new Vector2(0f, _field.Top + Span * 0.45f);
            _drift = HoverPoint();

            _eclipseLevel = 0f;
            _eclipseHold = 0f;
            _lastFlapPhase = 0f;
            _bodyAngle = 0f;
            _vanish = 1f;
            _turnFlipped = false;
            _turnSquash = 1f;
            _lash.enabled = false;
            for (int i = 0; i < _cores.Length; i++) MoveCore(i);
            _eclipse.enabled = false;
            _aimBeam.enabled = false;
            ClearPups();

            _body.sprite = SpriteFactory.MantaBack(false);
            _body.enabled = true;
            ApplyBody();
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _flap += dt / Mathf.Max(0.05f, _cfg.mantaFlapPeriod);

            _tint.Tick(dt);

            TickEclipse(dt);
            TickProjectiles(dt);
            TickPups(dt);
            TickWake(dt);

            switch (_phase)
            {
                case Phase.Arrival: TickArrival(dt); break;
                case Phase.Idle: TickIdle(dt); break;
                case Phase.Volley: TickVolley(dt); break;
                case Phase.Vanish: TickVanish(dt); break;
                case Phase.Turn: TickTurn(dt); break;
                case Phase.LashWindup: TickLashWindup(dt); break;
                case Phase.LashSweep: TickLashSweep(dt); break;
                case Phase.OrbRain: TickOrbRain(dt); break;
                case Phase.Lurk: TickLurk(dt); break;
                case Phase.Pass: TickPass(dt); break;
                case Phase.TailAim: TickTailAim(dt); break;
                case Phase.Fan: TickFan(dt); break;
                case Phase.Eclipse: TickEclipsePhase(dt); break;
                case Phase.Pups: TickPupsPhase(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            if (_phase != Phase.Dying && _phase != Phase.Arrival && _phase != Phase.Turn && _act == 0)
                TickCores(dt);

            // В третьем акте плеть на виду всегда: в покое она просто свисает вниз.
            // Появляясь только на замахе, хвост выглядел возникающим из ниоткуда.
            if (_act == 2 && _phase != Phase.Dying && _phase != Phase.Turn
                && _phase != Phase.LashWindup && _phase != Phase.LashSweep)
            {
                Vector2 down = _bubble.Center - LashRoot();
                float rest = down.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg : -90f;

                _lashLength = Mathf.Max(_lashLength, down.magnitude * _cfg.mantaLashReach);
                DrawLash(rest, 0.35f, lit: false);
            }

            // Одиночные разряды и жабры — принадлежность третьего акта. В первых
            // двух у него по одному приёму на акт, и мешать их значит смазать то,
            // ради чего акты вообще разделены.
            // Во время переворота он не атакует: там он уходит за кромку и сплющен,
            // и стрелять оттуда было бы стрельбой из ниоткуда.


            ApplyBody();
        }

        /// <summary>
        /// Выход. Он не выплывает из-за края, как остальные, — он наползает сверху и
        /// гасит свет. Тишину в этот момент держит GameManager: музыка умолкает, и
        /// несколько секунд слышен только низкий гул.
        /// </summary>
        void TickArrival(float dt)
        {
            _timer -= dt;

            _position = Vector2.Lerp(_position, HoverPoint(), 1f - Mathf.Exp(-2.2f * dt));

            // Тень наползает вместе с ним: экран темнеет тем сильнее, чем он ближе.
            _eclipseLevel = Mathf.Max(_eclipseLevel,
                Mathf.Lerp(0f, 0.45f, 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.mantaArrivalTime))));

            if (_timer <= 0f)
            {
                // Тень не снимается за кадр (E-09): раньше здесь стояло
                // _eclipseLevel = 0, и экран светлел в 1.8 раза одним кадром,
                // вместе с полусотней частиц. Её гасит TickEclipse за mantaEclipseFade.
                _eclipseHold = 0f;

                // Удар по воде: муть расходится от обеих кромок крыльев разом.
                float reach = Span * BodyScale * 0.46f;
                for (int side = -1; side <= 1; side += 2)
                    _fx.Burst(_position + new Vector2(side * reach, 0f),
                              Palette.MantaEdge, 26, 1.2f, 4.2f, 0.08f, 0.26f);

                Noised?.Invoke(Noise.Slam);

                ActChanged?.Invoke(0);
                StartIdle(0.9f);
            }
        }

        // ------------------------------------------------------------ дрейф

        /// <summary>Куда он сносится. С каждым актом опускается ниже — подбирается к игроку.</summary>
        Vector2 HoverPoint()
        {
            // Высота считается и от кромки экрана, и от пузырька, а берётся та, что
            // не уводит тушу за кадр. С одной привязкой к размаху выросший скат
            // уезжал вверх целиком: у него полуразмах больше половины экрана.
            float y = _act == 2
                ? Mathf.Min(_field.Top - Span * BodyScale * 0.02f, _bubble.Center.y + 6.2f)
                : Mathf.Max(_field.Top - Span * BodyScale * 0.22f, _bubble.Center.y + 3.8f);

            // Снос вбок. В первом акте он заметно шире: строго над пузырьком скат
            // давал одну верную позицию стража на весь акт, и больше от игрока ничего
            // не требовалось. Две гармоники — чтобы точка не ходила по ровной синусоиде
            // и её нельзя было предсказать наперёд.
            float amplitude = _field.PlayHalfWidth * (_act == 0 ? _cfg.mantaDriftSway : 0.22f);
            float sway = (Mathf.Sin(_flap * Mathf.PI) * 0.75f
                        + Mathf.Sin(_flap * Mathf.PI * 2.7f + 1.1f) * 0.25f) * amplitude;

            return new Vector2(Mathf.Clamp(sway, -_field.PlayHalfWidth, _field.PlayHalfWidth), y);
        }

        void StartIdle(float duration)
        {
            _phase = Phase.Idle;
            _timer = duration;
            _aimBeam.enabled = false;
        }

        void TickIdle(float dt)
        {
            _timer -= dt;
            Drift(dt);

            if (_timer <= 0f) ChooseAttack();
        }

        void Drift(float dt)
        {
            _drift = HoverPoint();

            // После прохода он оказывается далеко за кадром. Возвращаться оттуда
            // прогулочным ходом значит пропасть с экрана на несколько секунд.
            float speed = _cfg.mantaDriftSpeed;
            if (Vector2.Distance(_position, _drift) > Span * 0.5f) speed *= 6f;

            _position = Vector2.MoveTowards(_position, _drift, speed * dt);
        }

        /// <summary>
        /// Следующий приём. Набор растёт с актом: в первом он только вспоминает чужое,
        /// во втором добавляются затмение и выводок, в третьем всё идёт вперемешку.
        /// </summary>
        void ChooseAttack()
        {
            // Первый акт — только разряд, второй — только проходы. Каждый акт держится
            // на одном приёме: так игрок успевает разобраться, что от него хотят,
            // прежде чем правила снова поменяются.
            if (_act == 0) { StartVolley(); return; }

            // Во втором акте его между заходами вообще не видно: он уходит за кадр и
            // возвращается уже в движении, каждый раз с новой стороны.
            if (_act == 1) { StartLurk(UnityEngine.Random.Range(0.7f, 1.3f)); return; }

            float roll = UnityEngine.Random.value;

            // Третий акт — чередование двух приёмов, и оба видны за версту: тяжёлый
            // размах хвостом и град шаровых разрядов. Мелкие быстрые атаки первых двух
            // актов отсюда убраны совсем: на туше во весь экран они не читались.
            if (roll < 0.55f) StartLashWindup();
            else StartOrbRain();
        }

        // ----------------------------------------------------------- ядра (акт 1)

        /// <summary>
        /// Ядра — единственные уязвимые места в первом акте. Их два, по одному на
        /// крыло: отражённая молния снимает HP, только попав в одно из них, всё
        /// остальное тело разряды просто гасит.
        ///
        /// Это и отличает акт от грозовой медузы, где хватало оказаться на пути
        /// разряда. Мало отразить — надо отразить **туда**; а поскольку крылья
        /// разнесены на весь размах, одной удобной позицией стража не обойтись:
        /// под левое и под правое ядро он ставится совершенно по-разному.
        /// </summary>
        void TickCores(float dt)
        {
            for (int i = 0; i < _cores.Length; i++)
            {
                _coreTimer[i] -= dt;

                if (!_coreLit[i])
                {
                    if (_coreTimer[i] <= 0f) MoveCore(i);
                    else { _cores[i].enabled = false; continue; }
                }
                else if (_coreTimer[i] <= 0f) MoveCore(i);

                var sr = _cores[i];
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 6.5f + i * 1.6f);
                float size = _cfg.mantaCoreRadius * 2f * (0.88f + 0.12f * pulse);

                sr.enabled = true;
                sr.transform.position = CorePosition(i);
                sr.transform.localScale = new Vector3(size, size, 1f);
                sr.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * pulse);
            }
        }

        Vector2 CorePosition(int index) => BodyPoint(_coreLocal[index]);

        /// <summary>Зажечь ядро заново — на своём крыле, но в новом месте.</summary>
        void MoveCore(int index)
        {
            _coreLit[index] = true;
            _coreTimer[index] = _cfg.mantaCoreMoveTime * UnityEngine.Random.Range(0.8f, 1.2f);

            // Каждое ядро держится своей половины: левое всегда слева, правое справа.
            // Не у самых кончиков — там обвод тонкий, и огонёк висел бы за кромкой.
            float side = index == 0 ? -1f : 1f;
            _coreLocal[index] = new Vector2(side * UnityEngine.Random.Range(0.28f, 0.60f),
                                            UnityEngine.Random.Range(-0.08f, 0.18f));
        }

        void HideCore(int index, float pause)
        {
            _coreLit[index] = false;
            _coreTimer[index] = pause;
            if (_cores[index] != null) _cores[index].enabled = false;
        }

        void HideCores()
        {
            for (int i = 0; i < _cores.Length; i++)
            {
                _coreLit[i] = false;
                if (_cores[i] != null) _cores[i].enabled = false;
            }
        }

        /// <summary>Ближайшее горящее ядро; −1, если сейчас не горит ни одного.</summary>
        int NearestCore(Vector2 from)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _cores.Length; i++)
            {
                if (!_coreLit[i]) continue;

                float d = Vector2.Distance(from, CorePosition(i));
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = i;
            }
            return best;
        }

        // --------------------------------------------------------- очередь (акт 1)

        /// <summary>
        /// Первый акт: рывками бьёт молниями с кромок крыла. Механика та же, что у
        /// грозовой медузы, — отражать разряды стражем, — но с одним важным отличием.
        ///
        /// **Молния отскакивает от стража как от зеркала.** У медузы отражённый разряд
        /// сам находит цель, и целиться не нужно; здесь направление отскока зависит от
        /// того, какой стороной страж встретил молнию. Чтобы вернуть её в ската, стража
        /// надо ставить не просто на пути, а между молнией и тушей. Скат при этом
        /// медленно сносится, и угол всё время меняется.
        ///
        /// Промах ничем не наказывается: пузырёк всё равно прикрыт. Цена ошибки —
        /// только потерянный урон, и это правильный размен для акта, который учит.
        /// </summary>
        void StartVolley()
        {
            _phase = Phase.Volley;
            _volleyLeft = Mathf.RoundToInt(Mathf.Lerp(_cfg.mantaVolleyMin, _cfg.mantaVolleyMax, ActProgress));
            _volleyLeft = Mathf.Max(1, _volleyLeft);
            _volleyTimer = 0.25f;
            _volleySide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        void TickVolley(float dt)
        {
            Drift(dt);

            _volleyTimer -= dt;
            if (_volleyTimer > 0f) return;

            FireBolt();

            if (--_volleyLeft > 0)
            {
                _volleyTimer = Spaced(Mathf.Lerp(_cfg.mantaVolleyGapMax, _cfg.mantaVolleyGapMin, ActProgress));
                return;
            }

            StartIdle(Mathf.Lerp(_cfg.mantaVolleyRestMax, _cfg.mantaVolleyRestMin, ActProgress));
        }

        /// <summary>
        /// Молния с кромки крыла. Точка вылета гуляет вдоль кромки и перескакивает со
        /// стороны на сторону: если бить всегда из одного места, подстановка стража
        /// заучивается с первого раза и акт превращается в удержание одной позиции.
        /// </summary>
        void FireBolt()
        {
            float along = UnityEngine.Random.Range(0.22f, _cfg.mantaVolleySpread);

            // Ближе к кончику крыла обвод тоньше, поэтому точка чуть поднимается —
            // иначе молния рождалась бы в пустоте за кромкой.
            // Кромки крыльев у такой туши уходят далеко за кадр, поэтому точку вылета
            // подтягиваем к экрану: молния, рождающаяся за краем, для игрока просто
            // возникает из ниоткуда уже на подлёте.
            Vector2 origin = _field.ClampToScreen(
                BodyPoint(new Vector2(_volleySide * along, -0.06f + along * 0.10f)), 0.4f);

            _volleySide = -_volleySide;     // следующая — с другого крыла

            // Летит строго в пузырёк и по прямой. Разброс углов обеспечивает сам
            // разнос точек вылета: от кончика дальнего крыла молния приходит почти
            // сбоку, от центра — сверху.
            Vector2 toBubble = _bubble.Center - origin;
            if (toBubble.sqrMagnitude < 0.0001f) toBubble = Vector2.down;

            var p = RentProjectile();
            p.Activate(origin, toBubble.normalized * _cfg.mantaBoltSpeed, _cfg.mantaBoltRadius);
            _live.Add(p);

            _fx.Burst(origin, Palette.MantaEye, 10, 0.7f, 2.4f, 0.04f, 0.13f);
            Noised?.Invoke(Noise.Bolt);
        }

        // ---------------------------------------------------------- проход (акт 2)

        /// <summary>
        /// Переворот перед третьим актом. Раньше туша просто меняла спрайт на брюхо
        /// и оказывалась на новом месте — выглядело подменой картинки.
        ///
        /// Теперь это движение: он уходит вверх, за кромку кадра, сжимаясь по
        /// вертикали — будто встаёт к игроку ребром, — там переворачивается и
        /// опускается обратно уже брюхом вперёд. Смена спрайта приходится ровно на
        /// момент, когда туша сплющена в полоску и различить сторону нельзя.
        /// </summary>
        void StartTurn()
        {
            _phase = Phase.Turn;
            _timer = _cfg.mantaTurnTime;
            _turnFlipped = false;
            _bodyAngle = 0f;
            _vanish = 1f;
        }

        void TickTurn(float dt)
        {
            _timer -= dt;

            float total = Mathf.Max(0.01f, _cfg.mantaTurnTime);
            float t = 1f - Mathf.Clamp01(_timer / total);

            // Первая половина — уход вверх, вторая — снижение на место третьего акта.
            float lift = Span * 0.55f;
            Vector2 low = HoverPoint();
            Vector2 high = new Vector2(low.x, _field.Top + lift);

            _position = t < 0.5f
                ? Vector2.Lerp(_position, high, 1f - Mathf.Exp(-4.5f * dt))
                : Vector2.Lerp(_position, low, 1f - Mathf.Exp(-3.2f * dt));

            if (!_turnFlipped && t >= 0.5f)
            {
                _turnFlipped = true;
                _body.sprite = SpriteFactory.MantaBelly(false);

                _fx.Burst(_position, Palette.MantaEdge, 30, 1.6f, 5f, 0.08f, 0.24f);
                Noised?.Invoke(Noise.Slam);
            }

            if (_timer <= 0f)
            {
                _turnSquash = 1f;
                StartIdle(0.7f);
            }
        }

        /// <summary>Тает в фоне: тень просто перестаёт быть, оставив муть на месте.</summary>
        void StartVanish()
        {
            _phase = Phase.Vanish;
            _timer = 1.1f;
        }

        void TickVanish(float dt)
        {
            _timer -= dt;
            _vanish = Mathf.Clamp01(_timer / 1.1f);

            // чуть отступает вверх, пока растворяется
            _position += Vector2.up * (0.7f * dt);

            if (_timer <= 0f)
            {
                _vanish = 0f;
                _fx.Burst(_position, Palette.MantaEdge, 18, 0.6f, 2.2f, 0.08f, 0.2f);
                StartLurk(UnityEngine.Random.Range(0.9f, 1.5f));
            }
        }

        /// <summary>
        /// Ждёт за кадром, невидимый. Здесь же выбирается сторона следующего захода,
        /// и туша заранее переносится на исходную — чтобы появление было именно
        /// появлением, а не выездом из-за края на глазах у игрока.
        /// </summary>
        void StartLurk(float pause)
        {
            _phase = Phase.Lurk;
            _timer = pause;
            _vanish = 0f;
            ChoosePassRoute();
            _position = _passFrom;
            _bodyAngle = Mathf.Atan2(_passDirection.y, _passDirection.x) * Mathf.Rad2Deg - 90f;
        }

        void TickLurk(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;

            _phase = Phase.Pass;
            _passProgress = 0f;
            _ramResolved = false;
            _tailShotDone = false;
            _vanish = 1f;

            _fx.Burst(_position + _passDirection * (Span * 0.3f), Palette.MantaEdge,
                      22, 1.4f, 4.4f, 0.07f, 0.2f);
            Noised?.Invoke(Noise.Slam);
        }

        /// <summary>
        /// Второй акт: он больше не висит, а сам ходит через арену — сверху вниз, снизу
        /// вверх, слева направо, справа налево, — и гонит перед собой волны.
        ///
        /// Проходит намеренно мимо пузырька, со смещением: управляет игрок только
        /// стражем, и уйти с дороги пузырьку нечем. Опасны волны, а не туша.
        /// </summary>
        /// <summary>
        /// Откуда и куда пойдёт следующий заход. Сторона каждый раз новая, поэтому
        /// угадать, откуда он выйдет, нельзя — в этом и смысл того, что между
        /// проходами его не видно.
        /// </summary>
        void ChoosePassRoute()
        {
            // Курс идёт ПРЯМО ЧЕРЕЗ пузырёк: это таран, а не проплывание мимо.
            // Отойти пузырьку некуда, игрок управляет только стражем — значит всё
            // решает, успел ли он подставить его с нужной стороны.
            float reach = _field.HalfWidth + _field.HalfHeight + Span;
            float angle = UnityEngine.Random.Range(0, 4) * 90f + UnityEngine.Random.Range(-22f, 22f);
            float rad = angle * Mathf.Deg2Rad;

            Vector2 approach = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _passFrom = _bubble.Center + approach * reach;
            _passTo = _bubble.Center - approach * reach;
            _passDirection = -approach;
        }

        void TickPass(float dt)
        {
            float route = Vector2.Distance(_passFrom, _passTo);
            if (route < 0.01f) { StartLurk(0.8f); return; }

            // Идём подшагами: на такой скорости туша за кадр перепрыгнула бы стража
            // целиком, и удар не засчитался бы вовсе.
            float advance = _cfg.mantaPassSpeed * dt / route;
            int steps = Mathf.Max(1, Mathf.CeilToInt(_cfg.mantaPassSpeed * dt / 0.25f));

            for (int step = 0; step < steps; step++)
            {
                _passProgress += advance / steps;
                _position = Vector2.Lerp(_passFrom, _passTo, Mathf.Clamp01(_passProgress));
                ResolveRam();
            }

            // Пока он идёт, из-под крыльев поднимается муть. Без неё проход
            // выглядел проездом картинки по экрану. Камера при этом неподвижна.

            Vector2 side = new Vector2(-_passDirection.y, _passDirection.x);
            for (int i = -1; i <= 1; i += 2)
            {
                if (UnityEngine.Random.value > 0.4f) continue;
                Vector2 at = _position + side * (Span * 0.30f * i)
                             - _passDirection * (Span * 0.12f);
                _fx.Emit(at + UnityEngine.Random.insideUnitCircle * (Span * 0.05f),
                         -_passDirection * UnityEngine.Random.Range(0.6f, 2.2f),
                         Palette.MantaEdge, 1.2f, UnityEngine.Random.Range(0.07f, 0.18f), 0.55f);
            }

            // Миновав пузырёк, он бьёт хвостом вдогонку. Хвост к этому моменту уже
            // по другую сторону, и страж, только что принявший таран, стоит не там —
            // его надо успеть перебросить через пузырёк.
            if (!_tailShotDone && _passProgress >= _cfg.mantaTailShotAt)
            {
                _tailShotDone = true;
                FireTailBolt();
            }

            if (_passProgress >= 1f) StartLurk(UnityEngine.Random.Range(0.9f, 1.7f));
        }

        /// <summary>
        /// Столкновение на таране. Один заход — один удар: дальше туша просто идёт
        /// своей дорогой, иначе она снимала бы жизни всеми десятью юнитами подряд.
        /// </summary>
        void ResolveRam()
        {
            if (_ramResolved || _phase == Phase.Dying) return;

            Vector2 snout = Snout;
            float radius = _cfg.mantaRamRadius;

            // Страж принял удар — единственный способ ранить его в этом акте.
            if (_guardian.CanDeflect &&
                Vector2.Distance(snout, _guardian.Center) < radius + _guardian.Radius)
            {
                _ramResolved = true;
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(_guardian.Center);

                _fx.Burst(_guardian.Center, Palette.MantaEye, 24, 1.4f, 4.4f, 0.07f, 0.2f);
                Noised?.Invoke(Noise.Slam);
                ApplyDamage(_guardian.Center);
                return;
            }

            if (Vector2.Distance(snout, _bubble.Center) < radius + _bubble.Radius)
            {
                _ramResolved = true;
                BubbleHit?.Invoke(_bubble.Center);
                Noised?.Invoke(Noise.Slam);
            }
        }

        /// <summary>
        /// Молния с хвоста вслед за тараном. Летит прямо в пузырёк и отражается
        /// стражем обратно — то есть даёт второй шанс ранить его за тот же заход,
        /// но только если игрок успел переставить стража на другую сторону.
        /// </summary>
        void FireTailBolt()
        {
            Vector2 origin = _field.ClampToScreen(TailTip, 0.4f);

            Vector2 dir = _bubble.Center - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = -_passDirection;

            var p = RentProjectile();
            p.Activate(origin, dir.normalized * _cfg.mantaBoltSpeed, _cfg.mantaBoltRadius);
            _live.Add(p);

            _fx.Burst(origin, Palette.MantaEye, 14, 0.9f, 3f, 0.05f, 0.16f);
            Noised?.Invoke(Noise.Bolt);
        }

        // -------------------------------------------------------- удар хвостом

        void StartTailStrike(int count)
        {
            _phase = Phase.TailAim;
            _burstLeft = Mathf.Max(1, count);
            _timer = _cfg.mantaTailAimTime;

            // В третьем акте хвост целится не в пузырёк, а в самого стража: это
            // проверка «уводи защиту», и мишень у неё другая.
            _tailTarget = _act == 2 ? _guardian.Center : _bubble.Center;
            _aimBeam.enabled = true;
        }

        void TickTailAim(float dt)
        {
            _timer -= dt;
            Drift(dt);

            float reveal = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.mantaTailAimTime));
            UpdateAimBeam(TailTip, _tailTarget, reveal);

            if (_timer <= 0f)
            {
                FireSpike(TailTip, _tailTarget - TailTip, _cfg.mantaTailSpeed);
                _aimBeam.enabled = false;

                if (--_burstLeft > 0)
                {
                    _phase = Phase.TailAim;
                    _timer = Spaced(Mathf.Max(_cfg.mantaTailBurstGap, _cfg.mantaTailAimTime * 0.6f));
                    _tailTarget = _act == 2 ? _guardian.Center : PointAroundBubble();
                    _aimBeam.enabled = true;
                }
                else StartIdle(UnityEngine.Random.Range(0.5f, 1.0f));
            }
        }

        /// <summary>Точка рядом с пузырьком: серия бьёт с разных сторон, а не в одну точку.</summary>
        Vector2 PointAroundBubble()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return _bubble.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.9f;
        }

        // ---------------------------------------------------------- веер шипов

        /// <summary>
        /// Веер: настоящий шип один, остальные идут мимо. Приём «Каменного панциря» —
        /// потратить перезарядку стража на обманку значит пропустить настоящий.
        /// </summary>
        void StartFan()
        {
            _phase = Phase.Fan;
            _timer = _cfg.mantaTailAimTime * 0.8f;
            _tailTarget = _bubble.Center;
            _aimBeam.enabled = false;
        }

        void TickFan(float dt)
        {
            _timer -= dt;
            Drift(dt);

            if (_timer > 0f) return;

            Vector2 origin = TailTip;
            Vector2 straight = _bubble.Center - origin;
            int count = Mathf.Max(1, _cfg.mantaFanCount);
            int real = count / 2;

            for (int i = 0; i < count; i++)
            {
                float offset = (i - real) * _cfg.mantaFanSpread;
                Vector2 dir = i == real
                    ? straight
                    : (Vector2)(Quaternion.Euler(0f, 0f, offset) * straight);
                FireSpike(origin, dir, _cfg.mantaTailSpeed * 0.85f);
            }

            StartIdle(UnityEngine.Random.Range(0.7f, 1.2f));
        }

        // ------------------------------------------------------------ затмение

        /// <summary>
        /// Крыло накрывает экран. Приём «Чернильного морока», но мягче: стража он не
        /// забрасывает, зато разряды продолжают лететь в темноте — это проверка на то,
        /// помнит ли игрок, где что было.
        /// </summary>
        void StartEclipse()
        {
            _phase = Phase.Eclipse;
            _timer = _cfg.mantaEclipseHold;
            _eclipseHold = _cfg.mantaEclipseHold;
        }

        void TickEclipsePhase(float dt)
        {
            _timer -= dt;
            Drift(dt);
            if (_timer <= 0f) StartIdle(0.6f);
        }

        /// <summary>За сколько крыло накрывает экран, с. Было 0.2 — ступенька на весь экран (E-11).</summary>
        const float EclipseRise = 0.5f;

        void TickEclipse(float dt)
        {
            const float full = 0.92f;
            float target = _eclipseHold > 0f ? full : 0f;
            if (_eclipseHold > 0f) _eclipseHold -= dt;

            float rate = target > _eclipseLevel
                ? full / EclipseRise
                : 1f / Mathf.Max(0.05f, _cfg.mantaEclipseFade);
            _eclipseLevel = Mathf.MoveTowards(_eclipseLevel, target, rate * dt);

            if (_eclipseLevel <= 0.001f)
            {
                _eclipse.enabled = false;
                return;
            }

            _eclipse.enabled = true;
            _eclipse.color = new Color(0.02f, 0.03f, 0.05f, _eclipseLevel);
            _eclipse.transform.position = new Vector3(0f, _field.Cam.transform.position.y, 0f);
            _eclipse.transform.localScale = new Vector3(_field.HalfWidth * 2.4f, _field.HalfHeight * 2.4f, 1f);
        }

        // ------------------------------------------------------------ выводок

        void StartPups()
        {
            _phase = Phase.Pups;
            _timer = 1.4f;

            int count = Mathf.Max(1, _cfg.mantaPupCount);
            bool fromLeft = UnityEngine.Random.value < 0.5f;
            float edge = (_field.HalfWidth + 1.2f) * (fromLeft ? -1f : 1f);

            // Выходят клином — строй читается сразу, как у стаи пираний.
            for (int i = 0; i < count; i++)
            {
                var pup = RentPup();
                float row = i * 0.55f;
                pup.Position = new Vector2(edge - (fromLeft ? row : -row),
                                           _bubble.Center.y + 2.4f - Mathf.Abs(i - count * 0.5f) * 0.5f);

                Vector2 aim = (_bubble.Center - pup.Position).normalized;
                pup.Velocity = aim * _cfg.mantaPupSpeed;
                pup.Alive = true;
                pup.Sr.enabled = true;
            }
        }

        void TickPupsPhase(float dt)
        {
            _timer -= dt;
            Drift(dt);
            if (_timer <= 0f) StartIdle(0.7f);
        }

        // ------------------------------------------------------------- эффекты

        /// <summary>
        /// След от крыльев и горящие глаза — всё, что делает его живым, пока он никого
        /// не атакует. Без этого скат в фоне выглядел неподвижной декорацией: взмах
        /// заметен только по сжатию спрайта, а этого мало на таком размере.
        /// </summary>
        void TickWake(float dt)
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) { HideEyes(); return; }

            float half = Span * BodyScale * 0.5f;

            // Глаза видны, пока он не перевернулся брюхом (третий акт) и пока он
            // вообще здесь: растворившись перед заходом, он не светит ничем.
            bool eyesVisible = _act != 2 && _vanish > 0.05f;
            for (int i = 0; i < _eyes.Length; i++)
            {
                var sr = _eyes[i];
                sr.enabled = eyesVisible;
                if (!eyesVisible) continue;

                float side = i == 0 ? -1f : 1f;
                sr.transform.position = BodyPoint(new Vector2(side * 0.085f, 0.30f));

                // Пульс медленный и неровный: ровное мигание читалось бы индикатором.
                float pulse = 0.72f + 0.28f * Mathf.Sin(_flap * Mathf.PI * 2f * 1.6f + i * 0.7f);

                // Ореол соразмерен глазу, а не туше: со свечением в треть полуразмаха
                // огни выходили больше самого пузырька и читались двумя фонарями,
                // а не взглядом.
                float size = half * 0.115f * (0.85f + 0.15f * pulse);
                sr.transform.localScale = new Vector3(size, size, 1f);
                sr.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * pulse);
            }

            // Муть срывается с кромок крыльев на нижней точке взмаха — один раз за цикл,
            // а не каждый кадр: иначе из частиц получается сплошная каша.
            float phase = Mathf.Repeat(_flap, 1f);
            if (phase < _lastFlapPhase && _phase != Phase.Pass && _vanish > 0.5f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 tip = _position + new Vector2(side * half * 0.88f, -half * 0.10f);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 drift = new Vector2(side * UnityEngine.Random.Range(0.2f, 1.1f),
                                                    UnityEngine.Random.Range(-1.4f, -0.5f));
                        _fx.Emit(tip + UnityEngine.Random.insideUnitCircle * (half * 0.12f),
                                 drift, Palette.MantaEdge, 1.5f,
                                 UnityEngine.Random.Range(0.06f, 0.16f), 0.5f,
                                 SortingOrders.Silhouette + 1);
                    }
                }
            }
            _lastFlapPhase = phase;
        }

        void HideEyes()
        {
            if (_eyes == null) return;
            for (int i = 0; i < _eyes.Length; i++)
                if (_eyes[i] != null) _eyes[i].enabled = false;
        }

        void TickPups(float dt)
        {
            for (int i = 0; i < _pups.Count; i++)
            {
                var pup = _pups[i];
                if (!pup.Alive) continue;

                pup.Position += pup.Velocity * dt;

                if (_guardian.CanDeflect &&
                    Vector2.Distance(pup.Position, _guardian.Center) < _cfg.mantaPupSize + _guardian.Radius)
                {
                    _guardian.NotifyDeflect();
                    GuardianBlocked?.Invoke(pup.Position);
                    KillPup(pup);
                    continue;
                }

                if (Vector2.Distance(pup.Position, _bubble.Center) < _cfg.mantaPupSize + _bubble.Radius)
                {
                    BubbleHit?.Invoke(pup.Position);
                    KillPup(pup);
                    continue;
                }

                if (Mathf.Abs(pup.Position.x) > _field.HalfWidth + 2f ||
                    pup.Position.y < _field.Bottom - 2f || pup.Position.y > _field.Top + 2f)
                {
                    KillPup(pup);
                    continue;
                }

                pup.Sr.transform.position = pup.Position;
                float angle = Mathf.Atan2(pup.Velocity.y, pup.Velocity.x) * Mathf.Rad2Deg - 90f;
                pup.Sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                pup.Sr.transform.localScale = Vector3.one * (_cfg.mantaPupSize * 4f);
            }
        }

        Pup RentPup()
        {
            for (int i = 0; i < _pups.Count; i++)
                if (!_pups[i].Alive) return _pups[i];

            var go = new GameObject("MantaPup");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.MantaPup();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.Fish + 1;

            var pup = new Pup { Sr = sr };
            _pups.Add(pup);
            return pup;
        }

        void KillPup(Pup pup)
        {
            pup.Alive = false;
            if (pup.Sr != null) pup.Sr.enabled = false;
        }

        void ClearPups()
        {
            for (int i = 0; i < _pups.Count; i++) KillPup(_pups[i]);
        }

        // ------------------------------------------------------------- разряды

        /// <summary>
        /// Разряды идут фоном, независимо от того, чем он занят прямо сейчас. Это и
        /// есть единственный источник урона по нему: страж отражает молнию обратно.
        /// </summary>
        void TickBolts(float dt)
        {
            _boltTimer -= dt;
            if (_boltTimer > 0f) return;

            float t = _act / 2f;
            _boltTimer = Mathf.Lerp(_cfg.mantaBoltGapMax, _cfg.mantaBoltGapMin, t);

            // Бьёт с кромки крыла, но точку подтягиваем к экрану: у выросшей туши
            // кромки уходят далеко за кадр, и молния просто возникала бы из ниоткуда
            // уже на подлёте к пузырьку.
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Vector2 origin = _field.ClampToScreen(
                _position + new Vector2(side * Span * BodyScale * 0.34f, -Span * BodyScale * 0.06f),
                0.4f);

            Vector2 dir = _bubble.Center - origin;

            var p = RentProjectile();
            p.Activate(origin, dir.normalized * _cfg.mantaBoltSpeed, _cfg.mantaBoltRadius);
            _live.Add(p);
        }

        void FireSpike(Vector2 origin, Vector2 direction, float speed)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;

            var p = RentProjectile();
            p.Activate(origin, direction.normalized * speed, _cfg.mantaTailRadius);
            p.SetLook(SpriteFactory.MantaSpike(), Color.white, 5.5f);
            _spikes.Add(p);
            _live.Add(p);
        }

        bool IsSpike(BossProjectile p) => _spikes.Contains(p);

        void TickProjectiles(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (i >= _live.Count) continue;

                var p = _live[i];
                bool spike = IsSpike(p);

                if (p.Reflected && _phase != Phase.Dying)
                {
                    // В первом акте доводка почти выключена: снаряд летит туда, куда
                    // его послал страж, а не туда, куда надо игре.
                    float homing = _act == 0 ? _cfg.mantaMirrorHoming : _cfg.mantaReflectHoming;

                    // В первом акте доводим на то ядро, что ближе к самому снаряду:
                    // иначе молния, отражённая к левому крылу, тянулась бы к правому.
                    Vector2 aim = ReflectTarget();
                    if (_act == 0)
                    {
                        int core = NearestCore(p.Center);
                        if (core >= 0) aim = CorePosition(core);
                    }

                    p.SteerTowards(aim, homing, dt);
                }
                else if (!p.Reflected && _splitters.Contains(p) && p.Age >= _cfg.mantaSplitDelay)
                {
                    // Дозрел и разлетелся: сам снаряд снимаем, вместо него три осколка.
                    _splitters.Remove(p);
                    _live.RemoveAt(i);
                    SplitOrb(p);
                    RecycleProjectile(p);
                    continue;
                }
                else if (!p.Reflected && !spike && _act == 0)
                {
                    // Молнии первого акта летят строго по прямой — никакого доворота.
                    // Разброс углов даёт разнос точек вылета по крыльям и снос самой
                    // туши; всё, что пробовалось кривыми траекториями, либо кружило
                    // вокруг пузырька, либо уходило мимо.
                }

                bool alive = p.Tick(dt, _field);

                if (alive && !p.Reflected)
                {
                    bool caught = _guardian.CanDeflect &&
                                  Vector2.Distance(p.Center, _guardian.Center) < p.Radius + _guardian.Radius;

                    if (caught)
                    {
                        _guardian.NotifyDeflect();
                        GuardianBlocked?.Invoke(p.Center);

                        bool charged = _charged.Contains(p);

                        // Шар гаснет на месте, а не отскакивает: заряженный при этом
                        // отдаёт разряд обратно в ската, обычный — просто исчезает.
                        if (charged)
                        {
                            _fx.Burst(p.Center, Palette.MantaGlow, 24, 1.2f, 4f, 0.05f, 0.18f);
                            Noised?.Invoke(Noise.Gill);
                            ApplyDamage(p.Center);
                            alive = false;
                        }
                        // Шип отражать нельзя — он костяной, его можно только принять
                        // на стража. Отражается только разряд, и в этом вся разница.
                        else if (spike || _act == 2) alive = false;
                        else p.Reflect(ReflectDirection(p), _cfg.mantaReflectSpeed);
                    }
                    else if (Vector2.Distance(p.Center, _bubble.Center) < p.Radius + _bubble.Radius)
                    {
                        BubbleHit?.Invoke(p.Center);
                        alive = false;
                    }
                }
                else if (alive && p.Reflected && _phase != Phase.Dying)
                {
                    if (ReflectedHits(p)) alive = false;
                }

                if (!alive)
                {
                    int idx = _live.IndexOf(p);
                    if (idx >= 0) _live.RemoveAt(idx);
                    _spikes.Remove(p);
                    _charged.Remove(p);
                    _splitters.Remove(p);
                    RecycleProjectile(p);
                }
            }

            if (_phase == Phase.Dying && _live.Count > 0) ClearProjectiles();
        }

        /// <summary>
        /// Куда уходит отражённый снаряд.
        ///
        /// В первом акте — по закону зеркала: отскок считается от нормали в точке
        /// касания стража, и попадание целиком на совести игрока. В остальных актах
        /// снаряд направляется в цель сам: там скат либо сам движется поперёк, либо
        /// подставляет узкую щель, и требовать ещё и точного угла было бы слишком.
        /// </summary>
        Vector2 ReflectDirection(BossProjectile p)
        {
            if (_act == 0)
            {
                Vector2 normal = p.Center - _guardian.Center;
                if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;

                Vector2 bounced = Vector2.Reflect(p.Velocity.normalized, normal.normalized);
                if (bounced.sqrMagnitude > 0.0001f) return bounced;
            }

            Vector2 toBoss = ReflectTarget() - p.Center;
            return toBoss.sqrMagnitude > 0.0001f ? toBoss : Vector2.up;
        }

        /// <summary>Куда летит отражённый разряд: в ядро, если оно горит, иначе в тушу.</summary>
        Vector2 ReflectTarget()
        {
            if (_act == 0)
            {
                int core = NearestCore(_position);
                if (core >= 0) return CorePosition(core);
            }
            return _position;
        }

        /// <summary>
        /// Попал ли отражённый разряд. В первом акте — только по ядру, во втором
        /// достаточно задеть тушу.
        ///
        /// В третьем отражение не даёт ничего: там урон наносит сам страж, касаясь
        /// щелей, а разряды — только помеха. Прилетевший в брюхо разряд гасим с
        /// искрами, чтобы было видно, что он во что-то попал, а не исчез сам.
        /// </summary>
        bool ReflectedHits(BossProjectile p)
        {

            // Первый акт: попадание засчитывается только по ядру. Иначе достаточно
            // было держать стража на пути разряда — ровно как у грозовой медузы.
            if (_act == 0)
            {
                for (int i = 0; i < _cores.Length; i++)
                {
                    if (!_coreLit[i]) continue;
                    if (Vector2.Distance(p.Center, CorePosition(i)) > p.Radius + _cfg.mantaCoreRadius)
                        continue;

                    _fx.Burst(p.Center, Palette.MantaGlow, 22, 1.1f, 3.8f, 0.05f, 0.18f);
                    Noised?.Invoke(Noise.Gill);
                    ApplyDamage(p.Center);
                    HideCore(i, _cfg.mantaCoreHideTime);
                    return true;
                }
                return false;
            }

            // Туша огромная, но засчитываем попадание не по всему обводу, а по
            // центральному телу: иначе разряд «попадал» бы в кончик крыла за экраном.
            if (Vector2.Distance(p.Center, _position) > Span * BodyScale * 0.30f) return false;

            ApplyDamage(p.Center);
            return true;
        }

        // -------------------------------------------------- хвост и шары (акт 3)

        /// <summary>
        /// Третий акт устроен на двух приёмах, и оба нарочно крупные и медленные:
        /// туша занимает половину кадра, и всё мелкое рядом с ней просто теряется.
        ///
        /// **Размах хвоста.** Плеть поднимается, наливается светом — и проходит дугой
        /// прямо через пузырёк. Страж должен встретить жало: принял удар — скат ранен.
        ///
        /// **Град шаров.** С брюха срываются медленные светящиеся сферы. Гасятся
        /// касанием стража, но часть из них заряжена — заряженный шар, погашенный
        /// стражем, уходит обратно в ската и тоже снимает HP.
        ///
        /// Вместе это и даёт комбинацию: пока страж разбирает град, хвост уже
        /// замахивается, и приходится решать, что важнее прямо сейчас.
        /// </summary>
        void StartLashWindup()
        {
            _phase = Phase.LashWindup;
            _timer = _cfg.mantaLashWindup;
            _lashResolved = false;

            // Дуга строится вокруг направления на пузырёк, а не вокруг «вниз»:
            // скат сносит вбок, и при жёстко заданном направлении хвост размахивал
            // мимо цели тем сильнее, чем дальше он уплыл.
            Vector2 toBubble = _bubble.Center - LashRoot();
            float centre = toBubble.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(toBubble.y, toBubble.x) * Mathf.Rad2Deg
                : -90f;

            // Заходит с той стороны, где сейчас страж: удар должен требовать движения,
            // а не заставать его уже на месте.
            float side = _guardian.Center.x >= _bubble.Center.x ? 1f : -1f;
            float half = _cfg.mantaLashArc * 0.5f;

            _lashFrom = centre + side * half;
            _lashTo = centre - side * half;
            _lashAngle = _lashFrom;
            _lashProgress = 0f;
            _lashLength = toBubble.magnitude * _cfg.mantaLashReach;
        }

        void TickLashWindup(float dt)
        {
            _timer -= dt;
            Drift(dt);

            float reveal = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.mantaLashWindup));
            DrawLash(_lashAngle, reveal, lit: true);

            if (_timer <= 0f)
            {
                _phase = Phase.LashSweep;
                _timer = _cfg.mantaLashSweep;
                Noised?.Invoke(Noise.Slam);
            }
        }

        void TickLashSweep(float dt)
        {
            _timer -= dt;
            Drift(dt);

            float t = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.mantaLashSweep));

            // Подшаги от прошлой позиции до текущей. Раньше здесь по недосмотру все
            // четыре шага считались в одной и той же точке, и жало на скорости
            // перепрыгивало стража целиком — удар просто не засчитывался.
            const int Steps = 6;
            for (int i = 1; i <= Steps; i++)
            {
                float sub = Mathf.Lerp(_lashProgress, t, i / (float)Steps);
                _lashAngle = Mathf.Lerp(_lashFrom, _lashTo, Mathf.SmoothStep(0f, 1f, sub));
                ResolveLash();
            }
            _lashProgress = t;

            DrawLash(_lashAngle, 1f, lit: true);

            if (_timer <= 0f)
            {
                _lash.enabled = false;
                StartIdle(UnityEngine.Random.Range(0.5f, 0.9f));
            }
        }

        /// <summary>Конец плети в мире по текущему углу дуги.</summary>
        Vector2 LashTip(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return LashRoot() + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _lashLength;
        }

        /// <summary>
        /// Основание плети. Крепится недалеко от центра туши: у нижней кромки оно
        /// опускалось почти на сам пузырёк, расстояние между ними выходило меньше
        /// юнита — и плеть получалась короче пузырька, её просто не было видно.
        /// </summary>
        Vector2 LashRoot() => BodyPoint(new Vector2(0f, -_cfg.mantaLashRootSpan));

        /// <summary>Расстояние от точки до отрезка — плеть задевает всем телом, не только жалом.</summary>
        static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 axis = to - from;
            float length2 = axis.sqrMagnitude;
            if (length2 < 0.0001f) return Vector2.Distance(point, from);

            float t = Mathf.Clamp01(Vector2.Dot(point - from, axis) / length2);
            return Vector2.Distance(point, from + axis * t);
        }

        void ResolveLash()
        {
            if (_lashResolved || _phase == Phase.Dying) return;

            Vector2 tip = LashTip(_lashAngle);

            // Считаем по всей плети, а не по кончику жала. Радиус дуги равен расстоянию
            // до пузырька, поэтому страж, поставленный ближе к скату — а это
            // естественная позиция прикрытия, — оказывался внутри дуги, и жало
            // проходило дальше него. Блок при этом не засчитывался, хотя плеть
            // физически шла сквозь стража.
            if (_guardian.CanDeflect &&
                DistanceToSegment(_guardian.Center, LashRoot(), tip)
                    < _cfg.mantaLashRadius + _guardian.Radius)
            {
                _lashResolved = true;
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(_guardian.Center);

                _fx.Burst(_guardian.Center, Palette.MantaSpine, 26, 1.3f, 4.2f, 0.06f, 0.2f);
                Noised?.Invoke(Noise.Gill);
                ApplyDamage(_guardian.Center);
                return;
            }

            if (DistanceToSegment(_bubble.Center, LashRoot(), tip)
                    < _cfg.mantaLashRadius + _bubble.Radius)
            {
                _lashResolved = true;
                BubbleHit?.Invoke(_bubble.Center);
                Noised?.Invoke(Noise.Slam);
            }
        }

        /// <summary>Рисует плеть от основания хвоста до жала.</summary>
        void DrawLash(float angle, float intensity, bool lit)
        {
            Vector2 root = LashRoot();
            Vector2 tip = LashTip(angle);
            Vector2 delta = tip - root;

            float length = delta.magnitude;
            if (length < 0.2f) { _lash.enabled = false; return; }

            _lash.enabled = true;
            _lash.sprite = SpriteFactory.MantaLash(lit);
            _lash.transform.position = root + delta * 0.5f;
            _lash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // Спрайт вытянут 8:1: при scale = 1 его высота равна 1/8 ширины, и сама
            // плеть занимает по высоте около 85% спрайта. Отсюда и пересчёт нужной
            // толщины в масштаб — задавать его на глаз бессмысленно, он зависит от
            // пропорций текстуры.
            float thickness = _cfg.mantaLashThickness / (0.125f * 0.85f);
            _lash.transform.localScale = new Vector3(length, thickness, 1f);
            _lash.color = new Color(1f, 1f, 1f, 0.45f + 0.55f * Mathf.Clamp01(intensity));
        }

        // ---------------------------------------------------------------- град

        void StartOrbRain()
        {
            _phase = Phase.OrbRain;
            _orbsLeft = Mathf.Max(1, _cfg.mantaOrbCount);
            _orbTimer = 0.2f;
            _timer = Spaced(_cfg.mantaOrbGap) * _orbsLeft + 1.6f;
        }

        void TickOrbRain(float dt)
        {
            _timer -= dt;
            Drift(dt);

            _orbTimer -= dt;
            if (_orbTimer <= 0f && _orbsLeft > 0)
            {
                _orbsLeft--;

                // Вид выбирается на каждый заход: три разряда с разными повадками
                // заставляют читать, что именно летит, а не отбивать вслепую.
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0: DropOrb(); _orbTimer = Spaced(_cfg.mantaOrbGap); break;
                    case 1: DropChain(); _orbTimer = Spaced(_cfg.mantaOrbGap * 2.2f); break;
                    default: DropSplitter(); _orbTimer = Spaced(_cfg.mantaOrbGap * 1.6f); break;
                }
            }

            if (_timer <= 0f) StartIdle(UnityEngine.Random.Range(0.8f, 1.3f));
        }

        /// <summary>
        /// Откуда выходит разряд. Не из брюха, а из воды вокруг пузырька — по кольцу,
        /// под любым углом, включая нижние.
        ///
        /// Разряды, падавшие только сверху, позволяли держать стража в одной точке
        /// весь акт: всё, что летело, приходило по одной и той же линии. С кольцом
        /// приходится следить за всеми сторонами сразу.
        /// </summary>
        Vector2 OrbOrigin()
        {
            float ring = _field.PlayHalfWidth * 2f * _cfg.mantaOrbRingSpan;

            // Сектор снизу исключён. Разряд, заходящий строго из-под пузырька, сам
            // пузырёк и закрывает: заметить его удавалось в лучшем случае на подлёте,
            // и отбить это было не мастерством, а удачей. Сверху и с боков — сколько
            // угодно, там видно всё.
            float angle = UnityEngine.Random.Range(-35f, 215f) * Mathf.Deg2Rad;

            Vector2 raw = _bubble.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
            return _field.ClampToScreen(raw, 0.5f);
        }

        /// <summary>
        /// Цепь: несколько искр по одной линии, очередью. Быстрые и мелкие, а заряжена
        /// только последняя — растратив перезарядку стража на первые, до неё уже не
        /// успеть, и вся цепь пройдёт впустую.
        /// </summary>
        void DropChain()
        {
            Vector2 origin = OrbOrigin();
            Vector2 dir = _bubble.Center - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            dir.Normalize();

            int links = Mathf.Max(2, _cfg.mantaChainLinks);

            // Расстояние между искрами задаётся не напрямую, а через время, которое
            // стражу нужно на перезарядку: цепь, летящая чаще, была непробиваемой —
            // две искры из четырёх доходили до пузырька при любой игре.
            float step = Spaced(_cfg.mantaChainGap) * _cfg.mantaChainSpeed;

            for (int i = 0; i < links; i++)
            {
                // Разносим звенья по линии, а не по времени: так вся цепь видна сразу
                // и понятно, сколько их ещё летит.
                Vector2 at = origin - dir * (i * step);
                bool last = i == links - 1;

                var p = RentProjectile();
                p.Activate(at, dir * _cfg.mantaChainSpeed, _cfg.mantaChainRadius);
                p.SetLook(SpriteFactory.MantaSpark(),
                          last ? Palette.MantaGlow : Color.white, 4.5f);

                _live.Add(p);
                if (last) _charged.Add(p);
            }

            _fx.Burst(origin, Palette.MantaEye, 10, 0.6f, 2.2f, 0.04f, 0.12f);
            Noised?.Invoke(Noise.Bolt);
        }

        /// <summary>
        /// Раскольник: тяжёлый снаряд, который на полпути разлетается на три осколка.
        /// Сам он для ската безобиден — урон дают только осколки, и чтобы до них
        /// добраться, надо дать ему расколоться, а не гасить сразу.
        /// </summary>
        void DropSplitter()
        {
            Vector2 origin = OrbOrigin();
            Vector2 dir = _bubble.Center - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;

            var p = RentProjectile();
            p.Activate(origin, dir.normalized * _cfg.mantaSplitSpeed, _cfg.mantaSplitRadius);
            p.SetLook(SpriteFactory.MantaSplitter(), Color.white, 2.4f);

            _live.Add(p);
            _splitters.Add(p);

            _fx.Burst(origin, Palette.MantaSpine, 12, 0.7f, 2.6f, 0.05f, 0.15f);
            Noised?.Invoke(Noise.Bolt);
        }

        /// <summary>Разлёт раскольника на три заряженных осколка.</summary>
        void SplitOrb(BossProjectile source)
        {
            Vector2 at = source.Center;
            Vector2 dir = source.Velocity.sqrMagnitude > 0.0001f
                ? source.Velocity.normalized : Vector2.down;

            for (int i = -1; i <= 1; i++)
            {
                Vector2 shard = (Quaternion.Euler(0f, 0f, i * _cfg.mantaSplitSpread) * dir);

                var p = RentProjectile();
                p.Activate(at, shard * _cfg.mantaOrbSpeed * 1.4f, _cfg.mantaOrbRadius * 0.7f);
                p.SetLook(SpriteFactory.MantaOrb(true), Color.white, 2.6f);

                _live.Add(p);
                _charged.Add(p);       // осколки заряжены все
            }

            _fx.Burst(at, Palette.MantaGlow, 18, 1f, 3.4f, 0.05f, 0.16f);
            Noised?.Invoke(Noise.Bolt);
        }

        /// <summary>
        /// Сбросить шар. Заряженные отличаются цветом и ядром внутри: погасив такой,
        /// страж отправляет разряд обратно, и это единственный урон, который даёт град.
        /// </summary>
        void DropOrb()
        {
            float half = Span * BodyScale * 0.5f;
            Vector2 origin = _position + new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f) * half,
                                                     -half * 0.12f);
            origin = _field.ClampToScreen(origin, 0.5f);

            Vector2 dir = _bubble.Center - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;

            bool charged = UnityEngine.Random.value < _cfg.mantaOrbChargedShare;

            var p = RentProjectile();
            p.Activate(origin, dir.normalized * _cfg.mantaOrbSpeed, _cfg.mantaOrbRadius);
            p.SetLook(SpriteFactory.MantaOrb(charged), Color.white, 2.6f);

            _live.Add(p);
            if (charged) _charged.Add(p);

            _fx.Burst(origin, charged ? Palette.MantaGlow : Palette.MantaEye, 8, 0.5f, 1.8f, 0.04f, 0.12f);
            Noised?.Invoke(Noise.Bolt);
        }

        // --------------------------------------------------------------- урон

        void ApplyDamage(Vector2 at)
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(at);

            if (Hp <= 0)
            {
                _phase = Phase.Dying;
                _timer = 0f;
                _aimBeam.enabled = false;

                // Добить его могла и отражённая волна, догнавшая уже растворившуюся
                // тушу. Гибель обязана быть видимой, поэтому проявляем его обратно.
                _vanish = 1f;
                _eclipseHold = 0f;
                if (_lash != null) _lash.enabled = false;
                ClearPups();
                return;
            }

            int next = ActFromHp;
            if (next != _act) EnterAct(next);
        }

        /// <summary>
        /// Смена акта. Он подбирается ближе, меняет слой и переворачивается брюхом —
        /// это должно быть событием, а не тихим переключением флага.
        /// </summary>
        void EnterAct(int act)
        {
            _act = act;
            _actFlashTimer = 0.6f;

            // Второй акт разворачивает тушу по курсу и прячет её между заходами.
            // Возвращаясь к обычному поведению, оба состояния надо снять: иначе он
            // остаётся лежать боком или не проявляется вовсе.
            if (act != 1)
            {
                _bodyAngle = 0f;
                _vanish = 1f;
            }

            // Спрайт на брюхо меняем не здесь, а посреди переворота: до него туша
            // должна доплыть спиной, иначе он перещёлкивается прямо на глазах.
            if (act != 2) _body.sprite = SpriteFactory.MantaBack(false);

            if (_lash != null) _lash.enabled = false;
            HideCores();
            ClearProjectiles();
            _aimBeam.enabled = false;

            _fx.Burst(_position, Palette.MantaEye, 34, 1.4f, 4.6f, 0.07f, 0.22f);
            ActChanged?.Invoke(act);

            // Во второй акт он не переходит на глазах: сначала растворяется в фоне и
            // только потом заходит из-за кадра. Игрок не должен видеть, как он
            // разворачивается и разгоняется, — весь смысл в неожиданности захода.
            if (act == 1) StartVanish();
            else if (act == 2) StartTurn();
            else StartIdle(1.0f);
        }

        void TickDying(float dt)
        {
            _timer += dt;

            // Тонет: уходит вниз, заваливаясь набок, и медленно растворяется.
            _position += Vector2.down * (1.6f * dt);
            _eclipseHold = 0f;

            float fade = Mathf.Clamp01(1f - _timer / 2.2f);
            _body.color = new Color(1f, 1f, 1f, fade);

            if (_timer >= 2.2f)
            {
                _phase = Phase.Done;
                Defeated?.Invoke(_position);
                Despawn();
            }
        }

        // ------------------------------------------------------- вспомогательное

        /// <summary>
        /// Положение, масштаб и взмах крыльев. Взмах делается сжатием по горизонтали:
        /// вид сверху, крылья уходят вниз и обратно, и в проекции это именно сужение.
        /// </summary>
        void ApplyBody()
        {
            if (_actFlashTimer > 0f) _actFlashTimer -= Time.deltaTime;

            transform.position = _position;

            // В перевороте туша сплющивается по вертикали и распрямляется обратно:
            // именно в этот момент подменяется спрайт, и подмену не видно.
            if (_phase == Phase.Turn)
            {
                float t = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.mantaTurnTime));
                _turnSquash = Mathf.Max(0.08f, Mathf.Abs(Mathf.Cos(t * Mathf.PI)));
            }
            else _turnSquash = Mathf.MoveTowards(_turnSquash, 1f, Time.deltaTime * 2.5f);

            float flap = Mathf.Sin(_flap * Mathf.PI * 2f);
            float width = Span * BodyScale * (1f - 0.07f * Mathf.Abs(flap));
            float height = Span * BodyScale * (1f + 0.05f * flap) * _turnSquash;

            _body.transform.position = _position;
            _body.transform.rotation = Quaternion.Euler(0f, 0f, _bodyAngle);
            _body.transform.localScale = new Vector3(width, height, 1f);

            // В фоне он за кораллами и полупрозрачен — тень, а не рыба. Выйдя на
            // игровой слой, становится плотным: это и есть главный сигнал смены акта.
            _body.sortingOrder = _act == 0 ? SortingOrders.Silhouette : SortingOrders.Boss;

            if (_phase != Phase.Dying)
            {
                float alpha = _act == 0 ? _cfg.mantaBackgroundAlpha : 1f;
                if (_actFlashTimer > 0f) alpha = Mathf.Lerp(alpha, 1f, _actFlashTimer / 0.6f);
                _body.color = new Color(1f, 1f, 1f, alpha * _vanish);
            }

            // Пока он растворён, его нет совсем: невидимая туша не должна ни
            // перехватывать взгляд краем спрайта, ни светить глазами из-за кадра.
            _body.enabled = _vanish > 0.02f;
        }

        void UpdateAimBeam(Vector2 from, Vector2 to, float intensity)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.25f) { _aimBeam.enabled = false; return; }

            _aimBeam.enabled = true;
            _aimBeam.transform.position = from + delta * 0.5f;
            _aimBeam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _aimBeam.transform.localScale = new Vector3(distance, 0.45f + 0.35f * intensity, 1f);
            _aimBeam.color = Palette.Coral.WithAlpha(0.16f + 0.42f * intensity);
        }

        BossProjectile RentProjectile()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }

            var go = new GameObject("MantaProjectile");
            go.transform.SetParent(_projectileRoot, false);
            var proj = go.AddComponent<BossProjectile>();
            proj.Setup();
            return proj;
        }

        void RecycleProjectile(BossProjectile p)
        {
            if (p == null || p.Pooled) return;
            p.Pooled = true;
            p.Deactivate();
            _pool.Push(p);
        }

        /// <summary>
        /// Снимаем с арены всё, что летит, и возвращаем ската к спокойному кружению.
        /// Акт и его здоровье не трогаем — бой продолжается с того же места.
        /// </summary>
        public void Interrupt()
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;

            ClearProjectiles();
            ClearPups();
            _guardian.Release();

            if (_lash != null) _lash.enabled = false;
            _aimBeam.enabled = false;
            _eclipseHold = 0f;

            StartIdle(0.8f);
        }

        void ClearProjectiles()
        {
            for (int i = 0; i < _live.Count; i++) RecycleProjectile(_live[i]);
            _live.Clear();
            _spikes.Clear();
            _charged.Clear();
            _splitters.Clear();
        }

        public void Despawn()
        {
            _phase = Phase.Done;
            ClearProjectiles();
            ClearPups();
            if (_lash != null) _lash.enabled = false;
            HideEyes();
            HideCores();

            if (_aimBeam != null) _aimBeam.enabled = false;
            if (_eclipse != null) _eclipse.enabled = false;
            _eclipseLevel = 0f;
            _eclipseHold = 0f;

            gameObject.SetActive(false);
        }
    }
}
