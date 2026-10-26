using TMPro;
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

        var camera = BuildBackdrop();

        CreateInterface();
        LevelKit.CreateAudio();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);
        RegisterScenes();

        Debug.Log($"Menu criado em {kScenePath}. Ele é a primeira cena do build.");
        Selection.activeGameObject = camera;
    }

    /// <summary>
    /// O cenário do menu inteiro: luz, vulcão em miniatura, lava e a câmera que
    /// orbita e rebobina. Público porque a tela de resultados da campanha usa o
    /// mesmo fundo - o jogo começa e termina no mesmo lugar.
    /// </summary>
    public static GameObject BuildBackdrop()
    {
        CreateLighting();
        var focus = CreateDiorama();
        var volume = CreateRewindVolume();
        return CreateCamera(focus, volume);
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

        // Pela altura: o logotipo preso ao topo e os botões ao meio ficam
        // sempre na mesma relação, e numa tela mais larga (21:9) sobra
        // largura em vez de faltar altura. A coluna fica à esquerda e o
        // vulcão, no meio, continua livre.
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;

        var theme = ThemedUi.Theme;

        // Escurece o lado do menu sem tampar o cenário: é o que deixa o texto
        // legível por cima da lava acesa.
        var shade = ThemedUi.Stretched(root, "Sombra", theme.shade, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.88f));
        shade.type = Image.Type.Simple;
        shade.rectTransform.anchorMax = new Vector2(0f, 1f);
        shade.rectTransform.offsetMax = new Vector2(1150f, 0f);

        var logo = ThemedUi.Logo(root, new Vector2(0f, 1f), new Vector2(450f, -200f));

        // Os botoes vivem num grupo proprio: as telas de controles e opcoes
        // escondem o grupo inteiro em vez de abrir por cima dele.
        var group = UiBuilder.CreateRect(root, "Botoes", new Vector2(0f, 0.5f),
                                         new Vector2(420f, -150f), new Vector2(600f, 560f));

        var size = new Vector2(560f, 92f);
        var newGame = ThemedUi.MenuButton(group, "NOVO JOGO", new Vector2(0f, 212f), size,
                                          out var newGameSlot, out _);
        var resume = ThemedUi.MenuButton(group, "CONTINUAR", new Vector2(0f, 106f), size,
                                         out var resumeSlot, out var resumeDetail, withDetail: true);
        var levels = ThemedUi.MenuButton(group, "SELECIONAR FASE", new Vector2(0f, 0f), size,
                                         out var levelsSlot, out _);
        var settings = ThemedUi.MenuButton(group, "CONFIGURAÇÕES", new Vector2(0f, -106f), size,
                                           out var settingsSlot, out _);
        var quit = ThemedUi.MenuButton(group, "SAIR", new Vector2(0f, -212f), size,
                                       out var quitSlot, out _);

        UiBuilder.Bind(resume, menu, nameof(MainMenu.Continue));
        UiBuilder.Bind(levels, menu, nameof(MainMenu.OpenLevelSelect));
        UiBuilder.Bind(newGame, menu, nameof(MainMenu.NewGame));
        UiBuilder.Bind(settings, menu, nameof(MainMenu.OpenSettings));
        UiBuilder.Bind(quit, menu, nameof(MainMenu.Quit));

        // Entrada: o logotipo cai do alto, os botões vêm da esquerda em
        // cascata. Cada um com o seu, no mesmo ritmo.
        var logoIntro = logo.gameObject.AddComponent<UiIntroSequence>();
        EditorSetupUtil.SetReferences(logoIntro, "items", new Object[] { logo });
        SetVector2(logoIntro, "offset", new Vector2(0f, 40f));

        var buttonsIntro = group.gameObject.AddComponent<UiIntroSequence>();
        EditorSetupUtil.SetReferences(buttonsIntro, "items",
                                      new Object[] { newGameSlot, resumeSlot, levelsSlot, settingsSlot, quitSlot });
        LevelKit.SetFloat(buttonsIntro, "startDelay", 0.15f);

        ThemedUi.Text(root, "Dica", "SETAS / ANALÓGICO  navegar      ENTER / A  confirmar      ESC / B  voltar",
                      22f, new Color(theme.ash.r, theme.ash.g, theme.ash.b, 0.75f), new Vector2(0f, 0f),
                      new Vector2(470f, 44f), new Vector2(860f, 30f), TextAlignmentOptions.MidlineLeft,
                      bold: false, spacing: 2f);

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

    static Color Dim(UiTheme theme, float alpha) => new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, alpha);

    /// <summary>
    /// A pergunta que o Novo jogo faz antes de carregar a fase.
    ///
    /// O tutorial é a opção de cima e a que já vem selecionada: quem aperta
    /// Enter sem ler é justamente quem ainda não conhece os comandos. Quem já
    /// conhece desce uma opção e vai direto para a fase.
    /// </summary>
    static GameObject CreateTutorialPrompt(Transform root, MainMenu menu, out Button yes)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(root, "TelaDoTutorial");
        ThemedUi.Backdrop(holder, Dim(theme, 0.9f));

        var card = ThemedUi.Card(holder, "Cartao", "PRIMEIRA CORRIDA?", new Vector2(0f, 130f),
                                 new Vector2(1040f, 330f), out _);
        ThemedUi.Text(card, "Pergunta",
                      "Quer fazer o tutorial? Ele mostra os comandos um por um, no próprio vulcão, " +
                      "e depois segue para a primeira fase.",
                      34f, theme.sand, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(920f, 160f),
                      bold: false).textWrappingMode = TextWrappingModes.Normal;

        yes = ThemedUi.MenuButton(holder, "FAZER O TUTORIAL", new Vector2(0f, -130f), new Vector2(700f, 92f),
                                  out var yesSlot, out _);
        var no = ThemedUi.MenuButton(holder, "IR DIRETO PARA A FASE", new Vector2(0f, -236f),
                                     new Vector2(700f, 92f), out var noSlot, out _);

        Hint(holder, "ESC / B  voltar", -330f);

        UiBuilder.Bind(yes, menu, nameof(MainMenu.PlayTutorial));
        UiBuilder.Bind(no, menu, nameof(MainMenu.SkipTutorial));

        ThemedUi.Intro(holder, card, yesSlot, noSlot);
        return holder.gameObject;
    }

    static GameObject CreateControlsPanel(Transform root, MainMenu menu, out Button back)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(root, "TelaDeControles");
        ThemedUi.Backdrop(holder, Dim(theme, 0.94f));

        var card = ThemedUi.Card(holder, "Cartao", "CONTROLES", new Vector2(0f, 50f), new Vector2(1100f, 640f), out _);

        float y = 150f;
        foreach (var (key, description) in new[]
        {
            ("WASD / ANALÓG.", "mover"),
            ("ESPAÇO / A", "pular, e de novo no ar"),
            ("ESPAÇO / A", "encostada na parede: salto de parede"),
            ("BOTÃO DIR. / LB", "segurar: voltar no tempo"),
            ("ESC / START", "pausar"),
        })
        {
            ThemedUi.KeyRow(card, key, description, y, 1100f);
            y -= 82f;
        }

        back = ThemedUi.MenuButton(holder, "VOLTAR", new Vector2(0f, -370f), new Vector2(420f, 92f),
                                   out var backSlot, out _);
        UiBuilder.Bind(back, menu, nameof(MainMenu.OpenSettings));

        ThemedUi.Intro(holder, card, backSlot);
        return holder.gameObject;
    }

    static GameObject CreateSettingsPanel(Transform root, MainMenu menu, out Button back,
                                          out TMP_Text timerLabel, out TMP_Text speedLabel,
                                          out Image timerPill, out Image speedPill)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(root, "TelaDeOpcoes");
        ThemedUi.Backdrop(holder, Dim(theme, 0.94f));

        var card = ThemedUi.Card(holder, "Cartao", "CONFIGURAÇÕES", new Vector2(0f, 80f), new Vector2(1060f, 700f),
                                 out _);
        var row = new Vector2(960f, 88f);

        var volume = ThemedUi.SliderRow(card, "VOLUME", new Vector2(0f, 190f), row, out var volumeLabel, out _);

        var timerButton = ThemedUi.ToggleRow(card, "CRONÔMETRO", new Vector2(0f, 88f), row,
                                             out var timerState, out timerPill, out _);
        var speedButton = ThemedUi.ToggleRow(card, "VELOCIDADE", new Vector2(0f, -14f), row,
                                             out var speedState, out speedPill, out _);
        timerLabel = timerState;
        speedLabel = speedState;

        UiBuilder.Bind(timerButton, menu, nameof(MainMenu.ToggleTimer));
        UiBuilder.Bind(speedButton, menu, nameof(MainMenu.ToggleSpeed));
        UiBuilder.BindFloat(volume, menu, nameof(MainMenu.OnVolumeChanged));

        EditorSetupUtil.SetReference(menu, "volumeSlider", volume);
        EditorSetupUtil.SetReference(menu, "volumeLabel", volumeLabel);

        // Os controles moram aqui agora: o menu principal ficou só com as
        // cinco ações de campanha e o Sair.
        var controls = ThemedUi.MenuButton(card, "CONTROLES", new Vector2(0f, -116f), row, out _, out _);
        UiBuilder.Bind(controls, menu, nameof(MainMenu.OpenControls));

        // Por último e separado por um fio: é a única coisa do menu que não
        // se desfaz. Abre uma confirmação, não apaga direto.
        var line = UiBuilder.CreateRect(card, "Separador", new Vector2(0.5f, 0.5f), new Vector2(0f, -180f),
                                        new Vector2(900f, 2f)).gameObject.AddComponent<Image>();
        line.color = new Color(theme.metal.r, theme.metal.g, theme.metal.b, 0.5f);
        line.raycastTarget = false;

        var wipe = ThemedUi.MenuButton(card, "APAGAR TODOS OS DADOS", new Vector2(0f, -246f), row, out _, out _);
        UiBuilder.Bind(wipe, menu, nameof(MainMenu.AskDeleteAll));
        var wipeMarker = wipe.transform.Find("Marcador").GetComponent<Image>();
        wipeMarker.color = theme.danger;

        back = ThemedUi.MenuButton(holder, "VOLTAR", new Vector2(0f, -350f), new Vector2(420f, 92f),
                                   out var backSlot, out _);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        ThemedUi.Intro(holder, card, backSlot);
        return holder.gameObject;
    }

    /// <summary>
    /// Seleção de fases: uma linha por fase da <see cref="Campaign"/>, com o
    /// nome à esquerda, o recorde e a nota à direita. O texto de cada linha é
    /// escrito pelo MainMenu ao abrir a tela, a partir do save - aqui só a
    /// estrutura.
    /// </summary>
    static void CreateLevelSelect(Transform root, MainMenu menu)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(root, "TelaDeFases");
        ThemedUi.Backdrop(holder, Dim(theme, 0.94f));

        int count = Campaign.Levels.Length;
        const float rowHeight = 100f, step = 114f;
        float cardHeight = ThemedUi.CardHeader + 20f + count * step + 24f;

        var card = ThemedUi.Card(holder, "Cartao", "SELECIONAR FASE", new Vector2(0f, 70f),
                                 new Vector2(1300f, cardHeight), out _);

        var buttons = new Button[count];
        var titles = new TMP_Text[count];
        var details = new TMP_Text[count];
        var letters = new TMP_Text[count];
        var slots = new RectTransform[count + 2];
        slots[0] = card;

        float y = cardHeight * 0.5f - ThemedUi.CardHeader - 20f - rowHeight * 0.5f;
        for (int i = 0; i < count; i++, y -= step)
        {
            var button = ThemedUi.MenuButton(card, $"FASE {i + 1}", new Vector2(0f, y), new Vector2(1200f, rowHeight),
                                             out slots[i + 1], out _);

            // O rótulo do botão vira o nome, na metade esquerda; o recorde e
            // a letra da nota ficam na direita.
            var title = button.transform.Find("Rotulo").GetComponent<TextMeshProUGUI>();
            var titleRect = title.rectTransform;
            titleRect.sizeDelta = new Vector2(560f, titleRect.sizeDelta.y);
            titleRect.anchoredPosition = new Vector2(62f + 280f, 0f);
            title.fontSizeMax = 36f;

            details[i] = ThemedUi.Text(button.transform, "Recorde", "", 24f, theme.ash, new Vector2(1f, 0.5f),
                                       new Vector2(-120f - 180f, 0f), new Vector2(360f, 40f),
                                       TextAlignmentOptions.MidlineRight, spacing: 2f);
            letters[i] = ThemedUi.Text(button.transform, "Nota", "", 64f, theme.gold, new Vector2(1f, 0.5f),
                                       new Vector2(-62f, 0f), new Vector2(76f, 80f), material: theme.titleMaterial);

            // O recorde acompanha o destaque do botão, como uma segunda linha.
            var view = button.GetComponent<MenuButtonView>();
            EditorSetupUtil.SetReference(view, "detail", details[i]);

            UiBuilder.BindInt(button, menu, nameof(MainMenu.PlayLevel), i);
            buttons[i] = button;
            titles[i] = title;
        }

        var back = ThemedUi.MenuButton(holder, "VOLTAR", new Vector2(0f, 70f - cardHeight * 0.5f - 76f),
                                       new Vector2(420f, 92f), out slots[count + 1], out _);
        UiBuilder.Bind(back, menu, nameof(MainMenu.CloseControls));

        ThemedUi.Intro(holder, slots);

        EditorSetupUtil.SetReference(menu, "levelSelectPanel", holder.gameObject);
        EditorSetupUtil.SetReferences(menu, "levelButtons", buttons);
        EditorSetupUtil.SetReferences(menu, "levelTitles", titles);
        EditorSetupUtil.SetReferences(menu, "levelDetails", details);
        EditorSetupUtil.SetReferences(menu, "levelRanks", letters);
        EditorSetupUtil.SetReference(menu, "levelSelectBackButton", back);
    }

    /// <summary>
    /// Confirmação do Novo jogo e do apagar dados. Uma tela só, com título e
    /// texto trocados pelo MainMenu. Cancelar fica em cima e recebe o foco:
    /// quem aperta Enter sem ler não perde nada.
    /// </summary>
    static void CreateConfirmation(Transform root, MainMenu menu)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(root, "TelaDeConfirmacao");
        ThemedUi.Backdrop(holder, Dim(theme, 0.92f));

        var card = ThemedUi.Card(holder, "Cartao", "TEM CERTEZA?", new Vector2(0f, 130f), new Vector2(1040f, 330f),
                                 out var title);
        var message = ThemedUi.Text(card, "Mensagem", "", 34f, theme.sand, new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, -50f), new Vector2(920f, 160f), bold: false);
        message.textWrappingMode = TextWrappingModes.Normal;

        var cancel = ThemedUi.MenuButton(holder, "CANCELAR", new Vector2(0f, -130f), new Vector2(700f, 92f),
                                         out var cancelSlot, out _);
        var confirm = ThemedUi.MenuButton(holder, "CONFIRMAR", new Vector2(0f, -236f), new Vector2(700f, 92f),
                                          out var confirmSlot, out _);
        confirm.transform.Find("Marcador").GetComponent<Image>().color = theme.danger;

        UiBuilder.Bind(cancel, menu, nameof(MainMenu.CancelConfirm));
        UiBuilder.Bind(confirm, menu, nameof(MainMenu.Confirm));

        ThemedUi.Intro(holder, card, cancelSlot, confirmSlot);

        EditorSetupUtil.SetReference(menu, "confirmPanel", holder.gameObject);
        EditorSetupUtil.SetReference(menu, "confirmTitle", title);
        EditorSetupUtil.SetReference(menu, "confirmMessage", message);
        EditorSetupUtil.SetReference(menu, "confirmCancelButton", cancel);
    }

    /// <summary>Linha de teclas no pé de um painel.</summary>
    static void Hint(Transform parent, string text, float y)
    {
        var theme = ThemedUi.Theme;
        ThemedUi.Text(parent, "Dica", text, 24f, new Color(theme.ash.r, theme.ash.g, theme.ash.b, 0.8f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, 34f), bold: false, spacing: 2f);
    }

    static void SetVector2(Object target, string field, Vector2 value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
                            RampLevelBuilder.ScenePath,
                            ResultsSceneBuilder.ScenePath };
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
