using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Monta o menu inicial.
///
/// O fundo não é imagem: é um pedaço jogável do mundo - vulcão, lava e ilhas -
/// com a câmera orbitando. E a cada poucos segundos a cena REBOBINA: o efeito
/// de rewind toma a tela e a órbita corre para trás.
///
/// Isso foi escolhido no lugar de um título bonito e estático porque o jogo tem
/// uma ideia só, e ela é visual. Quem olha a tela por dez segundos entende o
/// que o jogo faz sem ler uma linha de texto - que é mais do que qualquer
/// subtítulo conseguiria.
/// </summary>
public static class MenuSceneBuilder
{
    const string kScenePath = "Assets/Scenes/Menu.unity";

    [MenuItem("Rewind Run/Criar Menu Inicial", priority = 6)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        CreateLighting();
        var focus = CreateDiorama();
        var volume = CreateRewindVolume();
        var camera = CreateCamera(focus, volume);

        CreateInterface();
        LevelKit.CreateAudio();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);
        RegisterScenes();

        Debug.Log($"Menu criado em {kScenePath}. Ele é a primeira cena do build.");
        Selection.activeGameObject = camera;
    }

    /// <summary>
    /// Um pedaço do mundo em miniatura: vulcão ao centro, ilhas em volta e a
    /// lava embaixo. Tudo com as mesmas peças da fase, para o menu não parecer
    /// de outro jogo.
    /// </summary>
    static Transform CreateDiorama()
    {
        var root = new GameObject("Diorama").transform;

        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            Place(root, "red", "platform_6x6x2", new Vector3(x * 6f, 0f, z * 6f));

        for (float y = 2f; y < 14f; y += 4f)
            Place(root, "red", "platform_6x6x4", new Vector3(0f, y, 0f));

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 14f, 0f));
        Place(root, "green", "flag_A", new Vector3(0f, 15f, 0f));

        // Espiral igual à da fase: é a silhueta que identifica o jogo.
        for (int step = 0; step < 6; step++)
        {
            float angle = step * 60f * Mathf.Deg2Rad;
            Place(root, "yellow", "platform_2x2x1",
                  new Vector3(Mathf.Sin(angle) * 6f, 2f + step * 2f, Mathf.Cos(angle) * 6f));
        }

        // Ilhas soltas ao redor, para a órbita ter o que atravessar.
        Place(root, "blue", "platform_6x2x1", new Vector3(-16f, 3f, 8f), 90f);
        Place(root, "green", "platform_6x6x1", new Vector3(18f, 1f, -10f));
        Place(root, "blue", "platform_2x2x1", new Vector3(13f, 5f, 12f));
        Place(root, "yellow", "platform_4x4x1", new Vector3(-14f, 6f, -12f));
        Place(root, "red", "arch", new Vector3(0f, 2f, -14f));
        Place(root, "red", "bomb_A", new Vector3(-6f, 2f, 4f));

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 0f), 80f, 80f);

        var focus = new GameObject("FocoDaOrbita").transform;
        focus.SetParent(root, false);
        focus.localPosition = new Vector3(0f, 2f, 0f);

        return focus;
    }

    static Volume CreateRewindVolume()
    {
        var volume = RewindEffectSetup.EnsureVolume();

        // O menu dirige o peso sozinho; o componente de jogo procura um
        // RewindManager que aqui não existe.
        if (volume.TryGetComponent<RewindScreenEffect>(out var effect))
            Object.DestroyImmediate(effect);

        volume.weight = 0f;
        return volume;
    }

    static GameObject CreateCamera(Transform focus, Volume volume)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };

        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 55f;
        camera.farClipPlane = 300f;
        go.AddComponent<AudioListener>();

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;

        var diorama = go.AddComponent<MenuDiorama>();
        EditorSetupUtil.SetReference(diorama, "target", focus);
        EditorSetupUtil.SetReference(diorama, "rewindVolume", volume);

        return go;
    }

    static void CreateInterface()
    {
        var canvas = UiBuilder.CreateCanvas("Menu", sortOrder: 10);
        var root = canvas.transform;
        var menu = canvas.gameObject.AddComponent<MainMenu>();

        CreateTitle(root);

        // Os botoes vivem num grupo proprio: as telas de controles e opcoes
        // escondem o grupo inteiro em vez de abrir por cima dele.
        // Descidos de -40 para -130: é o que abre os 148 px de respiro entre a
        // base do "RUN" e o topo do JOGAR, e ainda deixa o SAIR a 182 px do pé
        // da tela.
        var group = UiBuilder.CreateRect(root, "Botoes", new Vector2(0f, 0.5f),
                                         new Vector2(420f, -130f), new Vector2(560f, 520f));

        var play = UiBuilder.CreateButton(group, "JOGAR", new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, 150f), new Vector2(520f, 96f),
                                          UiBuilder.Swatch.Laranja);
        var controls = UiBuilder.CreateButton(group, "CONTROLES", new Vector2(0.5f, 0.5f),
                                              new Vector2(0f, 40f), new Vector2(520f, 96f),
                                              UiBuilder.Swatch.Ouro);
        var settings = UiBuilder.CreateButton(group, "OPCOES", new Vector2(0.5f, 0.5f),
                                              new Vector2(0f, -70f), new Vector2(520f, 96f),
                                              UiBuilder.Swatch.Terracota);
        var quit = UiBuilder.CreateButton(group, "SAIR", new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, -180f), new Vector2(520f, 96f),
                                          UiBuilder.Swatch.Ardosia);

        UiBuilder.Bind(play, menu, nameof(MainMenu.Play));
        UiBuilder.Bind(controls, menu, nameof(MainMenu.OpenControls));
        UiBuilder.Bind(settings, menu, nameof(MainMenu.OpenSettings));
        UiBuilder.Bind(quit, menu, nameof(MainMenu.Quit));

        var panel = CreateControlsPanel(root, menu, out var back);
        var options = CreateSettingsPanel(root, menu, out var optionsBack,
                                          out var timerLabel, out var speedLabel,
                                          out var timerPill, out var speedPill);

        EditorSetupUtil.SetReference(menu, "buttonsGroup", group.gameObject);
        EditorSetupUtil.SetReference(menu, "controlsPanel", panel);
        EditorSetupUtil.SetReference(menu, "settingsPanel", options);
        EditorSetupUtil.SetReference(menu, "firstButton", play);
        EditorSetupUtil.SetReference(menu, "backButton", back);
        EditorSetupUtil.SetReference(menu, "settingsBackButton", optionsBack);
        EditorSetupUtil.SetReference(menu, "timerToggleLabel", timerLabel);
        EditorSetupUtil.SetReference(menu, "speedToggleLabel", speedLabel);
        EditorSetupUtil.SetReference(menu, "timerPill", timerPill);
        EditorSetupUtil.SetReference(menu, "speedPill", speedPill);
    }

    /// <summary>
    /// Titulo a esquerda, com uma barra de brasa por baixo.
    ///
    /// Fica de lado e nao centralizado porque o vulcao esta no meio da tela:
    /// centralizado, o texto cobriria justamente a coisa que o fundo existe
    /// para mostrar.
    /// </summary>
    static void CreateTitle(Transform root)
    {
        // Ancorado no TOPO, não no meio. Título e botões estavam os dois
        // presos ao centro da tela, a 230 e a -40 - e com as letras em 130 px o
        // "RUN" descia 42 px por cima do botão JOGAR. Preso ao topo, o título
        // se afasta dos botões em tela alta em vez de se aproximar.
        var card = UiBuilder.CreateRect(root, "Titulo", new Vector2(0f, 1f),
                                        new Vector2(430f, -210f), new Vector2(760f, 240f));

        UiBuilder.CreateText(card, "Nome", "REWIND", 130, UiBuilder.Sand,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(760f, 160f),
                             TextAnchor.MiddleLeft, FontStyle.Bold);

        UiBuilder.CreateText(card, "Sobrenome", "RUN", 130, UiBuilder.Ember,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(760f, 160f),
                             TextAnchor.MiddleLeft, FontStyle.Bold);

        UiBuilder.CreateBlock(card, "Barra", UiBuilder.Swatch.Brasa, new Vector2(0.5f, 0.5f),
                              new Vector2(-150f, -10f), new Vector2(440f, 10f));
    }

    static GameObject CreateControlsPanel(Transform root, MainMenu menu, out Button back)
    {
        var holder = UiBuilder.CreateStretched(root, "TelaDeControles");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.035f, 0.95f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "CONTROLES", UiBuilder.Swatch.Ouro,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                                        new Vector2(1040f, 660f), titleSize: 56);

        float y = 150f;
        foreach (var (key, description) in new[]
        {
            ("WASD", "mover"),
            ("ESPACO", "pular, e de novo no ar"),
            ("ESPACO", "na parede, salto de parede"),
            ("Q", "voltar no tempo"),
            ("ESC ou P", "pausar"),
        })
        {
            UiBuilder.CreateKeyRow(card, key, description, y, 1040f);
            y -= 82f;
        }

        back = UiBuilder.CreateButton(holder, "VOLTAR", new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -400f), new Vector2(420f, 96f),
                                      UiBuilder.Swatch.Ardosia);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        return holder.gameObject;
    }

    static GameObject CreateSettingsPanel(Transform root, MainMenu menu, out Button back,
                                          out Text timerLabel, out Text speedLabel,
                                          out Image timerPill, out Image speedPill)
    {
        var holder = UiBuilder.CreateStretched(root, "TelaDeOpcoes");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.035f, 0.95f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "OPCOES", UiBuilder.Swatch.Terracota,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                                        new Vector2(1020f, 580f), titleSize: 56);

        var volume = UiBuilder.CreateSliderRow(card, "VOLUME", new Vector2(0f, 120f),
                                               new Vector2(940f, 96f), out var volumeLabel);

        var timerButton = UiBuilder.CreateToggleRow(card, "CRONOMETRO", new Vector2(0f, 10f),
                                                    new Vector2(940f, 96f),
                                                    out timerLabel, out timerPill);
        var speedButton = UiBuilder.CreateToggleRow(card, "VELOCIDADE", new Vector2(0f, -100f),
                                                    new Vector2(940f, 96f),
                                                    out speedLabel, out speedPill);

        UiBuilder.Bind(timerButton, menu, nameof(MainMenu.ToggleTimer));
        UiBuilder.Bind(speedButton, menu, nameof(MainMenu.ToggleSpeed));
        UiBuilder.BindFloat(volume, menu, nameof(MainMenu.OnVolumeChanged));

        EditorSetupUtil.SetReference(menu, "volumeSlider", volume);
        EditorSetupUtil.SetReference(menu, "volumeLabel", volumeLabel);

        back = UiBuilder.CreateButton(holder, "VOLTAR", new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -390f), new Vector2(420f, 96f),
                                      UiBuilder.Swatch.Ardosia);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        return holder.gameObject;
    }

    static void CreateLighting()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(38f, 150f, 0f));

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.9f;
        light.color = new Color(1f, 0.88f, 0.76f);
        light.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.2f, 0.24f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.24f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.68f, 0.28f, 0.12f);
    }

    static void Place(Transform parent, string color, string piece, Vector3 position,
                      float rotationY = 0f) =>
        KayKitPlacer.Place(parent, color, piece, position, rotationY);

    /// <summary>
    /// Põe o menu como primeira cena do build. Sem isso o jogo compilado abre
    /// direto na fase e o menu nunca aparece.
    /// </summary>
    static void RegisterScenes()
    {
        // A ordem aqui é a ordem do build, e o menu tem de ser o índice 0.
        // A Terra de Lava saiu: é protótipo de antes da revisão de movimento e
        // nada no jogo leva até ela - cena inalcançável dentro do build é peso
        // morto no zip e confunde quem abrir o projeto.
        var wanted = new[] { kScenePath,
                            "Assets/Scenes/Level_CorridaDeLava.unity",
                            "Assets/Scenes/Level_TorreDeBolhas.unity" };
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        foreach (var path in wanted)
        {
            if (System.IO.File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (System.Array.IndexOf(wanted, entry.path) < 0) list.Add(entry);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
