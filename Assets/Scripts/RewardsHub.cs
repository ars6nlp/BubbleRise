using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Система возвращения: сундук раз в час, календарь входов, квесты, колесо и
    /// достижения.
    ///
    /// **Почему статический класс, а не набор MonoBehaviour-менеджеров.** В этом
    /// проекте нет ни одного объекта-одиночки в сцене: сцена содержит камеру, свет
    /// и один игровой объект, всё остальное строится кодом. Wallet и SkinStore —
    /// тоже статические. Шесть новых компонентов в иерархии выбивались бы из этого
    /// устройства и потребовали бы искать друг друга, а выигрыша не дали бы:
    /// состояние у системы общее и живёт в одном сохранении.
    ///
    /// Связь с игрой — через <see cref="Report"/> и события: игра не знает, что
    /// существуют квесты, она просто сообщает, что произошло.
    ///
    /// **Главное правило экономики.** Монеты за разрушенные препятствия начисляет
    /// только GameManager. Здесь — второй, намеренно скромный источник: по расчёту
    /// в <see cref="RewardTables"/> это около трети дневного заработка игрой.
    /// </summary>
    public static class RewardsHub
    {
        /// <summary>Что-то изменилось — значку и экрану пора обновиться.</summary>
        public static event Action Changed;

        /// <summary>Награда выдана: UI показывает всплытие, звук играет фанфару.</summary>
        public static event Action<RewardGrant> Granted;

        /// <summary>Квест выполнен прямо сейчас — показать баннер в игре.</summary>
        public static event Action<string> QuestCompleted;

        static RewardState S => RewardStore.State;

        // ------------------------------------------------------------- запуск

        /// <summary>
        /// Пересчитать всё, что могло измениться, пока игра была закрыта.
        /// Зовётся при старте и при возвращении из фона.
        /// </summary>
        public static void Refresh()
        {
            int today = TimeService.DayNumber;

            // Перевод часов назад — ничего не начисляем, пока время не выправится.
            if (TimeService.RollbackDetected) return;

            TickChests();
            TickLogin(today);
            TickQuests(today);
            TickFreeSpin(today);

            RewardStore.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Дешёвая проверка «не пора ли пересчитать». Зовётся из меню каждый кадр:
        /// сундук созревает и пока игрок просто сидит на главном экране, а сутки
        /// меняются в полночь, а не при запуске игры.
        /// </summary>
        public static void Tick()
        {
            if (S.lastLoginDay != TimeService.DayNumber) { Refresh(); return; }
            if (S.chestsReady < RewardTables.ChestMaxStored && ChestCooldown <= TimeSpan.Zero) Refresh();
        }

        /// <summary>Сколько всего можно забрать прямо сейчас — число на значке.</summary>
        public static int PendingCount
        {
            get
            {
                int n = S.chestsReady;
                if (CanClaimDaily) n++;
                n += ReadyQuests(S.daily) + ReadyQuests(S.weekly);
                if (SweepReady) n++;
                if (S.spins > 0) n++;
                n += ReadyAchievements();
                return n;
            }
        }

        static int ReadyQuests(QuestSlot[] list)
        {
            if (list == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Length; i++) if (list[i] != null && list[i].Ready) n++;
            return n;
        }

        // ------------------------------------------------------------- сундук

        static void TickChests()
        {
            long anchorTicks = S.chestAnchor;
            DateTime now = TimeService.UtcNow;

            if (anchorTicks <= 0)
            {
                // Первый запуск: первый сундук выдаём сразу — знакомство с системой
                // не должно начинаться с часа ожидания.
                S.chestAnchor = now.Ticks;
                S.chestsReady = 1;
                return;
            }

            var anchor = new DateTime(anchorTicks, DateTimeKind.Utc);
            double hours = (now - anchor).TotalHours;
            if (hours < RewardTables.ChestHours) return;

            int matured = (int)(hours / RewardTables.ChestHours);
            S.chestsReady = Mathf.Min(RewardTables.ChestMaxStored, S.chestsReady + matured);
            S.chestAnchor = anchor.AddHours(matured * RewardTables.ChestHours).Ticks;
        }

        public static int ChestsReady => S.chestsReady;

        /// <summary>Сколько осталось до следующего сундука. Ноль, если уже готов.</summary>
        public static TimeSpan ChestCooldown
        {
            get
            {
                if (S.chestsReady >= RewardTables.ChestMaxStored) return TimeSpan.Zero;
                if (S.chestAnchor <= 0) return TimeSpan.Zero;

                var anchor = new DateTime(S.chestAnchor, DateTimeKind.Utc);
                DateTime ripe = anchor.AddHours(RewardTables.ChestHours);
                TimeSpan left = ripe - TimeService.UtcNow;
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }

        /// <summary>
        /// Открыть сундук. Списание идёт до выдачи и сразу пишется на диск: вылет
        /// игры в момент анимации не должен ни продублировать награду, ни съесть её.
        /// </summary>
        public static bool OpenChest(out RewardGrant grant)
        {
            grant = default;
            if (S.chestsReady <= 0) return false;

            S.chestsReady--;
            S.chestsOpened++;

            int index = RewardTables.Roll(RewardTables.ChestLoot);
            grant = RewardGrant.Coins(RewardTables.ChestLoot[index].Coins);

            Commit(grant);
            return true;
        }

        // ----------------------------------------------------------- календарь

        static void TickLogin(int today)
        {
            if (S.lastLoginDay == today) return;

            if (S.lastLoginDay < 0)
            {
                S.streak = 0;           // первый вход: серия начнётся с получения награды
            }
            else
            {
                int gap = today - S.lastLoginDay;

                if (gap == 1)
                {
                    // нормальный следующий день — серия продолжится при получении
                }
                else if (gap == 2 && RescueAvailable(today))
                {
                    // Спасательный пузырь: один пропуск в неделю серию не рвёт.
                    // Без него серия обрывается у любого, кто однажды лёг спать
                    // пораньше, и возвращаться становится незачем.
                    S.lastRescueDay = today;
                }
                else
                {
                    S.streak = 0;
                }
            }

            S.previousVisitDay = S.lastLoginDay;
            S.lastLoginDay = today;
            S.dailyClaimed = false;
        }

        /// <summary>
        /// Есть ли в запасе спасательный пузырь.
        ///
        /// Отрицательное значение значит «не использован ни разу», и это не то же
        /// самое, что «использован в нулевой день»: при простом сравнении дат
        /// первый же пропуск у нового игрока оставался без спасения — пузырь
        /// становился доступен только на восьмой день игры.
        /// </summary>
        static bool RescueAvailable(int today)
            => S.lastRescueDay < 0 || today - S.lastRescueDay >= RewardTables.RescueEveryDays;

        public static bool CanClaimDaily => !S.dailyClaimed && S.lastLoginDay == TimeService.DayNumber;

        /// <summary>Какой день календаря сейчас показывается, 0…6.</summary>
        public static int DailyIndex => S.streak % RewardTables.DailyCalendar.Length;

        public static int Streak => S.streak;

        /// <summary>Сработал ли спасательный пузырь при последнем входе.</summary>
        public static bool RescuedRecently => S.lastRescueDay == S.lastLoginDay && S.lastRescueDay >= 0;

        public static bool ClaimDaily(out RewardGrant grant)
        {
            grant = default;
            if (!CanClaimDaily) return false;

            grant = RewardTables.DailyCalendar[DailyIndex];

            S.dailyClaimed = true;
            S.streak++;
            S.bestStreak = Mathf.Max(S.bestStreak, S.streak);

            Commit(grant);
            return true;
        }

        // -------------------------------------------------------------- квесты

        public static QuestSlot[] Daily => S.daily;
        public static QuestSlot[] Weekly => S.weekly;
        public static int RerollsLeft => S.rerollsLeft;

        /// <summary>Все три дневных сделаны, а бонус за них ещё не взят.</summary>
        public static bool SweepReady
        {
            get
            {
                if (S.sweepClaimed || S.daily == null || S.daily.Length == 0) return false;
                for (int i = 0; i < S.daily.Length; i++)
                    if (S.daily[i] == null || !S.daily[i].Done) return false;
                return true;
            }
        }

        static void TickQuests(int today)
        {
            int week = today / 7;

            if (S.questDay != today || S.daily == null || S.daily.Length == 0)
            {
                S.daily = RollQuests(RewardTables.DailyQuests, today);
                S.questDay = today;
                S.rerollsLeft = RewardTables.QuestRerollsPerDay;
                S.sweepClaimed = false;
            }

            if (S.questWeek != week || S.weekly == null || S.weekly.Length == 0)
            {
                S.weekly = RollQuests(RewardTables.WeeklyQuests, week * 7 + 3);
                S.questWeek = week;
            }
        }

        /// <summary>
        /// По одному квесту каждой сложности.
        ///
        /// Набор зависит от номера суток, а не от случая: иначе перезапуск игры
        /// крутил бы задания, пока не выпадут удобные. Непроходимые отсеиваются —
        /// просить победить босса у того, кто до боссов не доплывал, бессмысленно.
        /// </summary>
        static QuestSlot[] RollQuests(QuestTemplate[] pool, int seed)
        {
            var rng = new System.Random(seed * 7919);
            bool bossSeen = SkinStore.MetMask != 0;

            var result = new QuestSlot[3];
            for (int tier = 0; tier < 3; tier++)
            {
                // Собираем подходящие шаблоны этой сложности.
                int count = 0;
                for (int i = 0; i < pool.Length; i++)
                    if (pool[i].Tier == tier && (!pool[i].NeedsBoss || bossSeen)) count++;

                if (count == 0)
                {
                    // Для этой сложности нечего предложить — берём любой доступный.
                    for (int i = 0; i < pool.Length; i++)
                        if (!pool[i].NeedsBoss || bossSeen) { result[tier] = Build(pool, i, rng); break; }
                    if (result[tier] == null) result[tier] = Build(pool, 0, rng);
                    continue;
                }

                int pick = rng.Next(count);
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].Tier != tier || (pool[i].NeedsBoss && !bossSeen)) continue;
                    if (pick-- > 0) continue;
                    result[tier] = Build(pool, i, rng);
                    break;
                }
            }
            return result;
        }

        static QuestSlot Build(QuestTemplate[] pool, int index, System.Random rng)
        {
            var template = pool[index];
            int span = Mathf.Max(1, (template.Max - template.Min) / Mathf.Max(1, template.Step) + 1);
            int target = template.Min + rng.Next(span) * template.Step;

            return new QuestSlot
            {
                template = index,
                target = Mathf.Max(1, target),
                progress = 0,
                claimed = false,
                param = template.Goal == QuestGoal.DestroyWithStatus ? PickStatus(rng) : 0,
            };
        }

        /// <summary>
        /// Какую стихию просить у игрока.
        ///
        /// Только ту, что ему доступна. Статус препятствия задаётся материалом
        /// скина пузырька (так его берёт SkinFxRuntime), значит «разбей двадцать
        /// пять препятствий пламенем» выполнимо лишь тем, у кого открыта магма.
        /// Просить недоступное — значит выдать задание, которое нельзя закрыть
        /// ничем, кроме покупки, то есть превратить квест в требование заплатить.
        /// </summary>
        static int PickStatus(System.Random rng)
        {
            int best = PlayerPrefs.GetInt(SaveData.BestKey, 0);
            var available = new int[SkinStore.Bubble.Length];
            int count = 0;

            for (int i = 0; i < SkinStore.Bubble.Length; i++)
            {
                var def = SkinStore.Bubble[i];
                if (!SkinStore.IsUnlocked(def, best)) continue;

                // Ровно та же ветка, что в SkinFxRuntime: у трофейных материала нет.
                var fx = def.Kind == UnlockKind.Boss
                    ? MaterialFxTable.OfBoss(def.UnlockBoss)
                    : MaterialFxTable.Of(def.Material);

                int status = (int)fx.Status;

                bool duplicate = false;
                for (int k = 0; k < count; k++) if (available[k] == status) { duplicate = true; break; }
                if (!duplicate) available[count++] = status;
            }

            // Пустого списка быть не должно: стартовый скин открыт всегда. Но если
            // сохранение испорчено, лучше дать выполнимый квест, чем никакой.
            if (count == 0) return (int)StatusKind.Soak;
            return available[rng.Next(count)];
        }

        /// <summary>Заменить один дневной квест. Раз в сутки и бесплатно.</summary>
        public static bool Reroll(int slot)
        {
            if (S.rerollsLeft <= 0 || S.daily == null || slot < 0 || slot >= S.daily.Length) return false;
            if (S.daily[slot] == null || S.daily[slot].claimed) return false;

            var rng = new System.Random(TimeService.DayNumber * 104729 + slot * 31 + S.rerollsLeft);
            int tier = RewardTables.DailyQuests[S.daily[slot].template].Tier;
            bool bossSeen = SkinStore.MetMask != 0;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                int i = rng.Next(RewardTables.DailyQuests.Length);
                var candidate = RewardTables.DailyQuests[i];
                if (candidate.Tier != tier) continue;
                if (candidate.NeedsBoss && !bossSeen) continue;
                if (i == S.daily[slot].template) continue;

                S.daily[slot] = Build(RewardTables.DailyQuests, i, rng);
                S.rerollsLeft--;
                RewardStore.Save();
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public static bool ClaimQuest(bool weekly, int slot, out RewardGrant grant)
        {
            grant = default;
            var list = weekly ? S.weekly : S.daily;
            if (list == null || slot < 0 || slot >= list.Length) return false;

            var quest = list[slot];
            if (quest == null || !quest.Ready) return false;

            quest.claimed = true;
            var pool = weekly ? RewardTables.WeeklyQuests : RewardTables.DailyQuests;
            grant = RewardGrant.Coins(pool[quest.template].Reward);

            Commit(grant);
            return true;
        }

        public static bool ClaimSweep(out RewardGrant grant)
        {
            grant = default;
            if (!SweepReady) return false;

            S.sweepClaimed = true;
            grant = RewardGrant.Coins(RewardTables.DailySweepReward);
            Commit(grant);
            return true;
        }

        /// <summary>
        /// Описание задания готовой строкой.
        ///
        /// Собирается здесь, а не в UI: текст зависит от данных квеста (цель, а у
        /// «стихии» ещё и статус), и склеивать его в двух местах — в списке заданий
        /// и во всплывающем баннере — значит однажды склеить по-разному.
        /// </summary>
        public static string QuestText(QuestSlot quest, bool weekly)
        {
            if (quest == null) return "";

            var pool = weekly ? RewardTables.WeeklyQuests : RewardTables.DailyQuests;
            var template = pool[quest.template];
            string name = Loc.Get(template.NameKey);

            if (template.Goal == QuestGoal.DestroyWithStatus)
                name += " " + StatusName((StatusKind)quest.param);

            return name + ": " + quest.target;
        }

        /// <summary>Название стихии для текста задания.</summary>
        public static string StatusName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Burn: return Loc.Get("status.burn");
                case StatusKind.Freeze: return Loc.Get("status.freeze");
                case StatusKind.Shock: return Loc.Get("status.shock");
                case StatusKind.Corrode: return Loc.Get("status.corrode");
                case StatusKind.Gild: return Loc.Get("status.gild");
                case StatusKind.Shadow: return Loc.Get("status.shadow");
                case StatusKind.Stardust: return Loc.Get("status.stardust");
                case StatusKind.Crystal: return Loc.Get("status.crystal");
                case StatusKind.Haze: return Loc.Get("status.haze");
                case StatusKind.Spectrum: return Loc.Get("status.spectrum");
                case StatusKind.Silt: return Loc.Get("status.silt");
                case StatusKind.Petrify: return Loc.Get("status.petrify");
                case StatusKind.Glitch: return Loc.Get("status.glitch");
                default: return Loc.Get("status.soak");
            }
        }

        /// <summary>Награда за задание — её показывает карточка.</summary>
        public static int QuestReward(QuestSlot quest, bool weekly)
        {
            if (quest == null) return 0;
            var pool = weekly ? RewardTables.WeeklyQuests : RewardTables.DailyQuests;
            return pool[quest.template].Reward;
        }

        // ------------------------------------------------------- приём событий

        /// <summary>
        /// Игра сообщает, что произошло. Квесты, достижения и счётчики двигаются
        /// отсюда и только отсюда — больше система о внешнем мире ничего не знает.
        /// </summary>
        public static void Report(QuestGoal goal, int amount, StatusKind status = StatusKind.Soak)
        {
            if (amount <= 0) return;

            Accumulate(goal, amount);
            bool finished = Advance(S.daily, RewardTables.DailyQuests, goal, amount, status, false);
            finished |= Advance(S.weekly, RewardTables.WeeklyQuests, goal, amount, status, false);

            if (finished) Changed?.Invoke();
        }

        /// <summary>
        /// Результат за один заплыв: глубина, монеты. Такие квесты берут максимум,
        /// а не сумму, — иначе «набрать 150 монет за заплыв» закрывалось бы тремя
        /// заплывами по пятьдесят, что не то же самое.
        /// </summary>
        public static void ReportRunBest(QuestGoal goal, int value)
        {
            if (value <= 0) return;

            // Лучшая глубина — тоже максимум, поэтому живёт здесь: повторный вызов
            // с тем же числом ничего не испортит. Накопительным счётчикам этого
            // мало, им нужна разница, — см. ReportCoinsEarned.
            if (goal == QuestGoal.DepthInRun) S.bestDepth = Mathf.Max(S.bestDepth, value);

            bool finished = Advance(S.daily, RewardTables.DailyQuests, goal, value, StatusKind.Soak, true);
            finished |= Advance(S.weekly, RewardTables.WeeklyQuests, goal, value, StatusKind.Soak, true);

            if (finished) Changed?.Invoke();
        }

        /// <summary>
        /// Монеты, добытые игрой, — только для достижения «Сборщик монет».
        ///
        /// Отдельно от <see cref="ReportRunBest"/>, потому что складывать и брать
        /// максимум здесь нужно от разных чисел: квест спрашивает «сколько за один
        /// заплыв», достижение — «сколько за всё время». Один аргумент на оба
        /// смысла давал бы верный ответ ровно до первой доп. жизни за рекламу.
        /// </summary>
        public static void ReportCoinsEarned(int delta)
        {
            if (delta <= 0) return;
            S.totalCoins += delta;
        }

        static void Accumulate(QuestGoal goal, int amount)
        {
            switch (goal)
            {
                case QuestGoal.Destroy: S.totalDestroyed += amount; break;
                case QuestGoal.Distance: S.totalDistance += amount; break;
                case QuestGoal.Runs: S.totalRuns += amount; break;
                case QuestGoal.Fish: S.totalFish += amount; break;
                case QuestGoal.BossBeaten: S.bossesBeaten += amount; break;
            }
        }

        /// <summary>
        /// Продвинуть подходящие квесты. Возвращает true, только если какой-то из
        /// них именно ДОШЁЛ до цели.
        ///
        /// Разница существенная: препятствий за заплыв бывают сотни, и будить на
        /// каждом событие <see cref="Changed"/> — значит пересчитывать значок и
        /// перебирать все достижения сотни раз за партию. Значку же важен один
        /// момент: когда появилось, что забрать.
        /// </summary>
        static bool Advance(QuestSlot[] list, QuestTemplate[] pool, QuestGoal goal, int amount,
                            StatusKind status, bool best)
        {
            if (list == null) return false;

            bool finished = false;
            for (int i = 0; i < list.Length; i++)
            {
                var quest = list[i];
                if (quest == null || quest.claimed || quest.Done) continue;

                var template = pool[quest.template];
                if (template.Goal != goal) continue;

                // «Стихией» — значит именно тем статусом, который выпал в задании.
                if (goal == QuestGoal.DestroyWithStatus && (int)status != quest.param) continue;

                int before = quest.progress;
                quest.progress = best ? Mathf.Max(quest.progress, amount) : quest.progress + amount;
                if (quest.progress == before) continue;

                if (!quest.Done) continue;

                finished = true;
                QuestCompleted?.Invoke(Loc.Get(template.NameKey));
            }
            return finished;
        }

        /// <summary>Разрушенное препятствие: двигает и общий счёт, и «стихию».</summary>
        public static void ReportDestroy(StatusKind status)
        {
            Report(QuestGoal.Destroy, 1);
            Report(QuestGoal.DestroyWithStatus, 1, status);
        }

        // -------------------------------------------------------------- колесо

        public static int Spins => S.spins;

        static void TickFreeSpin(int today)
        {
            if (S.lastFreeSpinDay == today) return;
            S.lastFreeSpinDay = today;
            S.spins += RewardTables.FreeSpinsPerDay;
        }

        /// <summary>
        /// Крутнуть. Результат определяется здесь и сразу записывается — анимация
        /// в UI только показывает то, что уже решено. Иначе вылет в середине
        /// вращения оставил бы игрока и без вращения, и без награды.
        /// </summary>
        public static bool Spin(out int sector, out RewardGrant grant)
        {
            sector = 0;
            grant = default;
            if (S.spins <= 0) return false;

            S.spins--;
            sector = RewardTables.Roll(RewardTables.WheelSectors);
            grant = RewardGrant.Coins(RewardTables.WheelSectors[sector].Coins);

            Commit(grant);
            return true;
        }

        // --------------------------------------------------------- достижения

        /// <summary>Сколько ступеней уже взято: 0 — ни одной, 3 — золото.</summary>
        public static int AchievementTier(int index)
        {
            if (S.achievementTier == null || index < 0 || index >= S.achievementTier.Length) return 0;
            return S.achievementTier[index];
        }

        /// <summary>Текущее значение счётчика достижения.</summary>
        public static int AchievementProgress(int index)
        {
            var def = RewardTables.Achievements[index];
            if (index == RewardTables.StreakAchievementIndex) return S.bestStreak;

            switch (def.Goal)
            {
                case QuestGoal.Destroy: return S.totalDestroyed;
                case QuestGoal.Distance: return (int)Math.Min(int.MaxValue, S.totalDistance);
                case QuestGoal.Runs: return S.totalRuns;
                case QuestGoal.Fish: return S.totalFish;
                case QuestGoal.DepthInRun: return S.bestDepth;
                case QuestGoal.BossBeaten: return S.bossesBeaten;
                case QuestGoal.CoinsInRun: return (int)Math.Min(int.MaxValue, S.totalCoins);
                default: return 0;
            }
        }

        /// <summary>Порог следующей ступени. Ноль — всё взято.</summary>
        public static int AchievementNext(int index)
        {
            var def = RewardTables.Achievements[index];
            switch (AchievementTier(index))
            {
                case 0: return def.Bronze;
                case 1: return def.Silver;
                case 2: return def.Gold;
                default: return 0;
            }
        }

        public static bool AchievementReady(int index)
        {
            int next = AchievementNext(index);
            return next > 0 && AchievementProgress(index) >= next;
        }

        static int ReadyAchievements()
        {
            int n = 0;
            for (int i = 0; i < RewardTables.Achievements.Length; i++) if (AchievementReady(i)) n++;
            return n;
        }

        public static bool ClaimAchievement(int index, out RewardGrant grant)
        {
            grant = default;
            if (!AchievementReady(index)) return false;

            var def = RewardTables.Achievements[index];
            int tier = AchievementTier(index);
            int reward = tier == 0 ? def.RewardBronze : tier == 1 ? def.RewardSilver : def.RewardGold;

            S.achievementTier[index] = tier + 1;
            grant = RewardGrant.Coins(reward);

            Commit(grant);
            return true;
        }

        // ------------------------------------------------------ выдача награды

        /// <summary>
        /// Единственное место, где награда превращается в монеты.
        ///
        /// Порядок важен: сперва запись состояния на диск, потом начисление, потом
        /// событие для UI. При вылете в любой момент игрок либо уже получил и это
        /// записано, либо не получил и может забрать снова — но не «получил и
        /// потерял».
        /// </summary>
        static void Commit(RewardGrant grant)
        {
            switch (grant.Kind)
            {
                case RewardKind.Spin: S.spins += grant.Amount; break;
                case RewardKind.Chest:
                    S.chestsReady = Mathf.Min(RewardTables.ChestMaxStored, S.chestsReady + grant.Amount);
                    break;
            }

            RewardStore.Save();
            TimeService.Persist();

            if (grant.Kind == RewardKind.Coins) Wallet.EarnFromReward(grant.Amount);

            Granted?.Invoke(grant);
            Changed?.Invoke();
        }

        /// <summary>
        /// Пора ли показать окно «С возвращением». Ответ «да» выдаётся один раз за
        /// сутки и сразу записывается.
        ///
        /// Без отметки в сохранении окно всплывало бы при каждом запуске в течение
        /// дня: «сколько дней вас не было» считается от прошлого визита, а прошлый
        /// визит меняется только со сменой суток. Встречать человека словами «вас не
        /// было три дня» на третий запуск за вечер — худшее, что можно сделать.
        /// </summary>
        public static bool ConsumeWelcomeBack()
        {
            if (DaysAway < 2) return false;
            if (S.welcomeDay == S.lastLoginDay) return false;

            S.welcomeDay = S.lastLoginDay;
            RewardStore.Save();
            return true;
        }

        /// <summary>Сколько суток игрок отсутствовал — для окна «С возвращением».</summary>
        public static int DaysAway
        {
            get
            {
                if (S.previousVisitDay < 0) return 0;
                return Mathf.Max(0, S.lastLoginDay - S.previousVisitDay - 1);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void DevCompleteQuests()
        {
            if (S.daily != null)
                for (int i = 0; i < S.daily.Length; i++)
                    if (S.daily[i] != null) S.daily[i].progress = S.daily[i].target;

            RewardStore.Save();
            Changed?.Invoke();
        }

        public static void DevResetQuests()
        {
            S.questDay = -1;
            S.questWeek = -1;
            Refresh();
        }

        public static void DevGrantSpin()
        {
            S.spins++;
            RewardStore.Save();
            Changed?.Invoke();
        }

        public static void DevResetChestTimer()
        {
            S.chestAnchor = TimeService.UtcNow.AddHours(-RewardTables.ChestHours).Ticks;
            Refresh();
        }

        public static void DevResetAll()
        {
            RewardStore.Reset();
            TimeService.DevResetShift();
            Refresh();
        }
#endif
    }
}
