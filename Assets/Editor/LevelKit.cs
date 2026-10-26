using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Peças comuns a todas as fases: jogador, câmera, luz, HUD, telas de fim,
/// sessão, e os perigos que mais de uma fase usa.
///
/// Existe porque a segunda fase precisava das mesmas duzentas linhas de
/// montagem da primeira. Copiá-las significaria que todo ajuste na HUD ou na
/// tela de vitória teria que ser feito duas vezes, e a segunda seria esquecida.
/// </summary>
public static class LevelKit
{
    public static GameObject Place(Transform parent, string color, string piece,
                                   Vector3 position, float rotationY = 0f) =>
        KayKitPlacer.Place(parent, color, piece, position, rotationY);

    /// <summary>Raio padrão da bolha. A malha do pack tem 1 m, então vale como escala.</summary>
    public const float BubbleRadius = 1.4f;

    /// <summary>
    /// Uma bolha flutuante: esfera, gatilho um pouco maior, e o balanço defasado.
    ///
    /// O gatilho tem 20 cm a mais que a esfera visível. A folga é generosidade
    /// deliberada: encostar de raspão na borda já conta, porque errar uma bolha
    /// num trecho sem chão custa a tentativa inteira.
    ///
    /// A malha perde os colliders que o importador põe nela. A bolha precisa
    /// ser atravessável - quem quica passa por dentro dela, não pousa em cima.
    /// </summary>
    public static GameObject Bubble(Transform root, Vector3 position, float bobPhase)
    {
        var go = new GameObject($"Bolha_{position.z:0}_{position.y:0}");
        go.transform.SetParent(root, false);
        go.transform.localPosition = position;

        var visual = Place(go.transform, "blue", "ball", Vector3.zero);
        DisableColliders(visual);

        // A AirBubble refaz isto no Awake, mas só em play mode. Aplicar aqui
        // também é o que faz a cena já parecer certa na janela do editor.
        if (visual != null) visual.transform.localScale = Vector3.one * BubbleRadius;

        // O raio real quem define é a própria AirBubble, a partir do campo
        // dela - aqui só existe para o RequireComponent ficar satisfeito.
        var area = go.AddComponent<SphereCollider>();
        area.isTrigger = true;

        var bubble = go.AddComponent<AirBubble>();
        EditorSetupUtil.SetReference(bubble, "visual", visual);
        SetFloat(bubble, "bobPhase", bobPhase);

        return go;
    }

    /// <summary>Plataforma que vai e volta, já com a peça e o componente.</summary>
    public static GameObject Platform(Transform root, string color, string piece,
                                      Vector3 position, Vector3 offset,
                                      float travelSeconds = 3f, float waitSeconds = 0.8f,
                                      float phase = 0f)
    {
        var go = new GameObject($"PlataformaMovel_{position.z:0}");
        go.transform.SetParent(root, false);
        go.transform.localPosition = position;

        Place(go.transform, color, piece, Vector3.zero);

        var move = go.AddComponent<MovingPlatform>();
        SetVector(move, "offset", offset);
        SetFloat(move, "travelSeconds", travelSeconds);
        SetFloat(move, "waitSeconds", waitSeconds);
        SetFloat(move, "phase", phase);

        return go;
    }

    /// <summary>
    /// Objeto de som da cena.
    ///
    /// Vai em toda cena que tenha interface - inclusive o menu, que não usa o
    /// AssembleSystems. Sem ele os botões continuam funcionando, só calados, o
    /// que é o tipo de falha que não aparece em teste nenhum e só se nota no
    /// vídeo de entrega.
    /// </summary>
    public static void CreateAudio()
    {
        var go = new GameObject("Audio");

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D: som de menu não tem lugar no mundo

        var sounds = go.AddComponent<UiSounds>();
        EditorSetupUtil.SetReference(sounds, "source", source);
        EditorSetupUtil.SetReference(sounds, "select", Clip("ui_selecao.ogg"));
        EditorSetupUtil.SetReference(sounds, "confirm", Clip("ui_confirmacao.ogg"));
        EditorSetupUtil.SetReference(sounds, "victory", Clip("fanfarra_vitoria.mp3"));
    }

    static AudioClip Clip(string fileName)
    {
        string path = "Assets/Audio/" + fileName;
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null) Debug.LogWarning($"Som não encontrado: {path}.");
        return clip;
    }

    /// <summary>
    /// Monta tudo que não é geometria: jogador, câmera, rewind, sessão, HUD e
    /// telas de fim, já ligados entre si.
    /// </summary>
    /// <param name="rankSeconds">Limites de S, A, B, C e D, em segundos e crescendo.</param>
    /// <param name="nextLevelLabel">Como a tela de vitória chama o que vem depois. Vazio é "próxima fase".</param>
    public static GameObject AssembleSystems(float[] rankSeconds, string nextLevelScene = null,
                                             string nextLevelLabel = null)
    {
        var player = AssembleSystemsWithoutSession(nextLevelScene, nextLevelLabel);
        CreateSession(rankSeconds);
        return player;
    }

    /// <summary>
    /// O mesmo conjunto, sem placar nem cronômetro. É o do tutorial: lá não há
    /// tempo a bater, e um relógio correndo enquanto se lê a instrução diria o
    /// contrário.
    /// </summary>
    public static GameObject AssembleSystemsWithoutSession(string nextLevelScene = null,
                                                           string nextLevelLabel = null)
    {
        var player = CreatePlayer();
        CreateRewindManager();

        var hud = CreateHud(player);
        CreateEndScreens(nextLevelScene, nextLevelLabel);
        CreatePauseMenu();
        CreateAudio();

        if (player != null)
        {
            var camera = CreateCamera(player.transform);
            EditorSetupUtil.SetReference(player.GetComponent<PlayerController>(),
                                         "cameraTransform", camera.transform);
            EditorSetupUtil.SetReference(hud, "player", player.GetComponent<PlayerController>());
        }

        RewindEffectSetup.EnsureVolume();
        RewindEffectSetup.EnablePostProcessingOnCameras();

        return player;
    }

    /// <summary>
    /// Uma prensa: bloco suspenso que cai ate a pista e volta.
    ///
    /// Sao duas coisas no mesmo objeto, e a separacao e o ponto. O bloco e
    /// solido: parado no chao ele e um muro em que se esbarra, nao um perigo.
    /// Dentro dele mora um volume menor que so mata enquanto a prensa desce -
    /// ver <see cref="CrusherHazard"/>.
    ///
    /// O volume letal e recuado 0,35 m nas laterais e ocupa so o metro de
    /// baixo. Fica inteiramente DENTRO da casca solida, entao quem esbarra na
    /// lateral para do lado de fora e nunca chega a toca-lo; so morre quem esta
    /// debaixo quando a face de baixo passa por cima dele.
    /// </summary>
    /// <param name="descendSeconds">Tempo da descida - a parte que mata.</param>
    /// <param name="riseSeconds">Tempo da subida. Zero usa o da descida, como na fase 1.</param>
    /// <param name="topWait">Espera no alto.</param>
    /// <param name="bottomWait">Espera embaixo. Negativo usa a do alto.</param>
    public static GameObject Crusher(Transform root, float x, float z, float topY, float restY,
                                     float phase, float descendSeconds = 0.45f, float riseSeconds = 0f,
                                     float topWait = 1f, float bottomWait = -1f)
    {
        var go = new GameObject($"Prensa_{z:0}_{x:0.#}");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(x, topY, z);

        DisableColliders(Place(go.transform, "red", "platform_4x4x2", Vector3.zero));

        var shell = go.AddComponent<BoxCollider>();
        shell.center = new Vector3(0f, 1f, 0f);
        shell.size = new Vector3(3.9f, 2f, 3.9f);

        AddKinematicBody(go);

        var crush = new GameObject("Esmagamento");
        crush.transform.SetParent(go.transform, false);

        var area = crush.AddComponent<BoxCollider>();
        area.center = new Vector3(0f, 0.5f, 0f);
        area.size = new Vector3(3.2f, 1f, 3.2f);
        area.isTrigger = true;

        crush.AddComponent<CrusherHazard>();

        var move = go.AddComponent<MovingPlatform>();
        SetVector(move, "offset", new Vector3(0f, restY - topY, 0f));
        SetFloat(move, "travelSeconds", descendSeconds);
        SetFloat(move, "waitSeconds", topWait);
        SetFloat(move, "returnSeconds", riseSeconds);
        SetFloat(move, "endWaitSeconds", bottomWait);
        SetFloat(move, "phase", phase);

        return go;
    }

    /// <summary>
    /// Uma helice: poste fixo com uma barra dupla girando em volta.
    ///
    /// O poste fica fora do eixo que gira, senao giraria junto. Ele mantem a
    /// colisao solida de proposito: e um obstaculo honesto no meio do corredor,
    /// nao um perigo, e serve de referencia para ler onde a barra esta.
    /// </summary>
    /// <param name="arms">1 ou 2 braços. Com um só, cada ponto da varredura fica
    /// livre o dobro do tempo - é a versão para ilhas pequenas.</param>
    public static GameObject Blade(Transform root, Vector3 position, float degreesPerSecond,
                                   float phase, int arms = 2)
    {
        var go = new GameObject($"Helice_{position.z:0}");
        go.transform.SetParent(root, false);
        go.transform.localPosition = position;

        Place(go.transform, "neutral", "pillar_1x1x4", Vector3.zero);

        var axle = new GameObject("Eixo");
        axle.transform.SetParent(go.transform, false);

        DisableColliders(Place(axle.transform, "red", "barrier_2x1x4", new Vector3(1.8f, 0f, 0f)));
        if (arms > 1)
            DisableColliders(Place(axle.transform, "red", "barrier_2x1x4", new Vector3(-1.8f, 0f, 0f)));

        // O gatilho cobre só os braços que existem: de 0,45 m (fora do poste)
        // até a ponta, de um lado ou dos dois.
        var area = axle.AddComponent<BoxCollider>();
        area.center = arms > 1 ? new Vector3(0f, 2f, 0f) : new Vector3(1.65f, 2f, 0f);
        area.size = arms > 1 ? new Vector3(5.7f, 4f, 1.1f) : new Vector3(2.4f, 4f, 1.1f);
        area.isTrigger = true;

        AddKinematicBody(axle);
        axle.AddComponent<BladePush>();

        var spin = axle.AddComponent<RotatingHazard>();
        SetVector(spin, "axis", Vector3.up);
        SetFloat(spin, "degreesPerSecond", degreesPerSecond);
        SetFloat(spin, "phase", phase);

        return go;
    }

    /// <summary>
    /// Gatilho que se move precisa de Rigidbody cinematico.
    ///
    /// Um collider sem corpo e tratado como cenario parado; a engine indexa a
    /// malha estatica e nao espera que ela ande. Movido assim, o gatilho pode
    /// atravessar a bruxa sem disparar OnTriggerEnter - o perigo encostaria e
    /// nada aconteceria. O corpo cinematico e o que registra o objeto como algo
    /// que se move por conta propria.
    /// </summary>
    public static void AddKinematicBody(GameObject go)
    {
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    /// <summary>
    /// Desliga, em vez de apagar, os colliders que o importador poe nas pecas.
    ///
    /// Apagar um componente de uma instancia de prefab vira uma sobreposicao
    /// que a cena precisa carregar; desligar da no mesmo para a fisica e nao
    /// mexe na ligacao com o asset.
    /// </summary>
    public static void DisableColliders(GameObject go)
    {
        if (go == null) return;

        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    public static void Zone(Transform root, string name, Vector3 center, Vector3 size,
                     float pitch, float distance)
    {
        KayKitPlacer.CreateCameraZone(root, $"Zona_{name}", new Bounds(center, size),
                                      0f, pitch, distance);
    }

    /// <summary>Placar da fase. Os tempos de rank são de cada fase, não globais.</summary>
    public static void CreateSession(float[] rankSeconds)
    {
        if (rankSeconds == null || rankSeconds.Length != Ranks.Timed)
            Debug.LogError($"A tabela de ranks precisa de {Ranks.Timed} tempos (S, A, B, C, D).");

        for (int i = 1; rankSeconds != null && i < rankSeconds.Length; i++)
        {
            if (rankSeconds[i] <= rankSeconds[i - 1])
                Debug.LogError("Os tempos de rank precisam crescer de S para D.");
        }

        var session = new GameObject("Sessao").AddComponent<GameSession>();
        EditorSetupUtil.SetFloats(session, "rankSeconds", rankSeconds);
    }

    /// <summary>
    /// HUD da fase: placar no canto de cima à esquerda (tempo e mortes),
    /// indicador do rewind no canto de baixo à esquerda e a barra de
    /// velocidade, opcional, embaixo no meio.
    ///
    /// Tudo nos cantos. O meio da tela é onde estão a bruxa, o próximo vão e
    /// o que vem rolando, e a câmera olha morro abaixo - o alto do centro é
    /// justamente onde a pista aparece. Os cantos da esquerda mostram céu ou
    /// lava distante em todas as fases.
    /// </summary>
    public static SpeedrunHud CreateHud(GameObject player)
    {
        var theme = ThemedUi.Theme;
        var canvas = UiBuilder.CreateCanvas("HUD", sortOrder: 5);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var root = canvas.transform;
        var hud = canvas.gameObject.AddComponent<SpeedrunHud>();

        // Placar: as duas placas lado a lado num grupo com layout, para a de
        // mortes encostar no canto quando o cronômetro é desligado nas opções.
        var board = UiBuilder.CreateRect(root, "Placar", new Vector2(0f, 1f), new Vector2(28f, -24f),
                                         new Vector2(520f, 100f));
        board.pivot = new Vector2(0f, 1f);
        var layout = board.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var stone = new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.9f);

        var timerPlate = ThemedUi.Plate(board, "Cronometro", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        Vector2.zero, new Vector2(330f, 100f), stone, out var timerFrame);
        timerFrame.color = theme.metalDark;
        ThemedUi.Text(timerPlate.transform, "Rotulo", "TEMPO", 17f, theme.ash, new Vector2(0f, 1f),
                      new Vector2(165f, -24f), new Vector2(270f, 22f), TextAlignmentOptions.MidlineLeft,
                      spacing: 10f);
        var timer = ThemedUi.Text(timerPlate.transform, "Tempo", "00:00,00", 52f, theme.sand, new Vector2(0f, 0f),
                                  new Vector2(165f, 36f), new Vector2(270f, 60f), TextAlignmentOptions.MidlineLeft,
                                  material: theme.titleMaterial);

        var deathsPlate = ThemedUi.Plate(board, "Mortes", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                         Vector2.zero, new Vector2(150f, 100f), stone, out var deathsFrame);
        deathsFrame.color = theme.metalDark;
        ThemedUi.Text(deathsPlate.transform, "Rotulo", "MORTES", 17f, theme.ash, new Vector2(0.5f, 1f),
                      new Vector2(0f, -24f), new Vector2(120f, 22f), spacing: 6f);
        var deaths = ThemedUi.Text(deathsPlate.transform, "Contagem", "0", 52f, theme.gold, new Vector2(0.5f, 0f),
                                   new Vector2(0f, 36f), new Vector2(120f, 60f), material: theme.titleMaterial);
        var pulse = deaths.gameObject.AddComponent<UiPulse>();
        EditorSetupUtil.SetReference(pulse, "text", deaths);
        EditorSetupUtil.SetColor(pulse, "flashColor", theme.danger);

        var speedGroup = UiBuilder.CreateRect(root, "Velocidade", new Vector2(0.5f, 0f),
                                              new Vector2(0f, 48f), new Vector2(440f, 30f));
        var speedTrack = ThemedUi.Plate(speedGroup, "Trilho", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        Vector2.zero, new Vector2(440f, 30f), stone, out var speedFrame);
        speedFrame.color = theme.metalDark;
        speedTrack.pixelsPerUnitMultiplier = 3f;
        speedFrame.pixelsPerUnitMultiplier = 3f;

        var fillRect = UiBuilder.CreateRect(speedGroup, "Preenchimento", new Vector2(0.5f, 0.5f),
                                            Vector2.zero, new Vector2(416f, 10f));
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = UiBuilder.Ember;
        fill.raycastTarget = false;

        EditorSetupUtil.SetReference(hud, "timerGroup", timerPlate.gameObject);
        EditorSetupUtil.SetReference(hud, "timerLabel", timer);
        EditorSetupUtil.SetReference(hud, "deathsGroup", deathsPlate.gameObject);
        EditorSetupUtil.SetReference(hud, "deathsLabel", deaths);
        EditorSetupUtil.SetReference(hud, "deathsPulse", pulse);
        EditorSetupUtil.SetReference(hud, "speedGroup", speedGroup.gameObject);
        EditorSetupUtil.SetReference(hud, "speedFill", fill);

        if (player != null)
            EditorSetupUtil.SetReference(hud, "player", player.GetComponent<PlayerController>());

        CreateRewindBar(root);
        return hud;
    }

    /// <summary>
    /// Indicador do rewind: ícone, barra de carga fina e o comando escrito.
    /// Pequeno e no canto - fica quase apagado com a carga cheia e só acende
    /// quando há o que dizer (ver <see cref="RewindHud"/>).
    /// </summary>
    public static void CreateRewindBar(Transform root)
    {
        var theme = ThemedUi.Theme;

        var chip = ThemedUi.Plate(root, "Rewind", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f),
                                  new Vector2(330f, 72f), new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.88f),
                                  out var frame);
        frame.color = theme.metalDark;

        // 72 px de altura não comportam os 36 + 36 de borda da moldura.
        chip.pixelsPerUnitMultiplier = 1.4f;
        frame.pixelsPerUnitMultiplier = 1.4f;

        var icon = ThemedUi.Icon(chip.transform, "Icone", theme.rewindIcon, Color.white, new Vector2(0f, 0.5f),
                                 new Vector2(42f, 0f), new Vector2(40f, 40f));

        var hint = ThemedUi.Text(chip.transform, "Comando", "SEGURE  BOTÃO DIREITO", 17f, theme.ash,
                                 new Vector2(0f, 0.5f), new Vector2(76f + 115f, 12f), new Vector2(230f, 22f),
                                 TextAlignmentOptions.MidlineLeft, spacing: 3f);

        var track = UiBuilder.CreateRect(chip.transform, "Trilho", new Vector2(0f, 0.5f), new Vector2(76f + 115f, -13f),
                                         new Vector2(230f, 10f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.6f);
        trackImage.raycastTarget = false;

        var fillRect = UiBuilder.CreateRect(track, "Preenchimento", new Vector2(0.5f, 0.5f), Vector2.zero,
                                            new Vector2(226f, 6f));
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.raycastTarget = false;

        var group = chip.gameObject.AddComponent<CanvasGroup>();
        var hud = chip.gameObject.AddComponent<RewindHud>();

        EditorSetupUtil.SetReference(hud, "chargeFill", fill);
        EditorSetupUtil.SetReference(hud, "canvasGroup", group);
        EditorSetupUtil.SetReference(hud, "icon", icon);
        EditorSetupUtil.SetReference(hud, "hintLabel", hint);
    }

    /// <summary>
    /// Telas de fim.
    ///
    /// A de vitoria e um cartao com tres blocos de numero lado a lado, em vez
    /// de um paragrafo. Numa corrida o jogador compara: tempo contra recorde,
    /// quedas contra a tentativa anterior - e comparar numeros empilhados num
    /// texto corrido exige ler, enquanto em blocos se faz de relance.
    /// </summary>
    /// <summary>
    /// Tela de pausa.
    ///
    /// Fica numa camada abaixo das telas de fim. Elas se recusam a conviver -
    /// a pausa nem abre com uma delas no ar - mas se algum dia escaparem juntas
    /// na tela, a ordem decide qual ganha, e a vitória tem que ganhar.
    /// </summary>
    public static void CreatePauseMenu()
    {
        var theme = ThemedUi.Theme;
        var canvas = UiBuilder.CreateCanvas("Pausa", sortOrder: 15);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var pause = canvas.gameObject.AddComponent<PauseMenu>();

        var holder = UiBuilder.CreateStretched(canvas.transform, "Painel");
        ThemedUi.Backdrop(holder, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.86f));

        var card = ThemedUi.Card(holder, "Cartao", "PAUSA", new Vector2(0f, 30f), new Vector2(900f, 560f), out _);
        var row = new Vector2(800f, 88f);

        var volume = ThemedUi.SliderRow(card, "VOLUME", new Vector2(0f, 120f), row, out var volumeLabel, out _);
        var resume = ThemedUi.MenuButton(card, "RETOMAR", new Vector2(0f, 14f), row, out _, out _);
        var restart = ThemedUi.MenuButton(card, "REINICIAR", new Vector2(0f, -92f), row, out _, out _);
        var quit = ThemedUi.MenuButton(card, "MENU", new Vector2(0f, -198f), row, out _, out _);

        ThemedUi.Text(holder, "Dica", "ESC / START  voltar a correr", 24f,
                      new Color(theme.ash.r, theme.ash.g, theme.ash.b, 0.8f), new Vector2(0.5f, 0.5f),
                      new Vector2(0f, -300f), new Vector2(800f, 34f), bold: false, spacing: 2f);

        UiBuilder.Bind(resume, pause, nameof(PauseMenu.Resume));
        UiBuilder.Bind(restart, pause, nameof(PauseMenu.Restart));
        UiBuilder.Bind(quit, pause, nameof(PauseMenu.ToMenu));
        UiBuilder.BindFloat(volume, pause, nameof(PauseMenu.OnVolumeChanged));

        ThemedUi.Intro(holder, card);

        EditorSetupUtil.SetReference(pause, "panel", holder.gameObject);
        EditorSetupUtil.SetReference(pause, "firstButton", resume);
        EditorSetupUtil.SetReference(pause, "volumeSlider", volume);
        EditorSetupUtil.SetReference(pause, "volumeLabel", volumeLabel);
    }

    public static void CreateEndScreens(string nextLevelScene = null, string nextLevelLabel = null)
    {
        var canvas = UiBuilder.CreateCanvas("TelasDeFim", sortOrder: 20);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var screen = canvas.gameObject.AddComponent<EndScreen>();

        var death = BuildDeath(canvas.transform, out var deathText);
        var victory = BuildVictory(canvas.transform, screen, out var victoryText);

        EditorSetupUtil.SetReference(screen, "deathPanel", death);
        EditorSetupUtil.SetReference(screen, "victoryPanel", victory);
        EditorSetupUtil.SetReference(screen, "deathSummary", deathText);
        EditorSetupUtil.SetReference(screen, "victorySummary", victoryText);

        if (!string.IsNullOrEmpty(nextLevelScene))
            EditorSetupUtil.SetString(screen, "nextLevelScene", nextLevelScene);
        if (!string.IsNullOrEmpty(nextLevelLabel))
            EditorSetupUtil.SetString(screen, "nextLevelLabel", nextLevelLabel);

        var finish = Object.FindAnyObjectByType<LevelFinish>();
        if (finish != null) EditorSetupUtil.SetReference(finish, "endScreen", screen);

        death.SetActive(false);
        victory.SetActive(false);
    }

    public static GameObject BuildDeath(Transform parent, out TMP_Text summary)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(parent, "TelaDeMorte");

        // Tintura translucida: ver onde se morreu ensina mais que um fundo preto.
        ThemedUi.Backdrop(holder, new Color(0.45f, 0.08f, 0.03f, 0.5f));

        var card = ThemedUi.Card(holder, "Cartao", "NÃO FOI DESSA VEZ", Vector2.zero, new Vector2(920f, 400f), out var title);
        title.colorGradient = new VertexGradient(theme.molten, theme.molten, theme.danger, theme.danger);

        var text = ThemedUi.Text(card, "Resumo", string.Empty, 34f, theme.sand, new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -50f), new Vector2(820f, 220f), bold: false);
        text.textWrappingMode = TextWrappingModes.Normal;
        summary = text;

        ThemedUi.Intro(holder, card);
        return holder.gameObject;
    }

    /// <summary>
    /// Tela de vitória: a nota grande à esquerda, tempo, mortes e recorde
    /// anterior à direita, uma linha com o recorde novo e o próximo rank, e as
    /// teclas no pé. Só isso - é a tela de passagem entre uma fase e outra.
    /// </summary>
    public static GameObject BuildVictory(Transform parent, EndScreen screen, out TMP_Text summary)
    {
        var theme = ThemedUi.Theme;
        var holder = UiBuilder.CreateStretched(parent, "TelaDeVitoria");
        ThemedUi.Backdrop(holder, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.86f));

        var card = ThemedUi.Card(holder, "Cartao", "CHEGOU", Vector2.zero, new Vector2(1200f, 600f), out _);

        // --- veredito ---------------------------------------------------
        var rankPlate = ThemedUi.Plate(card, "FundoDaNota", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-410f, 30f), new Vector2(280f, 280f), theme.stone, out var rankFrame);
        rankFrame.color = theme.gold;
        ThemedUi.Text(rankPlate.transform, "Rotulo", "NOTA", 26f, theme.ash, new Vector2(0.5f, 1f),
                      new Vector2(0f, -36f), new Vector2(220f, 34f), spacing: 10f);
        var rank = ThemedUi.Text(rankPlate.transform, "Nota", "-", 180f, theme.gold, new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -22f), new Vector2(240f, 200f), material: theme.titleMaterial);

        // Três blocos lado a lado, para comparar de relance.
        var timeValue = ThemedUi.Stat(card, "Tempo", "TEMPO", new Vector2(-110f, 95f), new Vector2(260f, 150f), theme.sand);
        var fallsValue = ThemedUi.Stat(card, "Mortes", "MORTES", new Vector2(165f, 95f), new Vector2(260f, 150f), theme.sand);
        var bestValue = ThemedUi.Stat(card, "Recorde", "RECORDE ANTERIOR", new Vector2(440f, 95f),
                                      new Vector2(260f, 150f), theme.sand);

        // Até duas linhas: a de cima só aparece quando o recorde cai.
        var nextRank = ThemedUi.Text(card, "ProximoRank", string.Empty, 30f, theme.sand, new Vector2(0.5f, 0.5f),
                                     new Vector2(165f, -50f), new Vector2(820f, 90f), bold: false);
        nextRank.textWrappingMode = TextWrappingModes.Normal;

        // --- teclas -----------------------------------------------------
        summary = ThemedUi.Text(card, "Dicas", string.Empty, 26f, theme.sand, new Vector2(0.5f, 0f),
                                new Vector2(0f, 50f), new Vector2(1100f, 40f), spacing: 2f);

        ThemedUi.Intro(holder, card);

        EditorSetupUtil.SetReference(screen, "rankLabel", rank);
        EditorSetupUtil.SetReference(screen, "timeValue", timeValue);
        EditorSetupUtil.SetReference(screen, "fallsValue", fallsValue);
        EditorSetupUtil.SetReference(screen, "bestValue", bestValue);
        EditorSetupUtil.SetReference(screen, "nextRankLabel", nextRank);

        return holder.gameObject;
    }

    public static void CreateLighting()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 24f, 0f), Quaternion.Euler(44f, 25f, 0f));

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.93f, 0.85f);
        light.shadows = LightShadows.Soft;

        // Céu vermelho e névoa da Terra da Lava Letal, iguais em todas as
        // fases: o mundo é um só, e o céu azul padrão do Unity sobre um mar de
        // lava parecia cenário de outro jogo.
        LavaLetalArt.ApplyAtmosphere();
    }

    public static GameObject CreatePlayer()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchSetup.PrefabPath) ?? WitchSetup.Build();
        if (prefab == null)
        {
            Debug.LogError("Não consegui montar a bruxa. A fase foi criada sem jogador.");
            return null;
        }

        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = new Vector3(0f, 9.2f, 0f);
        return player;
    }

    public static GameObject CreateCamera(Transform target)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.SetPositionAndRotation(new Vector3(0f, 18f, -18f), Quaternion.Euler(26f, 0f, 0f));

        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 62f;
        camera.farClipPlane = 400f;
        go.AddComponent<AudioListener>();

        var orbit = go.AddComponent<OrbitCamera>();
        EditorSetupUtil.SetReference(orbit, "target", target);
        EditorSetupUtil.SetReference(orbit, "input", target.GetComponent<PlayerInputReader>());

        SetFloat(orbit, "fixedPitch", 26f);
        SetFloat(orbit, "fixedDistance", 20f);

        return go;
    }

    public static void CreateRewindManager() => new GameObject("RewindManager").AddComponent<RewindManager>();

    public static void SetVector(Object target, string fieldName, Vector3 value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null) return;

        property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetFloat(Object target, string fieldName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null) return;

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void AddSceneToBuildSettings(string path)
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
