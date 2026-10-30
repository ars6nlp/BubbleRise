using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Всё, что игра помнит между сессиями. Один плоский объект, а не россыпь
    /// разрозненных ключей: именно в таком виде состояние уезжает в облако Яндекса
    /// одним вызовом setData.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public int best;
        /// <summary>−1 означает «игрок не выбирал язык», тогда решает язык площадки.</summary>
        public int lang = -1;
        /// <summary>
        /// Язык площадки, на котором сделан выбор; −1 — неизвестен. Нужен, чтобы выбор
        /// не переезжал на чужую языковую версию сайта вместе с профилем.
        /// </summary>
        public int langOrigin = -1;
        public int mute;
        public int music = 1;
        /// <summary>Вибрация. По умолчанию включена — на телефоне это часть удара.</summary>
        public int vibration = 1;
        /// <summary>Щадящий режим эффектов. Остаётся ради старых профилей.</summary>
        public int calmFx = 1;
        /// <summary>
        /// Яркость эффектов: 0 тихо, 1 обычно, 2 ярко. −1 — игрок не выбирал, и
        /// тогда ступень выводится из calmFx (см. <see cref="Visuals.Level"/>).
        /// </summary>
        public int fxBrightness = -1;
        /// <summary>Сложность: 0 лёгкая, 1 обычная, 2 сложная. −1 — игрок не выбирал.</summary>
        public int difficulty = -1;
        /// <summary>Вступление показано.</summary>
        public int tutorial;
        public int skinBubble;
        public int skinGuardian;
        /// <summary>Битовая маска побеждённых боссов (по BossKind) — от неё зависят скины.</summary>
        public int bosses;
        /// <summary>Битовая маска встреченных боссов — от неё открываются статьи справочника.</summary>
        public int bossesMet;
        /// <summary>Кто выйдет следующим: ротация не начинается заново каждую партию.</summary>
        public int bossIndex;
        /// <summary>Сколько рыб отбито стражем за всё время — условие открытия скина.</summary>
        public int fishBlocked;
        /// <summary>Сколько обломков отбито стражем за всё время.</summary>
        public int debrisBlocked;
        /// <summary>Монеты. Зарабатываются только за уничтоженные препятствия.</summary>
        public int coins;
        /// <summary>
        /// Купленные скины пузырька — битовая маска по индексу в каталоге.
        ///
        /// long, а не int, потому что скинов больше тридцати двух: с 33-м каталог
        /// молча потерял бы покупку, и заметили бы это только игроки.
        /// </summary>
        public long ownedBubble;
        public long ownedGuardian;
    }

    /// <summary>
    /// Локальное хранилище и мост к облаку.
    ///
    /// PlayerPrefs остаются единственным источником правды внутри игры: их читают
    /// Loc, SkinStore и AudioManager, каждый в своё время и не зная друг о друге.
    /// Облако лишь наполняет их при старте и получает копию при изменениях —
    /// так ни одна подсистема не обязана уметь работать с сетью.
    ///
    /// Ключи собраны здесь целиком не ради порядка, а чтобы состав сохранения нельзя
    /// было случайно разойтись с составом облачной копии: требование площадки —
    /// между сессиями переживают и прогресс, и настройки.
    /// </summary>
    public static class SaveData
    {
        public const string BestKey = "bubblerise_best_depth";
        public const string LangKey = "bubblerise_lang";
        public const string LangOriginKey = "bubblerise_lang_origin";
        public const string MuteKey = "bubblerise_muted";
        public const string MusicKey = "bubblerise_music";
        public const string VibrationKey = "bubblerise_vibration";
        /// <summary>Щадящий режим: меньше вспышек и свечения.</summary>
        public const string CalmFxKey = "bubblerise_calm_fx";
        public const string FxBrightnessKey = "bubblerise_fx_brightness";
        /// <summary>
        /// «Без вспышек». Только на этом устройстве: в облачный профиль не
        /// входит, прогресс не трогает.
        /// </summary>
        public const string NoFlashKey = "bubblerise_no_flash";
        public const string DifficultyKey = "bubblerise_difficulty";
        /// <summary>Вступление уже показывали — второй раз не нужно.</summary>
        public const string TutorialKey = "bubblerise_tutorial";
        public const string SkinBubbleKey = "bubblerise_skin_bubble";
        public const string SkinGuardianKey = "bubblerise_skin_guardian";
        public const string BossesKey = "bubblerise_bosses";
        public const string BossesMetKey = "bubblerise_bosses_met";
        public const string BossIndexKey = "bubblerise_boss_index";
        public const string FishBlockedKey = "bubblerise_fish_blocked";
        public const string DebrisBlockedKey = "bubblerise_debris_blocked";
        public const string CoinsKey = "bubblerise_coins";
        public const string OwnedBubbleKey = "bubblerise_owned_bubble";
        public const string OwnedGuardianKey = "bubblerise_owned_guardian";
        /// <summary>Отметка, что старые трофеи с боссов уже перенесены в покупки.</summary>
        public const string BossGrantKey = "bubblerise_boss_granted";
        /// <summary>Последнее виденное время — защита от перевода часов назад.</summary>
        public const string LastSeenKey = "bubblerise_last_seen";
        /// <summary>Состояние системы наград одной строкой JSON.</summary>
        public const string RewardsKey = "bubblerise_rewards";

        /// <summary>
        /// Маски покупок лежат строкой: у PlayerPrefs нет SetLong, а скинов больше
        /// тридцати двух, и в int они уже не помещаются.
        /// </summary>
        public static long GetMask(string key)
        {
            string raw = PlayerPrefs.GetString(key, "0");
            return long.TryParse(raw, out long value) ? value : 0L;
        }

        public static void SetMask(string key, long value)
            => PlayerPrefs.SetString(key, value.ToString());

        public static SaveState Capture()
        {
            return new SaveState
            {
                best = PlayerPrefs.GetInt(BestKey, 0),
                lang = PlayerPrefs.HasKey(LangKey) ? PlayerPrefs.GetInt(LangKey) : -1,
                langOrigin = PlayerPrefs.HasKey(LangOriginKey) ? PlayerPrefs.GetInt(LangOriginKey) : -1,
                mute = PlayerPrefs.GetInt(MuteKey, 0),
                music = PlayerPrefs.GetInt(MusicKey, 1),
                vibration = PlayerPrefs.GetInt(VibrationKey, 1),
                calmFx = PlayerPrefs.GetInt(CalmFxKey, 1),
                fxBrightness = PlayerPrefs.HasKey(FxBrightnessKey)
                    ? PlayerPrefs.GetInt(FxBrightnessKey) : -1,
                difficulty = PlayerPrefs.HasKey(DifficultyKey) ? PlayerPrefs.GetInt(DifficultyKey) : -1,
                tutorial = PlayerPrefs.GetInt(TutorialKey, 0),
                skinBubble = PlayerPrefs.GetInt(SkinBubbleKey, 0),
                skinGuardian = PlayerPrefs.GetInt(SkinGuardianKey, 0),
                bosses = PlayerPrefs.GetInt(BossesKey, 0),
                bossesMet = PlayerPrefs.GetInt(BossesMetKey, 0),
                bossIndex = PlayerPrefs.GetInt(BossIndexKey, 0),
                fishBlocked = PlayerPrefs.GetInt(FishBlockedKey, 0),
                debrisBlocked = PlayerPrefs.GetInt(DebrisBlockedKey, 0),
                coins = PlayerPrefs.GetInt(CoinsKey, 0),
                ownedBubble = GetMask(OwnedBubbleKey),
                ownedGuardian = GetMask(OwnedGuardianKey),
            };
        }

        /// <summary>
        /// Записать облачное состояние в локальное хранилище.
        /// Рекорд именно сливается, а не перезаписывается: на новом устройстве облако
        /// старше локального прогресса, и потерять только что набранные метры нельзя.
        /// </summary>
        public static void Apply(SaveState state)
        {
            if (state == null) return;

            PlayerPrefs.SetInt(BestKey, Mathf.Max(state.best, PlayerPrefs.GetInt(BestKey, 0)));

            // lang = −1 значит, что игрок не выбирал язык и в облаке его нет;
            // затирать этим локальный выбор нельзя.
            if (state.lang >= 0)
            {
                PlayerPrefs.SetInt(LangKey, state.lang);

                // Происхождение относится именно к этому выбору, поэтому переезжает
                // вместе с ним. Если облако его не знает, старое локальное значение
                // нужно забыть: чужая пара «выбор + версия площадки» хуже, чем её
                // отсутствие.
                if (state.langOrigin >= 0) PlayerPrefs.SetInt(LangOriginKey, state.langOrigin);
                else PlayerPrefs.DeleteKey(LangOriginKey);
            }

            PlayerPrefs.SetInt(MuteKey, state.mute);
            PlayerPrefs.SetInt(MusicKey, state.music);
            PlayerPrefs.SetInt(VibrationKey, state.vibration);

            // Щадящий режим переносим как есть: это настройка доступности, и
            // «в облаке было выключено» — такой же осознанный выбор, как и
            // «включено». Единица по умолчанию в SaveState делает своё дело для
            // профилей, сохранённых до появления настройки.
            PlayerPrefs.SetInt(CalmFxKey, state.calmFx);

            // Яркость переносим, только если игрок её выбирал: иначе облако,
            // записанное до появления настройки, затёрло бы выбор на этом
            // устройстве значением по умолчанию.
            if (state.fxBrightness >= 0)
                PlayerPrefs.SetInt(FxBrightnessKey, Mathf.Clamp(state.fxBrightness, 0, 2));

            Visuals.Invalidate();

            // Сложность переносим, только если игрок её выбирал: иначе облако со
            // значением по умолчанию затёрло бы выбор, сделанный на этом устройстве.
            if (state.difficulty >= 0) PlayerPrefs.SetInt(DifficultyKey, state.difficulty);

            // Вступление показано хотя бы на одном устройстве — значит показано.
            PlayerPrefs.SetInt(TutorialKey, Mathf.Max(state.tutorial, PlayerPrefs.GetInt(TutorialKey, 0)));
            PlayerPrefs.SetInt(SkinBubbleKey, state.skinBubble);
            PlayerPrefs.SetInt(SkinGuardianKey, state.skinGuardian);

            // Победы сливаем побитово, а не перезаписываем: на новом устройстве
            // облако может быть беднее локального прогресса, и отнимать у игрока
            // уже открытый скин нельзя.
            PlayerPrefs.SetInt(BossesKey, state.bosses | PlayerPrefs.GetInt(BossesKey, 0));

            // Встречи сливаем так же побитово. Победа над боссом означает и встречу
            // с ним, поэтому старые профили, где этой маски ещё не было, получают
            // открытые статьи хотя бы на всех побеждённых.
            PlayerPrefs.SetInt(BossesMetKey,
                state.bossesMet | state.bosses | PlayerPrefs.GetInt(BossesMetKey, 0));
            PlayerPrefs.SetInt(BossIndexKey, state.bossIndex);

            // Счётчики отбитого сливаем максимумом, как и рекорд: на новом устройстве
            // облако может быть беднее локального прогресса, и отнимать уже заработанный
            // скин нельзя.
            PlayerPrefs.SetInt(FishBlockedKey,
                Mathf.Max(state.fishBlocked, PlayerPrefs.GetInt(FishBlockedKey, 0)));
            PlayerPrefs.SetInt(DebrisBlockedKey,
                Mathf.Max(state.debrisBlocked, PlayerPrefs.GetInt(DebrisBlockedKey, 0)));

            // Монеты сливаем максимумом — тем же правилом, что рекорд и счётчики.
            // Правило не идеально: если игрок потратил монеты локально, а облако
            // помнит баланс до покупки, трата вернётся. Но обратная ошибка хуже:
            // сложение дублировало бы заработок при каждой синхронизации, а выбор
            // «облако главнее» отнимал бы у игрока только что заработанное.
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(state.coins, PlayerPrefs.GetInt(CoinsKey, 0)));

            // Покупки — побитово, как победы над боссами: купленное не отнимают.
            SetMask(OwnedBubbleKey, state.ownedBubble | GetMask(OwnedBubbleKey));
            SetMask(OwnedGuardianKey, state.ownedGuardian | GetMask(OwnedGuardianKey));

            PlayerPrefs.Save();
        }

        public static string ToJson(SaveState state) => JsonUtility.ToJson(state);

        public static SaveState FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<SaveState>(json); }
            catch (Exception) { return null; }   // мусор в облаке не должен ронять запуск
        }
    }
}
