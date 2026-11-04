using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Запекание атласов мира в PNG (см. <see cref="SpriteFactory"/>, файл
    /// SpriteFactoryBaked.cs).
    ///
    /// Генератор остаётся процедурным: картинки рисует тот же код, что и в игре,
    /// здесь они только сохраняются. Пересобирать — после любой правки
    /// генератора (Palette, SpriteFactory, SpriteFactoryWorld, SpriteFactoryWalls):
    /// вручную через меню или само — перед сборкой, если хеш исходников в meta
    /// не совпал. Tools/check_world.py ловит устаревшее в репозитории.
    /// </summary>
    public static class WorldAtlasBaker
    {
        public const string Folder = "Assets/Resources/" + SpriteFactory.BakedFolder;

        [MenuItem("Tools/BubbleRise/5. Запечь атласы мира", false, 40)]
        public static void BakeMenu()
        {
            Bake();
            EditorUtility.DisplayDialog("Атласы мира", "Готово: " + Folder, "OK");
        }

        /// <summary>Запечённое совпадает с текущим генератором.</summary>
        public static bool IsFresh()
        {
            string meta = Path.Combine(Folder, "meta.txt");
            if (!File.Exists(meta)) return false;
            string version = null;
            foreach (var line in File.ReadAllLines(meta))
                if (line.StartsWith("version ")) version = line.Substring(8).Trim();
            return version == CurrentVersion();
        }

        static string CurrentVersion() => SpriteFactory.SourceHash(Path.Combine(Application.dataPath, "Scripts"));

        public static void Bake()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var images = SpriteFactory.PaintWorldForBake(CurrentVersion(), out string meta);
            Directory.CreateDirectory(Folder);
            long bytes = 0;
            foreach (var img in images)
            {
                var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
                tex.SetPixels32(img.Pixels);
                tex.Apply(false);
                var png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                File.WriteAllBytes(Path.Combine(Folder, img.Name + ".png.bytes"), png);
                bytes += png.Length;
            }
            File.WriteAllText(Path.Combine(Folder, "meta.txt"), meta);
            AssetDatabase.Refresh();
            Debug.Log($"[BubbleRise] Атласы мира запечены: {images.Count} картинок, {bytes / 1024} КБ PNG, " +
                      $"{watch.ElapsedMilliseconds} мс → {Folder}");
        }

        /// <summary>Точка входа для командной строки: Unity -batchmode -executeMethod ….BakeBatch</summary>
        public static void BakeBatch()
        {
            Bake();
            EditorApplication.Exit(0);
        }
    }

    /// <summary>Перед сборкой: генератор менялся — запечь заново, чтобы в пакет не ушло старое.</summary>
    class WorldAtlasBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (WorldAtlasBaker.IsFresh()) return;
            Debug.Log("[BubbleRise] Атласы мира устарели или не запечены — запекаю перед сборкой.");
            WorldAtlasBaker.Bake();
        }
    }
}
