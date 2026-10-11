using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Presets de import do KayKit Adventurers (personagens e animações).
///
/// O rig é Generic e não Humanoid de propósito: os clipes foram feitos para o
/// próprio Rig_Medium, e Generic reproduz o movimento exatamente como o autor
/// desenhou, sem passar pelo retargeting. Se um dia entrarem animações de fora
/// (Mixamo, por exemplo), aí sim vale trocar para Humanoid.
/// </summary>
public class KayKitCharacterPostprocessor : AssetPostprocessor
{
    public const string kRoot = "Assets/Art/KayKit_Adventurers";
    public const string kCharactersFolder = kRoot + "/Characters";
    public const string kAnimationsFolder = kRoot + "/Animations";

    /// <summary>Clipes que devem repetir em loop; o resto toca uma vez só.</summary>
    static readonly string[] kLoopingClips =
    {
        "Idle_A", "Idle_B", "Walking_A", "Walking_B", "Walking_C",
        "Running_A", "Running_B", "Jump_Idle",
    };

    bool IsCharacter => assetPath.StartsWith(kCharactersFolder, StringComparison.Ordinal);
    bool IsAnimation => assetPath.StartsWith(kAnimationsFolder, StringComparison.Ordinal);

    void OnPreprocessModel()
    {
        if (!IsCharacter && !IsAnimation) return;

        var importer = (ModelImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        if (IsCharacter)
        {
            // O personagem é só malha e esqueleto; os clipes moram nos arquivos
            // de animação, que copiam este avatar.
            importer.importAnimation = false;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
        else
        {
            importer.importAnimation = true;
            ConfigureAnimationClips(importer);
        }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(kRoot, StringComparison.Ordinal)) return;

        var importer = (TextureImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        importer.filterMode = FilterMode.Point;
        importer.anisoLevel = 1;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    /// <summary>
    /// Marca como loop os clipes que se repetem e separa o deslocamento do
    /// osso raiz da pose.
    ///
    /// Os clipes de corrida e pulo do KayKit deslocam o osso "root". Sem
    /// declarar esse osso como nó de root motion, o deslocamento entra na pose
    /// e a bruxa desliza para longe do próprio collider. Declarando, o Unity
    /// tira esse movimento da pose e entrega como root motion - que o
    /// <see cref="PlayerAnimator"/> desliga, porque quem move é o código.
    /// </summary>
    public static void ConfigureAnimationClips(ModelImporter importer)
    {
        importer.motionNodeName = "root";

        var defaults = importer.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) return;

        var configured = new List<ModelImporterClipAnimation>(defaults.Length);
        foreach (var clip in defaults)
        {
            clip.loopTime = Array.IndexOf(kLoopingClips, clip.name) >= 0;
            configured.Add(clip);
        }

        importer.clipAnimations = configured.ToArray();
    }
}
