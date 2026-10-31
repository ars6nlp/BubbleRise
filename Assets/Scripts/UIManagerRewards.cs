using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Экран наград: подарки, задания, колесо, достижения.
    ///
    /// **Почему отдельным файлом, а не внутри UIManager.** UIManager и без этого
    /// экрана больше двух тысяч строк, а здесь четыре независимые страницы. Класс
    /// тот же — иначе пришлось бы дублировать <c>NewRect</c>, <c>Anchor</c>,
    /// <c>NewButton</c> и шрифт, — но файл свой, как у SpriteFactory с его
    /// боссами и материалами. Это уже принятый в проекте способ делить большой
    /// класс.
    ///
    /// **Четыре страницы в одном экране, а не четыре экрана.** Рамка, шапка,
    /// кошелёк и «Назад» у них общие, и переключение целыми экранами читалось бы
    /// как мигание кадра — ровно поэтому так же сделан справочник.
    ///
    /// **Что обновляется когда.** Виджеты создаются один раз, при сборке
    /// интерфейса. Дальше их только наполняют: <see cref="RefreshRewards"/> на
    /// открытии экрана и по событию <see cref="RewardsHub.Changed"/>,
    /// <see cref="TickRewards"/> — то, что живёт во времени: обратный отсчёт
    /// сундука, дрожь готового сундука, вращение колеса, всплытие награды.
    /// Создавать карточки заново на каждое изменение значило бы терять позицию
    /// прокрутки в списке заданий ровно в тот момент, когда игрок нажал «Забрать».
    /// </summary>
    public partial class UIManager
    {
        // ---------------------------------------------------------- состояние

        GameObject _rewardsScreen;
        int _rewardsTab;                        // 0 подарки, 1 задания, 2 колесо, 3 достижения
        readonly GameObject[] _rewardPages = new GameObject[RewardsLayout.TabCount];
        readonly Image[] _rewardTabImages = new Image[RewardsLayout.TabCount];
        readonly Text[] _rewardTabTexts = new Text[RewardsLayout.TabCount];
        Text _rewardsPurse;

        // кнопка в меню и значок на ней
        RectTransform _rewardsBadge;
        Text _rewardsBadgeText;
        float _badgePulse;

        // --- подарки
        Image _chestImage;
        RectTransform _chestHolder;
        Text _chestStatus;
        Text _chestStored;
        ButtonParts _chestButton;
        float _chestShake;
        float _chestOpenTimer;

        CalendarCell[] _calendarCells;
        Text _streakText;
        Image _streakFlame;
        ButtonParts _dailyButton;
        Text _dailyNote;

        // --- задания
        QuestCard[] _dailyCards;
        QuestCard[] _weeklyCards;
        ButtonParts _sweepButton;
        Text _sweepLabel;
        Text _questResetText;

        // --- колесо
        RectTransform _wheelFace;
        RectTransform _wheelOrbit;      // держатель указателя в центре диска, его и вращаем
        RectTransform _wheelPointer;
        Image _wheelPointerImage;
        Text[] _wheelLabels;
        Text _wheelSpinsText;
        Text _wheelResultText;
        ButtonParts _wheelButton;
        float _wheelAngle;              // угол указателя по кругу, градусы; 0 — сверху
        float _wheelSpinTime;           // сколько длится вращение
        float _wheelSpinLeft;           // сколько осталось
        float _wheelFrom, _wheelTo;     // откуда и куда идёт указатель
        bool _wheelInstant;             // «Без вспышек»: указатель не бежит, а проявляется на месте
        int _wheelPending = -1;         // выпавший сектор, пока крутится
        RewardGrant _wheelGrant;

        // --- достижения
        AchievementCard[] _achCards;

        // --- всплытие награды
        RectTransform _rewardPop;
        Image _rewardPopIcon;
        Image _rewardPopSparkle;
        Text _rewardPopText;
        float _rewardPopTimer;

        // --- окно «С возвращением»
        GameObject _welcomeBack;
        Text _welcomeBackDays;
        bool _welcomeBackShown;

        struct CalendarCell
        {
            public RectTransform Rt;
            public Image Background;
            public Image Icon;
            public Image Check;
            public Text Day;
            public Text Amount;
        }

        struct QuestCard
        {
            public RectTransform Rt;
            public Image Background;
            public Text Title;
            public Image BarBack;
            public Image BarFill;
            public Text Progress;
            public Text Reward;
            public ButtonParts Action;
        }

        struct AchievementCard
        {
            public RectTransform Rt;
            public Image Background;
            public Image Medal;
            public Text Title;
            public Text Tier;
            public Image BarBack;
            public Image BarFill;
            public Text Progress;
            public Text Reward;
            public ButtonParts Action;
        }

        // ------------------------------------------------------------- сборка

        /// <summary>Кнопка «Награды» в меню со значком непрочитанного.</summary>
        void BuildRewardsMenuButton(RectTransform content)
        {
            var rewards = NewButton("RewardsButton", content, Loc.Get("hub.button"), Palette.Aqua, Palette.Deep);
            Anchor(rewards.rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(156f, 76f));
            rewards.text.fontSize = 22;
            rewards.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); ShowRewards(); });

            // Значок висит на углу кнопки и показывает, сколько всего можно забрать.
            // Число, а не точка: «есть что-то» и «ждут четыре награды» — разные
            // поводы зайти, и смысла скрывать разницу нет.
            _rewardsBadge = NewRect("Badge", rewards.rt);
            _rewardsBadge.anchorMin = _rewardsBadge.anchorMax = new Vector2(1f, 1f);
            _rewardsBadge.pivot = new Vector2(0.5f, 0.5f);
            _rewardsBadge.anchoredPosition = new Vector2(-6f, -4f);
            _rewardsBadge.sizeDelta = new Vector2(34f, 34f);

            var badgeImage = _rewardsBadge.gameObject.AddComponent<Image>();
            badgeImage.sprite = SpriteFactory.Disc();
            badgeImage.color = Palette.Coral;
            badgeImage.raycastTarget = false;

            _rewardsBadgeText = NewText("Count", _rewardsBadge, "", 20, Palette.Foam, TextAnchor.MiddleCenter);
            Stretch(_rewardsBadgeText.rectTransform);
            _rewardsBadgeText.fontStyle = FontStyle.Bold;

            _rewardsBadge.gameObject.SetActive(false);
        }

        void BuildRewardsScreen()
        {
            _rewardsScreen = BuildScreenRoot("RewardsScreen");

            var content = NewRect("Content", _rewardsScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(RewardsLayout.ContentWidth, RewardsLayout.ContentHeight);

            BuildRewardsHeader(content);
            BuildRewardsTabs(content);

            // Страницы живут в одном окне и переключаются включением объектов.
            for (int i = 0; i < RewardsLayout.TabCount; i++)
            {
                var page = NewRect("Page" + i, content);
                Anchor(page, new Vector2(0.5f, 1f), new Vector2(0f, RewardsLayout.PageY),
                       new Vector2(RewardsLayout.ContentWidth, RewardsLayout.PageH));
                _rewardPages[i] = page.gameObject;
            }

            BuildGiftsPage((RectTransform)_rewardPages[0].transform);
            BuildQuestsPage((RectTransform)_rewardPages[1].transform);
            BuildWheelPage((RectTransform)_rewardPages[2].transform);
            BuildAchievementsPage((RectTransform)_rewardPages[3].transform);

            AddBackButton(content);
            BuildRewardPopup(content);
            BuildWelcomeBackWindow();

            // Страницы создаются включёнными, и без этого все четыре легли бы друг
            // на друга в первом же кадре после открытия экрана.
            SetRewardsTab(_rewardsTab);

            _rewardsScreen.SetActive(false);
        }

        void BuildRewardsHeader(RectTransform content)
        {
            var title = NewText("Title", content, Loc.Get("hub.title"), 40, Palette.Aqua, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(-130f, RewardsLayout.HeaderY), new Vector2(220f, RewardsLayout.HeaderH));
            title.fontStyle = FontStyle.Bold;

            // Кошелёк устроен ровно как в магазине: значок и число в одной плашке.
            // Награды тратятся там же, где зарабатываются, и выглядеть баланс должен
            // одинаково в обоих местах.
            var purse = NewRect("Purse", content);
            Anchor(purse, new Vector2(0.5f, 1f),
                   new Vector2(146f, RewardsLayout.HeaderY), new Vector2(180f, RewardsLayout.HeaderH));
            var purseBg = purse.gameObject.AddComponent<Image>();
            purseBg.sprite = SpriteFactory.RoundedRect("btn", 0.4f);
            purseBg.type = Image.Type.Sliced;
            purseBg.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.22f);
            purseBg.raycastTarget = false;

            var purseIcon = NewRect("Icon", purse);
            purseIcon.anchorMin = purseIcon.anchorMax = new Vector2(0f, 0.5f);
            purseIcon.pivot = new Vector2(0f, 0.5f);
            purseIcon.anchoredPosition = new Vector2(12f, 0f);
            purseIcon.sizeDelta = new Vector2(26f, 26f);
            var purseImg = purseIcon.gameObject.AddComponent<Image>();
            purseImg.sprite = SpriteFactory.Coin();
            purseImg.raycastTarget = false;

            _rewardsPurse = NewText("Balance", purse, "0", 22, Palette.Gold, TextAnchor.MiddleLeft);
            _rewardsPurse.rectTransform.anchorMin = _rewardsPurse.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _rewardsPurse.rectTransform.pivot = new Vector2(0f, 0.5f);
            _rewardsPurse.rectTransform.anchoredPosition = new Vector2(46f, 0f);
            _rewardsPurse.rectTransform.sizeDelta = new Vector2(124f, 30f);
            _rewardsPurse.fontStyle = FontStyle.Bold;
        }

        void BuildRewardsTabs(RectTransform content)
        {
            string[] keys = { "hub.tabGifts", "hub.tabQuests", "hub.tabWheel", "hub.tabAch" };
            float left = -(RewardsLayout.TabCount - 1) * 0.5f * RewardsLayout.TabStep;

            for (int i = 0; i < RewardsLayout.TabCount; i++)
            {
                int index = i;
                var tab = NewButton("Tab" + i, content, Loc.Get(keys[i]), Palette.Aqua, Palette.Deep);
                Anchor(tab.rt, new Vector2(0.5f, 1f),
                       new Vector2(left + i * RewardsLayout.TabStep, RewardsLayout.TabsY),
                       new Vector2(RewardsLayout.TabW, RewardsLayout.TabsH));
                tab.text.fontSize = 17;
                tab.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); SetRewardsTab(index); });
                _rewardTabImages[i] = tab.image;
                _rewardTabTexts[i] = tab.text;
            }
        }

        // ------------------------------------------------------ страница: подарки

        void BuildGiftsPage(RectTransform page)
        {
            BuildChestPanel(page);
            BuildCalendarPanel(page);
        }

        void BuildChestPanel(RectTransform page)
        {
            var panel = Panel("ChestPanel", page, RewardsLayout.ChestPanelY, RewardsLayout.ChestPanelH);

            var title = NewText("Title", panel, Loc.Get("chest.title"), 26, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(440f, 30f));
            title.fontStyle = FontStyle.Bold;

            // Сундук слева, состояние справа: читается как «вот вещь, вот что с ней».
            _chestHolder = NewRect("ChestHolder", panel);
            Anchor(_chestHolder, new Vector2(0f, 1f), new Vector2(92f, -48f), new Vector2(124f, 110f));
            // Сундук занимает 30…154 от левого края — ровно до начала правой колонки.

            _chestImage = _chestHolder.gameObject.AddComponent<Image>();
            _chestImage.sprite = SpriteFactory.Chest(false);
            _chestImage.preserveAspect = true;
            _chestImage.raycastTarget = false;

            // Правая колонка: pivot сдвинут к левому краю, поэтому 166 — это начало
            // текста, а не его середина. С pivot по умолчанию блок шириной 290
            // начинался бы с 21 и лёг прямо на сундук.
            _chestStatus = NewText("Status", panel, "", 22, Palette.Foam, TextAnchor.MiddleLeft);
            Anchor(_chestStatus.rectTransform, new Vector2(0f, 1f), new Vector2(166f, -58f), new Vector2(290f, 30f));
            _chestStatus.rectTransform.pivot = new Vector2(0f, 1f);
            _chestStatus.fontStyle = FontStyle.Bold;

            _chestStored = NewText("Stored", panel, "", 18, Palette.Gold.WithAlpha(0.9f), TextAnchor.MiddleLeft);
            Anchor(_chestStored.rectTransform, new Vector2(0f, 1f), new Vector2(166f, -92f), new Vector2(290f, 26f));
            _chestStored.rectTransform.pivot = new Vector2(0f, 1f);

            var hint = NewText("Hint", panel, Loc.Get("chest.hint"), 15,
                               Palette.Foam.WithAlpha(0.55f), TextAnchor.UpperLeft);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(166f, -120f), new Vector2(290f, 44f));
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            _chestButton = NewButton("ChestOpen", panel, Loc.Get("chest.open"), Palette.Gold, Palette.Deep);
            Anchor(_chestButton.rt, new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(300f, 56f));
            _chestButton.text.fontSize = 26;
            _chestButton.button.onClick.AddListener(OnChestClicked);
        }

        void BuildCalendarPanel(RectTransform page)
        {
            var panel = Panel("CalendarPanel", page, RewardsLayout.CalendarPanelY, RewardsLayout.CalendarPanelH);

            var title = NewText("Title", panel, Loc.Get("daily.title"), 26, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.CalendarTitleY),
                   new Vector2(440f, RewardsLayout.CalendarTitleH));
            title.fontStyle = FontStyle.Bold;

            int days = RewardTables.DailyCalendar.Length;
            _calendarCells = new CalendarCell[days];

            for (int i = 0; i < days; i++)
            {
                bool topRow = i < RewardsLayout.CellsTopRow;
                int inRow = topRow ? i : i - RewardsLayout.CellsTopRow;
                int rowCount = topRow ? RewardsLayout.CellsTopRow : days - RewardsLayout.CellsTopRow;

                float left = -(rowCount - 1) * 0.5f * RewardsLayout.CellStepX;
                float y = topRow ? RewardsLayout.CalendarRow1Y : RewardsLayout.CalendarRow2Y;

                _calendarCells[i] = BuildCalendarCell(panel, i,
                    new Vector2(left + inRow * RewardsLayout.CellStepX, y));
            }

            _streakFlame = NewRect("Flame", panel).gameObject.AddComponent<Image>();
            Anchor(_streakFlame.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(-96f, RewardsLayout.CalendarStreakY), new Vector2(26f, 30f));
            _streakFlame.sprite = SpriteFactory.StreakFlame();
            _streakFlame.preserveAspect = true;
            _streakFlame.raycastTarget = false;

            _streakText = NewText("Streak", panel, "", 20, Palette.Gold, TextAnchor.MiddleLeft);
            Anchor(_streakText.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(44f, RewardsLayout.CalendarStreakY),
                   new Vector2(240f, RewardsLayout.CalendarStreakH));
            _streakText.fontStyle = FontStyle.Bold;

            _dailyButton = NewButton("DailyClaim", panel, Loc.Get("daily.claim"), Palette.Aqua, Palette.Deep);
            Anchor(_dailyButton.rt, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.CalendarClaimY),
                   new Vector2(300f, RewardsLayout.CalendarClaimH));
            _dailyButton.text.fontSize = 28;
            _dailyButton.button.onClick.AddListener(OnDailyClicked);

            _dailyNote = NewText("Note", panel, "", 15, Palette.Aqua.WithAlpha(0.8f), TextAnchor.MiddleCenter);
            Anchor(_dailyNote.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.CalendarClaimY - RewardsLayout.CalendarClaimH - 2f),
                   new Vector2(460f, 26f));
        }

        CalendarCell BuildCalendarCell(RectTransform parent, int day, Vector2 position)
        {
            var rt = NewRect("Day" + day, parent);
            Anchor(rt, new Vector2(0.5f, 1f), position, new Vector2(RewardsLayout.CellW, RewardsLayout.CellH));

            var background = rt.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect("btn", 0.28f);
            background.type = Image.Type.Sliced;
            background.color = Palette.Deep;
            background.raycastTarget = false;

            var dayText = NewText("Day", rt, Loc.Get("daily.day") + (day + 1), 14,
                                  Palette.Foam.WithAlpha(0.75f), TextAnchor.MiddleCenter);
            Anchor(dayText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f),
                   new Vector2(RewardsLayout.CellW, 20f));
            dayText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var iconRect = NewRect("Icon", rt);
            Anchor(iconRect, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(50f, 50f));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var amount = NewText("Amount", rt, "", 18, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(amount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f),
                   new Vector2(RewardsLayout.CellW, 24f));
            amount.fontStyle = FontStyle.Bold;

            // Галочка поверх всей клетки: забранный день должен быть виден сразу,
            // не вчитываясь в цифры.
            var checkRect = NewRect("Check", rt);
            Anchor(checkRect, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(46f, 46f));
            var check = checkRect.gameObject.AddComponent<Image>();
            check.sprite = SpriteFactory.CheckMark();
            check.color = Palette.Aqua;
            check.raycastTarget = false;
            check.gameObject.SetActive(false);

            return new CalendarCell
            {
                Rt = rt, Background = background, Icon = icon,
                Check = check, Day = dayText, Amount = amount,
            };
        }

        // ------------------------------------------------------ страница: задания

        void BuildQuestsPage(RectTransform page)
        {
            var list = ScrollList("QuestList", page, out RectTransform viewport);

            float y = 0f;

            y = SectionLabel(list, Loc.Get("quest.daily"), y);

            _dailyCards = new QuestCard[3];
            for (int i = 0; i < _dailyCards.Length; i++)
            {
                _dailyCards[i] = BuildQuestCard(list, i, false, y);
                y -= RewardsLayout.QuestCardStep;
            }

            // Бонус за все три — отдельной полосой под дневными: он и логически
            // стоит после них.
            var sweep = NewRect("Sweep", list);
            Anchor(sweep, new Vector2(0.5f, 1f), new Vector2(0f, y),
                   new Vector2(RewardsLayout.QuestCardW, 64f));
            var sweepBg = sweep.gameObject.AddComponent<Image>();
            sweepBg.sprite = SpriteFactory.RoundedRect("btn", 0.3f);
            sweepBg.type = Image.Type.Sliced;
            sweepBg.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.16f);
            sweepBg.raycastTarget = false;

            _sweepLabel = NewText("Label", sweep, "", 19, Palette.Gold, TextAnchor.MiddleLeft);
            Anchor(_sweepLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(300f, 30f));
            // 16…316 от левого края; кнопка справа начинается на 334.
            _sweepLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            _sweepLabel.fontStyle = FontStyle.Bold;

            _sweepButton = NewButton("SweepClaim", sweep, Loc.Get("quest.claim"), Palette.Gold, Palette.Deep);
            _sweepButton.rt.anchorMin = _sweepButton.rt.anchorMax = new Vector2(1f, 0.5f);
            _sweepButton.rt.pivot = new Vector2(1f, 0.5f);
            _sweepButton.rt.anchoredPosition = new Vector2(-12f, 0f);
            _sweepButton.rt.sizeDelta = new Vector2(130f, 44f);
            _sweepButton.text.fontSize = 19;
            _sweepButton.button.onClick.AddListener(OnSweepClicked);

            y -= 76f;

            y = SectionLabel(list, Loc.Get("quest.weekly"), y);

            _weeklyCards = new QuestCard[RewardTables.WeeklyQuests.Length];
            for (int i = 0; i < _weeklyCards.Length; i++)
            {
                _weeklyCards[i] = BuildQuestCard(list, i, true, y);
                y -= RewardsLayout.QuestCardStep;
            }

            _questResetText = NewText("Reset", list, "", 16, Palette.Foam.WithAlpha(0.55f), TextAnchor.MiddleCenter);
            Anchor(_questResetText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 8f),
                   new Vector2(RewardsLayout.QuestCardW, 28f));
            y -= 44f;

            // Высота содержимого — ровно по последней карточке: иначе список либо
            // не доскроливается до низа, либо тянется в пустоту.
            list.sizeDelta = new Vector2(0f, Mathf.Max(RewardsLayout.PageH, -y));
        }

        QuestCard BuildQuestCard(RectTransform parent, int slot, bool weekly, float y)
        {
            int index = slot;
            bool isWeekly = weekly;

            var card = NewRect((weekly ? "Weekly" : "Daily") + slot, parent);
            Anchor(card, new Vector2(0.5f, 1f), new Vector2(0f, y),
                   new Vector2(RewardsLayout.QuestCardW, RewardsLayout.QuestCardH));

            var background = card.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect("btn", 0.28f);
            background.type = Image.Type.Sliced;
            background.color = Palette.Deep2;
            background.raycastTarget = false;

            var title = NewText("Title", card, "", 19, Palette.Foam, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(330f, 28f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Награда — у правого верхнего угла, со значком монеты.
            var coinRect = NewRect("Coin", card);
            Anchor(coinRect, new Vector2(1f, 1f), new Vector2(-96f, -12f), new Vector2(20f, 20f));
            coinRect.pivot = new Vector2(1f, 1f);
            var coin = coinRect.gameObject.AddComponent<Image>();
            coin.sprite = SpriteFactory.Coin();
            coin.raycastTarget = false;

            var reward = NewText("Reward", card, "", 19, Palette.Gold, TextAnchor.MiddleRight);
            Anchor(reward.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -10f), new Vector2(72f, 24f));
            reward.rectTransform.pivot = new Vector2(1f, 1f);
            reward.fontStyle = FontStyle.Bold;

            var barBack = Bar(card, new Vector2(0f, 1f),
                              new Vector2(RewardsLayout.QuestTextLeft, -48f),
                              RewardsLayout.QuestBarW, out Image barFill);

            var progress = NewText("Progress", card, "", 15, Palette.Foam.WithAlpha(0.8f), TextAnchor.MiddleLeft);
            Anchor(progress.rectTransform, new Vector2(0f, 1f),
                   new Vector2(RewardsLayout.QuestTextLeft, -66f),
                   new Vector2(RewardsLayout.QuestBarW, 24f));
            progress.rectTransform.pivot = new Vector2(0f, 1f);

            var action = NewButton("Action", card, "", Palette.Aqua, Palette.Deep);
            action.rt.anchorMin = action.rt.anchorMax = new Vector2(1f, 0f);
            action.rt.pivot = new Vector2(1f, 0f);
            action.rt.anchoredPosition = new Vector2(-RewardsLayout.ActionMargin, 14f);
            action.rt.sizeDelta = new Vector2(RewardsLayout.ActionW, 44f);
            action.text.fontSize = 18;
            action.button.onClick.AddListener(() => OnQuestClicked(isWeekly, index));

            return new QuestCard
            {
                Rt = card, Background = background, Title = title,
                BarBack = barBack, BarFill = barFill, Progress = progress,
                Reward = reward, Action = action,
            };
        }

        // ------------------------------------------------------- страница: колесо

        void BuildWheelPage(RectTransform page)
        {
            _wheelFace = NewRect("Wheel", page);
            Anchor(_wheelFace, new Vector2(0.5f, 1f), new Vector2(0f, RewardsLayout.WheelY),
                   new Vector2(RewardsLayout.WheelSize, RewardsLayout.WheelSize));
            // Опорная точка — центр диска, а не верх обода (H-01). Anchor ставит
            // её на верхний край, и всё, что вращалось вокруг неё, носилось по
            // верхней половине экрана поверх заголовка и вкладок. Верх диска при
            // этом остаётся там же, где был.
            _wheelFace.pivot = new Vector2(0.5f, 0.5f);
            _wheelFace.anchoredPosition = new Vector2(0f, RewardsLayout.WheelY - RewardsLayout.WheelSize * 0.5f);
            var face = _wheelFace.gameObject.AddComponent<Image>();
            face.sprite = SpriteFactory.WheelFace();
            face.preserveAspect = true;
            face.raycastTarget = false;

            // Диск неподвижен, поэтому подписи стоят ровно и читаются всегда: и до
            // вращения, и во время, и после. Под числом — шанс сектора (G-03):
            // сектора одинаковые по 60°, а «60» выпадает в одном случае из ста, и
            // без подписи круг обещал бы одну шестую.
            int sectors = RewardTables.WheelSectors.Length;
            _wheelLabels = new Text[sectors];
            float totalWeight = 0f;
            for (int i = 0; i < sectors; i++) totalWeight += RewardTables.WheelSectors[i].Weight;
            for (int i = 0; i < sectors; i++)
            {
                // Центр сектора i: отсчёт от направления «вправо», как в спрайте.
                float angle = (i + 0.5f) / sectors * Mathf.PI * 2f - Mathf.PI;
                var holder = NewRect("Sector" + i, _wheelFace);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                                          * RewardsLayout.WheelSize * 0.31f;
                holder.sizeDelta = new Vector2(84f, 58f);

                var label = NewText("Value", holder, RewardTables.WheelSectors[i].Coins.ToString(),
                                    26, Palette.Gold, TextAnchor.MiddleCenter);
                label.rectTransform.anchorMin = new Vector2(0f, 0.38f);
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.fontStyle = FontStyle.Bold;
                _wheelLabels[i] = label;

                float chance = totalWeight > 0f ? RewardTables.WheelSectors[i].Weight / totalWeight * 100f : 0f;
                var odds = NewText("Chance", holder, Mathf.RoundToInt(chance) + "%",
                                   16, Palette.Foam.WithAlpha(0.8f), TextAnchor.MiddleCenter);
                odds.rectTransform.anchorMin = Vector2.zero;
                odds.rectTransform.anchorMax = new Vector2(1f, 0.4f);
                odds.rectTransform.offsetMin = odds.rectTransform.offsetMax = Vector2.zero;
            }

            // Указатель ходит по кругу у обода, а диск стоит (E-04). Раньше
            // крутился диск: секторы двух яркостей на 4–6 оборотах давали до 10
            // перепадов в секунду на каждой точке колеса — строб. Держатель стоит
            // в центре диска, указатель — у верхнего края держателя и смотрит
            // внутрь; поворачиваем держатель.
            var orbit = NewRect("PointerOrbit", page);
            _wheelOrbit = orbit;
            orbit.anchorMin = orbit.anchorMax = new Vector2(0.5f, 1f);
            orbit.pivot = new Vector2(0.5f, 0.5f);
            orbit.anchoredPosition = _wheelFace.anchoredPosition;
            orbit.sizeDelta = Vector2.zero;

            var pointer = NewRect("Pointer", orbit);
            _wheelPointer = pointer;
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.pivot = new Vector2(0.5f, 0.5f);
            pointer.anchoredPosition = new Vector2(0f, RewardsLayout.WheelSize * 0.5f - 7f);
            pointer.sizeDelta = new Vector2(40f, 54f);
            var pointerImage = pointer.gameObject.AddComponent<Image>();
            pointerImage.sprite = SpriteFactory.WheelPointer();
            pointerImage.preserveAspect = true;
            pointerImage.raycastTarget = false;
            _wheelPointerImage = pointerImage;

            _wheelSpinsText = NewText("Spins", page, "", 22, Palette.Foam, TextAnchor.MiddleCenter);
            Anchor(_wheelSpinsText.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.WheelSpinsY),
                   new Vector2(460f, RewardsLayout.WheelSpinsH));
            _wheelSpinsText.fontStyle = FontStyle.Bold;

            _wheelButton = NewButton("Spin", page, Loc.Get("wheel.spin"), Palette.Gold, Palette.Deep);
            Anchor(_wheelButton.rt, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.WheelButtonY),
                   new Vector2(300f, RewardsLayout.WheelButtonH));
            _wheelButton.text.fontSize = 32;
            _wheelButton.button.onClick.AddListener(OnSpinClicked);

            _wheelResultText = NewText("Result", page, "", 22, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_wheelResultText.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.WheelResultY),
                   new Vector2(460f, RewardsLayout.WheelResultH));
            _wheelResultText.fontStyle = FontStyle.Bold;

            // Подсказка была 17 кеглем на 55 % прозрачности — терялась совсем.
            // Поднята до 21 и до 75 %, ширина сужена, чтобы строка переносилась по
            // смыслу, а не упиралась в края.
            var hint = NewText("Hint", page, Loc.Get("wheel.hint"), 21,
                               Palette.Foam.WithAlpha(0.75f), TextAnchor.UpperCenter);
            Anchor(hint.rectTransform, new Vector2(0.5f, 1f),
                   new Vector2(0f, RewardsLayout.WheelHintY),
                   new Vector2(380f, RewardsLayout.WheelHintH));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // --------------------------------------------------- страница: достижения

        void BuildAchievementsPage(RectTransform page)
        {
            var list = ScrollList("AchList", page, out RectTransform viewport);

            _achCards = new AchievementCard[RewardTables.Achievements.Length];
            float y = 0f;
            for (int i = 0; i < _achCards.Length; i++)
            {
                _achCards[i] = BuildAchievementCard(list, i, y);
                y -= RewardsLayout.AchCardStep;
            }

            list.sizeDelta = new Vector2(0f, Mathf.Max(RewardsLayout.PageH, -y + 10f));
        }

        AchievementCard BuildAchievementCard(RectTransform parent, int slot, float y)
        {
            int index = slot;

            var card = NewRect("Ach" + slot, parent);
            Anchor(card, new Vector2(0.5f, 1f), new Vector2(0f, y),
                   new Vector2(RewardsLayout.AchCardW, RewardsLayout.AchCardH));

            var background = card.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect("btn", 0.28f);
            background.type = Image.Type.Sliced;
            background.color = Palette.Deep2;
            background.raycastTarget = false;

            var medalRect = NewRect("Medal", card);
            Anchor(medalRect, new Vector2(0f, 1f), new Vector2(52f, -14f), new Vector2(72f, 72f));
            var medal = medalRect.gameObject.AddComponent<Image>();
            medal.preserveAspect = true;
            medal.raycastTarget = false;

            var title = NewText("Title", card, "", 20, Palette.Foam, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(98f, -10f), new Vector2(250f, 26f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.fontStyle = FontStyle.Bold;

            var tier = NewText("Tier", card, "", 15, Palette.Aqua.WithAlpha(0.85f), TextAnchor.MiddleLeft);
            Anchor(tier.rectTransform, new Vector2(0f, 1f), new Vector2(98f, -36f), new Vector2(250f, 22f));
            tier.rectTransform.pivot = new Vector2(0f, 1f);

            var coinRect = NewRect("Coin", card);
            Anchor(coinRect, new Vector2(1f, 1f), new Vector2(-96f, -12f), new Vector2(20f, 20f));
            coinRect.pivot = new Vector2(1f, 1f);
            var coin = coinRect.gameObject.AddComponent<Image>();
            coin.sprite = SpriteFactory.Coin();
            coin.raycastTarget = false;

            var reward = NewText("Reward", card, "", 19, Palette.Gold, TextAnchor.MiddleRight);
            Anchor(reward.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -10f), new Vector2(72f, 24f));
            reward.rectTransform.pivot = new Vector2(1f, 1f);
            reward.fontStyle = FontStyle.Bold;

            // Полоса короче, чем кажется нужным: кнопка «Забрать» прижата к правому
            // краю, и длинная полоса уходила бы под неё. Число — в RewardsLayout,
            // там же это и проверяется.
            var barBack = Bar(card, new Vector2(0f, 1f),
                              new Vector2(RewardsLayout.AchTextLeft, -62f),
                              RewardsLayout.AchBarW, out Image barFill);

            var progress = NewText("Progress", card, "", 15, Palette.Foam.WithAlpha(0.8f), TextAnchor.MiddleLeft);
            Anchor(progress.rectTransform, new Vector2(0f, 1f),
                   new Vector2(RewardsLayout.AchTextLeft, -80f),
                   new Vector2(RewardsLayout.AchBarW, 24f));
            progress.rectTransform.pivot = new Vector2(0f, 1f);

            var action = NewButton("Action", card, Loc.Get("ach.claim"), Palette.Aqua, Palette.Deep);
            action.rt.anchorMin = action.rt.anchorMax = new Vector2(1f, 0f);
            action.rt.pivot = new Vector2(1f, 0f);
            action.rt.anchoredPosition = new Vector2(-RewardsLayout.ActionMargin, 14f);
            action.rt.sizeDelta = new Vector2(RewardsLayout.ActionW, 44f);
            action.text.fontSize = 18;
            action.button.onClick.AddListener(() => OnAchievementClicked(index));

            return new AchievementCard
            {
                Rt = card, Background = background, Medal = medal,
                Title = title, Tier = tier, BarBack = barBack, BarFill = barFill,
                Progress = progress, Reward = reward, Action = action,
            };
        }

        // ------------------------------------------------- всплытие и «С возвращением»

        void BuildRewardPopup(RectTransform content)
        {
            _rewardPop = NewRect("RewardPop", content);
            _rewardPop.anchorMin = _rewardPop.anchorMax = new Vector2(0.5f, 0.5f);
            _rewardPop.pivot = new Vector2(0.5f, 0.5f);
            _rewardPop.anchoredPosition = new Vector2(0f, 60f);
            _rewardPop.sizeDelta = new Vector2(360f, 200f);

            var sparkleRect = NewRect("Sparkle", _rewardPop);
            sparkleRect.anchorMin = sparkleRect.anchorMax = new Vector2(0.5f, 0.5f);
            sparkleRect.pivot = new Vector2(0.5f, 0.5f);
            sparkleRect.anchoredPosition = new Vector2(0f, 24f);
            // Было 300 на 300 при яркости 0.6 — светящаяся звезда почти во всю
            // ширину окна. Уменьшена вдвое и приглушена.
            sparkleRect.sizeDelta = new Vector2(170f, 170f);
            _rewardPopSparkle = sparkleRect.gameObject.AddComponent<Image>();
            _rewardPopSparkle.sprite = SpriteFactory.Sparkle();
            _rewardPopSparkle.color = Palette.Gold.WithAlpha(0.3f);
            _rewardPopSparkle.raycastTarget = false;

            var iconRect = NewRect("Icon", _rewardPop);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 32f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            _rewardPopIcon = iconRect.gameObject.AddComponent<Image>();
            _rewardPopIcon.sprite = SpriteFactory.Coin();
            _rewardPopIcon.preserveAspect = true;
            _rewardPopIcon.raycastTarget = false;

            _rewardPopText = NewText("Text", _rewardPop, "", 34, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_rewardPopText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -10f),
                   new Vector2(360f, 44f));
            _rewardPopText.fontStyle = FontStyle.Bold;

            _rewardPop.gameObject.SetActive(false);
        }

        /// <summary>
        /// Окно «С возвращением». Отдельный экран поверх всего, а не строка в меню:
        /// после долгого отсутствия человеку нужно сказать, что его ждали, и сразу
        /// показать, что накопилось, — в строке это теряется.
        /// </summary>
        void BuildWelcomeBackWindow()
        {
            _welcomeBack = BuildScreenRoot("WelcomeBack");

            var panel = NewRect("Panel", _welcomeBack.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440f, 480f);
            var panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = SpriteFactory.RoundedRect("panel", 0.22f);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(Palette.Deep2.r, Palette.Deep2.g, Palette.Deep2.b, 0.97f);

            var title = NewText("Title", panel, Loc.Get("back.title"), 40, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(420f, 52f));
            title.fontStyle = FontStyle.Bold;

            var chestRect = NewRect("Chest", panel);
            Anchor(chestRect, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(150f, 132f));
            var chest = chestRect.gameObject.AddComponent<Image>();
            chest.sprite = SpriteFactory.Chest(true);
            chest.preserveAspect = true;
            chest.raycastTarget = false;

            _welcomeBackDays = NewText("Days", panel, "", 24, Palette.Gold, TextAnchor.MiddleCenter);
            Anchor(_welcomeBackDays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -232f),
                   new Vector2(420f, 34f));
            _welcomeBackDays.fontStyle = FontStyle.Bold;

            var sub = NewText("Sub", panel, Loc.Get("back.sub"), 20,
                              Palette.Foam.WithAlpha(0.8f), TextAnchor.UpperCenter);
            Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(400f, 76f));
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;

            var ok = NewButton("Collect", panel, Loc.Get("back.ok"), Palette.Gold, Palette.Deep);
            Anchor(ok.rt, new Vector2(0.5f, 1f), new Vector2(0f, -368f), new Vector2(320f, 84f));
            ok.text.fontSize = 28;
            ok.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                _welcomeBack.SetActive(false);
                ShowRewards();
            });

            _welcomeBack.SetActive(false);
        }

        // ------------------------------------------------------- общие заготовки

        /// <summary>Панель-подложка страницы: тот же вид, что у витрины магазина.</summary>
        RectTransform Panel(string name, RectTransform parent, float y, float height)
        {
            var panel = NewRect(name, parent);
            Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0f, y),
                   new Vector2(RewardsLayout.ContentWidth - RewardsLayout.SidePadding * 2f, height));
            var background = panel.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect("panel", 0.3f);
            background.type = Image.Type.Sliced;
            background.color = new Color(Palette.Deep2.r, Palette.Deep2.g, Palette.Deep2.b, 0.9f);
            background.raycastTarget = false;
            return panel;
        }

        /// <summary>Прокручиваемый список во всю страницу — заданиям и достижениям.</summary>
        RectTransform ScrollList(string name, RectTransform page, out RectTransform viewport)
        {
            viewport = NewRect(name + "Viewport", page);
            Anchor(viewport, new Vector2(0.5f, 1f), Vector2.zero,
                   new Vector2(RewardsLayout.ContentWidth, RewardsLayout.PageH));
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var list = NewRect(name + "Content", viewport);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            list.sizeDelta = new Vector2(0f, RewardsLayout.PageH);

            scroll.viewport = viewport;
            scroll.content = list;
            return list;
        }

        /// <summary>
        /// Заголовок раздела внутри списка. Возвращает новый Y.
        ///
        /// Не Section: так называется склейка текста статьи справочника, и две
        /// разные вещи под одним именем в одном классе читаются плохо, даже если
        /// компилятор их различает.
        /// </summary>
        float SectionLabel(RectTransform list, string caption, float y)
        {
            var text = NewText("Section" + caption, list, caption, 20,
                               Palette.Aqua.WithAlpha(0.9f), TextAnchor.MiddleLeft);
            // По центру, как карточки: левый край заголовка совпадает с их левым
            // краем, а не уезжает под маску.
            Anchor(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y),
                   new Vector2(RewardsLayout.QuestCardW, RewardsLayout.QuestSectionH));
            text.fontStyle = FontStyle.Bold;
            return y - RewardsLayout.QuestSectionH - 4f;
        }

        /// <summary>
        /// Полоса прогресса: подложка и заливка. Заливка растягивается по ширине
        /// через anchorMax, а не sizeDelta, — тогда ей всё равно, какой ширины
        /// подложка, и одна заготовка годится и заданиям, и достижениям.
        /// </summary>
        Image Bar(RectTransform parent, Vector2 anchor, Vector2 position, float width, out Image fill)
        {
            var back = NewRect("Bar", parent);
            back.anchorMin = back.anchorMax = anchor;
            back.pivot = new Vector2(0f, 1f);
            back.anchoredPosition = position;
            back.sizeDelta = new Vector2(width, RewardsLayout.BarH);

            var backImage = back.gameObject.AddComponent<Image>();
            backImage.sprite = SpriteFactory.RoundedRect("bar", 0.5f);
            backImage.type = Image.Type.Sliced;
            backImage.color = new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.9f);
            backImage.raycastTarget = false;

            var fillRect = NewRect("Fill", back);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = SpriteFactory.RoundedRect("bar", 0.5f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.color = Palette.Aqua;
            fill.raycastTarget = false;

            return backImage;
        }
    }
}
