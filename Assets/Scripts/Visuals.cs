using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Яркость эффектов. Три ступени, и ни одна не трогает размер.</summary>
    public enum FxBrightness
    {
        /// <summary>Тихо: тусклее и реже. Прежний «щадящий режим».</summary>
        Quiet = 0,
        /// <summary>Обычно: по умолчанию.</summary>
        Normal = 1,
        /// <summary>Ярко: насыщеннее, но в тех же пределах безопасности.</summary>
        Bright = 2,
    }

    /// <summary>
    /// Яркость эффектов и щадящий режим.
    ///
    /// **Что здесь изменилось и почему.** Раньше «Меньше вспышек и эффектов»
    /// резало всё подряд: яркость, количество частиц вдвое, амплитуду движения.
    /// Это ошибка, и она стоила игре главного — в щадящем режиме (а он включён у
    /// большинства) эффекты скинов переставали читаться вовсе.
    ///
    /// Теперь разделено honestly:
    ///
    /// * **Яркость, свечение, частота мерцаний** — вот что вредит глазам,
    ///   и только это настройка и убавляет. Тряски камеры нет ни на одной ступени.
    /// * **Размер, форма, число слоёв, длина молний, силуэт** — не трогаются
    ///   никогда и ни одной ступенью. Их нет среди чисел этого класса вовсе, и
    ///   это не случайность: <see cref="EnvelopeShape"/> не имеет доступа к
    ///   размеру отсюда, поэтому урезать его не может даже по ошибке.
    /// * **Плотность частиц** убавляется максимум на четверть, а не вдвое:
    ///   половина — это уже другой эффект, а не тот же потише.
    ///
    /// **Почему три ступени, а не ползунок.** Ползунок заставляет подбирать
    /// число, глядя на статичный экран настроек, где эффекта не видно. Три
    /// понятных слова выбираются один раз и навсегда.
    ///
    /// Класс статический, как Wallet и Difficulty рядом: числа нужны эффектам,
    /// камере и интерфейсу, и протягивать их через полдюжины Init значило бы
    /// тащить настройку туда, где она больше ничего не делает.
    /// </summary>
    public static class Visuals
    {
        /// <summary>Настройка изменилась — эффектам пора пересчитать свои числа.</summary>
        public static event Action Changed;

        static bool _loaded;
        static FxBrightness _level = FxBrightness.Normal;

        /// <summary>
        /// Текущая ступень яркости.
        ///
        /// **Перенос старых сохранений.** Ключа яркости нет — смотрим на старый
        /// ключ щадящего режима:
        ///
        /// * игрок его осознанно включал (ключ записан и равен единице) — значит
        ///   просил потише, и он получает «Тихо». Отобрать это молча нельзя: о
        ///   боли в глазах просят не ради красоты;
        /// * во всех остальных случаях, включая «никогда не открывал настройки», —
        ///   «Обычно». Эффекты теперь крупные, но не резкие: правила безопасности
        ///   из них вынесены в сам код и действуют на всех ступенях.
        /// </summary>
        public static FxBrightness Level
        {
            get
            {
                if (!_loaded)
                {
                    if (PlayerPrefs.HasKey(SaveData.FxBrightnessKey))
                    {
                        _level = (FxBrightness)Mathf.Clamp(
                            PlayerPrefs.GetInt(SaveData.FxBrightnessKey, 1), 0, 2);
                    }
                    else
                    {
                        bool choseCalm = PlayerPrefs.HasKey(SaveData.CalmFxKey)
                                         && PlayerPrefs.GetInt(SaveData.CalmFxKey, 1) != 0;
                        _level = choseCalm ? FxBrightness.Quiet : FxBrightness.Normal;
                    }
                    _loaded = true;
                }
                return _level;
            }
            set
            {
                _level = value;
                _loaded = true;
                PlayerPrefs.SetInt(SaveData.FxBrightnessKey, (int)value);

                // Старый ключ остаётся живым: его читают облачное сохранение и
                // игроки, вернувшиеся со старой версии. «Тихо» — это он и есть.
                PlayerPrefs.SetInt(SaveData.CalmFxKey, value == FxBrightness.Quiet ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Следующая ступень по кругу — одной кнопкой в настройках.</summary>
        public static void CycleLevel()
            => Level = (FxBrightness)(((int)Level + 1) % 3);

        /// <summary>
        /// Щадящий режим включён. Это ровно «Тихо» и ничто иное: второго
        /// независимого выключателя нет, иначе они противоречили бы друг другу.
        /// </summary>
        public static bool Calm
        {
            get => Level == FxBrightness.Quiet;
            set => Level = value ? FxBrightness.Quiet : FxBrightness.Normal;
        }

        public static void Toggle() => Calm = !Calm;

        static bool _noFlashLoaded;
        static bool _noFlashes = true;

        /// <summary>
        /// «Без вспышек»: отдельный переключатель поверх ступени яркости.
        ///
        /// Включён по умолчанию — и у новых игроков, и у тех, кто играл раньше:
        /// ключа до этой версии не было ни у кого, и отсутствие ключа значит «вкл».
        /// Выключить его можно только руками в настройках.
        ///
        /// Что он делает: попадание по боссу — контур вместо подмеса по телу,
        /// колесо наград без вращения диска, оболочки вдвое прозрачнее. Размер и
        /// силуэт не трогает, как и ступени яркости.
        /// </summary>
        public static bool NoFlashes
        {
            get
            {
                if (!_noFlashLoaded)
                {
                    _noFlashes = PlayerPrefs.GetInt(SaveData.NoFlashKey, 1) != 0;
                    _noFlashLoaded = true;
                }
                return _noFlashes;
            }
            set
            {
                _noFlashes = value;
                _noFlashLoaded = true;
                PlayerPrefs.SetInt(SaveData.NoFlashKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Облако прислало профиль — перечитать с диска.</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _noFlashLoaded = false;
            Changed?.Invoke();
        }

        // ------------------------------------------------------- готовые числа

        /// <summary>
        /// Множитель числа частиц. На «Тихо» минус четверть — не больше: половина
        /// превращала эффект в другой эффект, а не в тот же потише.
        /// </summary>
        public static float Particles => Level == FxBrightness.Quiet ? 0.75f : 1f;

        /// <summary>
        /// Множитель прозрачности частиц. На «Тихо» минус четверть (E-10):
        /// прежде «Тихо» частицы не трогало вовсе.
        /// </summary>
        public static float ParticleAlpha => Level == FxBrightness.Quiet ? 0.75f : 1f;

        /// <summary>Множитель прозрачности накладок статусов. На «Тихо» минус четверть.</summary>
        public static float StatusAlpha => Level == FxBrightness.Quiet ? 0.75f : 1f;

        /// <summary>Множитель яркости свечения и аур.</summary>
        public static float Glow
        {
            get
            {
                switch (Level)
                {
                    case FxBrightness.Quiet: return 0.62f;
                    case FxBrightness.Bright: return 1f;
                    default: return 0.85f;
                }
            }
        }

        /// <summary>
        /// Множитель прозрачности оболочки столкновения. Отдельно от
        /// <see cref="Glow"/>: свечение ауры и тело эффекта — разные вещи, и
        /// гасить плотную заливку так же сильно, как ореол, незачем.
        /// </summary>
        public static float EnvelopeAlpha
        {
            get
            {
                // «Без вспышек» гасит оболочку вдвое поверх ступени: размер и
                // силуэт те же, плотность ниже.
                float flash = NoFlashes ? 0.5f : 1f;
                switch (Level)
                {
                    case FxBrightness.Quiet: return 0.72f * flash;
                    case FxBrightness.Bright: return 1.08f * flash;
                    default: return flash;
                }
            }
        }

        /// <summary>
        /// Множитель частоты перестройки дуг оболочки. На «Тихо» дуги меняют
        /// форму раз в секунду вместо двух.
        /// </summary>
        public static float ArcRate => Level == FxBrightness.Quiet ? 0.5f : 1f;

        /// <summary>Множитель амплитуды пульсаций и дрожаний.</summary>
        public static float Motion => Level == FxBrightness.Quiet ? 0.5f : 1f;

        /// <summary>
        /// Множитель подмеса цвета к боссу при попадании (<see cref="HitTint"/>).
        /// На «Тихо» вдвое меньше. «Ярко» его не поднимает: потолок 25 % — это
        /// правило для глаз, а не вкус.
        /// </summary>
        public static float BossFlash => Level == FxBrightness.Quiet ? 0.5f : 1f;

        /// <summary>
        /// Нижний предел для «базового образа» скина.
        ///
        /// Форма, материал, движение, лёгкий след и мягкая аура — то, по чему
        /// скины отличаются друг от друга за секунду. Настройка вправе их
        /// приглушить, но не стереть: иначе в щадящем режиме все тридцать шесть
        /// скинов выглядят одинаково, и покупать их становится незачем.
        ///
        /// 0.55 — это «вдвое тише, но всё ещё видно». Ниже уже начинает пропадать.
        /// </summary>
        public const float BaseFloor = 0.55f;

        /// <summary>Ключ подписи ступени для настроек.</summary>
        public static string LevelKey(FxBrightness level)
        {
            switch (level)
            {
                case FxBrightness.Quiet: return "settings.fxQuiet";
                case FxBrightness.Bright: return "settings.fxBright";
                default: return "settings.fxNormal";
            }
        }

        public static string Key => Calm ? "settings.calmOn" : "settings.calmOff";
    }
}
