using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

public class CrunchCompress : AssetPostprocessor
{
    static readonly TextureImporterType[] TargetTypes = {
        TextureImporterType.Default,
        TextureImporterType.NormalMap,
        TextureImporterType.SingleChannel
    };

    const int DefaultQuality = 50;
    const int RampMatcapQuality = 75;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (!TargetTypes.Contains(importer.textureType)) return;

        SetMaxSize(importer);
        ApplyCrunch(importer);
    }

    void SetMaxSize(TextureImporter importer)
    {
        string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), importer.assetPath);
        int width = 0, height = 0;

        if (File.Exists(fullPath))
        {
            var fileData = File.ReadAllBytes(fullPath);
            var temp = new Texture2D(2, 2);
            if (temp.LoadImage(fileData, false))
            {
                width = temp.width;
                height = temp.height;
                Object.DestroyImmediate(temp);
            }
        }

        if (width == 0 || height == 0)
        {
            importer.maxTextureSize = 2048;
            Debug.LogWarning($"CrunchCompress: Could not read size for {importer.assetPath}, using 2048.");
            return;
        }

        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
    }

    void ApplyCrunch(TextureImporter importer)
    {
        bool isRampOrMatcap = importer.assetPath.ToLower().Contains("ramp") ||
                               importer.assetPath.ToLower().Contains("matcap");

        if (!importer.mipmapEnabled)
            Debug.LogWarning($"CrunchCompress: Texture missing mipmaps: {importer.assetPath}");

        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.streamingMipmapsPriority = 0;

        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = isRampOrMatcap ? RampMatcapQuality : DefaultQuality;

        Debug.Log($"CrunchCompress applied: {importer.assetPath} (Quality: {(isRampOrMatcap ? RampMatcapQuality : DefaultQuality)}, Max Size: {importer.maxTextureSize})");
    }

    [MenuItem("Assets/Trylk Tools/Crunch Compress", false, 2000)]
    public static void ManualCrunch()
    {
        var textures = Selection.objects.OfType<Texture2D>().ToArray();
        if (textures.Length == 0)
        {
            EditorUtility.DisplayDialog("Crunch Compress", "No textures selected!", "OK");
            return;
        }

        int appliedCount = 0;

        foreach (var tex in textures)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool isRampOrMatcap = path.ToLower().Contains("ramp") || path.ToLower().Contains("matcap");

            if (!importer.mipmapEnabled)
                Debug.LogWarning($"CrunchCompress: Texture missing mipmaps: {path}");

            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(tex.width, tex.height));
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;

            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = isRampOrMatcap ? RampMatcapQuality : DefaultQuality;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            appliedCount++;

            Debug.Log($"Manual Crunch applied: {path} (Quality: {(isRampOrMatcap ? RampMatcapQuality : DefaultQuality)}, Max Size: {importer.maxTextureSize})");
        }

        EditorUtility.DisplayDialog("Crunch Compress", $"Applied Crunch to {appliedCount} textures.", "OK");
    }
}