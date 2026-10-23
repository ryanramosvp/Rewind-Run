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
    public static GameObject AssembleSystems(float[] rankSeconds, string nextLevelScene = null)
    {
        var player = AssembleSystemsWithoutSession(nextLevelScene);
        CreateSession(rankSeconds);
        return player;
    }

    /// <summary>
    /// O mesmo conjunto, sem placar nem cronômetro. É o do tutorial: lá não há
    /// tempo a bater, e um relógio correndo enquanto se lê a instrução diria o
    /// contrário.
    /// </summary>
    public static GameObject AssembleSystemsWithoutSession(string nextLevelScene = null)
    {
        var player = CreatePlayer();
        CreateRewindManager();

        var hud = CreateHud(player);
        CreateEndScreens(nextLevelScene);
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

    /// <summary>Cronômetro, barra de velocidade e contador de corrente.</summary>
    public static SpeedrunHud CreateHud(GameObject player)
    {
        var canvas = UiBuilder.CreateCanvas("HUD", sortOrder: 5);
        var root = canvas.transform;
        var hud = canvas.gameObject.AddComponent<SpeedrunHud>();

        // Cronometro no alto, dentro de um bloco de areia: o numero precisa de
        // fundo proprio para ser legivel sobre lava em movimento.
        var timerGroup = UiBuilder.CreateRect(root, "Cronometro", new Vector2(0.5f, 1f),
                                              new Vector2(0f, -76f), new Vector2(440f, 112f));
        UiBuilder.CreateBlock(timerGroup, "Fundo", UiBuilder.Swatch.Ardosia,
                              new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 104f), 0.92f);
        UiBuilder.CreateBlock(timerGroup, "Fio", UiBuilder.Swatch.Laranja,
                              new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(400f, 8f));

        var timer = UiBuilder.CreateText(timerGroup, "Tempo", "00:00,00", 62, UiBuilder.Sand,
                                         new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                                         new Vector2(400f, 90f), style: FontStyle.Bold);

        var speedGroup = UiBuilder.CreateRect(root, "Velocidade", new Vector2(0.5f, 0f),
                                              new Vector2(0f, 128f), new Vector2(460f, 60f));
        UiBuilder.CreateBlock(speedGroup, "Trilho", UiBuilder.Swatch.Ardosia,
                              new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(424f, 26f), 0.9f);

        var fillRect = UiBuilder.CreateRect(speedGroup, "Preenchimento", new Vector2(0.5f, 0.5f),
                                            Vector2.zero, new Vector2(412f, 16f));
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = UiBuilder.Ember;
        fill.raycastTarget = false;

        EditorSetupUtil.SetReference(hud, "timerGroup", timerGroup.gameObject);
        EditorSetupUtil.SetReference(hud, "timerLabel", timer);
        EditorSetupUtil.SetReference(hud, "speedGroup", speedGroup.gameObject);
        EditorSetupUtil.SetReference(hud, "speedFill", fill);

        if (player != null)
            EditorSetupUtil.SetReference(hud, "player", player.GetComponent<PlayerController>());

        CreateRewindBar(root);
        return hud;
    }

    public static void CreateRewindBar(Transform root)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var bar = UiBuilder.CreatePanel(root, "BarraDeRewind", new Color(0f, 0f, 0f, 0.55f),
                                        new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                                        new Vector2(420f, 26f));

        var fillRect = UiBuilder.CreateRect(bar.transform, "Preenchimento", new Vector2(0.5f, 0.5f),
                                            Vector2.zero, new Vector2(412f, 18f));
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = sprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        var group = bar.gameObject.AddComponent<CanvasGroup>();
        var hud = bar.gameObject.AddComponent<RewindHud>();

        EditorSetupUtil.SetReference(hud, "chargeFill", fill);
        EditorSetupUtil.SetReference(hud, "canvasGroup", group);
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
        var canvas = UiBuilder.CreateCanvas("Pausa", sortOrder: 15);
        var pause = canvas.gameObject.AddComponent<PauseMenu>();

        var holder = UiBuilder.CreateStretched(canvas.transform, "Painel");
        UiBuilder.CreateFullScreen(holder, "Fundo", new Color(0.05f, 0.03f, 0.04f, 0.88f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "PAUSA", UiBuilder.Swatch.Ardosia,
                                        new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                                        new Vector2(860f, 600f), titleSize: 56);

        var volume = UiBuilder.CreateSliderRow(card, "VOLUME", new Vector2(0f, 150f),
                                               new Vector2(780f, 92f), out var volumeLabel);

        var resume = UiBuilder.CreateButton(card, "RETOMAR", new Vector2(0.5f, 0.5f),
                                            new Vector2(0f, 36f), new Vector2(700f, 96f),
                                            UiBuilder.Swatch.Laranja);
        var restart = UiBuilder.CreateButton(card, "REINICIAR", new Vector2(0.5f, 0.5f),
                                             new Vector2(0f, -74f), new Vector2(700f, 96f),
                                             UiBuilder.Swatch.Ouro);
        var quit = UiBuilder.CreateButton(card, "MENU", new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, -184f), new Vector2(700f, 96f),
                                          UiBuilder.Swatch.Terracota);

        UiBuilder.CreateText(holder, "Dica", "Esc para voltar a correr", 30, UiBuilder.Ash,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, -350f),
                             new Vector2(700f, 50f));

        UiBuilder.Bind(resume, pause, nameof(PauseMenu.Resume));
        UiBuilder.Bind(restart, pause, nameof(PauseMenu.Restart));
        UiBuilder.Bind(quit, pause, nameof(PauseMenu.ToMenu));

        UiBuilder.BindFloat(volume, pause, nameof(PauseMenu.OnVolumeChanged));

        EditorSetupUtil.SetReference(pause, "panel", holder.gameObject);
        EditorSetupUtil.SetReference(pause, "firstButton", resume);
        EditorSetupUtil.SetReference(pause, "volumeSlider", volume);
        EditorSetupUtil.SetReference(pause, "volumeLabel", volumeLabel);
    }

    public static void CreateEndScreens(string nextLevelScene = null)
    {
        var canvas = UiBuilder.CreateCanvas("TelasDeFim", sortOrder: 20);
        var screen = canvas.gameObject.AddComponent<EndScreen>();

        var death = BuildDeath(canvas.transform, out var deathText);
        var victory = BuildVictory(canvas.transform, screen, out var victoryText);

        EditorSetupUtil.SetReference(screen, "deathPanel", death);
        EditorSetupUtil.SetReference(screen, "victoryPanel", victory);
        EditorSetupUtil.SetReference(screen, "deathSummary", deathText);
        EditorSetupUtil.SetReference(screen, "victorySummary", victoryText);

        if (!string.IsNullOrEmpty(nextLevelScene))
            EditorSetupUtil.SetString(screen, "nextLevelScene", nextLevelScene);

        var finish = Object.FindAnyObjectByType<LevelFinish>();
        if (finish != null) EditorSetupUtil.SetReference(finish, "endScreen", screen);

        death.SetActive(false);
        victory.SetActive(false);
    }

    public static GameObject BuildDeath(Transform parent, out Text summary)
    {
        var holder = UiBuilder.CreateStretched(parent, "TelaDeMorte");

        // Tintura translucida: ver onde se morreu ensina mais que um fundo preto.
        UiBuilder.CreateFullScreen(holder, "Tintura", new Color(0.45f, 0.08f, 0.03f, 0.55f));

        var card = UiBuilder.CreateCard(holder, "Cartao", "NÃO FOI DESSA VEZ",
                                        UiBuilder.Swatch.Vermelho, new Vector2(0.5f, 0.5f),
                                        Vector2.zero, new Vector2(920f, 420f), titleSize: 52);

        // Quatro linhas de 38px pedem 220 de altura; os 180 de antes cortavam
        // a ultima agora que o texto trunca em vez de transbordar.
        summary = UiBuilder.CreateText(card, "Resumo", string.Empty, 38, UiBuilder.Ash,
                                       new Vector2(0.5f, 0.5f), new Vector2(0f, -46f),
                                       new Vector2(840f, 240f));

        return holder.gameObject;
    }

    /// <summary>
    /// Tela de vitória, em quatro faixas de cima para baixo: o veredito (rank
    /// e os três números), a escada de ranks, o registro do save e as teclas.
    ///
    /// A escada é horizontal, de S a F, porque é uma régua: lida da esquerda
    /// para a direita, o degrau aceso diz onde se está e os vizinhos dizem
    /// quanto falta e quanto sobrou, sem precisar ler uma tabela.
    /// </summary>
    public static GameObject BuildVictory(Transform parent, EndScreen screen, out Text summary)
    {
        var holder = UiBuilder.CreateStretched(parent, "TelaDeVitoria");
        UiBuilder.CreateFullScreen(holder, "Tintura", new Color(0.05f, 0.03f, 0.07f, 0.9f));

        // 1500 x 920 numa referência de 1920 x 1080: sobra 80 px em cima e
        // embaixo, e a escada de seis degraus cabe sem espremer os tempos.
        var card = UiBuilder.CreateCard(holder, "Cartao", "CHEGOU", UiBuilder.Swatch.Ouro,
                                        new Vector2(0.5f, 0.5f), Vector2.zero,
                                        new Vector2(1500f, 920f), titleSize: 56);

        // --- veredito ---------------------------------------------------
        UiBuilder.CreatePanel(card, "FundoDoRank", new Color(0.16f, 0.18f, 0.19f, 0.95f),
                              new Vector2(0.5f, 0.5f), new Vector2(-545f, 205f), new Vector2(300f, 280f));
        UiBuilder.CreateText(card, "RotuloDoRank", "RANK", 30, UiBuilder.Ash,
                             new Vector2(0.5f, 0.5f), new Vector2(-545f, 315f), new Vector2(260f, 40f),
                             style: FontStyle.Bold);
        var rank = UiBuilder.CreateText(card, "Rank", "-", 200, UiBuilder.Gold,
                                        new Vector2(0.5f, 0.5f), new Vector2(-545f, 185f),
                                        new Vector2(280f, 230f), style: FontStyle.Bold);

        // Três blocos lado a lado, para comparar de relance.
        var timeValue = UiBuilder.CreateStat(card, "Tempo", "TEMPO", UiBuilder.Swatch.Areia,
                                             new Vector2(-185f, 255f), new Vector2(320f, 150f));
        var fallsValue = UiBuilder.CreateStat(card, "Mortes", "MORTES", UiBuilder.Swatch.Terracota,
                                              new Vector2(155f, 255f), new Vector2(320f, 150f));
        var bestValue = UiBuilder.CreateStat(card, "Recorde", "RECORDE ANTERIOR", UiBuilder.Swatch.Ouro,
                                             new Vector2(495f, 255f), new Vector2(320f, 150f));

        // Duas linhas de altura: a segunda só aparece quando o recorde cai.
        var nextRank = UiBuilder.CreateText(card, "ProximoRank", string.Empty, 34, UiBuilder.Sand,
                                            new Vector2(0.5f, 0.5f), new Vector2(155f, 110f),
                                            new Vector2(1000f, 112f));

        // --- escada -----------------------------------------------------
        const float stepWidth = 214f;
        const float stepGap = 24f;
        float firstX = -(stepWidth + stepGap) * 2.5f;

        var panels = new Image[6];
        var letters = new Text[6];
        var limits = new Text[6];
        var deltas = new Text[6];

        for (int i = 0; i < 6; i++)
        {
            var step = (Rank)i;
            float x = firstX + i * (stepWidth + stepGap);

            panels[i] = UiBuilder.CreatePanel(card, $"Degrau_{step}", new Color(0.16f, 0.18f, 0.19f, 0.95f),
                                              new Vector2(0.5f, 0.5f), new Vector2(x, -55f),
                                              new Vector2(stepWidth, 200f));

            letters[i] = UiBuilder.CreateText(card, $"Letra_{step}", step.ToString(), 70, Ranks.ColorOf(step),
                                 new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                                 new Vector2(stepWidth - 20f, 86f), style: FontStyle.Bold);

            limits[i] = UiBuilder.CreateText(card, $"Limite_{step}", string.Empty, 26, UiBuilder.Ash,
                                             new Vector2(0.5f, 0.5f), new Vector2(x, -78f),
                                             new Vector2(stepWidth - 16f, 40f));

            deltas[i] = UiBuilder.CreateText(card, $"Diferenca_{step}", string.Empty, 34, UiBuilder.Ash,
                                             new Vector2(0.5f, 0.5f), new Vector2(x, -122f),
                                             new Vector2(stepWidth - 16f, 46f), style: FontStyle.Bold);
        }

        // --- registro ---------------------------------------------------
        UiBuilder.CreateText(card, "RotuloDoRegistro", "SEU REGISTRO NESTA FASE", 28, UiBuilder.Gold,
                             new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(1380f, 40f),
                             style: FontStyle.Bold);
        var record = UiBuilder.CreateText(card, "Registro", string.Empty, 30, UiBuilder.Ash,
                                          new Vector2(0.5f, 0.5f), new Vector2(0f, -268f),
                                          new Vector2(1380f, 90f));

        // --- teclas -----------------------------------------------------
        summary = UiBuilder.CreateText(card, "Dicas", string.Empty, 30, UiBuilder.Ash,
                                       new Vector2(0.5f, 0f), new Vector2(0f, 56f),
                                       new Vector2(1380f, 60f));

        EditorSetupUtil.SetReference(screen, "rankLabel", rank);
        EditorSetupUtil.SetReference(screen, "timeValue", timeValue);
        EditorSetupUtil.SetReference(screen, "fallsValue", fallsValue);
        EditorSetupUtil.SetReference(screen, "bestValue", bestValue);
        EditorSetupUtil.SetReference(screen, "nextRankLabel", nextRank);
        EditorSetupUtil.SetReference(screen, "recordLabel", record);
        EditorSetupUtil.SetReferences(screen, "ladderPanels", panels);
        EditorSetupUtil.SetReferences(screen, "ladderLetters", letters);
        EditorSetupUtil.SetReferences(screen, "ladderLimits", limits);
        EditorSetupUtil.SetReferences(screen, "ladderDeltas", deltas);

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
