using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Подпись, в которой ни одно слово не рвётся посередине (D-03, D-04).
    ///
    /// Встроенный Best Fit этого не умеет: при переносе он считает «влезшим» и
    /// слово, разорванное на две строки, — так и выходили «НАСТРОЙК/И» и
    /// «ДОСТИЖЕ/НИЯ». Здесь кегль уменьшается, пока самое широкое слово (или
    /// вся строка, если перенос выключен) не поместится в ширину подписи.
    ///
    /// Текст меняется на лету — язык, состояние кнопки, — поэтому проверка
    /// идёт при каждой смене строки, а исходный кегль запоминается один раз:
    /// короткое слово получает свой крупный размер обратно.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class UiFit : MonoBehaviour
    {
        Text _text;
        string _last;
        float _lastWidth;
        int _maxSize = -1;
        static TextGenerator _gen;
        static readonly char[] Spaces = { ' ', '\n' };

        /// <summary>Не мельче этого кегля — ниже подпись не читается (D-08).</summary>
        public int MinSize = 12;

        public static UiFit On(Text text, int minSize = 12)
        {
            var fit = text.GetComponent<UiFit>();
            if (fit == null) fit = text.gameObject.AddComponent<UiFit>();
            fit.MinSize = minSize;
            fit._text = text;
            return fit;
        }

        void LateUpdate()
        {
            if (_text == null) _text = GetComponent<Text>();
            float width = _text.rectTransform.rect.width;
            if (ReferenceEquals(_text.text, _last) && Mathf.Approximately(width, _lastWidth)) return;
            _last = _text.text;
            _lastWidth = width;
            Fit(width);
        }

        void Fit(float width)
        {
            if (width <= 1f || string.IsNullOrEmpty(_last)) return;
            if (_maxSize < 0) _maxSize = _text.resizeTextForBestFit ? _text.resizeTextMaxSize : _text.fontSize;
            _text.resizeTextForBestFit = false;
            if (_gen == null) _gen = new TextGenerator();

            bool wrap = _text.horizontalOverflow == HorizontalWrapMode.Wrap;
            string[] words = wrap ? _last.Split(Spaces) : new[] { _last };
            int size = _maxSize;
            while (size > MinSize)
            {
                var settings = _text.GetGenerationSettings(new Vector2(width, 10000f));
                settings.fontSize = size;
                settings.resizeTextForBestFit = false;
                settings.horizontalOverflow = HorizontalWrapMode.Overflow;
                float widest = 0f;
                for (int i = 0; i < words.Length; i++)
                    widest = Mathf.Max(widest, _gen.GetPreferredWidth(words[i], settings) / _text.pixelsPerUnit);
                if (widest <= width) break;
                size--;
            }
            _text.fontSize = size;
        }
    }
}
