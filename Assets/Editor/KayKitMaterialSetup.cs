using UnityEditor;
using UnityEngine;

/// <summary>
/// Liga as peças do KayKit a um material compartilhado com o atlas de cores.
///
/// As 370 peças declaram o mesmo material ("platformer") e a mesma textura -
/// a cor de cada peça é a região do atlas em que ela cai, não um material
/// diferente. Guardamos uma cópia só da textura, mas a busca automática de
/// textura do importer não sobe de pasta como a de material, então as peças
/// ficavam sem textura. Remapear explicitamente resolve isso de vez, e ainda
/// deixa o cenário inteiro num material só, que é ótimo para batching.
/// </summary>
public static class KayKitMaterialSetup
{
    public const string MaterialPath = KayKitAssetPostprocessor.kFolder + "/platformer.mat";

    const string kTexturePath = KayKitAssetPostprocessor.kFolder + "/platformer_texture.png";

    /// <summary>Nome do material declarado dentro dos arquivos FBX.</summary>
    const string kEmbeddedMaterialName = "platformer";

    [MenuItem("Rewind Run/Aplicar Material do KayKit", priority = 21)]
    public static void ApplyMenuItem() => Apply();

    /// <summary>Cria o material se preciso e aponta todas as peças para ele.</summary>
    public static Material Apply()
    {
        var material = CreateOrUpdateMaterial();
        if (material == null) return null;

        var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), kEmbeddedMaterialName);
        var models = AssetDatabase.FindAssets("t:Model", new[] { KayKitAssetPostprocessor.kFolder });
        int remapped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < models.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(models[i]);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

                // Já apontando para o material certo: não force um reimport à toa,
                // são 370 arquivos.
                if (importer.GetExternalObjectMap().TryGetValue(identifier, out var current)
                    && current == material)
                {
                    continue;
                }

                importer.AddRemap(identifier, material);
                importer.SaveAndReimport();
                remapped++;

                EditorUtility.DisplayProgressBar("KayKit", path, (i + 1) / (float)models.Length);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log(remapped == 0
            ? "Material do KayKit já estava aplicado em todas as peças."
            : $"Material do KayKit aplicado em {remapped} peças.");

        return material;
    }

    static Material CreateOrUpdateMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(kTexturePath);
        if (texture == null)
        {
            Debug.LogError($"Não achei o atlas em {kTexturePath}.");
            return null;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("Shader 'Universal Render Pipeline/Lit' não encontrado. O projeto está em URP?");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "platformer" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);

        // Visual estilizado: o atlas já traz a variação de cor, então brilho
        // especular quase zero mantém as peças chapadas como no material do pack.
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }
}
