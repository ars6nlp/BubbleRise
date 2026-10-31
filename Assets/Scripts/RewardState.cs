using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Один активный квест.</summary>
    [Serializable]
    public class QuestSlot
    {
        /// <summary>Индекс шаблона в таблице.</summary>
        public int template;
        /// <summary>Сколько нужно.</summary>
        public int target;
        /// <summary>Сколько уже есть.</summary>
        public int progress;
        /// <summary>Награда забрана.</summary>
        public bool claimed;
        /// <summary>
        /// Уточнение задания. Сейчас это нужный <see cref="StatusKind"/> для квестов
        /// «разбей стихией»: сам статус хранить здесь, а не выводить из шаблона,
        /// обязательно — иначе задание менялось бы вместе с выбранным скином.
        /// </summary>
        public int param;

        public bool Done => progress >= target;
        public bool Ready => Done && !claimed;
    }

    /// <summary>
    /// Всё, что система наград помнит между запусками.
    ///
    /// Отдельный объект, а не россыпь ключей: состояние связное — серия зависит от
    /// даты входа, квесты от номера суток, — и разъехаться его частям нельзя.
    /// Едет одной строкой JSON в том же PlayerPrefs, что и остальной профиль, и
    /// вместе с ним уходит в облако Яндекса.
    /// </summary>
    [Serializable]
    public class RewardState
    {
        /// <summary>Версия формата: старые сохранения не должны ломаться.</summary>
        public int version = 1;

        // --- сундук
        /// <summary>Когда сундук в последний раз созрел (тики UTC).</summary>
        public long chestAnchor;
        /// <summary>Сколько сундуков накоплено и ждёт открытия.</summary>
        public int chestsReady;

        // --- календарь
        /// <summary>Номер суток последнего входа.</summary>
        public int lastLoginDay;
        /// <summary>Текущая серия входов.</summary>
        public int streak;
        /// <summary>Награда за сегодняшний вход уже забрана.</summary>
        public bool dailyClaimed;
        /// <summary>Когда в последний раз сработал спасательный пузырь.</summary>
        public int lastRescueDay;

        // --- квесты
        public int questDay;
        public QuestSlot[] daily = new QuestSlot[0];
        public int questWeek;
        public QuestSlot[] weekly = new QuestSlot[0];
        public int rerollsLeft;
        public bool sweepClaimed;

        // --- колесо
        public int spins;
        public int lastFreeSpinDay;

        // --- достижения: по ступеням на каждое
        public int[] achievementTier = new int[0];
        /// <summary>Накопительные счётчики для достижений.</summary>
        public int totalDestroyed;
        public long totalDistance;
        public int totalRuns;
        public int totalFish;
        public int bestDepth;
        public int bossesBeaten;
        public int bestStreak;
        public long totalCoins;
        public int chestsOpened;

        // --- возвращение
        /// <summary>Номер суток, когда игрок заходил в прошлый раз, — для окна «С возвращением».</summary>
        public int previousVisitDay;
        /// <summary>В какие сутки окно «С возвращением» уже показывали.</summary>
        public int welcomeDay;
    }

    /// <summary>
    /// Хранение состояния наград.
    ///
    /// Подпись здесь простая и честная по назначению: она не защищает от взлома —
    /// ключ лежит в том же бинарнике, — а защищает от случайной порчи и от правки
    /// «на коленке» в редакторе PlayerPrefs. Городить криптографию в одиночной
    /// игре без покупок значило бы тратить силы на воображаемого противника.
    /// </summary>
    public static class RewardStore
    {
        const string Salt = "bubblerise.rewards.v1";

        static RewardState _cached;

        public static RewardState State
        {
            get
            {
                if (_cached == null) _cached = Load();
                return _cached;
            }
        }

        public static void Save()
        {
            if (_cached == null) return;

            string json = JsonUtility.ToJson(_cached);
            PlayerPrefs.SetString(SaveData.RewardsKey, Sign(json) + "|" + json);
            PlayerPrefs.Save();
        }

        static RewardState Load()
        {
            string raw = PlayerPrefs.GetString(SaveData.RewardsKey, "");
            if (string.IsNullOrEmpty(raw)) return Fresh();

            int split = raw.IndexOf('|');
            if (split <= 0) return Fresh();

            string signature = raw.Substring(0, split);
            string json = raw.Substring(split + 1);

            // Подпись не сошлась — состояние испорчено. Начинаем заново, но молча:
            // пугать игрока сообщением об ошибке тут не за что, он ни при чём.
            if (signature != Sign(json)) return Fresh();

            try
            {
                var state = JsonUtility.FromJson<RewardState>(json);
                if (state == null) return Fresh();
                Normalize(state);
                return state;
            }
            catch (Exception)
            {
                return Fresh();
            }
        }

        /// <summary>
        /// Подогнать старое сохранение под нынешние таблицы.
        ///
        /// Добавить девятое достижение — дело одной строки в таблице, и без этой
        /// подгонки оно ронялo бы игру у всех, кто играл до обновления: массив
        /// ступеней в их сохранении короче таблицы. Проверять это в каждом обращении
        /// дороже и легче забыть, чем выправить один раз при загрузке.
        /// </summary>
        static void Normalize(RewardState state)
        {
            int need = RewardTables.Achievements.Length;
            if (state.achievementTier == null || state.achievementTier.Length != need)
            {
                var tiers = new int[need];
                if (state.achievementTier != null)
                {
                    int copy = Mathf.Min(state.achievementTier.Length, need);
                    for (int i = 0; i < copy; i++) tiers[i] = state.achievementTier[i];
                }
                state.achievementTier = tiers;
            }

            if (state.daily == null) state.daily = new QuestSlot[0];
            if (state.weekly == null) state.weekly = new QuestSlot[0];

            // Квест ссылается на шаблон номером. Если таблица менялась между
            // версиями, номер может указывать в пустоту — тогда проще раздать
            // задания заново, чем читать по битому индексу.
            if (!Valid(state.daily, RewardTables.DailyQuests.Length)) state.questDay = -1;
            if (!Valid(state.weekly, RewardTables.WeeklyQuests.Length)) state.questWeek = -1;
        }

        static bool Valid(QuestSlot[] list, int templates)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i] == null || list[i].template < 0 || list[i].template >= templates) return false;
            return true;
        }

        static RewardState Fresh()
        {
            return new RewardState
            {
                achievementTier = new int[RewardTables.Achievements.Length],
                lastLoginDay = -1,
                questDay = -1,
                questWeek = -1,
                lastFreeSpinDay = -1,
                lastRescueDay = -1,
                previousVisitDay = -1,
                welcomeDay = -1,
            };
        }

        static string Sign(string json)
        {
            unchecked
            {
                int hash = 17;
                string payload = json + Salt;
                for (int i = 0; i < payload.Length; i++) hash = hash * 31 + payload[i];
                return hash.ToString("X8");
            }
        }

        /// <summary>Отладка и сброс профиля: начать всё заново.</summary>
        public static void Reset()
        {
            _cached = Fresh();
            Save();
        }
    }
}
