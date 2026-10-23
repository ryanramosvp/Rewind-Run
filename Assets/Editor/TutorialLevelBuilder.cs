using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial: o vulcão do menu, agora jogável.
///
/// O cenário é o mesmo de propósito. O jogador acabou de vê-lo girando atrás
/// do título, e entrar nele dá a sensação de atravessar a tela do menu - além
/// de ele já ter tudo que o tutorial pede: chão largo para andar, a parede da
/// torre para o salto de parede e a espiral para a subida final.
///
/// Não há placar nem rank. Quem aprende não está correndo contra o tempo, e
/// um cronômetro na tela diria que está.
/// </summary>
public static class TutorialLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Tutorial.unity";

    // Na base, do lado de onde a câmera olha, de frente para a torre. A 1,5 m
    // da beira e não mais perto: os primeiros passos não podem acabar na lava.
    // O ângulo (169 graus) cai bem dentro da fatia de 180 da câmera radial;
    // perto de uma divisa, ela abriria a fase já trocando de lado.
    static readonly Vector3 kSpawn = new Vector3(1.5f, 3f, -7.5f);

    [MenuItem("Rewind Run/Criar Tutorial", priority = 7)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        LevelKit.CreateLighting();

        var level = new GameObject("Vulcao").transform;
        MenuSceneBuilder.BuildVolcano(level);
        CreateWideBase(level);

        var player = LevelKit.AssembleSystemsWithoutSession("Level_CorridaDeLava");
        if (player != null)
        {
            Vector3 towardTower = new Vector3(-kSpawn.x, 0f, -kSpawn.z);
            player.transform.SetPositionAndRotation(kSpawn, Quaternion.LookRotation(towardTower));
        }

        RemoveTimer();

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 0f), 80f, 80f);
        HazardSetup.EnsureRespawn();

        CreateFraming(player);
        CreateGuide(player);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Debug.Log($"Tutorial criado em {ScenePath}. Ao terminar ele carrega a Corrida de Lava.");
    }

    /// <summary>
    /// Um anel de chão em volta da base do menu, levando a beira de 6 para 12 m
    /// da torre.
    ///
    /// Sem ele o passo do salto de parede era uma armadilha. O salto empurra a
    /// 9 m/s e a bruxa fica cerca de um segundo no ar, então pousa uns 9 m à
    /// frente da parede - três metros além da beira original. Medido num teste
    /// com entrada simulada: quem acertava o salto caía na lava logo depois do
    /// "BOA!", ou seja, era punido justamente por ter aprendido.
    /// </summary>
    static void CreateWideBase(Transform root)
    {
        var ring = new GameObject("Borda").transform;
        ring.SetParent(root, false);

        for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++)
        {
            if (Mathf.Abs(x) <= 1 && Mathf.Abs(z) <= 1) continue;   // o miolo é a base do menu
            LevelKit.Place(ring, "red", "platform_6x6x2", new Vector3(x * 6f, 0f, z * 6f));
        }
    }

    /// <summary>
    /// Tira o cronômetro da HUD. Sem sessão ele ficaria parado em 00:00,00, e
    /// um relógio parado parece defeito.
    /// </summary>
    static void RemoveTimer()
    {
        var hud = Object.FindAnyObjectByType<SpeedrunHud>();
        if (hud == null) return;

        var serialized = new SerializedObject(hud);
        var property = serialized.FindProperty("timerGroup");
        if (property == null) return;

        if (property.objectReferenceValue is GameObject group) Object.DestroyImmediate(group);

        property.objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateFraming(GameObject player)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;

        // Começa no ângulo que a câmera radial vai pedir para a largada, para
        // o primeiro quadro não abrir com um giro.
        float spawnAngle = Mathf.Atan2(kSpawn.x, kSpawn.z) * Mathf.Rad2Deg;
        float yaw = Mathf.Round(Mathf.Repeat(spawnAngle, 360f) / 60f) * 60f + 180f;
        LevelKit.SetFloat(camera, "fixedYaw", yaw);
        LevelKit.SetFloat(camera, "fixedPitch", 30f);
        LevelKit.SetFloat(camera, "fixedDistance", 19f);

        var framing = camera.gameObject.AddComponent<RadialCameraFraming>();
        EditorSetupUtil.SetReference(framing, "cameraRig", camera);
        if (player != null) EditorSetupUtil.SetReference(framing, "player", player.transform);
    }

    /// <summary>
    /// Cartão de instrução no alto da tela.
    ///
    /// No alto, e não no meio: o meio é onde a bruxa está, e uma instrução
    /// sobre ela esconderia justamente o que ela pede para fazer.
    /// </summary>
    static void CreateGuide(GameObject player)
    {
        var canvas = UiBuilder.CreateCanvas("Tutorial", sortOrder: 8);
        var guide = canvas.gameObject.AddComponent<TutorialGuide>();

        var card = UiBuilder.CreateCard(canvas.transform, "Cartao", "TUTORIAL", UiBuilder.Swatch.Ouro,
                                        new Vector2(0.5f, 1f), new Vector2(0f, -150f),
                                        new Vector2(1160f, 250f), titleSize: 44);

        var title = card.Find("Titulo") != null ? card.Find("Titulo").GetComponent<Text>() : null;

        var body = UiBuilder.CreateText(card, "Instrucao", string.Empty, 40, UiBuilder.Sand,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, -42f),
                                        new Vector2(1080f, 140f));

        EditorSetupUtil.SetReference(guide, "titleLabel", title);
        EditorSetupUtil.SetReference(guide, "bodyLabel", body);
        if (player != null)
            EditorSetupUtil.SetReference(guide, "player", player.GetComponent<PlayerController>());
    }
}
