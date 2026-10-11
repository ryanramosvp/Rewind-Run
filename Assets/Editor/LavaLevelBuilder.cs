using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Monta a fase "Terra de Lava", inspirada no Lethal Lava Land do Mario 64.
///
/// O que foi copiado da referência: mar de lava como piso, ilhas soltas em
/// alturas diferentes, um vulcão central que se sobe em espiral, e caminhos que
/// se cruzam em vez de seguirem em linha - do ramo lateral se volta por cima,
/// e não pelo mesmo caminho.
///
/// O que foi adaptado: lá a fase é totalmente aberta porque a câmera é livre.
/// Aqui ela é fixa, então o traçado foi desenhado para nunca correr na direção
/// da câmera - trechos laterais são vistos de lado, que num platformer lê até
/// melhor. Cada região ajusta altura e distância da câmera por uma
/// <see cref="CameraZone"/>, mas o ângulo horizontal nunca muda.
///
/// Linguagem de cor, decidida por contraste e não por estética: verde para
/// ilha firme, azul para travessia de precisão (é a cor mais distante do
/// laranja da lava, então a borda lê de longe), vermelho para o vulcão e os
/// props de perigo, amarelo para rampas e degraus da espiral. Numa fase cujo
/// piso é laranja brilhante, distinguir chão de morte importa mais que uma
/// paleta bonita.
///
/// Orçamento de movimento usado no traçado, todo medido por simulação:
///   pulo andando    7,2 m de alcance, 3,3 m de altura
///   pulo correndo  11,3 m
///   pulo duplo     19,0 m de alcance, 6,2 m de altura
///   wall jump       2,1 m de subida por salto, num vão de 2 m
///   slide          até 15 m/s, cerca de 30 m numa rampa de 34° mais o plano
/// </summary>
public static class LavaLevelBuilder
{
    const string kScenePath = "Assets/Scenes/Level_TerraDeLava.unity";

    [MenuItem("Rewind Run/Criar Fase: Terra de Lava", priority = 5)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        CreateLighting();

        var level = new GameObject("Fase").transform;
        CreateStartIsland(level);
        CreateNarrowBridge(level);
        CreateClimb(level);
        CreateSlideBranch(level);
        CreateWallJumpShaft(level);
        CreateReturnBridge(level);
        CreateRingIsland(level);
        CreateVolcano(level);

        var zones = new GameObject("ZonasDeCamera").transform;
        CreateCameraZones(zones);

        var player = CreatePlayer();
        CreateRewindManager();
        CreateHud();
        CreateEndScreens();

        new GameObject("Sessao").AddComponent<GameSession>();

        if (player != null)
        {
            var camera = CreateCamera(player.transform);
            EditorSetupUtil.SetReference(player.GetComponent<PlayerController>(),
                                         "cameraTransform", camera.transform);
        }

        RewindEffectSetup.EnsureVolume();
        RewindEffectSetup.EnablePostProcessingOnCameras();

        // A lava acompanha o tamanho da fase: x de -35 a 75, z de -25 a 125.
        HazardSetup.EnsureLava(new Vector3(20f, 0f, 50f), 110f, 150f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);
        // Não entra no build: esta fase é protótipo de antes da revisão de
        // movimento e nada no jogo leva até ela. O construtor segue existindo
        // para quem quiser retomá-la, mas sem sujar a entrega ao ser rodado.

        Debug.Log($"Fase criada em {kScenePath}. Suba o vulcão até a bandeira no topo.");
    }

    // ------------------------------------------------------------------
    // Geografia
    // ------------------------------------------------------------------

    /// <summary>Ilha de chegada: o unico lugar da fase onde nao da para morrer.</summary>
    static void CreateStartIsland(Transform root)
    {
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 0f, 0f));
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 0f, 6f));

        Place(root, "green", "flag_A", new Vector3(0f, 1f, -2f));
        Place(root, "red", "bomb_A", new Vector3(2.2f, 1f, 4f));
        Place(root, "red", "bomb_B", new Vector3(-2.2f, 1f, 7f));
        Place(root, "neutral", "signage_arrows_right", new Vector3(2.6f, 1f, 1f), 180f);
    }

    /// <summary>
    /// Travessia sobre a lava, em pedras de 2 m e duas balsas.
    ///
    /// O alvo encolheu de 6 m para 2 m: o vao de 5 m sai andando, mas errar a
    /// direcao em meio metro ja custa a fase. E precisao, nao distancia - e e
    /// onde o jogador aprende que o rewind serve para alguma coisa.
    ///
    /// A segunda balsa cruza o caminho em vez de segui-lo. Ela so passa pela
    /// linha de embarque durante parte do ciclo, entao alem de mirar e preciso
    /// esperar a hora.
    /// </summary>
    static void CreateNarrowBridge(Transform root)
    {
        Place(root, "blue", "platform_2x2x1", new Vector3(0f, 0f, 15f));

        CreateMovingPlatform(root, "yellow", "platform_4x4x1", new Vector3(0f, 0f, 22f),
                             new Vector3(0f, 0f, 8f), travel: 3.2f, wait: 0.9f);

        Place(root, "blue", "platform_2x2x1", new Vector3(0f, 0f, 36f));

        // Pendulo: atravessa a linha do caminho de um lado ao outro.
        CreateMovingPlatform(root, "yellow", "platform_4x4x1", new Vector3(-4f, 0f, 41f),
                             new Vector3(8f, 0f, 0f), travel: 2.8f, wait: 0.7f, phase: 0.35f);
    }

    /// <summary>
    /// Escalada ate o mirante. Os degraus viraram pedras de 2 m e o ultimo
    /// trecho so sobe de elevador - sem acertar o tempo dele nao ha caminho.
    /// </summary>
    static void CreateClimb(Transform root)
    {
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 0f, 47f));

        Place(root, "yellow", "platform_2x2x1", new Vector3(0f, 2f, 54f));
        Place(root, "yellow", "platform_2x2x1", new Vector3(-5f, 4f, 58f));

        CreateMovingPlatform(root, "yellow", "platform_4x4x1", new Vector3(2f, 3f, 60f),
                             new Vector3(0f, 5f, 0f), travel: 3f, wait: 1f, phase: 0.2f);

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 8f, 66f));
        Place(root, "neutral", "signage_arrows_right", new Vector3(2.6f, 9f, 64f), 180f);
    }

    /// <summary>
    /// Ramo lateral: duas rampas de 34 graus e um vao de 10 m no meio da corrida.
    ///
    /// O vao e a razao de o ramo existir. Descer andando chega ao fim com 7 m/s
    /// e alcanca 7,2 m - nao passa. Descer deslizando chega a 15 m/s e alcanca
    /// 15 m - passa com folga. O slide deixa de ser enfeite e vira requisito.
    /// </summary>
    static void CreateSlideBranch(Transform root)
    {
        // As rampas sobem no sentido -Z; giradas 90 graus passam a descer rumo a +X.
        Place(root, "yellow", "platform_slope_6x6x4", new Vector3(6f, 5f, 66f), 90f);
        Place(root, "yellow", "platform_slope_6x6x4", new Vector3(12f, 1f, 66f), 90f);

        Place(root, "green", "platform_6x6x1", new Vector3(18f, 0f, 66f));
        Place(root, "green", "platform_6x6x1", new Vector3(24f, 0f, 66f));

        // Vao de 10 m: x de 27 a 37.
        Place(root, "green", "platform_6x6x1", new Vector3(40f, 0f, 66f));
        Place(root, "green", "platform_6x6x1", new Vector3(46f, 0f, 66f));
        Place(root, "green", "platform_6x6x1", new Vector3(52f, 0f, 66f));

        Place(root, "yellow", "star", new Vector3(24f, 1.6f, 66f));
        Place(root, "red", "cone", new Vector3(43f, 1f, 68.5f));
    }

    /// <summary>
    /// Poco de wall jump, agora de 1 m a 14 m. Sao 13 m, mais que o dobro do
    /// pulo duplo, e exige cerca de seis saltos alternando as paredes.
    /// </summary>
    static void CreateWallJumpShaft(Transform root)
    {
        foreach (float height in new[] { 1f, 5f, 9f })
        {
            Place(root, "blue", "platform_6x2x4", new Vector3(50f, height, 66f), 90f);
            Place(root, "blue", "platform_6x2x4", new Vector3(54f, height, 66f), 90f);
        }

        Place(root, "green", "platform_4x4x1", new Vector3(52f, 13f, 72f));
    }

    /// <summary>
    /// Volta por cima, saltando entre balsas em movimento.
    ///
    /// As duas balsas andam defasadas, entao nao adianta decorar um ritmo so -
    /// cada travessia pede uma leitura nova. E o trecho mais dificil da fase, e
    /// fica por cima do percurso inteiro de proposito: errar aqui da a queda
    /// mais longa, que e tambem a mais facil de desfazer com o rewind.
    /// </summary>
    static void CreateReturnBridge(Transform root)
    {
        CreateMovingPlatform(root, "yellow", "platform_4x4x1", new Vector3(44f, 13f, 72f),
                             new Vector3(-8f, 0f, 0f), travel: 3f, wait: 0.8f);

        Place(root, "blue", "platform_2x2x1", new Vector3(30f, 13f, 72f));

        CreateMovingPlatform(root, "yellow", "platform_4x4x1", new Vector3(22f, 13f, 72f),
                             new Vector3(-8f, 0f, 0f), travel: 3f, wait: 0.8f, phase: 0.5f);

        Place(root, "blue", "platform_2x2x1", new Vector3(8f, 13f, 72f));
    }

    /// <summary>
    /// Anel sobre a lava: a peca tem um buraco no meio, entao so da para
    /// contorna-la pela borda. Erro aqui cai direto na lava.
    /// </summary>
    static void CreateRingIsland(Transform root)
    {
        Place(root, "blue", "platform_hole_6x6x1", new Vector3(0f, 8f, 75f));
    }

    /// <summary>
    /// Vulcao central, subido em espiral de degraus de 2 m.
    ///
    /// O raio subiu de 5 m para 6 m: o vao entre degraus passou de 3 m para
    /// 4 m, em alvos de 2 m. E um dos degraus sobe e desce, o que quebra o
    /// ritmo decorado justamente no meio da subida.
    /// </summary>
    static void CreateVolcano(Transform root)
    {
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            Place(root, "red", "platform_6x6x2", new Vector3(x * 6f, 0f, 95f + z * 6f));

        for (float y = 2f; y < 14f; y += 4f)
            Place(root, "red", "platform_6x6x4", new Vector3(0f, y, 95f));

        const float radius = 6f;
        for (int step = 0; step < 6; step++)
        {
            float angle = step * 60f * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(angle) * radius, 2f + step * 2f,
                                       95f + Mathf.Cos(angle) * radius);

            // O degrau do meio se move: sobe e desce dois metros.
            if (step == 3)
            {
                CreateMovingPlatform(root, "yellow", "platform_2x2x1", position,
                                     new Vector3(0f, 4f, 0f), travel: 2.4f, wait: 0.8f);
            }
            else
            {
                Place(root, "yellow", "platform_2x2x1", position);
            }
        }

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 14f, 95f));
        Place(root, "green", "flag_A", new Vector3(0f, 15f, 95f));

        CreateFinishTrigger(root, new Vector3(0f, 16f, 95f));

        Place(root, "red", "bomb_A", new Vector3(-6f, 2f, 91f));
        Place(root, "red", "bomb_B", new Vector3(7f, 2f, 99f));
        Place(root, "red", "arch", new Vector3(0f, 2f, 87f));
    }

    /// <summary>
    /// Telas de morte e de vitoria.
    ///
    /// Ficam na mesma cena da fase, desligadas, em vez de cenas separadas: uma
    /// troca de cena perderia o placar da tentativa e custaria um carregamento
    /// a cada queda - e queda e a coisa mais comum do jogo.
    /// </summary>
    static void CreateEndScreens()
    {
        var canvas = UiBuilder.CreateCanvas("TelasDeFim", sortOrder: 20);
        var screen = canvas.gameObject.AddComponent<EndScreen>();

        var death = BuildPanel(canvas.transform, "TelaDeMorte", "A LAVA TE PEGOU",
                               new Color(0.55f, 0.12f, 0.05f, 0.55f),
                               new Color(1f, 0.45f, 0.25f), out var deathText);

        var victory = BuildPanel(canvas.transform, "TelaDeVitoria", "VOCE CHEGOU AO TOPO",
                                 new Color(0.08f, 0.05f, 0.12f, 0.72f),
                                 UiBuilder.Ember, out var victoryText);

        EditorSetupUtil.SetReference(screen, "deathPanel", death);
        EditorSetupUtil.SetReference(screen, "victoryPanel", victory);
        EditorSetupUtil.SetReference(screen, "deathSummary", deathText);
        EditorSetupUtil.SetReference(screen, "victorySummary", victoryText);

        var finish = Object.FindAnyObjectByType<LevelFinish>();
        if (finish != null) EditorSetupUtil.SetReference(finish, "endScreen", screen);

        death.SetActive(false);
        victory.SetActive(false);
    }

    static GameObject BuildPanel(Transform parent, string name, string title, Color tint,
                                 Color titleColor, out UnityEngine.UI.Text summary)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);

        var rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // A tintura cobre a tela inteira mas e translucida: ver onde se morreu
        // ensina mais que um fundo preto.
        UiBuilder.CreateFullScreen(holder.transform, "Tintura", tint);

        UiBuilder.CreateText(holder.transform, "Titulo", title, 100, titleColor,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1600f, 140f),
                             style: FontStyle.Bold);

        summary = UiBuilder.CreateText(holder.transform, "Resumo", string.Empty, 44, UiBuilder.Ash,
                                       new Vector2(0.5f, 0.5f), new Vector2(0f, -50f),
                                       new Vector2(1200f, 260f));

        return holder;
    }

    /// <summary>
    /// Peca que vai e volta, obedecendo ao rewind.
    ///
    /// O RewindableTransform entra junto porque uma plataforma movel e
    /// exatamente o tipo de coisa que o jogador vai querer desfazer: perder o
    /// embarque e esperar o ciclo inteiro e a espera que mais irrita num
    /// platformer, e aqui ela custa dois segundos de rewind.
    /// </summary>
    static void CreateMovingPlatform(Transform root, string color, string piece, Vector3 position,
                                     Vector3 offset, float travel, float wait, float phase = 0f)
    {
        var platform = Place(root, color, piece, position);
        if (platform == null) return;

        var mover = platform.AddComponent<MovingPlatform>();
        SetVector(mover, "offset", offset);
        SetFloat(mover, "travelSeconds", travel);
        SetFloat(mover, "waitSeconds", wait);
        SetFloat(mover, "phase", phase);

        platform.AddComponent<RewindableTransform>();
    }

    static void SetVector(Object target, string fieldName, Vector3 value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null) return;

        property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateFinishTrigger(Transform root, Vector3 position)
    {
        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.localPosition = position;

        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(4f, 3f, 4f);
        area.isTrigger = true;

        go.AddComponent<LevelFinish>();
    }

    // ------------------------------------------------------------------
    // Câmera por região
    // ------------------------------------------------------------------

    /// <summary>
    /// De onde cada região é vista.
    ///
    /// Todas com o MESMO yaw, de propósito. O movimento é relativo à câmera, e
    /// girar o yaw redefine o que "para frente" significa - a bruxa virava
    /// sozinha no meio da corrida. Pitch e distância não têm esse efeito: mudam
    /// só o enquadramento, nunca o significado do input.
    ///
    /// Trechos que correm no eixo X ficam vistos de lado, o que não é problema
    /// num platformer: de lado a altura e o vão leem melhor do que de frente.
    /// O ângulo global só precisaria mudar se a fase dobrasse para -Z, onde o
    /// jogador correria na direção da câmera - e é justamente isso que o
    /// traçado evita.
    /// </summary>
    static void CreateCameraZones(Transform root)
    {
        Zone(root, "Chegada", new Vector3(0f, 4f, 16f), new Vector3(26f, 24f, 60f), 30f, 16f);
        Zone(root, "Subida", new Vector3(0f, 7f, 58f), new Vector3(26f, 26f, 28f), 38f, 15f);
        Zone(root, "Slide", new Vector3(26f, 4f, 66f), new Vector3(44f, 18f, 14f), 22f, 24f);
        Zone(root, "Poco", new Vector3(52f, 7f, 66f), new Vector3(10f, 28f, 12f), 16f, 16f);
        Zone(root, "Retorno", new Vector3(34f, 16f, 72f), new Vector3(42f, 8f, 10f), 32f, 18f);
        Zone(root, "Anel", new Vector3(0f, 10f, 75f), new Vector3(14f, 20f, 10f), 45f, 14f);
        Zone(root, "Vulcao", new Vector3(0f, 10f, 96f), new Vector3(40f, 44f, 32f), 55f, 26f);
    }

    /// <summary>Yaw fica sempre em zero: quem muda é só o enquadramento.</summary>
    static void Zone(Transform root, string name, Vector3 center, Vector3 size,
                     float pitch, float distance)
    {
        KayKitPlacer.CreateCameraZone(root, $"Zona_{name}", new Bounds(center, size),
                                      0f, pitch, distance);
    }

    // ------------------------------------------------------------------
    // Montagem comum
    // ------------------------------------------------------------------

    static GameObject Place(Transform parent, string color, string piece, Vector3 position,
                            float rotationY = 0f) =>
        KayKitPlacer.Place(parent, color, piece, position, rotationY);

    static void CreateLighting()
    {
        var lightGo = new GameObject("Directional Light");
        lightGo.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(45f, 20f, 0f));

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.93f, 0.85f);
        light.shadows = LightShadows.Soft;

        // Ambiente quente, puxando o laranja da lava para as sombras.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.35f, 0.35f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.3f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.6f, 0.26f, 0.12f);
    }

    static GameObject CreatePlayer()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchSetup.PrefabPath) ?? WitchSetup.Build();
        if (prefab == null)
        {
            Debug.LogError("Não consegui montar a bruxa. A fase foi criada sem jogador.");
            return null;
        }

        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = new Vector3(0f, 1.2f, 0f);
        return player;
    }

    static GameObject CreateCamera(Transform target)
    {
        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 9f, -14f), Quaternion.Euler(30f, 0f, 0f));

        var camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.farClipPlane = 300f;
        cameraGo.AddComponent<AudioListener>();

        var orbit = cameraGo.AddComponent<OrbitCamera>();
        EditorSetupUtil.SetReference(orbit, "target", target);
        EditorSetupUtil.SetReference(orbit, "input", target.GetComponent<PlayerInputReader>());

        // O jogador começa dentro da primeira zona, e um trigger não dispara
        // para quem já está lá dentro - então o enquadramento inicial é ajustado
        // aqui para casar com ela.
        SetFloat(orbit, "fixedPitch", 30f);
        SetFloat(orbit, "fixedDistance", 16f);

        return cameraGo;
    }

    static void SetFloat(Object target, string fieldName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null) return;

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateRewindManager() => new GameObject("RewindManager").AddComponent<RewindManager>();

    static void CreateHud()
    {
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var barGo = new GameObject("BarraDeRewind", typeof(Image), typeof(CanvasGroup));
        barGo.transform.SetParent(canvasGo.transform, false);

        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 60f);
        barRect.sizeDelta = new Vector2(420f, 26f);

        var background = barGo.GetComponent<Image>();
        background.sprite = sprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0f, 0f, 0f, 0.55f);

        var fillGo = new GameObject("Preenchimento", typeof(Image));
        fillGo.transform.SetParent(barGo.transform, false);

        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        var fill = fillGo.GetComponent<Image>();
        fill.sprite = sprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        var hud = canvasGo.AddComponent<RewindHud>();
        EditorSetupUtil.SetReference(hud, "chargeFill", fill);
        EditorSetupUtil.SetReference(hud, "canvasGroup", barGo.GetComponent<CanvasGroup>());

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var entry in scenes)
        {
            if (entry.path == path) return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
        EditorBuildSettings.scenes = updated;
    }
}
