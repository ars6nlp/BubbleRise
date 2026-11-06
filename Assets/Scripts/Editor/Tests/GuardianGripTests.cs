using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BubbleRise.Tests
{
    /// <summary>
    /// Страж держится за палец: живой GuardianOrb, касания — настоящими событиями
    /// Touchscreen из Input System, кадр за кадром.
    ///
    /// Сценарии — те, что в Unity-прогоне (режим grip в AuditCapture) уводили
    /// стража к пузырьку посреди ведения: касание на кадр отменено системой и
    /// пришло заново под другим id, разрыв в три кадра, второй палец, нажатие на
    /// кнопку. Тесты зовут только то, что у стража было всегда (Init, Tick,
    /// IsDragging, Center), а проверку «касание над интерфейсом» подставляют через
    /// отражение — поэтому собираются и на прежнем коде и там падают.
    /// </summary>
    public class GuardianGripTests
    {
        const float Dt = 1f / 60f;

        GameObject _root;
        Camera _cam;
        GameConfig _cfg;
        BubbleController _bubble;
        GuardianOrb _guard;
        Touchscreen _ts;
        RenderTexture _rt;
        InputSettings.UpdateMode _mode;

        Vector2 Home => _bubble.Center + _cfg.guardianHomeOffset;

        [SetUp]
        public void SetUp()
        {
            _mode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            _ts = InputSystem.AddDevice<Touchscreen>();
            _ts.MakeCurrent();

            _root = new GameObject("GripTest");
            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            _cam = camGo.AddComponent<Camera>();
            _rt = new RenderTexture(1080, 2340, 0);   // 19.5:9, как у телефона
            _cam.targetTexture = _rt;

            _cfg = _root.AddComponent<GameConfig>();
            var field = _root.AddComponent<Playfield>();
            field.Init(_cam, _cfg.worldHeight, _cfg.designAspect);

            var bubbleGo = new GameObject("Bubble");
            bubbleGo.transform.SetParent(_root.transform, false);
            _bubble = bubbleGo.AddComponent<BubbleController>();
            _bubble.Init(_cfg, field);

            var guardGo = new GameObject("Guardian");
            guardGo.transform.SetParent(_root.transform, false);
            _guard = guardGo.AddComponent<GuardianOrb>();
            _guard.Init(_cfg, field, _bubble);

            SetUiHook(null);
            for (int i = 0; i < 5; i++) Frame();
        }

        [TearDown]
        public void TearDown()
        {
            SetUiHook(null);
            if (_ts != null) InputSystem.RemoveDevice(_ts);
            InputSystem.settings.updateMode = _mode;
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) Object.DestroyImmediate(_rt);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        // ------------------------------------------------------------ помощники

        void Frame()
        {
            InputSystem.Update();
            _guard.Tick(Dt);
        }

        void Touch(int id, TouchPhase phase, Vector2 world)
        {
            var sp = _cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            InputSystem.QueueStateEvent(_ts, new TouchState
            {
                touchId = id,
                phase = phase,
                position = new Vector2(sp.x, sp.y),
                pressure = 1f,
            });
        }

        Vector2 ToScreen(Vector2 world)
        {
            var sp = _cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(sp.x, sp.y);
        }

        /// <summary>«Над интерфейсом» — круг вокруг точки мира. На прежнем коде поля нет — проверки нет.</summary>
        static void SetUiHook(Func<Vector2, bool> test)
        {
            var f = typeof(GuardianOrb).GetField("UiHitTest", BindingFlags.Static | BindingFlags.Public);
            f?.SetValue(null, test);
        }

        /// <summary>Путь пальца: дугой вверх-вбок от точки рядом со стражем, ≈3 юн/с.</summary>
        Func<int, Vector2> Path(Vector2 start) =>
            f => start + new Vector2(Mathf.Sin(f * 0.04f) * 1.2f, (1f - Mathf.Cos(f * 0.04f)) * 0.9f);

        /// <summary>
        /// Ведём стража пальцем id 1 по пути; act(f) добавляет своё в кадр f (и
        /// решает, слать ли Moved для id 1). Возвращает наибольшее отклонение стража
        /// от «палец + смещение захвата» на кадрах, где палец ведёт (с 10-го).
        /// </summary>
        float Drag(int frames, Func<int, Func<int, Vector2>, bool> act, Func<int, bool> leading = null)
        {
            Vector2 start = _guard.Center + new Vector2(0.18f, 0.08f);
            Vector2 offset = _guard.Center - start;
            var path = Path(start);
            float worst = 0f;
            for (int f = 0; f < frames; f++)
            {
                bool sendFirst = act == null || act(f, path);
                if (f == 0) Touch(1, TouchPhase.Began, path(0));
                else if (sendFirst) Touch(1, TouchPhase.Moved, path(f));
                Frame();
                if (f >= 10 && (leading == null || leading(f)))
                    worst = Mathf.Max(worst, Vector2.Distance(_guard.Center, path(f) + offset));
            }
            return worst;
        }

        // ------------------------------------------------------------ тесты

        /// <summary>Сколько допускаем отставания от пальца: λ = 30…38, палец ≈3 юн/с — ≈0.1.</summary>
        const float Follow = 0.2f;

        [Test]
        public void ContinuousDrag_FollowsFinger()
        {
            float worst = Drag(60, null);
            Assert.That(_guard.IsDragging, Is.True);
            Assert.That(worst, Is.LessThan(Follow));
        }

        [Test]
        public void CanceledForOneFrame_ThenNewId_KeepsGrip()
        {
            float worst = Drag(60, (f, path) =>
            {
                if (f == 30) Touch(1, TouchPhase.Canceled, path(f));
                if (f == 31) Touch(2, TouchPhase.Began, path(f));
                if (f > 31) Touch(2, TouchPhase.Moved, path(f));
                return f < 30;
            });
            Assert.That(worst, Is.LessThan(Follow), "страж ушёл к пузырьку или потерял смещение захвата");
            Assert.That(_guard.IsDragging, Is.True);
        }

        [Test]
        public void EndedThenBeganThreeFramesLater_KeepsGrip()
        {
            float worst = Drag(60, (f, path) =>
            {
                if (f == 30) Touch(1, TouchPhase.Ended, path(f));
                if (f == 33) Touch(2, TouchPhase.Began, path(f));
                if (f > 33) Touch(2, TouchPhase.Moved, path(f));
                return f < 30;
            });
            Assert.That(worst, Is.LessThan(Follow), "разрыв в три кадра увёл стража к пузырьку");
        }

        [Test]
        public void SecondFingerAndPalm_DoNotStealGrip()
        {
            var far = new Vector2(1.0f, -3.0f);
            float worst = Drag(60, (f, path) =>
            {
                if (f == 15) Touch(2, TouchPhase.Began, far);
                if (f > 15 && f < 40) Touch(2, TouchPhase.Moved, far + new Vector2(0f, f * 0.01f));
                if (f == 40) Touch(2, TouchPhase.Ended, far);
                return true;
            });
            Assert.That(worst, Is.LessThan(Follow));
        }

        [Test]
        public void FirstFingerUp_SecondStays_GuardianGoesHome()
        {
            var far = new Vector2(1.0f, -3.0f);
            float atRelease = 0f;
            Drag(80, (f, path) =>
            {
                if (f == 15) Touch(2, TouchPhase.Began, far);
                if (f > 15) Touch(2, TouchPhase.Moved, far + new Vector2(-f * 0.01f, 0f));
                if (f == 30) { Touch(1, TouchPhase.Ended, path(f)); atRelease = Vector2.Distance(_guard.Center, Home); }
                return f < 30;
            });
            Assert.That(_guard.IsDragging, Is.False, "ведёт первый палец — он ушёл, захвата быть не должно");
            Assert.That(Vector2.Distance(_guard.Center, Home), Is.LessThan(atRelease - 0.3f),
                        "страж застыл, а не пошёл к пузырьку");
        }

        [Test]
        public void PressStartedOverButton_DoesNotGrab()
        {
            Vector2 button = Home + new Vector2(1.6f, -2.2f);
            Vector2 buttonScreen = ToScreen(button);
            SetUiHook(sp => Vector2.Distance(sp, buttonScreen) < 60f);
            Touch(1, TouchPhase.Began, button);
            Frame();
            for (int f = 1; f < 40; f++)
            {
                Touch(1, TouchPhase.Moved, Vector2.Lerp(button, Vector2.zero, f / 40f));
                Frame();
                Assert.That(_guard.IsDragging, Is.False, "нажатие на кнопку схватило стража (кадр " + f + ")");
            }
            Assert.That(Vector2.Distance(_guard.Center, Home), Is.LessThan(0.3f));
        }

        [Test]
        public void DragAcrossButton_KeepsGrip()
        {
            // кнопка — на середине пути пальца
            Vector2 start = _guard.Center + new Vector2(0.18f, 0.08f);
            Vector2 buttonScreen = ToScreen(Path(start)(30));
            SetUiHook(sp => Vector2.Distance(sp, buttonScreen) < 80f);
            float worst = Drag(60, null);
            Assert.That(worst, Is.LessThan(Follow));
            Assert.That(_guard.IsDragging, Is.True);
        }

        [Test]
        public void HitsAndCooldown_DoNotBreakGrip()
        {
            // что делает с ним удар или разрушение препятствия: перезарядка и отдача
            float worst = Drag(60, (f, path) =>
            {
                if (f == 20 || f == 32 || f == 44)
                {
                    _guard.NotifyDeflect();
                    _guard.Nudge(Vector2.down * 0.05f);
                }
                return true;
            });
            Assert.That(_guard.IsDragging, Is.True);
            Assert.That(worst, Is.LessThan(Follow + 0.05f));
        }

        [Test]
        public void RealRelease_ReturnsHome()
        {
            Drag(30, null);
            Touch(1, TouchPhase.Ended, _guard.Center);
            float before = Vector2.Distance(_guard.Center, Home);
            for (int i = 0; i < 30; i++) Frame();
            Assert.That(_guard.IsDragging, Is.False);
            Assert.That(Vector2.Distance(_guard.Center, Home), Is.LessThan(before * 0.5f));
        }

        [Test]
        public void Response90_NotSlowerThanBefore()
        {
            // палец на страже, затем скачок на 1 юн. вверх: за сколько страж проходит 90 %
            Vector2 p = _guard.Center;
            Touch(1, TouchPhase.Began, p);
            Frame();
            Vector2 from = _guard.Center;
            Vector2 to = from + Vector2.up;
            int frames = 0;
            for (; frames < 30; frames++)
            {
                Touch(1, TouchPhase.Moved, p + Vector2.up);
                Frame();
                if (Vector2.Distance(_guard.Center, to) <= 0.1f) { frames++; break; }
            }
            // 1 − e^(−λt) = 0.9: λ 30 → 77 мс, λ 38 → 61 мс; кадр 16.7 мс
            Assert.That(frames * Dt, Is.LessThanOrEqualTo(0.077f + Dt));
        }

        [Test]
        public void InputDisabled_DropsGrip_AndNeedsFreshPress()
        {
            Drag(20, null);
            _guard.InputEnabled = false;
            Frame();
            Assert.That(_guard.IsDragging, Is.False);
            _guard.InputEnabled = true;
            for (int i = 0; i < 5; i++) { Touch(1, TouchPhase.Moved, _guard.Center + Vector2.up); Frame(); }
            Assert.That(_guard.IsDragging, Is.False, "держал палец со времён паузы — без нового нажатия не хватает");
        }
    }
}
