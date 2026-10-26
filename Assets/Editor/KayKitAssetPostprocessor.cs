using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Presets de import dos assets do KayKit Platformer Pack.
///
/// O postprocessor só age no primeiro import de cada arquivo, para não desfazer
/// ajustes manuais feitos depois no Inspector. Isso tem um efeito colateral que
/// já mordeu uma vez: assets importados ANTES deste script existir ficam com os
/// presets errados para sempre, porque nunca mais passam por um "primeiro
/// import". É para isso que serve o menu de reaplicar lá embaixo.
/// </summary>
public class KayKitAssetPostprocessor : AssetPostprocessor
{
    public const string kFolder = "Assets/Art/KayKit_Platformer";

    /// <summary>
    /// Peças que o jogador pisa ou esbarra e que ganham MeshCollider no import.
    /// O resto (colecionáveis, molas, botões, placas) recebe collider de trigger
    /// no prefab, porque a forma exata da malha não importa para eles.
    /// </summary>
    static readonly string[] kSolidPrefixes =
    {
        "platform_", "floor_", "pillar_", "barrier_", "structure_",
        "arch", "railing_", "strut_", "bracing_", "pipe_",
    };

    static bool IsKayKitAsset(string path) => path.StartsWith(kFolder, StringComparison.Ordinal);

    void OnPreprocessModel()
    {
        if (!IsKayKitAsset(assetPath)) return;

        var importer = (ModelImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        ApplyModelSettings(importer);
    }

    void OnPreprocessTexture()
    {
        if (!IsKayKitAsset(assetPath)) return;

        var importer = (TextureImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        ApplyTextureSettings(importer);
    }

    /// <summary>
    /// Reaplica os presets em tudo, ignorando a guarda de primeiro import.
    /// Use depois de mexer nas regras aqui, ou se as peças estiverem sem colisão.
    /// </summary>
    [MenuItem("Rewind Run/Reaplicar Presets de Import do KayKit", priority = 20)]
    public static void ReapplyAll()
    {
        var models = AssetDatabase.FindAssets("t:Model", new[] { kFolder });
        var textures = AssetDatabase.FindAssets("t:Texture", new[] { kFolder });
        int total = models.Length + textures.Length;
        int done = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var guid in models)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                {
                    ApplyModelSettings(importer);
                    importer.SaveAndReimport();
                }

                EditorUtility.DisplayProgressBar("KayKit", path, ++done / (float)total);
            }

            foreach (var guid in textures)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    ApplyTextureSettings(importer);
                    importer.SaveAndReimport();
                }

                EditorUtility.DisplayProgressBar("KayKit", path, ++done / (float)total);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Presets reaplicados em {models.Length} modelos e {textures.Length} texturas do KayKit.");
    }

    static void ApplyModelSettings(ModelImporter importer)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
        importer.addCollider = IsSolidPiece(fileName);

        // São props estáticos: sem rig, sem animação, sem câmeras/luzes do FBX.
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
    }

    static void ApplyTextureSettings(TextureImporter importer)
    {
        // O atlas do KayKit é dividido em faixas de cor chapada. Filtro bilinear
        // mistura as faixas vizinhas e suja as bordas; Point mantém a cor exata.
        importer.filterMode = FilterMode.Point;
        importer.anisoLevel = 1;
        importer.mipmapEnabled = true;

        // Cores chapadas e gradientes suaves são o pior caso do DXT (banding
        // visível). O BC7 (CompressedHQ) não tem esse problema e ocupa um
        // quarto do atlas sem compressão - ver BuildTools.
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }

    static bool IsSolidPiece(string fileName)
    {
        foreach (var prefix in kSolidPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
