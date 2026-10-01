using System.IO;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Портреты соперников — заглушки, нарисованные кодом: силуэт цветом соперника, лицо, головной убор по аксессуару
    /// заглушки в сцене (капюшон, цилиндр, треуголка), у боссов золотая рамка. Портрет — картинка в
    /// OpponentConfig.Portrait: настоящий арт (или снимок будущей 3D-модели) подставляется туда без кода.
    /// Уже заданный портрет и уже нарисованный файл не перерисовываются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string PortraitsFolder = Root + "/Art/Sprites/Portraits";
        private const int PortraitSize = 256;

        private enum Headwear
        {
            None,
            Hood,
            TopHat,
            Tricorn,
        }

        private static void BuildPortraits(ArtSet art)
        {
            EnsureFolder(PortraitsFolder);
            foreach (var guid in AssetDatabase.FindAssets("t:OpponentConfig", new[] { ConfigsFolder }))
            {
                var opponent = AssetDatabase.LoadAssetAtPath<OpponentConfig>(AssetDatabase.GUIDToAssetPath(guid));
                // Грозная версия берёт портрет своего босса (LinkDreadPortraits), свой не рисуется.
                if (opponent == null || opponent.Portrait != null || string.IsNullOrEmpty(opponent.Id) || opponent.DreadOf != null)
                    continue;

                var headwear = opponent.Accessory == null ? Headwear.None
                    : opponent.Accessory == art.Hood ? Headwear.Hood
                    : opponent.Accessory == art.HatTop ? Headwear.TopHat
                    : opponent.Accessory == art.HatPirate ? Headwear.Tricorn
                    : Headwear.None;

                var path = PortraitsFolder + "/Portrait_" + opponent.Id + ".png";
                if (!File.Exists(path))
                {
                    var texture = PaintPortrait(opponent.BodyColor, headwear, opponent.IsBoss, opponent.Id.GetHashCode());
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                }

                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = PortraitSize;
                    importer.SaveAndReimport();
                }

                opponent.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorUtility.SetDirty(opponent);
            }
        }

        /// <summary>Монетка для полёта в кошелёк: золотой диск с ободком и бликом. Уже заданную в UiConfig не трогает.</summary>
        private static void BuildCoinSprite(Zonk.UI.UiConfig ui)
        {
            if (ui == null || ui.CoinSprite != null)
                return;

            var folder = Root + "/Art/Sprites";
            EnsureFolder(folder);
            var path = folder + "/Coin.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                var pixels = new Color[size * size];
                var gold = new Color(1f, 0.8f, 0.25f);
                var rim = new Color(0.75f, 0.5f, 0.1f);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var disc = Ellipse(x, y, 32f, 32f, 30f, 30f);
                    var inner = Ellipse(x, y, 32f, 32f, 23f, 23f);
                    var shine = Ellipse(x, y, 24f, 42f, 7f, 5f);
                    var color = Color.Lerp(rim, gold, inner);
                    color = Color.Lerp(color, Color.white, shine * 0.7f);
                    color.a = disc;
                    pixels[y * size + x] = color;
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            ui.CoinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(ui);
        }

        /// <summary>
        /// Значки внутри текста: символ ★ (U+2605) рисуется картинкой, потому что в шрифте Noto Sans его нет
        /// (вместо него был квадрат). Звезда белая: TextMeshPro красит её цветом текста. Свою картинку звезды можно
        /// положить поверх Art/Sprites/Star.png — набор значков пересобирать не нужно.
        /// </summary>
        private static void BuildIconSprites(Zonk.UI.UiConfig ui)
        {
            if (ui == null)
                return;

            var folder = Root + "/Art/Sprites";
            EnsureFolder(folder);
            var texturePath = folder + "/Star.png";
            if (!File.Exists(texturePath))
            {
                const int size = 64;
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var inside = StarCoverage(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f, size * 0.48f, size * 0.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, inside);
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
            }

            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var assetPath = folder + "/Icons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>(assetPath);

            // Набор без версии или без звезды — сломан (TextMeshPro принял его за старый формат и очистил таблицы):
            // пересоздаём. Рабочий набор, в том числе дополненный руками, не трогаем.
            if (icons != null && (string.IsNullOrEmpty(SpriteAssetVersion(icons)) || icons.spriteCharacterTable.Count == 0))
            {
                if (ui.Icons == icons)
                    ui.Icons = null;
                AssetDatabase.DeleteAsset(assetPath);
                icons = null;
            }

            if (ui.Icons != null)
                return;

            var starTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<TMPro.TMP_SpriteAsset>();
                icons.name = "Icons";
                icons.spriteSheet = starTexture;

                var width = starTexture.width;
                var height = starTexture.height;
                var glyph = new TMPro.TMP_SpriteGlyph(0, new UnityEngine.TextCore.GlyphMetrics(width, height, 0f, height * 0.82f, width),
                    new UnityEngine.TextCore.GlyphRect(0, 0, width, height), 1f, 0, starSprite);
                icons.spriteGlyphTable.Add(glyph);
                icons.spriteCharacterTable.Add(new TMPro.TMP_SpriteCharacter(0x2605, glyph) { name = "star" });

                var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "Icons Material" };
                material.SetTexture(TMPro.ShaderUtilities.ID_MainTex, starTexture);
                icons.material = material;

                AssetDatabase.CreateAsset(icons, assetPath);
                AssetDatabase.AddObjectToAsset(material, icons);

                // Версия формата обязательна: без неё TextMeshPro считает набор старым и «обновляет» его, стирая
                // таблицы (и падает на пустом старом списке). Ставится до первого обращения к таблицам.
                var serialized = new SerializedObject(icons);
                serialized.FindProperty("m_Version").stringValue = "1.1.0";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                icons.UpdateLookupTables();
                EditorUtility.SetDirty(icons);
                AssetDatabase.SaveAssetIfDirty(icons);
            }

            ui.Icons = icons;
            EditorUtility.SetDirty(ui);
        }

        private static string SpriteAssetVersion(TMPro.TMP_SpriteAsset asset)
        {
            var property = new SerializedObject(asset).FindProperty("m_Version");
            return property != null ? property.stringValue : null;
        }

        /// <summary>Покрытие пикселя пятиконечной звездой (центр в 0,0) с мягким краем.</summary>
        private static float StarCoverage(float x, float y, float outer, float inner)
        {
            var angle = Mathf.Atan2(x, y);
            var radius = Mathf.Sqrt(x * x + y * y);
            var sector = Mathf.PI * 2f / 5f;
            var local = Mathf.Repeat(angle, sector) - sector * 0.5f;
            // Граница луча: от внешней вершины (local = 0) к внутренней (local = ±sector/2).
            var t = Mathf.Abs(local) / (sector * 0.5f);
            var edge = Mathf.Lerp(outer, inner, Mathf.Pow(t, 0.9f));
            return Mathf.Clamp01(edge - radius + 0.5f);
        }

        private static Texture2D PaintPortrait(Color body, Headwear headwear, bool boss, int seed)
        {
            var size = PortraitSize;
            var pixels = new Color[size * size];
            var dark = body * 0.35f;
            dark.a = 1f;
            var face = Color.Lerp(body, new Color(0.96f, 0.84f, 0.7f), 0.55f);
            var hat = Color.Lerp(body, Color.black, 0.65f);
            var trim = new Color(1f, 0.8f, 0.3f);
            var random = new System.Random(seed);
            var eyeShift = (float)random.NextDouble() * 6f - 3f;
            var smile = (float)random.NextDouble() * 6f - 2f;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Фон: мягкий вертикальный градиент с виньеткой.
                var dx = (x - 128f) / 128f;
                var dy = (y - 128f) / 128f;
                var vignette = 1f - Mathf.Clamp01((dx * dx + dy * dy) * 0.5f);
                var color = Color.Lerp(dark * 0.6f, Color.Lerp(dark, body, 0.35f), y / (float)size) * (0.6f + 0.4f * vignette);

                // Плечи и шея.
                color = Paint(color, body * 0.85f, Ellipse(x, y, 128f, 0f, 112f, 92f));
                color = Paint(color, face * 0.9f, Box(x, y, 112f, 70f, 144f, 110f));

                // Голова и лицо.
                color = Paint(color, face, Ellipse(x, y, 128f, 140f, 50f, 58f));
                var eyeY = 150f + eyeShift;
                color = Paint(color, Color.white, Ellipse(x, y, 108f, eyeY, 9f, 7f));
                color = Paint(color, Color.white, Ellipse(x, y, 148f, eyeY, 9f, 7f));
                color = Paint(color, new Color(0.12f, 0.08f, 0.06f), Ellipse(x, y, 109f, eyeY, 4f, 4f));
                color = Paint(color, new Color(0.12f, 0.08f, 0.06f), Ellipse(x, y, 149f, eyeY, 4f, 4f));
                var mouth = Mathf.Abs(y - (116f + smile * Mathf.Pow((x - 128f) / 18f, 2f) * 0.3f));
                if (x > 112f && x < 144f)
                    color = Paint(color, new Color(0.35f, 0.12f, 0.1f), 1f - Mathf.Clamp01(mouth - 1.5f));

                // Головной убор.
                switch (headwear)
                {
                    case Headwear.Hood:
                        var outer = Ellipse(x, y, 128f, 150f, 74f, 84f);
                        var inner = Ellipse(x, y, 128f, 138f, 54f, 62f);
                        color = Paint(color, hat, Mathf.Clamp01(outer - inner));
                        break;
                    case Headwear.TopHat:
                        color = Paint(color, hat, Box(x, y, 92f, 190f, 164f, 250f));
                        color = Paint(color, hat * 1.4f, Box(x, y, 74f, 182f, 182f, 196f));
                        color = Paint(color, trim * 0.8f, Box(x, y, 92f, 198f, 164f, 206f));
                        break;
                    case Headwear.Tricorn:
                        var tricorn = Box(x, y, 60f, 184f, 196f, 200f) +
                                      (y > 196f && Mathf.Abs(x - 128f) < (238f - y) * 1.2f ? 1f : 0f);
                        color = Paint(color, hat, Mathf.Clamp01(tricorn));
                        color = Paint(color, trim, Box(x, y, 60f, 184f, 196f, 189f));
                        break;
                }

                // Рамка: у босса золотая и толще.
                var edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                var frame = boss ? 10f : 4f;
                if (edge < frame)
                    color = boss ? trim * (0.8f + 0.2f * Mathf.Sin(x * 0.3f + y * 0.3f)) : new Color(0.1f, 0.08f, 0.06f);

                color.a = 1f;
                pixels[y * size + x] = color;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Покрытие точки эллипсом 0..1 с мягким краем в 1.5 пикселя.</summary>
        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            var nx = (x - cx) / rx;
            var ny = (y - cy) / ry;
            var distance = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
            return Mathf.Clamp01(0.5f - distance / 1.5f);
        }

        private static float Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1 ? 1f : 0f;
        }

        private static Color Paint(Color under, Color over, float amount)
        {
            return amount <= 0f ? under : Color.Lerp(under, over, Mathf.Clamp01(amount));
        }
    }
}
