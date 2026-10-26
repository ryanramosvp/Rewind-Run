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
        BuildVolcano(root);

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 0f), 80f, 80f);

        // Desfaz e remonta o cenário em ciclo. Quem dirige é a câmera, que
        // conhece o tempo do rewind.
        root.gameObject.AddComponent<DioramaDissolve>();

        var focus = new GameObject("FocoDaOrbita").transform;
        focus.SetParent(root, false);
        focus.localPosition = new Vector3(0f, 2f, 0f);

        return focus;
    }

    /// <summary>
    /// As peças do vulcão, sem lava nem câmera.
    ///
    /// Pública porque o tutorial acontece neste mesmo cenário. Duas cópias da
    /// geometria sairiam do lugar uma em relação à outra no primeiro ajuste, e
    /// o jogador que acabou de ver o menu reconhece o vulcão de cara.
    /// </summary>
    public static void BuildVolcano(Transform root)
    {
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
        EditorSetupUtil.SetReference(diorama, "dissolve",
            focus.parent != null ? focus.parent.GetComponent<DioramaDissolve>() : null);

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
        // Seis botões de 84 px a cada 94: o grupo cresceu para cima e para
        // baixo em volta do mesmo centro, e o topo do CONTINUAR ainda fica
        // abaixo da base do "RUN".
        var group = UiBuilder.CreateRect(root, "Botoes", new Vector2(0f, 0.5f),
                                         new Vector2(420f, -130f), new Vector2(560f, 580f));

        var size = new Vector2(520f, 84f);
        var center = new Vector2(0.5f, 0.5f);
        var resume = UiBuilder.CreateButton(group, "CONTINUAR", center, new Vector2(0f, 235f), size,
                                            UiBuilder.Swatch.Laranja);
        var levels = UiBuilder.CreateButton(group, "SELECIONAR FASE", center, new Vector2(0f, 141f), size,
                                            UiBuilder.Swatch.Brasa);
        var newGame = UiBuilder.CreateButton(group, "NOVO JOGO", center, new Vector2(0f, 47f), size,
                                             UiBuilder.Swatch.Verde);
        var controls = UiBuilder.CreateButton(group, "CONTROLES", center, new Vector2(0f, -47f), size,
                                              UiBuilder.Swatch.Ouro);
        var settings = UiBuilder.CreateButton(group, "OPÇÕES", center, new Vector2(0f, -141f), size,
                                              UiBuilder.Swatch.Terracota);
        var quit = UiBuilder.CreateButton(group, "SAIR", center, new Vector2(0f, -235f), size,
                                          UiBuilder.Swatch.Ardosia);

        // O nome da fase que o Continuar abre, pequeno, embaixo do rótulo.
        var resumeLabel = (RectTransform)resume.transform.Find("Rotulo");
        resumeLabel.anchoredPosition += new Vector2(0f, 12f);
        resumeLabel.sizeDelta = new Vector2(resumeLabel.sizeDelta.x, 46f);
        var resumeDetail = UiBuilder.CreateText(resume.transform, "Fase", "", 24, UiBuilder.Ash,
                                                new Vector2(0.5f, 0.5f), new Vector2(14f, -24f),
                                                new Vector2(440f, 28f));

        UiBuilder.Bind(resume, menu, nameof(MainMenu.Continue));
        UiBuilder.Bind(levels, menu, nameof(MainMenu.OpenLevelSelect));
        UiBuilder.Bind(newGame, menu, nameof(MainMenu.NewGame));
        UiBuilder.Bind(controls, menu, nameof(MainMenu.OpenControls));
        UiBuilder.Bind(settings, menu, nameof(MainMenu.OpenSettings));
        UiBuilder.Bind(quit, menu, nameof(MainMenu.Quit));

        var tutorial = CreateTutorialPrompt(root, menu, out var tutorialYes);
        var panel = CreateControlsPanel(root, menu, out var back);
        var options = CreateSettingsPanel(root, menu, out var optionsBack,
                                          out var timerLabel, out var speedLabel,
                                          out var timerPill, out var speedPill);
        CreateLevelSelect(root, menu);
        CreateConfirmation(root, menu);

        EditorSetupUtil.SetReference(menu, "continueButton", resume);
        EditorSetupUtil.SetReference(menu, "continueDetail", resumeDetail);
        EditorSetupUtil.SetReference(menu, "newGameButton", newGame);
        EditorSetupUtil.SetReference(menu, "buttonsGroup", group.gameObject);
        EditorSetupUtil.SetReference(menu, "tutorialPanel", tutorial);
        EditorSetupUtil.SetReference(menu, "tutorialYesButton", tutorialYes);
        EditorSetupUtil.SetReference(menu, "controlsPanel", panel);
        EditorSetupUtil.SetReference(menu, "settingsPanel", options);
        EditorSetupUtil.SetReference(menu, "firstButton", newGame);
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

    /// <summary>
    /// A pergunta que o JOGAR faz antes de carregar a fase.
    ///
    /// O tutorial é a opção de cima e a que já vem selecionada: quem aperta
    /// Enter sem ler é justamente quem ainda não conhece os comandos. Quem já
    /// conhece desce uma opção e vai direto para a fase.
    /// </summary>
    static GameObject CreateTutorialPrompt(Transform root, MainMenu menu, out Button yes)
    {
        var holder = UiBuilder.CreateStretched(root, "TelaDoTutorial");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.035f, 0.9f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "PRIMEIRA CORRIDA?", UiBuilder.Swatch.Laranja,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 110f),
                                        new Vector2(1000f, 330f), titleSize: 52);

        UiBuilder.CreateText(card, "Pergunta",
                             "Quer fazer o tutorial? Ele mostra os comandos um por um, " +
                             "no próprio vulcão, e depois segue para a primeira fase.",
                             38, UiBuilder.Ash, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                             new Vector2(900f, 180f));

        yes = UiBuilder.CreateButton(holder, "FAZER O TUTORIAL", new Vector2(0.5f, 0.5f),
                                     new Vector2(0f, -150f), new Vector2(700f, 96f),
                                     UiBuilder.Swatch.Laranja);
        var no = UiBuilder.CreateButton(holder, "IR DIRETO PARA A FASE", new Vector2(0.5f, 0.5f),
                                        new Vector2(0f, -260f), new Vector2(700f, 96f),
                                        UiBuilder.Swatch.Ardosia);

        UiBuilder.CreateText(holder, "Dica", "Esc para voltar", 30, UiBuilder.Ash,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, -360f),
                             new Vector2(700f, 50f));

        UiBuilder.Bind(yes, menu, nameof(MainMenu.PlayTutorial));
        UiBuilder.Bind(no, menu, nameof(MainMenu.SkipTutorial));

        return holder.gameObject;
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
            ("ESPAÇO", "pular, e de novo no ar"),
            ("ESPAÇO", "na parede, salto de parede"),
            ("BOTÃO DIR.", "segurar: voltar no tempo"),
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

        var card = UiBuilder.CreateCard(holder, "Cartao", "OPÇÕES", UiBuilder.Swatch.Terracota,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                                        new Vector2(1020f, 700f), titleSize: 56);

        var volume = UiBuilder.CreateSliderRow(card, "VOLUME", new Vector2(0f, 120f),
                                               new Vector2(940f, 96f), out var volumeLabel);

        var timerButton = UiBuilder.CreateToggleRow(card, "CRONÔMETRO", new Vector2(0f, 10f),
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

        // Separado dos ajustes e em vermelho: é a única coisa do menu que não
        // se desfaz. Abre uma confirmação, não apaga direto.
        var wipe = UiBuilder.CreateButton(card, "APAGAR TODOS OS DADOS", new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, -215f), new Vector2(940f, 96f),
                                          UiBuilder.Swatch.Vermelho);
        UiBuilder.Bind(wipe, menu, nameof(MainMenu.AskDeleteAll));

        back = UiBuilder.CreateButton(holder, "VOLTAR", new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -390f), new Vector2(420f, 96f),
                                      UiBuilder.Swatch.Ardosia);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        return holder.gameObject;
    }

    /// <summary>
    /// Seleção de fases: uma linha por fase da <see cref="Campaign"/>, com o
    /// nome à esquerda e o recorde à direita. O texto de cada linha é escrito
    /// pelo MainMenu ao abrir a tela, a partir do save - aqui só a estrutura.
    /// </summary>
    static void CreateLevelSelect(Transform root, MainMenu menu)
    {
        var holder = UiBuilder.CreateStretched(root, "TelaDeFases");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.035f, 0.95f));

        int count = Campaign.Levels.Length;
        const float rowHeight = 100f, step = 115f;
        float cardHeight = 86f + 40f + count * step + 20f;

        var card = UiBuilder.CreateCard(holder, "Cartao", "SELECIONAR FASE", UiBuilder.Swatch.Brasa,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                                        new Vector2(1100f, cardHeight), titleSize: 56);

        var buttons = new Button[count];
        var titles = new Text[count];
        var details = new Text[count];

        float y = cardHeight * 0.5f - 86f - 30f - rowHeight * 0.5f;
        for (int i = 0; i < count; i++, y -= step)
        {
            var button = UiBuilder.CreateButton(card, $"FASE {i + 1}", new Vector2(0.5f, 0.5f),
                                                new Vector2(0f, y), new Vector2(1000f, rowHeight),
                                                UiBuilder.Swatch.Laranja);

            // O rótulo do botão vira o nome, alinhado à esquerda na metade
            // dele; o recorde ocupa a outra metade, alinhado à direita.
            var title = button.transform.Find("Rotulo").GetComponent<Text>();
            title.alignment = TextAnchor.MiddleLeft;
            var titleRect = title.rectTransform;
            titleRect.sizeDelta = new Vector2(470f, titleRect.sizeDelta.y);
            titleRect.anchoredPosition = new Vector2(40f + 235f, 0f);

            details[i] = UiBuilder.CreateText(button.transform, "Recorde", "", 32, UiBuilder.Ash,
                                              new Vector2(1f, 0.5f), new Vector2(-24f - 225f, 0f),
                                              new Vector2(450f, rowHeight - 18f), TextAnchor.MiddleRight);

            UiBuilder.BindInt(button, menu, nameof(MainMenu.PlayLevel), i);
            buttons[i] = button;
            titles[i] = title;
        }

        var back = UiBuilder.CreateButton(holder, "VOLTAR", new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, 60f - cardHeight * 0.5f - 80f), new Vector2(420f, 96f),
                                          UiBuilder.Swatch.Ardosia);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        EditorSetupUtil.SetReference(menu, "levelSelectPanel", holder.gameObject);
        EditorSetupUtil.SetReferences(menu, "levelButtons", buttons);
        EditorSetupUtil.SetReferences(menu, "levelTitles", titles);
        EditorSetupUtil.SetReferences(menu, "levelDetails", details);
        EditorSetupUtil.SetReference(menu, "levelSelectBackButton", back);
    }

    /// <summary>
    /// Confirmação do Novo jogo e do apagar dados. Uma tela só, com título e
    /// texto trocados pelo MainMenu. Cancelar fica em cima e recebe o foco:
    /// quem aperta Enter sem ler não perde nada.
    /// </summary>
    static void CreateConfirmation(Transform root, MainMenu menu)
    {
        var holder = UiBuilder.CreateStretched(root, "TelaDeConfirmacao");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.035f, 0.92f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "TEM CERTEZA?", UiBuilder.Swatch.Vermelho,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 110f),
                                        new Vector2(1000f, 330f), titleSize: 52);

        var title = card.Find("Titulo").GetComponent<Text>();
        var message = UiBuilder.CreateText(card, "Mensagem", "", 38, UiBuilder.Ash, new Vector2(0.5f, 0.5f),
                                           new Vector2(0f, -40f), new Vector2(900f, 180f));

        var cancel = UiBuilder.CreateButton(holder, "CANCELAR", new Vector2(0.5f, 0.5f),
                                            new Vector2(0f, -150f), new Vector2(700f, 96f),
                                            UiBuilder.Swatch.Ardosia);
        var confirm = UiBuilder.CreateButton(holder, "CONFIRMAR", new Vector2(0.5f, 0.5f),
                                             new Vector2(0f, -260f), new Vector2(700f, 96f),
                                             UiBuilder.Swatch.Vermelho);

        UiBuilder.Bind(cancel, menu, nameof(MainMenu.CancelConfirm));
        UiBuilder.Bind(confirm, menu, nameof(MainMenu.Confirm));

        EditorSetupUtil.SetReference(menu, "confirmPanel", holder.gameObject);
        EditorSetupUtil.SetReference(menu, "confirmTitle", title);
        EditorSetupUtil.SetReference(menu, "confirmMessage", message);
        EditorSetupUtil.SetReference(menu, "confirmCancelButton", cancel);
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

        // O mesmo céu das fases: o menu é o mundo do jogo visto de fora.
        LavaLetalArt.ApplyAtmosphere();
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
                            TutorialLevelBuilder.ScenePath,
                            "Assets/Scenes/Level_CorridaDeLava.unity",
                            "Assets/Scenes/Level_TorreDeBolhas.unity",
                            VolcanoLevelBuilder.ScenePath,
                            RampLevelBuilder.ScenePath };
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
