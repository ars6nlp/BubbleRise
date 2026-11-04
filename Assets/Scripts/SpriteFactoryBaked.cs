using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Запечённые атласы мира.
    ///
    /// Атласы мира и льда, каустики и фактуры стен — процедурные, но рисовать их
    /// на каждой загрузке дорого: около 2 с на ПК, на слабом телефоне — в разы
    /// больше. Поэтому редактор запекает их в PNG (меню Tools → BubbleRise → 5 и
    /// перед каждой сборкой, если генератор изменился), а игра на загрузке только
    /// распаковывает картинки — без потерь, пиксель в пиксель.
    ///
    /// Файлы: <c>Assets/Resources/BakedWorld/имя.png.bytes</c> и <c>meta.txt</c>
    /// (версия генератора, размеры, средние цвета рисунков). Нет файлов, размер
    /// не совпал с разметкой, а в редакторе — генератор изменился после
    /// запекания: рисуем сами, как раньше. Игре запечённые файлы не обязательны.
    /// </summary>
    public static partial class SpriteFactory
    {
        public const string BakedFolder = "BakedWorld";

        /// <summary>Отладка и замер: false — всегда рисовать, даже если готовое есть.</summary>
        public static bool UseBaked = true;

        /// <summary>Сколько текстур мира взято готовыми и сколько нарисовано — для замера загрузки.</summary>
        public static int AtlasesBaked { get; private set; }
        public static int AtlasesPainted { get; private set; }

        /// <summary>
        /// Исходники генератора. Их хеш пишется в meta при запекании и сверяется в
        /// редакторе и в Tools/check_world.py: поменялся генератор — запечённое устарело.
        /// </summary>
        public static readonly string[] GeneratorSources =
            { "Palette.cs", "SpriteFactory.cs", "SpriteFactoryWalls.cs", "SpriteFactoryWorld.cs", "SpriteFactoryZones.cs" };

        static Dictionary<string, string> _meta;
        static bool _metaRead;
        static bool _bakedChecked, _bakedOk;

        /// <summary>Готовая текстура из запечённых файлов или null.</summary>
        static Texture2D LoadBaked(string name, TextureWrapMode wrap, int width, int height)
        {
            if (!BakedUsable()) return null;
            var asset = Resources.Load<TextAsset>(BakedFolder + "/" + name + ".png");
            if (asset == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            // markNonReadable: копия в памяти процессора не нужна — как у нарисованных
            bool ok = tex.LoadImage(asset.bytes, true);
            Resources.UnloadAsset(asset);
            if (!ok || tex.width != width || tex.height != height)
            {
                // журнал — по-английски, как и прочие сообщения игры (русский — только через Loc)
                Debug.LogWarning("[BubbleRise] Baked texture " + name + " does not match (" + tex.width + "x" +
                                 tex.height + " instead of " + width + "x" + height + "), painting it instead.");
                DestroyTexture(tex);
                return null;
            }
            tex.name = name;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = wrap;
            AtlasesBaked++;
            return tex;
        }

        /// <summary>Средние цвета рисунков атласа из meta. Без них слои не знают тона.</summary>
        static bool ReadBakedAvg(string atlas, AtlasPiece[] into)
        {
            ReadMeta();
            if (_meta == null) return false;
            var inv = CultureInfo.InvariantCulture;
            for (int id = 0; id < into.Length; id++)
            {
                if (!_meta.TryGetValue("avg " + atlas + " " + id, out var v)) return false;
                var parts = v.Split(' ');
                if (parts.Length != 3) return false;
                into[id].Avg = new Color(float.Parse(parts[0], inv), float.Parse(parts[1], inv),
                                         float.Parse(parts[2], inv), 1f);
            }
            return true;
        }

        static void ReadMeta()
        {
            if (_metaRead) return;
            _metaRead = true;
            var asset = Resources.Load<TextAsset>(BakedFolder + "/meta");
            if (asset == null) return;
            _meta = new Dictionary<string, string>();
            foreach (var raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                // ключ — всё до последних трёх чисел у avg, до первого пробела у остальных
                if (line.StartsWith("avg "))
                {
                    var p = line.Split(' ');
                    if (p.Length != 6) continue;
                    _meta["avg " + p[1] + " " + p[2]] = p[3] + " " + p[4] + " " + p[5];
                }
                else
                {
                    int sp = line.IndexOf(' ');
                    if (sp > 0) _meta[line.Substring(0, sp)] = line.Substring(sp + 1);
                }
            }
            Resources.UnloadAsset(asset);
        }

        /// <summary>
        /// Можно ли брать готовое. В сборке — да, если файлы есть: их запекает шаг
        /// перед сборкой. В редакторе — только если генератор не менялся после
        /// запекания, иначе показывали бы старую картинку.
        /// </summary>
        static bool BakedUsable()
        {
            if (!UseBaked) return false;
            if (_bakedChecked) return _bakedOk;
            _bakedChecked = true;
            ReadMeta();
            _bakedOk = _meta != null && _meta.ContainsKey("version");
#if UNITY_EDITOR
            if (_bakedOk)
            {
                string now = SourceHash(System.IO.Path.Combine(Application.dataPath, "Scripts"));
                if (now != _meta["version"])
                {
                    Debug.LogWarning("[BubbleRise] Baked world atlases are stale (generator changed), painting them. " +
                                     "Rebake: Tools > BubbleRise > 5.");
                    _bakedOk = false;
                }
            }
#endif
            return _bakedOk;
        }

        static void DestroyTexture(Texture2D tex)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
            else UnityEngine.Object.DestroyImmediate(tex);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Хеш исходников генератора: имя и текст каждого файла, переводы строк
        /// приведены к \n. Тот же расчёт — в Tools/check_world.py.
        /// </summary>
        public static string SourceHash(string scriptsDir)
        {
            var sb = new StringBuilder();
            foreach (var file in GeneratorSources)
            {
                string text = System.IO.File.ReadAllText(System.IO.Path.Combine(scriptsDir, file)).Replace("\r\n", "\n");
                sb.Append(file).Append('\n').Append(text).Append('\n');
            }
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }
        }

        /// <summary>Картинка для запекания: имя файла без расширения, размер, пиксели.</summary>
        public struct BakedImage
        {
            public string Name;
            public int Width, Height;
            public Color32[] Pixels;
        }

        /// <summary>
        /// Нарисовать всё, что запекается: атласы мира и льда, каустики, фактуры
        /// стен. Возвращает картинки и текст meta (версия, размеры, средние цвета).
        /// Только для редактора — игра это не зовёт.
        /// </summary>
        public static List<BakedImage> PaintWorldForBake(string version, out string meta)
        {
            var images = new List<BakedImage>();
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append("# BubbleRise: запечённые атласы мира. Пересобрать: Tools → BubbleRise → 5. Запечь атласы мира.\n");
            sb.Append("version ").Append(version).Append('\n');

            void Atlas(string name, List<PieceDef> defs, int width, AtlasPiece[] into)
            {
                var pos = new Vector2Int[defs.Count];
                int height = LayoutAtlas(defs, width, into, pos);
                images.Add(new BakedImage { Name = name, Width = width, Height = height,
                                            Pixels = PaintAtlas(name, defs, width, height, pos, into) });
                for (int id = 0; id < into.Length; id++)
                {
                    var c = into[id].Avg;
                    sb.Append("avg ").Append(name).Append(' ').Append(id).Append(' ')
                      .Append(c.r.ToString("R", inv)).Append(' ').Append(c.g.ToString("R", inv)).Append(' ')
                      .Append(c.b.ToString("R", inv)).Append('\n');
                }
            }

            Atlas("world_atlas", WorldDefs(), 2048, new AtlasPiece[(int)WorldPiece.Count]);
            Atlas("ice_atlas", IceDefs(), 1024, new AtlasPiece[(int)IcePiece.Count]);
            Atlas("zones_atlas", ZonesDefs(), 1024, new AtlasPiece[(int)ZonePiece.Count]);
            images.Add(new BakedImage { Name = "caustics", Width = CausticSize, Height = CausticSize, Pixels = PaintCaustics() });
            foreach (WallStyle style in Enum.GetValues(typeof(WallStyle)))
                images.Add(new BakedImage { Name = "wall_" + style, Width = WallW, Height = WallH, Pixels = PaintWall(style) });

            foreach (var img in images)
                sb.Append("image ").Append(img.Name).Append(' ').Append(img.Width).Append(' ').Append(img.Height).Append('\n');
            meta = sb.ToString();
            return images;
        }
#endif
    }
}
