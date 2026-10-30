using UnityEngine;

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
        /// (стартовый экран и экран поражения).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Курсор над дев-панелью — не тащим стража за кликом по кнопке.</summary>
        public bool DevInputBlocked { get; set; }

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

        bool _prevPressed;
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
            IsDragging = false;
            _prevPressed = true; // требуем свежее нажатие, чтобы не «прыгнуть» на палец после клика по кнопке
            _cooldown = 0f;
            _isPinned = false;
            _isDisplaced = false;
            _hasRepelZone = false;
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
            _prevPressed = true;   // нужно свежее нажатие, иначе палец «поймает» стража сам
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
            _prevPressed = true; // после освобождения нужно свежее нажатие
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

            HandleInput();

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

        void HandleInput()
        {
            if (!InputEnabled || DevInputBlocked)
            {
                IsDragging = false;
                _prevPressed = true; // после разблокировки нужно свежее нажатие
                return;
            }

            if (!PointerInput.TryRead(out Vector2 screenPos, out bool pressed))
            {
                IsDragging = false;
                _prevPressed = false;
                return;
            }

            var cam = _field.Cam;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            _pointerWorld = new Vector2(world.x, world.y);

            bool justPressed = pressed && !_prevPressed;
            _prevPressed = pressed;

            if (!pressed)
            {
                IsDragging = false;
                return;
            }

            if (justPressed)
            {
                // Заброшенного стража надо именно найти и подобрать, поэтому зона
                // захвата у него шире (тыкать пиксель в пиксель на телефоне нечестно),
                // а «хватать откуда угодно» временно не работает — иначе страж прилетал
                // бы под палец сам и искать было бы нечего.
                float multiplier = _isDisplaced
                    ? _cfg.guardianDisplacedGrabMultiplier
                    : _cfg.guardianGrabRadiusMultiplier;

                bool onOrb = Vector2.Distance(_pointerWorld, Center) <= Radius * multiplier;

                if (onOrb)
                {
                    IsDragging = true;
                    _grabOffset = Center - _pointerWorld; // тянем «за то место, где схватили»
                }
                else if (_cfg.grabAnywhere && !_isDisplaced)
                {
                    IsDragging = true;
                    _grabOffset = Vector2.zero;           // страж подлетает под палец
                }
            }
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
