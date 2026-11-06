using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubbleRise
{
    /// <summary>
    /// Страж — отдельный шарик-защитник, который игрок перетаскивает мышью или пальцем
    /// в любую точку экрана. Отпустил — страж плавно возвращается к пузырьку.
    /// Здоровья у стража нет, урон он не получает: это чистый инструмент защиты.
    /// </summary>
    public class GuardianOrb : MonoBehaviour
    {
        public float Radius { get; private set; }

        /// <summary>
        /// Радиус, которым страж достаёт препятствия. Чуть больше рисунка.
        ///
        /// Понадобился после уменьшения пары на пятую часть. Расчёт
        /// (Tools/check_size.py) показал неожиданное: уворот стал легче на 9.8 %,
        /// а вот перехват — труднее на 12.2 %, то есть игра в сумме стала не
        /// легче, а сложнее. Страж — единственное, чем игрок управляет, и
        /// отнимать у него пятую часть досягаемости без компенсации значит
        /// наказывать за правку, которую он не просил.
        ///
        /// Прибавка идёт в пользу игрока, как и уменьшенный хитбокс пузырька:
        /// видимое и засчитанное расходятся там, где это помогает.
        /// </summary>
        public float Reach => Radius * Difficulty.Current.guardianReachScale;
        public bool IsDragging { get; private set; }
        public Vector2 Center => transform.position;

        /// <summary>Пока выключено — страж просто держится у пузырька и не реагирует на указатель
        /// (стартовый экран, пауза, экран поражения). Выключение отпускает стража
        /// сразу, а не в следующем Tick: на паузе Tick не идёт вовсе.</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (_inputEnabled == value) return;
                _inputEnabled = value;
                if (!value) DropGrip(GripBreak.InputOff);
                else RequireFreshPress();
            }
        }
        bool _inputEnabled = true;

        /// <summary>
        /// Дев-панель и её кнопка, экранные пиксели (начало снизу слева). Касание,
        /// начатое здесь, стража не хватает; захват, начатый в игре, сюда заходит
        /// свободно.
        /// </summary>
        public Rect DevUiRect { get; set; }

        /// <summary>
        /// Проверка «касание над интерфейсом» вместо EventSystem — для тестов.
        /// null — луч EventSystem по интерактивным элементам.
        /// </summary>
        public static System.Func<Vector2, bool> UiHitTest;

        /// <summary>Почему страж ушёл домой, хотя палец, по всем признакам, был на экране.</summary>
        public enum GripBreak { None, Canceled, Vanished, EndedRetouch, InputOff, Reset }

        /// <summary>
        /// Разрывы захвата для дев-панели: сколько раз страж уходил домой, пока
        /// палец, по всем признакам, ещё был на экране, почему в последний раз и
        /// когда (Time.unscaledTime). Подхваты — сколько раз касание пропадало и
        /// находилось в пределах допуска, и через сколько миллисекунд. Текст
        /// причин — в DevPanel: в игре строк для игрока здесь нет.
        /// </summary>
        public int GripBreaks { get; private set; }
        public GripBreak LastGripBreak { get; private set; }
        public float LastGripBreakAt { get; private set; }
        public int LastGripBreakMs { get; private set; }
        public int GripRescues { get; private set; }
        public PointerInput.ContactPhase? LastRescuePhase { get; private set; }
        public int LastRescueMs { get; private set; }

        /// <summary>Оттенок выбранного скина. Домножается к цвету состояния (перезарядка, захват).</summary>
        public Color Tint { get; set; } = Color.white;

        /// <summary>
        /// Надеть скин. Свечение и пульсация перезарядки остаются прежними: они
        /// сообщают состояние защиты, и подменять их косметикой нельзя.
        /// </summary>
        /// <summary>Множитель масштаба от скина — только вид, радиус не трогает.</summary>
        public Vector2 SkinScale { get; set; } = Vector2.one;

        SpriteRenderer _core;
        SpriteRenderer _aura;
        float _coreAngle;

        /// <summary>
        /// Надеть скин: оболочка и начинка — как у пузырька. Страж носит тот же
        /// материал, что и шарик: это пара, и разный материал у половинок читался бы
        /// как две разные вещи.
        /// </summary>
        public void ApplySkin(Sprite sprite, Sprite core, Color tint)
        {
            Tint = tint;
            if (_sr != null) _sr.sprite = sprite != null ? sprite : SpriteFactory.Guardian();

            if (_core != null)
            {
                _core.sprite = core;
                _core.enabled = core != null;
                _core.color = tint;
            }
            if (core != null && _sr != null) _sr.color = Color.white;
        }

        /// <summary>Страж заряжен и способен отбить угрозу.</summary>
        public bool CanDeflect => _cooldown <= 0f && !_isPinned;

        /// <summary>Стража выпихнуло из запретной зоны в этом кадре — для эффектов.</summary>
        public bool RepelledThisFrame { get; private set; }

        /// <summary>Перезарядка завершилась именно в этом кадре — щелчок готовности.</summary>
        public bool JustRecharged { get; private set; }

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        SpriteRenderer _sr;

        Vector2 _grabOffset;
        float _pulse;
        float _cooldown;

        bool _isPinned;
        Vector2 _pinPosition;
        float _pinShake;
        /// <summary>Видимый заряд для приглушения: 0 → 1 плавно, сброс за 0.08 с, а не за кадр.</summary>
        float _chargeShown = 1f;

        bool _hasRepelZone;
        Vector2 _repelCenter;
        float _repelRadius;

        // Кольцо с проёмом (кольцо змея): изнутри наружу страж проходит только
        // через проём. Тело змея для него — стенка, а не урон.
        bool _hasConfine;
        bool _confineInside;
        Vector2 _confineCenter;
        float _confineRadius;
        float _confineGapAngle;
        float _confineGapHalf;

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            Radius = PlayerSize.Guardian(cfg);

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = SpriteFactory.Guardian();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Guardian;

            // Аура — ниже всего: свечение из-под стража.
            var auraObject = new GameObject("Aura");
            auraObject.transform.SetParent(transform, false);
            _aura = auraObject.AddComponent<SpriteRenderer>();
            _aura.sharedMaterial = SpriteFactory.Unlit;
            _aura.sprite = SpriteFactory.SoftDot();
            _aura.sortingOrder = SortingOrders.Guardian - 2;
            _aura.enabled = false;

            // Начинка под оболочкой: крутится она, блик на стекле — нет.
            var coreObject = new GameObject("Core");
            coreObject.transform.SetParent(transform, false);
            _core = coreObject.AddComponent<SpriteRenderer>();
            _core.sharedMaterial = SpriteFactory.Unlit;
            _core.sortingOrder = SortingOrders.Guardian - 1;
            _core.enabled = false;

            transform.localScale = Vector3.one * (Radius * 2f);

            ResetState();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Состояние слоёв для дев-оверлея: что включено и каким оно сейчас стало.
        ///
        /// Только для отладки, поэтому за препроцессором: в релизной сборке
        /// отражать внутренности наружу незачем, да и строки эти на русском и
        /// мимо словаря — игрок их видеть не должен ни при каких условиях.
        /// </summary>
        public string DebugLayers()
        {
            if (_sr == null) return "нет";
            return string.Format(
                "оболочка {0}  начинка {1}  аура {2}  масштаб {3:0.000}x{4:0.000}  угол {5:0}",
                _sr.enabled ? "вкл" : "ВЫКЛ",
                _core != null && _core.enabled ? "вкл" : "ВЫКЛ",
                _aura != null && _aura.enabled ? "вкл" : "ВЫКЛ",
                SkinScale.x, SkinScale.y, _coreAngle % 360f);
        }
#endif

        public void ResetState()
        {
            Radius = PlayerSize.Guardian(_cfg);
            transform.localScale = Vector3.one * (Radius * 2f);
            DropGrip(GripBreak.Reset);
            RequireFreshPress();   // свежее нажатие: не «прыгнуть» на палец после клика по кнопке
            _cooldown = 0f;
            _isPinned = false;
            _isDisplaced = false;
            _hasRepelZone = false;
            _hasConfine = false;
            RepelledThisFrame = false;
            _sr.color = Tint;
            _sr.enabled = true;   // «Чернильный морок» прячет стража; партия начинается с видимого
            transform.position = HomePosition();
        }

        /// <summary>Отметить срабатывание: страж уходит на перезарядку.</summary>
        public void NotifyDeflect()
        {
            _cooldown = Mathf.Max(_cooldown, _cfg.guardianDeflectCooldown);
        }

        /// <summary>Стража схватили — он не слушается игрока и не защищает.</summary>
        public bool IsPinned => _isPinned;

        /// <summary>Стража зашвырнуло и он остался лежать: игрок ещё не подобрал его.</summary>
        public bool IsDisplaced => _isDisplaced;

        /// <summary>
        /// Забросить стража в точку и оставить там. Обычно страж сам возвращается
        /// к пузырьку — здесь возврат отключается до первого касания игрока, иначе
        /// он уплыл бы домой сам и искать было бы нечего.
        ///
        /// Защищать он при этом продолжает: он просто не в том месте.
        /// </summary>
        public void Displace(Vector2 worldPosition)
        {
            _isDisplaced = true;
            _displacedAt = _field.ClampToScreen(worldPosition, Radius * 1.2f);
            transform.position = _displacedAt;
            IsDragging = false;
            RequireFreshPress();   // нужно свежее нажатие, иначе палец «поймает» стража сам
        }

        public void ClearDisplacement() => _isDisplaced = false;

        /// <summary>
        /// Взять стража в захват (клешня краба). Позицию нужно обновлять каждый кадр,
        /// пока держит: страж физически находится внутри клешни.
        /// </summary>
        public void Pin(Vector2 worldPosition)
        {
            _isPinned = true;
            _pinPosition = worldPosition;
            IsDragging = false;
        }

        public void Release()
        {
            if (!_isPinned) return;
            _isPinned = false;
            RequireFreshPress(); // после освобождения нужно свежее нажатие
        }

        /// <summary>Зона, куда стража не пускают (поле босса).</summary>
        public void SetRepelZone(Vector2 center, float radius)
        {
            _hasRepelZone = true;
            _repelCenter = center;
            _repelRadius = radius;
        }

        public void ClearRepelZone() => _hasRepelZone = false;

        /// <summary>
        /// Обвить стража кольцом с проёмом. Пока страж внутри, дальше
        /// <paramref name="radius"/> он уходит только через проём: направление
        /// <paramref name="gapAngle"/> (радианы), полуширина <paramref name="gapHalf"/>.
        /// Зовётся каждый кадр — кольцо сжимается. Был снаружи при первом вызове —
        /// кольцо его не касается.
        /// </summary>
        public void SetConfine(Vector2 center, float radius, float gapAngle, float gapHalf)
        {
            if (!_hasConfine)
                _confineInside = Vector2.Distance(transform.position, center) < radius;
            _hasConfine = true;
            _confineCenter = center;
            _confineRadius = Mathf.Max(0.01f, radius);
            _confineGapAngle = gapAngle;
            _confineGapHalf = Mathf.Max(0f, gapHalf);
        }

        public void ClearConfine() => _hasConfine = false;

        /// <summary>Страж всё ещё внутри кольца — то есть не вышел через проём.</summary>
        public bool ConfinedInside => _hasConfine && _confineInside;

        void ApplyConfine()
        {
            if (!_hasConfine || !_confineInside) return;

            Vector2 pos = transform.position;
            Vector2 d = pos - _confineCenter;
            float dist = d.magnitude;
            if (dist < 0.0001f) return;

            // Проём сужается на угловой радиус самого стража: целиком пролезть
            // должен шарик, а не его центр.
            float angle = Mathf.Atan2(d.y, d.x);
            float off = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, _confineGapAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float shoulder = Mathf.Asin(Mathf.Clamp01(Radius / Mathf.Max(dist, Radius)));
            bool inGap = off < _confineGapHalf - shoulder;

            float wall = Mathf.Max(0f, _confineRadius - Radius);
            if (dist > wall && !inGap)
            {
                transform.position = _confineCenter + d / dist * wall;
                return;
            }
            if (dist > _confineRadius + Radius) _confineInside = false;   // вышел через проём
        }

        /// <summary>
        /// Притяжение водоворота. Копится за кадр и гасится в Tick: воронок на
        /// экране может быть несколько, и каждая тянет в свою сторону.
        ///
        /// Тянем именно стража, а не пузырёк: пузырёк игрок не ведёт, и бороться с
        /// воронкой ему было бы нечем — получилась бы случайная смерть. Страж —
        /// единственное, чем игрок управляет, и борьба с течением здесь честная.
        /// </summary>
        public void AddPull(Vector2 center, float strength)
        {
            Vector2 delta = center - (Vector2)transform.position;
            float distance = delta.magnitude;
            if (distance < 0.0001f || distance > 2.6f) return;

            // Вблизи тянет сильнее, но не бесконечно: иначе страж прилипал бы
            // к воронке намертво и игрок терял бы управление вовсе.
            float falloff = Mathf.Clamp01(1f - distance / 2.6f);
            _pull += delta.normalized * (strength * falloff * falloff * 1.6f);
        }

        /// <summary>
        /// Отдача от удара: страж чуть отскакивает назад. Складывается в ту же
        /// копилку, что и течение воронок, и так же гасится за кадр — отдача обязана
        /// кончиться к следующему удару, иначе она копится и уводит стража с поля.
        /// </summary>
        public void Nudge(Vector2 delta) => _pull += delta;

        Vector2 _pull;

        bool _isDisplaced;
        Vector2 _displacedAt;

        Vector2 HomePosition() => _bubble.Center + _cfg.guardianHomeOffset;

        public void Tick(float dt)
        {
            if (_isPinned)
            {
                TickPinned(dt);
                return;
            }

            RepelledThisFrame = false;
            JustRecharged = false;

            if (_cooldown > 0f)
            {
                _cooldown -= dt;
                if (_cooldown <= 0f) JustRecharged = true;
            }

            HandleInput(dt);

            Vector2 target;
            float lerp;

            if (IsDragging)
            {
                _isDisplaced = false;   // подобрали — дальше всё как обычно
                target = _pointerWorld + _grabOffset;

                // Отзывчивость — настройка сложности, а не константа баланса: на
                // лёгкой страж идёт за пальцем заметно резвее. Прежние 26 давали
                // треть пути за кадр, и на быстрых препятствиях страж попросту не
                // успевал туда, куда его вели.
                lerp = Mathf.Max(_cfg.guardianFollowLerp, Difficulty.Current.guardianFollowLerp);
            }
            else if (_isDisplaced)
            {
                // Лежит там, куда закинули. Возврата к пузырьку нет — в этом вся суть.
                target = _displacedAt;
                lerp = _cfg.guardianReturnLerp;
            }
            else
            {
                target = HomePosition();
                lerp = _cfg.guardianReturnLerp;
            }

            // «Поводок»: по умолчанию огромный, но дизайнер может ограничить в инспекторе.
            // Заброшенного стража он не касается — иначе притянул бы его обратно сам,
            // и искать было бы нечего.
            Vector2 fromBubble = target - _bubble.Center;
            float max = _cfg.guardianMaxDistanceFromBubble;
            if (!_isDisplaced && max > 0f && fromBubble.magnitude > max)
                target = _bubble.Center + fromBubble.normalized * max;

            // Ближняя граница: страж кружит рядом, но не садится на пузырёк.
            // Иначе он перехватывал бы вообще всё с любой стороны и игра теряла смысл.
            // Поводок — тоже расстояние, и он обязан ужаться вместе с парой:
            // иначе уменьшенные шарики разойдутся на прежнюю дистанцию и
            // поведение пары изменится, хотя просили сохранить его.
            float min = _cfg.guardianMinDistanceFromBubble * PlayerSize.Scale;
            if (!_isDisplaced && min > 0f)
            {
                Vector2 offset = target - _bubble.Center;
                if (offset.magnitude < min)
                {
                    if (offset.sqrMagnitude < 0.0001f) offset = Vector2.up;
                    target = _bubble.Center + offset.normalized * min;
                }
            }

            // Поле босса: цель выталкивается за границу зоны, страж скользит вдоль неё.
            if (_hasRepelZone)
            {
                Vector2 fromBoss = target - _repelCenter;
                float minDistance = _repelRadius + Radius * 0.5f;
                if (fromBoss.magnitude < minDistance)
                {
                    if (fromBoss.sqrMagnitude < 0.0001f) fromBoss = Vector2.down;
                    Vector2 pushed = _repelCenter + fromBoss.normalized * minDistance;

                    // Если ближайшая точка вне зоны не помещается на экране (босс у края),
                    // выталкиваем вниз: там граница зоны всегда достижима.
                    Vector2 onScreen = _field.ClampToScreen(pushed, Radius * 0.35f);
                    if (Vector2.Distance(onScreen, _repelCenter) < minDistance - 0.01f)
                        pushed = new Vector2(target.x, _repelCenter.y - minDistance);

                    target = pushed;
                    RepelledThisFrame = true;
                }
            }

            // Течение воронок: смещаем цель, а не позицию, — иначе страж дрожал бы
            // между своим местом и притянутым.
            target += _pull;
            _pull = Vector2.zero;

            target = _field.ClampToScreen(target, Radius * 0.35f);

            float k = 1f - Mathf.Exp(-lerp * dt); // независимо от частоты кадров
            transform.position = Vector2.Lerp(transform.position, target, k);
            ApplyConfine();

            // пульсация свечения; на перезарядке страж тускнеет и сжимается
            _pulse += dt * 4f;
            float charge = _cfg.guardianDeflectCooldown > 0.0001f
                ? 1f - Mathf.Clamp01(_cooldown / _cfg.guardianDeflectCooldown)
                : 1f;

            // Цвет оболочки каждый кадр собирался заново и затирал то, что
            // поставил скин: оттенок домножался второй раз поверх уже окрашенной
            // начинки, а на перезарядке оболочка уходила в серо-бирюзовый при
            // прозрачности 0.55. Витрина показывала чистый белый над материалом —
            // отсюда «в магазине материал видно, в игре он мутный».
            //
            // Теперь берём тот же цвет, что и ApplySkin, а перезарядку показываем
            // только приглушением и прозрачностью, не подмешивая чужой тон.
            Color skinColor = _core != null && _core.enabled ? Color.white : Tint;
            // Удар обнулял заряд, и страж темнел на четверть за один кадр (E-17).
            // Сброс теперь идёт за 0.08 с, восстановление — как и было, по заряду.
            _chargeShown = charge < _chargeShown
                ? Mathf.MoveTowards(_chargeShown, charge, dt / 0.08f)
                : charge;
            float dim = Mathf.Lerp(0.74f, 1f, _chargeShown);
            _sr.color = new Color(skinColor.r * dim, skinColor.g * dim, skinColor.b * dim,
                                  skinColor.a * Mathf.Lerp(0.78f, 1f, _chargeShown));

            // Размах перезарядки срезан с ±18 % до ±8 %. Движение самого скина —
            // ±2…6 %, и при прежнем размахе характер скина попросту тонул в
            // пульсации заряда. Цвет остаётся главной подсказкой о перезарядке,
            // он никуда не делся.
            float s = Radius * 2f * (0.92f + 0.08f * _chargeShown) * (1f + Mathf.Sin(_pulse) * 0.03f);
            transform.localScale = new Vector3(s * SkinScale.x, s * SkinScale.y, 1f);
            TickCore();
        }

        /// <summary>Пока держат — только трясётся в клешне и краснеет.</summary>
        void TickPinned(float dt)
        {
            RepelledThisFrame = false;
            JustRecharged = false;
            _pinShake += dt;

            // Дрожь была 41 и 33 рад/с — около шести колебаний в секунду.
            // Замедлена вчетверо и срезана по амплитуде: захват и так читается
            // по тому, что страж не слушается.
            Vector2 jitter = new Vector2(Mathf.Sin(_pinShake * 10f), Mathf.Cos(_pinShake * 8f)) * 0.025f;
            transform.position = Vector2.Lerp(transform.position, _pinPosition + jitter,
                                              1f - Mathf.Exp(-22f * dt));

            _sr.color = Color.Lerp(new Color(1f, 0.55f, 0.5f, 0.9f), Color.white,
                                   0.55f + 0.2f * Mathf.Sin(_pinShake * 4f)) * Tint;

            // Пульс захвата — цикл за 1.6 с (было 1.26): правило «не чаще раза в 1.5 с».
            float s = Radius * 2f * (0.88f + 0.02f * Mathf.Sin(_pinShake * 4f));
            transform.localScale = new Vector3(s * SkinScale.x, s * SkinScale.y, 1f);
            TickCore();
        }

        Vector2 _pointerWorld;

        // ------------------------------------------------------------ захват
        //
        // Страж держится за конкретное касание (id), а не за «главное касание»
        // платформы. Прежде любой кадр без нажатия отпускал стража: система на
        // кадр отменяла касание (ладонь, жест у края, повторная выдача пальца под
        // новым id) — и страж уходил к пузырьку, а когда касание возвращалось,
        // хватался заново уже без смещения захвата и прыгал под палец.
        //
        // Теперь ведёт только палец, который схватил; второй палец и ладонь его не
        // сбивают. Пропал этот палец — ждём guardianReleaseGrace: вернулся под новым
        // id рядом — это тот же палец, смещение захвата прежнее. Не вернулся — страж
        // уходит домой, как и раньше. Касание, начатое над кнопкой, стража не
        // хватает; захват, начатый в игре, над кнопками не теряется.

        /// <summary>
        /// Насколько далеко (доля высоты экрана) от точки, где пропал палец, может
        /// появиться «тот же» палец. За 0.08 с даже резкий мах проходит меньше.
        /// </summary>
        const float RegripRadius = 0.16f;

        /// <summary>Повторное касание после отпускания раньше этого — палец, скорее всего, не отрывали.</summary>
        const float QuickRetouch = 0.3f;

        readonly List<PointerInput.Contact> _contacts = new List<PointerInput.Contact>();
        HashSet<int> _activeIds = new HashSet<int>();
        HashSet<int> _prevActiveIds = new HashSet<int>();
        readonly HashSet<int> _ignoredIds = new HashSet<int>();
        int _gripId;
        float _lostFor;
        PointerInput.ContactPhase? _lostPhase;
        bool _requireFresh = true;
        float _releasedAt = -10f;
        Vector2 _releasedWorld;
        PointerEventData _uiEvent;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        /// <summary>Касания, которые уже на экране, хватать не должны — только новые.</summary>
        void RequireFreshPress() => _requireFresh = true;

        void DropGrip(GripBreak reason)
        {
            if (!IsDragging) return;
            IsDragging = false;
            // палец ещё держал — домой страж пошёл не по воле игрока
            if (_lostFor <= 0f) NoteBreak(reason, 0);
            _lostFor = 0f;
            _lostPhase = null;
        }

        void NoteBreak(GripBreak reason, int ms)
        {
            GripBreaks++;
            LastGripBreak = reason;
            LastGripBreakMs = ms;
            LastGripBreakAt = Time.unscaledTime;
        }

        Vector2 ToWorld(Vector2 screen)
        {
            var cam = _field.Cam;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        void HandleInput(float dt)
        {
            PointerInput.ReadContacts(_contacts);

            // Новые касания — те, которых в прошлом кадре не было среди идущих.
            var swap = _prevActiveIds;
            _prevActiveIds = _activeIds;
            _activeIds = swap;
            _activeIds.Clear();
            for (int i = 0; i < _contacts.Count; i++)
                if (_contacts[i].Phase == PointerInput.ContactPhase.Active) _activeIds.Add(_contacts[i].Id);
            _ignoredIds.RemoveWhere(id => !_activeIds.Contains(id));
            if (_requireFresh)
            {
                _ignoredIds.UnionWith(_activeIds);
                _requireFresh = false;
            }

            if (!_inputEnabled)
            {
                IsDragging = false;
                _ignoredIds.UnionWith(_activeIds);
                return;
            }

            if (IsDragging && FollowGrip(dt)) return;
            TryStartGrip();
        }

        /// <summary>Вести за своим пальцем. false — палец ушёл, страж отпущен.</summary>
        bool FollowGrip(float dt)
        {
            // id платформа отдаёт заново после конца касания: в списке может быть и
            // старая закончившаяся запись с тем же id — идущая важнее.
            int ended = -1;
            for (int i = 0; i < _contacts.Count; i++)
            {
                var c = _contacts[i];
                if (c.Id != _gripId) continue;
                if (c.Phase != PointerInput.ContactPhase.Active) { ended = i; continue; }
                _pointerWorld = ToWorld(c.Screen);
                _lostFor = 0f;
                _lostPhase = null;
                return true;
            }
            if (ended >= 0 && _lostPhase == null) _lostPhase = _contacts[ended].Phase;

            // Тот же палец мог прийти заново под другим id — новое касание рядом.
            float radius = RegripRadius * (_field.Top - _field.Bottom);
            int best = -1;
            float bestDistance = radius;
            for (int i = 0; i < _contacts.Count; i++)
            {
                var c = _contacts[i];
                if (c.Phase != PointerInput.ContactPhase.Active || _prevActiveIds.Contains(c.Id)
                    || _ignoredIds.Contains(c.Id)) continue;
                float d = Vector2.Distance(ToWorld(c.Screen), _pointerWorld);
                if (d <= bestDistance) { best = i; bestDistance = d; }
            }
            if (best >= 0)
            {
                GripRescues++;
                LastRescuePhase = _lostPhase;
                LastRescueMs = Mathf.RoundToInt(_lostFor * 1000f);
                _gripId = _contacts[best].Id;
                _pointerWorld = ToWorld(_contacts[best].Screen);
                _lostFor = 0f;
                _lostPhase = null;
                return true;
            }

            // Ждём допуск, держа стража там, где был палец: короткое пропадание
            // касания не должно уводить его к пузырьку.
            _lostFor += dt;
            if (_lostFor < _cfg.guardianReleaseGrace) return true;

            IsDragging = false;
            if (_lostPhase == PointerInput.ContactPhase.Canceled) NoteBreak(GripBreak.Canceled, 0);
            else if (_lostPhase == null) NoteBreak(GripBreak.Vanished, 0);
            _releasedAt = Time.unscaledTime;
            _releasedWorld = _pointerWorld;
            _lostFor = 0f;
            _lostPhase = null;
            return false;
        }

        void TryStartGrip()
        {
            for (int i = 0; i < _contacts.Count; i++)
            {
                var c = _contacts[i];
                if (c.Phase != PointerInput.ContactPhase.Active || _prevActiveIds.Contains(c.Id)
                    || _ignoredIds.Contains(c.Id)) continue;

                // Нажатие над кнопкой — нажатие кнопки, а не захват (A-12). Касание
                // помечаем: пока палец не отпущен, стража оно не схватит.
                if (DevUiRect.Contains(c.Screen) || OverInteractiveUi(c.Screen))
                {
                    _ignoredIds.Add(c.Id);
                    continue;
                }

                Vector2 world = ToWorld(c.Screen);

                // Ушёл домой по Ended и почти сразу коснулись там же — скорее всего,
                // палец и не отрывали: система прислала Ended посреди ведения.
                if (Time.unscaledTime - _releasedAt < QuickRetouch &&
                    Vector2.Distance(world, _releasedWorld) < RegripRadius * (_field.Top - _field.Bottom))
                    NoteBreak(GripBreak.EndedRetouch, Mathf.RoundToInt((Time.unscaledTime - _releasedAt) * 1000f));
                _releasedAt = -10f;

                // Заброшенного стража надо именно найти и подобрать, поэтому зона
                // захвата у него шире (тыкать пиксель в пиксель на телефоне нечестно),
                // а «хватать откуда угодно» временно не работает — иначе страж прилетал
                // бы под палец сам и искать было бы нечего.
                float multiplier = _isDisplaced
                    ? _cfg.guardianDisplacedGrabMultiplier
                    : _cfg.guardianGrabRadiusMultiplier;

                bool onOrb = Vector2.Distance(world, Center) <= Radius * multiplier;

                if (onOrb)
                    _grabOffset = Center - world;   // тянем «за то место, где схватили»
                else if (_cfg.grabAnywhere && !_isDisplaced)
                    _grabOffset = Vector2.zero;     // страж подлетает под палец
                else
                {
                    _ignoredIds.Add(c.Id);          // мимо заброшенного стража — ждём новое касание
                    continue;
                }

                IsDragging = true;
                _gripId = c.Id;
                _pointerWorld = world;
                _lostFor = 0f;
                _lostPhase = null;
                return;
            }
        }

        /// <summary>
        /// Касание над кнопкой или другим элементом, который сам ловит нажатие.
        /// Луч EventSystem пускаем сами в этом же кадре: модуль ввода UI мог ещё
        /// не обработать новое касание, и его «последний луч» был бы прошлым.
        /// Надписи и баннеры нажатие не ловят — над ними стража хватать можно.
        /// </summary>
        bool OverInteractiveUi(Vector2 screen)
        {
            if (UiHitTest != null) return UiHitTest(screen);
            var es = EventSystem.current;
            if (es == null) return false;
            if (_uiEvent == null) _uiEvent = new PointerEventData(es);
            _uiEvent.Reset();
            _uiEvent.position = screen;
            _uiHits.Clear();
            es.RaycastAll(_uiEvent, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                var go = _uiHits[i].gameObject;
                if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null)
                    return true;
            }
            return false;
        }

        /// <summary>Вызывается при старте партии: сбросить состояние ввода.</summary>
        /// <summary>Внутреннее движение материала стража — медленное, как у пузырька.</summary>
        void TickCore()
        {
            if (_core == null || !_core.enabled) return;

            _coreAngle += Time.unscaledDeltaTime * SkinVisuals.CoreSpin;
            _core.transform.localRotation = Quaternion.Euler(0f, 0f, _coreAngle);

            // Компенсируем только неравномерность, а не сам размер: иначе
            // материал стоит, пока оболочка дышит. Подробности — в
            // BubbleController.Tick, там та же правка и та же причина.
            float mean = Mathf.Sqrt(Mathf.Max(0.0001f, SkinScale.x * SkinScale.y));
            float sx = SkinScale.x > 0.01f ? mean / SkinScale.x : 1f;
            float sy = SkinScale.y > 0.01f ? mean / SkinScale.y : 1f;
            _core.transform.localScale = new Vector3(sx, sy, 1f);
        }


        /// <summary>
        /// Аура скина: мягкое свечение под телом. Отдельный слой, потому что ею
        /// управляет SkinFxRuntime — она дышит по своему закону, а не вместе с
        /// пузырьком, и гасится, когда скин без ауры.
        /// </summary>
        /// <summary>
        /// Спрятать на время модального экрана.
        ///
        /// Гасим рендереры, а не сам объект: объект продолжает получать Tick от
        /// GameManager, и выключение сбило бы его положение и фазу анимации. Под
        /// плотной подложкой меню этого всё равно не видно, но полагаться только
        /// на прозрачность подложки не стоит — один полупрозрачный элемент
        /// интерфейса, и свечение снова проступит.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_sr != null) _sr.enabled = visible;
            if (_core != null) _core.enabled = visible && _core.sprite != null;
            if (_aura != null) _aura.enabled = visible && _auraOn;
        }

        bool _auraOn;

        public void SetAura(Color color, float intensity)
        {
            if (_aura == null) return;

            bool on = intensity > 0.01f;
            _auraOn = on;
            _aura.enabled = on;
            if (!on) return;

            _aura.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(intensity));

            // Радиус свечения урезан примерно на треть: при прежних 1.45 + 0.5·I
            // гало доходило до 1.8 диаметра тела и закрывало препятствия рядом с
            // шариком — ровно то, от чего игрок уворачивается.
            _aura.transform.localScale = Vector3.one * (1.1f + intensity * 0.25f);
        }

    }
}
