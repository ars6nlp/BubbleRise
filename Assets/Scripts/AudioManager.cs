using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Весь звук синтезируется кодом при загрузке: ни одного аудиофайла в проекте.
    /// Причины две — вес WebGL-сборки (в билде это 0 байт) и авторские права:
    /// сторонних сэмплов нет вообще, права на всё содержимое наши.
    ///
    /// Тракт: генератор -> ADSR -> фильтр нижних частот (подводная приглушённость)
    /// -> короткая ревербация -> нормализация. Зацикленные дорожки (музыка, эмбиент)
    /// сшиваются без щелчка на стыке.
    ///
    /// В браузере звук стартует только после жеста пользователя — фон включается
    /// на первом нажатии кнопки (StartAmbience).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int SampleRate = 22050;
        const int SourcePoolSize = 8;
        const string MuteKey = SaveData.MuteKey;

        GameConfig _cfg;

        AudioSource[] _pool;
        int _cursor;

        // Две дорожки под музыку: спокойная и боевая переключаются кроссфейдом,
        // поэтому нужны два источника, звучащих одновременно во время перехода.
        AudioSource _musicA, _musicB;
        AudioSource _activeMusic, _fadingOutMusic;
        AudioSource _ambientSource;

        float _activeGain;
        float _fadeTimer, _fadeDuration;
        bool _fading;
        bool _stopping;
        float _stopFromVolume;

        /// <summary>
        /// Какая тема уместна прямо сейчас по ходу игры. Ведётся независимо от того,
        /// звучит ли музыка на самом деле: она может быть выключена в настройках, и
        /// при обратном включении заиграть должна именно эта, а не та, что осталась
        /// от прошлого раза. Пусто — тишина стоит намеренно.
        /// </summary>
        AudioClip _wantedMusic;
        float _wantedGain;

        AudioClip _deflect, _reflect, _coralBreak, _hurt, _bossHit, _bossDie, _bossAppear,
                  _win, _gameOver, _reward, _uiClick, _guardianReady, _repel, _milestone,
                  _heartbeat, _blip, _inkSplat, _coin,
                  _hitSpark, _hitIce, _hitWater, _hitMetal;
        AudioClip _music, _bossMusic, _finaleMusic, _ambient;
        AudioClip _mantaRoar, _mantaBolt, _mantaSlam, _mantaGill, _mantaShock;

        bool _ambienceStarted;

        /// <summary>Глобальное выключение звука игроком. Сохраняется между сессиями.</summary>
        public bool Muted { get; private set; }

        // ------------------------------------------------------------- запуск

        public void Init(GameConfig cfg)
        {
            _cfg = cfg;
            Muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;

            // Музыка тоже настройка игрока, а не только значение из конфига:
            // площадка требует, чтобы настройки переживали перезапуск.
            if (PlayerPrefs.HasKey(SaveData.MusicKey))
                cfg.musicEnabled = PlayerPrefs.GetInt(SaveData.MusicKey) != 0;

            _pool = new AudioSource[SourcePoolSize];
            for (int i = 0; i < SourcePoolSize; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }

            _musicA = CreateLoopSource("MusicA");
            _musicB = CreateLoopSource("MusicB");
            _ambientSource = CreateLoopSource("Ambient");

            BuildClips();

            _ambientSource.clip = _ambient;
            _activeGain = cfg.musicVolume;
            ApplyVolumes();
        }

        AudioSource CreateLoopSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            return src;
        }

        /// <summary>Вызывается после первого клика игрока: до жеста браузер звук не пустит.</summary>
        public void StartAmbience()
        {
            if (_ambienceStarted) return;
            _ambienceStarted = true;

            if (_ambient != null) _ambientSource.Play();
            PlayCalmMusic(0.4f);
            ApplyVolumes();
        }

        /// <summary>Спокойная тема подъёма.</summary>
        public void PlayCalmMusic(float crossfade = 1.2f) => SwitchMusic(_music, _cfg.musicVolume, crossfade);

        /// <summary>Тревожная тема боя с боссом.</summary>
        public void PlayBossMusic(float crossfade = 0.45f) => SwitchMusic(_bossMusic, _cfg.bossMusicVolume, crossfade);

        /// <summary>Тема финального босса. Отдельная: обычная боевая для него слишком бодрая.</summary>
        public void PlayFinaleMusic(float crossfade = 0.6f)
            => SwitchMusic(_finaleMusic != null ? _finaleMusic : _bossMusic, _cfg.finaleMusicVolume, crossfade);

        /// <summary>
        /// Увести музыку в тишину. Нужна перед выходом «Великой тени»: несколько
        /// секунд без музыки работают сильнее любого звука, который можно сыграть.
        ///
        /// Это не SwitchMusic(null): тот меняет одну дорожку на другую, а здесь нужно
        /// именно затухание в ничто, без второй дорожки.
        /// </summary>
        public void StopMusic(float fade = 0.8f)
        {
            // Тишина здесь намеренная, поэтому забываем и намерение: иначе игрок,
            // щёлкнув музыкой в настройках, вернул бы тему поверх задуманной паузы.
            _wantedMusic = null;

            if (_activeMusic == null || _stopping) return;

            if (_fadingOutMusic != null) { _fadingOutMusic.Stop(); _fadingOutMusic = null; }

            _stopping = true;
            _fading = false;
            _stopFromVolume = _activeMusic.volume;
            _fadeDuration = Mathf.Max(0.05f, fade);
            _fadeTimer = 0f;
        }

        public void PlayMantaRoar() => Play(_mantaRoar, 1f);

        // Звуки ската идут пачками, поэтому у каждого свой запас громкости и лёгкий
        // разброс высоты: одинаковые повторы на третьем десятке начинают раздражать.
        public void PlayMantaBolt()
            => Play(_mantaBolt, 0.5f, UnityEngine.Random.Range(0.92f, 1.1f));

        public void PlayMantaSlam()
            => Play(_mantaSlam, 0.95f, UnityEngine.Random.Range(0.94f, 1.06f));

        public void PlayMantaGill()
            => Play(_mantaGill, 0.85f, UnityEngine.Random.Range(0.97f, 1.04f));

        public void PlayMantaShock()
            => Play(_mantaShock, 0.8f, UnityEngine.Random.Range(0.95f, 1.06f));

        void SwitchMusic(AudioClip clip, float gain, float crossfade)
        {
            if (clip == null || _cfg == null) return;

            // Намерение запоминаем ДО всех проверок. Музыка может быть выключена в
            // настройках или ещё не разрешена браузером, но игра всё это время
            // продолжает сообщать, какая тема сейчас уместна. Без этой памяти
            // включение музыки посреди боя с боссом возвращало спокойную тему
            // подъёма — ту, что игралась в последний раз, когда музыка работала.
            _wantedMusic = clip;
            _wantedGain = gain;

            if (!_ambienceStarted || !_cfg.musicEnabled) return;

            // уже играет нужная тема — только поправим громкость
            if (_activeMusic != null && _activeMusic.clip == clip)
            {
                _activeGain = gain;
                return;
            }

            _stopping = false;   // музыка возвращается — отменяем затухание в тишину

            var next = _activeMusic == _musicA ? _musicB : _musicA;
            next.clip = clip;
            next.volume = 0f;
            next.time = 0f;
            next.Play();

            _fadingOutMusic = _activeMusic;
            _activeMusic = next;
            _activeGain = gain;
            _fadeDuration = Mathf.Max(0.05f, crossfade);
            _fadeTimer = 0f;
            _fading = true;
        }

        void Update()
        {
            if (_activeMusic == null) return;

            if (_stopping)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                float fade = Mathf.Clamp01(_fadeTimer / _fadeDuration);
                _activeMusic.volume = _stopFromVolume * (1f - fade);

                if (fade >= 1f)
                {
                    _activeMusic.Stop();
                    _activeMusic = null;
                    _stopping = false;
                }
                return;
            }

            float target = MusicTarget();

            if (!_fading)
            {
                _activeMusic.volume = target;
                return;
            }

            _fadeTimer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_fadeTimer / _fadeDuration);

            _activeMusic.volume = target * k;
            if (_fadingOutMusic != null) _fadingOutMusic.volume = target * (1f - k);

            if (k >= 1f)
            {
                _fading = false;
                if (_fadingOutMusic != null)
                {
                    _fadingOutMusic.Stop();
                    _fadingOutMusic = null;
                }
            }
        }

        float MusicTarget()
            => (_cfg != null && _cfg.audioEnabled && _cfg.musicEnabled && !Muted) ? _activeGain : 0f;

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public void ToggleMute() => SetMuted(!Muted);

        /// <summary>Включить или выключить музыку из настроек, не трогая эффекты.</summary>
        public void SetMusicEnabled(bool enabled)
        {
            if (_cfg != null) _cfg.musicEnabled = enabled;
            PlayerPrefs.SetInt(SaveData.MusicKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (!enabled)
            {
                if (_musicA != null) _musicA.Stop();
                if (_musicB != null) _musicB.Stop();
                _activeMusic = null;
                _fadingOutMusic = null;
                _fading = false;
                // Затухание в тишину тоже отменяем: иначе флаг остаётся поднятым,
                // и следующий StopMusic решит, что он уже идёт, и не сработает.
                _stopping = false;
            }
            else if (_ambienceStarted && _wantedMusic != null)
            {
                // Возвращаем ту тему, которая уместна сейчас, а не начальную.
                // Если _wantedMusic пуст, тишина стоит намеренно — так обставлен
                // выход «Великой тени», и включение музыки её ломать не должно.
                SwitchMusic(_wantedMusic, _wantedGain, 0.4f);
            }
        }

        void ApplyVolumes()
        {
            bool on = _cfg != null && _cfg.audioEnabled && !Muted;
            _ambientSource.volume = on ? _cfg.ambientVolume : 0f;
            if (_activeMusic != null && !_fading) _activeMusic.volume = MusicTarget();

            // Глушим и то, что уже звучит. Play() отсекает только новые эффекты, и без
            // этого длинный звук — появление босса, взрыв — продолжал играть секунду
            // после нажатия на глушилку. Игрок в этот момент считает, что кнопка не
            // сработала, и жмёт её второй раз.
            if (!on && _pool != null)
                foreach (var src in _pool)
                    if (src != null && src.isPlaying) src.Stop();
        }

        // -------------------------------------------------------- воспроизведение

        void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            if (clip == null || _cfg == null || !_cfg.audioEnabled || Muted) return;

            _cursor = (_cursor + 1) % _pool.Length;
            var src = _pool[_cursor];
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * _cfg.sfxVolume);
            src.pitch = pitch;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.Play();
        }

        /// <summary>Панорама по положению события на экране: -1 слева, +1 справа.</summary>
        float PanFor(Vector2 worldPosition, float halfWidth)
            => halfWidth > 0.01f ? Mathf.Clamp(worldPosition.x / halfWidth, -1f, 1f) * 0.7f : 0f;

        public void PlayDeflect(Vector2 at = default, float halfWidth = 0f)
            => Play(_deflect, 0.85f, UnityEngine.Random.Range(0.92f, 1.10f), PanFor(at, halfWidth));

        public void PlayReflect() => Play(_reflect, 0.95f, UnityEngine.Random.Range(0.96f, 1.06f));

        public void PlayCoralBreak(Vector2 at = default, float halfWidth = 0f)
            => Play(_coralBreak, 0.95f, UnityEngine.Random.Range(0.88f, 1.12f), PanFor(at, halfWidth));

        public void PlayHurt() => Play(_hurt, 1f);
        public void PlayBossHit() => Play(_bossHit, 0.9f);
        public void PlayBossDie() => Play(_bossDie, 1f);
        public void PlayBossAppear() => Play(_bossAppear, 1f);
        public void PlayWin() => Play(_win, 0.9f);
        public void PlayGameOver() => Play(_gameOver, 0.9f);
        public void PlayReward() => Play(_reward, 0.9f);

        /// <summary>
        /// Монета. Громкость низкая и высота случайная: за секунду их бывает
        /// несколько, и одинаковый звук подряд превращается в треск.
        /// </summary>
        /// <summary>
        /// Звук удара стража. Семью выбирает рисунок эффекта, а не скин: на слух
        /// лёд отличается от искры, а «иней» от «призмы» — нет.
        /// </summary>
        /// <param name="pitch">
        /// Высота тона стихии. Семья звука выбирается рисунком эффекта, а высота —
        /// стихией: так лёд и кристалл звучат родственно, но не одинаково, а
        /// огонь и золото не спутать вовсе. Своих сэмплов под каждую стихию в
        /// проекте нет и быть не может — звуки синтезируются кодом, — поэтому
        /// различие делается высотой и громкостью, как и просили.
        /// </param>
        public void PlayImpact(ImpactPattern pattern, float pitch = 1f,
                               Vector2 at = default, float halfWidth = 0f)
        {
            AudioClip clip;
            switch (pattern)
            {
                case ImpactPattern.Sparks:
                case ImpactPattern.Arc:
                case ImpactPattern.Flash:
                    clip = _hitSpark; break;

                case ImpactPattern.Frost:
                case ImpactPattern.Shatter:
                case ImpactPattern.Gleam:
                    clip = _hitIce; break;

                case ImpactPattern.Grit:
                case ImpactPattern.Dust:
                case ImpactPattern.Void:
                    clip = _hitMetal; break;

                default:
                    clip = _hitWater; break;
            }

            // Разброс оставляем: без него два удара подряд сливаются в один
            // механический щелчок. Но теперь он вокруг высоты стихии, а не вокруг
            // единицы.
            float p = Mathf.Clamp(pitch * UnityEngine.Random.Range(0.96f, 1.05f), 0.3f, 3f);

            // Низкие стихии звучат глуше, но не тише: громкость чуть растёт с
            // понижением тона, иначе тень и камень просто пропадают из слышимости.
            float volume = 0.33f * Mathf.Lerp(1.18f, 0.92f, Mathf.InverseLerp(0.4f, 1.9f, pitch));

            Play(clip, volume, p, PanFor(at, halfWidth));
        }

        public void PlayCoin() => Play(_coin, 0.42f, UnityEngine.Random.Range(0.92f, 1.12f));
        public void PlayUiClick() => Play(_uiClick, 0.7f);

        /// <summary>
        /// Щелчок указателя колеса о границу сектора.
        ///
        /// Тот же клип, что у кнопок, но втрое тише и выше: за вращение их
        /// набегает пара десятков, и звук кнопки в таком темпе превратился бы в
        /// треск. Разброс высоты — чтобы подряд идущие щелчки не слипались.
        /// </summary>
        public void PlayWheelTick()
            => Play(_uiClick, 0.22f, UnityEngine.Random.Range(1.5f, 1.75f));
        public void PlayGuardianReady() => Play(_guardianReady, 0.30f, UnityEngine.Random.Range(0.97f, 1.05f));
        public void PlayRepel() => Play(_repel, 0.55f);
        public void PlayMilestone() => Play(_milestone, 0.65f);
        public void PlayHeartbeat() => Play(_heartbeat, 0.55f);
        public void PlayInkSplat() => Play(_inkSplat, 1f, UnityEngine.Random.Range(0.94f, 1.04f));

        public void PlayBubbleBlip()
            => Play(_blip, 0.22f, UnityEngine.Random.Range(0.8f, 1.35f), UnityEngine.Random.Range(-0.4f, 0.4f));

        // ------------------------------------------------------------- синтез

        void BuildClips()
        {
            // --- защита
            _deflect = Clip("sfx_deflect", Post(
                Render(0.16f, (t, s) => Sine(s, Mathf.Lerp(620f, 1250f, t)) * Decay(t, 6f), 0.12f),
                lowPass: 4200f, reverb: 0.22f, peak: 0.85f));

            _reflect = Clip("sfx_reflect", Post(
                Render(0.20f, (t, s) =>
                    (Sine(s, Mathf.Lerp(880f, 1900f, t * t)) * 0.8f + Square(s, Mathf.Lerp(440f, 950f, t)) * 0.2f)
                    * Decay(t, 5f), 0.14f),
                lowPass: 5200f, reverb: 0.26f, peak: 0.9f));

            // хруст кораллового блока: шумовой всплеск плюс низкий удар
            _coralBreak = Clip("sfx_coral", Post(
                Render(0.26f, (t, s) =>
                    Noise() * Decay(t, 14f) * 0.8f +
                    Sine(s, Mathf.Lerp(150f, 70f, t)) * Decay(t, 11f) * 0.7f, 0.16f),
                lowPass: 3400f, reverb: 0.24f, peak: 0.9f));

            // --- урон и босс
            _hurt = Clip("sfx_hurt", Post(
                Render(0.34f, (t, s) => (Saw(s, Mathf.Lerp(400f, 105f, t)) * 0.85f + Noise() * 0.15f) * Decay(t, 4.5f), 0.2f),
                lowPass: 2400f, reverb: 0.28f, peak: 0.95f));

            _bossHit = Clip("sfx_bosshit", Post(
                Render(0.20f, (t, s) => (Square(s, 235f) * 0.55f + Noise() * 0.45f) * Decay(t, 7f), 0.15f),
                lowPass: 3000f, reverb: 0.25f, peak: 0.9f));

            _bossDie = Clip("sfx_bossdie", Post(
                Render(0.95f, (t, s) =>
                {
                    float vibrato = Mathf.Sin(s * 11f) * 12f;
                    return (Saw(s, Mathf.Lerp(320f, 48f, t) + vibrato) * 0.7f + Noise() * 0.3f) * Decay(t, 2.6f);
                }, 0.5f),
                lowPass: 1900f, reverb: 0.34f, peak: 1f));

            // рык появления: две расстроенные пилы плюс саб
            _bossAppear = Clip("sfx_bossappear", Post(
                Render(1.5f, (t, s) =>
                {
                    float wob = Mathf.Sin(s * 5.5f) * 2.5f;
                    float growl = Saw(s, 54f + wob) * 0.5f + Saw(s, 57.5f - wob) * 0.5f;
                    float sub = Sine(s, 38f) * 0.6f;
                    float env = Adsr(t, 0.18f, 0.25f, 0.75f, 0.4f);
                    return (growl + sub) * env;
                }, 0.6f),
                lowPass: 780f, reverb: 0.38f, peak: 1f));

            // --- мелодические сигналы
            _win = Clip("sfx_win", Post(
                Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.16f, 5.5f),
                lowPass: 6000f, reverb: 0.30f, peak: 0.85f));

            _reward = Clip("sfx_reward", Post(
                Arpeggio(new[] { 440f, 587.33f, 739.99f, 880f, 1174.7f }, 0.13f, 5f),
                lowPass: 6500f, reverb: 0.32f, peak: 0.85f));

            _gameOver = Clip("sfx_gameover", Post(
                Arpeggio(new[] { 392f, 329.63f, 261.63f, 196f }, 0.28f, 2.6f),
                lowPass: 2000f, reverb: 0.40f, peak: 0.85f));

            _milestone = Clip("sfx_milestone", Post(
                Render(0.55f, (t, s) =>
                {
                    float f = t < 0.35f ? 783.99f : 1174.7f;
                    float local = t < 0.35f ? t / 0.35f : (t - 0.35f) / 0.65f;
                    // колокольчик: основной тон плюс вторая гармоника
                    return (Sine(s, f) * 0.75f + Sine(s, f * 2.01f) * 0.25f) * Decay(local, 4.5f);
                }, 0.35f),
                lowPass: 7000f, reverb: 0.35f, peak: 0.8f));

            // Монета: два коротких тона подряд, второй выше. Один тон читается как
            // клик интерфейса, а пара — как «звякнуло». Длина 0.1 с не случайна:
            // препятствия ломаются очередями, и всё, что длиннее, слипается в гул.
            _coin = Clip("sfx_coin", Post(
                Render(0.1f, (t, s) =>
                {
                    float f = t < 0.045f ? 1180f : 1760f;
                    float local = t < 0.045f ? t / 0.045f : (t - 0.045f) / 0.055f;
                    return (Sine(s, f) * 0.8f + Sine(s, f * 2.5f) * 0.2f) * Decay(local, 9f);
                }, 0.08f),
                lowPass: 9000f, reverb: 0.18f, peak: 0.7f));

            // Удары стража. Четыре семьи на все скины: отдельный звук на каждый из
            // тридцати шести был бы неотличим на слух, а вес сборки вырос бы зря.
            // Все короткие — удары идут очередями, и всё длиннее 0.1 с слипается.
            _hitSpark = Clip("sfx_hit_spark", Post(
                Render(0.08f, (t, s) => (Noise() * 0.6f + Sine(s, 2400f) * 0.4f) * Decay(t, 26f), 0.06f),
                lowPass: 9000f, reverb: 0.14f, peak: 0.7f));

            _hitIce = Clip("sfx_hit_ice", Post(
                Render(0.12f, (t, s) => (Sine(s, 3100f) * 0.7f + Sine(s, 4650f) * 0.3f) * Decay(t, 14f), 0.1f),
                lowPass: 11000f, reverb: 0.26f, peak: 0.65f));

            _hitWater = Clip("sfx_hit_water", Post(
                Render(0.1f, (t, s) => Noise() * Decay(t, 18f) * 0.8f, 0.08f),
                lowPass: 1800f, reverb: 0.22f, peak: 0.6f));

            _hitMetal = Clip("sfx_hit_metal", Post(
                Render(0.14f, (t, s) => (Sine(s, 1180f) * 0.5f + Sine(s, 1770f) * 0.3f
                                         + Sine(s, 2650f) * 0.2f) * Decay(t, 11f), 0.12f),
                lowPass: 8000f, reverb: 0.3f, peak: 0.7f));

            // --- мелкие сигналы обратной связи
            _uiClick = Clip("sfx_click", Post(
                Render(0.07f, (t, s) => Sine(s, Mathf.Lerp(760f, 980f, t)) * Decay(t, 16f), 0.06f),
                lowPass: 6000f, reverb: 0.18f, peak: 0.7f));

            // щелчок готовности стража — помогает читать перезарядку на слух
            _guardianReady = Clip("sfx_ready", Post(
                Render(0.06f, (t, s) => Sine(s, Mathf.Lerp(1500f, 1950f, t)) * Decay(t, 22f), 0.05f),
                lowPass: 8000f, reverb: 0.12f, peak: 0.6f));

            // упор в поле босса: жужжание с амплитудной модуляцией
            _repel = Clip("sfx_repel", Post(
                Render(0.22f, (t, s) =>
                    Square(s, 118f) * (0.6f + 0.4f * Mathf.Sin(s * 190f)) * Decay(t, 8f), 0.12f),
                lowPass: 1500f, reverb: 0.2f, peak: 0.75f));

            _heartbeat = Clip("sfx_heartbeat", Post(
                Render(0.40f, (t, s) =>
                {
                    float first = Sine(s, 58f) * Decay(t, 22f);
                    float secondT = t - 0.30f;
                    float second = secondT > 0f ? Sine(s, 52f) * Decay(secondT, 20f) * 0.75f : 0f;
                    return first + second;
                }, 0.2f),
                lowPass: 420f, reverb: 0.15f, peak: 0.9f));

            // Плеск чернил по стеклу: шумовой всплеск через очень низкий срез
            // плюс уходящий вниз тон. Низкий lowPass и есть «мокро»: у влажного
            // удара верха нет, в отличие от хруста коралла.
            _inkSplat = Clip("sfx_ink", Post(
                Render(0.55f, (t, s) =>
                    Noise() * Decay(t, 5.5f) * 0.9f +
                    Sine(s, Mathf.Lerp(190f, 42f, t)) * Decay(t, 3.2f) * 0.8f, 0.3f),
                lowPass: 900f, reverb: 0.42f, peak: 1f));

            // Рёв «Великой тени»: не крик, а низкий гул, от которого дрожит вода.
            // Основной тон уходит ниже всего, что есть в игре, поэтому его слышно
            // даже сквозь музыку — он занимает пустой снизу регистр.
            _mantaRoar = Clip("sfx_manta_roar", Post(
                Render(2.4f, (t, s) =>
                    (Sine(s, Mathf.Lerp(38f, 27f, t)) * 0.9f +
                     Sine(s, Mathf.Lerp(57f, 40f, t)) * 0.35f +
                     Noise() * 0.10f * Decay(t, 2.2f))
                    * Adsr(t, 0.25f, 0.5f, 0.75f, 0.9f), 0.6f),
                lowPass: 420f, reverb: 0.70f, peak: 1f));

            // Разряд ската: короткий электрический треск. Шум через узкий резонанс —
            // так он слышен поверх музыки, но не режет ухо, а этих звуков за бой
            // набирается несколько десятков.
            _mantaBolt = Clip("sfx_manta_bolt", Post(
                Render(0.22f, (t, s) =>
                    (Noise() * 0.6f + Sine(s, Mathf.Lerp(1400f, 520f, t)) * 0.5f)
                    * Decay(t, 16f), 0.12f),
                lowPass: 3400f, reverb: 0.3f, peak: 0.55f));

            // Удар туши: низкий толчок с коротким «выдохом» воды следом.
            _mantaSlam = Clip("sfx_manta_slam", Post(
                Render(0.7f, (t, s) =>
                    Sine(s, Mathf.Lerp(120f, 34f, Mathf.Min(1f, t * 3f))) * Decay(t, 5f) * 0.9f
                    + Noise() * 0.25f * Decay(t, 9f), 0.35f),
                lowPass: 900f, reverb: 0.5f, peak: 1f));

            // Погасшая щель: звонкий нисходящий тон — награда должна быть слышна.
            _mantaGill = Clip("sfx_manta_gill", Post(
                Render(0.45f, (t, s) =>
                    (Sine(s, Mathf.Lerp(880f, 320f, t)) + Sine(s, Mathf.Lerp(1320f, 480f, t)) * 0.4f)
                    * Decay(t, 7f), 0.25f),
                lowPass: 4200f, reverb: 0.45f, peak: 0.8f));

            // Разряд по стражу: резкий щелчок с гудением — ошибку слышно сразу.
            _mantaShock = Clip("sfx_manta_shock", Post(
                Render(0.5f, (t, s) =>
                    (Noise() * Decay(t, 22f) * 0.9f
                     + Saw(s, Mathf.Lerp(220f, 90f, t)) * Decay(t, 4.5f) * 0.5f), 0.25f),
                lowPass: 2600f, reverb: 0.35f, peak: 0.85f));

            _blip = Clip("sfx_blip", Post(
                Render(0.10f, (t, s) => Sine(s, Mathf.Lerp(680f, 940f, t)) * Decay(t, 13f), 0.08f),
                lowPass: 5000f, reverb: 0.22f, peak: 0.6f));

            // --- фоновые дорожки
            if (_cfg.musicEnabled)
            {
                _music = Clip("music_loop", BuildMusicLoop());
                _bossMusic = Clip("music_boss_loop", BuildBossMusicLoop());
                _finaleMusic = Clip("music_finale_loop", BuildFinaleMusicLoop());
            }
            _ambient = Clip("ambient_loop", BuildAmbientLoop());
        }

        // ---------------------------------------------------- фоновые дорожки

        /// <summary>
        /// Медленный луп в ля-миноре: пад, тихое арпеджио и бас.
        /// Прогрессия Am — F — C — G, по такту на аккорд.
        /// </summary>
        static float[] BuildMusicLoop()
        {
            const float bpm = 70f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            float loop = bar * 4f;                 // четыре такта
            float tail = 1.6f;                     // хвост ревербации заворачиваем в начало

            float[] roots = { 110.00f, 87.31f, 130.81f, 98.00f };            // A2, F2, C3, G2
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f },   // Am
                new[] { 174.61f, 220.00f, 261.63f },   // F
                new[] { 261.63f, 329.63f, 392.00f },   // C
                new[] { 196.00f, 246.94f, 293.66f },   // G
            };
            int[] arpPattern = { 0, 1, 2, 1 };

            var data = Render(loop, (t, s) =>
            {
                int barIndex = Mathf.Clamp((int)(s / bar), 0, 3);
                float barPhase = Mathf.Clamp01((s - barIndex * bar) / bar);
                var chord = chords[barIndex];

                // пад: аккорд с мягкой атакой на весь такт
                float padEnv = Adsr(barPhase, 0.22f, 0.28f, 0.72f, 0.30f);
                float pad = 0f;
                for (int i = 0; i < chord.Length; i++)
                    pad += Sine(s, chord[i]) + Sine(s, chord[i] * 1.004f) * 0.5f; // лёгкий расстрой
                pad *= padEnv * 0.10f;

                // арпеджио восьмыми
                float eighthLen = beat * 0.5f;
                int eighth = (int)(s / eighthLen);
                float eighthPhase = Mathf.Clamp01((s - eighth * eighthLen) / eighthLen);
                float arpNote = chord[arpPattern[eighth % arpPattern.Length]] * 2f;
                float arp = Sine(s, arpNote) * Decay(eighthPhase, 5.5f) * 0.09f;

                // бас: корень на каждый такт
                float bass = Sine(s, roots[barIndex]) * Adsr(barPhase, 0.05f, 0.35f, 0.55f, 0.25f) * 0.16f;

                return pad + arp + bass;
            }, tail);

            data = LowPass(data, 2100f);
            data = Reverb(data, 0.041f, 0.30f);
            data = Reverb(data, 0.067f, 0.22f);
            data = WrapTail(data, Mathf.RoundToInt(loop * SampleRate));
            return Normalize(data, 0.75f);
        }

        /// <summary>
        /// Боевая тема. Всё сделано ровно наоборот к спокойной: вдвое быстрее,
        /// пульсирующий бас восьмыми, удар на каждую долю, тремоло на тритоне
        /// и хроматический сдвиг корня на полтона в третьем такте — самое неуютное
        /// созвучие, какое можно получить двумя нотами.
        /// Фильтр открыт шире (3200 Гц против 2100), поэтому тема звучит резче и ближе.
        /// </summary>
        static float[] BuildBossMusicLoop()
        {
            const float bpm = 124f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            float loop = bar * 4f;                 // ≈ 7.7 с
            float tail = 1.2f;

            // D — D — Eb — C#: корень «ползёт» по полутонам вокруг основного тона
            float[] roots = { 73.42f, 73.42f, 77.78f, 69.30f };

            var data = Render(loop, (t, s) =>
            {
                int barIndex = Mathf.Clamp((int)(s / bar), 0, 3);
                float barPhase = Mathf.Clamp01((s - barIndex * bar) / bar);
                float root = roots[barIndex];

                // удар на каждую долю: короткий падающий тон
                int beatIndex = (int)(s / beat);
                float beatPhase = Mathf.Clamp01((s - beatIndex * beat) / beat);
                float kick = Sine(s, Mathf.Lerp(95f, 44f, Mathf.Min(1f, beatPhase * 6f)))
                             * Decay(beatPhase, 18f) * 0.30f;

                // пульсирующий бас восьмыми
                float eighthLen = beat * 0.5f;
                int eighth = (int)(s / eighthLen);
                float eighthPhase = Mathf.Clamp01((s - eighth * eighthLen) / eighthLen);
                float bass = Saw(s, root) * Decay(eighthPhase, 8f) * 0.22f;

                // тремоло на тритоне — «сирена»
                float tritone = Semitone(root, 6);
                float tremolo = 0.55f + 0.45f * Mathf.Sin(s * Mathf.PI * 2f * 11f);
                float pad = (Sine(s, root * 2f) + Sine(s, tritone * 2f)) * tremolo
                            * Adsr(barPhase, 0.06f, 0.2f, 0.8f, 0.15f) * 0.10f;

                // тревожное арпеджио по уменьшенному аккорду, со смещением на шестнадцатую
                float offset = eighthLen * 0.5f;
                int arpStep = (int)((s + offset) / eighthLen);
                float arpPhase = Mathf.Clamp01((s + offset - arpStep * eighthLen) / eighthLen);
                int[] steps = { 0, 3, 6, 3 };
                float arpNote = Semitone(root * 4f, steps[arpStep % steps.Length]);
                float arp = Sine(s, arpNote) * Decay(arpPhase, 7f) * 0.07f;

                return kick + bass + pad + arp;
            }, tail);

            data = LowPass(data, 3200f);
            data = Reverb(data, 0.033f, 0.26f);
            data = Reverb(data, 0.051f, 0.18f);
            data = WrapTail(data, Mathf.RoundToInt(loop * SampleRate));
            return Normalize(data, 0.85f);
        }

        /// <summary>
        /// Тема «Великой тени». Боевая тема для финала не годится: она бодрая и
        /// подгоняет, а здесь нужно обратное — ощущение, что происходящее больше тебя.
        ///
        /// Поэтому всё сделано наоборот к ней: темп вдвое медленнее, вместо ударов на
        /// каждую долю — один тяжёлый удар на такт, вместо арпеджио — длинные тоны.
        /// Бас опущен в самый низ (D1), а сверху висит малая секунда — созвучие, от
        /// которого физически не по себе. Фильтр прикрыт до 1400 Гц: тема звучит
        /// глухо, будто сквозь толщу воды.
        /// </summary>
        static float[] BuildFinaleMusicLoop()
        {
            const float bpm = 64f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            float loop = bar * 4f;                 // ≈ 15 с
            float tail = 2.2f;

            // D1 держится всю тему: корень не движется вообще. Двигается верхний
            // голос, и от этого тема давит, а не ведёт.
            const float root = 36.71f;
            float[] tops = { 73.42f, 77.78f, 73.42f, 69.30f };

            var data = Render(loop, (t, s) =>
            {
                int barIndex = Mathf.Clamp((int)(s / bar), 0, 3);
                float barPhase = Mathf.Clamp01((s - barIndex * bar) / bar);

                // Сердцебиение: два удара подряд, потом тишина. Первая версия темы
                // держала один ровный удар на такт, и её просто не замечали за звуками
                // боя — а от сдвоенного удара никуда не деться, он читается телом.
                float beatPhase = Mathf.Repeat(s, beat) / beat;
                float thump = Sine(s, Mathf.Lerp(78f, 33f, Mathf.Min(1f, beatPhase * 5f)))
                              * Decay(beatPhase, 7f);
                float second = Mathf.Repeat(s - beat * 0.32f, beat) / beat;
                thump += Sine(s, Mathf.Lerp(70f, 31f, Mathf.Min(1f, second * 5f)))
                         * Decay(second, 9f) * 0.62f;
                thump *= 0.46f;

                // неподвижный бас в самом низу
                float bass = (Sine(s, root) * 0.8f + Saw(s, root * 2f) * 0.16f)
                             * (0.72f + 0.28f * Mathf.Sin(s * Mathf.PI * 2f * 0.5f)) * 0.32f;

                // верхний голос и малая секунда к нему — основное неуютное созвучие
                float top = tops[barIndex];
                float pad = (Sine(s, top) + Sine(s, Semitone(top, 1)) * 0.7f)
                            * Adsr(barPhase, 0.3f, 0.3f, 0.75f, 0.45f) * 0.16f;

                // Тревожное тремоло на тритоне поверх — тонкая дрожащая линия, которая
                // не даёт теме превратиться в ровный гул.
                float tritone = Semitone(root * 4f, 6);
                float shimmer = Sine(s, tritone) * (0.5f + 0.5f * Mathf.Sin(s * Mathf.PI * 2f * 7f))
                                * Adsr(barPhase, 0.5f, 0.2f, 0.6f, 0.4f) * 0.075f;

                // дыхание: медленный шум приливами, как толща воды над головой
                float breath = Noise() * 0.04f
                               * (0.5f + 0.5f * Mathf.Sin(s * Mathf.PI * 2f * 0.22f));

                return thump + bass + pad + shimmer + breath;
            }, tail);

            data = SeamlessLoop(data, Mathf.RoundToInt(loop * SampleRate),
                                Mathf.RoundToInt(0.5f * SampleRate));

            // Фильтр открыт шире прежнего (2400 вместо 1400): совсем глухая тема
            // терялась под звуками боя, а вместе с ней и вся тревога.
            return Normalize(Post(data, lowPass: 2400f, reverb: 0.58f, peak: 0.95f), 0.95f);
        }

        static float Semitone(float frequency, int semitones)
            => frequency * Mathf.Pow(2f, semitones / 12f);

        /// <summary>Подводный гул: сильно отфильтрованный шум, медленный LFO и низкий дрон.</summary>
        static float[] BuildAmbientLoop()
        {
            const float loop = 10f;
            const float fade = 0.6f;

            var data = Render(loop + fade, (t, s) =>
            {
                float swell = 0.55f + 0.45f * Mathf.Sin(s * (Mathf.PI * 2f / 5f));
                float rumble = Noise() * swell;
                float drone = (Sine(s, 55f) * 0.5f + Sine(s, 82.4f) * 0.25f)
                              * (0.5f + 0.5f * Mathf.Sin(s * (Mathf.PI * 2f / 7f)));
                return rumble * 0.7f + drone * 0.3f;
            });

            data = LowPass(data, 340f);
            data = LowPass(data, 340f); // второй проход — крутче срез, глуше «толща воды»
            data = SeamlessLoop(data, Mathf.RoundToInt(loop * SampleRate), Mathf.RoundToInt(fade * SampleRate));
            return Normalize(data, 0.55f);
        }

        // ----------------------------------------------------- строительные блоки

        static float[] Arpeggio(float[] notes, float noteLength, float decay)
        {
            float total = notes.Length * noteLength;
            return Render(total, (t, s) =>
            {
                int index = Mathf.Clamp((int)(s / noteLength), 0, notes.Length - 1);
                float local = Mathf.Clamp01((s - index * noteLength) / noteLength);
                return (Sine(s, notes[index]) * 0.8f + Sine(s, notes[index] * 2f) * 0.2f) * Decay(local, decay);
            }, 0.5f);
        }

        /// <param name="fn">(t01 в пределах тела звука, секунды) -> амплитуда.</param>
        /// <param name="tail">Тишина после тела — место для хвоста ревербации.</param>
        static float[] Render(float duration, Func<float, float, float> fn, float tail = 0f)
        {
            int body = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            int total = body + Mathf.RoundToInt(tail * SampleRate);
            var data = new float[total];

            for (int i = 0; i < body; i++)
            {
                float seconds = i / (float)SampleRate;
                float t = body > 1 ? i / (float)(body - 1) : 1f;
                data[i] = fn(t, seconds);
            }
            return data;
        }

        static float[] Post(float[] data, float lowPass, float reverb, float peak)
        {
            data = LowPass(data, lowPass);
            if (reverb > 0f)
            {
                data = Reverb(data, 0.029f, reverb);
                data = Reverb(data, 0.047f, reverb * 0.7f);
            }
            return Normalize(data, peak);
        }

        /// <summary>Однополюсный ФНЧ — та самая приглушённость «из-под воды».</summary>
        static float[] LowPass(float[] data, float cutoffHz)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * Mathf.Max(20f, cutoffHz));
            float alpha = dt / (rc + dt);
            float prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                prev += alpha * (data[i] - prev);
                data[i] = prev;
            }
            return data;
        }

        /// <summary>Гребенчатая задержка — дешёвая ревербация.</summary>
        static float[] Reverb(float[] data, float delaySeconds, float feedback)
        {
            int delay = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * SampleRate));
            for (int i = delay; i < data.Length; i++)
                data[i] += data[i - delay] * feedback;
            return data;
        }

        static float[] Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-5f) return data;

            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * k, -1f, 1f);
            return data;
        }

        /// <summary>Завернуть хвост в начало: стык лупа без обрыва затухания.</summary>
        static float[] WrapTail(float[] data, int loopSamples)
        {
            loopSamples = Mathf.Clamp(loopSamples, 1, data.Length);
            var loop = new float[loopSamples];
            Array.Copy(data, loop, loopSamples);

            for (int i = loopSamples; i < data.Length; i++)
            {
                int target = i - loopSamples;
                if (target >= loopSamples) break;
                loop[target] += data[i];
            }
            return loop;
        }

        /// <summary>Кроссфейд конца в начало: для шумовых дорожек, где хвоста как такового нет.</summary>
        static float[] SeamlessLoop(float[] data, int loopSamples, int fadeSamples)
        {
            loopSamples = Mathf.Clamp(loopSamples, 1, data.Length);
            fadeSamples = Mathf.Clamp(fadeSamples, 0, Mathf.Min(loopSamples, data.Length - loopSamples));

            var loop = new float[loopSamples];
            Array.Copy(data, loop, loopSamples);

            for (int i = 0; i < fadeSamples; i++)
            {
                float k = i / (float)fadeSamples;
                loop[i] = loop[i] * k + data[loopSamples + i] * (1f - k);
            }
            return loop;
        }

        static AudioClip Clip(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // -------------------------------------------------------- генераторы

        static float Decay(float t, float k) => Mathf.Exp(-k * t);
        static float Sine(float s, float f) => Mathf.Sin(s * f * Mathf.PI * 2f);
        static float Saw(float s, float f) => Mathf.Repeat(s * f, 1f) * 2f - 1f;
        static float Square(float s, float f) => Mathf.Repeat(s * f, 1f) < 0.5f ? 1f : -1f;
        static float Noise() => UnityEngine.Random.value * 2f - 1f;

        static float Adsr(float t, float attack, float decay, float sustain, float release)
        {
            if (t < attack) return t / Mathf.Max(1e-4f, attack);
            if (t < attack + decay) return Mathf.Lerp(1f, sustain, (t - attack) / Mathf.Max(1e-4f, decay));
            if (t < 1f - release) return sustain;
            return Mathf.Lerp(sustain, 0f, (t - (1f - release)) / Mathf.Max(1e-4f, release));
        }
    }
}
