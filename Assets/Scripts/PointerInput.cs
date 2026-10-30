using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Единая точка чтения указателя: мышь на ПК и палец в мобильном браузере.
    /// Работает и на новой Input System (в проекте активна именно она), и на legacy —
    /// код выбирается по дефайнам, так что переключение Active Input Handling ничего не ломает.
    /// </summary>
    public static class PointerInput
    {
        /// <summary>Экранные координаты указателя и признак нажатия.</summary>
        public static bool TryRead(out Vector2 screenPos, out bool pressed)
        {
            screenPos = Vector2.zero;
            pressed = false;

#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                pressed = true;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPos = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                screenPos = pointer.position.ReadValue();
                pressed = pointer.press.isPressed;
                return true;
            }
            return false;
#else
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                screenPos = t.position;
                pressed = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                return true;
            }
            screenPos = Input.mousePosition;
            pressed = Input.GetMouseButton(0);
            return true;
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
