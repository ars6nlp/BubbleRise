using System.IO;
using UnityEditor;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Иконка приложения, нарисованная кодом — как и всё остальное в этой игре.
    ///
    /// Google Play иконку требует обязательно, а в проекте нет ни одного файла
    /// графики, и заводить его ради одной картинки не хочется: это сразу вопрос
    /// прав, размеров под разные плотности экрана и лишних килобайт в пакете.
    /// Поэтому PNG генерируются здесь и кладутся в Assets/Icons.
    ///
    /// С версии 2 это уже не вся правда: <c>AppIcon.png</c> заменён иконкой из
    /// карточки Google Play. Тестировщики видели на телефоне одну картинку, а в
    /// магазине другую, и это выглядело как чужое приложение. Генератор оставлен —
    /// им сделаны адаптивные слои и им же можно вернуть нарисованную версию, — но
    /// перерисовка теперь спрашивает подтверждение.
    ///
    /// Иконок три, потому что Android с восьмой версии рисует их слоями:
    /// фон и передний план лаунчер двигает друг относительно друга и обрезает
    /// произвольной маской — кругом, квадратом со скруглением, каплей. Поэтому
    /// содержимое переднего слоя держится в центральных 60% холста: всё, что
    /// выходит за эту зону, на части телефонов просто срежется.
    /// </summary>
    public static class BubbleRiseIcon
    {
        const string IconDir = "Assets/Icons";
        const string Foreground = IconDir + "/AppIconForeground.png";
        const string Background = IconDir + "/AppIconBackground.png";
        const string Legacy = IconDir + "/AppIcon.png";

        const int Size = 512;

        [MenuItem("Tools/BubbleRise/Перерисовать иконку приложения", false, 40)]
        public static void Regenerate()
        {
            // AppIcon.png больше не нарисован кодом: там лежит та же картинка, что
            // стоит иконкой в карточке Google Play, и на телефонах игроков должна
            // быть именно она. Без этого вопроса один пункт меню молча вернул бы
            // нарисованную версию, и разошлись бы магазин с рабочим столом.
            bool ok = EditorUtility.DisplayDialog(
                "Перерисовать иконку?",
                "AppIcon.png сейчас — иконка из карточки Google Play. Перерисовка " +
                "заменит её нарисованной кодом, и иконка в магазине перестанет " +
                "совпадать с иконкой на телефоне.",
                "Перерисовать", "Отмена");
            if (!ok) return;

            Generate(force: true);
            Debug.Log("[BubbleRise] Иконки перерисованы: " + IconDir);
        }

        /// <summary>Нарисовать иконки, если их ещё нет. Возвращает true, если файлы на месте.</summary>
        public static bool Generate(bool force = false)
        {
            Directory.CreateDirectory(IconDir);

            bool missing = force
                           || !File.Exists(Foreground)
                           || !File.Exists(Background)
                           || !File.Exists(Legacy);
            if (!missing) return true;

            Write(Legacy, PaintLegacy);
            Write(Background, PaintBackground);
            Write(Foreground, PaintForeground);

            AssetDatabase.Refresh();

            foreach (string path in new[] { Legacy, Background, Foreground }) ApplyImportSettings(path);

            AssetDatabase.Refresh();
            return true;
        }

        delegate Color Painter(float x, float y);

        /// <param name="painter">Цвет точки в координатах −1…1, центр в нуле.</param>
        static void Write(string path, Painter painter)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];

            for (int py = 0; py < Size; py++)
            {
                // Texture2D считает строки снизу вверх, а рисовать удобнее сверху вниз.
                float y = 1f - 2f * (py + 0.5f) / Size;
                for (int px = 0; px < Size; px++)
                {
                    float x = 2f * (px + 0.5f) / Size - 1f;
                    pixels[py * Size + px] = painter(x, -y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>Фоновый слой: та же вода, что в игре, от тёмного низа к светлеющему верху.</summary>
        static Color PaintBackground(float x, float y)
        {
            // y здесь растёт вниз, поэтому верх — это y около −1.
            float t = Mathf.InverseLerp(-1f, 1f, y);
            Color water = Color.Lerp(Palette.GradTopSurface, Palette.GradBotDeep, t);

            // Косые лучи света, как в самой игре: иконка должна читаться как кадр
            // из неё, а не как абстрактный градиент.
            float ray = Mathf.Sin((x * 2.4f + y * 0.7f) * 3.1f);
            if (ray > 0.86f) water = Color.Lerp(water, Palette.Teal, (ray - 0.86f) * 3.2f);

            return water;
        }

        /// <summary>
        /// Передний слой: пузырёк и страж на прозрачном фоне, в центральных 60% холста.
        /// </summary>
        static Color PaintForeground(float x, float y)
        {
            var c = PaintSubject(x, y, scale: 0.6f);
            return c;
        }

        /// <summary>Обычная иконка: фон и предметы вместе, предметы можно взять крупнее.</summary>
        static Color PaintLegacy(float x, float y)
        {
            Color water = PaintBackground(x, y);
            Color subject = PaintSubject(x, y, scale: 0.78f);
            return Color.Lerp(water, subject, subject.a);
        }

        /// <summary>
        /// Сами герои: крупный пузырёк и страж, прикрывающий его сверху справа.
        /// Именно эта пара и есть игра, поэтому на иконке нет ничего больше —
        /// враги на 48 точках превратились бы в кашу.
        /// </summary>
        static Color PaintSubject(float x, float y, float scale)
        {
            x /= scale;
            y /= scale;

            Color result = Color.clear;

            // --- пузырёк: кольцо со светлым бликом, центр чуть ниже середины
            Vector2 bubble = new Vector2(x + 0.10f, y - 0.14f);
            float bubbleDistance = bubble.magnitude;
            const float bubbleRadius = 0.62f;
            const float bubbleWall = 0.13f;

            if (bubbleDistance < bubbleRadius)
            {
                // вода внутри чуть светлее фона — пузырёк не дыра, а объём
                result = Palette.Aqua.WithAlpha(0.13f);

                float wall = Mathf.Abs(bubbleDistance - (bubbleRadius - bubbleWall * 0.5f));
                if (wall < bubbleWall * 0.5f)
                {
                    float edge = 1f - wall / (bubbleWall * 0.5f);
                    result = Palette.Aqua.WithAlpha(Mathf.SmoothStep(0.35f, 1f, edge));
                }

                // блик сверху слева — он и делает шар шаром
                var highlight = new Vector2(bubble.x + 0.26f, bubble.y + 0.28f);
                float glare = 1f - Mathf.Clamp01(highlight.magnitude / 0.24f);
                if (glare > 0f) result = Color.Lerp(result, Palette.Foam, glare * 0.75f);
            }

            // --- страж: плотный светящийся шарик сверху справа
            var orb = new Vector2(x - 0.52f, y + 0.5f);
            float orbDistance = orb.magnitude;
            const float orbRadius = 0.30f;

            if (orbDistance < orbRadius * 2.1f)
            {
                float glow = 1f - Mathf.Clamp01(orbDistance / (orbRadius * 2.1f));
                result = Color.Lerp(result, Palette.Aqua.WithAlpha(1f), glow * glow * 0.55f);
            }

            if (orbDistance < orbRadius)
            {
                float core = 1f - Mathf.Clamp01(orbDistance / orbRadius);
                Color body = Color.Lerp(Palette.Aqua, Palette.Foam, Mathf.SmoothStep(0f, 1f, core));
                result = new Color(body.r, body.g, body.b, 1f);
            }

            return result;
        }

        /// <summary>
        /// Настройки импорта. Сжатие выключено намеренно: иконка одна, весит копейки,
        /// а артефакты сжатия на ней видно сразу — это первое, что увидит игрок в списке.
        /// </summary>
        static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        public static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        public static Texture2D ForegroundTexture => Load(Foreground);
        public static Texture2D BackgroundTexture => Load(Background);
        public static Texture2D LegacyTexture => Load(Legacy);
    }
}
