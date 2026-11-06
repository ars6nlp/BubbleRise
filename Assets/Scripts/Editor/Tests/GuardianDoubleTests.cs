using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BubbleRise.Tests
{
    /// <summary>
    /// «Двойник стража»: при ударе перед пузырьком мелькала вторая защита.
    ///
    /// Тест запускает настоящую игру (сцена Main, Play Mode, 60 кадров в секунду),
    /// надевает скины — все десять боссовых пар и несколько обычных и легендарных, —
    /// бросает в стража препятствия всех видов и после удара водит его из стороны в
    /// сторону тем же путём, что и бой (цель, к которой страж тянется в такте). В
    /// конце каждого кадра считает:
    ///
    /// * образы стража — видимые рендереры с его спрайтом или его текстурой (сетка
    ///   мини-босса): не больше одного;
    /// * слои стража — образы и оболочку удара: каждый не дальше 0.02 юнита от стража.
    ///
    /// Потом пауза → продолжить и поражение → «Ещё раз», и снова удары.
    ///
    /// На прежнем коде падает: оболочка удара обновлялась до движения стража и стояла
    /// на кадр позади — при ведении на 0.1–0.26 юнита в стороне.
    /// </summary>
    public class GuardianDoubleTests
    {
        const float Tolerance = 0.02f;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static readonly int[] Skins = { 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 2, 4, 15, 31, 33 };

        GameManager _gm;
        GuardianOrb _guard;
        BubbleController _bubble;
        readonly List<string> _failures = new List<string>();
        int _impacts, _frames, _maxImages;
        float _maxOffset;
        bool _couldDeflect = true;
        string _where = "";

        // настройки редактора, которые тест меняет на время Play Mode, — вернуть в TearDown
        SceneSetup[] _setup;
        bool _optionsWere, _changed;
        EnterPlayModeOptions _optionsWas;

        static object Call(object o, string name, params object[] args)
        {
            var m = o.GetType().GetMethods(Any).First(x => x.Name == name && x.GetParameters().Length == args.Length);
            return m.Invoke(o, args);
        }

        static void Press(string name)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.gameObject.name == name && b.isActiveAndEnabled) { b.onClick.Invoke(); return; }
        }

        /// <summary>Конец кадра: всё сдвинуто и нарисовано — считаем образы и слои.</summary>
        void Measure()
        {
            _frames++;
            bool can = _guard.CanDeflect;
            if (_couldDeflect && !can) _impacts++;
            _couldDeflect = can;

            var host = _guard.GetComponent<SpriteRenderer>();
            Sprite sprite = host.sprite;
            Texture texture = sprite != null ? sprite.texture : null;
            Vector2 at = _guard.transform.position;
            var block = new MaterialPropertyBlock();
            int images = 0;
            float offset = 0f;
            string far = "";
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                bool image = false;
                if (r is SpriteRenderer sr)
                    image = sprite != null && sr.sprite != null && sr.color.a > 0.02f
                            && (sr.sprite == sprite || sr.sprite.texture == texture);
                else if (r is MeshRenderer mr && texture != null)
                {
                    mr.GetPropertyBlock(block);
                    image = block.GetTexture("_MainTex") == texture;
                }
                if (!image) continue;
                images++;
                float d = Vector2.Distance(r.transform.position, at);
                if (d > offset) { offset = d; far = r.name; }
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name != "CollisionEnvelope") continue;
                // считается только то, что видно: кольцо или частица оболочки
                bool drawn = t.GetComponentsInChildren<SpriteRenderer>(false).Any(s => s.enabled && s.color.a > 0.02f);
                if (!drawn) continue;
                float d = Vector2.Distance(t.position, at);
                if (d > offset) { offset = d; far = "оболочка удара"; }
            }
            _maxImages = Mathf.Max(_maxImages, images);
            _maxOffset = Mathf.Max(_maxOffset, offset);
            if ((images > 1 || offset > Tolerance) && _failures.Count < 12)
                _failures.Add($"{_where}: образов {images}, «{far}» в {offset:F3} ю от стража");
        }

        /// <summary>
        /// Кадры игры. Страж ведётся так же, как в бою: цель сдвигается, а сам он тянется
        /// к ней в своём такте. Телепорт (Displace) тут не годится — он двигает стража
        /// вне игрового цикла, и отставание было бы у теста, а не у игры.
        /// </summary>
        IEnumerator Frames(int count, Func<int, Vector2?> steer = null)
        {
            for (int f = 0; f < count; f++)
            {
                var target = steer?.Invoke(f);
                if (target.HasValue) _guard.Nudge(target.Value - (Vector2)Call(_guard, "HomePosition"));
                yield return NextFrame();
                Measure();
            }
        }

        /// <summary>
        /// Ровно один кадр игры. Корутина EditMode-теста просыпается чаще, чем идут
        /// кадры, — без этого цель стража сдвигалась бы несколько раз за кадр.
        /// </summary>
        static IEnumerator NextFrame()
        {
            int frame = Time.frameCount;
            while (Time.frameCount == frame) yield return null;
        }

        IEnumerator Hit(ObstacleType type, int side)
        {
            // прошлое препятствие могло схватить стража (челюсть держит его — Pin) или
            // отбросить (вертушка — Displace): отпускаем и чистим поле, чтобы удар был новым
            _guard.Release();
            _guard.ClearDisplacement();
            _gm.DevClearField();
            Vector2 home = (Vector2)Call(_guard, "HomePosition");
            yield return Frames(6);
            // страж заряжен и стоит — препятствие летит прямо в него
            for (int i = 0; i < 90 && !_guard.CanDeflect; i++) yield return Frames(1);
            _gm.DevSpawnObstacle();
            var list = (IList)typeof(GameManager).GetField("_obstacles", Any).GetValue(_gm);
            if (list.Count > 0)
            {
                var o = (Obstacle)list[list.Count - 1];
                Vector2 g = _guard.transform.position;
                o.Activate(type, 0.26f, g + new Vector2(0f, 0.45f), new Vector2(0f, -3f), 0f, 0f, 1f, Color.white);
            }
            int before = _impacts;
            bool charged = _guard.CanDeflect;
            for (int i = 0; i < 20 && _impacts == before; i++) yield return Frames(1);
            if (_impacts == before)
                Debug.Log($"[GuardianDoubleTests] без удара: {_where}, заряд {charged}, состояние {_gm.State}, страж {_guard.transform.position}, держат {typeof(GuardianOrb).GetField("_isPinned", Any).GetValue(_guard)}");
            // после удара оболочка живёт 2–2.6 с; страж идёт в сторону — так её и видно отстающей
            yield return Frames(30, f => home + new Vector2(side * Mathf.Sin(f * 0.2f) * 1.0f, 0.3f));
        }

        void Wear(int k)
        {
            SkinStore.SelectedBubble = k;
            SkinStore.SelectedGuardian = k;
            Call(_gm, "OnSkinChosen", 0, k);
            _gm.DevClearField();
        }

        /// <summary>
        /// Вернуть редактор как был — и при падении теста тоже. Настройку входа в Play Mode
        /// Unity пишет на диск при входе, поэтому возвращённое значение записываем явно:
        /// иначе в проекте осталась бы отключённая перезагрузка домена.
        /// </summary>
        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Time.captureFramerate = 0;
            if (!_changed) yield break;
            EditorSettings.enterPlayModeOptionsEnabled = _optionsWere;
            EditorSettings.enterPlayModeOptions = _optionsWas;
            AssetDatabase.SaveAssets();
            if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
            _changed = false;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Guardian_OneImage_LayersOnGuardian_DuringHits()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
            _optionsWere = EditorSettings.enterPlayModeOptionsEnabled;
            _optionsWas = EditorSettings.enterPlayModeOptions;
            _changed = true;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            // без перезагрузки домена тест идёт одним проходом; вернёт Restore()
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            yield return new EnterPlayMode();
            Time.captureFramerate = 60;

            for (int i = 0; i < 900 && Object.FindAnyObjectByType<UIManager>() == null; i++) yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            _gm = Object.FindAnyObjectByType<GameManager>();
            _guard = Object.FindAnyObjectByType<GuardianOrb>();
            _bubble = Object.FindAnyObjectByType<BubbleController>();
            bool booted = _gm != null && _guard != null && _bubble != null;

            if (booted)
            {
                Press("Skip");
                for (int i = 0; i < 30; i++) yield return null;
                SkinStore.DevUnlockAll = true;
                Press("PlayButton");
                for (int i = 0; i < 120; i++) yield return null;
                _bubble.Invincible = true;
                _gm.DevAddLife(40);
                foreach (var name in new[] { "_nextBossAt", "_nextZoneAt" })
                    typeof(GameManager).GetField(name, Any)?.SetValue(_gm, _gm.Distance + 1e6f);

                var types = (ObstacleType[])Enum.GetValues(typeof(ObstacleType));
                foreach (int k in Skins)
                {
                    Wear(k);
                    yield return Frames(10);
                    for (int t = 0; t < types.Length; t++)
                    {
                        _where = $"скин {k} ({SkinStore.Guardian[k].Name}), {types[t]}";
                        yield return Hit(types[t], t % 2 == 0 ? 1 : -1);
                    }
                }

                // пауза → продолжить, поражение → «Ещё раз»: и после них ровно один страж
                Wear(13);
                Press("PauseButton");
                yield return Frames(10);
                Press("ResumeButton");
                _where = "после паузы";
                yield return Hit(ObstacleType.Rock, 1);
                Call(_gm, "GameOver");
                yield return Frames(60);
                Press("RetryButton");
                for (int i = 0; i < 120; i++) yield return null;
                _bubble.Invincible = true;
                _where = "после «Ещё раз»";
                yield return Hit(ObstacleType.Coral, -1);
            }

            Time.captureFramerate = 0;
            yield return new ExitPlayMode();

            Assert.IsTrue(booted, "игра не загрузилась в Play Mode");
            Debug.Log($"[GuardianDoubleTests] кадров {_frames}, ударов {_impacts}, образов макс {_maxImages}, слой дальше всего {_maxOffset:F3} ю");
            Assert.GreaterOrEqual(_impacts, 100, "ударов почти не было — проверять нечего");
            Assert.IsEmpty(_failures, "двойник стража: " + string.Join("; ", _failures));
        }
    }
}
