using UnityEditor;
using UnityEngine;

/// <summary>
/// Posiciona peças do KayKit numa cena.
///
/// As peças têm 1 unidade = 1 metro, pivô no centro da base, e o nome descreve
/// o tamanho: platform_4x4x1 ocupa 4 em X, 4 em Z e 1 em Y. Toda posição
/// passada aqui é o canto de baixo da peça.
/// </summary>
public static class KayKitPlacer
{
    public const string Folder = "Assets/Art/KayKit_Platformer";

    public static GameObject Place(Transform parent, string color, string pieceName,
                                   Vector3 position, float rotationY = 0f)
    {
        // As peças coloridas repetem a cor no nome do arquivo; as neutras não.
        string fileName = color == "neutral" ? pieceName : $"{pieceName}_{color}";
        string path = $"{Folder}/{color}/{fileName}.fbx";

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogWarning($"Peça não encontrada: {path}. A cena foi montada sem ela.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        instance.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
        return instance;
    }

    /// <summary>Caixa que envolve os renderers da peça, em espaço local dela.</summary>
    public static Bounds GetLocalBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.up * 0.5f, Vector3.one);

        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        return new Bounds(go.transform.InverseTransformPoint(worldBounds.center), worldBounds.size);
    }

    /// <summary>Cria uma zona de câmera cobrindo a caixa indicada.</summary>
    public static GameObject CreateCameraZone(Transform parent, string name, Bounds area,
                                              float yaw, float pitch, float distance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = area.center;

        var box = go.AddComponent<BoxCollider>();
        box.size = area.size;
        box.isTrigger = true;

        var zone = go.AddComponent<CameraZone>();
        SetFloat(zone, "yaw", yaw);
        SetFloat(zone, "pitch", pitch);
        SetFloat(zone, "distance", distance);

        return go;
    }

    static void SetFloat(Object target, string fieldName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null) return;

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
