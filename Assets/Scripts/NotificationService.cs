using System;
using UnityEngine;

#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
using Unity.Notifications.Android;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Локальные напоминания: сундук созрел, награда за вход ждёт, задания
    /// обновились.
    ///
    /// **Почему за дефайном.** Расписание уведомлений на Android умеет только пакет
    /// com.unity.mobile.notifications, и без него код с `using Unity.Notifications`
    /// не компилируется вовсе. Пакет в проекте не стоит, ставит его человек, который
    /// собирает сборку, — поэтому реализация спрятана за BUBBLERISE_NOTIFICATIONS, а
    /// без дефайна класс остаётся рабочей пустышкой. Так проект собирается и сейчас,
    /// а включение уведомлений — это установка пакета и один символ в настройках,
    /// без правок кода. Что именно нужно сделать — написано в отчёте.
    ///
    /// **Почему расписание, а не push.** Напоминания целиком локальные: сервера у
    /// игры нет, аккаунтов нет, и отправлять что-либо наружу не требуется. Все сроки
    /// известны заранее — сундук созревает ровно через час, сутки сменяются в
    /// полночь, — поэтому расписание составляется при уходе из игры и этого хватает.
    ///
    /// **Правила, по которым решалось, когда будить игрока.** Напоминание — самая
    /// легко раздражающая часть системы, поэтому их всего три, и каждое отменяется,
    /// как только становится ненужным:
    ///
    /// * ночью не пишем — всё, что попадает в интервал с 23 до 9 по местному
    ///   времени, сдвигается на утро;
    /// * про сундук — один раз, когда созреет следующий, а не на каждый из трёх;
    /// * про вход — только если награда за сегодня не забрана, и только вечером,
    ///   когда человек скорее свободен;
    /// * пока игра открыта, расписание пустое: звать туда, где игрок уже есть,
    ///   незачем.
    /// </summary>
    public static class NotificationService
    {
        const string ChannelId = "bubblerise_rewards";

        /// <summary>С какого часа по местному времени можно будить.</summary>
        const int QuietEndHour = 9;
        /// <summary>С какого часа уже нельзя.</summary>
        const int QuietStartHour = 23;
        /// <summary>Когда напоминать про ежедневную награду.</summary>
        const int DailyReminderHour = 19;

        static bool _ready;

        /// <summary>Доступны ли уведомления в этой сборке.</summary>
        public static bool Supported
        {
            get
            {
#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
                return !Application.isEditor;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Подготовить канал и спросить разрешение. Зовётся один раз при запуске.
        /// </summary>
        public static void Prepare()
        {
            if (_ready || !Supported) return;
            _ready = true;

#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
            var channel = new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = Loc.Get("hub.title"),
                Importance = Importance.Default,
                Description = Loc.Get("chest.hint"),
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);

            // Android 13 и новее требует спросить разрешение в явном виде. Отказ —
            // нормальный исход: расписание просто не доедет до системы, а игра
            // работает как работала.
            var request = new PermissionRequest();
            if (request.Status == PermissionStatus.RequestPending) { /* ответит игрок */ }
#endif
        }

        /// <summary>
        /// Составить расписание заново. Зовётся при уходе игры в фон и при выходе:
        /// ровно тогда, когда известно и состояние наград, и то, что игрока уже нет.
        /// </summary>
        public static void Reschedule()
        {
            if (!Supported) return;

            Clear();

#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
            DateTime now = TimeService.LocalNow;

            // 1. Сундук. Напоминаем про следующий созревающий, а не про каждый:
            // три уведомления за вечер — это уже навязчивость.
            if (RewardsHub.ChestsReady < RewardTables.ChestMaxStored)
            {
                TimeSpan left = RewardsHub.ChestCooldown;
                if (left <= TimeSpan.Zero) left = TimeSpan.FromMinutes(5);
                Schedule(Loc.Get("notif.chestTitle"), Loc.Get("notif.chestBody"), now + left);
            }

            // 2. Награда за вход. Если сегодняшняя не забрана — вечером сегодня,
            // иначе вечером завтра.
            DateTime daily = RewardsHub.CanClaimDaily
                ? now.Date.AddHours(DailyReminderHour)
                : now.Date.AddDays(1).AddHours(DailyReminderHour);
            if (daily <= now) daily = daily.AddDays(1);
            Schedule(Loc.Get("notif.dailyTitle"), Loc.Get("notif.dailyBody"), daily);

            // 3. Новые задания — утром после смены суток по UTC, когда они уже
            // точно сменились.
            DateTime questsAt = (TimeService.UtcNow + TimeService.UntilNextDay).ToLocalTime().AddMinutes(1);
            Schedule(Loc.Get("notif.questTitle"), Loc.Get("notif.questBody"), questsAt);
#endif
        }

        /// <summary>Игра открыта — напоминания больше не нужны.</summary>
        public static void Clear()
        {
            if (!Supported) return;

#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#endif
        }

#if UNITY_ANDROID && BUBBLERISE_NOTIFICATIONS
        static void Schedule(string title, string body, DateTime when)
        {
            when = AvoidQuietHours(when);
            if (when <= TimeService.LocalNow) return;

            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = when,
                ShouldAutoCancel = true,
            };
            AndroidNotificationCenter.SendNotification(notification, ChannelId);
        }
#endif

        /// <summary>
        /// Сдвинуть ночное время на утро.
        ///
        /// Отдельный метод с явными границами, а не проверка на месте: будить игрока
        /// в три ночи из-за сундука на пять монет — самый быстрый способ получить
        /// отключённые уведомления и единицу в отзывах.
        /// </summary>
        public static DateTime AvoidQuietHours(DateTime when)
        {
            if (when.Hour >= QuietEndHour && when.Hour < QuietStartHour) return when;

            // После 23:00 — переносим на утро следующего дня, до 9:00 — того же.
            DateTime morning = when.Hour >= QuietStartHour
                ? when.Date.AddDays(1).AddHours(QuietEndHour)
                : when.Date.AddHours(QuietEndHour);
            return morning;
        }
    }
}
