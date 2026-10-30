using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Единственный объект, который лежит в сцене. Собирает всю игру кодом:
    /// камеру, фон, пузырёк, стража, спавнер, босса, эффекты, UI, звук и мост к Yandex SDK.
    ///
    /// Такой подход выбран сознательно: спрайты процедурные, UI строится кодом,
    /// поэтому «класть» в сцену нечего — зато всё поведение видно в скриптах,
    /// а настройки баланса лежат рядом, в GameConfig на этом же объекте.
    /// </summary>
    [RequireComponent(typeof(GameConfig))]
    public class GameBootstrap : MonoBehaviour
    {
        IPlatform _platform;
        UIManager _ui;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var cfg = GetComponent<GameConfig>();
            if (cfg == null) cfg = gameObject.AddComponent<GameConfig>();

            // Сложность — до всего остального: от неё зависят и спавнеры, и
            // хитбоксы, и отзывчивость стража, то есть почти всё, что собирается
            // ниже. Компонент добавляется сам, если его нет: настройки правятся в
            // инспекторе, но игра обязана запускаться и без ручной подготовки сцены.
            // Размер игрока — до создания пузырька и стража: они читают радиус
            // в своём Init, и привязка после этого опоздала бы на кадр.
            PlayerSize.Bind(cfg);

            var difficulty = GetComponent<DifficultyConfig>();
            if (difficulty == null) difficulty = gameObject.AddComponent<DifficultyConfig>();
            Difficulty.Bind(difficulty);

            SpriteFactory.Prewarm(); // рисуем все текстуры на загрузке, а не по ходу игры

            var cam = SetupCamera();

            var field = gameObject.AddComponent<Playfield>();
            field.Init(cam, cfg.worldHeight, cfg.designAspect);

            var background = NewChild("Background").AddComponent<BackgroundController>();
            background.Init(cfg, field);

            var fx = NewChild("FX").AddComponent<FxManager>();
            fx.Init(cfg);

            var bubble = NewChild("Bubble").AddComponent<BubbleController>();
            bubble.Init(cfg, field);

            var guardian = NewChild("Guardian").AddComponent<GuardianOrb>();
            guardian.Init(cfg, field, bubble);

            var obstacleRoot = NewChild("Obstacles").transform;
            var spawner = gameObject.AddComponent<ObstacleSpawner>();
            spawner.Init(cfg, field, obstacleRoot);

            var zoneRoot = NewChild("Zones").transform;
            var zoneSpawner = gameObject.AddComponent<ZoneSpawner>();
            zoneSpawner.Init(cfg, field, zoneRoot);

            var spireRoot = NewChild("Spires").transform;
            var spireSpawner = gameObject.AddComponent<SpireSpawner>();
            spireSpawner.Init(cfg, field, spireRoot);

            var formationRoot = NewChild("Formations").transform;
            var formationSpawner = gameObject.AddComponent<FormationSpawner>();
            formationSpawner.Init(cfg, field, formationRoot);

            var sceneryRoot = NewChild("Scenery").transform;
            var scenerySpawner = gameObject.AddComponent<ScenerySpawner>();
            scenerySpawner.Init(cfg, field, sceneryRoot);

            var fishRoot = NewChild("Fish").transform;
            var fishSpawner = gameObject.AddComponent<FishSpawner>();
            fishSpawner.Init(cfg, field, fishRoot);

            var projectileRoot = NewChild("BossProjectiles").transform;
            var boss = NewChild("JellyBoss").AddComponent<JellyBossController>();
            boss.Init(cfg, field, bubble, guardian, projectileRoot);

            var serpent = NewChild("SerpentBoss").AddComponent<SerpentBossController>();
            serpent.Init(cfg, field, bubble, guardian);

            var octopus = NewChild("OctopusBoss").AddComponent<OctopusBossController>();
            octopus.Init(cfg, field, bubble, guardian);

            var swordfish = NewChild("SwordfishBoss").AddComponent<SwordfishBossController>();
            swordfish.Init(cfg, field, bubble, guardian);

            var crab = NewChild("CrabBoss").AddComponent<CrabBossController>();
            crab.Init(cfg, field, bubble, guardian);

            var inkBoss = NewChild("InkBoss").AddComponent<InkBossController>();
            inkBoss.Init(cfg, field, bubble, guardian);

            var piranhas = NewChild("PiranhaBoss").AddComponent<PiranhaBossController>();
            piranhas.Init(cfg, field, bubble, guardian);

            var turtles = NewChild("TurtleBoss").AddComponent<TurtleBossController>();
            turtles.Init(cfg, field, bubble, guardian);

            var shark = NewChild("SharkBoss").AddComponent<SharkBossController>();
            shark.Init(cfg, field, bubble, guardian);

            var manta = NewChild("MantaBoss").AddComponent<MantaBossController>();
            manta.Init(cfg, field, bubble, guardian, projectileRoot, fx);

            // Порядок массива — это и есть порядок выхода боссов. Добавить следующего
            // теперь значит дописать сюда одну строчку: GameManager принимает список,
            // а не россыпь отдельных ссылок.
            var bosses = new IBoss[]
            {
                // Порядок = расписание по глубине: 1000 м — медуза, 2000 — змей и так
                // далее до ската на 10000 м. Сложность внутри ряда нарастает не только
                // самими боссами, но и тем, сколько игрок к этому моменту уже проплыл.
                boss,       // 1000 м — Грозовая медуза
                serpent,    // 2000 м — Змей бездны
                swordfish,  // 3000 м — Клинок бездны
                turtles,    // 4000 м — Каменный панцирь
                piranhas,   // 5000 м — Стая пираний
                inkBoss,    // 6000 м — Чернильный морок
                octopus,    // 7000 м — Спрут глубин
                crab,       // 8000 м — Клешнерук
                shark,      // 9000 м — Белая смерть
                manta,      // 10000 м — Великая тень
            };

            var audio = NewChild("Audio").AddComponent<AudioManager>();
            audio.Init(cfg);

            // Систему наград пересчитываем ДО сборки интерфейса: меню рисует значок
            // с числом готовых наград, и считать его по пустому состоянию значило бы
            // показать ноль там, где игрока ждёт сундук.
            RewardsHub.Refresh();
            NotificationService.Prepare();

            var ui = NewChild("UI").AddComponent<UIManager>();
            ui.Init(cfg);
            _ui = ui;

            _platform = CreatePlatform();

            var gm = gameObject.AddComponent<GameManager>();
            gm.Init(cfg, field, bubble, guardian, spawner, fishSpawner, formationSpawner, scenerySpawner,
                    zoneSpawner, spireSpawner, bosses, inkBoss, manta,
                    background, fx, ui, audio, _platform);

            // Инициализируем площадку ПОСЛЕ GameManager: без сети она отдаёт рекорд и язык
            // сразу, синхронно. Если сделать это раньше, события уйдут в пустоту и рекорд
            // не подхватится — а от него зависят и магазин, и экран поражения.
            _platform.Init(cfg);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // В релизной сборке этой ветки нет: препроцессор вырезает её целиком.
            var dev = NewChild("DevPanel").AddComponent<DevPanel>();
            dev.Init(cfg, gm, bubble, guardian);
#endif
        }

        void Start()
        {
            // Стартовый экран уже на экране и кликабелен — можно убирать загрузчик Яндекса.
            _platform.NotifyGameReady();

            // Порядок важен. Вступление — первому запуску, окно «С возвращением» —
            // тому, кто уже играл; одновременно они не нужны никогда, и показывать
            // новичку «вас не было три дня» было бы странно вдвойне.
            if (!_ui.TryShowTutorial()) _ui.MaybeShowWelcomeBack();
        }

        /// <summary>
        /// Уход в фон и возвращение.
        ///
        /// Напоминания ставятся ровно тогда, когда игрок уходит, и снимаются, когда
        /// возвращается: звать туда, где человек уже есть, незачем. Заодно это
        /// единственный надёжный момент записать время на диск — OnApplicationQuit
        /// на Android вызывается не всегда.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                TimeService.Persist();
                NotificationService.Reschedule();
            }
            else
            {
                NotificationService.Clear();
                RewardsHub.Refresh();
            }
        }

        void OnApplicationQuit()
        {
            TimeService.Persist();
            NotificationService.Reschedule();
        }

        /// <summary>
        /// Единственное место во всей игре, где решается, на какой площадке мы запущены.
        /// Ветка выбирается препроцессором, поэтому в сборку под телефон код Яндекса
        /// не попадает вовсе, и наоборот.
        /// </summary>
        IPlatform CreatePlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return NewChild("AndroidPlatform").AddComponent<AndroidPlatform>();
#else
            // Имя объекта важно: по нему JS шлёт колбэки через SendMessage.
            return NewChild(YandexSdkManager.ObjectName).AddComponent<YandexSdkManager>();
#endif
        }

        Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Deep;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            return cam;
        }

        GameObject NewChild(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go;
        }
    }
}
