using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    static readonly float[] kRanks = { 24f, 29f, 37f, 48f, 66f };

    /// <summary>Onde as ilhas começam, abaixo da lava: a base nunca aparece.</summary>
    const float kBedrock = -1.5f;

    /// <summary>Fração mínima do raio no sorteio das rochas (ver <see cref="LowPolyMesh.Rock"/>).</summary>
    const float kRockMin = 0.88f;

    static readonly Vector3 kSpawn = new Vector3(0f, 4.3f, 1.5f);

    /// <summary>Contorno de cada plataforma do percurso, em XZ de mundo, para a validação.</summary>
    static readonly Dictionary<string, (float top, Vector2[] poly)> s_Footprints =
        new Dictionary<string, (float, Vector2[])>();

    /// <summary>Centro de cada bolha, para a validação dos combos.</summary>
    static readonly Dictionary<string, Vector3> s_Bubbles = new Dictionary<string, Vector3>();

    /// <summary>Ponta de baixo de cada rampa: posição, altura e direção da descida.</summary>
    static readonly Dictionary<string, (Vector2 xz, float y, Vector2 forward)> s_RampLips =
        new Dictionary<string, (Vector2, float, Vector2)>();

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
        LavaLetalArt.Ensure();
        s_Footprints.Clear();
        s_Bubbles.Clear();
        s_RampLips.Clear();

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

        var player = LevelKit.AssembleSystems(kRanks);
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
        Plate(root, "Placa1", 2f, 21f, 4.6f, 5f, 5f, 15f, LavaLetalArt.Metal, trim: true, supports: true);
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

        // Um degrau metálico em cima da ilha, para ela misturar pedra e chapa
        // como a referência. Fica fora da linha entre a entrada e a saída.
        Plate(root, "Podio", 14f, 79.5f, 5.6f, 3f, 3f, 10f, LavaLetalArt.Metal, trim: true, supports: false);

        Plate(root, "Patio", 28f, 81f, 5f, 8f, 8f, 0f, LavaLetalArt.Metal, trim: true, supports: true);
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
        Plate(root, "Segura2", 35f, 101f, 6f, 4f, 6f, 10f, LavaLetalArt.MetalBlue, trim: true, supports: true);
        Rock(root, "Segura3", 29.5f, 109.5f, 6.2f, 3.4f, 6, 105, LavaLetalArt.RockGray);
        Rock(root, "Juncao", 20f, 111f, 6.4f, 4.6f, 7, 106, LavaLetalArt.RockBurnt);

        LevelKit.Place(root, "yellow", "platform_2x2x1", new Vector3(20f, 5f, 92f));
        LevelKit.Place(root, "neutral", "pillar_1x1x8", new Vector3(20f, -3f, 92f));
        Register("Atalho1", 6f, Square(20f, 92f, 2f));
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

        Column(root, "Pedra1", -3.5f, 113.5f, 5f, 107);
        Column(root, "Pedra2", -8f, 117f, 5f, 108);
        Column(root, "Pedra3", -12.5f, 113.5f, 5f, 109);

        Rock(root, "Torre", -24f, 107f, 8f, 3.6f, 6, 110, LavaLetalArt.RockGray);
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
    // Peças
    // ------------------------------------------------------------------

    /// <summary>Quanto a base das ilhas se abre em relação ao topo, perto da lava.</summary>
    const float kFlare = 1.12f;

    /// <summary>
    /// Ilha de pedra que sobe da lava: polígono irregular, borda escura, topo
    /// na cor pedida.
    ///
    /// A base é 12% mais larga que o topo, e não o contrário: rocha que sai da
    /// lava se alarga embaixo. E é o topo que guarda o tamanho pedido, porque
    /// é nele que se pousa.
    /// </summary>
    static GameObject Rock(Transform root, string name, float x, float z, float top, float radius,
                           int sides, int seed, Material topMaterial, bool route = true)
    {
        var ring = LowPolyMesh.Rock(sides, radius, kRockMin, new System.Random(seed));
        var mesh = LowPolyMesh.Prism(Flare(ring), kBedrock, top, LavaLetalArt.RockTile, 1f / kFlare, name);

        var go = Solid(root, name, mesh, topMaterial, LavaLetalArt.RockSide, new Vector3(x, 0f, z), 0f);
        AddMeshCollider(go);

        if (route) Register(name, top, World(go.transform, ring));
        return go;
    }

    /// <summary>Coluna de basalto hexagonal, do tamanho de um bloco de 2 m.</summary>
    static void Column(Transform root, string name, float x, float z, float top, int seed)
    {
        float turn = (float)new System.Random(seed).NextDouble() * 60f;
        var ring = LowPolyMesh.Regular(6, 1.2f, turn);

        var go = Solid(root, name, LowPolyMesh.Prism(ring, kBedrock, top, LavaLetalArt.RockTile, 1f, name),
                       LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, new Vector3(x, 0f, z), 0f);
        AddMeshCollider(go);
        Register(name, top, World(go.transform, ring));
    }

    /// <summary>
    /// Placa metálica xadrez: tampo, faixas de perigo nas bordas e pés até a
    /// lava.
    ///
    /// Só o tampo tem colisão, numa caixa. Faixas e pés são imagem: os pés
    /// ficam embaixo da placa, onde ninguém pisa, e as faixas ficam dentro do
    /// contorno do tampo, então não mudam onde a bruxa pode pousar.
    /// </summary>
    static void Plate(Transform root, string name, float x, float z, float top, float sizeX, float sizeZ,
                      float yaw, Material material, bool trim, bool supports, float thickness = 0.6f)
    {
        var rectangle = LowPolyMesh.Rectangle(sizeX, sizeZ);
        var go = Solid(root, name, LowPolyMesh.Prism(rectangle, -thickness, 0f, LavaLetalArt.MetalTile, 1f, name),
                       material, LavaLetalArt.Dark, new Vector3(x, top, z), yaw);

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -thickness * 0.5f, 0f);
        box.size = new Vector3(sizeX, thickness, sizeZ);

        if (trim)
        {
            const float w = 0.28f;
            Trim(go.transform, new Vector3(0f, 0f, sizeZ * 0.5f - w * 0.5f), new Vector2(sizeX, w));
            Trim(go.transform, new Vector3(0f, 0f, -sizeZ * 0.5f + w * 0.5f), new Vector2(sizeX, w));
            Trim(go.transform, new Vector3(sizeX * 0.5f - w * 0.5f, 0f, 0f), new Vector2(w, sizeZ - 2f * w));
            Trim(go.transform, new Vector3(-sizeX * 0.5f + w * 0.5f, 0f, 0f), new Vector2(w, sizeZ - 2f * w));
        }

        if (supports)
        {
            float length = top - thickness - kBedrock;
            float ix = Mathf.Max(0f, sizeX * 0.5f - 0.6f), iz = Mathf.Max(0f, sizeZ * 0.5f - 0.6f);
            var leg = LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.4f, 0.4f), -length, 0f, 1f, 1f, "Pe");

            foreach (var corner in new[] { new Vector2(ix, iz), new Vector2(-ix, iz), new Vector2(ix, -iz), new Vector2(-ix, -iz) })
            {
                var foot = Solid(go.transform, "Pe", leg, LavaLetalArt.Dark, LavaLetalArt.Dark,
                                 new Vector3(corner.x, -thickness, corner.y), 0f);
                foot.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        Register(name, top, World(go.transform, rectangle));
    }

    /// <summary>A bolha da Torre de Bolhas, igual: mesmo prefab, mesmo quique, mesmo rewind.</summary>
    static void Bubble(Transform root, string name, Vector3 center, float bobPhase)
    {
        LevelKit.Bubble(root, center, bobPhase).name = name;
        s_Bubbles[name] = center;
    }

    // Perfil das rampas: 3 m de entrada a 17 graus, e o resto a 34. A entrada
    // fica abaixo dos 25 graus do deslize - ali a bruxa ainda corre - e é o
    // que impede quem chega a 11 m/s de voar por cima da rampa (ver
    // LowPolyMesh.Ramp). O trecho de 34 é o que dispara o deslize.
    const float kRampEntry = 3f;
    const float kRampEntryAngle = 17f;
    const float kRampAngle = 34f;

    /// <summary>Comprimento total de uma rampa com essa queda, entrada inclusa.</summary>
    static float RampLength(float drop)
    {
        float entryDrop = kRampEntry * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad);
        return kRampEntry + (drop - entryDrop) / Mathf.Tan(kRampAngle * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Rampa de chapa sobre bloco de pedra, descendo para +Z (girada por
    /// <paramref name="yaw"/>). <paramref name="topEdge"/> é o meio da aresta de
    /// cima; a de baixo termina num degrau sobre a lava.
    ///
    /// Entrando a 11 m/s, a bruxa ganha o impulso de 3 ao começar a deslizar e
    /// chega ao teto de 15 m/s antes do fim do trecho íngreme.
    /// </summary>
    static void Ramp(Transform root, string name, Vector3 topEdge, float width, float drop, float yaw)
    {
        float length = RampLength(drop);
        float entryDrop = kRampEntry * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad);

        var mesh = LowPolyMesh.Ramp(width, length, drop, kBedrock - topEdge.y, LavaLetalArt.MetalTile,
                                    kRampEntry, entryDrop, name);
        var go = Solid(root, name, mesh, LavaLetalArt.Metal, LavaLetalArt.RockSide, topEdge, yaw);
        AddMeshCollider(go);

        // Faixas de perigo nas duas bordas laterais, uma por trecho da descida.
        var corners = new[] { Vector2.zero, new Vector2(kRampEntry, -entryDrop), new Vector2(length, -drop) };
        for (int i = 0; i + 1 < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[i + 1];
            float angle = Mathf.Atan2(a.y - b.y, b.x - a.x) * Mathf.Rad2Deg;
            float span = Vector2.Distance(a, b);
            var mid = (a + b) * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                var strip = new GameObject("Faixa");
                strip.transform.SetParent(go.transform, false);
                strip.transform.SetLocalPositionAndRotation(
                    new Vector3(side * (width * 0.5f - 0.14f), mid.y, mid.x), Quaternion.Euler(angle, 0f, 0f));
                Solid(strip.transform, "Faixa",
                      LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.28f, span), -0.24f, 0.015f, 1f, 1f, "Faixa"),
                      LavaLetalArt.Hazard, LavaLetalArt.Hazard, Vector3.zero, 0f);
            }
        }

        var lip = go.transform.TransformPoint(new Vector3(0f, -drop, length));
        var forward = go.transform.forward;
        s_RampLips[name] = (new Vector2(lip.x, lip.z), lip.y, new Vector2(forward.x, forward.z).normalized);
    }

    static void Trim(Transform plate, Vector3 center, Vector2 size)
    {
        var strip = LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.y), -0.24f, 0.015f, 1f, 1f, "Faixa");
        Solid(plate, "Faixa", strip, LavaLetalArt.Hazard, LavaLetalArt.Hazard, center, 0f);
    }

    static void FloatingBlock(Transform root, string name, Vector3 center, Vector3 size, float yaw)
    {
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.z), -size.y * 0.5f, size.y * 0.5f,
                                     LavaLetalArt.RockTile, 1f, name);
        Solid(root, name, mesh, LavaLetalArt.RockBurnt, LavaLetalArt.Dark, center, yaw);
    }

    /// <summary>
    /// Estrutura escura e retangular com luzes pequenas na face: a "fábrica"
    /// ao longe da referência, reduzida ao que se lê a 80 m de distância.
    /// </summary>
    static void DarkTower(Transform root, string name, Vector3 basePosition, Vector3 size, float yaw, int seed)
    {
        var tower = Solid(root, name, LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.z), kBedrock, size.y,
                                                        LavaLetalArt.RockTile * 2f, 1f, name),
                          LavaLetalArt.Dark, LavaLetalArt.Dark, basePosition, yaw);

        var lights = new[] { LavaLetalArt.GlowRed, LavaLetalArt.GlowYellow, LavaLetalArt.GlowCyan };
        // Semente fixa, e não o hash do nome: o hash de string do .NET muda a
        // cada execução, e as luzes trocariam de lugar a cada reconstrução.
        var random = new System.Random(seed);

        for (int i = 0; i < 7; i++)
        {
            float y = size.y * (0.25f + 0.65f * (float)random.NextDouble());
            float x = (float)(random.NextDouble() - 0.5) * size.x * 0.7f;
            var lamp = LowPolyMesh.Prism(LowPolyMesh.Rectangle(1.2f, 0.3f), -0.4f, 0.4f, 1f, 1f, "Luz");

            Solid(tower.transform, "Luz", lamp, lights[i % lights.Length], lights[i % lights.Length],
                  new Vector3(x, y, -size.z * 0.5f - 0.1f), 0f);
        }
    }

    // ------------------------------------------------------------------
    // Montagem
    // ------------------------------------------------------------------

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Solid(Transform parent, string name, Mesh mesh, Material top, Material sides,
                            Vector3 localPosition, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, sides };

        // Tudo aqui é cenário que não se mexe: estático para o batching.
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void Decor(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition) =>
        Solid(parent, name, mesh, material, material, localPosition, 0f);

    /// <summary>
    /// Colisor convexo da própria malha. Convexo porque a malha tem uma dúzia
    /// de faces, e o polígono sorteado é quase convexo - a diferença na borda
    /// é de centímetros, e colisor convexo é o barato e o estável.
    /// </summary>
    static void AddMeshCollider(GameObject go)
    {
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = true;
    }

    static Vector2[] Flare(Vector2[] ring)
    {
        var wide = new Vector2[ring.Length];
        for (int i = 0; i < ring.Length; i++) wide[i] = ring[i] * kFlare;
        return wide;
    }

    static Vector2[] World(Transform t, Vector2[] local)
    {
        var world = new Vector2[local.Length];
        for (int i = 0; i < local.Length; i++)
        {
            var p = t.TransformPoint(new Vector3(local[i].x, 0f, local[i].y));
            world[i] = new Vector2(p.x, p.z);
        }

        return world;
    }

    static Vector2[] Square(float x, float z, float size)
    {
        var ring = LowPolyMesh.Rectangle(size, size);
        for (int i = 0; i < ring.Length; i++) ring[i] += new Vector2(x, z);
        return ring;
    }

    static void Register(string name, float top, Vector2[] worldPolygon) => s_Footprints[name] = (top, worldPolygon);

    // ------------------------------------------------------------------
    // Validação dos saltos
    // ------------------------------------------------------------------

    /// <summary>
    /// Refaz, a cada reconstrução, a conta de cada salto da rota.
    ///
    /// Usa os números do prefab da bruxa, e não constantes copiadas: se alguém
    /// mexer na altura do pulo ou na gravidade, a próxima reconstrução diz no
    /// console qual salto deixou de fechar, em vez de isso ser descoberto
    /// jogando.
    ///
    /// O vão é a menor distância entre os contornos, e o critério é
    /// conservador: a bruxa sai 0,3 m antes da beira e precisa pousar 0,5 m
    /// para dentro; o pulo duplo conta disparado no topo do primeiro (0,5 s),
    /// não no instante ideal, que é tarde e quase ninguém acerta.
    /// </summary>
    static void ValidateJumps()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchSetup.PrefabPath);
        var controller = prefab != null ? prefab.GetComponent<PlayerController>() : null;
        if (controller == null)
        {
            Debug.LogWarning("Sem o prefab da bruxa não dá para validar os saltos da fase 3.");
            return;
        }

        var p = new SerializedObject(controller);
        var jump = new JumpModel
        {
            Speed = p.FindProperty("moveSpeed").floatValue,
            Gravity = p.FindProperty("gravity").floatValue,
            Fall = p.FindProperty("fallGravityMultiplier").floatValue,
            ApexThreshold = p.FindProperty("apexThreshold").floatValue,
            Apex = p.FindProperty("apexGravityMultiplier").floatValue,
            Jump = p.FindProperty("jumpHeight").floatValue,
            DoubleJump = p.FindProperty("doubleJumpHeight").floatValue,
        };

        var report = new StringBuilder("Fase 3 - saltos validados contra o pulo real " +
                                       $"({jump.Speed} m/s, pulo {jump.Jump} m, duplo {jump.DoubleJump} m):\n");
        int impossible = 0;

        void Check(string title, string[] route)
        {
            report.Append($"\n{title}\n");
            for (int i = 0; i + 1 < route.Length; i++)
            {
                if (!s_Footprints.TryGetValue(route[i], out var a) || !s_Footprints.TryGetValue(route[i + 1], out var b))
                {
                    report.Append($"  {route[i]} -> {route[i + 1]}: peça não registrada\n");
                    impossible++;
                    continue;
                }

                float gap = Gap(a.poly, b.poly);
                float dy = b.top - a.top;
                float need = gap + 0.8f;
                float single = jump.Reach(dy, null);
                float twin = jump.Reach(dy, 0.5f);

                string verdict;
                if (single > 0f && need <= 0.75f * single) verdict = "simples, folgado";
                else if (single > 0f && need <= 0.95f * single) verdict = "simples, no limite";
                else if (need <= 0.85f * twin) verdict = "PEDE PULO DUPLO";
                else { verdict = "IMPOSSÍVEL"; impossible++; }

                report.Append($"  {route[i]} -> {route[i + 1]}: vão {gap:0.0} m, desnível {dy:+0.0;-0.0;0.0} m " +
                              $"(alcance {single:0.0} / {twin:0.0} m) - {verdict}\n");
            }
        }

        for (int i = 0; i < kMainRoute.Length; i++)
            Check($"Rota principal, trecho {i + 1}", kMainRoute[i]);
        Check("Atalho", kShortcut);

        impossible += ValidateCombos(jump, report);

        if (impossible > 0)
            Debug.LogWarning(report.Append($"\n{impossible} salto(s) impossível(is).").ToString());
        else
            Debug.Log(report.ToString());
    }

    /// <summary>
    /// Os combos de bolha e rampa, simulados quadro a quadro.
    ///
    /// Cada um é testado com as bolhas nos dois extremos do balanço (±0,3 m),
    /// porque o jogador chega nelas em qualquer fase dele. E os que dependem
    /// de velocidade são testados nas duas pontas: 11 m/s (correndo) e 15
    /// (saindo da rampa), além de deslizar para fora sem pular.
    /// </summary>
    static int ValidateCombos(JumpModel jump, StringBuilder report)
    {
        int failures = 0;
        report.Append("\nCombos de bolha e rampa\n");

        void Expect(string title, bool shouldLand, (bool landed, float d) result)
        {
            bool ok = result.landed == shouldLand;
            if (!ok) failures++;

            string what = result.landed ? $"pousa a {result.d:0.0} m" : "não alcança";
            string verdict = ok ? (shouldLand ? "ok" : "ok, como pedido") : (shouldLand ? "FALHA" : "PASSA SEM O COMBO");
            report.Append($"  {title}: {what} - {verdict}\n");
        }

        // Combo A: ilhota -> bolha -> alto.
        foreach (float bob in new[] { -0.3f, 0.3f })
            Expect($"Ilhota1 -> Bolha_A -> Alto_A (balanço {bob:+0.0;-0.0})", true,
                   FromIsland(jump, "Ilhota1", "Alto_A", jump.Speed, new[] { "Bolha_A" }, bob));
        Expect("Ilhota1 -> Alto_A sem a bolha", false,
               FromIsland(jump, "Ilhota1", "Alto_A", jump.Speed, new string[0], 0f));

        // Rampa A -> pista: os dois jeitos de sair precisam pousar.
        Expect("Rampa_A -> Pista deslizando para fora", true,
               FromRamp(jump, "Rampa_A", "Pista", 14f, new string[0], 0f, jumps: false));
        Expect("Rampa_A -> Pista pulando na beira a 15 m/s", true,
               FromRamp(jump, "Rampa_A", "Pista", 15f, new string[0], 0f, jumps: true));
        Expect("Rampa_A -> Pista pulando 3 m antes da beira a 15 m/s", true,
               FromRamp(jump, "Rampa_A", "Pista", 15f, new string[0], 0f, jumps: true, early: 3f));

        // Combo B: torre -> duas bolhas -> mirante.
        foreach (float bob in new[] { -0.3f, 0.3f })
            Expect($"Torre -> Bolha_B1 -> Bolha_B2 -> Mirante (balanço {bob:+0.0;-0.0})", true,
                   FromIsland(jump, "Torre", "Mirante", jump.Speed, new[] { "Bolha_B1", "Bolha_B2" }, bob));

        // Combo C: rampa B -> bolha no vão -> chegada, pulando da beira ou até
        // 3 m antes dela - ninguém pula exatamente na beira.
        foreach (float speed in new[] { 12f, 15f })
        foreach (float early in new[] { 0f, 1.5f, 3f })
        foreach (float bob in new[] { -0.3f, 0.3f })
            Expect($"Rampa_B -> Bolha_B3 -> Chegada a {speed:0} m/s, {early:0.0} m antes da beira " +
                   $"(balanço {bob:+0.0;-0.0})", true,
                   FromRamp(jump, "Rampa_B", "Chegada", speed, new[] { "Bolha_B3" }, bob, jumps: true, early: early));
        Expect("Rampa_B -> Chegada a 15 m/s sem bolha nem duplo", false,
               FromRamp(jump, "Rampa_B", "Chegada", 15f, new string[0], 0f, jumps: true));
        Expect("Rampa_B -> Chegada a 15 m/s sem bolha, com duplo", true,
               FromRamp(jump, "Rampa_B", "Chegada", 15f, new string[0], 0f, jumps: true, doubleAt: 0.5f));

        return failures;
    }

    /// <summary>Salto que sai da beira de uma ilha, na direção de outra.</summary>
    static (bool, float) FromIsland(JumpModel jump, string from, string to, float speed,
                                    string[] bubbles, float bob, float? doubleAt = null)
    {
        if (!s_Footprints.TryGetValue(from, out var a) || !s_Footprints.TryGetValue(to, out var b))
            return (false, 0f);

        var origin = Centroid(a.poly);
        var direction = (Centroid(b.poly) - origin).normalized;
        var start = origin + direction * RayHits(a.poly, origin, direction).max;

        return Fly(jump, start, a.top, direction, speed, b, bubbles, bob, true, doubleAt);
    }

    /// <summary>
    /// Salto que sai de uma rampa a uma velocidade de deslize, da ponta de
    /// baixo ou <paramref name="early"/> metros antes dela (mais alto, no
    /// trecho de 34 graus).
    /// </summary>
    static (bool, float) FromRamp(JumpModel jump, string ramp, string to, float speed,
                                  string[] bubbles, float bob, bool jumps, float? doubleAt = null,
                                  float early = 0f)
    {
        if (!s_RampLips.TryGetValue(ramp, out var lip) || !s_Footprints.TryGetValue(to, out var b))
            return (false, 0f);

        var start = lip.xz - lip.forward * early;
        float height = lip.y + early * Mathf.Tan(kRampAngle * Mathf.Deg2Rad);
        return Fly(jump, start, height, lip.forward, speed, b, bubbles, bob, jumps, doubleAt);
    }

    /// <summary>
    /// Voa em linha reta a partir de <paramref name="start"/> e diz se pousa
    /// no topo de <paramref name="target"/>. As bolhas valem quando a bruxa
    /// passa a menos do raio do gatilho do centro delas, como no AirBubble.
    /// </summary>
    static (bool landed, float d) Fly(JumpModel jump, Vector2 start, float y0, Vector2 direction, float speed,
                                      (float top, Vector2[] poly) target, string[] bubbles, float bob,
                                      bool jumps, float? doubleAt)
    {
        const float dt = 1f / 240f;
        const float trigger = LevelKit.BubbleRadius + 0.2f;

        var hits = RayHits(target.poly, start, direction);
        float landFrom = hits.min + 0.3f, landTo = hits.max - 0.3f;

        var points = new List<(float d, float y)>();
        foreach (var name in bubbles)
        {
            if (!s_Bubbles.TryGetValue(name, out var c)) continue;
            points.Add((Vector2.Dot(new Vector2(c.x, c.z) - start, direction), c.y + bob));
        }

        float v = jumps ? Mathf.Sqrt(2f * jump.Gravity * jump.Jump) : 0f;
        float y = y0, d = 0f, t = 0f;
        var used = new bool[points.Count];
        bool doubled = !doubleAt.HasValue;

        while (t < 8f && y > 0f)
        {
            if (!doubled && t >= doubleAt.Value)
            {
                v = Mathf.Sqrt(2f * jump.Gravity * jump.DoubleJump);
                doubled = true;
            }

            float scale = v < 0f ? jump.Fall : 1f;
            if (Mathf.Abs(v) < jump.ApexThreshold) scale *= jump.Apex;

            v -= jump.Gravity * scale * dt;
            float previous = y;
            y += v * dt;
            d += speed * dt;
            t += dt;

            for (int i = 0; i < points.Count; i++)
            {
                if (used[i] || new Vector2(d - points[i].d, y - points[i].y).magnitude > trigger) continue;

                v = Mathf.Max(v, Mathf.Sqrt(2f * jump.Gravity * BubbleHeight));
                used[i] = true;
            }

            if (v < 0f && previous > target.top && y <= target.top && d >= landFrom && d <= landTo)
                return (true, d);
        }

        return (false, d);
    }

    /// <summary>Altura do quique da bolha, a mesma do AirBubble.</summary>
    const float BubbleHeight = 4.5f;

    static Vector2 Centroid(Vector2[] poly)
    {
        var sum = Vector2.zero;
        foreach (var p in poly) sum += p;
        return sum / poly.Length;
    }

    /// <summary>Onde o raio cruza o contorno: a primeira e a última distância.</summary>
    static (float min, float max) RayHits(Vector2[] poly, Vector2 origin, Vector2 direction)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var e = poly[(i + 1) % poly.Length] - a;
            float denominator = direction.x * e.y - direction.y * e.x;
            if (Mathf.Abs(denominator) < 1e-6f) continue;

            var w = a - origin;
            float t = (w.x * e.y - w.y * e.x) / denominator;
            float u = (w.x * direction.y - w.y * direction.x) / denominator;
            if (u < 0f || u > 1f) continue;

            min = Mathf.Min(min, t);
            max = Mathf.Max(max, t);
        }

        return (min, max);
    }

    /// <summary>Arco do pulo, com a mesma gravidade em faixas do PlayerController.</summary>
    struct JumpModel
    {
        public float Speed, Gravity, Fall, ApexThreshold, Apex, Jump, DoubleJump;

        /// <summary>Distância horizontal até pousar numa superfície <paramref name="dy"/> acima.</summary>
        public float Reach(float dy, float? secondJumpAt)
        {
            const float dt = 1f / 240f;
            float v = Mathf.Sqrt(2f * Gravity * Jump), y = 0f, t = 0f;
            bool used = false;

            while (t < 6f)
            {
                if (secondJumpAt.HasValue && !used && t >= secondJumpAt.Value)
                {
                    v = Mathf.Sqrt(2f * Gravity * DoubleJump);
                    used = true;
                }

                float scale = v < 0f ? Fall : 1f;
                if (Mathf.Abs(v) < ApexThreshold) scale *= Apex;

                v -= Gravity * scale * dt;
                y += v * dt;
                t += dt;

                if (v < 0f && y <= dy) return Speed * t;
            }

            return 0f;
        }
    }

    static float Gap(Vector2[] a, Vector2[] b)
    {
        float best = float.MaxValue;
        for (int pass = 0; pass < 2; pass++)
        {
            var from = pass == 0 ? a : b;
            var to = pass == 0 ? b : a;

            foreach (var point in from)
            for (int i = 0; i < to.Length; i++)
                best = Mathf.Min(best, SegmentDistance(point, to[i], to[(i + 1) % to.Length]));
        }

        return best;
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
