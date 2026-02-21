using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class MetadataSniffer : AssetPostprocessor
{
    static readonly TextureImporterType[] TargetTypes = {
        TextureImporterType.Default,
        TextureImporterType.NormalMap,
        TextureImporterType.SingleChannel
    };

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (!TargetTypes.Contains(importer.textureType)) return;

        ProcessTexture(importer);
    }

    static void ProcessTexture(TextureImporter importer)
    {
        SetMaxSize(importer);
        ApplyAlphaTransparency(importer);
    }

    static void SetMaxSize(TextureImporter importer)
    {
        string path = Path.Combine(Application.dataPath.Replace("Assets", ""), importer.assetPath);
        if (!File.Exists(path)) return;

        var data = File.ReadAllBytes(path);
        var temp = new Texture2D(2, 2);
        if (!temp.LoadImage(data, false)) return;

        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(temp.width, temp.height));
        Object.DestroyImmediate(temp);
    }

    static void ApplyAlphaTransparency(TextureImporter importer)
    {
        if (importer.textureType == TextureImporterType.NormalMap) return;

        if (TextureHasAlpha(importer.assetPath))
            importer.alphaIsTransparency = true;
    }

    static bool TextureHasAlpha(string assetPath)
    {
        string path = Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
        if (!File.Exists(path)) return false;

        var data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data, false)) return false;

        var pixels = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        int step = Mathf.Max(1, pixels.Length / 4096);
        for (int i = 0; i < pixels.Length; i += step)
        {
            if (pixels[i].a < 255) return true;
        }
        return false;
    }

    // ---------- Context Menus ----------

    [MenuItem("Assets/Trylk Tools/Sniff Folder", true)]
    static bool ValidateSniffFolder() => Selection.GetFiltered<Object>(SelectionMode.Assets)
        .Any(o => AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(o)));

    [MenuItem("Assets/Trylk Tools/Sniff Folder")]
    static void SniffFolderContext() => SniffFolder(SelectedFolders());

    [MenuItem("Assets/Trylk Tools/Sniff & Fix", true)]
    static bool ValidateSniffAndFix() => ValidateSniffFolder();

    [MenuItem("Assets/Trylk Tools/Sniff & Fix")]
    static void SniffAndFixContext() => SniffAndFix(SelectedFolders());

    // ---------- Top Menu ----------

    [MenuItem("Trylk Tools/Sniff Project")]
    static void SniffProjectMenu() => SniffFolder(new[] { "Assets" });

    [MenuItem("Trylk Tools/Sniff & Fix Project")]
    static void SniffAndFixProjectMenu() => SniffAndFix(new[] { "Assets" });

    // ---------- Folder Helpers ----------

    static string[] SelectedFolders() =>
        Selection.GetFiltered<Object>(SelectionMode.Assets)
        .Where(o => AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(o)))
        .Select(o => AssetDatabase.GetAssetPath(o))
        .ToArray();

    static void SniffFolder(string[] folders)
    {
        int normalCount = 0, singleChannelCount = 0, alphaMissingCount = 0;
        string log = "=== Metadata Sniffer Scan ===\n";

        foreach (var folder in folders)
        {
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                                               .Select(guid => AssetDatabase.GUIDToAssetPath(guid)))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                switch (importer.textureType)
                {
                    case TextureImporterType.NormalMap:
                        normalCount++;
                        log += $"[Normal Map] {path}\n";
                        break;
                    case TextureImporterType.SingleChannel:
                        singleChannelCount++;
                        log += $"[Color Ramp] {path}\n";
                        break;
                    case TextureImporterType.Default:
                        if (TextureHasAlpha(path) && !importer.alphaIsTransparency)
                        {
                            alphaMissingCount++;
                            log += $"[Alpha Missing] {path}\n";
                        }
                        else log += $"[Default] {path}\n";
                        break;
                }
            }
        }

        Debug.Log(log);

        if (EditorUtility.DisplayDialog("Metadata Sniffer",
            $"Scan Result:\n\nNormal maps: {normalCount}\nColor ramps: {singleChannelCount}\nAlpha missing: {alphaMissingCount}\n\nFix missing alpha textures?",
            "Fix Now", "Cancel"))
        {
            SniffAndFix(folders);
        }
    }

    static void SniffAndFix(string[] folders)
    {
        int fixedCount = 0;
        string log = "=== Metadata Sniffer Fix Log ===\n";

        foreach (var folder in folders)
        {
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                                               .Select(guid => AssetDatabase.GUIDToAssetPath(guid)))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (importer.textureType == TextureImporterType.Default &&
                    TextureHasAlpha(path) &&
                    !importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    EditorUtility.SetDirty(importer);
                    fixedCount++;
                    log += $"[Fixed] {path}\n";
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(log);
        EditorUtility.DisplayDialog("Metadata Sniffer", $"Sniff & Fix completed. Fixed {fixedCount} textures.", "OK");
    }
}