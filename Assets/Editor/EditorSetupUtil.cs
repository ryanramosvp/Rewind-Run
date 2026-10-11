using UnityEditor;
using UnityEngine;

/// <summary>Utilitários compartilhados pelos scripts de montagem do Editor.</summary>
public static class EditorSetupUtil
{
    /// <summary>
    /// Preenche um campo [SerializeField] privado de fora do componente.
    /// Os campos são privados de propósito - só o Inspector e estes scripts de
    /// montagem escrevem neles, o resto do jogo passa pelas propriedades.
    /// </summary>
    public static void SetReference(Object target, string fieldName, Object value)
    {
        if (target == null) return;

        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"Campo '{fieldName}' não existe em {target.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Mesma ideia da <see cref="SetReference"/>, para campos de texto.</summary>
    public static void SetString(Object target, string fieldName, string value)
    {
        if (target == null) return;

        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"Campo '{fieldName}' não existe em {target.GetType().Name}.");
            return;
        }

        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Cria a pasta do asset se ela ainda não existir.</summary>
    public static void EnsureFolder(string assetPath)
    {
        var folder = System.IO.Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(folder)) return;

        System.IO.Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }
}
