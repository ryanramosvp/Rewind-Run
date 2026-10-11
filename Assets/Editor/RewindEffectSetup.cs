using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cria o Volume de pós-processamento do rewind e liga o pós-processamento na
/// câmera. Existe como menu próprio para dar para adicionar o efeito a uma cena
/// que já existe, sem precisar remontar tudo e perder ajustes manuais.
/// </summary>
public static class RewindEffectSetup
{
    public const string ProfilePath = "Assets/Settings/RewindVolumeProfile.asset";
    const string kVolumeName = "RewindVolume";

    [MenuItem("Rewind Run/Adicionar Efeito de Rewind na Cena", priority = 1)]
    public static void AddToOpenScene()
    {
        var volume = EnsureVolume();
        EnablePostProcessingOnCameras();

        EditorUtility.SetDirty(volume);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);

        Debug.Log("Efeito de rewind adicionado à cena aberta. Salve a cena para manter.");
    }

    /// <summary>Cria o Volume na cena atual, ou devolve o que já existe.</summary>
    public static Volume EnsureVolume()
    {
        var existing = Object.FindAnyObjectByType<RewindScreenEffect>();
        if (existing != null)
        {
            var found = existing.GetComponent<Volume>();
            found.sharedProfile = CreateOrLoadProfile();
            return found;
        }

        var go = new GameObject(kVolumeName);

        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;

        // Prioridade acima do volume padrão da cena, senão o perfil do ambiente
        // venceria e o efeito nunca apareceria.
        volume.priority = 10f;
        volume.weight = 0f;
        volume.sharedProfile = CreateOrLoadProfile();

        go.AddComponent<RewindScreenEffect>();
        return volume;
    }

    /// <summary>Sem isto a câmera ignora o Volume e nada aparece na tela.</summary>
    public static void EnablePostProcessingOnCameras()
    {
        foreach (var camera in Object.FindObjectsByType<Camera>())
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            data.renderPostProcessing = true;
            EditorUtility.SetDirty(camera);
        }
    }

    static VolumeProfile CreateOrLoadProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (existing != null) return existing;

        EditorSetupUtil.EnsureFolder(ProfilePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        // Separação de canais: a assinatura visual de imagem "quebrada".
        var chromatic = profile.Add<ChromaticAberration>(true);
        chromatic.intensity.value = 0.9f;

        // Vinheta azulada fecha as bordas e tira o jogador do tempo normal.
        var vignette = profile.Add<Vignette>(true);
        vignette.color.value = new Color(0.16f, 0.34f, 0.8f);
        vignette.intensity.value = 0.45f;
        vignette.smoothness.value = 0.6f;

        // Granulação: a textura de fita velha.
        var grain = profile.Add<FilmGrain>(true);
        grain.type.value = FilmGrainLookup.Medium3;
        grain.intensity.value = 0.65f;
        grain.response.value = 0.8f;

        // Dessaturar e puxar para o frio deixa claro que o mundo saiu do
        // estado normal, sem esconder o cenário.
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.value = -45f;
        color.contrast.value = 12f;
        color.colorFilter.value = new Color(0.76f, 0.86f, 1f);

        // Puxão sutil nas bordas, para a tela parecer sugada para trás.
        var lens = profile.Add<LensDistortion>(true);
        lens.intensity.value = -0.22f;

        // Cada override precisa virar sub-asset, senão o perfil salva vazio.
        foreach (var component in profile.components)
            AssetDatabase.AddObjectToAsset(component, profile);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath);
        return profile;
    }
}
