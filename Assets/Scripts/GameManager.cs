using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    public enum GameState { Start, Playing, BossFight, GameOver, Paused }

    /// <summary>
    /// Игровой цикл: состояния, дистанция, сложность, столкновения и связка всех систем.
    /// Все объекты тикаются отсюда явно — так порядок «движение -> столкновения» гарантирован
    /// и не зависит от Script Execution Order.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Start;
        public float Distance { get; private set; }

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;
        ObstacleSpawner _spawner;
        FishSpawner _fishSpawner;
        FormationSpawner _formationSpawner;
        ScenerySpawner _scenerySpawner;
        ZoneSpawner _zoneSpawner;
        SpireSpawner _spireSpawner;
        IBoss[] _bosses;
        MantaBossController _manta;

        /// <summary>
        /// Сцена очищена под финального босса: ни декораций, ни крабов на камнях.
        /// Скат занимает собой почти весь кадр, и любая коралловая ветка рядом с ним
        /// превращается в визуальный шум, за которым не видно ни щелей, ни разрядов.
        /// </summary>
        bool _emptyStage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _forcedByDev;
#endif
        InkBossController _inkBoss;
        IBoss _activeBoss;
        /// <summary>
        /// Индекс босса — только для прыжка из дев-панели. Расписание он больше не
        /// ведёт: кто выйдет, определяет глубина (см. PickBoss). Ключ bossIndex в
        /// профиле остался ради совместимости со старыми сохранениями.
        /// </summary>
        int _bossIndex;
        BackgroundController _background;
        FxManager _fx;
        CoinFx _coinFx;
        StatusFx _statusFx;
        float _statusMoteTimer;
        SkinFxRuntime _skinFx;

        /// <summary>
        /// Оболочка столкновения стража. Есть только у легендарных скинов и
        /// трофеев; у остальных <see cref="_hasEnvelope"/> ложно, и в кадре не
        /// делается ровно ничего.
        /// </summary>
        EnvelopeBank _envelope;
        EnvelopeDef _envelopeDef;
        bool _hasEnvelope;

        UIManager _ui;
        AudioManager _audio;
        IPlatform _yandex;

        readonly List<Obstacle> _obstacles = new List<Obstacle>();
        readonly List<Fish> _fish = new List<Fish>();
        readonly List<CoralFormation> _formations = new List<CoralFormation>();
        readonly List<CoralSpire> _spires = new List<CoralSpire>();
        readonly List<Scenery> _scenery = new List<Scenery>();
        readonly List<SideCrab> _sideCrabs = new List<SideCrab>();

        OceanZone _activeZone;
        float _nextZoneAt;

        float _repelFxCooldown;
        float _trailTimer;
        float _blipTimer;
        float _heartbeatTimer;
        float _nextMilestone;
        float _scrollSpeed;
        /// <summary>
        /// Текущий потолок скорости. Растёт с каждым побеждённым боссом, поэтому
        /// отдельное поле: сам по себе maxScrollSpeed — только стартовое значение.
        /// </summary>
        float _speedCap;
        float _nextBossAt;
        int _sessionBest;
        int _persistentBest;
        int _deathCount;
        int _lastShownDepth = -1;
        bool _rewardUsedThisRun;

        // Сколько уже засчитано квестам за текущий заплыв. Нужно из-за доп. жизни
        // за рекламу: она продолжает тот же заплыв, GameOver случается второй раз,
        // и без этих счётчиков метры и монеты первой половины зачлись бы дважды.
        int _reportedDistance;
        int _reportedCoins;

        /// <summary>На какой глубине в последний раз вернули жизнь.</summary>
        float _healedAt;

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian,
                         ObstacleSpawner spawner, FishSpawner fishSpawner,
                         FormationSpawner formationSpawner, ScenerySpawner scenerySpawner,
                         ZoneSpawner zoneSpawner, SpireSpawner spireSpawner,
                         IBoss[] bosses, InkBossController inkBoss, MantaBossController manta,
                         BackgroundController background,
                         FxManager fx, UIManager ui, AudioManager audio, IPlatform platform)
        {
            _formationSpawner = formationSpawner;
            _scenerySpawner = scenerySpawner;
            _zoneSpawner = zoneSpawner;
            _spireSpawner = spireSpawner;
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;
            _spawner = spawner;
            _fishSpawner = fishSpawner;
            _bosses = bosses;
            _manta = manta;

            // Плеск чернил — единственное событие боссов, которого нет в IBoss:
            // звук нужен только этому бою, и тащить его в общий контракт незачем.
            _inkBoss = inkBoss;
            _inkBoss.InkSplashed += OnInkSplashed;
            _background = background;
            _fx = fx;

            // Монеты и эффекты скинов заводим здесь, а не в GameBootstrap: обоим
            // нужен только FxManager, который уже в руках, и тащить их через
            // двадцатистрочную подпись Init значило бы менять её ради ничего.
            // Тряски камеры в игре нет ни на одной ступени яркости: от неё болят
            // глаза. Вес удара передают звук, вибрация и частицы.

            _coinFx = gameObject.AddComponent<CoinFx>();
            _coinFx.Init(transform);

            // Статусы — общие на все виды препятствий: обломки, блоки строя,
            // сегменты шпилей и шары локаций. Общего у них ровно Transform и
            // SpriteRenderer, на них компонент и опирается.
            _statusFx = gameObject.AddComponent<StatusFx>();
            _statusFx.Init();

            _skinFx = new SkinFxRuntime();
            _skinFx.Init(fx, bubble, guardian, cfg.skinTrailDensity);

            // Оболочка столкновения: своя иерархия, потому что ей нужна орбита
            // вокруг стража, а частицы общего пула летят по прямой.
            _envelope = new EnvelopeBank();
            _envelope.Init(transform, guardian.transform);
            _envelope.SetBounds(field.HalfWidth, field.Bottom, field.Top);

            _ui = ui;
            _audio = audio;
            _yandex = platform;

            _ui.PlayClicked += OnRunRequested;
            _ui.RetryClicked += OnRunRequested;
            _ui.MenuOpened += OnMenuOpened;
            _ui.ShopOpened += OnShopOpened;
            _ui.PauseClicked += PauseRun;
            _ui.ResumeClicked += ResumeRun;
            _ui.QuitToMenuClicked += QuitRunToMenu;
            _ui.RewardedClicked += OnRewardedRequested;
            _ui.ButtonClicked += OnAnyButtonClicked;
            _ui.MuteToggled += OnMuteToggled;
            _ui.MusicToggled += OnMusicToggled;
            // Старые трофеи с боссов стали платными, и игрок, победивший их раньше,
            // не должен этого заметить: перенос делает их уже купленными.
            SkinStore.MigrateBossTrophies();

            _ui.SkinChosen += OnSkinChosen;
            _ui.SkinPurchased += OnSkinPurchased;
            _ui.AudioTick += OnWheelTick;
            _ui.ModalChanged += OnModalChanged;

            for (int i = 0; i < _bosses.Length; i++)
            {
                _bosses[i].GuardianBlocked += OnBossAttackBlocked;
                _bosses[i].Damaged += OnBossDamaged;
                _bosses[i].Defeated += OnBossDefeated;
                _bosses[i].BubbleHit += OnBubbleHitByBoss;
            }

            if (_manta != null)
            {
                _manta.ActChanged += OnMantaAct;
                _manta.Noised += OnMantaNoise;
            }

            _yandex.BestDepthLoaded += OnBestLoaded;
            _yandex.LanguageDetected += OnPlatformLanguage;
            _yandex.StateLoaded += OnCloudStateLoaded;
            Loc.Changed += OnLanguageChanged;
            RewardsHub.QuestCompleted += OnQuestCompleted;

            // Даже на стартовом экране мир должен ползти вниз — сразу видно, куда мы плывём.
            _scrollSpeed = _cfg.baseScrollSpeed;
            _speedCap = _cfg.maxScrollSpeed * Difficulty.Current.speedCapScale;
            _scenerySpawner.Prefill(_scenery);
            _ui.SetAudioState(_audio.Muted, _cfg.musicEnabled);
            ApplySkins();

            EnterStartState();
        }

        void OnDestroy()
        {
            if (_ui != null)
            {
                _ui.PlayClicked -= OnRunRequested;
                _ui.RetryClicked -= OnRunRequested;
                _ui.MenuOpened -= OnMenuOpened;
                _ui.ShopOpened -= OnShopOpened;
                _ui.PauseClicked -= PauseRun;
                _ui.ResumeClicked -= ResumeRun;
                _ui.QuitToMenuClicked -= QuitRunToMenu;
                _ui.RewardedClicked -= OnRewardedRequested;
                _ui.ButtonClicked -= OnAnyButtonClicked;
                _ui.MuteToggled -= OnMuteToggled;
                _ui.MusicToggled -= OnMusicToggled;
                _ui.SkinChosen -= OnSkinChosen;
                _ui.SkinPurchased -= OnSkinPurchased;
                _ui.AudioTick -= OnWheelTick;
                _ui.ModalChanged -= OnModalChanged;
            }

            RewardsHub.QuestCompleted -= OnQuestCompleted;

            if (_bosses != null)
            {
                for (int i = 0; i < _bosses.Length; i++)
                {
                    if (_bosses[i] == null) continue;
                    _bosses[i].GuardianBlocked -= OnBossAttackBlocked;
                    _bosses[i].Damaged -= OnBossDamaged;
                    _bosses[i].Defeated -= OnBossDefeated;
                    _bosses[i].BubbleHit -= OnBubbleHitByBoss;
                }
            }
            if (_yandex != null)
            {
                _yandex.BestDepthLoaded -= OnBestLoaded;
                _yandex.LanguageDetected -= OnPlatformLanguage;
                _yandex.StateLoaded -= OnCloudStateLoaded;
            }
            Loc.Changed -= OnLanguageChanged;
            if (_inkBoss != null) _inkBoss.InkSplashed -= OnInkSplashed;
            if (_manta != null)
            {
                _manta.ActChanged -= OnMantaAct;
                _manta.Noised -= OnMantaNoise;
            }
        }

        /// <summary>
        /// Язык площадки. Применяем только пока игрок сам не выбрал язык кнопкой:
        /// его выбор всегда важнее того, с какой страницы игру открыли.
        /// </summary>
        void OnPlatformLanguage(string code) => Loc.ApplyPlatformLanguage(code);

        // ------------------------------------------------------------- реклама
        //
        // Три формата и жёсткое разделение по месту:
        //
        //   * Rewarded — только по кнопке на экране поражения, одна на партию.
        //     Даёт жизнь и возвращает в ту же точку, метры не сбрасываются.
        //   * Межстраничная — только на переходах между экранами: старт партии,
        //     возврат в меню, вход в магазин, плюс каждая N-я смерть. Внутри партии
        //     не показывается никогда — это прямой запрет площадки.
        //   * Баннер — на экранах вне партии. В геймплее скрыт: он перекрывал бы
        //     поле, по которому игрок водит стража.
        //
        // Реальную частоту межстраничной ограничивает шаблон: не чаще раза в минуту
        // и никогда в первую минуту сессии. Поэтому здесь можно спокойно звать её на
        // каждом переходе — лишние вызовы отсекутся и вернут колбэк сразу.

        /// <summary>
        /// Сколько партий уже сыграно. До первой рекламу на переходах не показываем:
        /// первое, что видит новый игрок, не должно быть роликом.
        /// </summary>
        int _runsPlayed;

        void OnRunRequested()
        {
            if (_cfg.interstitialOnRunStart && _runsPlayed > 0 && _yandex.IsReady)
                _yandex.ShowInterstitial(StartRun);   // партия начнётся, когда ролик закроется
            else
                StartRun();
        }

        void OnMenuOpened()
        {
            _ui.SyncCoins();
            UpdateBanner();
            if (_cfg.interstitialOnMenu && _runsPlayed > 0) _yandex.ShowInterstitial(null);
        }

        void OnShopOpened()
        {
            _ui.SyncCoins();
            if (_cfg.interstitialOnShop && _runsPlayed > 0) _yandex.ShowInterstitial(null);
        }

        // ---------------------------------------------------------------- пауза

        /// <summary>Откуда вернёмся: партия могла стоять на боссе.</summary>
        GameState _stateBeforePause;

        /// <summary>
        /// Пауза по кнопке игрока. Мир замирает целиком: и препятствия, и декорации,
        /// и фон — иначе за время в магазине пузырёк уехал бы неизвестно куда.
        ///
        /// Реализована состоянием, а не Time.timeScale, потому что timeScale уже занят
        /// паузой площадки (реклама и скрытая вкладка). Два независимых механизма на
        /// одном поле рано или поздно разошлись бы: закрылся ролик — и timeScale
        /// вернулся в единицу прямо поверх открытого меню паузы.
        /// </summary>
        void PauseRun()
        {
            if (State != GameState.Playing && State != GameState.BossFight) return;

            _stateBeforePause = State;
            State = GameState.Paused;
            _guardian.InputEnabled = false;
            _yandex.GameplayStop();
            UpdateBanner();
            _ui.ShowPause();
        }

        void ResumeRun()
        {
            if (State != GameState.Paused) return;

            State = _stateBeforePause;
            _guardian.InputEnabled = true;

            // Экран паузы гасит всё, включая HUD и полосу босса, — возвращаем их сами.
            // Полосу только если бой действительно шёл: иначе она повиснет пустой.
            _ui.ShowHud();
            if (_activeBoss != null)
            {
                _ui.ShowBossBar(true);
                _ui.SetBossName(_activeBoss.DisplayName);
                _ui.SetBossHp(_activeBoss.MaxHp > 0 ? _activeBoss.Hp / (float)_activeBoss.MaxHp : 0f);
            }

            UpdateBanner();
            _yandex.GameplayStart();
        }

        /// <summary>
        /// Игрок бросает партию из паузы. Метры засчитываем: он их прошёл, и терять
        /// рекорд из-за того, что вышел добровольно, было бы несправедливо.
        /// </summary>
        void QuitRunToMenu()
        {
            if (State != GameState.Paused) return;

            int depth = Mathf.FloorToInt(Distance);
            _sessionBest = Mathf.Max(_sessionBest, depth);
            if (depth > _persistentBest)
            {
                _persistentBest = depth;
                _yandex.SaveBest(_persistentBest);
                _yandex.SubmitScore(_persistentBest);
            }

            EnterStartState();   // ShowStart сам погасит экран паузы
        }

        /// <summary>Баннер видно везде, кроме самой партии.</summary>
        void UpdateBanner()
        {
            bool inRun = State == GameState.Playing || State == GameState.BossFight;
            _yandex.SetBannerVisible(_cfg.bannerEnabled && !inRun);
        }

        /// <summary>Первый клик — это тот самый жест пользователя, после которого браузер пускает звук.</summary>
        void OnAnyButtonClicked()
        {
            _audio.StartAmbience();
            _audio.PlayUiClick();
        }

        void OnMuteToggled()
        {
            _audio.ToggleMute();
            _ui.SetAudioState(_audio.Muted, _cfg.musicEnabled);
            _yandex.MarkDirty();
        }

        void OnMusicToggled()
        {
            _cfg.musicEnabled = !_cfg.musicEnabled;
            _audio.SetMusicEnabled(_cfg.musicEnabled);
            _ui.SetAudioState(_audio.Muted, _cfg.musicEnabled);
            _yandex.MarkDirty();
        }

        /// <summary>
        /// Покупка состоялась. Монеты списал магазин, нам остаётся звук — и отметка
        /// «есть что отправить в облако»: баланс и покупки едут туда же, где рекорд.
        /// </summary>
        void OnSkinPurchased()
        {
            _audio.PlayReward();
            _yandex.MarkDirty(immediate: true);
        }

        void OnSkinChosen(int category, int index)
        {
            ApplySkins();

            // Скин поменялся — вместе с ним меняются след и анимация тела.
            _skinFx.Refresh();
            _yandex.MarkDirty();
        }

        void OnLanguageChanged() => _yandex.MarkDirty();

        void OnInkSplashed() => _audio.PlayInkSplat();

        /// <summary>
        /// Из облака приехало сохранение — оно уже разложено по PlayerPrefs, осталось
        /// заставить подсистемы перечитать себя. Порядок важен: язык последним, потому
        /// что его смена пересобирает интерфейс, и он должен увидеть уже свежие звук
        /// и скины.
        /// </summary>
        void OnCloudStateLoaded()
        {
            // Облако могло принести победы над боссами, которых это устройство не
            // видело. Перенос трофеев повторяем — он сам отработает один раз.
            SkinStore.MigrateBossTrophies();

            _audio.SetMuted(PlayerPrefs.GetInt(SaveData.MuteKey, 0) != 0);
            _audio.SetMusicEnabled(PlayerPrefs.GetInt(SaveData.MusicKey, 1) != 0);
            _ui.SetAudioState(_audio.Muted, _cfg.musicEnabled);

            ApplySkins();
            Loc.Reload();

            // Меню открылось до готовности SDK, и первый показ баннера ушёл впустую:
            // SetBannerVisible молча выходит, пока IsReady == false. Повторяем здесь —
            // это первый момент, когда SDK точно отозвался.
            UpdateBanner();
        }

        /// <summary>
        /// Надеть выбранные скины. Скины за глубину — только оттенок, трофейные
        /// меняют ещё и рисунок, поэтому передаём и спрайт.
        /// </summary>
        /// <summary>
        /// Надеть выбранные скины.
        ///
        /// **Здесь была главная причина расхождения с магазином.** Материал брался
        /// у ПУЗЫРЬКА и выдавался обеим половинкам: «это пара, и разная начинка
        /// читалась бы как две разные вещи». На деле выходило хуже. Половинки
        /// выбираются независимо, и у игрока со стандартным пузырьком «Пена»
        /// материал стеклянный, то есть отсутствует вовсе, — поэтому любой
        /// купленный страж в бою оставался БЕЗ начинки, хотя в витрине она текла.
        ///
        /// Теперь каждая половина берёт свой вид через <see cref="SkinVisuals"/> —
        /// ту же функцию, которой пользуется витрина.
        /// </summary>
        void ApplySkins()
        {
            SkinStore.ValidateSelection(_persistentBest);

            var bubble = SkinVisuals.Bubble();
            var guardian = SkinVisuals.Guardian();

            _bubble.ApplySkin(bubble.Shell, bubble.Core, bubble.Tint);
            _guardian.ApplySkin(guardian.Shell, guardian.Core, guardian.Tint);

            // Оболочка — такая же часть скина, как оболочка и начинка, и берётся
            // отсюда же, из выбранного стража. Сменил скин — сменилась и она, без
            // перезапуска заплыва.
            _hasEnvelope = EnvelopeTable.TryGet(
                SkinStore.Guardian[SkinStore.SelectedGuardian], out _envelopeDef);

            // Старая оболочка от прежнего скина досматривать себя не должна.
            if (_envelope != null) _envelope.Clear();
        }

        void OnBestLoaded(int best)
        {
            _persistentBest = Mathf.Max(_persistentBest, best);
            _sessionBest = Mathf.Max(_sessionBest, best);
            _ui.SetBestDepth(_persistentBest);
            ApplySkins();
        }

        // ----------------------------------------------------------- состояния

        void EnterStartState()
        {
            State = GameState.Start;
            _guardian.InputEnabled = false;

            // Босса убираем именно здесь. Смерть его больше не снимает — он ждёт,
            // не возьмёт ли игрок доп. жизнь, — так что на выходе в меню его надо
            // погасить явно, иначе туша осталась бы висеть над стартовым экраном.
            DespawnAllBosses();

            if (_emptyStage)
            {
                _emptyStage = false;
                _scenerySpawner.ResetState();
                _scenerySpawner.Prefill(_scenery);
            }

            _bubble.ResetState();
            _guardian.ResetState();
            _ui.ShowStart();
        }

        public void StartRun()
        {
            ClearObstacles();
            ClearFish();
            ClearFormations();
            ClearSpires();
            ClearZone();
            _fx.ClearAll();
            _envelope.Clear();
            DespawnAllBosses();

            Distance = 0f;
            _emptyStage = false;

            Wallet.BeginRun();
            _reportedDistance = 0;
            _reportedCoins = 0;
            _coinFx.ClearAll();
            _statusFx.ClearAll();

            // Скин мог смениться в магазине между заплывами — перечитываем выбор,
            // иначе след и анимация остались бы от прошлого.
            _skinFx.Refresh();
            // Цель полёта монет — центр иконки в плашке баланса, а не число
            // «от края экрана»: плашка живёт в безопасной зоне, и на телефоне с
            // вырезом приближение промахивалось мимо неё.
            _coinFx.SetTarget(_ui.CoinTargetWorld(_field.Cam));
            _scrollSpeed = _cfg.baseScrollSpeed;
            _speedCap = _cfg.maxScrollSpeed * Difficulty.Current.speedCapScale;
            _healedAt = 0f;
            _nextBossAt = BossStep;
            BossTuning.Reset();
            _lastShownDepth = -1;
            _rewardUsedThisRun = false;
            _runsPlayed++;
            _nextMilestone = _cfg.milestoneEvery;
            _blipTimer = _cfg.bubbleBlipInterval;
            _heartbeatTimer = 0f;
            _nextZoneAt = _cfg.zoneFirstAt * Mathf.Max(1f, Difficulty.Current.unlockDepthScale);

            _bubble.ResetState();
            _guardian.ResetState();
            _guardian.InputEnabled = true;
            _spawner.ResetState();
            _fishSpawner.ResetState();
            _formationSpawner.ResetState();
            _spireSpawner.ResetState();

            _ui.ShowHud();
            _ui.ShowBossBar(false);
            _ui.SetLives(_bubble.Lives);
            _ui.SetDepth(0);

            State = GameState.Playing;
            UpdateBanner();               // в партии баннер убираем
            _audio.PlayCalmMusic(0.4f);
            _yandex.GameplayStart();
        }

        void GameOver()
        {
            State = GameState.GameOver;
            _guardian.InputEnabled = false;

            // Босса оставляем на месте, если он ещё жив: игрок может взять доп. жизнь
            // за рекламу, и тогда бой продолжится с того же HP. Раньше босс исчезал
            // здесь же, и реклама фактически дарила пропуск боя вместе с трофеем.
            // Стража при этом освобождаем: держать его мёртвой хваткой через смерть
            // некому и незачем.
            _guardian.Release();
            if (_activeBoss == null) DespawnAllBosses();

            _ui.ShowBossBar(false);

            int depth = Mathf.FloorToInt(Distance);
            _sessionBest = Mathf.Max(_sessionBest, depth);

            // Монеты копились в памяти весь заплыв — вот теперь на диск. Писать их
            // на каждое разбитое препятствие нельзя: в WebGL PlayerPrefs.Save()
            // синхронизирует IndexedDB, а разрушений за партию бывают сотни.
            Wallet.FlushRun();
            if (Wallet.EarnedThisRun > 0) _yandex.MarkDirty();

            ReportRunToQuests(depth);

            // Три коротких проигрыша подряд — и следующий заплыв пойдёт мягче.
            // Игроку об этом не говорим: сообщение превратило бы помощь в диагноз.
            Difficulty.ReportRunEnd(depth);

            if (depth > _persistentBest)
            {
                _persistentBest = depth;
                _yandex.SaveBest(_persistentBest);
                _yandex.SubmitScore(_persistentBest);
                _ui.SetBestDepth(_persistentBest); // мог открыться новый скин
            }

            _deathCount++;
            _audio.PlayGameOver();
            _audio.PlayCalmMusic();
            _yandex.GameplayStop();

            bool offerRewarded = _cfg.rewardedExtraLifeEnabled && !_rewardUsedThisRun
                                 && _yandex.CanShowRewarded;
            _ui.ShowGameOver(depth, Mathf.Max(_sessionBest, _persistentBest), offerRewarded);

            UpdateBanner();               // вне партии баннер снова виден

            int every = Mathf.Max(1, _cfg.interstitialEveryNDeaths);
            if (_deathCount % every == 0) _yandex.ShowInterstitial(null);
        }

        void OnRewardedRequested()
        {
            if (_rewardUsedThisRun) return;
            _rewardUsedThisRun = true;
            _ui.HideRewardedButton();

            _yandex.ShowRewarded(
                onGranted: ContinueWithExtraLife,
                onClosed: null);
        }

        void ContinueWithExtraLife()
        {
            ClearObstacles();
            ClearFish();
            ClearFormations();
            _fx.ClearAll();

            // Пал у босса — туда и возвращаемся: тот же бой, то же его здоровье.
            // Но всё, что он начал до смерти, отменяем: летящие снаряды и зажатый
            // страж обрушились бы на игрока в первый же кадр после возрождения.
            bool resumeBoss = _activeBoss != null;
            if (resumeBoss) _activeBoss.Interrupt();
            else DespawnAllBosses();

            _bubble.AddLife();
            _guardian.ResetState();
            _guardian.InputEnabled = true;

            _ui.ShowHud();
            _ui.ShowBossBar(resumeBoss);
            _ui.SetLives(_bubble.Lives);
            _ui.ShowBanner(Loc.Get("banner.extraLife"), Palette.Aqua, 1.4f);
            _audio.PlayReward();

            if (resumeBoss)
            {
                _ui.SetBossName(_activeBoss.DisplayName);
                _ui.SetBossHp(_activeBoss.MaxHp > 0
                    ? _activeBoss.Hp / (float)_activeBoss.MaxHp : 1f);

                if (_activeBoss.Kind == BossKind.Manta) _audio.PlayFinaleMusic();
                else _audio.PlayBossMusic();
            }
            else
            {
                _audio.PlayCalmMusic();

                // Босса не было — просто едем дальше по сетке.
                if (_nextBossAt <= Distance) _nextBossAt = NextBossMark(Distance);
            }

            State = resumeBoss ? GameState.BossFight : GameState.Playing;
            UpdateBanner();               // снова в партии — баннер прячем
            _yandex.GameplayStart();
        }

        // --------------------------------------------------------------- цикл

        /// <summary>
        /// Системная кнопка «Назад». На Android она обязана работать всегда и
        /// предсказуемо: из партии — пауза, из паузы — обратно в игру, из любого
        /// экрана — на шаг выше, из главного меню — выход из приложения.
        ///
        /// Приложение, которое по «Назад» не делает ничего или сразу закрывается
        /// посреди партии, — типовая придирка при проверке в Google Play.
        /// </summary>
        void HandleBackButton()
        {
            switch (State)
            {
                case GameState.Playing:
                case GameState.BossFight:
                    PauseRun();
                    return;

                case GameState.Paused:
                    ResumeRun();
                    return;

                case GameState.GameOver:
                    // С экрана поражения «Назад» = «в меню», как и одноимённая кнопка.
                    EnterStartState();
                    return;
            }

            // В меню: сначала отдаём кнопку интерфейсу — вдруг открыт магазин или
            // справочник. Только если закрывать нечего, выходим из игры.
            if (_ui.TryGoBack()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Application.Quit();
#endif
        }

        void Update()
        {
            if (_cfg == null) return; // Init ещё не вызван

            float dt = Mathf.Min(Time.deltaTime, 0.033f); // как в прототипе — не даём «телепортов» после лага

            // Остановки кадра от удара здесь больше нет. Она делала удар весомее,
            // но ценой рывков: страж бьёт по нескольку раз в секунду, и каждый раз
            // мир дёргался. Управление в этой игре — единственное, чем игрок
            // владеет, и портить его ради весомости нельзя.

            if (PointerInput.BackPressed()) HandleBackButton();

            // На паузе замирает всё: мир, фон, декорации. Живым остаётся только
            // интерфейс — иначе меню паузы не смогло бы даже мигнуть баннером.
            // Пузырёк за время, проведённое игроком в магазине, не должен уехать.
            if (State == GameState.Paused) { _ui.Tick(dt); return; }

            _field.Tick(dt);
            _background.Tick(dt, Distance, _scrollSpeed);
            TickScenery(dt);
            EmitRiseTrail(dt);   // след идёт и на стартовом экране — сразу видно, что мы всплываем
            _fx.Tick(dt);
            _coinFx.Tick(dt);
            _statusFx.Tick(dt);
            TickStatusMotes(dt);

            // Такт оболочки идёт до возврата по состояниям: удар может прийтись
            // на последний кадр заплыва, и оболочка обязана догаснуть, а не
            // застыть на экране над надписью «игра окончена».
            // Границы обновляем каждый кадр: шесть присваиваний против поворота
            // экрана, после которого отсечка считала бы по старой ширине.
            _envelope.SetBounds(_field.HalfWidth, _field.Bottom, _field.Top);
            _envelope.Tick(dt);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_devEnvelopeLoop && _hasEnvelope && _envelope.DebugLive == 0)
                DevGuardianCollision();
#endif
            _skinFx.Tick(dt, State == GameState.Playing);
            _ui.Tick(dt);

            if (_repelFxCooldown > 0f) _repelFxCooldown -= dt;

            if (State == GameState.Start || State == GameState.GameOver)
            {
                // Убираем остатки прошлой партии — здесь это безопасно, вне цикла столкновений.
                if (_obstacles.Count > 0) ClearObstacles();
                if (_fish.Count > 0) ClearFish();
                if (_formations.Count > 0) ClearFormations();
                if (_spires.Count > 0) ClearSpires();
                if (_activeZone != null) ClearZone();
                _bubble.Tick(dt);
                _guardian.Tick(dt);
                return;
            }

            _bubble.Tick(dt);
            _guardian.Tick(dt);

            if (State == GameState.Playing) TickPlaying(dt);
            else if (State == GameState.BossFight) TickBossFight(dt);

            TickZone(dt);

            // рыбы охотятся и во время боя с боссом
            TickFish(dt);
            SpawnFishIfDue(dt);

            if (_guardian.JustRecharged) _audio.PlayGuardianReady();
            TickAmbientCues(dt);

            if (_guardian.RepelledThisFrame && _repelFxCooldown <= 0f)
            {
                _repelFxCooldown = 0.35f;
                _fx.Burst(_guardian.Center, Palette.Coral, 5, 0.3f, 1.1f, 0.04f, 0.08f);
                _audio.PlayRepel();
            }
        }

        void TickPlaying(float dt)
        {
            Distance += _scrollSpeed * dt * _cfg.metersPerUnit;
            // Разгон — главная причина, по которой игра «вдруг становится
            // невозможной»: он копится незаметно, а чувствуется сразу весь.
            _scrollSpeed = Mathf.Min(_speedCap,
                _scrollSpeed + _cfg.scrollAcceleration * Difficulty.Current.accelerationScale * dt);

            int shown = Mathf.FloorToInt(Distance);
            if (shown != _lastShownDepth)
            {
                _lastShownDepth = shown;
                _ui.SetDepth(shown);
            }

            // Внутри локации обычный поток обломков и построек не идёт:
            // локация — самодостаточный участок, как уровень в Rise Up.
            if (_activeZone == null)
            {
                var spawned = _spawner.Tick(dt, _scrollSpeed, Mathf.FloorToInt(Distance));
                if (spawned != null) _obstacles.Add(spawned);

                var formation = _formationSpawner.Tick(dt, Distance);
                if (formation != null) _formations.Add(formation);

                var spire = _spireSpawner.Tick(dt, Distance);
                if (spire != null) _spires.Add(spire);

                TrySpawnZone();
            }

            TickObstacles(dt, checkCollisions: true);
            TickFormations(dt, checkCollisions: true);
            TickSpires(dt, checkCollisions: true);

            // Босс ждёт, пока локация не проплывёт целиком — иначе будет каша.
            if (Distance >= _nextBossAt && _activeZone == null) StartBossFight();
        }

        void TickBossFight(float dt)
        {
            // Дистанция не растёт: путь перекрыт, пока босс жив.
            TickObstacles(dt, checkCollisions: false);
            TickFormations(dt, checkCollisions: false);
            TickSpires(dt, checkCollisions: false);
            if (_activeBoss != null) _activeBoss.Tick(dt);
        }

        void TickObstacles(float dt, bool checkCollisions)
        {
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var o = _obstacles[i];
                bool alive = o.Tick(dt, _field);

                // Воронка тянет стража, пока цела. Делаем это до проверки
                // столкновений: иначе течение опаздывало бы на кадр и выглядело
                // как рывок после удара, а не как сопротивление воды.
                if (alive && checkCollisions && o.PullStrength > 0f)
                    _guardian.AddPull(o.Center, o.PullStrength);

                if (alive && checkCollisions && !o.Deflected && o.Harmful)
                {
                    // 1) страж бьёт препятствие — если успел перезарядиться
                    if (_guardian.CanDeflect &&
                        Vector2.Distance(o.Center, _guardian.Center) < o.Radius + _guardian.Reach)
                    {
                        Vector2 dir = o.Center - _guardian.Center;
                        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

                        var outcome = o.Hit(dir + Vector2.up * 0.4f, _cfg.deflectImpulse);

                        _guardian.NotifyDeflect();
                        _audio.PlayDeflect(o.Center, _field.HalfWidth);
                        Distance += _cfg.deflectMeterBonus;

                        // Удар показываем всегда и первым: по прочной створке страж
                        // бьёт трижды, и все три раза игрок обязан видеть, что
                        // попадание засчитано, а не только на последнем.
                        bool broke = outcome == HitOutcome.Destroyed;

                        o.SetHitColor(_skinFx.HitColor);
                        GuardianImpact(o.Center, o.Radius, outcome == HitOutcome.Damaged, broke);

                        // Статус переживает удар: препятствие горит, леденеет или
                        // разъедается ещё несколько секунд. Но только если оно
                        // уцелело: на разбитом статус не успевает прочитаться и
                        // ложится третьим слоем поверх удара и разрушения.
                        if (!broke)
                        {
                            Ignite(o.transform, o.Renderer);
                            if (_skinFx.StatusKind == StatusKind.Freeze) o.ChillSpin(dt);
                        }

                        if (broke) RewardHazard(o, chain: true);
                    }
                    // 2) препятствие достало пузырёк
                    else if (HitsBubble(o.Center, o.Radius))
                    {
                        Vector2 away = o.Center - _bubble.Center;
                        if (away.sqrMagnitude < 0.0001f) away = Vector2.down;

                        // Именно Deflect, а не Hit: за то, во что врезались сами,
                        // монет не платят. Разные входы в препятствие ровно затем и
                        // сделаны, чтобы этот путь физически не мог начислить награду.
                        o.Deflect(away.normalized + Vector2.down * 0.6f, _cfg.deflectImpulse * 0.8f);
                        _skinFx.OnHurt(_bubble.Center);
                        DamageBubble(_bubble.Center);
                    }
                }

                if (!alive)
                {
                    _obstacles.RemoveAt(i);
                    _spawner.Recycle(o);
                }
            }
        }


        /// <summary>
        /// Наложить статус скина на что угодно, у чего есть Transform и рендерер.
        ///
        /// Одна строка на любой вид препятствия — именно ради этого статус и вынесен
        /// в общий компонент. Раньше он жил внутри Obstacle и доставался только
        /// обломкам, а блоки строя, цепочки шпилей и шары локаций оставались
        /// невозмутимыми, сколько по ним ни бей.
        /// </summary>
        void Ignite(Transform target, SpriteRenderer renderer)
        {
            if (_statusFx == null || renderer == null) return;

            // Длительность срезана потолком: в таблице материалов есть значения
            // до четырёх секунд, а дольше трёх статус живёт уже за пределами того
            // отрезка, где игрок связывает его со своим ударом.
            _statusFx.Apply(target, renderer, _skinFx.StatusKind,
                            _skinFx.StatusHot, _skinFx.StatusCool,
                            Mathf.Min(_skinFx.StatusTime, StatusFx.MaxDuration));
        }

        /// <summary>
        /// Единая точка для удара стража обо что угодно: препятствие, коралл, рыбу.
        ///
        /// Точку касания считаем сами — она лежит на поверхности стража по линии к
        /// цели. Центр стража и центр цели не годятся: удар произошёл между ними, и
        /// эффект обязан быть там же, иначе это не удар, а вспышка рядом.
        ///
        /// Звук, вибрация и частицы удара идут отсюда же, чтобы их нельзя было
        /// забыть добавить к очередному новому виду угрозы. Остановки кадра и
        /// тряски камеры нет и не будет: от них болят глаза.
        /// </summary>
        /// <param name="destroyed">
        /// Цель разрушена этим же ударом. Тогда частиц удара нет вовсе: удар,
        /// статус и разрушение, наложенные в один кадр, — это три слоя эффектов на
        /// одном препятствии, и разобрать в них что-либо нельзя. Остаются звук и
        /// вибрация: они не загромождают экран, а подпись скина несут ту же.
        /// </param>
        void GuardianImpact(Vector2 targetCenter, float targetRadius, bool heavy,
                            bool destroyed = false)
        {
            Vector2 toTarget = targetCenter - _guardian.Center;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector2.up;

            Vector2 n = toTarget.normalized;

            // Середина перекрытия: страж и цель уже пересеклись, и точка касания —
            // не поверхность стража, а место между поверхностями.
            float gap = Mathf.Max(0f, toTarget.magnitude - targetRadius);
            Vector2 contact = _guardian.Center + n * Mathf.Lerp(gap, _guardian.Radius, 0.5f);

            // Отдача: страж чуть подаётся назад. Срезана вдвое — просили, чтобы
            // подрагивание было еле заметным.
            _guardian.Nudge(-n * 0.05f);

            // Подпись стихии: звук, вибрация и цвет частиц. Вспышка на весь экран
            // убрана целиком, hit-stop тоже — от первого болели глаза, второй давал
            // рывки. Различие между скинами осталось в том, что не слепит: в цвете,
            // форме и типе частиц, в высоте звука и в рисунке вибрации.
            var arch = _skinFx.Archetype;

            _audio.PlayImpact(_skinFx.ImpactKind, arch.SoundPitch, contact, _field.HalfWidth);
            Haptics.Play(arch.Haptic);

            // Оболочка — у легендарных и трофейных. Здесь, до возврата по
            // разрушению: сам по себе удар состоялся, и вокруг стража должно
            // вспыхнуть даже тогда, когда препятствия через кадр уже нет. Нить к
            // точке касания при этом не тянется — тянуться уже не к чему.
            if (_hasEnvelope)
                _envelope.Play(_envelopeDef, contact, targetCenter, _guardian.Radius,
                               targetRadius, link: !destroyed);

            // Разрушили — показываем только разрушение, его сыграет RewardHazard.
            if (destroyed) return;

            _skinFx.OnImpact(contact, n);
        }

        /// <summary>
        /// Препятствие разрушено: монеты, эффект, звук.
        ///
        /// Единственное место во всей игре, где начисляются деньги. Если когда-нибудь
        /// понадобится второй источник — заводить его нужно отдельным методом, а не
        /// вызовом этого: иначе «монеты только за уничтоженное» перестанет быть
        /// правилом и станет совпадением.
        /// </summary>
        void RewardHazard(Obstacle o, bool chain)
        {
            int reward = o.Reward;
            Wallet.Earn(reward);
            RewardsHub.ReportDestroy(_skinFx.StatusKind);

            _coinFx.Spill(o.Center, reward);
            _ui.ShowCoinGain(o.Center, reward);
            _skinFx.OnHazardDestroyed(o.Center, o.Death);
            _audio.PlayCoin();

            // Мина уносит соседей. Цепь ровно в один шаг: взрыв от взрыва увёл бы
            // половину экрана и, что хуже, подарил бы за один удар целое состояние.
            if (chain && o.Death == HazardDeath.Blast) Detonate(o);
        }

        /// <summary>
        /// Награда за разбитый коралл: шар локации, сегмент шпиля, блок строя.
        ///
        /// Отдельно от <see cref="RewardHazard"/>, потому что у кораллов нет ни
        /// прочности, ни вида из таблицы — это части большего объекта. Цена у них
        /// одна и небольшая: ломаются они пачками, и платить за каждый как за
        /// отдельную угрозу значило бы сделать строй кораллов золотой жилой.
        /// </summary>
        void RewardCoral(Vector2 at)
        {
            int reward = Mathf.Max(0, _cfg.coralCoinReward);
            if (reward <= 0) return;

            Wallet.Earn(reward);
            RewardsHub.ReportDestroy(_skinFx.StatusKind);
            _coinFx.Spill(at, reward);
            _ui.ShowCoinGain(at, reward);
            _skinFx.OnHazardDestroyed(at, HazardDeath.Crumble);
            _audio.PlayCoin();
        }

        /// <summary>
        /// Квест выполнен прямо в бою.
        ///
        /// Говорим об этом тем же баннером, что и об открытом пути, и не уводя
        /// игрока с экрана: прерывать заплыв окном награды — верный способ, чтобы
        /// квесты начали раздражать. Забрать монеты можно потом, в меню.
        /// </summary>
        /// <summary>
        /// Открылся или закрылся экран поверх игры.
        ///
        /// Прячем пузырёк со стражем: подложка меню плотная, но полагаться только
        /// на неё нельзя — любой полупрозрачный элемент интерфейса (например,
        /// приглушённая кнопка) снова пропустил бы их свечение наружу.
        ///
        /// Эффекты и препятствия не трогаем: они тусклее, их за подложкой не
        /// видно, а гасить их означало бы спорить с тем, что мир в меню живёт.
        /// </summary>
        void OnModalChanged(bool modal)
        {
            _bubble.SetVisible(!modal);
            _guardian.SetVisible(!modal);

            // На паузе такт не идёт, и оболочка, застигнутая открытием магазина,
            // осталась бы висеть над затемнением. Гасим её вместе со стражем.
            if (modal) _envelope.Clear();
        }

        /// <summary>Щелчок указателя колеса. Звуком владеет GameManager, не UI.</summary>
        void OnWheelTick() => _audio.PlayWheelTick();

        void OnQuestCompleted(string questName)
        {
            if (State != GameState.Playing) return;
            _ui.ShowBanner(Loc.Get("quest.done") + "\n" + questName, Palette.Gold, 2.2f);
        }

        /// <summary>
        /// Итоги заплыва для квестов и достижений.
        ///
        /// Считается разница с тем, что уже засчитано: доп. жизнь за рекламу
        /// продолжает тот же заплыв, и GameOver по нему случается дважды. Без
        /// разницы «проплыть 3000 метров» закрывалось бы одним заплывом на 1600 с
        /// одним просмотром рекламы.
        ///
        /// Глубина и монеты за заплыв идут через ReportRunBest: такие квесты берут
        /// максимум, а не сумму. «Набрать 150 монет за один заплыв» — это про один
        /// заплыв, а не про три по пятьдесят.
        /// </summary>
        void ReportRunToQuests(int depth)
        {
            int coins = Wallet.EarnedThisRun;

            RewardsHub.Report(QuestGoal.Runs, 1);
            RewardsHub.Report(QuestGoal.Distance, Mathf.Max(0, depth - _reportedDistance));
            RewardsHub.ReportRunBest(QuestGoal.DepthInRun, depth);
            RewardsHub.ReportRunBest(QuestGoal.CoinsInRun, coins);
            RewardsHub.ReportCoinsEarned(coins - _reportedCoins);

            _reportedDistance = Mathf.Max(_reportedDistance, depth);
            _reportedCoins = Mathf.Max(_reportedCoins, coins);

            // Прогресс квестов пишем на диск здесь, а не на каждое препятствие, —
            // ровно по той же причине, по которой так же устроен кошелёк: в WebGL
            // PlayerPrefs.Save() синхронизирует IndexedDB, а разрушений за партию
            // бывают сотни. Конец заплыва — естественная и достаточно частая точка.
            RewardStore.Save();
            TimeService.Persist();
        }

        /// <summary>Взрыв мины: всё в радиусе получает удар, своя награда — за своё.</summary>
        void Detonate(Obstacle mine)
        {
            const float blastRadius = 1.6f;

            _fx.Burst(mine.Center, Palette.Hex("FFB45B"), 16, 1.6f, 4.2f, 0.04f, 0.1f);
            _audio.PlayCoralBreak(mine.Center, _field.HalfWidth);

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var other = _obstacles[i];
                if (other == mine || other.Deflected) continue;
                if (Vector2.Distance(other.Center, mine.Center) > blastRadius) continue;

                Vector2 push = other.Center - mine.Center;
                if (push.sqrMagnitude < 0.0001f) push = Vector2.up;

                if (other.Hit(push, _cfg.deflectImpulse) == HitOutcome.Destroyed)
                    RewardHazard(other, chain: false);
            }
        }

        /// <summary>
        /// Частицы горящих и леденеющих препятствий.
        ///
        /// Идут по списку активных статусов, а не по обломкам: гореть может блок
        /// строя или сегмент шпиля, и перебор одних обломков их бы не нашёл.
        /// </summary>
        void TickStatusMotes(float dt)
        {
            _statusMoteTimer -= dt;
            if (_statusMoteTimer > 0f) return;
            _statusMoteTimer = 0.09f;

            for (int i = 0; i < 8; i++)
            {
                if (!_statusFx.TryGetActive(i, out Vector2 at, out float radius, out StatusKind kind))
                    continue;

                _skinFx.EmitStatusMote(at, radius, kind);
            }
        }

        void ClearObstacles()
        {
            for (int i = 0; i < _obstacles.Count; i++) _spawner.Recycle(_obstacles[i]);
            _obstacles.Clear();
        }

        // ------------------------------------------------------------ локации

        void TrySpawnZone()
        {
            if (_activeZone != null) return;
            if (Distance < _nextZoneAt) return;
            // не начинаем локацию впритык к боссу
            if (Distance > _nextBossAt - 80f) return;

            _activeZone = _zoneSpawner.Spawn();
            if (_cfg.zoneShowBanner)
                _ui.ShowBanner(_activeZone.DisplayName, _activeZone.BannerColor, 2.2f);
        }

        void TickZone(float dt)
        {
            if (_activeZone == null)
            {
                _background.ClearTheme();
                _spawner.BiomeTint = Color.white;   // вне локации — свои цвета видов
                return;
            }

            bool alive = _activeZone.Tick(dt, _scrollSpeed, _field);

            // цвет воды подмешивается по тому, насколько локация закрывает экран
            float coverage = _activeZone.ScreenCoverage(_field);
            _background.SetTheme(_activeZone.WaterTop, _activeZone.WaterBottom, coverage);

            // Тем же цветом красим и препятствия — но осветлённым и ровно настолько,
            // насколько локация уже заняла экран. Красить на полную нельзя: вода у
            // впадины почти чёрная, и камни в ней стали бы невидимы.
            Color water = Color.Lerp(_activeZone.WaterTop, _activeZone.WaterBottom, 0.5f);
            Color lifted = Color.Lerp(Color.white, water * 1.9f, 0.8f);
            _spawner.BiomeTint = Color.Lerp(Color.white, lifted, Mathf.Clamp01(coverage));

            if (State == GameState.Playing) CheckZoneCollisions(_activeZone);

            if (!alive)
            {
                _zoneSpawner.Recycle(_activeZone);
                _activeZone = null;
                _background.ClearTheme();
                _nextZoneAt = Distance + Random.Range(_cfg.zoneIntervalMin, _cfg.zoneIntervalMax)
                              * Difficulty.FeatureIntervalFactor(Distance);
            }
        }

        void CheckZoneCollisions(OceanZone zone)
        {
            var blobs = zone.Blobs;

            for (int i = 0; i < blobs.Count; i++)
            {
                var blob = blobs[i];
                if (!blob.Alive) continue;

                if (_guardian.CanDeflect &&
                    Vector2.Distance(blob.Center, _guardian.Center) < blob.Radius + _guardian.Reach)
                {
                    zone.Shatter(blob);
                    RewardCoral(blob.Center);
                    // Шар локации разбивается с одного удара, поэтому ни удара,
                    // ни статуса: только разрушение, которое сыграл RewardCoral.
                    GuardianImpact(blob.Center, blob.Radius, heavy: false, destroyed: true);
                    _guardian.NotifyDeflect();
                    _fx.Burst(blob.Center, Palette.Foam, Mathf.RoundToInt(8 * Visuals.Particles));
                    _audio.PlayCoralBreak(blob.Center, _field.HalfWidth);
                    Distance += _cfg.zoneMeterBonus;
                    return; // один удар — один шар
                }

                if (HitsBubble(blob.Center, blob.Radius))
                {
                    zone.Shatter(blob);
                    _fx.Burst(blob.Center, Palette.Foam, 18);
                    _audio.PlayCoralBreak(blob.Center, _field.HalfWidth);
                    DamageBubble(_bubble.Center);
                    return;
                }
            }
        }

        void ClearZone()
        {
            if (_activeZone == null) return;
            _zoneSpawner.Recycle(_activeZone);
            _activeZone = null;
            _background.ClearTheme();
        }

        // -------------------------------------------------- длинные кораллы

        void TickSpires(float dt, bool checkCollisions)
        {
            for (int i = _spires.Count - 1; i >= 0; i--)
            {
                // столкновение может закончить партию и почистить список прямо здесь
                if (i >= _spires.Count) continue;

                var spire = _spires[i];
                bool alive = spire.Tick(dt, _scrollSpeed, _field);

                if (alive && checkCollisions) CheckSpireCollisions(spire);

                if (!alive)
                {
                    int idx = _spires.IndexOf(spire);
                    if (idx >= 0) _spires.RemoveAt(idx);
                    _spireSpawner.Recycle(spire);
                }
            }
        }

        void CheckSpireCollisions(CoralSpire spire)
        {
            var segments = spire.Segments;

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (!segment.Alive) continue;

                if (_guardian.CanDeflect &&
                    Vector2.Distance(segment.Center, _guardian.Center) < segment.Radius + _guardian.Reach)
                {
                    spire.Shatter(segment);
                    RewardCoral(segment.Center);
                    GuardianImpact(segment.Center, segment.Radius, heavy: false, destroyed: true);
                    _guardian.NotifyDeflect();
                    _fx.Burst(segment.Center, Palette.CoralTip, Mathf.RoundToInt(7 * Visuals.Particles));
                    _audio.PlayCoralBreak(segment.Center, _field.HalfWidth);
                    Distance += _cfg.spireMeterBonus;
                    return; // одно звено за удар
                }

                if (HitsBubble(segment.Center, segment.Radius))
                {
                    spire.Shatter(segment);
                    _fx.Burst(segment.Center, Palette.CoralTip, 16);
                    _audio.PlayCoralBreak(segment.Center, _field.HalfWidth);
                    DamageBubble(_bubble.Center);
                    return;
                }
            }
        }

        void ClearSpires()
        {
            for (int i = 0; i < _spires.Count; i++) _spireSpawner.Recycle(_spires[i]);
            _spires.Clear();
        }

        // ------------------------------------------------- коралловые постройки

        void TickFormations(float dt, bool checkCollisions)
        {
            for (int i = _formations.Count - 1; i >= 0; i--)
            {
                // столкновение может закончить партию и почистить список прямо здесь
                if (i >= _formations.Count) continue;

                var f = _formations[i];
                bool alive = f.Tick(dt, _scrollSpeed, _field);

                if (alive && checkCollisions) CheckFormationCollisions(f);

                if (!alive)
                {
                    int idx = _formations.IndexOf(f);
                    if (idx >= 0) _formations.RemoveAt(idx);
                    _formationSpawner.Recycle(f);
                }
            }
        }

        void CheckFormationCollisions(CoralFormation formation)
        {
            var blocks = formation.Blocks;
            float guardianReach = formation.BlockRadius + _guardian.Reach;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (!block.Alive) continue;

                if (_guardian.CanDeflect && Vector2.Distance(block.Center, _guardian.Center) < guardianReach)
                {
                    formation.Shatter(block);
                    RewardCoral(block.Center);
                    GuardianImpact(block.Center, block.Size * 0.5f, heavy: false, destroyed: true);
                    _guardian.NotifyDeflect();
                    _fx.Burst(block.Center, Palette.CoralTip, Mathf.RoundToInt(7 * Visuals.Particles));
                    _audio.PlayCoralBreak(block.Center, _field.HalfWidth);
                    Distance += _cfg.formationMeterBonus;
                    return; // за один удар — один блок, дальше нужна перезарядка
                }

                if (HitsBubble(block.Center, formation.BlockRadius))
                {
                    formation.Shatter(block);
                    _fx.Burst(block.Center, Palette.CoralTip, 16);
                    _audio.PlayCoralBreak(block.Center, _field.HalfWidth);
                    DamageBubble(_bubble.Center);
                    return;
                }
            }
        }

        void ClearFormations()
        {
            for (int i = 0; i < _formations.Count; i++) _formationSpawner.Recycle(_formations[i]);
            _formations.Clear();
        }

        // ----------------------------------------------------- подъём: декор и след

        /// <summary>
        /// Декорации живут всегда, включая стартовый экран и экран поражения:
        /// мир должен ползти вниз непрерывно.
        /// </summary>
        /// <summary>Снять со сцены все декорации и крабов разом.</summary>
        void ClearScenery()
        {
            for (int i = _scenery.Count - 1; i >= 0; i--) _scenerySpawner.Recycle(_scenery[i]);
            _scenery.Clear();

            for (int i = _sideCrabs.Count - 1; i >= 0; i--) _scenerySpawner.RecycleCrab(_sideCrabs[i]);
            _sideCrabs.Clear();
        }

        void TickScenery(float dt)
        {
            for (int i = _scenery.Count - 1; i >= 0; i--)
            {
                var s = _scenery[i];
                if (!s.Tick(dt, _scrollSpeed, _field))
                {
                    _scenery.RemoveAt(i);
                    _scenerySpawner.Recycle(s);
                }
            }

            // На пустой сцене ничего нового не заводим — только доживают старые,
            // если они каким-то образом уцелели.
            if (!_emptyStage)
            {
                var spawned = _scenerySpawner.Tick(dt);
                if (spawned != null) _scenery.Add(spawned);
            }

            for (int i = _sideCrabs.Count - 1; i >= 0; i--)
            {
                var crab = _sideCrabs[i];
                if (!crab.Tick(dt, _scrollSpeed, _field))
                {
                    _sideCrabs.RemoveAt(i);
                    _scenerySpawner.RecycleCrab(crab);
                }
            }

            var crawler = _emptyStage ? null : _scenerySpawner.TickCrab(dt);
            if (crawler != null) _sideCrabs.Add(crawler);
        }

        /// <summary>
        /// Вернуть жизнь за пройденные метры.
        ///
        /// Проигрыш в этой игре складывается из трёх ошибок, и третья случается
        /// обычно сильно позже первой — а жизнь за первую так и не возвращается.
        /// Выдача за расстояние чинит именно это: игрок, который разобрался и
        /// плывёт дальше, постепенно отыгрывает запас назад.
        ///
        /// Не выдаём молча: пузырёк вспыхивает и звучит награда, иначе лишнюю
        /// жизнь замечают только в момент, когда она кончилась.
        /// </summary>
        void TickHealing()
        {
            float every = Difficulty.Current.lifeEveryMeters;
            if (every <= 0f || State != GameState.Playing) return;
            if (Distance < _healedAt + every) return;

            _healedAt = Distance;
            if (!_bubble.CanHeal) return;      // отметку всё равно сдвинули: копить нечего

            _bubble.AddLife(1);
            _ui.SetLives(_bubble.Lives);
            _fx.Burst(_bubble.Center, Palette.Aqua, 16);
            _audio.PlayReward();
            Haptics.Play(HapticPattern.Reward);
        }

        /// <summary>
        /// Звуковые подсказки, не привязанные к разовым событиям: веха каждые N метров,
        /// сердцебиение на последней жизни и редкое бульканье следа.
        /// </summary>
        void TickAmbientCues(float dt)
        {
            if (_cfg.milestoneEvery > 0f && Distance >= _nextMilestone)
            {
                _nextMilestone += _cfg.milestoneEvery;
                _audio.PlayMilestone();
            }

            TickHealing();

            if (_cfg.heartbeatInterval > 0f && _bubble.Lives == 1)
            {
                _heartbeatTimer -= dt;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = _cfg.heartbeatInterval;
                    _audio.PlayHeartbeat();
                }
            }
            else
            {
                _heartbeatTimer = 0f;
            }

            if (_cfg.bubbleBlipInterval > 0f)
            {
                _blipTimer -= dt;
                if (_blipTimer <= 0f)
                {
                    _blipTimer = _cfg.bubbleBlipInterval * Random.Range(0.6f, 1.6f);
                    _audio.PlayBubbleBlip();
                }
            }
        }

        /// <summary>
        /// Пузырьковый след. Он остаётся позади и уходит вниз со скоростью подъёма —
        /// самый прямой визуальный сигнал того, что движется именно пузырёк.
        /// </summary>
        void EmitRiseTrail(float dt)
        {
            if (_cfg.trailInterval <= 0f) return;

            _trailTimer -= dt;
            if (_trailTimer > 0f) return;
            _trailTimer = _cfg.trailInterval;

            Vector2 offset = Random.insideUnitCircle * (_bubble.Radius * 0.75f);
            Vector2 position = _bubble.Center + offset + Vector2.down * (_bubble.Radius * 0.4f);
            Vector2 velocity = new Vector2(Random.Range(-0.18f, 0.18f),
                                           -_scrollSpeed * Random.Range(0.75f, 1.05f));

            _fx.Emit(position, velocity, Palette.Foam, _cfg.trailLifetime,
                     Random.Range(0.05f, 0.11f), _cfg.trailAlpha, SortingOrders.BubbleTrail);
        }

        // ---------------------------------------------------------------- рыбы

        void SpawnFishIfDue(float dt)
        {
            if (State != GameState.Playing && State != GameState.BossFight) return;

            // Пока «Чернильный морок» держит экран залитым, новых рыб не выпускаем:
            // получить удар от того, чего физически не видно, — не сложность, а обман.
            // Уже плавающие рыбы остаются: они проступают в просветах и по мере того,
            // как чернила сходят.
            if (ReferenceEquals(_activeBoss, _inkBoss) && _inkBoss.InkCoverage > 0.35f) return;

            var fish = _fishSpawner.Tick(dt, Distance, State == GameState.BossFight,
                                         _bubble.Center, _scrollSpeed);
            if (fish != null) _fish.Add(fish);
        }

        void TickFish(float dt)
        {
            for (int i = _fish.Count - 1; i >= 0; i--)
            {
                // столкновение может закончить партию и почистить список прямо здесь
                if (i >= _fish.Count) continue;

                var f = _fish[i];
                bool alive = f.Tick(dt, _field, _bubble.Center);

                if (alive && !f.Dying)
                {
                    if (_guardian.CanDeflect &&
                        Vector2.Distance(f.Center, _guardian.Center) < f.Radius + _guardian.Reach)
                    {
                        Vector2 dir = f.Center - _guardian.Center;
                        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                        f.Kill(dir, _cfg.deflectImpulse);

                        _guardian.NotifyDeflect();
                        _audio.PlayDeflect(f.Center, _field.HalfWidth);
                        Distance += _cfg.deflectMeterBonus;
                        RewardsHub.Report(QuestGoal.Fish, 1);

                        // Рыба тоже угроза, и отбивают её тем же стражем: странно,
                        // когда камень горит, а медуза принимает огонь как должное.
                        GuardianImpact(f.Center, f.Radius, heavy: false);
                        Ignite(f.transform, f.Renderer);
                    }
                    else if (HitsBubble(f.Center, f.Radius))
                    {
                        Vector2 away = f.Center - _bubble.Center;
                        if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
                        f.Kill(away, _cfg.deflectImpulse * 0.7f);
                        DamageBubble(_bubble.Center);
                    }
                }

                if (!alive)
                {
                    int idx = _fish.IndexOf(f);
                    if (idx >= 0) _fish.RemoveAt(idx);
                    _fishSpawner.Recycle(f);
                }
            }
        }

        void ClearFish()
        {
            for (int i = 0; i < _fish.Count; i++) _fishSpawner.Recycle(_fish[i]);
            _fish.Clear();
        }

        /// <summary>Мягко убрать препятствия и постройки с экрана — при появлении босса.</summary>
        void SweepObstacles()
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                if (!_obstacles[i].Deflected)
                    _obstacles[i].Deflect(Vector2.down + Vector2.right * Random.Range(-0.4f, 0.4f), _cfg.deflectImpulse * 0.6f, 0.6f);
            }

            for (int i = 0; i < _formations.Count; i++)
            {
                _fx.Burst(_formations[i].transform.position, Palette.CoralTip, 14);
                _formations[i].ShatterAll();
            }

            for (int i = 0; i < _spires.Count; i++)
            {
                _fx.Burst(_spires[i].transform.position, Palette.CoralTip, 12);
                _spires[i].ShatterAll();
            }
        }

        // ---------------------------------------------------------------- босс

        /// <summary>Боссы выходят по очереди: «Страж глубин», затем «Змей бездны», и снова.</summary>
        void StartBossFight()
        {
            State = GameState.BossFight;
            SweepObstacles();

            _activeBoss = PickBoss();

            // Ряд боссов больше не переносится между партиями: он привязан к глубине,
            // и на 3000 м всегда третий, в какой бы заход туда ни дошли. Индекс в профиле
            // остаётся ради совместимости со старыми сохранениями, но расписание им
            // больше не управляется.
            _bossIndex = BossNumberAt(_nextBossAt) % _bosses.Length;
            _activeBoss.Spawn();

            _ui.ShowBossBar(true);
            _ui.SetBossHp(1f);
            _ui.SetBossName(_activeBoss.DisplayName);

            // Статья справочника открывается по встрече, а не по победе: она нужна
            // как раз тому, кто до босса дошёл и застрял.
            if (SkinStore.MarkBossMet(_activeBoss.Kind)) _yandex.MarkDirty();
            RewardsHub.Report(QuestGoal.BossMet, 1);

            if (_activeBoss.Kind == BossKind.Manta)
            {
                // Убираем декорации прямо сейчас: выход ската и так идёт под
                // затемнением, и подмены никто не замечает.
                _emptyStage = true;
                ClearScenery();

                // Финал объявляется иначе. Музыка обрывается совсем, и несколько секунд
                // слышен только его гул: тишина здесь работает сильнее любого звука,
                // который можно было бы сыграть. Тему включает уже сам босс, когда
                // доползёт до места, — см. OnMantaAct.
                _audio.StopMusic(0.5f);
                _audio.PlayMantaRoar();
                _ui.ShowBanner(_activeBoss.DisplayName + "\n" + Loc.Get("boss.manta.sub"), Palette.Coral);
            }
            else
            {
                _ui.ShowBanner(_activeBoss.DisplayName, Palette.Coral);
                _audio.PlayBossAppear();
                _audio.PlayBossMusic();
            }
        }

        float BossStep => Mathf.Max(50f, _cfg.bossEveryMeters);

        /// <summary>Ближайшая отметка боссов строго больше заданной: 1000, 2000, 3000...</summary>
        float NextBossMark(float from) => (Mathf.Floor(from / BossStep) + 1f) * BossStep;

        /// <summary>Порядковый номер боя на этой отметке: 1000 м — первый, 2000 м — второй.</summary>
        int BossNumberAt(float mark) => Mathf.Max(1, Mathf.RoundToInt(mark / BossStep));

        /// <summary>
        /// Кто выйдет сейчас. Босс определяется глубиной, а не счётчиком боёв: на каждой
        /// круглой отметке стоит свой, и игрок точно знает, кто его ждёт на 4000 м.
        ///
        /// Пройден весь ряд — он начинается заново, но тяжелее: круг задаётся в
        /// BossTuning и поднимает всем запас здоровья и стартовую ярость.
        ///
        /// «Великая тень» — финал ряда, и её ещё и запирает маска побед: если игрок
        /// каким-то образом добрался до отметки, не одолев остальных, вместо неё выходит
        /// первый босс круга.
        /// </summary>
        IBoss PickBoss()
        {
            int number = BossNumberAt(_nextBossAt);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Прыжок из дев-панели уважаем как есть: иначе ни финального босса, ни
            // второго круга нельзя было бы посмотреть, не пройдя всё заново.
            if (_forcedByDev)
            {
                _forcedByDev = false;
                BossTuning.SetLap(0);
                return _bosses[_bossIndex % _bosses.Length];
            }
#endif

            BossTuning.SetLap((number - 1) / _bosses.Length);

            var boss = _bosses[(number - 1) % _bosses.Length];
            if (boss.Kind == BossKind.Manta && !FinaleUnlocked()) boss = _bosses[0];
            return boss;
        }

        /// <summary>Побеждены ли все боссы, кроме самой «Великой тени».</summary>
        bool FinaleUnlocked()
        {
            for (int i = 0; i < _bosses.Length; i++)
            {
                if (_bosses[i].Kind == BossKind.Manta) continue;
                if (!SkinStore.IsBossDefeated(_bosses[i].Kind)) return false;
            }
            return true;
        }

        /// <summary>
        /// Звук от финального босса. Раньше на каждый его толчок шёл общий звук
        /// попадания по боссу — и разряд, и таран, и погасшая щель звучали одинаково.
        /// </summary>
        void OnMantaNoise(MantaBossController.Noise noise)
        {
            switch (noise)
            {
                case MantaBossController.Noise.Bolt: _audio.PlayMantaBolt(); break;
                case MantaBossController.Noise.Slam: _audio.PlayMantaSlam(); break;
                case MantaBossController.Noise.Gill: _audio.PlayMantaGill(); break;
                default: _audio.PlayMantaShock(); break;
            }
        }

        /// <summary>
        /// Смена акта финала. Первый акт включает тему — до него держалась тишина,
        /// каждый следующий отмечается рёвом: слышно, что стало хуже.
        /// </summary>
        void OnMantaAct(int act)
        {
            if (act == 0)
            {
                _audio.PlayFinaleMusic();
                return;
            }

            // Смену акта нужно объявлять словами. Она меняет и повадки зверя, и
            // способ его ранить, а по одной полосе HP этого не прочесть: без баннера
            // игрок просто не понимает, что правила только что стали другими.
            _audio.PlayMantaRoar();
            _ui.ShowBanner(Loc.Get(act == 1 ? "boss.manta.act2" : "boss.manta.act3"),
                           Palette.Coral, 2.0f);
        }

        void DespawnAllBosses()
        {
            if (_bosses == null) return;
            for (int i = 0; i < _bosses.Length; i++) _bosses[i]?.Despawn();
            _activeBoss = null;
        }

        /// <summary>Страж принял атаку на себя: отбил снаряд или встретил рывок змея.</summary>
        void OnBossAttackBlocked(Vector2 pos)
        {
            _fx.Burst(pos, Palette.Aqua, 12);
            _audio.PlayReflect();

            // Отбитый снаряд босса — такое же касание стража, как удар о камень,
            // и оболочка обязана быть и тут. Иначе в бою с боссом — там, где
            // трофейный скин и носят, — его подпись как раз и пропала бы.
            // Радиуса у снаряда нет в событии, и выдумывать его незачем:
            // полрадиуса стража — размер с точностью, которой хватает, чтобы
            // огонь растёкся по снаряду, а не мимо.
            if (_hasEnvelope)
                _envelope.Play(_envelopeDef, pos, pos, _guardian.Radius, _guardian.Radius * 0.5f);
        }

        void OnBossDamaged(Vector2 pos)
        {
            // Попадание по боссу — золото (D-16), Coral остаётся опасности: тот
            // же цвет у удара по боссу и урона игроку путал. Тон приглушённый,
            // как у подмеса тела: чистый Gold втрое ярче прежнего Coral, и
            // искры на тёмной воде давали лишние перепады яркости. [ТЕСТ]
            _fx.Burst(pos, HitTint.Tone, 16);
            _audio.PlayBossHit();

            if (_activeBoss == null) return;
            _ui.SetBossHp(_activeBoss.MaxHp > 0 ? _activeBoss.Hp / (float)_activeBoss.MaxHp : 0f);
            if (_activeBoss.Hp <= 0) _audio.PlayBossDie();
        }

        void OnBossDefeated(Vector2 pos)
        {
            // Финал ряда: Великая тень стоит десятой и приходит только после всех
            // остальных, поэтому её падение — это и есть «пройдено всё».
            bool finale = _activeBoss != null && _activeBoss.Kind == BossKind.Manta;

            _fx.Burst(pos, Palette.Gold, 44, 0.8f, 3.4f, 0.06f, 0.16f);
            _ui.ShowBossBar(false);
            if (!finale) _ui.ShowBanner(Loc.Get("banner.pathOpen"), Palette.Aqua);
            _audio.PlayWin();
            _audio.PlayCalmMusic();

            // Море снова обживается: декорации возвращаются сразу, чтобы пустота не
            // тянулась через весь перегон до следующего босса.
            if (_emptyStage)
            {
                _emptyStage = false;
                _scenerySpawner.ResetState();
                _scenerySpawner.Prefill(_scenery);
            }

            _nextBossAt = NextBossMark(Distance);
            // Побеждённый босс поднимает и потолок, и саму скорость — множителем, а не
            // слагаемым: на быстрых скоростях прибавка иначе перестаёт ощущаться.
            // Расти бесконечно потолку нельзя, иначе вернётся та же беда, ради которой
            // он и заводился, — предел задан maxScrollSpeedGrowth.
            float ceiling = _cfg.maxScrollSpeed * Difficulty.Current.speedCapScale
                            * Mathf.Max(1f, _cfg.maxScrollSpeedGrowth);
            _speedCap = Mathf.Min(ceiling, _speedCap * Mathf.Max(1f, _cfg.bossClearedSpeedFactor));
            _scrollSpeed = Mathf.Min(_speedCap, _scrollSpeed * Mathf.Max(1f, _cfg.bossClearedSpeedFactor));
            _spawner.SpawnInterval = Mathf.Max(_cfg.minSpawnInterval, _spawner.SpawnInterval - _cfg.bossClearedSpawnBonus);

            // Квесту и достижению важна каждая победа, а не только первая над
            // этим боссом, — поэтому отдельной строкой, до проверки трофея.
            if (_activeBoss != null) RewardsHub.Report(QuestGoal.BossBeaten, 1);

            // Трофей за босса: скины, которые нельзя добыть упорством. Отмечаем
            // до сброса ссылки — после неё уже не у кого спросить, кого победили.
            if (_activeBoss != null && SkinStore.MarkBossDefeated(_activeBoss.Kind))
            {
                // На финале баннер о скине не показываем: его тут же перекрыло бы
                // поздравление, а скин всё равно уже лежит в магазине.
                if (!finale) _ui.ShowBanner(Loc.Get("banner.newSkin"), Palette.Gold, 2.2f);
                _ui.SetBestDepth(_persistentBest);   // магазин пересчитает замки
                _yandex.MarkDirty();
            }

            // Поздравление показываем последним, чтобы его ничем не перебило.
            if (finale)
            {
                _ui.ShowBanner(Loc.Get("banner.finale"), Palette.Gold, 4.2f);
            }

            _activeBoss = null;
            State = GameState.Playing;
        }

        void OnBubbleHitByBoss(Vector2 pos)
        {
            DamageBubble(pos);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ------------------------------------------------------- дев-инструменты
        // Весь блок вырезается препроцессором из релизной сборки: в игре у Яндекса
        // этого кода физически нет, дотянуться до него неоткуда.

        public int BossCount => _bosses != null ? _bosses.Length : 0;
        public string BossName(int index) => _bosses[index % _bosses.Length].DisplayName.TrimEnd('!');
        public string ActiveBossName => _activeBoss != null ? _activeBoss.DisplayName.TrimEnd('!') : "—";
        public int ActiveBossHp => _activeBoss?.Hp ?? 0;
        public float ScrollSpeed => _scrollSpeed;
        public bool ZoneActive => _activeZone != null;
        /// <summary>Размеры краба из активной сборки — сразу видно, не устарел ли скомпилированный код.</summary>
        public string CrabSizeInfo
        {
            get
            {
                float column = _field.PlayHalfWidth * 2f;
                return $"краб {column * _cfg.crabShellSpan:0.0} / клешня {column * _cfg.crabPincerSpan:0.0}";
            }
        }

        /// <summary>Немедленно начать бой с конкретным боссом.</summary>
        public void DevStartBoss(int index)
        {
            if (State == GameState.Start || State == GameState.GameOver) StartRun();
            if (State == GameState.BossFight) DevKillBoss();

            ClearZone();
            DespawnAllBosses();
            _bossIndex = index;                 // StartBossFight возьмёт именно его
            _forcedByDev = true;                // финал иначе пропустился бы как закрытый
            _nextBossAt = Distance + 100000f;   // расписание не должно вмешаться
            StartBossFight();
        }

        /// <summary>Убить текущего босса так, будто игрок его победил.</summary>
        public void DevKillBoss()
        {
            if (State != GameState.BossFight || _activeBoss == null) return;
            var at = _bubble.Center + Vector2.up * 3f;
            _activeBoss.Despawn();
            OnBossDefeated(at);
        }

        /// <summary>Отмотать дистанцию до следующего босса по обычному расписанию.</summary>
        public void DevSkipToBoss()
        {
            if (State != GameState.Playing) return;
            Distance = Mathf.Max(Distance, _nextBossAt);
        }

        public void DevSpawnZoneNow()
        {
            if (State != GameState.Playing) return;
            ClearZone();
            _activeZone = _zoneSpawner.Spawn();
            if (_cfg.zoneShowBanner)
                _ui.ShowBanner(_activeZone.DisplayName, _activeZone.BannerColor, 2.2f);
        }

        public void DevSpawnFormation()
        {
            if (State != GameState.Playing) return;
            _formations.Add(_formationSpawner.DevSpawn());
        }

        public void DevSpawnSpire()
        {
            if (State != GameState.Playing) return;
            _spires.Add(_spireSpawner.DevSpawn());
        }

        /// <summary>
        /// Наложить статус выбранного скина на всё, что сейчас на экране.
        ///
        /// Нужна не для красоты: проверять горение, гоняясь за препятствиями
        /// стражем, — занятие на полчаса, а так эффект виден за одно нажатие.
        /// </summary>
        public void DevIgniteAll()
        {
            for (int i = 0; i < _obstacles.Count; i++)
                Ignite(_obstacles[i].transform, _obstacles[i].Renderer);

            for (int i = 0; i < _fish.Count; i++)
                Ignite(_fish[i].transform, _fish[i].Renderer);

            for (int i = 0; i < _formations.Count; i++)
            {
                var blocks = _formations[i].Blocks;
                for (int b = 0; b < blocks.Count; b++)
                    if (blocks[b].Alive) Ignite(blocks[b].Tr, blocks[b].Sr);
            }

            for (int i = 0; i < _spires.Count; i++)
            {
                var segments = _spires[i].Segments;
                for (int s = 0; s < segments.Count; s++)
                    if (segments[s].Alive) Ignite(segments[s].Tr, segments[s].Sr);
            }
        }

        public void DevSpawnObstacle()
        {
            if (State != GameState.Playing) return;
            _obstacles.Add(_spawner.DevSpawn(_scrollSpeed, Mathf.FloorToInt(Distance)));
        }

        public void DevSpawnFish()
        {
            if (State != GameState.Playing && State != GameState.BossFight) return;
            _fish.Add(_fishSpawner.DevSpawn(_bubble.Center));
        }

        public void DevSpawnJellyfish()
        {
            if (State != GameState.Playing) return;
            _fish.Add(_fishSpawner.DevSpawnJellyfish(_scrollSpeed));
        }

        public void DevSpawnSideCrab()
        {
            var crab = _scenerySpawner.DevSpawnCrab();
            if (crab != null) _sideCrabs.Add(crab);
        }

        public void DevAddDistance(float meters)
        {
            if (State != GameState.Playing) return;
            Distance += meters;
        }

        public void DevAddLife(int amount)
        {
            if (amount > 0) _bubble.AddLife(amount);
            else _bubble.TakeDamage();
            _ui.SetLives(_bubble.Lives);
            if (_bubble.Lives <= 0) GameOver();
        }

        public void DevClearField()
        {
            ClearObstacles();
            ClearFish();
            ClearFormations();
            ClearSpires();
            ClearZone();
        }

        /// <summary>
        /// Удар о препятствие понарошку: тот же <see cref="GuardianImpact"/>, что
        /// и в бою, с точкой касания справа от стража.
        ///
        /// Нужен затем, что иначе проверить оболочку значило бы доиграть до
        /// легендарного скина и попасть в камень — и так шестнадцать раз.
        /// </summary>
        public void DevGuardianCollision()
        {
            // Ближайшее настоящее препятствие, если оно есть: эффект надо
            // смотреть на том, по чему он и бьёт, а не в пустоте.
            float best = float.MaxValue;
            Vector2 at = Vector2.zero;
            float rad = 0f;

            // Только то, до чего страж реально дотянулся бы: дальняя цель даёт
            // связь через полэкрана, какой в бою не бывает.
            float maxReach = _guardian.Radius * 6f;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                float d = Vector2.Distance(_obstacles[i].Center, _guardian.Center);
                if (d >= best || d > maxReach) continue;
                best = d;
                at = _obstacles[i].Center;
                rad = _obstacles[i].Radius;
            }

            if (rad <= 0f)
            {
                float r = Mathf.Max(0.05f, _guardian.Radius);
                at = _guardian.Center + Vector2.right * (r + 0.3f);
                rad = 0.3f;
            }

            GuardianImpact(at, rad, heavy: false);
        }

        /// <summary>Зациклить оболочку выбранного скина, чтобы спокойно её настроить.</summary>
        public void DevLoopEnvelope(bool on)
        {
            _devEnvelopeLoop = on;
            if (!on) _envelope.Clear();
        }

        public bool DevEnvelopeLooping => _devEnvelopeLoop;
        bool _devEnvelopeLoop;

        /// <summary>Показать оболочку выбранного скина в витрине.</summary>
        public void DevShowShopEnvelope() => _ui.DebugPlayShopEnvelope();

        /// <summary>Как медуза помещается в кадр — дев-панели.</summary>
        public string DevJellyFit()
        {
            for (int i = 0; i < _bosses.Length; i++)
                if (_bosses[i] is JellyBossController j) return j.DebugFit();
            return "нет в сцене";
        }

        /// <summary>Состояние оболочки строкой — дев-панели.</summary>
        public string DevEnvelopeInfo()
        {
            if (!_hasEnvelope) return "нет у этого скина";

            return string.Format("{0}/{1}  R={2:0.0} D  {3:0.0} с  живых {4}  частиц {5}  x{6:0.00}",
                _envelopeDef.Kind, _envelopeDef.Link, _envelopeDef.RadiusD,
                Mathf.Max(0f, _envelope.DebugLife), _envelope.DebugLive,
                _envelope.DebugActive, _envelope.DebugIntensity);
        }
#endif

        // -------------------------------------------------------------- урон

        /// <summary>
        /// Задевает ли угроза пузырёк по-настоящему.
        ///
        /// Две поблажки сразу, и обе просили:
        ///
        /// 1. **Хитбоксы меньше рисунка.** И у пузырька, и у угрозы. Касание по
        ///    самому краю картинки перестаёт быть ударом. Уменьшаем именно проверку,
        ///    а не спрайты: игрок должен видеть угрозу такой, какая она есть, и
        ///    выигрывать на расхождении, а не на том, что всё стало мельче.
        ///
        /// 2. **Окно прощения.** Касание глубиной меньше того, что угроза проходит
        ///    за coyoteTime, — это скольжение по краю, а не удар. Настоящее
        ///    попадание на следующем кадре станет глубже и всё равно засчитается;
        ///    а угроза, прошедшая мимо по касательной, уже уйдёт.
        ///
        /// Проверка одна на все виды угроз: обломки, шары локаций, звенья шпилей,
        /// блоки построек и рыб. Пять отдельных проверок разошлись бы при первой же
        /// правке — ровно так и случилось со статусами препятствий.
        /// </summary>
        bool HitsBubble(Vector2 center, float radius)
        {
            float reach = radius * Difficulty.Current.hazardHitboxScale + _bubble.HitRadius;
            float distance = Vector2.Distance(center, _bubble.Center);
            if (distance >= reach) return false;

            float forgive = Difficulty.Current.coyoteTime * Mathf.Max(1f, _scrollSpeed) * 0.5f;
            return reach - distance >= forgive;
        }

        void DamageBubble(Vector2 pos)
        {
            if (!_bubble.TakeDamage()) return;

            // Урон — цвет опасности (D-16): Aqua в игре значит «добро», и всплеск
            // урона в нём читался как успех.
            _fx.Burst(pos, Palette.Coral, Mathf.RoundToInt(14 * Visuals.Particles));
            _audio.PlayHurt();
            Haptics.Play(HapticPattern.Hurt);
            _ui.SetLives(_bubble.Lives);

            if (_bubble.Lives <= 0) GameOver();
        }
    }
}
