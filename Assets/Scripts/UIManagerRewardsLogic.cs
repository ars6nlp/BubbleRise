using System;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Поведение экрана наград: что показать, что включить, что оживить.
    ///
    /// Разделение со сборкой намеренное. Сборка выполняется один раз и описывает,
    /// где что лежит; здесь — всё, что меняется: состояние кнопок, числа, отсчёты и
    /// анимации. Пока это было вместе, в похожих экранах проекта каждое исправление
    /// числа приходилось искать среди создания виджетов.
    /// </summary>
    public partial class UIManager
    {
        /// <summary>
        /// Сколько секунд длится вращение колеса, не меньше. Длинный ход
        /// указателя растягивает его — см. <see cref="WheelMaxTurnsPerSecond"/>.
        /// </summary>
        const float WheelSpinDuration = 2.6f;

        /// <summary>
        /// Указатель обходит диск не быстрее полуоборота в секунду: каждая точка
        /// у обода меняется не чаще раза в две секунды. Скорость — правило для
        /// глаз, длительность под неё подстраивается.
        /// </summary>
        const float WheelMaxTurnsPerSecond = 0.5f;

        /// <summary>Пик скорости кривой <see cref="WheelEase"/> к средней.</summary>
        const float WheelEasePeak = 1.5f;

        /// <summary>Самый короткий ход указателя, градусы: полтора сектора.</summary>
        const float WheelMinTravel = 90f;

        /// <summary>«Без вспышек»: указатель гаснет на месте и проявляется на выпавшем секторе.</summary>
        const float WheelRevealTime = 0.6f;

        /// <summary>Сколько держится всплытие награды.</summary>
        const float RewardPopDuration = 1.7f;

        /// <summary>
        /// Пока крутится колесо, экран не обновляется и награда не всплывает.
        ///
        /// Держать приходится всё, а не только всплытие: монеты начисляются при
        /// нажатии, и без паузы счётчик в шапке успел бы подрасти на нужную сумму
        /// раньше, чем колесо остановится. Вращение после этого не показывает
        /// ничего нового.
        /// </summary>
        bool _popupHeld;

        /// <summary>Отклонение указателя при проходе границы сектора, 0…1.</summary>
        float _wheelPointerKick;
        /// <summary>Под каким сектором указатель был в прошлом кадре.</summary>
        int _wheelLastTick = -1;
        /// <summary>Яркость подсветки выигравшего сектора, 0…1. Нарастает за 0.3 с.</summary>
        float _wheelHighlight;

        // ----------------------------------------------------------- подписка

        /// <summary>
        /// Подписаться на систему наград. Зовётся из <c>Init</c>, то есть один раз
        /// за жизнь объекта, а не при каждой пересборке интерфейса: пересборка
        /// уничтожает виджеты, но не сам UIManager, и подписка в <c>BuildAll</c>
        /// копилась бы с каждой сменой языка.
        /// </summary>
        void HookRewards()
        {
            RewardsHub.Changed += OnRewardsChanged;
            RewardsHub.Granted += OnRewardGranted;
        }

        void UnhookRewards()
        {
            RewardsHub.Changed -= OnRewardsChanged;
            RewardsHub.Granted -= OnRewardGranted;
        }

        void OnRewardsChanged()
        {
            if (_popupHeld) return;          // колесо крутится — не выдаём итог заранее

            RefreshRewardsBadge();
            if (_rewardsScreen != null && _rewardsScreen.activeSelf) RefreshRewards();
        }

        void OnRewardGranted(RewardGrant grant)
        {
            if (_popupHeld) return;          // колесо ещё крутится — объявим на остановке
            ShowRewardPopup(grant);
        }

        // -------------------------------------------------------------- показ

        public void ShowRewards()
        {
            SetScreen(6);
            HideAllScreens();
            _rewardsScreen.SetActive(true);

            // Экран могли закрыть посреди вращения. Монеты при этом уже начислены и
            // записаны, поэтому доигрывать нечего: ставим колесо на выпавший сектор
            // и снимаем удержание, иначе экран остался бы замороженным навсегда.
            if (_wheelSpinLeft > 0f)
            {
                _wheelAngle = _wheelTo;
                _wheelSpinLeft = 0f;
                _wheelInstant = false;
                if (_wheelPending >= 0)
                {
                    _wheelResultText.text = "+" + RewardTables.WheelSectors[_wheelPending].Coins;
                    _wheelPending = -1;
                }
            }
            _popupHeld = false;

            SetRewardsTab(_rewardsTab);
            RefreshRewards();
        }

        /// <summary>
        /// Показать окно «С возвращением», если игрока долго не было.
        ///
        /// Один раз за запуск и только после двух и более пропущенных суток: после
        /// одного пропуска это выглядело бы навязчиво, а каждый запуск — как упрёк.
        /// </summary>
        public void MaybeShowWelcomeBack()
        {
            if (_welcomeBackShown || _welcomeBack == null) return;
            if (!RewardsHub.ConsumeWelcomeBack()) return;

            _welcomeBackShown = true;
            _welcomeBackDays.text = Loc.Get("back.days") + RewardsHub.DaysAway;
            _welcomeBack.SetActive(true);
            _welcomeBack.transform.SetAsLastSibling();
        }

        void SetRewardsTab(int tab)
        {
            _rewardsTab = Mathf.Clamp(tab, 0, RewardsLayout.TabCount - 1);

            for (int i = 0; i < RewardsLayout.TabCount; i++)
            {
                bool active = i == _rewardsTab;
                if (_rewardPages[i] != null) _rewardPages[i].SetActive(active);

                // Выбранная вкладка — залита, остальные приглушены. Тот же приём,
                // что у половинок пары в магазине: один взгляд, и понятно, где ты.
                if (_rewardTabImages[i] != null)
                    _rewardTabImages[i].color = active ? Palette.Aqua : Palette.Aqua.WithAlpha(0.3f);
                if (_rewardTabTexts[i] != null)
                    _rewardTabTexts[i].color = active ? Palette.Deep : Palette.Foam.WithAlpha(0.85f);
            }

            RefreshRewards();
        }

        /// <summary>Значок на кнопке меню. Ноль — значка нет вовсе.</summary>
        void RefreshRewardsBadge()
        {
            if (_rewardsBadge == null) return;

            int pending = RewardsHub.PendingCount;
            _rewardsBadge.gameObject.SetActive(pending > 0);
            if (pending > 0) _rewardsBadgeText.text = pending > 9 ? "9+" : pending.ToString();
        }

        // ---------------------------------------------------------- наполнение

        void RefreshRewards()
        {
            if (_rewardsScreen == null) return;

            if (_rewardsPurse != null) _rewardsPurse.text = Wallet.Balance.ToString();

            RefreshChest();
            RefreshCalendar();
            RefreshQuests();
            RefreshWheel();
            RefreshAchievements();
            RefreshRewardsBadge();
        }

        void RefreshChest()
        {
            if (_chestStatus == null) return;

            int ready = RewardsHub.ChestsReady;
            bool full = ready >= RewardTables.ChestMaxStored;

            _chestImage.sprite = SpriteFactory.Chest(ready > 0);
            _chestStatus.text = ready > 0 ? Loc.Get("chest.ready") : Loc.Get("chest.wait") + Clock(RewardsHub.ChestCooldown);
            _chestStatus.color = ready > 0 ? Palette.Gold : Palette.Foam.WithAlpha(0.8f);

            _chestStored.text = full ? Loc.Get("chest.full") : Loc.Get("chest.stored") + ready;

            SetButtonEnabled(_chestButton, ready > 0);
        }

        void RefreshCalendar()
        {
            if (_calendarCells == null) return;

            int today = RewardsHub.DailyIndex;

            for (int i = 0; i < _calendarCells.Length; i++)
            {
                var cell = _calendarCells[i];
                var grant = RewardTables.DailyCalendar[i];

                // Клетки до сегодняшней в этом круге уже закрыты, сегодняшняя
                // подсвечена, остальные ждут.
                bool done = i < today;
                bool current = i == today;

                cell.Icon.sprite = IconFor(grant);
                cell.Icon.color = done ? Palette.Foam.WithAlpha(0.35f) : Color.white;
                cell.Amount.text = grant.Kind == RewardKind.Coins ? grant.Amount.ToString() : Loc.Get(LabelFor(grant));
                cell.Amount.fontSize = grant.Kind == RewardKind.Coins ? 18 : 13;
                cell.Amount.color = done ? Palette.Foam.WithAlpha(0.4f) : Palette.Gold;

                cell.Background.color = current
                    ? new Color(Palette.Aqua.r, Palette.Aqua.g, Palette.Aqua.b, 0.22f)
                    : done ? new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.55f) : Palette.Deep;

                cell.Day.color = current ? Palette.Aqua : Palette.Foam.WithAlpha(done ? 0.4f : 0.75f);
                cell.Check.gameObject.SetActive(done);

                // Седьмой день крупнее на треть — крупный приз должен выглядеть
                // крупным призом, а не такой же клеткой в ряду.
                float scale = i == _calendarCells.Length - 1 ? 1.06f : 1f;
                cell.Rt.localScale = new Vector3(scale, scale, 1f);
            }

            _streakText.text = Loc.Get("daily.streak") + RewardsHub.Streak;

            bool canClaim = RewardsHub.CanClaimDaily;
            _dailyButton.text.text = Loc.Get(canClaim ? "daily.claim" : "daily.claimed");
            SetButtonEnabled(_dailyButton, canClaim);

            // Под кнопкой — либо отсчёт до следующего дня, либо сообщение о том,
            // что серию спас пузырь. Второе важнее: оно объясняет, почему серия не
            // обнулилась, и это ровно тот момент, когда игрок полез бы проверять.
            if (RewardsHub.RescuedRecently && !canClaim) _dailyNote.text = Loc.Get("daily.rescue");
            else if (!canClaim) _dailyNote.text = Loc.Get("daily.next") + Clock(TimeService.UntilNextDay);
            else _dailyNote.text = "";
        }

        void RefreshQuests()
        {
            if (_dailyCards == null) return;

            FillQuestCards(_dailyCards, RewardsHub.Daily, false);
            FillQuestCards(_weeklyCards, RewardsHub.Weekly, true);

            _sweepLabel.text = Loc.Get("quest.sweep") + "  +" + RewardTables.DailySweepReward;
            SetButtonEnabled(_sweepButton, RewardsHub.SweepReady);
            _sweepButton.text.text = Loc.Get(RewardsHub.SweepReady ? "quest.claim" : "quest.claimed");

            _questResetText.text = Loc.Get("quest.resetIn") + Clock(TimeService.UntilNextDay);
        }

        void FillQuestCards(QuestCard[] cards, QuestSlot[] slots, bool weekly)
        {
            if (cards == null) return;

            for (int i = 0; i < cards.Length; i++)
            {
                bool present = slots != null && i < slots.Length && slots[i] != null;
                cards[i].Rt.gameObject.SetActive(present);
                if (!present) continue;

                var quest = slots[i];
                var card = cards[i];

                card.Title.text = RewardsHub.QuestText(quest, weekly);
                card.Reward.text = "+" + RewardsHub.QuestReward(quest, weekly);

                float ratio = quest.target > 0 ? Mathf.Clamp01(quest.progress / (float)quest.target) : 0f;
                card.BarFill.fillAmount = ratio;
                card.BarFill.color = quest.Done ? Palette.Gold : Palette.Aqua;
                card.Progress.text = Mathf.Min(quest.progress, quest.target) + " / " + quest.target;

                card.Background.color = quest.claimed
                    ? new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.6f)
                    : Palette.Deep2;

                // Одна кнопка на три состояния: забрать, заменить, ничего. Три
                // отдельные кнопки в карточке высотой в сто точек не поместились бы,
                // а главное — в каждый момент осмысленно ровно одно действие.
                if (quest.Ready)
                {
                    card.Action.rt.gameObject.SetActive(true);
                    card.Action.text.text = Loc.Get("quest.claim");
                    card.Action.image.color = Palette.Gold;
                    SetButtonEnabled(card.Action, true);
                }
                else if (quest.claimed)
                {
                    card.Action.rt.gameObject.SetActive(true);
                    card.Action.text.text = Loc.Get("quest.claimed");
                    card.Action.image.color = Palette.Aqua;
                    SetButtonEnabled(card.Action, false);
                }
                else if (!weekly && RewardsHub.RerollsLeft > 0)
                {
                    card.Action.rt.gameObject.SetActive(true);
                    card.Action.text.text = Loc.Get("quest.reroll");
                    card.Action.image.color = Palette.Aqua.WithAlpha(0.85f);
                    SetButtonEnabled(card.Action, true);
                }
                else
                {
                    card.Action.rt.gameObject.SetActive(false);
                }
            }
        }

        void RefreshWheel()
        {
            if (_wheelSpinsText == null) return;

            int spins = RewardsHub.Spins;
            _wheelSpinsText.text = spins > 0 ? Loc.Get("wheel.left") + spins : Loc.Get("wheel.none");
            _wheelSpinsText.color = spins > 0 ? Palette.Foam : Palette.Foam.WithAlpha(0.55f);

            bool spinning = _wheelSpinLeft > 0f;
            bool can = spins > 0 && !spinning;

            // Три разных состояния — три разные подписи. Серая кнопка со словом
            // «КРУТИТЬ» заставляет игрока нажимать и гадать, почему ничего не
            // происходит; кнопка, которая сама говорит «следующее через 04:12»,
            // отвечает на вопрос до нажатия.
            if (spinning) _wheelButton.text.text = Loc.Get("wheel.spinning");
            else if (can) _wheelButton.text.text = Loc.Get("wheel.spin");
            else _wheelButton.text.text = Loc.Get("wheel.nextIn") + Clock(TimeService.UntilNextDay);

            // Цвет задаём сами, а не полагаемся на прозрачность: приглушённая
            // золотая кнопка на тёмном фоне читалась как коричневая, и тёмный
            // текст на ней пропадал.
            _wheelButton.image.color = can ? Palette.Gold : Palette.Hex("2A3744");
            _wheelButton.text.color = can ? Palette.Deep : Palette.Foam.WithAlpha(0.75f);
            _wheelButton.button.interactable = can;
        }

        void RefreshAchievements()
        {
            if (_achCards == null) return;

            for (int i = 0; i < _achCards.Length; i++)
            {
                var card = _achCards[i];
                var def = RewardTables.Achievements[i];

                int tier = RewardsHub.AchievementTier(i);
                int next = RewardsHub.AchievementNext(i);
                int progress = RewardsHub.AchievementProgress(i);
                bool complete = next <= 0;

                card.Title.text = Loc.Get(def.NameKey);

                // Медаль показывает взятую ступень, а не следующую: это награда за
                // сделанное. Пока не взято ничего — бронза, но приглушённая.
                card.Medal.sprite = SpriteFactory.Medal(Mathf.Clamp(tier, 1, 3));
                card.Medal.color = tier > 0 ? Color.white : Palette.Foam.WithAlpha(0.22f);

                card.Tier.text = complete ? Loc.Get("ach.complete") : Loc.Get(TierKey(tier));

                if (complete)
                {
                    card.BarFill.fillAmount = 1f;
                    card.BarFill.color = Palette.Gold;
                    card.Progress.text = progress.ToString();
                    card.Reward.text = "";
                    card.Action.rt.gameObject.SetActive(false);
                }
                else
                {
                    card.BarFill.fillAmount = Mathf.Clamp01(progress / (float)next);
                    card.BarFill.color = progress >= next ? Palette.Gold : Palette.Aqua;
                    card.Progress.text = Mathf.Min(progress, next) + " / " + next;
                    card.Reward.text = "+" + RewardForTier(def, tier);

                    bool ready = RewardsHub.AchievementReady(i);
                    card.Action.rt.gameObject.SetActive(ready);
                    SetButtonEnabled(card.Action, ready);
                }

                card.Background.color = complete
                    ? new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.12f)
                    : Palette.Deep2;
            }
        }

        /// <summary>
        /// Ключ названия ступени. Явным перечислением, а не склейкой «ach.tier» с
        /// номером: склеенный ключ не найти ни поиском по словарю, ни проверкой
        /// локализации, и пропажа перевода всплыла бы уже у игрока.
        /// </summary>
        static string TierKey(int tier)
        {
            switch (tier)
            {
                case 0: return "ach.tier1";
                case 1: return "ach.tier2";
                default: return "ach.tier3";
            }
        }

        static int RewardForTier(AchievementDef def, int tier)
            => tier == 0 ? def.RewardBronze : tier == 1 ? def.RewardSilver : def.RewardGold;

        // ------------------------------------------------------------- нажатия

        void OnChestClicked()
        {
            ButtonClicked?.Invoke();
            if (!RewardsHub.OpenChest(out RewardGrant grant)) return;

            _chestOpenTimer = 0.45f;
            SkinPurchased?.Invoke();        // тот же звук награды, что у покупки
        }

        void OnDailyClicked()
        {
            ButtonClicked?.Invoke();
            if (!RewardsHub.ClaimDaily(out RewardGrant grant)) return;

            SkinPurchased?.Invoke();
        }

        void OnQuestClicked(bool weekly, int slot)
        {
            ButtonClicked?.Invoke();

            var list = weekly ? RewardsHub.Weekly : RewardsHub.Daily;
            if (list == null || slot >= list.Length || list[slot] == null) return;

            // Одна кнопка на два действия, поэтому решаем по состоянию квеста, а не
            // по тому, что написано на кнопке: подпись могла устареть на кадр.
            if (list[slot].Ready)
            {
                if (RewardsHub.ClaimQuest(weekly, slot, out RewardGrant grant)) SkinPurchased?.Invoke();
            }
            else if (!weekly)
            {
                RewardsHub.Reroll(slot);
            }
        }

        void OnSweepClicked()
        {
            ButtonClicked?.Invoke();
            if (RewardsHub.ClaimSweep(out RewardGrant grant)) SkinPurchased?.Invoke();
        }

        void OnAchievementClicked(int index)
        {
            ButtonClicked?.Invoke();
            if (RewardsHub.ClaimAchievement(index, out RewardGrant grant)) SkinPurchased?.Invoke();
        }

        /// <summary>
        /// Крутнуть колесо.
        ///
        /// Приз определяется сразу, а анимация только показывает уже решённое —
        /// иначе вылет посреди вращения оставил бы игрока без вращения и без
        /// награды. Поэтому всплытие приходится придержать: объявить приз раньше,
        /// чем колесо остановилось, значит обесценить само вращение.
        /// </summary>
        void OnSpinClicked()
        {
            ButtonClicked?.Invoke();
            if (_wheelSpinLeft > 0f) return;

            _popupHeld = true;
            bool ok = RewardsHub.Spin(out int sector, out RewardGrant grant);
            if (!ok) { _popupHeld = false; return; }

            _wheelPending = sector;
            _wheelGrant = grant;
            _wheelResultText.text = "";
            _wheelHighlight = 0f;

            // Указатель идёт по часовой, то есть угол убывает.
            float travel = PointerTravel(sector, _wheelAngle);
            _wheelFrom = _wheelAngle;
            _wheelTo = _wheelAngle - travel;
            _wheelInstant = Visuals.NoFlashes;
            _wheelSpinTime = _wheelInstant ? WheelRevealTime : SpinTime(travel);
            _wheelSpinLeft = _wheelSpinTime;
            _wheelLastTick = SectorUnderPointer(_wheelAngle);

            // Счётчик вращений и кнопка обязаны измениться в тот же кадр, что и
            // нажатие: иначе успеваешь нажать второй раз.
            RefreshWheel();
        }

        /// <summary>
        /// Кривая хода: плавный разгон и плавная остановка, 3k² − 2k³.
        ///
        /// Прежняя кривая склеивалась из двух ветвей, и на стыке (k = 0.12)
        /// скорость скачком удваивалась (E-25). У этой скорость непрерывна, на
        /// концах равна нулю, а пик в середине — ровно <see cref="WheelEasePeak"/>
        /// средней.
        /// </summary>
        static float WheelEase(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>
        /// Длительность хода на travel градусов: не короче WheelSpinDuration и
        /// такая, чтобы пик скорости не превышал WheelMaxTurnsPerSecond.
        /// </summary>
        static float SpinTime(float travel)
            => Mathf.Max(WheelSpinDuration, WheelEasePeak * (travel / 360f) / WheelMaxTurnsPerSecond);

        /// <summary>
        /// Центр сектора в экранных градусах при неподвижном колесе.
        ///
        /// Спрайт считает сектор по <c>Atan2(y, x) + π</c>, то есть нулевой сектор
        /// начинается слева, а не справа. Подписи расставлены по той же формуле.
        /// Отсюда вычет 180: без него центр оказывается диаметрально напротив.
        /// </summary>
        static float SectorCenter(int index)
        {
            int n = RewardTables.WheelSectors.Length;
            return (index + 0.5f) * 360f / n - 180f;
        }

        /// <summary>
        /// На сколько градусов по часовой пройти указателю, чтобы встать на сектор.
        ///
        /// Диск больше не вращается: сектор стоит на своём экранном угле
        /// <see cref="SectorCenter"/>, а указатель с углом a смотрит из точки
        /// 90 + a. Ход — от полутора секторов до оборота с четвертью: меньше
        /// не читается как вращение, больше не уложить в полуоборот в секунду.
        ///
        /// Об истории этой формулы — ниже: прежде расходился показ, и стрелка
        /// стояла на противоположном секторе.
        ///
        /// **Здесь была ошибка, и она стоила правдоподобия всему колесу.** Код
        /// считал доворот как <c>90 − (i + 0.5)·60</c>, забыв про тот самый вычет
        /// 180 из <see cref="SectorCenter"/>. В итоге колесо останавливалось ровно
        /// на противоположном секторе: игрок выигрывал четыре монеты, а стрелка
        /// показывала на шестнадцать. Начисление при этом было верным — расходился
        /// только показ, и поймать это глазами можно было лишь сверив число на
        /// секторе с числом в окне награды.
        ///
        /// Небольшое смещение внутри сектора — чтобы указатель не замирал ровно на
        /// оси симметрии и тем более не на границе между секторами.
        /// </summary>
        static float PointerTravel(int sector, float from)
        {
            int n = RewardTables.WheelSectors.Length;
            float half = 360f / n * 0.5f;
            float jitter = UnityEngine.Random.Range(-half * 0.45f, half * 0.45f);

            // Цель указателя — центр сектора; угол указателя отсчитан от верха.
            float want = SectorCenter(sector) + jitter - 90f;
            float travel = Mathf.Repeat(from - want, 360f);
            if (travel < WheelMinTravel) travel += 360f;
            return travel;
        }

        /// <summary>
        /// Какой сектор сейчас под указателем: тот, что на экранном угле
        /// 90 + angle. Спрайт считает сектор по <c>Atan2(y, x) + π</c>.
        /// </summary>
        static int SectorUnderPointer(float angle)
        {
            int n = RewardTables.WheelSectors.Length;
            float local = Mathf.Repeat(90f + angle + 180f, 360f);
            return Mathf.Clamp((int)(local / (360f / n)), 0, n - 1);
        }

        // ------------------------------------------------------------- анимация

        /// <summary>
        /// Всё, что живёт во времени. Зовётся из <c>Update</c> нескалированным
        /// временем: экран наград открывается из меню, где игра стоит на паузе, и
        /// на <c>Time.deltaTime</c> анимации замерли бы.
        /// </summary>
        void TickRewards(float dt)
        {
            TickBadge(dt);
            TickRewardPopup(dt);

            // Сундук созревает и пока игрок просто стоит в меню, а сутки меняются в
            // полночь. Проверяем это на обоих экранах, где число наград видно, —
            // но не в бою: там лишний опрос часов каждый кадр ни к чему.
            bool inMenu = (_startScreen != null && _startScreen.activeSelf)
                          || (_rewardsScreen != null && _rewardsScreen.activeSelf);
            if (inMenu) RewardsHub.Tick();

            if (_rewardsScreen == null || !_rewardsScreen.activeSelf) return;

            TickChestVisual(dt);
            TickWheel(dt);
            TickCountdowns();
        }

        void TickBadge(float dt)
        {
            if (_rewardsBadge == null || !_rewardsBadge.gameObject.activeSelf) return;

            // Пульс мягкий и медленный: значок должен звать, а не мигать.
            // Один цикл примерно за два секунды, размах 8 % — и вдвое меньше
            // в щадящем режиме.
            _badgePulse += dt * 3.2f;
            float scale = 1f + Mathf.Sin(_badgePulse) * 0.08f * Visuals.Motion;
            _rewardsBadge.localScale = new Vector3(scale, scale, 1f);
        }

        void TickChestVisual(float dt)
        {
            if (_chestHolder == null) return;

            if (_chestOpenTimer > 0f)
            {
                // Открытие: короткий подскок с отдачей.
                _chestOpenTimer -= dt;
                float k = Mathf.Clamp01(1f - _chestOpenTimer / 0.45f);
                float pop = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f * Visuals.Motion;
                _chestHolder.localScale = new Vector3(pop, pop, 1f);
                _chestHolder.localRotation = Quaternion.identity;
                if (_chestOpenTimer <= 0f) _chestHolder.localScale = Vector3.one;
                return;
            }

            if (RewardsHub.ChestsReady > 0)
            {
                // Готовый сундук подрагивает: это единственная подсказка «нажми
                // сюда», которая не требует ни слова текста. Частота внутри
                // покачивания снижена с 6 до 3 рад/с, угол с 5 до 3 градусов, а в
                // щадящем режиме покачивание почти останавливается.
                _chestShake += dt * 1.6f;
                float wobble = Mathf.Sin(_chestShake * 3f) * Mathf.Max(0f, Mathf.Sin(_chestShake))
                               * 3f * Visuals.Motion;
                _chestHolder.localRotation = Quaternion.Euler(0f, 0f, wobble);
                _chestHolder.localScale = Vector3.one;
            }
            else
            {
                _chestShake = 0f;
                _chestHolder.localRotation = Quaternion.identity;
                _chestHolder.localScale = Vector3.one;
            }
        }

        void TickWheel(float dt)
        {
            if (_wheelFace == null) return;

            float pointerAlpha = 1f;
            if (_wheelSpinLeft > 0f)
            {
                _wheelSpinLeft -= dt;
                float k = Mathf.Clamp01(1f - _wheelSpinLeft / _wheelSpinTime);

                if (_wheelInstant)
                {
                    // «Без вспышек»: указатель гаснет на старом месте за 0.24 с и
                    // проявляется на выпавшем секторе за 0.36 с. Ни бега, ни
                    // мелькания секторов.
                    const float split = 0.4f;
                    _wheelAngle = k < split ? _wheelFrom : _wheelTo;
                    pointerAlpha = k < split
                        ? 1f - Mathf.SmoothStep(0f, 1f, k / split)
                        : Mathf.SmoothStep(0f, 1f, (k - split) / (1f - split));
                }
                else
                {
                    _wheelAngle = Mathf.Lerp(_wheelFrom, _wheelTo, WheelEase(k));
                    TickWheelPointer(dt);
                }

                if (_wheelSpinLeft <= 0f)
                {
                    _wheelAngle = _wheelTo;
                    _wheelSpinLeft = 0f;
                    _wheelInstant = false;
                    pointerAlpha = 1f;
                    FinishSpin();
                }
            }
            else if (_wheelPointerKick > 0f)
            {
                _wheelPointerKick = Mathf.Max(0f, _wheelPointerKick - dt * 6f);
            }

            // Диск стоит. Вращается держатель указателя вокруг центра диска.
            if (_wheelOrbit != null)
                _wheelOrbit.localRotation = Quaternion.Euler(0f, 0f, _wheelAngle);
            if (_wheelPointerImage != null)
                _wheelPointerImage.color = Color.white.WithAlpha(pointerAlpha);

            // Указатель щёлкает: отклоняется на границе сектора и возвращается.
            // Это подсказка, что он прошёл границу, и она не требует ни звука, ни
            // вспышки. На полуобороте в секунду щелчков не больше трёх в секунду.
            if (_wheelPointer != null)
                _wheelPointer.localRotation = Quaternion.Euler(0f, 0f, _wheelPointerKick * 14f);

            TickWheelHighlight(dt);
        }

        /// <summary>
        /// Колесо встало: объявляем приз.
        ///
        /// Награда уже начислена и записана на диск — это сделал
        /// <see cref="RewardsHub.Spin"/> в момент нажатия. Здесь только показ,
        /// поэтому повторно получить её нельзя ни двойным нажатием, ни выходом с
        /// экрана посреди вращения, ни вылетом игры.
        /// </summary>
        void FinishSpin()
        {
            _popupHeld = false;
            _wheelPointerKick = 0f;

            if (_wheelPending >= 0)
            {
                _wheelResultText.text = "+" + RewardTables.WheelSectors[_wheelPending].Coins;
                ShowRewardPopup(_wheelGrant);
                SkinPurchased?.Invoke();
                _wheelPending = -1;
            }

            RefreshRewards();
        }

        /// <summary>
        /// Щелчок указателя на границе сектора.
        ///
        /// Считаем по тому, какой сектор сейчас под стрелкой, а не по углу: на
        /// быстром вращении за кадр проскакивает больше сектора, и щелчок по
        /// углу то задваивался бы, то пропадал.
        /// </summary>
        void TickWheelPointer(float dt)
        {
            _wheelPointerKick = Mathf.Max(0f, _wheelPointerKick - dt * 6f);

            int now = SectorUnderPointer(_wheelAngle);
            if (now == _wheelLastTick) return;

            _wheelLastTick = now;
            _wheelPointerKick = 1f;

            // Ближе к остановке щелчки редкие, и звук уместен. На разгоне их
            // десятки в секунду — там молчим, иначе выходит треск.
            if (_wheelSpinLeft < _wheelSpinTime * 0.55f) AudioTick?.Invoke();
        }

        /// <summary>Тихое тиканье указателя. Звук берёт на себя GameManager.</summary>
        public event System.Action AudioTick;

        /// <summary>
        /// Подсветка выигравшего сектора: плавное появление за 0.3 с, без мигания.
        ///
        /// Подсвечиваем подпись, а не рисуем ещё один слой поверх колеса: лишний
        /// спрайт пришлось бы вращать вместе с ним и совмещать по углу, а подпись
        /// и так стоит ровно в центре своего сектора.
        /// </summary>
        void TickWheelHighlight(float dt)
        {
            if (_wheelLabels == null) return;

            bool won = _wheelSpinLeft <= 0f && _wheelResultText != null
                       && !string.IsNullOrEmpty(_wheelResultText.text);

            // Появление 0.3 с, угасание 0.35 с: оба фронта в пределах правил.
            _wheelHighlight = won
                ? Mathf.Min(1f, _wheelHighlight + dt / 0.3f)
                : Mathf.Max(0f, _wheelHighlight - dt / 0.35f);

            // Число выигрыша проявляется вместе с подсветкой, а не возникает за кадр.
            if (_wheelResultText != null)
                _wheelResultText.color = Palette.Gold.WithAlpha(Mathf.SmoothStep(0f, 1f, _wheelHighlight));

            int lit = won ? SectorUnderPointer(_wheelAngle) : -1;
            for (int i = 0; i < _wheelLabels.Length; i++)
            {
                bool self = i == lit;
                // Светлее, но не белее: сумма каналов не выше 2.3.
                _wheelLabels[i].color = self
                    ? Palette.SafeLight(Color.Lerp(Palette.Gold, Palette.Foam, _wheelHighlight * 0.6f))
                    : Palette.Gold.WithAlpha(0.85f);

                float scale = self ? 1f + _wheelHighlight * 0.18f : 1f;
                _wheelLabels[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Отсчёты обновляем раз в кадр: иначе цифры стоят, пока экран открыт.</summary>
        void TickCountdowns()
        {
            if (_rewardsTab == 0)
            {
                if (RewardsHub.ChestsReady <= 0 && _chestStatus != null)
                    _chestStatus.text = Loc.Get("chest.wait") + Clock(RewardsHub.ChestCooldown);

                if (_dailyNote != null && !RewardsHub.CanClaimDaily && !RewardsHub.RescuedRecently)
                    _dailyNote.text = Loc.Get("daily.next") + Clock(TimeService.UntilNextDay);
            }
            else if (_rewardsTab == 1 && _questResetText != null)
            {
                _questResetText.text = Loc.Get("quest.resetIn") + Clock(TimeService.UntilNextDay);
            }
        }

        void ShowRewardPopup(RewardGrant grant)
        {
            if (_rewardPop == null) return;

            _rewardPopIcon.sprite = IconFor(grant);
            _rewardPopText.text = grant.Kind == RewardKind.Coins
                ? "+" + grant.Amount + " " + Loc.Get("reward.coins")
                : Loc.Get(LabelFor(grant));

            _rewardPopTimer = RewardPopDuration;
            _rewardPop.gameObject.SetActive(true);
            _rewardPop.SetAsLastSibling();
        }

        void TickRewardPopup(float dt)
        {
            if (_rewardPop == null || _rewardPopTimer <= 0f) return;

            _rewardPopTimer -= dt;
            float k = Mathf.Clamp01(1f - _rewardPopTimer / RewardPopDuration);

            // Три фазы в одном выражении: выскочить с перехлёстом, повисеть, уплыть.
            // Появление растянуто с 0.22 до 0.3 от длительности, перехлёст
            // срезан с 12 % до 6 %: награда всё так же «выскакивает», но без
            // рывка за два кадра.
            float pop = k < 0.3f
                ? Mathf.Sin(k / 0.3f * Mathf.PI * 0.5f) * 1.06f
                : Mathf.Lerp(1.06f, 1f, Mathf.InverseLerp(0.3f, 0.5f, k));

            _rewardPop.localScale = new Vector3(pop, pop, 1f);
            _rewardPop.anchoredPosition = new Vector2(0f, 60f + k * 40f);

            float alpha = k < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, k);
            _rewardPopText.color = Palette.Gold.WithAlpha(alpha);
            _rewardPopIcon.color = Color.white.WithAlpha(alpha);
            // Искра приглушена: на тёмном фоне золотая звезда в треть экрана
            // была самым ярким, что вообще есть в игре.
            _rewardPopSparkle.color = Palette.Gold.WithAlpha(alpha * 0.3f * Visuals.Glow);
            _rewardPopSparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);

            if (_rewardPopTimer <= 0f) _rewardPop.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------- мелочи

        /// <summary>Иконка награды: монета, вращение или сундук.</summary>
        static Sprite IconFor(RewardGrant grant)
        {
            switch (grant.Kind)
            {
                case RewardKind.Spin: return SpriteFactory.WheelFace();
                case RewardKind.Chest: return SpriteFactory.Chest(false);
                default: return SpriteFactory.Coin();
            }
        }

        static string LabelFor(RewardGrant grant)
        {
            switch (grant.Kind)
            {
                case RewardKind.Spin: return "reward.spin";
                case RewardKind.Chest: return "reward.chest";
                default: return "reward.coins";
            }
        }

        /// <summary>
        /// Недоступная кнопка гасится, а не скрывается: исчезающие кнопки заставляют
        /// игрока искать, куда делось действие. Interactable выключается вместе с
        /// прозрачностью, иначе по серой кнопке всё равно можно нажать.
        /// </summary>
        /// <summary>Цвет выключенной кнопки. Не прозрачность, а свой тон.</summary>
        static readonly Color DisabledFill = Palette.Hex("2A3744");

        static void SetButtonEnabled(ButtonParts parts, bool enabled)
        {
            if (parts.button == null) return;

            parts.button.interactable = enabled;

            // Раньше выключенная кнопка просто гасилась до 30 % прозрачности. На
            // тёмном фоне от неё оставался почти невидимый прямоугольник, и
            // подпись «ВЫБРАНО» читалась как текст, висящий поверх панели. Теперь
            // у выключенного состояния свой непрозрачный тон: кнопка видна как
            // кнопка, просто серая.
            if (enabled)
            {
                parts.image.color = parts.enabledFill;
                parts.text.color = parts.enabledText;
            }
            else
            {
                parts.image.color = DisabledFill;
                parts.text.color = Palette.Foam.WithAlpha(0.6f);
            }
        }

        /// <summary>
        /// Отсчёт в виде «1:04:12» или «04:12». Часы показываем только когда они
        /// есть: «00:04:12» заставляет читать строку целиком, чтобы понять, что
        /// ждать всего четыре минуты.
        /// </summary>
        static string Clock(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;

            int hours = (int)span.TotalHours;
            if (hours > 0) return hours + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            return span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
        }
    }
}
