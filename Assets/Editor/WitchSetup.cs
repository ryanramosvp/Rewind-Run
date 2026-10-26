using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Monta a bruxa jogável a partir do KayKit Adventurers: configura o rig,
/// liga as animações ao avatar dela, gera o Animator Controller e salva o
/// prefab do jogador com todos os componentes já conectados.
/// </summary>
public static class WitchSetup
{
    public const string PrefabPath = "Assets/Prefabs/Bruxa.prefab";

    const string kCharacterPath = KayKitCharacterPostprocessor.kCharactersFolder + "/Mage.fbx";
    const string kAnimationsFolder = KayKitCharacterPostprocessor.kAnimationsFolder;
    const string kControllerPath = KayKitCharacterPostprocessor.kRoot + "/Bruxa.controller";
    const string kInputAssetPath = "Assets/InputSystem_Actions.inputactions";

    // Medidas tiradas da malha da Mage: pés em 0, topo da cabeça em 2,16 e a
    // ponta do chapéu em 2,65. O collider para na cabeça de propósito - senão
    // o chapéu esbarraria em tudo.
    const float kBodyHeight = 2.1f;
    const float kBodyRadius = 0.42f;

    [MenuItem("Rewind Run/Preparar Bruxa", priority = 10)]
    public static void BuildMenuItem() => Build();

    /// <summary>Devolve o prefab da bruxa, montando tudo do zero.</summary>
    public static GameObject Build()
    {
        var avatar = ConfigureCharacterRig();
        if (avatar == null) return null;

        var clips = ConfigureAnimationFiles(avatar);
        if (clips.Count == 0)
        {
            Debug.LogError($"Nenhum clipe de animação encontrado em {kAnimationsFolder}.");
            return null;
        }

        var controller = BuildAnimatorController(clips);
        var prefab = BuildPlayerPrefab(avatar, controller);

        AssetDatabase.SaveAssets();
        Debug.Log($"Bruxa pronta em {PrefabPath} ({clips.Count} clipes de animação importados).");
        return prefab;
    }

    /// <summary>Importa a Mage como Generic e devolve o avatar gerado pelo rig dela.</summary>
    static Avatar ConfigureCharacterRig()
    {
        var importer = AssetImporter.GetAtPath(kCharacterPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Não achei {kCharacterPath}. O pack KayKit Adventurers foi importado?");
            return null;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.SaveAndReimport();

        var avatar = AssetDatabase.LoadAllAssetsAtPath(kCharacterPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null)
            Debug.LogError($"O import de {kCharacterPath} não gerou avatar. Confira o rig no Inspector.");

        return avatar;
    }

    /// <summary>
    /// Aponta os arquivos de animação para o avatar da bruxa (Copy From Other
    /// Avatar) e coleta os clipes resultantes por nome.
    /// </summary>
    static Dictionary<string, AnimationClip> ConfigureAnimationFiles(Avatar avatar)
    {
        var clips = new Dictionary<string, AnimationClip>();

        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { kAnimationsFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.sourceAvatar = avatar;
            KayKitCharacterPostprocessor.ConfigureAnimationClips(importer);
            importer.SaveAndReimport();

            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
            {
                // O Unity guarda um clipe oculto de preview junto dos de verdade.
                if (clip.name.StartsWith("__preview__")) continue;
                clips[clip.name] = clip;
            }
        }

        return clips;
    }

    static AnimatorController BuildAnimatorController(Dictionary<string, AnimationClip> clips)
    {
        EditorSetupUtil.EnsureFolder(kControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(kControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter(PlayerAnimator.StateParameter, AnimatorControllerParameterType.Int);

        var machine = controller.layers[0].stateMachine;

        // Parada -> andando -> correndo num blend tree só, guiado pela
        // velocidade real do personagem.
        //
        // Os limites acompanham o moveSpeed de 13. Como não existe mais botão
        // de correr, no teclado a bruxa vive acima de 11 e a corrida é o estado
        // normal; o trecho de caminhada só aparece na arrancada, ou no analógico
        // de um controle, que entrega velocidade parcial.
        var locomotion = controller.CreateBlendTreeInController("Locomocao", out var tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        AddToTree(tree, clips, "Idle_A", 0f);
        AddToTree(tree, clips, "Walking_A", 5f);
        AddToTree(tree, clips, "Running_A", 11f);

        // O nome vem do runtime: o PlayerAnimator reinicia este estado a cada
        // pulo, então os dois lados não podem divergir.
        var air = machine.AddState(PlayerAnimator.AirStateName);
        air.motion = Find(clips, "Jump_Idle");

        var land = machine.AddState("Aterrissagem");
        land.motion = Find(clips, "Jump_Land");

        // Sem clipe próprio para nenhum dos dois.
        //
        // O slide usa a pose parada: reclinada 50° para trás pelo PlayerBodyPose,
        // as pernas retas apontam para a frente e o tronco para trás, que é a
        // silhueta de escorregar sentada. A pose de ar não serve aqui - braços
        // abertos leem como queda, não como deslize. Se quiser experimentar
        // outras, "Idle_B", "Interact" e "PickUp" são as mais próximas de agachar.
        var slide = machine.AddState("Slide");
        slide.motion = Find(clips, "Idle_A");

        machine.defaultState = locomotion;

        // De qualquer estado para o slide: ele pode começar correndo, pulando
        // ou caindo, e listar cada origem seria repetição pura.
        AddStateEntry(machine, slide, (int)PlayerMoveState.Slide);
        ExitToNormal(slide, locomotion);

        // Sair do chão corta na hora; as transições de volta são curtas para o
        // controle não parecer atrasado em relação à animação.
        Connect(locomotion, air, "Grounded", false, 0.08f);
        Connect(air, land, "Grounded", true, 0.05f);
        Connect(land, air, "Grounded", false, 0.05f);

        var backToLocomotion = land.AddTransition(locomotion);
        backToLocomotion.hasExitTime = true;
        backToLocomotion.exitTime = 0.55f;
        backToLocomotion.duration = 0.15f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static GameObject BuildPlayerPrefab(Avatar avatar, AnimatorController controller)
    {
        EditorSetupUtil.EnsureFolder(PrefabPath);

        var root = new GameObject("Bruxa");

        var body = root.AddComponent<CharacterController>();
        body.height = kBodyHeight;
        body.radius = kBodyRadius;
        body.center = new Vector3(0f, kBodyHeight * 0.5f, 0f);
        body.slopeLimit = 50f;
        body.stepOffset = 0.4f;
        body.skinWidth = 0.03f;

        // Três transforms encadeados porque cada efeito precisa de um pivô
        // diferente e não pode sobrescrever o do outro:
        //   Orientacao -> recebe o giro para a direção do movimento
        //   Giro       -> pivô na cintura, onde a cambalhota gira
        //   Modelo     -> pivô nos pés, onde o squash achata sem afundar no chão
        var orientation = new GameObject("Orientacao");
        orientation.transform.SetParent(root.transform, false);

        var spin = new GameObject("Giro");
        spin.transform.SetParent(orientation.transform, false);
        spin.transform.localPosition = new Vector3(0f, kBodyHeight * 0.5f, 0f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(kCharacterPath);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, spin.transform);
        visual.name = "Modelo";
        visual.transform.SetLocalPositionAndRotation(
            new Vector3(0f, -kBodyHeight * 0.5f, 0f), Quaternion.identity);

        var animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var reader = root.AddComponent<PlayerInputReader>();
        EditorSetupUtil.SetReference(reader, "inputActions",
            AssetDatabase.LoadAssetAtPath<Object>(kInputAssetPath));

        var player = root.AddComponent<PlayerController>();
        EditorSetupUtil.SetReference(player, "input", reader);
        EditorSetupUtil.SetReference(player, "visual", orientation.transform);

        root.AddComponent<PlayerRewind>();

        var playerAnimator = root.AddComponent<PlayerAnimator>();
        EditorSetupUtil.SetReference(playerAnimator, "animator", animator);

        var squash = root.AddComponent<PlayerSquash>();
        EditorSetupUtil.SetReference(squash, "visual", visual.transform);

        var pose = root.AddComponent<PlayerBodyPose>();
        EditorSetupUtil.SetReference(pose, "visual", spin.transform);

        // Ferramenta de desenvolvimento: mostra as condições de cada estado.
        var voice = root.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;

        var audio = root.AddComponent<PlayerAudio>();
        EditorSetupUtil.SetReference(audio, "jump",
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pulo.mp3"));

        var shadow = root.AddComponent<PlayerDropShadow>();
        EditorSetupUtil.SetReference(shadow, "material", DropShadowMaterial());

        var rewindFx = root.AddComponent<PlayerRewindFx>();
        EditorSetupUtil.SetReference(rewindFx, "glowMaterial", RewindGlowMaterial());

        root.AddComponent<PlayerDebugHud>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    const string kShadowFolder = "Assets/Art/Generated/Bruxa";

    /// <summary>
    /// Material da sombra de pouso: URP Unlit, opaco, quase preto com um
    /// fundo de vermelho para não destoar da luz da lava.
    /// </summary>
    static Material DropShadowMaterial()
    {
        System.IO.Directory.CreateDirectory(kShadowFolder);

        string path = kShadowFolder + "/SombraDePouso.mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "SombraDePouso" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", new Color(0.09f, 0.04f, 0.035f));

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>
    /// Material dos efeitos do rewind: URP Unlit transparente e aditivo, sem
    /// escrever profundidade e sem descartar faces de trás. Aditivo porque o
    /// eco tem de parecer luz, não um fantasma cinza por cima do cenário - e
    /// luz somada nunca escurece a lava atrás dela.
    ///
    /// As propriedades são postas uma a uma porque a interface do material
    /// (que faria isso sozinha) só roda quando alguém o abre no Inspector.
    /// </summary>
    static Material RewindGlowMaterial()
    {
        System.IO.Directory.CreateDirectory(kShadowFolder);

        string path = kShadowFolder + "/BrilhoDoRewind.mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "BrilhoDoRewind" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", new Color(0.35f, 0.78f, 1f, 0.5f));

        material.SetFloat("_Surface", 1f);   // transparente
        material.SetFloat("_Blend", 2f);     // aditivo
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void AddStateEntry(AnimatorStateMachine machine, AnimatorState state, int value)
    {
        var transition = machine.AddAnyStateTransition(state);
        transition.AddCondition(AnimatorConditionMode.Equals, value, PlayerAnimator.StateParameter);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
    }

    static void ExitToNormal(AnimatorState from, AnimatorState to)
    {
        var transition = from.AddTransition(to);
        transition.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMoveState.Normal,
                                PlayerAnimator.StateParameter);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
    }

    static void AddToTree(BlendTree tree, Dictionary<string, AnimationClip> clips, string name, float threshold)
    {
        var clip = Find(clips, name);
        if (clip != null) tree.AddChild(clip, threshold);
    }

    static AnimationClip Find(Dictionary<string, AnimationClip> clips, string name)
    {
        if (clips.TryGetValue(name, out var clip)) return clip;

        Debug.LogWarning($"Clipe '{name}' não encontrado. Disponíveis: {string.Join(", ", clips.Keys)}");
        return null;
    }

    static void Connect(AnimatorState from, AnimatorState to, string parameter, bool value, float duration)
    {
        var transition = from.AddTransition(to);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        transition.hasExitTime = false;
        transition.duration = duration;
    }
}
