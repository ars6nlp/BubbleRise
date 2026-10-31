#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Панель разработчика: прыжок к любому боссу, спавн любого контента, бессмертие,
    /// ускорение времени. Открывается клавишей F1 или кнопкой DEV в углу.
    ///
    /// ВЕСЬ ФАЙЛ обёрнут в #if UNITY_EDITOR || DEVELOPMENT_BUILD. В обычной сборке
    /// (галка Development Build снята — так и делает Tools/BubbleRise/2. Собрать WebGL)
    /// этого кода в билде нет вообще: не «спрятан», а физически отсутствует.
    /// Отключить в редакторе — снять галку GameConfig.devTools.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        GameConfig _cfg;
        GameManager _game;
        BubbleController _bubble;
        GuardianOrb _guardian;

        bool _open;
        Rect _panelRect;
        Vector2 _scroll;

        public void Init(GameConfig cfg, GameManager game, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _game = game;
            _bubble = bubble;
            _guardian = guardian;
        }

        void Update()
        {
            if (_cfg == null || !_cfg.devTools) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _open = !_open;
#else
            if (Input.GetKeyDown(KeyCode.F1)) _open = !_open;
#endif

            // пока курсор над панелью, страж за кликами не бегает
            bool overPanel = false;
            if (_open)
            {
                Vector2 mouse = ReadPointer();
                overPanel = _panelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            }
            _guardian.DevInputBlocked = overPanel;
        }

        static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        void OnGUI()
        {
            if (_cfg == null || !_cfg.devTools) return;

            float scale = Mathf.Max(1f, Screen.height / 900f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float viewHeight = Screen.height / scale;

            if (!_open)
            {
                if (GUI.Button(new Rect(8f, viewHeight - 34f, 66f, 26f), "DEV (F1)")) _open = true;
                _panelRect = Rect.zero;
                GUI.matrix = previousMatrix;
                return;
            }

            // Высота — по экрану, а не фиксированные 470 пикселей: с ростом числа
            // боссов нижние кнопки уезжали за край, и добраться до них было нечем.
            // Плюс прокрутка, чтобы панель оставалась рабочей на любой высоте окна.
            _panelRect = new Rect(8f, 8f, 264f, Mathf.Min(620f, viewHeight - 16f));
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawContents();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // прямоугольник для проверки курсора должен быть в экранных пикселях
            _panelRect = new Rect(_panelRect.x * scale, _panelRect.y * scale,
                                  _panelRect.width * scale, _panelRect.height * scale);

            GUI.matrix = previousMatrix;
        }

        void DrawContents()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>ПАНЕЛЬ РАЗРАБОТЧИКА</b>", RichLabel());
            if (GUILayout.Button("×", GUILayout.Width(24f))) _open = false;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Состояние: {_game.State}   {Mathf.FloorToInt(_game.Distance)} м");
            GUILayout.Label($"Жизни: {_bubble.Lives}   Скорость: {_game.ScrollSpeed:F2}");
            GUILayout.Label($"Босс: {_game.ActiveBossName} ({_game.ActiveBossHp})" +
                            (_game.ZoneActive ? "   [в локации]" : ""));
            GUILayout.Label(_game.CrabSizeInfo);

            GUILayout.Space(4f);
            GUILayout.Label("— Боссы —");

            // Номер и глубина прямо в кнопке: боссы теперь стоят на круглых отметках,
            // и знать, кто именно ждёт на 7000 м, полезнее, чем просто список имён.
            for (int i = 0; i < _game.BossCount; i++)
            {
                string label = $"{i + 1}. {_game.BossName(i)}";
                if (GUILayout.Button(label, GUILayout.Height(21f))) _game.DevStartBoss(i);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Убить босса")) _game.DevKillBoss();
            if (GUILayout.Button("К боссу")) _game.DevSkipToBoss();
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("— Контент —");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Локация")) _game.DevSpawnZoneNow();
            if (GUILayout.Button("Постройка")) _game.DevSpawnFormation();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Коралл")) _game.DevSpawnSpire();
            // Магазин без фарма: цена легендарного — шестьдесят заплывов, и проверять
            // покупку, честно их наплавав, невозможно.
            if (GUILayout.Button("+1000 монет")) Wallet.DevGrant(1000);
            // Статус скина на всё сразу: иначе проверка горения — это погоня
            // стражем за каждым препятствием по отдельности.
            if (GUILayout.Button("Поджечь всё")) _game.DevIgniteAll();
            if (GUILayout.Button("Обломок")) _game.DevSpawnObstacle();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Рыба")) _game.DevSpawnFish();
            if (GUILayout.Button("Медуза")) _game.DevSpawnJellyfish();
            if (GUILayout.Button("Краб на камнях")) _game.DevSpawnSideCrab();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Очистить экран")) _game.DevClearField();

            GUILayout.Space(4f);
            GUILayout.Label("— Игрок —");
            bool invincible = GUILayout.Toggle(_bubble.Invincible, " Бессмертие");
            if (invincible != _bubble.Invincible) _bubble.Invincible = invincible;

            // По умолчанию включено: иначе, чтобы просто посмотреть рисунок скина,
            // пришлось бы отбить сотню рыб. Снимите галку, чтобы проверить, как
            // магазин выглядит у настоящего игрока.
            bool allSkins = GUILayout.Toggle(SkinStore.DevUnlockAll, " Все скины открыты");
            if (allSkins != SkinStore.DevUnlockAll) SkinStore.DevUnlockAll = allSkins;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 жизнь")) _game.DevAddLife(1);
            if (GUILayout.Button("−1 жизнь")) _game.DevAddLife(-1);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("— Дистанция —");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100 м")) _game.DevAddDistance(100f);
            if (GUILayout.Button("+500 м")) _game.DevAddDistance(500f);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("— Слои скина —");

            // Главный вопрос, ради которого оверлей и заведён: совпадает ли то,
            // что собрано для боя, с тем, что показывает витрина. Обе строки
            // берутся из одного SkinVisuals, поэтому расхождение здесь сразу
            // означает, что разошлись не данные, а применение.
            var bubbleLook = SkinVisuals.Bubble();
            var guardLook = SkinVisuals.Guardian();

            GUILayout.Label($"пузырёк #{SkinStore.SelectedBubble} {SkinStore.Bubble[SkinStore.SelectedBubble].Name}");
            GUILayout.Label($"  {bubbleLook.Archetype.Shape}/{bubbleLook.Fx.Motion}/{bubbleLook.Fx.Trail}" +
                            $"  материал: {(bubbleLook.Core != null ? "есть" : "НЕТ")}");
            GUILayout.Label("  " + _bubble.DebugLayers());

            GUILayout.Label($"страж #{SkinStore.SelectedGuardian} {SkinStore.Guardian[SkinStore.SelectedGuardian].Name}");
            GUILayout.Label($"  {guardLook.Archetype.Shape}/{guardLook.Fx.Motion}/{guardLook.Fx.Trail}" +
                            $"  материал: {(guardLook.Core != null ? "есть" : "НЕТ")}");
            GUILayout.Label("  " + _guardian.DebugLayers());

            GUILayout.Space(4f);
            GUILayout.Label("— Медуза —");
            GUILayout.Label("  " + _game.DevJellyFit());
            if (GUILayout.Button("Проверить медузу по форматам экрана"))
                Debug.Log(JellyFitReport.Run());

            GUILayout.Space(4f);
            GUILayout.Label("— Оболочка столкновения —");
            GUILayout.Label("  " + _game.DevEnvelopeInfo());

            if (GUILayout.Button("Тест оболочки на ближайшем препятствии"))
                _game.DevGuardianCollision();

            bool loop = _game.DevEnvelopeLooping;
            if (GUILayout.Button(loop ? "Зациклено — остановить"
                                      : "Показать оболочку выбранного скина (зациклить)"))
                _game.DevLoopEnvelope(!loop);

            if (GUILayout.Button("Показать оболочку в витрине"))
                _game.DevShowShopEnvelope();

            // Ползунок размера: подбирать радиус, глядя на телефон, быстрее, чем
            // править таблицу и ждать перекомпиляции.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"размер x{CollisionEnvelope.DebugSizeScale:0.00}", GUILayout.Width(110f));
            CollisionEnvelope.DebugSizeScale =
                GUILayout.HorizontalSlider(CollisionEnvelope.DebugSizeScale, 0.25f, 3f);
            if (GUILayout.Button("1x", GUILayout.Width(34f)))
                CollisionEnvelope.DebugSizeScale = 1f;
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"эффекты: {(Visuals.Calm ? "щадящие" : "обычные")}   " +
                            $"свечение x{Visuals.Glow:0.00}   частицы x{Visuals.Particles:0.00}");
            GUILayout.Label($"время: t={Time.time:0.0}  unscaled={Time.unscaledTime:0.0}  " +
                            $"timeScale={Time.timeScale:0.00}");

            GUILayout.Space(4f);
            GUILayout.Label($"— Сложность: {Loc.Get(Difficulty.LevelKey(Difficulty.Level))} —");

            // Переключать прямо в заплыве: разница между режимами видна только в
            // сравнении, а выходить ради неё в меню — значит каждый раз начинать
            // заново и сравнивать по памяти.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Лёгкая")) Difficulty.Set(DifficultyLevel.Easy);
            if (GUILayout.Button("Обычная")) Difficulty.Set(DifficultyLevel.Normal);
            if (GUILayout.Button("Сложная")) Difficulty.Set(DifficultyLevel.Hard);
            GUILayout.EndHorizontal();

            var dp = Difficulty.Current;
            GUILayout.Label($"Старт {dp.warmupMeters:0} м   видов открыто: " +
                            $"{HazardTable.UnlockedCount(Mathf.FloorToInt(_game.Distance))}/{HazardTable.All.Length}");
            GUILayout.Label($"Хитбокс {dp.bubbleHitboxScale:0.00}   прощение {dp.coyoteTime:0.00} с   " +
                            (Difficulty.ReliefActive ? "помощь ВКЛ" : "помощь выкл"));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Вступление заново"))
            {
                PlayerPrefs.DeleteKey(SaveData.TutorialKey);
                PlayerPrefs.Save();
            }
            if (GUILayout.Button(Haptics.Enabled ? "Вибро: вкл" : "Вибро: выкл")) Haptics.Toggle();
            GUILayout.EndHorizontal();

            // Щадящий режим переключается прямо в заплыве: разницу между ступенями
            // яркости видно только в сравнении, а выходить ради неё в меню — значит
            // каждый раз начинать заново.
            if (GUILayout.Button("Эффекты: " + Loc.Get(Visuals.LevelKey(Visuals.Level))))
                Visuals.CycleLevel();

            GUILayout.Space(4f);
            GUILayout.Label("— Награды —");

            // Сутки и часы нельзя проверить, ожидая сутки и часы. Сдвиг времени
            // живёт отдельно от защиты от перевода часов (см. TimeService), поэтому
            // игра не считает проверку обманом и ведёт себя как у игрока.
            GUILayout.Label($"Готово: {RewardsHub.PendingCount}   " +
                            $"Сундуки: {RewardsHub.ChestsReady}   Серия: {RewardsHub.Streak}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 час")) { TimeService.DevShift(System.TimeSpan.FromHours(1)); RewardsHub.Refresh(); }
            if (GUILayout.Button("+1 день")) { TimeService.DevShift(System.TimeSpan.FromDays(1)); RewardsHub.Refresh(); }
            if (GUILayout.Button("+7 дней")) { TimeService.DevShift(System.TimeSpan.FromDays(7)); RewardsHub.Refresh(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сундук сейчас")) RewardsHub.DevResetChestTimer();
            if (GUILayout.Button("+1 вращение")) RewardsHub.DevGrantSpin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Квесты: выполнить")) RewardsHub.DevCompleteQuests();
            if (GUILayout.Button("Квесты: заново")) RewardsHub.DevResetQuests();
            GUILayout.EndHorizontal();

            // Сброс полный: и состояние наград, и сдвиг времени. Проверять первый
            // запуск иначе пришлось бы удалением профиля целиком, вместе со скинами.
            if (GUILayout.Button("Сбросить награды")) RewardsHub.DevResetAll();

            GUILayout.Space(4f);
            GUILayout.Label($"— Время: x{Time.timeScale:F2} —");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0")) Time.timeScale = 0f;
            if (GUILayout.Button("0.25")) Time.timeScale = 0.25f;
            if (GUILayout.Button("1")) Time.timeScale = 1f;
            if (GUILayout.Button("2")) Time.timeScale = 2f;
            if (GUILayout.Button("4")) Time.timeScale = 4f;
            GUILayout.EndHorizontal();
        }

        static GUIStyle _richLabel;

        static GUIStyle RichLabel()
        {
            if (_richLabel == null)
                _richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            return _richLabel;
        }

        void OnDisable()
        {
            // не оставляем игру на паузе или в ускорении после выхода из Play Mode
            Time.timeScale = 1f;
            if (_guardian != null) _guardian.DevInputBlocked = false;
        }
    }
}
#endif
