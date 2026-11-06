using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Единая точка чтения указателя: пальцы, мышь на ПК, перо — каждое касание
    /// отдельно и со своим id (см. ReadContacts). Работает и на новой Input System
    /// (в проекте активна именно она), и на legacy — код выбирается по дефайнам, так
    /// что переключение Active Input Handling ничего не ломает.
    /// </summary>
    public static class PointerInput
    {
        /// <summary>Как закончилось касание: идёт, отпущено, отменено системой.</summary>
        public enum ContactPhase : byte { Active, Ended, Canceled }

        /// <summary>
        /// Одно касание: палец (свой id на всё время касания), зажатая кнопка мыши
        /// или перо. Положение — экранные пиксели.
        /// </summary>
        public struct Contact
        {
            public int Id;
            public Vector2 Screen;
            public ContactPhase Phase;
        }

        /// <summary>Id мыши и пера — отрицательные, чтобы не совпасть с пальцами.</summary>
        public const int MouseId = -1;
        public const int PenId = -2;

        /// <summary>
        /// Все касания экрана по отдельности, каждое со своим id.
        ///
        /// TryRead отдаёт только «главное» касание платформы, и страж держался за
        /// него: стоило системе на кадр отменить касание (ладонь, жест у края,
        /// повторная выдача пальца под новым id), как «главного» не оставалось —
        /// и страж уходил к пузырьку, пока палец всё ещё на стекле. Здесь страж
        /// сам следит за своим пальцем по id. Закончившиеся касания тоже в списке
        /// (Ended/Canceled) — чтобы понять, как именно ушёл палец.
        /// </summary>
        public static void ReadContacts(System.Collections.Generic.List<Contact> into)
        {
            into.Clear();
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var touches = touch.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    var phase = t.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                    into.Add(new Contact
                    {
                        Id = t.touchId.ReadValue(),
                        Screen = t.position.ReadValue(),
                        Phase = phase == UnityEngine.InputSystem.TouchPhase.Ended ? ContactPhase.Ended
                              : phase == UnityEngine.InputSystem.TouchPhase.Canceled ? ContactPhase.Canceled
                              : ContactPhase.Active,
                    });
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                into.Add(new Contact { Id = MouseId, Screen = mouse.position.ReadValue(), Phase = ContactPhase.Active });

            var pen = Pen.current;
            if (pen != null && pen.tip.isPressed)
                into.Add(new Contact { Id = PenId, Screen = pen.position.ReadValue(), Phase = ContactPhase.Active });
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                into.Add(new Contact
                {
                    Id = t.fingerId + 1,
                    Screen = t.position,
                    Phase = t.phase == TouchPhase.Ended ? ContactPhase.Ended
                          : t.phase == TouchPhase.Canceled ? ContactPhase.Canceled
                          : ContactPhase.Active,
                });
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                into.Add(new Contact { Id = MouseId, Screen = Input.mousePosition, Phase = ContactPhase.Active });
#endif
        }

        /// <summary>
        /// Нажата ли системная кнопка «Назад». На Android она приходит как Escape —
        /// отдельного кода клавиши у неё нет ни в новой Input System, ни в старой.
        /// На ПК той же клавишей удобно закрывать экраны, так что развязывать их незачем.
        /// </summary>
        public static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
