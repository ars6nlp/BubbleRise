using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Время, которому можно верить.
    ///
    /// Все награды в игре висят на таймерах, а таймеры на часах устройства — и это
    /// прямое приглашение перевести часы на сутки вперёд и собрать недельный
    /// календарь за минуту. Поэтому тут три правила:
    ///
    /// 1. **Время только идёт вперёд.** Запоминаем последнее виденное время и
    ///    монотонный счётчик (<see cref="Stopwatch"/>, который не зависит от часов).
    ///    Если системные часы вдруг оказались раньше запомненного — верим не им,
    ///    а счётчику. Перевод назад не даёт ни одной лишней награды.
    /// 2. **Прыжок вперёд не наказывается.** Перевод часов вперёд выдаст награды
    ///    раньше срока — это обман в свою пользу, но без него честный игрок в
    ///    самолёте или со сменой часового пояса терял бы серию. Ограничиваемся тем,
    ///    что ловим откат; остальное оставляем совести игрока. Игра одиночная,
    ///    валюта не продаётся за деньги — строить крепость незачем.
    /// 3. **Всё в UTC.** Смена часового пояса не должна ни ломать серию, ни дарить
    ///    лишний день. Для показа игроку дата переводится в местную уже в UI.
    ///
    /// Сетевого времени здесь нет сознательно. Запрос к серверу времени означал бы
    /// ожидание на старте, обработку офлайна и ещё одно разрешение в манифесте —
    /// ради защиты, которая в одиночной игре без покупок не окупается. Монотонный
    /// счётчик закрывает главную дыру и не стоит ничего.
    /// </summary>
    public static class TimeService
    {
        /// <summary>Сколько секунд расхождения считаем нормальным дрейфом часов.</summary>
        const double ToleranceSeconds = 120;

        static DateTime _anchorUtc;          // время, зафиксированное при последней сверке
        static float _anchorRealtime;        // показание монотонного счётчика в тот же миг
        static bool _ready;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Сдвиг из дев-панели. Отдельно от якоря, чтобы проверка суток
        /// не выглядела для самой системы как перевод часов.</summary>
        static TimeSpan _devOffset;
#endif

        /// <summary>Поймали перевод часов назад — UI может мягко об этом сказать.</summary>
        public static bool RollbackDetected { get; private set; }

        /// <summary>Текущее время в UTC, устойчивое к переводу часов назад.</summary>
        public static DateTime UtcNow
        {
            get
            {
                EnsureReady();

                // Сколько реально прошло с момента последней сверки — по счётчику,
                // которому часы устройства не указ.
                double elapsed = Time.realtimeSinceStartupAsDouble - _anchorRealtime;
                DateTime monotonic = _anchorUtc.AddSeconds(elapsed);
                DateTime system = DateTime.UtcNow;

                if (system >= monotonic.AddSeconds(-ToleranceSeconds))
                {
                    // Часы идут нормально (или вперёд) — сверяемся с ними и живём дальше.
                    _anchorUtc = system;
                    _anchorRealtime = (float)Time.realtimeSinceStartupAsDouble;
                    RollbackDetected = false;
                    return Shifted(system);
                }

                // Часы отстали от того, что мы уже видели. Верим счётчику.
                RollbackDetected = true;
                return Shifted(monotonic);
            }
        }

        /// <summary>Местное время — только для показа игроку.</summary>
        public static DateTime LocalNow => UtcNow.ToLocalTime();

        /// <summary>
        /// Номер суток по UTC. По нему считаются серия входов и смена квестов:
        /// сравнивать даты целыми числами надёжнее, чем датами с часовыми поясами.
        /// </summary>
        public static int DayNumber => (int)(UtcNow - new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;

        /// <summary>Сколько осталось до конца текущих суток UTC.</summary>
        public static TimeSpan UntilNextDay
        {
            get
            {
                DateTime now = UtcNow;
                return now.Date.AddDays(1) - now;
            }
        }

        static DateTime Shifted(DateTime value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return value.Add(_devOffset);
#else
            return value;
#endif
        }

        static void EnsureReady()
        {
            if (_ready) return;
            _ready = true;

            // Последнее виденное время переживает перезапуск: иначе достаточно
            // закрыть игру, отвести часы и открыть заново.
            long stored = 0;
            string raw = PlayerPrefs.GetString(SaveData.LastSeenKey, "0");
            long.TryParse(raw, out stored);

            DateTime system = DateTime.UtcNow;
            DateTime seen = stored > 0
                ? new DateTime(stored, DateTimeKind.Utc)
                : system;

            _anchorUtc = system > seen ? system : seen;
            _anchorRealtime = (float)Time.realtimeSinceStartupAsDouble;
            RollbackDetected = system < seen.AddSeconds(-ToleranceSeconds);
        }

        /// <summary>
        /// Запомнить текущее время на диске. Зовётся при выдаче награды и при уходе
        /// из игры — то есть ровно тогда, когда забыть его дороже всего.
        /// </summary>
        public static void Persist()
        {
            PlayerPrefs.SetString(SaveData.LastSeenKey, UtcNow.Ticks.ToString());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Отладка: сдвинуть время вперёд. Проверять сутки, ожидая сутки, —
        /// единственный способ, который точно не работает.
        /// </summary>
        public static void DevShift(TimeSpan delta)
        {
            EnsureReady();
            _devOffset += delta;
            Persist();
        }

        /// <summary>Отладка: вернуть время как есть.</summary>
        public static void DevResetShift()
        {
            _devOffset = TimeSpan.Zero;
            Persist();
        }
#endif
    }
}
