using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Весь интерфейс собирается кодом: стартовый экран, HUD, полоса босса,
    /// баннеры и экран поражения. Тексты — русские, в тоне прототипа.
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        public event Action PlayClicked;
        public event Action RetryClicked;
        public event Action RewardedClicked;
        /// <summary>Нажата кнопка-глушилка. Аргумент — новое состояние «выключено».</summary>
        public event Action MuteToggled;
        /// <summary>Нажата любая кнопка — для щелчка и для запуска звука после жеста игрока.</summary>
        public event Action ButtonClicked;
        /// <summary>Открылось меню — момент для межстраничной рекламы.</summary>
        public event Action MenuOpened;
        /// <summary>
        /// Открылся или закрылся экран, который обязан закрывать игру целиком.
        /// Аргумент — истина, когда такой экран открыт.
        ///
        /// Нужен, чтобы спрятать пузырёк со стражем: их свечение проступало
        /// сквозь интерфейс. Событие, а не прямой вызов, потому что UIManager
        /// про игровые объекты ничего не знает и знать не должен.
        /// </summary>
        public event Action<bool> ModalChanged;
        /// <summary>Открылся магазин — тоже логическая пауза.</summary>
        public event Action ShopOpened;
        /// <summary>Скин куплен — поводов больше нет, но звук награды тут уместен.</summary>
        public event Action SkinPurchased;
        /// <summary>Игрок нажал паузу.</summary>
        public event Action PauseClicked;
        /// <summary>Игрок закрыл паузу и хочет играть дальше.</summary>
        public event Action ResumeClicked;
        /// <summary>Игрок бросает партию и уходит в меню.</summary>
        public event Action QuitToMenuClicked;
        /// <summary>Переключена музыка в настройках.</summary>
        public event Action MusicToggled;
        /// <summary>Игрок выбрал скин: (0 — пузырёк, 1 — страж).</summary>
        public event Action<int, int> SkinChosen;

        // Плитки идут сеткой. В каждой вкладке: цветные за глубину и за отбитое,
        // по одному трофейному с каждого босса и награда за полную коллекцию.

        /// <summary>
        /// Слот, в котором стоит награда за полную коллекцию: центр последнего ряда.
        /// Она стоит отдельно от общего ряда намеренно — это не очередной скин, а
        /// отметка о том, что собрано всё остальное.
        /// </summary>

        /// <summary>
        /// Какой скин показывать в слоте; −1 — слот пустой.
        ///
        /// Награда за коллекцию всегда лежит последней в каталоге, поэтому её и
        /// вынимаем из общего ряда: остальные идут подряд, а она — в свой слот.
        /// </summary>

        struct ShopTile
        {
            public RectTransform Card;
            public Image Background;
            /// <summary>Начинка материала: лежит под оболочкой и красится оттенком скина.</summary>
            public Image Core;
            public Image Icon;
            public Text Name;
            public Text Rarity;
            /// <summary>Цена или статус — одно поле: вместе они на карточке не нужны.</summary>
            public Text Lock;
            public Image PriceIcon;
            /// <summary>Прямоугольник монеты: раскладку строки считаем по нему.</summary>
            public RectTransform PriceIconRect;
            /// <summary>Держатель шарика: его и анимируем, чтобы слои не разъезжались.</summary>
            public RectTransform Ball;
            /// <summary>Одна строка на два случая: «рекомендуем» или «не хватает N».</summary>
            public Text Note;
        }

        const int RefW = 720;
        const int RefH = 1280;

        /// <summary>
        /// Ширина экранных раскладок. Канвас тянется по высоте, поэтому его ширина
        /// в опорных единицах зависит от соотношения сторон: на телефоне 21:9 она
        /// падает примерно до 550. Всё содержимое экранов обязано укладываться в это
        /// число, иначе на узких телефонах края обрежутся.
        /// </summary>
        const float ScreenWidth = 500f;

        GameConfig _cfg;

        RectTransform _canvasRect;
        RectTransform _column;      // центральная «портретная» колонка под HUD
        RectTransform _safeArea;    // экран за вычетом выреза и полосы жестов
        Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

        // HUD
        GameObject _hud;
        Text _depthText;
        Image[] _lifeIcons;
        GameObject _bossBar;
        Image _bossFill;
        Text _bossLabel;

        // Баннер
        Text _banner;
        float _bannerTimer;
        float _bannerAge;

        /// <summary>
        /// Экраны и баннеры появляются за это время, а не за кадр (E-17), если
        /// появляются поверх игры: пауза, итоги заплыва, полоса босса.
        /// Смена экрана на экран остаётся мгновенной: старый закрывается за
        /// кадр, и проявление нового с нуля показало бы между ними мир — замер
        /// дал скачок средней яркости 0.084 вместо прежних 0.022. [ТЕСТ]
        /// </summary>
        const float ScreenFadeIn = 0.2f;
        readonly GameObject[] _fadeRoots = new GameObject[10];
        readonly CanvasGroup[] _fadeGroups = new CanvasGroup[10];
        readonly bool[] _fadeWas = new bool[10];

        // Экраны
        GameObject _startScreen;
        GameObject _overScreen;
        Text _finalDepthText;
        Text _bestDepthText;
        Text _overSubText;
        GameObject _rewardedButton;
        GameObject _pauseScreen;
        GameObject _pauseButton;
        Image _muteIcon;

        // Меню, настройки, магазин
        Text _menuBestText;
        GameObject _settingsScreen;
        GameObject _shopScreen;
        Text _musicLabel;
        Text _soundLabel;
        Image _bubbleTab;
        Image _guardianTab;
        Image _shopPreview;
        Image _shopPreviewCore;
        Image _shopPreviewAura;
        /// <summary>Сколько частиц следа «накопилось» к выпуску в витрине.</summary>
        float _previewTrailEmit;
        Text _shopSkinName;
        Text _shopStatus;
        ShopTile[] _shopTiles;
        int _shopTab;
        float _previewPhase;

        // Фильтр и сортировка. Каталог вырос до тридцати шести записей, и листать
        // две страницы в поисках нужной редкости — не выбор, а поиск.
        int _shopFilter;            // 0 — все, дальше по редкостям, последний — боссы
        int _shopTools;             // 0 — по каталогу, 1–2 сортировка, 3–4 «куплено»
        Text _shopRarityLabel;
        Text _shopPurse;
        RectTransform _shopPurseIcon;
        Text _shopDemoLabel;
        Image _shopActionButton;
        const int DemoDotCount = 14;
        Image[] _demoDots;
        float[] _demoDotAge;
        Vector2[] _demoDotVelocity;
        Image _demoRing;
        Text _shopActionLabel;
        Image[] _shopTabButtons;
        Text[] _shopTabLabels;
        Text _shopToolsLabel;
        RectTransform _shopListContent;
        RectTransform _shopPreviewHolder;
        float _demoTimer;
        readonly System.Collections.Generic.List<int> _shopOrder = new System.Collections.Generic.List<int>();
        int _bestDepth;

        // Монеты. Счётчик на экране догоняет настоящий баланс плавно: монеты летят
        // к нему почти секунду, и цифра, прыгнувшая раньше прилёта, рассинхронила бы
        // анимацию с арифметикой.
        Text _coinText;
        /// <summary>Иконка монеты в HUD. По ней считается цель полёта монет.</summary>
        RectTransform _coinIcon;
        Text _menuCoinText;
        Text _coinsEarnedText;
        float _coinShown;

        const int CoinPopCount = 8;
        Text[] _coinPops;
        float[] _coinPopAge;
        Vector2[] _coinPopFrom;
        int _coinPopCursor;

        // Справочник по боссам
        GameObject _codexScreen;
        GameObject _codexList;
        GameObject _codexDetail;
        CodexRow[] _codexRows;
        Image _codexIcon;
        Text _codexName;
        Text _codexDepth;
        Text _codexBody;
        int _codexPage = -1;    // −1 — открыт список, иначе номер статьи

        class CodexRow
        {
            public Image Background;
            public Image Icon;
            public Text Label;
            public Button Button;
        }

        static Font _font;

        // ------------------------------------------------------------- сборка

        bool _lastMuted;
        bool _lastMusicOn = true;
        int _screen;            // 0 меню, 1 настройки, 2 магазин, 3 HUD, 4 поражение

        public void Init(GameConfig cfg)
        {
            _cfg = cfg;
            Loc.Changed += Rebuild;
            HookRewards();
            BuildAll();
        }

        void OnDestroy()
        {
            Loc.Changed -= Rebuild;
            UnhookRewards();
        }

        /// <summary>
        /// Смена языка пересобирает интерфейс целиком. Строки зашиты в виджеты при
        /// создании, и обновлять их по одной пришлось бы через десятки ссылок —
        /// пересборка надёжнее и происходит только по нажатию кнопки языка.
        /// </summary>
        void Rebuild()
        {
            if (_canvasRect != null)
            {
                var old = _canvasRect.gameObject;
                old.SetActive(false);       // Destroy отложен до конца кадра — гасим сразу
                Destroy(old);
            }

            BuildAll();
            SetBestDepth(_bestDepth);
            SetAudioState(_lastMuted, _lastMusicOn);

            switch (_screen)
            {
                case 1: ShowSettings(); break;
                case 2: ShowShop(); break;
                case 5: ShowCodex(); break;
                case 6: ShowRewards(); break;
                case 7: TryShowTutorial(); break;
                default: ShowStart(); break;
            }
        }

        void BuildAll()
        {
            var cfg = _cfg;
            var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Тянемся строго по высоте, а не в компромиссе 50/50. При компромиссе
            // опорная высота канваса на широком экране проседает (на 3840x2160 —
            // до 720 единиц), и высокие экраны вроде магазина не помещаются целиком.
            // С match = 1 высота канваса всегда ровно RefH, а плавает ширина —
            // под неё и подогнаны экраны, см. ScreenWidth.
            scaler.matchWidthOrHeight = 1f;

            _canvasRect = canvasGo.GetComponent<RectTransform>();

            EnsureEventSystem();

            // Безопасная зона. Всё, что живёт у кромки экрана — счётчик метров, жизни,
            // полоса босса, кнопки звука и паузы, — лежит внутри неё, иначе на телефоне
            // с вырезом верх HUD уезжает под чёлку, а нижние кнопки под полосу жестов.
            // Полноэкранные экраны меню сюда не входят намеренно: их затемнение обязано
            // закрывать кадр целиком, включая вырез, а содержимое у них и так по центру.
            _safeArea = NewRect("SafeArea", _canvasRect);
            // Сначала растягиваем на весь канвас. Это состояние по умолчанию на случай,
            // если пересчёт не сработает: у свежего RectTransform размер около сотни
            // единиц в центре экрана, и всё, что к нему привязано углами, схлопнулось
            // бы в кучку посреди кадра — ровно этот баг и был.
            Stretch(_safeArea);

            // Пересборка интерфейса создаёт новый прямоугольник, а память о прошлом
            // размере живёт в самом UIManager и пересборку переживает. Без сброса
            // ApplySafeArea решил бы, что менять нечего, и новый прямоугольник остался
            // бы ненастроенным.
            _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
            ApplySafeArea();

            _column = NewRect("Column", _safeArea);
            _column.anchorMin = _column.anchorMax = new Vector2(0.5f, 0.5f);
            _column.pivot = new Vector2(0.5f, 0.5f);
            _column.anchoredPosition = Vector2.zero;

            BuildHud();
            BuildBanner();
            BuildStartScreen();
            BuildSettingsScreen();
            BuildShopScreen();
            BuildTutorialScreen();
            BuildRewardsScreen();
            BuildCodexScreen();
            BuildOverScreen();
            BuildPauseScreen();
            BuildMuteButton();  // последними — чтобы кнопки были поверх экранов
            BuildPauseButton();

            LayoutColumn();
            ShowStart();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void LayoutColumn()
        {
            Vector2 size = _safeArea != null ? _safeArea.rect.size : _canvasRect.rect.size;
            if (size.x <= 1f || size.y <= 1f) size = new Vector2(RefW, RefH);
            float w = Mathf.Min(size.x, size.y * _cfg.designAspect);
            _column.sizeDelta = new Vector2(w, size.y);
        }

        /// <summary>
        /// Подогнать безопасную зону под экран. Считается в долях, а не в пикселях:
        /// канвас масштабируется, и абсолютные отступы разъехались бы на плотных экранах.
        ///
        /// Пересчитывается каждый кадр, но реально что-то меняет только при повороте
        /// или смене режима навигации — сравнение прямоугольников дешевле, чем поиск
        /// потом причины, почему HUD уполз на чужом телефоне.
        /// </summary>
        void ApplySafeArea()
        {
            if (_safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;

            var safe = Screen.safeArea;
            if (safe == _lastSafeArea) return;
            _lastSafeArea = safe;

            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
        }

        public void Tick(float dt)
        {
            ApplySafeArea();
            LayoutColumn();

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= dt;
                _bannerAge += dt;
                var c = _banner.color;
                // Появление за 0.2 с (E-17), угасание за 0.4 — как было.
                c.a = Mathf.Clamp01(Mathf.Min(_bannerTimer / 0.4f, _bannerAge / ScreenFadeIn));
                _banner.color = c;
                if (_bannerTimer <= 0f) _banner.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- HUD

        void BuildHud()
        {
            _hud = NewRect("HUD", _column).gameObject;
            Stretch(_hud.GetComponent<RectTransform>());

            // глубина
            var depthBg = NewRect("Depth", _hud.transform);
            depthBg.anchorMin = depthBg.anchorMax = new Vector2(0f, 1f);
            depthBg.pivot = new Vector2(0f, 1f);
            depthBg.anchoredPosition = new Vector2(24f, -24f);
            depthBg.sizeDelta = new Vector2(190f, 62f);
            var bgImg = depthBg.gameObject.AddComponent<Image>();
            bgImg.sprite = SpriteFactory.RoundedOutline("hud", 0.35f, 0.06f);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(Palette.Aqua.r, Palette.Aqua.g, Palette.Aqua.b, 0.28f);
            bgImg.raycastTarget = false;

            _depthText = NewText("DepthText", depthBg, Loc.Get("hud.metersZero"), 34, Palette.Foam, TextAnchor.MiddleCenter);
            Stretch(_depthText.rectTransform);
            _depthText.fontStyle = FontStyle.Bold;

            // Баланс монет — под счётчиком метров, в том же левом столбце. Именно
            // сюда слетаются монеты из разбитых препятствий (цель задаёт GameManager),
            // поэтому двигать блок можно только вместе с той точкой.
            var coinBg = NewRect("CoinBg", _hud.transform);
            coinBg.anchorMin = coinBg.anchorMax = new Vector2(0f, 1f);
            coinBg.pivot = new Vector2(0f, 1f);
            coinBg.anchoredPosition = new Vector2(24f, -92f);
            coinBg.sizeDelta = new Vector2(150f, 46f);
            var coinBgImg = coinBg.gameObject.AddComponent<Image>();
            coinBgImg.sprite = SpriteFactory.RoundedOutline("hud", 0.35f, 0.06f);
            coinBgImg.type = Image.Type.Sliced;
            coinBgImg.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.22f);
            coinBgImg.raycastTarget = false;

            // Значок и число — пара, выровненная по центру плашки. Раньше значок был
            // прибит к левому краю, а число к правому, и при коротком балансе между
            // ними зияла дыра: «монета ... 7» читалось как два разных показателя.
            var coinIcon = NewRect("CoinIcon", coinBg);
            coinIcon.sizeDelta = new Vector2(28f, 28f);
            var coinIconImg = coinIcon.gameObject.AddComponent<Image>();
            coinIconImg.sprite = SpriteFactory.Coin();
            coinIconImg.preserveAspect = true;
            coinIconImg.raycastTarget = false;
            _coinIcon = coinIcon;

            _coinText = NewText("CoinText", coinBg, "0", 26, Palette.Gold, TextAnchor.MiddleLeft);
            _coinText.rectTransform.sizeDelta = new Vector2(100f, 36f);
            _coinText.fontStyle = FontStyle.Bold;

            // Раскладку считает LayoutCoinRow и пересчитывает при каждом изменении
            // числа: ширина «7» и «7000» отличается втрое.
            LayoutCoinRow(coinIcon, _coinText);

            BuildCoinPops();

            // жизни
            var livesRoot = NewRect("Lives", _hud.transform);
            livesRoot.anchorMin = livesRoot.anchorMax = new Vector2(1f, 1f);
            livesRoot.pivot = new Vector2(1f, 1f);
            livesRoot.anchoredPosition = new Vector2(-24f, -24f);
            livesRoot.sizeDelta = new Vector2(160f, 62f);

            _lifeIcons = new Image[Mathf.Max(1, _cfg.startLives)];
            for (int i = 0; i < _lifeIcons.Length; i++)
            {
                var icon = NewRect("Life" + i, livesRoot);
                icon.anchorMin = icon.anchorMax = new Vector2(1f, 0.5f);
                icon.pivot = new Vector2(1f, 0.5f);
                icon.sizeDelta = new Vector2(38f, 38f);
                icon.anchoredPosition = new Vector2(-i * 46f, 0f);
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = SpriteFactory.Guardian();
                img.raycastTarget = false;
                _lifeIcons[i] = img;
            }

            // Полоса босса — внизу экрана. Наверху она соседствовала со счётчиком
            // метров и полосой жизней, и в бою взгляд разрывался между тремя
            // показателями в одном углу. Внизу она сама по себе и не мешает
            // читать глубину.
            var barRoot = NewRect("BossBar", _hud.transform);
            barRoot.anchorMin = barRoot.anchorMax = new Vector2(0.5f, 0f);
            barRoot.pivot = new Vector2(0.5f, 0f);
            // Высота и ширина подобраны под две соседние вещи: кнопка звука занимает
            // правый нижний угол (72 px от края), а стража игрок иногда уводит к самой
            // нижней кромке — под кольцо черепах. Полоса поэтому узкая и не во всю
            // ширину: даже когда страж проходит за ней, видно почти весь его шар.
            barRoot.anchoredPosition = new Vector2(0f, 104f);
            barRoot.sizeDelta = new Vector2(360f, 18f);
            _bossBar = barRoot.gameObject;

            var barBg = barRoot.gameObject.AddComponent<Image>();
            barBg.sprite = SpriteFactory.RoundedRect("bar", 0.5f);
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.60f);
            barBg.raycastTarget = false;

            var fillRect = NewRect("Fill", barRoot);
            Stretch(fillRect, 3f);
            _bossFill = fillRect.gameObject.AddComponent<Image>();
            _bossFill.sprite = SpriteFactory.RoundedRect("bar", 0.5f);
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.fillOrigin = 0;
            _bossFill.fillAmount = 1f;
            _bossFill.color = Palette.Coral;
            _bossFill.raycastTarget = false;

            // Подпись над полосой, а не под: под ней кромка экрана и кнопка звука.
            _bossLabel = NewText("Label", barRoot, Loc.Get("hud.boss"), 20, Palette.Coral, TextAnchor.LowerCenter);
            _bossLabel.raycastTarget = false;
            var label = _bossLabel;
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            label.rectTransform.sizeDelta = new Vector2(0f, 28f);
            label.fontStyle = FontStyle.Bold;

            _bossBar.SetActive(false);
            _hud.SetActive(false);
        }

        void BuildBanner()
        {
            _banner = NewText("Banner", _column, "", 52, Palette.Gold, TextAnchor.MiddleCenter);
            var rt = _banner.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 180f);
            rt.sizeDelta = new Vector2(-40f, 140f);
            _banner.fontStyle = FontStyle.Bold;
            _banner.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Финальное поздравление занимает три строки и в 140 точек не влезает.
            // Текст выровнен по центру, поэтому лишние строки расходятся симметрично
            // и ничего не сдвигают.
            _banner.verticalOverflow = VerticalWrapMode.Overflow;
            _banner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------- экраны

        /// <param name="seeThrough">
        /// Пропускать ли сквозь себя игровой мир. Истина только у стартового
        /// экрана: там плывущий фон — часть вида, и бояться нечего. У всех
        /// остальных ложь, потому что сквозь полупрозрачную виньетку проступали
        /// пузырёк со стражем — их свечение читалось прямо поверх кнопок.
        /// </param>
        GameObject BuildScreenRoot(string name, bool seeThrough = false)
        {
            var rt = NewRect(name, _canvasRect);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = seeThrough ? SpriteFactory.Vignette() : SpriteFactory.VignetteSolid();
            img.color = Color.white;
            img.raycastTarget = true; // перекрываем ввод по стражу, пока экран открыт
            return rt.gameObject;
        }

        // ------------------------------------------------------- главный экран

        void BuildStartScreen()
        {
            _startScreen = BuildScreenRoot("MenuScreen", seeThrough: true);

            var content = NewRect("Content", _startScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 900f);

            // Кегль подобран под самое длинное из названий: английское BUBBLE RISE
            // длиннее русского ПУЗЫРЁК и при 92 не помещалось в ширину экрана.
            var title = NewText("Title", content, Loc.Get("game.title"), 74, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(ScreenWidth, 110f));
            title.fontStyle = FontStyle.Bold;

            var subtitle = NewText("Subtitle", content, Loc.Get("game.subtitle"), 26,
                                   Palette.Foam.WithAlpha(0.7f), TextAnchor.MiddleCenter);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(ScreenWidth, 40f));

            _menuBestText = NewText("Best", content, "", 28, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_menuBestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(ScreenWidth, 40f));
            _menuBestText.fontStyle = FontStyle.Bold;

            // Баланс в меню — чтобы решение «ещё заплыв или уже в магазин» принималось
            // здесь, а не после захода в магазин и обратно.
            _menuCoinText = NewText("MenuCoins", content, "", 24, Palette.Gold.WithAlpha(0.9f),
                                    TextAnchor.MiddleCenter);
            Anchor(_menuCoinText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -258f),
                   new Vector2(ScreenWidth, 34f));

            // Главное действие экрана — крупная кнопка по центру, всё остальное мельче.
            // Ряд второстепенных кнопок поднят: под ним встал справочник, а ниже
            // начинается подсказка у нижней кромки, и наезжать на неё нельзя.
            var play = NewButton("PlayButton", content, Loc.Get("menu.play"), Palette.Aqua, Palette.Deep);
            Anchor(play.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 116f));
            play.text.fontSize = 40;
            play.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); PlayClicked?.Invoke(); });

            // Три кнопки в ряд вместо прежних двух. Награды встали между магазином
            // и настройками намеренно: это ежедневная кнопка, и искать её в
            // настройках игрок не стал бы, а отдельной строкой ниже она спорила бы
            // со справочником за внимание.
            var shop = NewButton("ShopButton", content, Loc.Get("menu.shop"), Palette.Gold, Palette.Deep);
            Anchor(shop.rt, new Vector2(0.5f, 0.5f), new Vector2(-162f, -110f), new Vector2(156f, 76f));
            shop.text.fontSize = 22;
            shop.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowShop(); });

            BuildRewardsMenuButton(content);

            var settings = NewButton("SettingsButton", content, Loc.Get("menu.settings"),
                                     Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(settings.rt, new Vector2(0.5f, 0.5f), new Vector2(162f, -110f), new Vector2(156f, 76f));
            settings.text.fontSize = 22;
            settings.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowSettings(); });

            var codex = NewButton("CodexButton", content, Loc.Get("menu.codex"),
                                  Palette.Aqua.WithAlpha(0.85f), Palette.Deep);
            Anchor(codex.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -196f), new Vector2(384f, 76f));
            codex.text.fontSize = 26;
            codex.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowCodex(); });

            var hint = NewText("Hint", content, Loc.Get("menu.hint"), 24, Palette.Foam.WithAlpha(0.75f), TextAnchor.UpperCenter);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(ScreenWidth - 20f, 90f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ---------------------------------------------------------- настройки

        void BuildSettingsScreen()
        {
            _settingsScreen = BuildScreenRoot("SettingsScreen");

            var content = NewRect("Content", _settingsScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 1080f);

            var title = NewText("Title", content, Loc.Get("settings.title"), 56, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(ScreenWidth, 80f));
            title.fontStyle = FontStyle.Bold;

            // Сложность — первой: это главная настройка игры, и ради неё сюда и
            // заходят. Музыка и звук ниже: их меняют раз в жизни.
            var difficulty = NewButton("DifficultyButton", content, "", Palette.Gold, Palette.Deep);
            Anchor(difficulty.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(420f, 88f));
            difficulty.text.fontSize = 28;
            difficulty.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                Difficulty.Cycle();
                RefreshSettings();
            });
            _difficultyLabel = difficulty.text;

            var diffHint = NewText("DiffHint", content, Loc.Get("diff.hint"), 19,
                                   Palette.Foam.WithAlpha(0.6f), TextAnchor.UpperCenter);
            Anchor(diffHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 148f),
                   new Vector2(ScreenWidth - 20f, 58f));
            diffHint.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Яркость эффектов — сразу за сложностью и выше звука. Это настройка
            // доступности: тот, кому она нужна, не должен за ней листать.
            //
            // Трёх ступеней, а не выключателя: «меньше вспышек» резало и размер
            // тоже, и у большинства игроков эффекты скинов не читались вовсе.
            // Теперь убавляется только то, что вредит глазам, а «Тихо» — это
            // прежний щадящий режим под своим именем.
            //
            // Рядом — «Без вспышек» (K-02): отдельный выключатель поверх ступени.
            // Он включён по умолчанию у всех, и место ему в том же ряду: это одна
            // тема, и искать вторую половину ниже звука никто не станет.
            var calm = NewButton("CalmButton", content, "", Palette.Aqua, Palette.Deep);
            Anchor(calm.rt, new Vector2(0.5f, 0.5f), new Vector2(-108f, 86f), new Vector2(204f, 88f));
            calm.text.fontSize = 20;
            calm.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                Visuals.CycleLevel();
                RefreshSettings();
            });
            _calmLabel = calm.text;

            var noFlash = NewButton("NoFlashButton", content, "", Palette.Aqua, Palette.Deep);
            Anchor(noFlash.rt, new Vector2(0.5f, 0.5f), new Vector2(108f, 86f), new Vector2(204f, 88f));
            noFlash.text.fontSize = 20;
            noFlash.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                Visuals.NoFlashes = !Visuals.NoFlashes;
                RefreshSettings();
            });
            _noFlashLabel = noFlash.text;

            // Одна строка ровно в промежутке между рядом яркости (низ на 42) и
            // музыкой (верх на 16): две строки уходили под кнопки. [ТЕСТ]
            var calmHint = NewText("CalmHint", content, Loc.Get("settings.calmHint"), 17,
                                   Palette.Foam.WithAlpha(0.6f), TextAnchor.MiddleCenter);
            Anchor(calmHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 29f),
                   new Vector2(ScreenWidth - 16f, 26f));
            calmHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            calmHint.resizeTextForBestFit = true;
            calmHint.resizeTextMinSize = 16;
            calmHint.resizeTextMaxSize = 17;

            var music = NewButton("MusicButton", content, "", Palette.Aqua, Palette.Deep);
            Anchor(music.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(420f, 88f));
            music.text.fontSize = 28;
            music.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); MusicToggled?.Invoke(); });
            _musicLabel = music.text;

            var sound = NewButton("SoundButton", content, "", Palette.Aqua, Palette.Deep);
            Anchor(sound.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -126f), new Vector2(420f, 88f));
            sound.text.fontSize = 28;
            sound.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); MuteToggled?.Invoke(); });
            _soundLabel = sound.text;

            // Вибрация: кнопка есть всегда, но на устройстве без вибратора она
            // гаснет. Прятать нельзя — игрок искал бы её и решил, что игра сломана.
            var vibration = NewButton("VibrationButton", content, "", Palette.Aqua, Palette.Deep);
            Anchor(vibration.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -224f), new Vector2(420f, 88f));
            vibration.text.fontSize = 28;
            vibration.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                Haptics.Toggle();
                if (Haptics.Enabled) Haptics.Play(HapticPattern.Reward);   // сразу дать почувствовать
                RefreshSettings();
            });
            _vibrationLabel = vibration.text;
            _vibrationButton = vibration.button;

            // Кнопка языка. Требование площадки: игра из списка языков обязана
            // действительно открываться на этом языке, и переключать его должен игрок.
            var language = NewButton("LanguageButton", content, Loc.Get("settings.language"),
                                     Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(language.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -322f), new Vector2(420f, 88f));
            language.text.fontSize = 28;
            language.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                Loc.Toggle();      // Loc.Changed приведёт к полной пересборке интерфейса
            });

            var note = NewText("Note", content, Loc.Get("settings.note"), 22, Palette.Foam.WithAlpha(0.6f), TextAnchor.UpperCenter);
            Anchor(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -416f), new Vector2(ScreenWidth - 20f, 60f));
            note.horizontalOverflow = HorizontalWrapMode.Wrap;

            AddBackButton(content);
            RefreshSettings();
            _settingsScreen.SetActive(false);
        }

        // ------------------------------------------------------------ магазин

        void BuildShopScreen()
        {
            _shopScreen = BuildScreenRoot("ShopScreen");

            var content = NewRect("Content", _shopScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ShopLayout.ContentWidth, ShopLayout.ContentHeight);

            BuildShopHeader(content);
            BuildShopPreview(content);
            BuildShopFilters(content);
            BuildShopList(content);

            AddBackButton(content);

            // Большой показ — последним ребёнком экрана, поверх всего
            // остального: он перекрывает и список, и панель выбранного.
            BuildBigDemo(_shopScreen);

            _shopScreen.SetActive(false);
        }

        /// <summary>Шапка: название, баланс и переключатель половинок пары.</summary>
        void BuildShopHeader(RectTransform content)
        {
            var title = NewText("Title", content, Loc.Get("shop.title"), 40, Palette.Aqua, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(-130f, ShopLayout.HeaderY), new Vector2(220f, ShopLayout.HeaderH));
            title.fontStyle = FontStyle.Bold;

            // Баланс: значок и число внутри одной плашки, по центру по вертикали.
            // Раньше значок стоял у левого края, число у правого, и при длинном
            // балансе они налезали друг на друга.
            var purse = NewRect("Purse", content);
            Anchor(purse, new Vector2(0.5f, 1f),
                   new Vector2(146f, ShopLayout.HeaderY), new Vector2(180f, ShopLayout.HeaderH));
            var purseBg = purse.gameObject.AddComponent<Image>();
            purseBg.sprite = SpriteFactory.RoundedRect("btn", 0.4f);
            purseBg.type = Image.Type.Sliced;
            purseBg.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.22f);
            purseBg.raycastTarget = false;

            var purseIcon = NewRect("Icon", purse);
            purseIcon.sizeDelta = new Vector2(26f, 26f);
            var purseImg = purseIcon.gameObject.AddComponent<Image>();
            purseImg.sprite = SpriteFactory.Coin();
            purseImg.preserveAspect = true;
            purseImg.raycastTarget = false;
            _shopPurseIcon = purseIcon;

            _shopPurse = NewText("Balance", purse, "0", 22, Palette.Gold, TextAnchor.MiddleLeft);
            _shopPurse.rectTransform.sizeDelta = new Vector2(124f, 30f);
            _shopPurse.fontStyle = FontStyle.Bold;
            LayoutCoinRow(purseIcon, _shopPurse);

            // Две вкладки с иконкой: названия «Пузырь» и «Шарик» сами по себе
            // ничего не объясняли, и разницу между разделами приходилось
            // выяснивать методом тыка. Иконка показывает, что именно меняется,
            // строка под вкладками — зачем оно нужно.
            var bubbleTab = NewButton("TabBubble", content, Loc.Get("shop.tabBubble"), Palette.Aqua, Palette.Deep);
            Anchor(bubbleTab.rt, new Vector2(0.5f, 1f),
                   new Vector2(-120f, ShopLayout.TypeTabsY), new Vector2(228f, ShopLayout.TypeTabsH));
            bubbleTab.text.fontSize = 22;
            bubbleTab.text.alignment = TextAnchor.MiddleRight;
            Stretch(bubbleTab.text.rectTransform, 10f);
            bubbleTab.text.rectTransform.offsetMin = new Vector2(52f, 10f);
            bubbleTab.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); SetShopTab(0); });
            _bubbleTab = bubbleTab.image;
            TabIcon(bubbleTab.rt, SpriteFactory.Bubble());

            var guardianTab = NewButton("TabGuardian", content, Loc.Get("shop.tabGuardian"), Palette.Aqua, Palette.Deep);
            Anchor(guardianTab.rt, new Vector2(0.5f, 1f),
                   new Vector2(120f, ShopLayout.TypeTabsY), new Vector2(228f, ShopLayout.TypeTabsH));
            guardianTab.text.fontSize = 22;
            guardianTab.text.alignment = TextAnchor.MiddleRight;
            Stretch(guardianTab.text.rectTransform, 10f);
            guardianTab.text.rectTransform.offsetMin = new Vector2(52f, 10f);
            guardianTab.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); SetShopTab(1); });
            _guardianTab = guardianTab.image;
            TabIcon(guardianTab.rt, SpriteFactory.GuardianGlass());

            _shopTabHint = NewText("TabHint", content, "", 18,
                                   Palette.Foam.WithAlpha(0.7f), TextAnchor.MiddleCenter);
            Anchor(_shopTabHint.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, ShopLayout.TabHintY),
                   new Vector2(ShopLayout.ContentWidth - 20f, ShopLayout.TabHintH));
        }

        /// <summary>Иконка слева внутри вкладки — та же вещь, что игрок покупает.</summary>
        void TabIcon(RectTransform tab, Sprite sprite)
        {
            var rt = NewRect("Icon", tab);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(14f, 0f);
            rt.sizeDelta = new Vector2(36f, 36f);

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        /// <summary>Панель выбранного скина: шарик слева, всё про него справа.</summary>
        void BuildShopPreview(RectTransform content)
        {
            var panel = NewRect("Preview", content);
            Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0f, ShopLayout.PreviewY),
                   new Vector2(ShopLayout.ContentWidth - ShopLayout.SidePadding * 2f, ShopLayout.PreviewH));
            var panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = SpriteFactory.RoundedRect("panel", 0.3f);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(Palette.Deep2.r, Palette.Deep2.g, Palette.Deep2.b, 0.9f);
            panelBg.raycastTarget = false;

            var ball = NewRect("Ball", panel);
            Anchor(ball, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewLeftX, ShopLayout.PreviewBallY),
                   new Vector2(ShopLayout.PreviewBallSize, ShopLayout.PreviewBallSize));
            _shopPreviewHolder = ball;

            // Аура — самый нижний слой, как и в игре: светится из-под тела, а не
            // поверх него. В витрине её не было вовсе, и мягкое свечение скина
            // игрок впервые видел уже в бою.
            var auraRect = NewRect("Aura", ball);
            Stretch(auraRect);
            _shopPreviewAura = auraRect.gameObject.AddComponent<Image>();
            _shopPreviewAura.sprite = SpriteFactory.SoftDot();
            _shopPreviewAura.raycastTarget = false;
            _shopPreviewAura.preserveAspect = true;
            _shopPreviewAura.enabled = false;

            var coreRect = NewRect("Core", ball);
            Stretch(coreRect);
            _shopPreviewCore = coreRect.gameObject.AddComponent<Image>();
            _shopPreviewCore.raycastTarget = false;
            _shopPreviewCore.preserveAspect = true;

            var shellRect = NewRect("Shell", ball);
            Stretch(shellRect);
            _shopPreview = shellRect.gameObject.AddComponent<Image>();
            _shopPreview.raycastTarget = false;
            _shopPreview.preserveAspect = true;

            BuildDemoLayers(ball);
            BuildDemoTarget(ball);

            _shopSkinName = NewText("Name", panel, "", 22, Palette.Foam, TextAnchor.MiddleLeft);
            Anchor(_shopSkinName.rectTransform, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX, ShopLayout.PreviewNameY),
                   new Vector2(ShopLayout.PreviewRightW, 26f));
            _shopSkinName.fontStyle = FontStyle.Bold;
            _shopSkinName.horizontalOverflow = HorizontalWrapMode.Overflow;

            _shopRarityLabel = NewText("Rarity", panel, "", 15, Palette.Foam, TextAnchor.MiddleLeft);
            Anchor(_shopRarityLabel.rectTransform, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX, ShopLayout.PreviewRarityY),
                   new Vector2(ShopLayout.PreviewRightW, 18f));

            _shopStatus = NewText("Status", panel, "", 15, Palette.Gold, TextAnchor.UpperLeft);
            Anchor(_shopStatus.rectTransform, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX, ShopLayout.PreviewStatusY),
                   new Vector2(ShopLayout.PreviewRightW, 26f));
            _shopStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

            var action = NewButton("Action", panel, "", Palette.Aqua, Palette.Deep);
            Anchor(action.rt, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX, ShopLayout.PreviewActionY),
                   new Vector2(ShopLayout.PreviewRightW, ShopLayout.PreviewActionH));
            action.text.fontSize = 20;
            action.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ActOnSelected(); });
            _shopActionButton = action.image;
            _shopActionLabel = action.text;

            // «Примерить» и «Сравнить» рядом: решение о покупке принимают,
            // посмотрев на скин в деле и рядом с тем, что уже надето. Без второго
            // сравнивать приходилось по памяти, выйдя из магазина и вернувшись.
            float half = (ShopLayout.PreviewRightW - 8f) * 0.5f;

            var demo = NewButton("Demo", panel, Loc.Get("fx.tryOn"), Palette.Gold, Palette.Deep);
            Anchor(demo.rt, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX - (half + 4f) * 0.5f, ShopLayout.PreviewButtonsY),
                   new Vector2(half, ShopLayout.PreviewButtonsH));
            demo.text.fontSize = 15;
            demo.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                _demoTimer = 0.01f;
                PlayShopEnvelope();
                OpenBigDemo();
            });
            _shopDemoLabel = demo.text;

            var compare = NewButton("Compare", panel, Loc.Get("fx.compare"), Palette.Deep2, Palette.Foam);
            Anchor(compare.rt, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX + (half + 4f) * 0.5f, ShopLayout.PreviewButtonsY),
                   new Vector2(half, ShopLayout.PreviewButtonsH));
            compare.text.fontSize = 15;
            compare.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ToggleCompare(); });
            _shopCompareLabel = compare.text;

            BuildSkinTraits(panel);
            BuildCompareBall(panel);
            BuildShopEnvelopes(panel);
        }

        /// <summary>
        /// Три метки-характеристики: удар, след, звук.
        ///
        /// Ровно три и ровно эти: именно они отвечают на вопрос «чем этот скин
        /// отличается от моего», который по одной картинке не читался. Больше трёх
        /// превратилось бы в таблицу характеристик, которую никто не читает.
        /// </summary>
        void BuildSkinTraits(RectTransform panel)
        {
            var caption = NewText("TraitsCaption", panel, Loc.Get("fx.table"), 12,
                                  Palette.Aqua.WithAlpha(0.7f), TextAnchor.MiddleLeft);
            Anchor(caption.rectTransform, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewRightX, ShopLayout.PreviewTraitsY),
                   new Vector2(ShopLayout.PreviewRightW, 16f));

            _shopTraits = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var row = NewText("Trait" + i, panel, "", 14, Palette.Foam.WithAlpha(0.9f), TextAnchor.MiddleLeft);
                Anchor(row.rectTransform, new Vector2(0f, 1f),
                       new Vector2(ShopLayout.PreviewRightX,
                                   ShopLayout.PreviewTraitsY - 16f - i * ShopLayout.PreviewTraitStep),
                       new Vector2(ShopLayout.PreviewRightW, ShopLayout.PreviewTraitStep));
                row.horizontalOverflow = HorizontalWrapMode.Overflow;
                _shopTraits[i] = row;
            }
        }

        /// <summary>
        /// Шарик «сейчас» под выбранным. Появляется по кнопке «Сравнить».
        ///
        /// Не отдельная сцена и не второй экран: два шарика рядом в той же витрине
        /// отвечают на вопрос быстрее любого переключения и не теряют контекст —
        /// цену и кнопку покупки видно там же.
        /// </summary>
        void BuildCompareBall(RectTransform panel)
        {
            var holder = NewRect("CompareBall", panel);
            Anchor(holder, new Vector2(0f, 1f),
                   new Vector2(ShopLayout.PreviewLeftX, ShopLayout.PreviewCompareY),
                   new Vector2(ShopLayout.PreviewCompareSize, ShopLayout.PreviewCompareSize));
            _shopCompareHolder = holder;

            var coreRect = NewRect("Core", holder);
            Stretch(coreRect);
            _shopCompareCore = coreRect.gameObject.AddComponent<Image>();
            _shopCompareCore.raycastTarget = false;
            _shopCompareCore.preserveAspect = true;

            var shellRect = NewRect("Shell", holder);
            Stretch(shellRect);
            _shopCompareShell = shellRect.gameObject.AddComponent<Image>();
            _shopCompareShell.raycastTarget = false;
            _shopCompareShell.preserveAspect = true;

            var caption = NewText("Caption", holder, Loc.Get("fx.current"), 12,
                                  Palette.Foam.WithAlpha(0.6f), TextAnchor.UpperCenter);
            Anchor(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -16f), new Vector2(90f, 16f));

            holder.gameObject.SetActive(false);
        }

        /// <summary>
        /// Строка редкостей и кнопка сортировки под ней.
        ///
        /// **Что было и почему вернулось.** Шесть чипов были свёрнуты в кнопку
        /// «Фильтры» с выпадающей панелью. Панель накрывала верхний ряд карточек,
        /// а до самих фильтров нужно было сначала догадаться. Строка вернулась на
        /// место: шесть чипов помещаются в ряд на любом экране, и видно, что
        /// именно сейчас выбрано, без единого нажатия.
        ///
        /// Ряд лежит в горизонтальной прокрутке. Сейчас она ничего не делает —
        /// 6 × 80 = 480 при ширине 500, — но седьмая редкость не сломает экран,
        /// а сделает ряд прокручиваемым.
        /// </summary>
        void BuildShopFilters(RectTransform content)
        {
            var viewport = NewRect("RarityViewport", content);
            Anchor(viewport, new Vector2(0.5f, 1f), new Vector2(0f, ShopLayout.RarityY),
                   new Vector2(ShopLayout.ContentWidth, ShopLayout.RarityH));
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            float span = ShopLayout.FilterCount * ShopLayout.FilterChipStepX;

            var row = NewRect("RarityRow", viewport);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(span, ShopLayout.RarityH);

            scroll.viewport = viewport;
            scroll.content = row;

            _shopTabButtons = new Image[ShopLayout.FilterCount];
            _shopTabLabels = new Text[ShopLayout.FilterCount];

            float left = -(ShopLayout.FilterCount - 1) * 0.5f * ShopLayout.FilterChipStepX;
            for (int i = 0; i < ShopLayout.FilterCount; i++)
            {
                int index = i;
                var chip = NewButton("Rarity" + i, row, "", Palette.Deep2, Palette.Foam);
                chip.rt.anchorMin = chip.rt.anchorMax = new Vector2(0.5f, 0.5f);
                chip.rt.pivot = new Vector2(0.5f, 0.5f);
                chip.rt.anchoredPosition = new Vector2(left + i * ShopLayout.FilterChipStepX, 0f);
                chip.rt.sizeDelta = new Vector2(ShopLayout.FilterChipW, ShopLayout.RarityH - 6f);

                // «ЛЕГЕНДАРНЫЙ» — одиннадцать букв, и в чип шириной 76 они не
                // влезают ни при каком постоянном кегле. С переносом слово
                // ломалось пополам: «ОБЫЧ/НЫЙ» в две строки читается как две
                // разные кнопки. Поэтому строка одна, а кегль подбирается:
                // короткие слова остаются крупными, длинное ужимается.
                chip.text.fontSize = 12;
                chip.text.horizontalOverflow = HorizontalWrapMode.Overflow;
                chip.text.verticalOverflow = VerticalWrapMode.Truncate;
                chip.text.resizeTextForBestFit = true;
                chip.text.resizeTextMinSize = 9;
                chip.text.resizeTextMaxSize = 13;
                chip.button.onClick.AddListener(() =>
                {
                    ButtonClicked?.Invoke();
                    _shopFilter = index;
                    RefreshShop();
                });
                _shopTabButtons[i] = chip.image;
                _shopTabLabels[i] = chip.text;
            }

            // Сортировка — одной кнопкой во всю ширину под редкостями. Перебирает
            // пять состояний и всегда показывает текущее словами.
            var sort = NewButton("Sort", content, "", Palette.Deep2, Palette.Foam);
            Anchor(sort.rt, new Vector2(0.5f, 1f), new Vector2(0f, ShopLayout.SortY),
                   new Vector2(ShopLayout.ContentWidth - ShopLayout.SidePadding * 2f,
                               ShopLayout.SortH));
            sort.text.fontSize = 15;
            sort.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); CycleShopTools(); });
            _shopSortLabel = sort.text;
            _shopToolsLabel = sort.text;   // одна и та же подпись, второго места нет
        }

        /// <summary>
        /// Список карточек с прокруткой.
        ///
        /// Листалка «‹ ›» убрана: страницы заставляли угадывать, на какой из них
        /// нужный скин. Прокрутка показывает тот же каталог сплошняком, а окно
        /// списка жёстко ограничено полосой — карточки физически не могут выехать
        /// на кнопку «Назад», как это было раньше.
        /// </summary>
        void BuildShopList(RectTransform content)
        {
            var viewport = NewRect("ListViewport", content);
            Anchor(viewport, new Vector2(0.5f, 1f), new Vector2(0f, ShopLayout.ListY),
                   new Vector2(ShopLayout.ContentWidth, ShopLayout.ListH));
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var list = NewRect("ListContent", viewport);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = new Vector2(0f, 0f);
            list.offsetMax = new Vector2(0f, 0f);
            list.sizeDelta = new Vector2(0f, ShopLayout.ListH);

            scroll.viewport = viewport;
            scroll.content = list;
            _shopListContent = list;

            // Карточек ровно столько, сколько скинов: пул на весь каталог, а не на
            // страницу — так при прокрутке ничего не пересоздаётся.
            int total = SkinStore.Bubble.Length;
            _shopTiles = new ShopTile[total];
            for (int i = 0; i < total; i++) _shopTiles[i] = BuildCard(list, i);
        }

        ShopTile BuildCard(RectTransform parent, int slot)
        {
            int index = slot;

            var card = NewRect("Card" + slot, parent);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(ShopLayout.CardW, ShopLayout.CardH);

            var background = card.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect("btn", 0.28f);
            background.type = Image.Type.Sliced;
            background.color = Palette.Deep2;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => { ButtonClicked?.Invoke(); TrySelectSkin(index); });

            // Шарик сверху по центру, подписи под ним — ничего не пересекается.
            var ball = NewRect("Ball", card);
            Anchor(ball, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(54f, 54f));

            var coreRect = NewRect("Core", ball);
            Stretch(coreRect);
            var core = coreRect.gameObject.AddComponent<Image>();
            core.raycastTarget = false;
            core.preserveAspect = true;

            var shellRect = NewRect("Shell", ball);
            Stretch(shellRect);
            var icon = shellRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            // Ритм карточки пересчитан по-честному, в точках от её верха. Было:
            // шарик −6…−60, имя −64…−88, редкость −90…−108, цена −110…−132,
            // подсказка −128…−146. Два изъяна сразу: подсказка налезала на строку
            // цены (и на «ВЫБРАНО», которое живёт в той же строке) на четыре
            // точки, а до нижнего края карточки высотой 150 ей оставалось тоже
            // четыре — оттого «РЕКОМЕНДУЕМ» и лежало на самом обрезе.
            //
            // Стало: −6…−60, −62…−86, −88…−104, −106…−126, −128…−142. Между
            // блоками по два пункта, под подсказкой восемь до края.
            var name = NewText("Name", card, "", 18, Palette.Foam, TextAnchor.MiddleCenter);
            Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(212f, 24f));
            name.fontStyle = FontStyle.Bold;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rarity = NewText("Rarity", card, "", 13, Palette.Foam, TextAnchor.MiddleCenter);
            Anchor(rarity.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(212f, 16f));

            // Монета и цена живут в своей строке, чтобы их можно было двигать
            // парой: при выравнивании по центру отступ между ними зависел от
            // длины числа, и у «360» он был вдвое больше, чем у «10000».
            var priceRow = NewRect("PriceRow", card);
            Anchor(priceRow, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(212f, 20f));

            var priceIcon = NewRect("PriceIcon", priceRow);
            priceIcon.sizeDelta = new Vector2(18f, 18f);
            var priceImg = priceIcon.gameObject.AddComponent<Image>();
            priceImg.sprite = SpriteFactory.Coin();
            priceImg.preserveAspect = true;
            priceImg.raycastTarget = false;

            var price = NewText("Price", priceRow, "", 17, Palette.Gold, TextAnchor.MiddleLeft);
            price.rectTransform.sizeDelta = new Vector2(104f, 20f);
            price.fontStyle = FontStyle.Bold;

            // Подсказка под ценой: либо «рекомендуем», либо сколько не хватает.
            // Одна строка на оба случая — в карточке высотой полтораста точек
            // второй не поместилось бы, а нужны они никогда не одновременно.
            var note = NewText("Note", card, "", 12, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(212f, 14f));
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.verticalOverflow = VerticalWrapMode.Overflow;

            return new ShopTile
            {
                Background = background, Core = core, Icon = icon,
                Name = name, Rarity = rarity, Lock = price, PriceIcon = priceImg,
                PriceIconRect = priceIcon,
                Ball = ball, Card = card, Note = note,
            };
        }

        // --------------------------------------------------------- справочник

        /// <summary>
        /// Экран справочника: список из десяти боссов и статья по выбранному.
        ///
        /// Обе половины живут в одном экране и переключаются включением объектов.
        /// Отдельным экраном статью делать не за чем: у неё та же рамка, тот же
        /// заголовок, и переключение списком было бы видно как мигание всего кадра.
        /// </summary>
        void BuildCodexScreen()
        {
            _codexScreen = BuildScreenRoot("CodexScreen");

            var content = NewRect("Content", _codexScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 1010f);

            var title = NewText("Title", content, Loc.Get("codex.title"), 50, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(ScreenWidth, 70f));
            title.fontStyle = FontStyle.Bold;

            BuildCodexList(content);
            BuildCodexDetail(content);

            _codexScreen.SetActive(false);
        }

        void BuildCodexList(RectTransform content)
        {
            var listRect = NewRect("List", content);
            Stretch(listRect);
            _codexList = listRect.gameObject;

            var hint = NewText("Hint", listRect, Loc.Get("codex.hint"), 20,
                               Palette.Foam.WithAlpha(0.6f), TextAnchor.UpperCenter);
            Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(ScreenWidth - 40f, 44f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            _codexRows = new CodexRow[Codex.Count];
            for (int i = 0; i < _codexRows.Length; i++)
            {
                int index = i;

                // Шаг 72 при высоте 64: десять строк плюс кнопка «Назад» — ровно та
                // высота, в которую экран ещё влезает целиком. Не увеличивайте, не
                // проверив нижнюю строку, она подходит к кнопке вплотную.
                var rowRect = NewRect("Row" + i, listRect);
                Anchor(rowRect, new Vector2(0.5f, 1f), new Vector2(0f, -162f - i * 72f),
                       new Vector2(ScreenWidth - 40f, 64f));

                var background = rowRect.gameObject.AddComponent<Image>();
                background.sprite = SpriteFactory.RoundedRect("btn", 0.35f);
                background.type = Image.Type.Sliced;
                background.color = Palette.Deep2;

                var button = rowRect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowCodexPage(index); });

                var iconRect = NewRect("Icon", rowRect);
                Anchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(-(ScreenWidth - 40f) * 0.5f + 44f, 0f),
                       new Vector2(46f, 46f));
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                // Текст строки выровнен по левому краю и начинается за значком: имена
                // у боссов разной длины, и по центру список выглядел бы рваным.
                var label = NewText("Label", rowRect, "", 22, Palette.Foam, TextAnchor.MiddleLeft);
                Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(38f, 0f),
                       new Vector2(ScreenWidth - 140f, 60f));
                label.raycastTarget = false;

                _codexRows[i] = new CodexRow
                {
                    Background = background, Icon = icon, Label = label, Button = button,
                };
            }

            AddBackButton(listRect);
        }

        void BuildCodexDetail(RectTransform content)
        {
            var detailRect = NewRect("Detail", content);
            Stretch(detailRect);
            _codexDetail = detailRect.gameObject;

            var iconRect = NewRect("Icon", detailRect);
            Anchor(iconRect, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(104f, 104f));
            _codexIcon = iconRect.gameObject.AddComponent<Image>();
            _codexIcon.raycastTarget = false;
            _codexIcon.preserveAspect = true;

            _codexName = NewText("Name", detailRect, "", 32, Palette.Coral, TextAnchor.MiddleCenter);
            Anchor(_codexName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(ScreenWidth, 44f));
            _codexName.fontStyle = FontStyle.Bold;

            _codexDepth = NewText("Depth", detailRect, "", 20, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_codexDepth.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(ScreenWidth, 32f));

            // Вся статья — один текст с разметкой вместо трёх подписей и трёх абзацев:
            // блоки разной длины, и при раздельных виджетах пришлось бы вручную
            // подгонять высоту каждого под самый длинный язык.
            // Кегль 19, а не 21: самая длинная статья — про Великую тень, и на 21 она
            // вылезала из отведённой высоты прямо на кнопку «Назад».
            _codexBody = NewText("Body", detailRect, "", 19, Palette.Foam, TextAnchor.UpperLeft);
            Anchor(_codexBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -292f),
                   new Vector2(ScreenWidth - 50f, 580f));
            _codexBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _codexBody.verticalOverflow = VerticalWrapMode.Overflow;
            _codexBody.lineSpacing = 1.1f;
            // Единственный текст в игре с включённой разметкой: в NewText она выключена
            // для всех, чтобы случайная угловая скобка в переводе не съела строку.
            // Здесь теги ставит только Section, и содержимое статьи в них не попадает.
            _codexBody.supportRichText = true;

            var back = NewButton("BackButton", detailRect, Loc.Get("common.back"),
                                 Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(back.rt, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(260f, 80f));
            back.text.fontSize = 28;
            // Из статьи «назад» ведёт в список, а не в меню: иначе за чтением двух
            // статей подряд игрок каждый раз проходил бы через главный экран.
            back.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowCodexList(); });

            _codexDetail.SetActive(false);
        }

        /// <summary>
        /// Открыта ли статья. В дев-сборке открыты все — иначе, чтобы посмотреть на
        /// вёрстку десятой статьи, пришлось бы каждый раз доплывать до 10000 метров.
        /// </summary>
        static bool CodexUnlocked(BossKind kind)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SkinStore.DevUnlockAll) return true;
#endif
            return SkinStore.IsBossMet(kind);
        }

        void RefreshCodexList()
        {
            if (_codexRows == null) return;

            for (int i = 0; i < _codexRows.Length; i++)
            {
                var kind = Codex.Order[i];
                var row = _codexRows[i];
                bool met = CodexUnlocked(kind);

                row.Button.interactable = met;
                row.Background.color = met ? Palette.Deep2 : Palette.Deep2.WithAlpha(0.45f);

                row.Icon.sprite = SpriteFactory.GuardianSkin(Codex.Art(kind));
                // Неоткрытый босс показывается силуэтом: рисунок уже намекал бы, кто там.
                row.Icon.color = met ? Color.white : Palette.Deep.WithAlpha(0.9f);

                string depth = Codex.DepthOf(i) + Loc.Get("hud.meters");
                row.Label.text = met ? depth + "  ·  " + Codex.Name(kind)
                                     : depth + "  ·  " + Loc.Get("codex.locked");
                row.Label.color = met ? Palette.Foam : Palette.Foam.WithAlpha(0.4f);
            }
        }

        void ShowCodexList()
        {
            _codexPage = -1;
            _codexDetail.SetActive(false);
            _codexList.SetActive(true);
            RefreshCodexList();
        }

        void ShowCodexPage(int index)
        {
            if (index < 0 || index >= Codex.Count) return;

            var kind = Codex.Order[index];
            if (!CodexUnlocked(kind)) return;

            _codexPage = index;
            _codexList.SetActive(false);
            _codexDetail.SetActive(true);

            _codexIcon.sprite = SpriteFactory.GuardianSkin(Codex.Art(kind));
            _codexIcon.color = Color.white;
            _codexName.text = Codex.Name(kind);
            _codexDepth.text = Loc.Get("codex.depth") + Codex.DepthOf(index) + Loc.Get("hud.meters");

            _codexBody.text =
                Section(Loc.Get("codex.what"), Codex.What(kind)) +
                Section(Loc.Get("codex.weak"), Codex.Weak(kind)) +
                Section(Loc.Get("codex.how"), Codex.How(kind), last: true);
        }

        /// <summary>Заголовок блока плюс текст. Цвет заголовка задан тегом — так блоки видны сразу.</summary>
        static string Section(string header, string body, bool last = false)
        {
            string gold = ColorUtility.ToHtmlStringRGB(Palette.Gold);
            return "<b><color=#" + gold + ">" + header + "</color></b>\n" + body + (last ? "" : "\n\n");
        }

        void AddBackButton(RectTransform content)
        {
            var back = NewButton("BackButton", content, Loc.Get("common.back"), Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(back.rt, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(260f, 80f));
            back.text.fontSize = 28;
            back.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowStart(); });
        }

        void BuildOverScreen()
        {
            _overScreen = BuildScreenRoot("OverScreen");

            var content = NewRect("Content", _overScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 700f);

            // Кегль 50, а не 62: «Пузырёк лопнул» при ширине экрана 500 переносилось
            // на две строки и налезало на подзаголовок. Проверяйте после правок текста.
            var title = NewText("Title", content, Loc.Get("over.title"), 50, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(ScreenWidth, 90f));
            title.fontStyle = FontStyle.Bold;

            _overSubText = NewText("Sub", content, Loc.Get("over.sub"), 27, Palette.Foam.WithAlpha(0.85f), TextAnchor.UpperCenter);
            Anchor(_overSubText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(ScreenWidth - 20f, 90f));
            _overSubText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // статистика
            var stats = NewRect("Stats", content);
            Anchor(stats, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(ScreenWidth - 20f, 130f));

            _finalDepthText = BuildStat(stats, -130f, Loc.Get("over.meters"), out _);
            _bestDepthText = BuildStat(stats, 130f, Loc.Get("over.record"), out _);

            // Сколько заработано за заплыв. Отдельной строкой под статистикой, а не
            // третьим столбцом: столбцы здесь про глубину, и монеты среди них
            // читались бы как ещё одна разновидность метров.
            _coinsEarnedText = NewText("Earned", content, "", 26, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_coinsEarnedText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -424f),
                   new Vector2(ScreenWidth - 20f, 40f));
            _coinsEarnedText.fontStyle = FontStyle.Bold;

            var retry = NewButton("RetryButton", content, Loc.Get("over.retry"), Palette.Aqua, Palette.Deep);
            Anchor(retry.rt, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(300f, 92f));
            retry.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); RetryClicked?.Invoke(); });

            var toMenu = NewButton("MenuButton", content, Loc.Get("over.menu"),
                                   Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(toMenu.rt, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(240f, 70f));
            toMenu.text.fontSize = 26;
            toMenu.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowStart(); });

            var rewarded = NewButton("RewardedButton", content, Loc.Get("over.rewarded"), Palette.Gold, Palette.Deep);
            Anchor(rewarded.rt, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(400f, 76f));
            rewarded.text.fontSize = 26;
            rewarded.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); RewardedClicked?.Invoke(); });
            _rewardedButton = rewarded.rt.gameObject;
            _rewardedButton.SetActive(false);

            _overScreen.SetActive(false);
        }

        Text BuildStat(RectTransform parent, float x, string label, out Text labelText)
        {
            var holder = NewRect("Stat_" + label, parent);
            Anchor(holder, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(240f, 120f));

            var value = NewText("Value", holder, "0", 52, Palette.Gold, TextAnchor.UpperCenter);
            Anchor(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(240f, 64f));
            value.fontStyle = FontStyle.Bold;

            labelText = NewText("Label", holder, label, 22, Palette.Foam.WithAlpha(0.7f), TextAnchor.UpperCenter);
            Anchor(labelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(240f, 32f));

            return value;
        }

        /// <summary>
        /// Кнопка паузы — рядом со звуком, в том же нижнем углу. Видна только в партии:
        /// в меню паузе неоткуда взяться, а лишняя круглая кнопка там путала бы.
        /// </summary>
        void BuildPauseButton()
        {
            var rt = NewRect("PauseButton", _safeArea);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-104f, 22f);   // левее кнопки звука
            rt.sizeDelta = new Vector2(72f, 72f);

            var background = rt.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.Disc();
            background.color = new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.55f);
            background.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => { ButtonClicked?.Invoke(); PauseClicked?.Invoke(); });

            // Значок — две полосы. Рисовать ради него отдельный спрайт незачем.
            for (int i = -1; i <= 1; i += 2)
            {
                var bar = NewRect("Bar" + i, rt);
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = new Vector2(i * 8f, 0f);
                bar.sizeDelta = new Vector2(9f, 30f);
                var img = bar.gameObject.AddComponent<Image>();
                img.sprite = SpriteFactory.RoundedRect("bar", 0.5f);
                img.type = Image.Type.Sliced;
                img.color = Palette.Aqua;
                img.raycastTarget = false;
            }

            _pauseButton = rt.gameObject;
            _pauseButton.SetActive(false);
        }

        /// <summary>
        /// Экран паузы: продолжить, магазин, выход в меню. Из паузы в магазин ходить
        /// разрешено — скин можно сменить, не бросая партию.
        /// </summary>
        void BuildPauseScreen()
        {
            _pauseScreen = BuildScreenRoot("PauseScreen");

            var content = NewRect("Content", _pauseScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 420f);

            var title = NewText("Title", content, Loc.Get("pause.title"), 56, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(ScreenWidth, 80f));
            title.fontStyle = FontStyle.Bold;

            var resume = NewButton("ResumeButton", content, Loc.Get("pause.resume"), Palette.Aqua, Palette.Deep);
            Anchor(resume.rt, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(360f, 96f));
            resume.text.fontSize = 34;
            resume.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ResumeClicked?.Invoke(); });

            // Магазина здесь нет намеренно: за скином игрок выходит в меню. Открывать
            // его поверх паузы значит держать открытыми два экрана сразу — на этом уже
            // один раз обожглись, они наложились друг на друга.
            var quit = NewButton("QuitButton", content, Loc.Get("pause.quit"),
                                 Palette.Foam.WithAlpha(0.85f), Palette.Deep);
            Anchor(quit.rt, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(260f, 70f));
            quit.text.fontSize = 26;
            quit.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); QuitToMenuClicked?.Invoke(); });

            _pauseScreen.SetActive(false);
        }

        /// <summary>
        /// Погасить все экраны разом. Каждый Show* начинается с этого и дальше
        /// включает только своё: перечислять «что ещё скрыть» в каждом методе —
        /// верный способ однажды забыть один и получить два экрана поверх друг друга.
        /// </summary>
        /// <summary>
        /// Экран закрывает игровой мир целиком. Стартовый не в счёт: там мир
        /// сквозь виньетку виден намеренно.
        /// </summary>
        static bool IsModalScreen(int screen) => screen != 0 && screen != 3;

        void SetScreen(int screen)
        {
            bool was = IsModalScreen(_screen);
            _screen = screen;
            bool now = IsModalScreen(screen);
            if (was != now) ModalChanged?.Invoke(now);
        }

        void HideAllScreens()
        {
            _startScreen.SetActive(false);
            _settingsScreen.SetActive(false);
            _shopScreen.SetActive(false);
            _rewardsScreen.SetActive(false);
            if (_tutorialScreen != null) _tutorialScreen.SetActive(false);
            _codexScreen.SetActive(false);
            _overScreen.SetActive(false);
            _pauseScreen.SetActive(false);
            _hud.SetActive(false);
            _bossBar.SetActive(false);
            _pauseButton.SetActive(false);
        }

        public void ShowPause()
        {
            SetScreen(4);
            HideAllScreens();
            _pauseScreen.SetActive(true);
        }

        void BuildMuteButton()
        {
            var rt = NewRect("MuteButton", _safeArea);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-22f, 22f);
            rt.sizeDelta = new Vector2(72f, 72f);

            var background = rt.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.Disc();
            background.color = new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.55f);
            background.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                MuteToggled?.Invoke();
            });

            var iconRect = NewRect("Icon", rt);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(40f, 40f);

            _muteIcon = iconRect.gameObject.AddComponent<Image>();
            _muteIcon.sprite = SpriteFactory.SpeakerIcon(false);
            _muteIcon.color = Palette.Aqua;
            _muteIcon.raycastTarget = false;
        }

        /// <summary>Обновить иконку под текущее состояние звука.</summary>
        public void SetMuteState(bool muted)
        {
            if (_muteIcon == null) return;
            _muteIcon.sprite = SpriteFactory.SpeakerIcon(muted);
            _muteIcon.color = muted ? Palette.Foam.WithAlpha(0.45f) : Palette.Aqua;
        }

        // ---------------------------------------------------------------- API

        public void ShowStart()
        {
            SetScreen(0);
            HideAllScreens();
            _startScreen.SetActive(true);
            HideBanner();
            RefreshMenu();
            MenuOpened?.Invoke();
        }

        public void ShowSettings()
        {
            SetScreen(1);
            HideAllScreens();
            _settingsScreen.SetActive(true);
            RefreshSettings();
        }

        public void ShowShop()
        {
            SetScreen(2);
            HideAllScreens();
            _shopScreen.SetActive(true);
            SetShopTab(_shopTab);
            ShopOpened?.Invoke();
        }

        /// <summary>
        /// Справочник открывается на той же странице, где его закрыли: при смене языка
        /// интерфейс пересобирается целиком, и выкидывать игрока из статьи в список
        /// было бы обиднее всего именно на длинном тексте.
        /// </summary>
        public void ShowCodex()
        {
            SetScreen(5);
            HideAllScreens();
            _codexScreen.SetActive(true);

            if (_codexPage >= 0) ShowCodexPage(_codexPage);
            else ShowCodexList();
        }

        /// <summary>
        /// Обработать системную кнопку «Назад»: закрыть текущий экран и вернуться
        /// на шаг выше. Возвращает false, если возвращаться уже некуда — тогда
        /// решать вызывающему (на Android это выход из приложения).
        ///
        /// Логика живёт здесь, а не в GameManager, потому что про иерархию экранов
        /// знает только интерфейс. Из статьи справочника, например, надо вернуться
        /// в список, а не сразу в меню.
        /// </summary>
        public bool TryGoBack()
        {
            switch (_screen)
            {
                case 1:                                   // настройки
                case 2:                                   // магазин
                case 6:                                   // награды
                    ShowStart();
                    return true;

                case 5:                                   // справочник
                    if (_codexPage >= 0) { ShowCodexList(); return true; }
                    ShowStart();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Рекорд нужен магазину: скины открываются глубиной.</summary>
        public void SetBestDepth(int best)
        {
            _bestDepth = best;
            RefreshMenu();
            if (_shopScreen != null && _shopScreen.activeSelf) SetShopTab(_shopTab);
        }

        void RefreshMenu()
        {
            if (_menuBestText == null) return;
            _menuBestText.text = _bestDepth > 0 ? Loc.Get("menu.record") + _bestDepth + Loc.Get("hud.meters") : "";
            if (_menuCoinText != null) _menuCoinText.text = Loc.Get("menu.coins") + Wallet.Balance;
            RefreshRewardsBadge();
        }

        /// <summary>Подписи в настройках держим синхронными с реальным состоянием звука.</summary>
        public void SetAudioState(bool muted, bool musicOn)
        {
            _lastMuted = muted;
            _lastMusicOn = musicOn;
            SetMuteState(muted);
            if (_soundLabel != null) _soundLabel.text = Loc.Get(muted ? "settings.soundOff" : "settings.soundOn");
            if (_musicLabel != null) _musicLabel.text = Loc.Get(musicOn ? "settings.musicOn" : "settings.musicOff");
        }

        Text _shopTabHint;
        Text _shopSortLabel;
        Text _shopCompareLabel;
        Text[] _shopTraits;
        RectTransform _shopCompareHolder;
        Image _shopCompareCore;
        Image _shopCompareShell;
        bool _shopComparing;
        /// <summary>Какой скин рекомендован в этой раскладке. −1 — ни один.</summary>
        int _shopRecommended = -1;

        Text _difficultyLabel;
        Text _calmLabel;
        Text _noFlashLabel;
        Text _vibrationLabel;
        Button _vibrationButton;

        /// <summary>
        /// Обновить подписи, которые зависят не от звука, а от выбора игрока.
        ///
        /// Отдельно от <see cref="SetAudioState"/>: сложность и вибрацию игра не
        /// присылает извне, их переключают прямо здесь, и дёргать ради них
        /// состояние звука было бы странно.
        /// </summary>
        void RefreshSettings()
        {
            if (_difficultyLabel != null)
                _difficultyLabel.text = Loc.Get("settings.difficulty")
                                        + Loc.Get(Difficulty.LevelKey(Difficulty.Level));

            if (_calmLabel != null)
                _calmLabel.text = Loc.Get(Visuals.LevelKey(Visuals.Level));

            if (_noFlashLabel != null)
                _noFlashLabel.text = Loc.Get(Visuals.NoFlashes ? "settings.noFlashOn" : "settings.noFlashOff");

            if (_vibrationLabel != null)
                _vibrationLabel.text = Loc.Get(Haptics.Enabled ? "settings.vibrationOn" : "settings.vibrationOff");

            // На устройстве без вибратора кнопка видна, но не нажимается: так
            // понятно, что настройка существует и что дело не в игре.
            if (_vibrationButton != null) _vibrationButton.interactable = Haptics.Supported;
        }

        // ------------------------------------------------------ логика магазина

        /// <summary>Пул точек и кольцо для показа эффектов в витрине.</summary>
        void BuildDemoLayers(RectTransform ball)
        {
            _demoDots = new Image[DemoDotCount];
            _demoDotAge = new float[DemoDotCount];
            _demoDotVelocity = new Vector2[DemoDotCount];

            var ringRect = NewRect("DemoRing", ball);
            ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(120f, 120f);
            _demoRing = ringRect.gameObject.AddComponent<Image>();
            _demoRing.sprite = SpriteFactory.Ring();
            _demoRing.raycastTarget = false;
            _demoRing.enabled = false;

            for (int i = 0; i < DemoDotCount; i++)
            {
                var dotRect = NewRect("DemoDot" + i, ball);
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(12f, 12f);
                var dot = dotRect.gameObject.AddComponent<Image>();
                dot.sprite = SpriteFactory.SoftDot();
                dot.raycastTarget = false;
                dot.enabled = false;
                _demoDots[i] = dot;
                _demoDotAge[i] = 99f;
            }
        }

        void SetShopTab(int tab)
        {
            _shopTab = Mathf.Clamp(tab, 0, 1);
            RefreshShop();
        }

        /// <summary>
        /// Сортировка и «купленные» в одной кнопке, по кругу.
        ///
        /// Шесть вкладок редкости и ещё два переключателя в ряд на холст шириной
        /// пятьсот единиц не помещаются, а экран и так был тесным. Одна кнопка,
        /// которая всегда показывает текущее состояние, честнее списка, в котором
        /// половина пунктов не нужна.
        /// </summary>
        void CycleShopTools()
        {
            _shopTools = (_shopTools + 1) % 5;
            RefreshShop();
        }

        int SortMode => _shopTools == 1 ? 1 : _shopTools == 2 ? 2 : 0;
        int OwnedMode => _shopTools == 3 ? 1 : _shopTools == 4 ? 2 : 0;

        string ToolsText()
        {
            switch (_shopTools)
            {
                case 1: return Loc.Get("shop.sortCheap");
                case 2: return Loc.Get("shop.sortRich");
                case 3: return Loc.Get("shop.ownedYes");
                case 4: return Loc.Get("shop.ownedNo");
                default: return Loc.Get("shop.sortDefault");
            }
        }

        /// <summary>Цвет редкости. Один на весь интерфейс: имя, рамка карточки, вкладка.</summary>
        static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return Palette.Hex("6BC8FF");
                case Rarity.Epic: return Palette.Hex("C48CFF");
                case Rarity.Legendary: return Palette.Hex("FFC83D");
                case Rarity.Boss: return Palette.Hex("FF6B5B");
                default: return Palette.Foam;
            }
        }

        static string RarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return Loc.Get("rarity.rare");
                case Rarity.Epic: return Loc.Get("rarity.epic");
                case Rarity.Legendary: return Loc.Get("rarity.legendary");
                case Rarity.Boss: return Loc.Get("rarity.boss");
                default: return Loc.Get("rarity.common");
            }
        }

        static string FilterName(int filter)
        {
            switch (filter)
            {
                case 1: return Loc.Get("rarity.common");
                case 2: return Loc.Get("rarity.rare");
                case 3: return Loc.Get("rarity.epic");
                case 4: return Loc.Get("rarity.legendary");
                case 5: return Loc.Get("rarity.boss");
                default: return Loc.Get("shop.filterAll");
            }
        }

        static Rarity TabRarity(int tab)
        {
            switch (tab)
            {
                case 2: return Rarity.Rare;
                case 3: return Rarity.Epic;
                case 4: return Rarity.Legendary;
                case 5: return Rarity.Boss;
                default: return Rarity.Common;
            }
        }

        /// <summary>Какие скины показывать и в каком порядке.</summary>
        void RebuildShopOrder(SkinDef[] skins)
        {
            _shopOrder.Clear();

            for (int i = 0; i < skins.Length; i++)
            {
                var skin = skins[i];

                if (_shopFilter == 5 && skin.Kind != UnlockKind.Boss) continue;
                if (_shopFilter > 0 && _shopFilter < 5 && (int)skin.Rarity != _shopFilter - 1) continue;

                bool have = SkinStore.IsUnlocked(skin, _bestDepth);
                if (OwnedMode == 1 && !have) continue;
                if (OwnedMode == 2 && have) continue;

                _shopOrder.Add(i);
            }

            if (SortMode == 0)
            {
                // Порядок по умолчанию — не порядок каталога, а порядок полезности:
                // сначала то, что можно купить прямо сейчас, потом то, на что
                // ближе всего копить, и только потом остальное. Каталог начинался
                // с давно купленных скинов, и первое, что видел игрок в магазине,
                // было тем, что у него уже есть.
                _shopOrder.Sort((a, b) =>
                {
                    int ra = ShopRank(skins[a], a);
                    int rb = ShopRank(skins[b], b);
                    if (ra != rb) return ra.CompareTo(rb);

                    int pa = skins[a].Price > 0 ? skins[a].Price : int.MaxValue;
                    int pb = skins[b].Price > 0 ? skins[b].Price : int.MaxValue;
                    if (pa != pb) return pa.CompareTo(pb);

                    return a.CompareTo(b);
                });

                // Рекомендуем первый покупаемый: самый дешёвый по карману, а если
                // не хватает ни на что — ближайший по цене.
                _shopRecommended = -1;
                for (int i = 0; i < _shopOrder.Count; i++)
                {
                    int index = _shopOrder[i];
                    if (!CanBuyNow(skins[index], index)) continue;
                    _shopRecommended = index;
                    break;
                }
                return;
            }

            _shopRecommended = -1;

            // Бесплатные уходят в конец: сортировка нужна, чтобы выбрать, на что
            // копить, и ноль в начале списка этому только мешает.
            int direction = SortMode == 1 ? 1 : -1;
            _shopOrder.Sort((a, b) =>
            {
                int pa = skins[a].Price > 0 ? skins[a].Price : int.MaxValue;
                int pb = skins[b].Price > 0 ? skins[b].Price : int.MaxValue;
                if (pa == pb) return a.CompareTo(b);
                if (pa == int.MaxValue) return 1;
                if (pb == int.MaxValue) return -1;
                return direction * pa.CompareTo(pb);
            });
        }

        /// <summary>
        /// Место скина в порядке по умолчанию. Меньше — выше в списке.
        ///
        /// 0 — можно купить сейчас и хватает монет;
        /// 1 — можно купить, но не хватает;
        /// 2 — открыто (уже наше);
        /// 3 — закрыто условием, а не ценой: за глубину, за босса, за коллекцию.
        /// </summary>
        int ShopRank(SkinDef skin, int index)
        {
            if (CanBuyNow(skin, index)) return Wallet.CanAfford(skin.Price) ? 0 : 1;
            return SkinStore.IsUnlocked(skin, _bestDepth) ? 2 : 3;
        }

        /// <summary>Перерисовать магазин целиком: вкладки, список, превью.</summary>
        void RefreshShop()
        {
            _bubbleTab.color = _shopTab == 0 ? Palette.Aqua : Palette.Deep2;
            _guardianTab.color = _shopTab == 1 ? Palette.Aqua : Palette.Deep2;

            var skins = _shopTab == 0 ? SkinStore.Bubble : SkinStore.Guardian;
            int selected = _shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian;

            RebuildShopOrder(skins);

            for (int i = 0; i < _shopTabButtons.Length; i++)
            {
                _shopTabLabels[i].text = FilterName(i);
                bool active = _shopFilter == i;
                _shopTabButtons[i].color = active ? RarityColor(TabRarity(i)) : Palette.Deep2;
                _shopTabLabels[i].color = active ? Palette.Deep : Palette.Foam.WithAlpha(0.8f);
            }

            if (_shopSortLabel != null) _shopSortLabel.text = ToolsText();

            // В строке у кнопки «Фильтры» — только то, что реально включено.
            // Писать «по порядку, все, все» значило бы занимать место сообщением
            // «ничего не выбрано».
            bool filtered = _shopFilter != 0 || _shopTools != 0;
            _shopToolsLabel.text = filtered ? FilterName(_shopFilter) + "  ·  " + ToolsText() : "";

            _shopTabHint.text = Loc.Get(_shopTab == 0 ? "shop.hintBubble" : "shop.hintGuardian");
            _shopPurse.text = Wallet.Balance.ToString();
            LayoutCoinRow(_shopPurseIcon, _shopPurse);

            RefreshPreview(skins[selected], selected);

            // Карточки раскладываем по порядку, лишние прячем. Высота содержимого
            // считается по числу рядов — от неё зависит, докуда доезжает прокрутка.
            int rows = Mathf.CeilToInt(_shopOrder.Count / (float)ShopLayout.Columns);
            _shopListContent.sizeDelta = new Vector2(0f,
                Mathf.Max(ShopLayout.ListH,
                          rows * ShopLayout.CardStepY + ShopLayout.ListBottomPad));

            for (int i = 0; i < _shopTiles.Length; i++)
            {
                var tile = _shopTiles[i];
                bool used = i < _shopOrder.Count;
                tile.Card.gameObject.SetActive(used);
                if (!used) continue;

                int col = i % ShopLayout.Columns;
                int row = i / ShopLayout.Columns;
                tile.Card.anchoredPosition = new Vector2(
                    (col - (ShopLayout.Columns - 1) * 0.5f) * ShopLayout.CardStepX,
                    -6f - row * ShopLayout.CardStepY);

                FillCard(tile, skins[_shopOrder[i]], _shopOrder[i], _shopOrder[i] == selected);
            }
        }

        /// <summary>Имя в одну строку: длинное обрезается многоточием, а не переносится.</summary>
        static string Clip(string text, int limit)
            => string.IsNullOrEmpty(text) || text.Length <= limit ? text : text.Substring(0, limit - 1) + "…";

        /// <summary>Одна карточка: шарик, имя, редкость и одно из двух — цена или статус.</summary>
        void FillCard(ShopTile tile, SkinDef skin, int index, bool chosen)
        {
            bool unlocked = SkinStore.IsUnlocked(skin, _bestDepth);

            // Награду за коллекцию до открытия не показываем совсем: весь смысл в
            // том, чтобы игрок не знал заранее, что именно там лежит.
            bool hidden = !unlocked && skin.Kind == UnlockKind.Mastery;

            var look = SkinVisuals.Resolve(skin, _shopTab == 1, index);
            Color shade = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.3f);

            tile.Icon.sprite = look.Shell;
            tile.Icon.enabled = !hidden;
            tile.Icon.color = look.ShellColor * shade;

            tile.Core.sprite = look.Core;
            tile.Core.enabled = look.Core != null && !hidden;
            tile.Core.color = look.Tint * shade;

            tile.Name.text = hidden ? "?" : Clip(skin.Name, 14);
            tile.Name.color = unlocked ? Palette.Foam : Palette.Foam.WithAlpha(0.65f);

            tile.Rarity.text = hidden ? "" : RarityName(skin.Rarity);
            tile.Rarity.color = RarityColor(skin.Rarity).WithAlpha(unlocked ? 1f : 0.7f);

            if (chosen)
            {
                tile.Lock.text = Loc.Get("shop.chosen");
                tile.Lock.color = Palette.Aqua;
                tile.PriceIcon.enabled = false;
            }
            else if (unlocked)
            {
                tile.Lock.text = Loc.Get("shop.owned");
                tile.Lock.color = Palette.Foam.WithAlpha(0.8f);
                tile.PriceIcon.enabled = false;
            }
            else if (skin.Price > 0 && CanBuyNow(skin, index))
            {
                // Цену видно только там, где её можно заплатить: у трофея
                // непобеждённого босса ценник бессмыслен, сначала победа.
                tile.Lock.text = skin.Price.ToString();
                tile.Lock.color = Wallet.CanAfford(skin.Price) ? Palette.Gold : Palette.Hex("FF8A7A");
                tile.PriceIcon.enabled = true;
            }
            else
            {
                tile.Lock.text = Loc.Get("shop.locked");
                tile.Lock.color = Palette.Foam.WithAlpha(0.55f);
                tile.PriceIcon.enabled = false;
            }

            // Строку пересобираем после текста: ширина «КУПЛЕНО» и «10000» разная,
            // и центрировать пару можно только зная её.
            if (tile.PriceIconRect != null)
            {
                tile.PriceIconRect.gameObject.SetActive(tile.PriceIcon.enabled);
                LayoutCoinRow(tile.PriceIconRect, tile.Lock);
                if (!tile.PriceIcon.enabled)
                {
                    // Без монеты центрируем один текст, иначе он уезжает вправо.
                    var lt = tile.Lock.rectTransform;
                    lt.pivot = new Vector2(0.5f, 0.5f);
                    lt.anchoredPosition = Vector2.zero;
                    tile.Lock.alignment = TextAnchor.MiddleCenter;
                }
            }

            tile.Background.color = chosen ? Palette.Aqua.WithAlpha(0.55f)
                                 : unlocked ? Color.Lerp(Palette.Deep2, RarityColor(skin.Rarity), 0.28f)
                                 : Palette.Deep2 * new Color(1f, 1f, 1f, 0.6f);

            // Подсказка: рекомендация — только одной карточке, нехватка монет —
            // любой покупаемой. Третьего случая нет, поэтому строка одна.
            if (tile.Note == null) return;

            if (index == _shopRecommended && !chosen)
            {
                tile.Note.text = Loc.Get("shop.recommend");
                tile.Note.color = Palette.Aqua;
            }
            else if (!hidden && !unlocked && skin.Price > 0 && CanBuyNow(skin, index)
                     && !Wallet.CanAfford(skin.Price))
            {
                tile.Note.text = Loc.Get("shop.short") + (skin.Price - Wallet.Balance);
                tile.Note.color = Palette.Hex("FF8A7A");
            }
            else
            {
                tile.Note.text = "";
            }
        }

        /// <summary>Можно ли купить прямо сейчас: для трофея нужна ещё и победа.</summary>
        static bool CanBuyNow(SkinDef skin, int index)
        {
            if (skin.Kind == UnlockKind.Purchase) return !SkinStore.IsOwned(index);
            if (skin.Kind == UnlockKind.Boss)
                return SkinStore.IsBossDefeated(skin.UnlockBoss) && !SkinStore.IsOwned(index);
            return false;
        }

        void RefreshPreview(SkinDef skin, int index)
        {
            // Вид собирает SkinVisuals — та же функция, которой пользуется игра.
            // Раньше витрина складывала слои сама, и пути разошлись: она брала
            // материал показанного скина, а игра выдавала обеим половинкам
            // материал пузырька.
            var look = SkinVisuals.Resolve(skin, _shopTab == 1, index);
            _previewLook = look;

            // Оболочка — часть паспорта скина, и берётся она из той же таблицы,
            // что в бою. Показ прежнего скина при этом гасим: досматривать чужую
            // оболочку на новом шарике незачем.
            _hasPreviewEnv = EnvelopeTable.TryGet(skin, out _previewEnvDef);
            StopShopEnvelopes();

            _shopPreview.sprite = look.Shell;
            _shopPreview.color = look.ShellColor;

            _shopPreviewCore.sprite = look.Core;
            _shopPreviewCore.enabled = look.Core != null;
            _shopPreviewCore.color = look.Tint;

            RefreshTraits(skin);
            RefreshCompare();

            _shopSkinName.text = Clip(skin.Name.ToUpperInvariant(), 16);
            _shopRarityLabel.text = RarityName(skin.Rarity);
            _shopRarityLabel.color = RarityColor(skin.Rarity);
            _shopSkinName.color = RarityColor(skin.Rarity);

            bool unlocked = SkinStore.IsUnlocked(skin, _bestDepth);
            bool chosen = index == (_shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian);

            if (chosen)
            {
                _shopStatus.text = "";
                _shopActionLabel.text = Loc.Get("shop.chosen");
                _shopActionButton.color = Palette.Deep2;
            }
            else if (unlocked)
            {
                _shopStatus.text = "";
                _shopActionLabel.text = Loc.Get("shop.select");
                _shopActionButton.color = Palette.Aqua;
            }
            else if (CanBuyNow(skin, index) && Wallet.CanAfford(skin.Price))
            {
                _shopStatus.text = "";
                _shopActionLabel.text = Loc.Get("shop.buyFor") + skin.Price;
                _shopActionButton.color = Palette.Gold;
            }
            else
            {
                _shopStatus.text = SkinStore.UnlockText(skin);
                _shopActionLabel.text = Loc.Get("shop.locked");
                _shopActionButton.color = Palette.Deep2 * new Color(1f, 1f, 1f, 0.7f);
            }

            _shopDemoLabel.text = Loc.Get("shop.demo");
        }

        /// <summary>Главная кнопка панели: делает с выбранным скином то, что написано.</summary>
        /// <summary>
        /// Три метки выбранного скина: удар, след, звук.
        ///
        /// Берутся из стихии, а не пишутся руками у каждого скина: стихия выводится
        /// из материала, и новый скин получает подписи сам. Руками их пришлось бы
        /// заполнять в двадцати с лишним местах, и первое же расхождение между
        /// подписью и тем, что слышно, было бы хуже отсутствия подписи.
        /// </summary>
        void RefreshTraits(SkinDef skin)
        {
            if (_shopTraits == null) return;

            var arch = ArchetypeTable.Of(skin);
            string element = Loc.Get(arch.NameKey);

            // Метки зависят от половины, и это не придирка. Панель писала «Удар:
            // ТЬМОЙ» и для пузырька тоже, хотя по препятствиям бьёт страж и удар
            // всегда был его. На вкладке «Шарик» обещание и вовсе расходилось с
            // делом: игра брала стихию пузырька.
            if (_shopTab == 0)
            {
                // Пузырёк: его видно весь заплыв. Он решает, как выглядит сам и
                // как рассыпается то, что разбили.
                _shopTraits[0].text = Loc.Get("fx.element") + element;
                _shopTraits[1].text = Loc.Get("fx.trail") + Loc.Get(arch.TrailKey);
                _shopTraits[2].text = Loc.Get("fx.destroy") + element;
            }
            else
            {
                // Страж: он бьёт, значит его стихия и остаётся на препятствии.
                _shopTraits[0].text = Loc.Get("fx.impact") + element;
                _shopTraits[1].text = Loc.Get("fx.trail") + Loc.Get(arch.TrailKey);
                _shopTraits[2].text = Loc.Get("fx.sound") + Loc.Get(arch.SoundKey);
            }

            // Цвет меток — цвет стихии: ещё один канал, по которому скины
            // различаются, и он работает даже без чтения.
            Color tone = Color.Lerp(Palette.Foam, arch.Tone, 0.55f);
            for (int i = 0; i < _shopTraits.Length; i++) _shopTraits[i].color = tone;
        }

        void ToggleCompare()
        {
            _shopComparing = !_shopComparing;
            RefreshCompare();

            // Сравнение без эффекта — это два неподвижных шарика. Открыли —
            // сразу показываем обе оболочки, иначе игрок не узнает, что разница
            // между ними есть.
            if (_shopComparing) PlayShopEnvelope();
            else StopShopEnvelopes();
        }

        /// <summary>Показать рядом то, что надето сейчас.</summary>
        void RefreshCompare()
        {
            if (_shopCompareHolder == null) return;

            _shopCompareHolder.gameObject.SetActive(_shopComparing);
            if (_shopCompareLabel != null)
                _shopCompareLabel.text = Loc.Get(_shopComparing ? "fx.close" : "fx.compare");
            if (!_shopComparing) return;

            // Сравниваем с надетым в той же половине пары: сопоставлять пузырёк со
            // стражем бессмысленно, это разные вещи.
            var skins = _shopTab == 0 ? SkinStore.Bubble : SkinStore.Guardian;
            int worn = _shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian;
            var skin = skins[worn];

            var look = SkinVisuals.At(_shopTab == 1, worn);
            _shopCompareShell.sprite = look.Shell;
            _shopCompareShell.color = look.ShellColor;
            _shopCompareCore.sprite = look.Core;
            _shopCompareCore.enabled = look.Core != null;
            _shopCompareCore.color = look.Tint;

            _hasCompareEnv = EnvelopeTable.TryGet(skin, out _compareEnvDef);
        }

        void ActOnSelected()
        {
            int index = _shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian;
            TrySelectSkin(-1, index);
        }

        /// <summary>
        /// Оболочка карточки — та же функция, что и в игре. Витрина обязана брать
        /// ровно то же, иначе однажды покажет не то, что игрок получит.
        /// </summary>
        /// <summary>Вид, который сейчас показан в витрине. Им живут движение и след.</summary>
        SkinLook _previewLook;


        /// <summary>
        /// Нажатие по карточке: закрытый объясняет условие, продающийся покупается,
        /// открытый выбирается.
        /// </summary>
        void TrySelectSkin(int slotIndex, int direct = -1)
        {
            var skins = _shopTab == 0 ? SkinStore.Bubble : SkinStore.Guardian;

            int index = direct >= 0 ? direct
                      : slotIndex >= 0 && slotIndex < _shopOrder.Count ? _shopOrder[slotIndex]
                      : -1;
            if (index < 0 || index >= skins.Length) return;

            var skin = skins[index];

            // Платить нужно только за то, что и правда закрыто: IsUnlocked в
            // дев-сборке отвечает «открыто» на всё подряд.
            bool needsPurchase = CanBuyNow(skin, index) && !SkinStore.IsUnlocked(skin, _bestDepth);

            if (needsPurchase)
            {
                if (!Wallet.CanAfford(skin.Price))
                {
                    _shopStatus.text = SkinStore.UnlockText(skin);
                    RefreshShop();
                    return;
                }

                if (!SkinStore.Buy(index)) return;
                SkinPurchased?.Invoke();
            }
            else if (!SkinStore.IsUnlocked(skin, _bestDepth))
            {
                _shopStatus.text = SkinStore.UnlockText(skin);
                RefreshShop();
                return;
            }

            if (_shopTab == 0) SkinStore.SelectedBubble = index;
            else SkinStore.SelectedGuardian = index;

            SkinChosen?.Invoke(_shopTab, index);
            RefreshShop();
        }

        public void ShowHud()
        {
            // Экран 3 немодальный: только эта смена шлёт ModalChanged(false), и
            // пузырёк со стражем снова видны. Без неё после паузы (экран 4) они
            // оставались спрятанными до конца заплыва и во всех «Ещё раз».
            SetScreen(3);
            HideAllScreens();
            _hud.SetActive(true);
            _pauseButton.SetActive(true);
        }

        public void ShowGameOver(int depth, int best, bool offerRewarded)
        {
            HideAllScreens();
            _overScreen.SetActive(true);
            _finalDepthText.text = depth.ToString();
            _bestDepthText.text = best.ToString();
            _rewardedButton.SetActive(offerRewarded);

            // Пустую строку не показываем: заплыв, в котором не разбито ни одного
            // препятствия, — это нормально, и «+0 монет» выглядело бы упрёком.
            _coinsEarnedText.text = Wallet.EarnedThisRun > 0
                ? Loc.Get("over.earned") + Wallet.EarnedThisRun
                : "";

            HideBanner();
        }

        public void HideRewardedButton() => _rewardedButton.SetActive(false);

        public void SetDepth(int meters) => _depthText.text = meters + Loc.Get("hud.meters");

        // ------------------------------------------------------------- монеты

        /// <summary>
        /// Пул всплывающих «+N». Восемь штук с запасом: мина сносит разом несколько
        /// препятствий, но девятая надпись за ту же долю секунды всё равно слилась бы
        /// с остальными в нечитаемую кашу.
        /// </summary>
        void BuildCoinPops()
        {
            _coinPops = new Text[CoinPopCount];
            _coinPopAge = new float[CoinPopCount];
            _coinPopFrom = new Vector2[CoinPopCount];

            for (int i = 0; i < CoinPopCount; i++)
            {
                var label = NewText("CoinPop" + i, _hud.GetComponent<RectTransform>(), "", 26,
                                    Palette.Gold, TextAnchor.MiddleCenter);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(120f, 40f);
                label.fontStyle = FontStyle.Bold;
                label.gameObject.SetActive(false);

                _coinPops[i] = label;
                _coinPopAge[i] = 99f;
            }
        }

        /// <summary>
        /// Показать «+N» над местом разрушения. Точка приходит мировая — переводим её
        /// в координаты холста сами, потому что холст экранный и ничего о сцене не знает.
        /// </summary>
        public void ShowCoinGain(Vector2 worldPosition, int amount)
        {
            if (_coinPops == null || amount <= 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = cam.WorldToScreenPoint(worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screen, null, out Vector2 local))
                return;

            int i = _coinPopCursor;
            _coinPopCursor = (_coinPopCursor + 1) % CoinPopCount;

            _coinPops[i].text = "+" + amount;
            _coinPops[i].gameObject.SetActive(true);
            _coinPopAge[i] = 0f;
            _coinPopFrom[i] = local;
            _coinPops[i].rectTransform.anchoredPosition = local;
        }

        /// <summary>
        /// Анимация надписей и догоняющий счётчик.
        ///
        /// unscaledDeltaTime намеренно: на паузе и на экране итогов игровое время
        /// стоит, а монеты в этот момент ещё долетают — с обычным deltaTime они
        /// зависли бы в воздухе до следующего заплыва.
        /// </summary>
        /// <summary>
        /// Проявление экранов. В LateUpdate, а не в Update: экран включают кнопки
        /// и игра в своих Update, и проверка до них пропустила бы первый кадр —
        /// он ушёл бы на экран целиком непрозрачным.
        /// </summary>
        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _fadeRoots[0] = _startScreen;
            _fadeRoots[1] = _settingsScreen;
            _fadeRoots[2] = _shopScreen;
            _fadeRoots[3] = _rewardsScreen;
            _fadeRoots[4] = _tutorialScreen;
            _fadeRoots[5] = _codexScreen;
            _fadeRoots[6] = _overScreen;
            _fadeRoots[7] = _pauseScreen;
            _fadeRoots[8] = _hud;
            _fadeRoots[9] = _bossBar;

            // Закрылся ли в этом кадре другой экран: тогда новый ставим сразу.
            // HUD и полоса босса (8 и 9) не в счёт: пауза и итоги прячут HUD,
            // но появляются поверх игры и должны проявляться.
            bool swapped = false;
            for (int i = 0; i < 8; i++)
                if (_fadeRoots[i] != null && _fadeWas[i] && !_fadeRoots[i].activeSelf) swapped = true;

            for (int i = 0; i < _fadeRoots.Length; i++)
            {
                var go = _fadeRoots[i];
                if (go == null) { _fadeWas[i] = false; continue; }

                bool on = go.activeSelf;
                if (on && !_fadeWas[i])
                {
                    if (_fadeGroups[i] == null || _fadeGroups[i].gameObject != go)
                    {
                        _fadeGroups[i] = go.GetComponent<CanvasGroup>();
                        if (_fadeGroups[i] == null) _fadeGroups[i] = go.AddComponent<CanvasGroup>();
                    }
                    _fadeGroups[i].alpha = swapped ? 1f : 0f;
                }
                else if (on && _fadeGroups[i] != null && _fadeGroups[i].alpha < 1f)
                {
                    _fadeGroups[i].alpha = Mathf.Min(1f, _fadeGroups[i].alpha + dt / ScreenFadeIn);
                }
                _fadeWas[i] = on;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            TickRewards(dt);
            TickLives(dt);

            if (_coinPops != null)
            {
                for (int i = 0; i < _coinPops.Length; i++)
                {
                    if (_coinPopAge[i] > 1f) continue;

                    _coinPopAge[i] += dt * 1.4f;
                    float k = Mathf.Clamp01(_coinPopAge[i]);

                    _coinPops[i].rectTransform.anchoredPosition = _coinPopFrom[i] + Vector2.up * (k * 54f);
                    _coinPops[i].color = Palette.Gold.WithAlpha(1f - k * k);

                    if (k >= 1f) _coinPops[i].gameObject.SetActive(false);
                }
            }

            // Живое превью: шарик дышит и крутит начинку ровно так же, как в воде.
            //
            // Раньше анимация висела на оболочке, а начинка стояла на месте — слои
            // разъезжались, и витрина показывала не то, что игрок получит. Теперь
            // дышит держатель (а значит оба слоя), крутится только начинка, и цвет
            // накладывается одним слоем, а не двумя.
            if (_shopScreen != null && _shopScreen.activeSelf && _shopPreviewHolder != null)
            {
                var skins = _shopTab == 0 ? SkinStore.Bubble : SkinStore.Guardian;
                int selected = _shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian;

                _previewPhase += dt;

                // Множитель размера от стихии применяется и здесь. В игре он был,
                // в витрине нет — золотой скин в бою оказывался чуть крупнее, чем
                // его показывали.
                Vector2 s = SkinFxRuntime.MotionScale(_previewLook.Fx, _previewPhase)
                            * _previewLook.VisualScale;
                _shopPreviewHolder.localScale = new Vector3(s.x, s.y, 1f);

                if (_shopPreviewCore.enabled)
                    _shopPreviewCore.rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, _previewPhase * SkinVisuals.CoreSpin);

                TickPreviewAura();
                TickPreviewTrail(dt);
                TickShopDemo(dt, skins[selected]);
                TickShopEnvelopes(dt);
                TickBigDemo(dt);
            }

            if (_coinText != null)
            {
                float target = Wallet.Balance;
                _coinShown = Mathf.Abs(target - _coinShown) < 1f
                    ? target
                    : Mathf.Lerp(_coinShown, target, 1f - Mathf.Exp(-9f * dt));

                string shown = Mathf.RoundToInt(_coinShown).ToString();
                if (_coinText.text != shown)
                {
                    _coinText.text = shown;
                    LayoutCoinRow(_coinIcon, _coinText);
                }
            }
        }


        /// <summary>
        /// Показ удара и разрушения прямо в витрине.
        ///
        /// Это упрощение, и сознательное: холст экранный, игровая система частиц
        /// живёт в мире, и тащить её сюда ради демонстрации значило бы держать два
        /// рендера одного и того же. Поэтому здесь свои точки — но цвета, направление
        /// и характер берутся из той же таблицы материала, что и в игре, так что
        /// игрок видит именно свой эффект, а не абстрактный салют.
        ///
        /// Две фазы подряд, как в бою: сначала удар, потом разрушение.
        /// </summary>
        /// <summary>
        /// Мишень для показа: обычный камень справа от шарика.
        ///
        /// Нужна не для красоты. Удар и разрушение — это то, что происходит
        /// С ПРЕПЯТСТВИЕМ, и показывать их в пустоте значит показывать салют.
        /// С мишенью видно всю цепочку: страж коснулся, на камне остался статус
        /// стихии, камень развалился.
        /// </summary>
        void BuildDemoTarget(RectTransform ball)
        {
            var rt = NewRect("DemoTarget", ball);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(62f, 0f);
            rt.sizeDelta = new Vector2(44f, 44f);

            _demoTarget = rt.gameObject.AddComponent<Image>();
            _demoTarget.sprite = SpriteFactory.Rock();
            _demoTarget.preserveAspect = true;
            _demoTarget.raycastTarget = false;
            _demoTarget.enabled = false;
        }

        Image _demoTarget;

        /// <summary>
        /// Мишень по фазам показа: появляется, принимает цвет стихии в момент
        /// удара, держит его, пока идёт статус, и рассыпается в конце.
        /// </summary>
        void TickDemoTarget(float dt, MaterialFxDef m)
        {
            if (_demoTarget == null) return;

            if (_demoTimer <= 0f) { _demoTarget.enabled = false; return; }

            _demoTarget.enabled = true;

            if (_demoTimer < 0.42f)
            {
                // Удар: короткая вспышка цветом стихии и отдача вправо.
                float k = Mathf.Clamp01(_demoTimer / 0.42f);
                _demoTarget.color = Color.Lerp(m.Hot, Color.white, k * 0.6f);
                _demoTarget.rectTransform.anchoredPosition = new Vector2(62f + (1f - k) * 7f, 0f);
                _demoTarget.rectTransform.localScale = Vector3.one * (1f + (1f - k) * 0.18f);
            }
            else if (_demoTimer < 0.95f)
            {
                // Статус: камень окрашен стихией и подрагивает.
                _demoTarget.color = Color.Lerp(Color.white, m.Cool, 0.65f);
                _demoTarget.rectTransform.anchoredPosition =
                    new Vector2(62f + Mathf.Sin(_demoTimer * 42f) * 1.2f, 0f);
                _demoTarget.rectTransform.localScale = Vector3.one;
            }
            else
            {
                // Разрушение: сжимается и гаснет.
                float k = Mathf.Clamp01((_demoTimer - 0.95f) / 0.35f);
                _demoTarget.color = Color.Lerp(m.Cool, Color.clear, k);
                _demoTarget.rectTransform.localScale = Vector3.one * (1f - k * 0.7f);
                if (k >= 1f) _demoTarget.enabled = false;
            }
        }

        /// <summary>
        /// Аура в витрине — та же формула, что в игре.
        ///
        /// Берётся из <see cref="SkinFxRuntime.AuraIntensity"/>, а не считается
        /// заново: вторая копия формулы разошлась бы с первой при первой же
        /// правке яркости.
        /// </summary>
        void TickPreviewAura()
        {
            if (_shopPreviewAura == null) return;

            Color aura = _previewLook.Material.Aura;
            float intensity = SkinFxRuntime.AuraIntensity(_previewLook, _previewPhase);

            bool on = intensity > 0.01f;
            _shopPreviewAura.enabled = on;
            if (!on) return;

            _shopPreviewAura.color = new Color(aura.r, aura.g, aura.b, Mathf.Clamp01(intensity));

            // Радиус — тот же закон, что у BubbleController.SetAura.
            float scale = 1.1f + intensity * 0.25f;
            _shopPreviewAura.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// След в витрине.
        ///
        /// Параметры частицы берутся из <see cref="SkinFxRuntime.SampleTrail"/> —
        /// того же описания, по которому след рисуется в бою. Отличается только
        /// пересчёт мировых единиц в точки холста: радиус шарика в витрине 56
        /// точек против 0.22 юнита в игре.
        ///
        /// Рисуем тем же пулом точек, что и показ эффектов: заводить второй ради
        /// следа значило бы держать на экране два пула по тридцать объектов.
        /// </summary>
        void TickPreviewTrail(float dt)
        {
            if (_demoDots == null || _shopPreviewHolder == null) return;

            // Плотность вдвое ниже игровой: витрина маленькая, и полная плотность
            // забивает её частицами до нечитаемости.
            _previewTrailEmit += dt * _previewLook.Fx.Rate * 0.5f;

            // Сколько точек холста приходится на юнит мира. Радиус шарика в
            // витрине — половина высоты держателя.
            float unit = _shopPreviewHolder.sizeDelta.y * 0.5f;

            while (_previewTrailEmit >= 1f)
            {
                _previewTrailEmit -= 1f;

                var t = SkinFxRuntime.SampleTrail(_previewLook, _previewPhase);
                int slot = NextDemoDot();
                if (slot < 0) break;

                var dot = _demoDots[slot];
                dot.enabled = true;
                dot.color = t.Color.WithAlpha(t.Alpha);
                dot.rectTransform.sizeDelta = Vector2.one * Mathf.Max(3f, t.Size * unit * 4f);
                dot.rectTransform.anchoredPosition = t.Offset * unit;

                _demoDotAge[slot] = 0f;
                _demoDotVelocity[slot] = t.Velocity * unit * 0.45f;
            }
        }

        /// <summary>Свободная точка в пуле витрины. −1, если все заняты.</summary>
        int NextDemoDot()
        {
            for (int i = 0; i < _demoDots.Length; i++)
                if (_demoDotAge[i] > 1f) return i;
            return -1;
        }

        void TickShopDemo(float dt, SkinDef skin)
        {
            var m = skin.Kind == UnlockKind.Boss
                ? MaterialFxTable.OfBoss(skin.UnlockBoss)
                : MaterialFxTable.Of(skin.Material);

            if (_demoTimer > 0f)
            {
                float before = _demoTimer;
                _demoTimer += dt;

                // Три фазы подряд, ровно как в бою: удар, статус на препятствии,
                // разрушение. Без середины витрина обещала бы не то, что произойдёт.
                if (before < 0.02f) SpawnDemoBurst(m.Hot, m.Cool, impact: true);
                if (before < 0.42f && _demoTimer >= 0.42f) SpawnDemoStatus(m);
                if (before < 0.95f && _demoTimer >= 0.95f) SpawnDemoBurst(m.Hot, m.Cool, impact: false);
                if (_demoTimer > 1.7f) _demoTimer = 0f;
            }

            TickDemoTarget(dt, m);

            // Кольцо живёт вместе с показом и гаснет само.
            if (_demoRing.enabled)
            {
                float k = Mathf.Clamp01(_demoRingAge);
                _demoRingAge += dt * 2.6f;
                _demoRing.rectTransform.localScale = Vector3.one * (0.3f + k * 1.4f);
                _demoRing.color = _demoRingColor.WithAlpha(1f - k);
                if (_demoRingAge >= 1f) _demoRing.enabled = false;
            }

            for (int i = 0; i < _demoDots.Length; i++)
            {
                if (_demoDotAge[i] > 1f) continue;

                _demoDotAge[i] += dt * 1.8f;
                var rt = _demoDots[i].rectTransform;
                rt.anchoredPosition += _demoDotVelocity[i] * dt;

                // Вязкость воды: скорость гаснет, и остаток пути частица проплывает.
                _demoDotVelocity[i] *= Mathf.Pow(0.08f, dt);
                _demoDotVelocity[i] += Vector2.up * (18f * dt);   // лёгкое всплытие

                var c = _demoDots[i].color;
                c.a = 1f - _demoDotAge[i];
                _demoDots[i].color = c;

                if (_demoDotAge[i] >= 1f) _demoDots[i].enabled = false;
            }
        }

        float _demoRingAge = 2f;
        Color _demoRingColor = Color.white;

        /// <summary>
        /// Средняя фаза показа: то, что остаётся на препятствии после удара.
        /// Точки медленно всплывают — так ведёт себя и горящее, и леденеющее.
        /// </summary>
        void SpawnDemoStatus(MaterialFxDef m)
        {
            _demoRing.enabled = true;
            _demoRingAge = 0f;
            _demoRingColor = m.Cool;

            for (int i = 0; i < 6; i++)
            {
                int slot = -1;
                for (int j = 0; j < _demoDots.Length; j++)
                    if (_demoDotAge[j] > 1f) { slot = j; break; }
                if (slot < 0) return;

                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                _demoDots[slot].enabled = true;
                _demoDots[slot].color = i % 2 == 0 ? m.Hot : m.Cool;
                _demoDots[slot].rectTransform.anchoredPosition = dir * 34f;
                _demoDots[slot].rectTransform.localScale = Vector3.one * 0.8f;
                _demoDotVelocity[slot] = new Vector2(UnityEngine.Random.Range(-18f, 18f), 46f);
                _demoDotAge[slot] = 0f;
            }
        }

        /// <summary>Порция точек: удар летит в одну сторону, разрушение — во все.</summary>
        void SpawnDemoBurst(Color hot, Color cool, bool impact)
        {
            _demoRing.enabled = true;
            _demoRingAge = 0f;
            _demoRingColor = impact ? hot : cool;

            int count = impact ? 6 : DemoDotCount;
            for (int i = 0; i < count; i++)
            {
                int slot = -1;
                for (int j = 0; j < _demoDots.Length; j++)
                    if (_demoDotAge[j] > 1f) { slot = j; break; }
                if (slot < 0) return;

                float angle = impact
                    ? Mathf.PI * 0.25f + UnityEngine.Random.Range(-0.45f, 0.45f)   // удар приходит сбоку
                    : i / (float)count * Mathf.PI * 2f;

                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                _demoDots[slot].enabled = true;
                _demoDots[slot].color = i % 2 == 0 ? hot : cool;
                _demoDots[slot].rectTransform.anchoredPosition = dir * (impact ? 46f : 10f);
                _demoDots[slot].rectTransform.localScale = Vector3.one * UnityEngine.Random.Range(0.6f, 1.1f);
                _demoDotVelocity[slot] = dir * UnityEngine.Random.Range(impact ? 90f : 140f, impact ? 170f : 240f);
                _demoDotAge[slot] = 0f;
            }
        }

        /// <summary>Поставить счётчик ровно: перед заплывом догонять нечего.</summary>
        public void SyncCoins()
        {
            _coinShown = Wallet.Balance;
            if (_coinText != null) _coinText.text = Mathf.RoundToInt(_coinShown).ToString();
            if (_menuCoinText != null) _menuCoinText.text = Wallet.Balance.ToString();
        }

        /// <summary>
        /// Значки жизней. Потерянная гаснет за LifeFade, а не за кадр (A-08):
        /// через четверть секунды после удара на кадре был виден только уже
        /// погасший значок, и сам момент потери не читался.
        /// </summary>
        public void SetLives(int lives)
        {
            if (_lifeWant == null || _lifeWant.Length != _lifeIcons.Length)
            {
                _lifeWant = new float[_lifeIcons.Length];
                _lifeShown = new float[_lifeIcons.Length];
                for (int i = 0; i < _lifeShown.Length; i++) _lifeShown[i] = 1f;
            }
            for (int i = 0; i < _lifeIcons.Length; i++)
                _lifeWant[i] = i < lives ? 1f : 0.12f;
        }

        const float LifeFade = 0.4f;
        float[] _lifeWant;
        float[] _lifeShown;

        void TickLives(float dt)
        {
            if (_lifeWant == null || _lifeIcons == null || _lifeIcons.Length != _lifeWant.Length) return;
            for (int i = 0; i < _lifeIcons.Length; i++)
            {
                if (_lifeIcons[i] == null) continue;
                _lifeShown[i] = Mathf.MoveTowards(_lifeShown[i], _lifeWant[i], dt * (1f - 0.12f) / LifeFade);
                _lifeIcons[i].color = new Color(1f, 1f, 1f, _lifeShown[i]);
            }
        }

        /// <summary>Подписать полосу здоровья именем текущего босса.</summary>
        public void SetBossName(string bossName)
        {
            if (_bossLabel != null) _bossLabel.text = bossName.TrimEnd('!');
        }

        public void ShowBossBar(bool show)
        {
            _bossBar.SetActive(show);
            if (show) _bossFill.fillAmount = 1f;
        }

        public void SetBossHp(float normalized) => _bossFill.fillAmount = Mathf.Clamp01(normalized);

        public void ShowBanner(string text, Color color, float duration = 1.8f)
        {
            _banner.text = text;
            _banner.color = color.WithAlpha(0f);
            _banner.gameObject.SetActive(true);
            _bannerTimer = duration;
            _bannerAge = 0f;
        }

        public void HideBanner()
        {
            _bannerTimer = 0f;
            _banner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ фабрика

        /// <summary>
        /// Шрифт интерфейса.
        ///
        /// Встроенный LegacyRuntime.ttf в редакторе работает, а в WebGL-сборке отдаёт
        /// только ASCII: кириллица пропадает, и весь интерфейс превращается в пустые
        /// кнопки. Проверено на живом билде — цифры рисовались, буквы нет.
        ///
        /// Поэтому в проекте лежит настоящий файл: Inter под SIL Open Font License,
        /// с полной кириллицей. Лицензия едет вместе со сборкой — она лежит в папке
        /// WebGL-шаблона и попадает в корень архива рядом с index.html.
        ///
        /// Запасные варианты оставлены на случай, если ресурс не найдётся: пусть лучше
        /// текст будет некрасивым, чем никаким.
        /// </summary>
        static Font UiFont
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.Load<Font>("Inter-Regular");
                if (_font == null) { try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } }
                if (_font == null) { try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return _font;
            }
        }

        /// <summary>
        /// Центр иконки монеты в мировых координатах — туда летят монеты.
        ///
        /// Раньше цель задавалась числом: «полширины влево и чуть ниже верха».
        /// Приближение держалось только на одном соотношении сторон и уезжало от
        /// иконки на телефонах с вырезом, потому что сама плашка живёт внутри
        /// безопасной зоны, а число про неё не знало.
        ///
        /// Канвас здесь ScreenSpaceOverlay: позиция RectTransform и есть позиция
        /// в экранных точках, поэтому хватает одного преобразования камерой.
        /// </summary>
        public Vector2 CoinTargetWorld(Camera cam)
        {
            if (_coinIcon == null || cam == null) return Vector2.zero;

            Vector3 screen = _coinIcon.position;
            screen.z = -cam.transform.position.z;     // плоскость игрового мира
            return cam.ScreenToWorldPoint(screen);
        }

        /// <summary>Отступ между монетой и числом. 4–8 точек по заданию.</summary>
        const float CoinGap = 6f;

        /// <summary>
        /// Поставить монету и число одной строкой, выровненной по центру родителя.
        ///
        /// **Зачем отдельный помощник.** Монета в игре встречается в семи местах, и
        /// в каждом её ставили на глаз. В HUD это кончилось наложением: иконка
        /// занимала x от −34 до −6, а число с выравниванием по левому краю
        /// рисовалось с −46 — потому что <see cref="Anchor"/> ЦЕНТРИРУЕТ
        /// прямоугольник, а не прижимает его, и «левый край» оказался левее
        /// иконки. В карточках и наградах было наоборот: число выравнивалось по
        /// краю, и при коротком значении между монетой и цифрами зияла дыра в
        /// полсотни точек.
        ///
        /// Ширину числа берём у самого текста (<c>preferredWidth</c>), поэтому
        /// отступ держится ровно при любом балансе — и при семи, и при семи
        /// тысячах.
        /// </summary>
        /// <param name="centerX">Куда поставить центр пары внутри родителя.</param>
        static void LayoutCoinRow(RectTransform icon, Text number, float centerX = 0f)
        {
            if (icon == null || number == null) return;

            float iconW = icon.sizeDelta.x;
            float textW = Mathf.Max(number.preferredWidth, 1f);
            float total = iconW + CoinGap + textW;
            float left = centerX - total * 0.5f;

            // Обе половины ставим от левого края пары: pivot слева, якорь по центру
            // родителя. Так ни одна не зависит от размера другой.
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(left, 0f);

            var rt = number.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left + iconW + CoinGap, 0f);
            rt.sizeDelta = new Vector2(textW, rt.sizeDelta.y);
            number.alignment = TextAnchor.MiddleLeft;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        struct ButtonParts
        {
            public RectTransform rt;
            public Button button;
            public Text text;
            public Image image;
            /// <summary>Цвета включённого состояния: выключение их не стирает.</summary>
            public Color enabledFill;
            public Color enabledText;
        }

        static ButtonParts NewButton(string name, Transform parent, string label, Color fill, Color textColor)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.RoundedRect("btn", 0.5f);
            img.type = Image.Type.Sliced;
            img.color = fill;
            img.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var text = NewText("Label", rt, label, 34, textColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 10f);
            text.fontStyle = FontStyle.Bold;

            return new ButtonParts
            {
                rt = rt, button = button, text = text, image = img,
                enabledFill = fill, enabledText = textColor,
            };
        }
    }
}
