using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static VolcanicKit;

/// <summary>
/// Fase 3: Terra da Lava Letal, inspirada na fase de mesmo espírito do Super
/// Mario 64.
///
/// As duas primeiras fases são caminhos: uma pista e uma torre. Esta é um
/// arquipélago. A lava fica rente às ilhas (Y = 0, contra -10 nas outras) e
/// cobre tudo; o que se vê de cima é um conjunto de ilhas soltas em direções
/// diferentes, e o percurso precisa ser lido no cenário em vez de seguido num
/// corredor. É o que a fase de referência tem de mais marcante, e o que as
/// outras duas não têm.
///
/// Seis trechos: largada, primeira sequência de ilhas, ilha central, corredor
/// de precisão com atalho, grande travessia e chegada. Todo vão foi medido
/// contra o arco real do pulo - ver <see cref="ValidateJumps"/>, que refaz a
/// conta a cada reconstrução e avisa no console se algum salto ficou
/// impossível.
///
/// A lava baixa muda o rewind, e isso foi aceito de propósito. Caindo de 5 m a
/// queda dura uns 0,45 s, não o segundo inteiro das outras fases; o rewind
/// continua útil, mas é usado no ar, quando o salto já parece errado, e não
/// durante a queda. É a fase que cobra reflexo.
/// </summary>
public static class VolcanoLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_LavaLetal.unity";

    // Limites de S, A, B, C e D, em segundos. Estimativa, não medição: a rota
    // tem uns 250 m entre centros de plataforma, parte deles a 15 m/s nas
    // rampas, o que dá ~21 s sem erro; o S fica um pouco acima disso. O save
    // registra as corridas, então recalibrar depois de jogar é ler o melhor
    // tempo dele.
    //
    // Os obstáculos somam espera: prensas, gêiseres e plataformas móveis
    // custam uns 3 s numa corrida limpa, daí o S em 27.
    static readonly float[] kRanks = { 27f, 33f, 42f, 54f, 74f };


    static readonly Vector3 kSpawn = new Vector3(0f, 4.3f, 1.5f);


    // A rota principal em trechos de saltos simples. Os combos de bolha e
    // rampa entre eles são validados à parte, em ValidateCombos.
    static readonly string[][] kMainRoute =
    {
        new[] { "Largada", "Placa1", "Ilhota1" },
        new[] { "Pista", "Central", "Patio", "Segura1", "Segura2", "Segura3", "Juncao",
                "Passarela3", "Pedra1", "Pedra2", "Pedra3", "Torre" },
    };

    static readonly string[] kShortcut = { "Patio", "Atalho1", "Juncao" };

    [MenuItem("Rewind Run/Criar Fase 3: Terra da Lava Letal", priority = 9)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();
        BeginLevel();

        CreateAtmosphere();

        var level = new GameObject("Fase").transform;
        CreateStart(Group(level, "1_Largada"));
        CreateFirstIslands(Group(level, "2_PrimeirasIlhas"));
        CreateCenter(Group(level, "3_IlhaCentral"));
        CreateCorridor(Group(level, "4_Corredor"));
        CreateCrossing(Group(level, "5_Travessia"));
        CreateFinish(Group(level, "6_Chegada"));
        CreateScenery(Group(level, "Cenario"));

        CreateCameraZones(new GameObject("ZonasDeCamera").transform);

        var player = LevelKit.AssembleSystems(kRanks, RampLevelBuilder.SceneName);
        LevelKit.CreateMusic(LevelKit.DarkLevelMusic);
        if (player != null) player.transform.SetPositionAndRotation(kSpawn, Quaternion.identity);

        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera != null)
        {
            LevelKit.SetFloat(camera, "fixedPitch", 34f);
            LevelKit.SetFloat(camera, "fixedDistance", 22f);
        }

        CreateLava();
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        ValidateJumps();
        Debug.Log($"Fase 3 criada em {ScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s.");
    }

    // ------------------------------------------------------------------
    // Trechos
    // ------------------------------------------------------------------

    /// <summary>
    /// Largada: ilha larga, com dois blocos baixos no caminho.
    ///
    /// Os blocos têm 1 m, e ficam na linha da primeira placa. Pular ou
    /// contornar é a primeira decisão da fase, e ela não custa nada - a ilha
    /// tem 15 m de lado e lava nenhuma por perto.
    /// </summary>
    static void CreateStart(Transform root)
    {
        Rock(root, "Largada", 0f, 6f, 4f, 8.5f, 7, 101, LavaLetalArt.RockGray);

        LevelKit.Place(root, "yellow", "platform_2x2x1", new Vector3(-3f, 4f, 9.5f));
        LevelKit.Place(root, "red", "platform_2x2x1", new Vector3(3.5f, 4f, 11.5f), 20f);
    }

    /// <summary>
    /// Primeira sequência, e a primeira bolha com rampa.
    ///
    /// Duas plataformas fáceis, e da ilhota uma bolha leva ao rochedo alto
    /// (10 m). Dele, uma rampa de 34 graus desce 4 m até a pista metálica. É
    /// o combo da fase apresentado num lugar sem risco: a bolha está logo
    /// acima do pulo normal, e a pista é longa o bastante para receber tanto
    /// quem só desliza para fora da rampa (pousa a 3,7 m) quanto quem pula na
    /// beira a 15 m/s (pousa a 15,2 m). Ver <see cref="ValidateJumps"/>.
    /// </summary>
    static void CreateFirstIslands(Transform root)
    {
        // Primeira plataforma móvel: anda 6 m de lado, devagar e com parada
        // longa. Das duas pontas o salto de ida e o de volta são folgados (ver
        // a validação) - é a mecânica apresentada sem cobrar nada, só para o
        // jogador ver que plataforma que anda o carrega junto.
        var placa = Plate(root, "Placa1", 2f, 21f, 4.6f, 5f, 5f, 15f, LavaLetalArt.Metal, trim: true, supports: false);
        MakeMoving(placa, new Vector3(-6f, 0f, 0f), travelSeconds: 2.4f, waitSeconds: 0.9f, phase: 0f);

        Rock(root, "Ilhota1", -5f, 31f, 5.2f, 3.4f, 6, 102, LavaLetalArt.RockBurnt);

        Bubble(root, "Bolha_A", new Vector3(-5f, 8.2f, 38.5f), 0f);
        Rock(root, "Alto_A", -5f, 48.5f, 10f, 3.5f, 7, 103, LavaLetalArt.RockGray);
        // A rampa termina em Z = 51 + RampLength(4) = 58,6, e a pista começa
        // 2,5 m depois.
        Ramp(root, "Rampa_A", new Vector3(-5f, 10f, 51f), 6f, 4f, 0f);

        // Azul, como o bloco do pack que ficava aqui: o contraste mais forte
        // possível com a lava, logo na primeira descida.
        Plate(root, "Pista", -5f, 69.25f, 5f, 8f, 16.5f, 0f, LavaLetalArt.MetalBlue, trim: true, supports: true);
    }

    /// <summary>
    /// Ilha central: o marco da fase.
    ///
    /// Octógono de 20 m com borda escura, topo cinza e um miolo laranja. O
    /// miolo é pedra quente, não lava - tem textura de rocha e brilha pouco -
    /// e é o que torna a ilha reconhecível de qualquer ponto da fase.
    ///
    /// A rota entra pelo sudoeste e sai pelo leste, para o pátio metálico: é a
    /// mudança de direção do trecho. O pátio tem 8 x 8 m, o maior respiro
    /// antes do corredor de precisão.
    /// </summary>
    static void CreateCenter(Transform root)
    {
        var ring = LowPolyMesh.Regular(8, 10f, 22.5f);
        var island = Solid(root, "Central", LowPolyMesh.Prism(Flare(ring), kBedrock, 5f, LavaLetalArt.RockTile,
                                                             1f / kFlare, "Central"),
                           LavaLetalArt.RockSide, LavaLetalArt.RockSide, new Vector3(11f, 0f, 74f), 0f);
        AddMeshCollider(island);
        Register("Central", 5f, World(island.transform, ring));

        // Topo claro e miolo quente: camadas finas sem colisão, 4 cm uma acima
        // da outra para não brigarem no depth buffer. O pé afunda 8 cm no
        // miolo, o que não se percebe.
        Decor(root, "Central_Topo", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 8.7f, 22.5f), 4.98f, 5.04f, LavaLetalArt.RockTile),
              LavaLetalArt.RockGray, new Vector3(11f, 0f, 74f));
        Decor(root, "Central_Miolo", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 3.4f, 0f), 5.02f, 5.08f, LavaLetalArt.RockTile),
              LavaLetalArt.Ember, new Vector3(11f, 0f, 74f));

        // Primeiro gêiser, no miolo quente da ilha: o lugar que já parecia
        // perigoso agora é. A ilha tem 20 m, e o jato 1,8 - dá para contornar
        // sem esperar, ou parar na beira e assistir um ciclo inteiro. É onde
        // se aprende a ler o aviso antes de ele cobrar alguma coisa.
        Geyser(root, "Geiser_Central", new Vector3(11f, 5.08f, 74f), idleSeconds: 3.4f, phase: 0f);

        // Um degrau metálico em cima da ilha, para ela misturar pedra e chapa
        // como a referência. Fica fora da linha entre a entrada e a saída.
        Plate(root, "Podio", 14f, 79.5f, 5.6f, 3f, 3f, 10f, LavaLetalArt.Metal, trim: true, supports: false);

        Plate(root, "Patio", 28f, 81f, 5f, 8f, 8f, 0f, LavaLetalArt.Metal, trim: true, supports: true);

        // Primeira hélice, no pátio largo: dois braços de 2,85 m numa placa de
        // 8, girando devagar. Sobra uma faixa de 1,1 m na beira que a barra
        // nunca alcança, e as quinas ficam livres - dá para atravessar pela
        // borda ou esperar o braço passar. A barra empurra, não mata (é a
        // BladePush da fase 1); o perigo é ser empurrado para fora.
        LevelKit.Blade(root, new Vector3(28f, 5f, 81f), 50f, 0f);
    }

    /// <summary>
    /// Corredor de precisão, com duas rotas do pátio até a junção.
    ///
    /// Segura: três plataformas, vãos de 3 a 4 m, desvio pelo leste. Longa,
    /// mas nenhum salto passa de 40% do alcance.
    ///
    /// Atalho: um bloco de 2 x 2 m e, dele, um vão de 14 m - que o pulo
    /// simples (11 m) não vence e o duplo vence. São 10 m a menos de percurso
    /// e dois saltos no lugar de quatro, cerca de 0,9 s; o preço é pousar num
    /// bloco de 2 m vindo a 11 m/s e acertar o duplo sobre a lava.
    /// </summary>
    static void CreateCorridor(Transform root)
    {
        Rock(root, "Segura1", 36f, 91f, 5.6f, 4f, 5, 104, LavaLetalArt.RockBrown);

        // Plataforma sobre o vão: vai e volta 5 m de lado entre as duas
        // rochas. É grande (4 x 6) e as duas pontas servem para os dois
        // saltos, mas numa delas o salto de saída fica longo - escolher quando
        // pular passa a ser parte da rota segura.
        var meio = Plate(root, "Segura2", 33f, 101f, 6f, 4f, 6f, 10f, LavaLetalArt.MetalBlue, trim: true, supports: false);
        MakeMoving(meio, new Vector3(5f, 0f, 0f), travelSeconds: 2f, waitSeconds: 0.6f, phase: 0.3f);

        Rock(root, "Segura3", 29.5f, 109.5f, 6.2f, 3.4f, 6, 105, LavaLetalArt.RockGray);
        Rock(root, "Juncao", 20f, 111f, 6.4f, 4.6f, 7, 106, LavaLetalArt.RockBurnt);

        // O atalho agora anda: de 89 a 95 m em Z, na direção da junção. Perto,
        // o salto final é um pulo duplo de 11 m; longe, passa de 17 m e não
        // fecha. A rota rápida pede, além do pulo duplo, o momento certo.
        var atalho = Plate(root, "Atalho1", 20f, 89f, 6f, 3f, 3f, 0f, LavaLetalArt.MetalBlue, trim: true, supports: false);
        MakeMoving(atalho, new Vector3(0f, 0f, 6f), travelSeconds: 2.2f, waitSeconds: 0.7f, phase: 0f);

        // Segundo gêiser, na junção, perto da saída para a passarela. A ilha é
        // estreita (4,6 m) e as duas rotas pousam nela; o jato não fecha a
        // saída, mas obriga a escolher de que beira pular, ou a esperar 1,8 s.
        Geyser(root, "Geiser_Juncao", new Vector3(18.2f, 6.4f, 112.2f), idleSeconds: 2.6f, phase: 0.4f);
    }

    /// <summary>
    /// Grande travessia, para oeste.
    ///
    /// Passarela longa, três colunas baixas e a torre. As colunas ficam a
    /// 2,9 m uma da outra, perto o bastante para encadear pulos no ritmo - é
    /// onde o bunny hop rende, para quem sabe dele, sem ser exigido.
    ///
    /// A torre é o único salto obrigatório com pulo duplo na rota principal:
    /// 3 m acima da última coluna e a 9 m dela. O pulo simples sobe 3,4 m mas,
    /// subindo 3, só avança 8,4 m. A conta está em <see cref="ValidateJumps"/>.
    /// </summary>
    static void CreateCrossing(Transform root)
    {
        Plate(root, "Passarela3", 7.5f, 116f, 7f, 12f, 2.2f, 0f, LavaLetalArt.Metal, trim: true, supports: true);

        // Duas prensas na passarela, com ciclos de 3,0 e 3,3 s. Caem em 0,4 s
        // (só aí matam), ficam 0,4 s no chão como muro e sobem devagar. Os
        // ciclos diferentes fazem a janela das duas abertas ao mesmo tempo
        // mudar a cada volta, mas ela sempre volta - quem espera na junção
        // vê o padrão inteiro antes de entrar.
        LevelKit.Crusher(root, 10.5f, 116f, 13f, 7f, 0f, descendSeconds: 0.4f, riseSeconds: 1f,
                         topWait: 1.2f, bottomWait: 0.4f);
        LevelKit.Crusher(root, 4.5f, 116f, 13f, 7f, 0.5f, descendSeconds: 0.4f, riseSeconds: 1.1f,
                         topWait: 1.4f, bottomWait: 0.4f);

        Column(root, "Pedra1", -3.5f, 113.5f, 5f, 107);

        // A coluna do meio sobe e desce 2,5 m. Os dois saltos fecham nas duas
        // alturas, mas embaixo a ida é uma descida e em cima uma subida de
        // 2,5 - e o pulo encadeado do bunny hop pede pousar no ritmo dela.
        var pedra = Column(root, "Pedra2", -8f, 117f, 5f, 108, bottom: -4.5f);
        MakeMoving(pedra, new Vector3(0f, 2.5f, 0f), travelSeconds: 1.6f, waitSeconds: 0.6f, phase: 0f);

        Column(root, "Pedra3", -12.5f, 113.5f, 5f, 109);

        Rock(root, "Torre", -24f, 107f, 8f, 3.6f, 6, 110, LavaLetalArt.RockGray);

        // Hélice de um braço na torre, girando ao contrário da do pátio. A
        // torre é onde se pousa do pulo duplo e de onde se sai para a primeira
        // bolha: com um braço só, cada ponto da ilha fica livre cinco de cada
        // seis segundos, e o jogador escolhe pousar no lado vazio.
        LevelKit.Blade(root, new Vector3(-24f, 8f, 107f), -60f, 0.25f, arms: 1);
    }

    /// <summary>
    /// Final: subida de bolhas, a grande rampa e o salto até a chegada.
    ///
    /// Da torre, duas bolhas sobem até o mirante, a 15 m. Dele, uma rampa de
    /// 13,5 m desce 8 m e termina sobre a lava. A bruxa sai dela a 15 m/s, e no
    /// meio do vão há uma terceira bolha: tocá-la renova a subida e o pulo
    /// duplo, e é o que leva até a ilha da chegada.
    ///
    /// O vão foi medido para pedir o combo. Só o deslize, sem bolha nem pulo
    /// duplo, pousa a 15,9 m e a ilha começa a 16,5 m. Com a bolha, a bruxa
    /// pousa entre 22 e 28 m, conforme a velocidade e onde pulou. Quem perde a
    /// bolha ainda tem o pulo duplo (pousa a 25 m) - errar o combo custa
    /// precisão, não a corrida inteira.
    ///
    /// O farol é o que faz a chegada ser vista de longe. A bandeira do pack
    /// tem 1,5 m e some na névoa a 60 m; no alto de duas colunas e com uma luz
    /// amarela em cima, ela aparece desde o corredor.
    /// </summary>
    static void CreateFinish(Transform root)
    {
        Bubble(root, "Bolha_B1", new Vector3(-24f, 11.3f, 114.7f), 0f);
        Bubble(root, "Bolha_B2", new Vector3(-24f, 15.3f, 121.2f), 0.5f);
        Rock(root, "Mirante", -24f, 132.5f, 15f, 5f, 8, 111, LavaLetalArt.RockGray);

        // A rampa termina em Z = 135,5 + RampLength(8) = 149; a bolha fica
        // 7,5 m depois e a ilha da chegada começa 16,5 m depois.
        //
        // A bolha está na altura do topo do pulo, e não no meio do caminho.
        // Ninguém pula exatamente na beira: num teste com entrada simulada a
        // bruxa saltou 0,7 m antes, mais alto, e passou por cima da bolha que
        // ficava 1,75 m abaixo desta. Aqui ela pega qualquer pulo dos últimos
        // 3 m da rampa, de 12 a 15 m/s.
        Ramp(root, "Rampa_B", new Vector3(-24f, 15f, 135.5f), 6f, 8f, 0f);
        Bubble(root, "Bolha_B3", new Vector3(-24f, 11.25f, 156.5f), 0.25f);

        Rock(root, "Chegada", -24f, 172.5f, 7f, 8f, 8, 112, LavaLetalArt.RockGray);

        LevelKit.Place(root, "green", "flag_A", new Vector3(-24f, 7f, 175.5f));
        LevelKit.Place(root, "neutral", "signage_finish", new Vector3(-24f, 7f, 168f));

        LevelKit.Place(root, "neutral", "pillar_2x2x8", new Vector3(-29f, 7f, 176.5f));
        LevelKit.Place(root, "neutral", "pillar_2x2x8", new Vector3(-29f, 15f, 176.5f));
        LevelKit.Place(root, "green", "flag_A", new Vector3(-29f, 23f, 176.5f));
        Decor(root, "Farol", LowPolyMesh.Prism(LowPolyMesh.Regular(4, 0.9f, 45f), 23f, 24.4f, 1f),
              LavaLetalArt.GlowYellow, new Vector3(-29f, 0f, 176.5f));

        // Cobre de 167 a 178 m: o pouso depois da última bolha cai entre 171 e
        // 177, conforme a velocidade, então a chegada já conta no pouso.
        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(-24f, 8.5f, 172.5f);

        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(8f, 3f, 11f);
        area.isTrigger = true;

        go.AddComponent<LevelFinish>();
    }

    /// <summary>
    /// Cenário fora do percurso: ilhas soltas, pináculos, blocos suspensos e
    /// estruturas ao longe.
    ///
    /// As ilhas soltas têm colisão - são pedra saindo da lava, e atravessá-las
    /// pareceria defeito - mas ficam a mais de 10 m de qualquer plataforma da
    /// rota, para não virarem caminho por acidente. O resto (blocos no ar,
    /// torres e vulcão distantes) é só imagem e não tem colisão nenhuma.
    ///
    /// Os blocos suspensos ficam a mais de 25 m da rota em X. A câmera anda
    /// 13 m acima e 18 m atrás da bruxa; um bloco no ar perto da rota
    /// entraria na frente dela.
    /// </summary>
    static void CreateScenery(Transform root)
    {
        Rock(root, "Solta_A", -26f, 22f, 3.5f, 5f, 6, 201, LavaLetalArt.RockBrown, route: false);
        Rock(root, "Solta_B", 25f, 36f, 3f, 6f, 7, 202, LavaLetalArt.RockBurnt, route: false);
        Rock(root, "Solta_C", -31f, 72f, 4.5f, 4f, 5, 203, LavaLetalArt.RockGray, route: false);
        Rock(root, "Solta_D", 40f, 58f, 3f, 4.5f, 6, 204, LavaLetalArt.RockBrown, route: false);
        Rock(root, "Solta_E", -13f, 89f, 3.5f, 3.5f, 6, 205, LavaLetalArt.RockBurnt, route: false);
        Rock(root, "Solta_F", 4f, 96f, 3f, 3f, 5, 206, LavaLetalArt.RockGray, route: false);

        Rock(root, "Pinaculo_A", -16f, 16f, 11f, 1.6f, 5, 211, LavaLetalArt.RockSide, route: false);
        Rock(root, "Pinaculo_B", 18f, 50f, 9f, 1.4f, 5, 212, LavaLetalArt.RockSide, route: false);
        // Longe da travessia: ali a câmera vira para oeste e fica do lado
        // leste da bruxa, e um pináculo mais perto entrava no quadro.
        Rock(root, "Pinaculo_C", 44f, 134f, 12f, 2f, 6, 213, LavaLetalArt.RockSide, route: false);
        Rock(root, "Pinaculo_D", -36f, 100f, 10f, 1.8f, 5, 214, LavaLetalArt.RockSide, route: false);

        FloatingBlock(root, "Suspenso_A", new Vector3(-34f, 13f, 46f), new Vector3(5f, 5f, 5f), 20f);
        FloatingBlock(root, "Suspenso_B", new Vector3(48f, 15f, 84f), new Vector3(6f, 4f, 6f), -15f);
        // Longe da subida de bolhas, que passa na mesma altura em X = -24.
        FloatingBlock(root, "Suspenso_C", new Vector3(-52f, 16f, 112f), new Vector3(4f, 6f, 4f), 35f);

        DarkTower(root, "Torre_Escura_A", new Vector3(-58f, 0f, 145f), new Vector3(12f, 28f, 10f), 15f, 221);
        DarkTower(root, "Torre_Escura_B", new Vector3(88f, 0f, 64f), new Vector3(10f, 18f, 16f), -10f, 222);

        // O vulcão no fundo, atrás da chegada: dá profundidade e diz de onde
        // vem tanta lava. Topo estreitado e com a cratera acesa.
        var volcano = Solid(root, "Vulcao", LowPolyMesh.Prism(LowPolyMesh.Regular(9, 34f, 10f), kBedrock, 34f,
                                                           LavaLetalArt.RockTile * 3f, 0.24f, "Vulcao"),
                            LavaLetalArt.Ember, LavaLetalArt.RockSide, new Vector3(30f, 0f, 190f), 0f);
        volcano.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Zonas de câmera. Altas (34 a 38 graus), como na Torre de Bolhas: o
    /// teste com jogadores mostrou que câmera baixa esconde o tamanho do vão.
    ///
    /// A travessia vira a câmera para -60 graus. Ali o caminho corre para
    /// oeste; com a câmera para +Z ele passaria de lado na tela, e o próximo
    /// salto ficaria na borda do quadro.
    ///
    /// Na torre ela volta para o norte, mais baixa (30 graus) e mais longe:
    /// a subida de bolhas é vertical, e de cima a altura achata.
    /// A zona começa em X = -20,5, depois da última coluna, para a virada
    /// acontecer no pouso da torre e não no meio do pulo duplo até ela.
    /// </summary>
    static void CreateCameraZones(Transform root)
    {
        KayKitPlacer.CreateCameraZone(root, "Zona_Largada", new Bounds(new Vector3(0f, 8f, 37f), new Vector3(60f, 34f, 84f)),
                                      0f, 34f, 22f);
        KayKitPlacer.CreateCameraZone(root, "Zona_Centro", new Bounds(new Vector3(24f, 6f, 87f), new Vector3(44f, 30f, 50f)),
                                      0f, 36f, 24f);
        KayKitPlacer.CreateCameraZone(root, "Zona_Travessia", new Bounds(new Vector3(-2f, 8f, 116f), new Vector3(37f, 30f, 24f)),
                                      -60f, 38f, 23f);
        KayKitPlacer.CreateCameraZone(root, "Zona_Subida", new Bounds(new Vector3(-33f, 14f, 115f), new Vector3(25f, 44f, 32f)),
                                      0f, 30f, 25f);

        // Do mirante em diante a câmera sobe para 45 graus. A 30, a rampa
        // descia logo depois da beira e ficava escondida atrás do próprio
        // mirante: quem chegava lá em cima via a bolha e a chegada, mas não o
        // caminho até elas. A zona começa no meio do mirante, então a virada
        // acontece já com os pés no chão.
        KayKitPlacer.CreateCameraZone(root, "Zona_Rampa", new Bounds(new Vector3(-33f, 14f, 158f), new Vector3(25f, 44f, 54f)),
                                      0f, 45f, 22f);
    }

    // ------------------------------------------------------------------
    // Ambiente
    // ------------------------------------------------------------------

    /// <summary>Luz quente e baixa, com o céu e a névoa compartilhados pelas fases.</summary>
    static void CreateAtmosphere()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 30f, 0f), Quaternion.Euler(38f, -35f, 0f));

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.74f, 0.55f);
        light.shadows = LightShadows.Soft;

        LavaLetalArt.ApplyAtmosphere();
    }

    /// <summary>
    /// A lava: a laje do <see cref="HazardSetup"/>, só que rente às ilhas
    /// (Y = 0) e bem maior, para cobrir o arquipélago e o horizonte.
    ///
    /// Reaproveitar a laje é o que mantém a morte igual às outras fases - é o
    /// mesmo KillZone e a mesma rede de segurança embaixo.
    /// </summary>
    static void CreateLava() =>
        HazardSetup.EnsureLava(new Vector3(5f, 0f, 75f), 230f, 260f, surfaceHeight: 0f);

    // ------------------------------------------------------------------
    // Validação dos saltos
    // ------------------------------------------------------------------

    /// <summary>
    /// Refaz, a cada reconstrução, a conta de cada salto da rota (ver
    /// <see cref="VolcanicKit.JumpReport"/>).
    /// </summary>
    static void ValidateJumps()
    {
        var report = JumpReport.Begin("Fase 3");
        if (report == null) return;

        for (int i = 0; i < kMainRoute.Length; i++)
            report.Route($"Rota principal, trecho {i + 1}", kMainRoute[i]);
        report.Route("Atalho", kShortcut);

        ValidateCombos(report);
        report.Finish();
    }

    /// <summary>
    /// Os combos de bolha e rampa, simulados quadro a quadro.
    ///
    /// Cada um é testado com as bolhas nos dois extremos do balanço (±0,3 m),
    /// porque o jogador chega nelas em qualquer fase dele. E os que dependem
    /// de velocidade são testados nas duas pontas: 11 m/s (correndo) e 15
    /// (saindo da rampa), além de deslizar para fora sem pular.
    /// </summary>
    static void ValidateCombos(JumpReport report)
    {
        report.Section("Combos de bolha e rampa");
        var jump = report.Jump;


        // Combo A: ilhota -> bolha -> alto.
        foreach (float bob in new[] { -0.3f, 0.3f })
            report.Expect($"Ilhota1 -> Bolha_A -> Alto_A (balanço {bob:+0.0;-0.0})", true,
                   FromIsland(jump, "Ilhota1", "Alto_A", jump.Speed, new[] { "Bolha_A" }, bob));
        report.Expect("Ilhota1 -> Alto_A sem a bolha", false,
               FromIsland(jump, "Ilhota1", "Alto_A", jump.Speed, new string[0], 0f));

        // Rampa A -> pista: os dois jeitos de sair precisam pousar.
        report.Expect("Rampa_A -> Pista deslizando para fora", true,
               FromRamp(jump, "Rampa_A", "Pista", 14f, new string[0], 0f, jumps: false));
        report.Expect("Rampa_A -> Pista pulando na beira a 15 m/s", true,
               FromRamp(jump, "Rampa_A", "Pista", 15f, new string[0], 0f, jumps: true));
        report.Expect("Rampa_A -> Pista pulando 3 m antes da beira a 15 m/s", true,
               FromRamp(jump, "Rampa_A", "Pista", 15f, new string[0], 0f, jumps: true, early: 3f));

        // Combo B: torre -> duas bolhas -> mirante.
        foreach (float bob in new[] { -0.3f, 0.3f })
            report.Expect($"Torre -> Bolha_B1 -> Bolha_B2 -> Mirante (balanço {bob:+0.0;-0.0})", true,
                   FromIsland(jump, "Torre", "Mirante", jump.Speed, new[] { "Bolha_B1", "Bolha_B2" }, bob));

        // Combo C: rampa B -> bolha no vão -> chegada, pulando da beira ou até
        // 3 m antes dela - ninguém pula exatamente na beira.
        foreach (float speed in new[] { 12f, 15f })
        foreach (float early in new[] { 0f, 1.5f, 3f })
        foreach (float bob in new[] { -0.3f, 0.3f })
            report.Expect($"Rampa_B -> Bolha_B3 -> Chegada a {speed:0} m/s, {early:0.0} m antes da beira " +
                   $"(balanço {bob:+0.0;-0.0})", true,
                   FromRamp(jump, "Rampa_B", "Chegada", speed, new[] { "Bolha_B3" }, bob, jumps: true, early: early));
        report.Expect("Rampa_B -> Chegada a 15 m/s sem bolha nem duplo", false,
               FromRamp(jump, "Rampa_B", "Chegada", 15f, new string[0], 0f, jumps: true));
        report.Expect("Rampa_B -> Chegada a 15 m/s sem bolha, com duplo", true,
               FromRamp(jump, "Rampa_B", "Chegada", 15f, new string[0], 0f, jumps: true, doubleAt: 0.5f));

    }

}
