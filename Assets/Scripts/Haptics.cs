using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Рисунок вибрации. У каждой стихии свой — это третий канал, которым скины
    /// отличаются друг от друга, после вида и звука.
    /// </summary>
    public enum HapticPattern
    {
        /// <summary>Ничего. Для скинов без отклика и когда вибрация выключена.</summary>
        None = 0,
        /// <summary>Огонь: короткие частые импульсы.</summary>
        Flame,
        /// <summary>Лёд: один чистый щелчок.</summary>
        Ice,
        /// <summary>Электричество: быстрая серия.</summary>
        Shock,
        /// <summary>Кислота: дрожащая цепочка слабых.</summary>
        Acid,
        /// <summary>Тень: один низкий долгий.</summary>
        Shadow,
        /// <summary>Золото: двойной, тяжёлый.</summary>
        Gold,
        /// <summary>Космос: нарастающая тройка.</summary>
        Cosmos,
        /// <summary>Кристалл: два звонких подряд.</summary>
        Crystal,
        /// <summary>Вода: один мягкий.</summary>
        Water,
        /// <summary>Призрак: едва заметное касание.</summary>
        Ghost,
        /// <summary>Радуга: переливчатая четвёрка.</summary>
        Prism,
        /// <summary>Урон: один длинный и неприятный.</summary>
        Hurt,
        /// <summary>Награда: лёгкий двойной.</summary>
        Reward,
    }

    /// <summary>
    /// Вибрация на Android.
    ///
    /// **Почему своими руками, а не Handheld.Vibrate.** Единственный штатный способ
    /// в Unity — <c>Handheld.Vibrate()</c>, и он всегда даёт одну и ту же долгую
    /// вибрацию примерно на полсекунды. Для «у огня частые импульсы, у льда один
    /// щелчок» этого не хватает совсем: все скины ощущались бы одинаково, а сама
    /// вибрация — как сбой. Поэтому здесь прямой вызов системного Vibrator через
    /// JNI, с длительностями в миллисекундах и собственным рисунком у каждой
    /// стихии.
    ///
    /// **Почему без пакета.** Готовые решения для вибрации тянут зависимость ради
    /// одного системного вызова. Здесь нужен ровно <c>vibrate(long[], int)</c> —
    /// он есть в Android с четвёртой версии, и обёртка занимает двадцать строк.
    ///
    /// **Ограничения, принятые сознательно.** На Android 8 и новее у вибратора
    /// есть амплитуда (VibrationEffect), и рисунки можно было бы делать мягче.
    /// Здесь используется только длительность и паузы: это работает на всех
    /// версиях одинаково, а разницу между «частые короткие» и «один долгий» слышно
    /// пальцем и без амплитуды.
    ///
    /// На других площадках (WebGL у Яндекса, редактор) класс молчит: вибрации там
    /// либо нет, либо она требует жеста пользователя в тот же кадр.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Рисунки: пауза, вибрация, пауза, вибрация… в миллисекундах.</summary>
        static readonly long[][] Patterns =
        {
            null,                                   // None
            new long[] { 0, 12, 28, 12, 28, 12 },   // Flame — три коротких толчка
            new long[] { 0, 26 },                   // Ice — один чистый
            new long[] { 0, 8, 14, 8, 14, 8, 14, 8 },  // Shock — быстрая серия
            new long[] { 0, 10, 40, 10, 40, 10, 40, 10 }, // Acid — дрожь с паузами
            new long[] { 0, 90 },                   // Shadow — низкий долгий
            new long[] { 0, 35, 60, 45 },           // Gold — тяжёлый двойной
            new long[] { 0, 10, 30, 20, 30, 40 },   // Cosmos — нарастающая тройка
            new long[] { 0, 14, 22, 14 },           // Crystal — два звонких
            new long[] { 0, 20 },                   // Water — один мягкий
            new long[] { 0, 6 },                    // Ghost — едва заметный
            new long[] { 0, 9, 18, 9, 18, 9, 18, 9 },  // Prism — переливчатая четвёрка
            new long[] { 0, 130 },                  // Hurt — длинный неприятный
            new long[] { 0, 18, 50, 28 },           // Reward — лёгкий двойной
        };

        /// <summary>
        /// Не чаще, чем раз в столько секунд. Страж бьёт несколько раз в секунду, и
        /// без ограничения телефон превратился бы в одну сплошную дрожь — заодно
        /// это заметно сажает батарею.
        /// </summary>
        const float MinInterval = 0.09f;

        static float _lastAt = -10f;
        static bool _probed;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static bool _hasVibrator;
#endif

        /// <summary>Включена ли вибрация в настройках.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(SaveData.VibrationKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(SaveData.VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Есть ли вибратор на этом устройстве — настройкам нужно знать.</summary>
        public static bool Supported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Probe();
                return _hasVibrator;
#else
                return false;
#endif
            }
        }

        public static void Toggle() => Enabled = !Enabled;

        /// <summary>Сыграть рисунок. Молча ничего не делает там, где вибрации нет.</summary>
        public static void Play(HapticPattern pattern)
        {
            if (pattern == HapticPattern.None || !Enabled) return;

            // Ограничение по времени — до любой работы с JNI: именно частые вызовы
            // и стоят дорого.
            if (Time.unscaledTime - _lastAt < MinInterval) return;
            _lastAt = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            Probe();
            if (!_hasVibrator) return;

            long[] timings = Patterns[(int)pattern];
            if (timings == null) return;

            try
            {
                // −1 значит «не повторять». Повтор здесь не нужен никогда, а
                // забытый повтор оставил бы телефон вибрировать до перезапуска.
                _vibrator.Call("vibrate", timings, -1);
            }
            catch (System.Exception)
            {
                // Производители меняют поведение вибратора чаще, чем хотелось бы.
                // Вибрация — украшение; ронять из-за неё игру нельзя.
                _hasVibrator = false;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void Probe()
        {
            if (_probed) return;
            _probed = true;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    _hasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
                }
            }
            catch (System.Exception)
            {
                _hasVibrator = false;
            }
        }
#else
        // Вне Android зондировать нечего, но флаг обязан читаться и здесь:
        // иначе компилятор справедливо ругается на поле, которому присваивают и
        // которое никто не смотрит.
        static void Probe()
        {
            if (_probed) return;
            _probed = true;
        }
#endif
    }
}
