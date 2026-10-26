using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a fase "Corrida de Lava", desenhada para ser corrida contra o relógio.
///
/// O traçado mudou de lógica em relação à fase anterior. Lá eram ilhas soltas,
/// e cada salto era uma parada; aqui o chão é contínuo em trechos longos, que é
/// o que o bunny hop exige - sem solo para encadear, o embalo nunca sobe.
///
/// A fase se divide em quatro ritmos:
///   pista      corrida longa com barreiras, onde o embalo se constrói
///   descida    rampas que o slide atravessa, convertendo altura em velocidade
///   ilhas      precisão, onde a velocidade vira risco em vez de vantagem
///   poço       subida de parede, o único trecho que zera o relógio de embalo
///
/// Entre as ilhas há uma bifurcação: o atalho é um vão de 14 m, que o pulo
/// correndo não alcança (11,3 m) e o pulo com embalo cheio alcança (18,4 m).
/// É a única passagem da fase que cobra a mecânica em vez de sugeri-la, e tem
/// uma rota segura ao lado para quem perdeu o ritmo.
/// </summary>
public static class SpeedrunLevelBuilder
{
    const string kScenePath = "Assets/Scenes/Level_CorridaDeLava.unity";

    // Altura do piso da pista e de onde as prensas largam.
    const float kRunwayTop = 9f;
    const float kCrusherTop = 17f;

    // Limites de S, A, B, C e D, em segundos. Acima de D é F.
    //
    // Ancorados numa corrida real, não no traçado. As medalhas antigas eram
    // estimativas (ouro 46 s) e o melhor tempo gravado no save chegou a
    // 20,26 s - o ouro dava para pegar andando a menos da metade do ritmo.
    // O S fica logo acima desse recorde: é o tempo de quem domina a fase. Os
    // degraus abrem em proporção (cerca de +20%, +50%, +95% e +170% sobre o
    // S), então cada rank pede uma melhora do mesmo tamanho relativo.
    static readonly float[] kRanks = { 21f, 25f, 31f, 41f, 57f };

    [MenuItem("Rewind Run/Criar Fase: Corrida de Lava", priority = 7)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        LevelKit.CreateLighting();

        var level = new GameObject("Fase").transform;
        CreateRunway(level);
        CreateDescent(level);
        CreateIslands(level);
        CreateShaft(level);
        CreateFinish(level);

        var zones = new GameObject("ZonasDeCamera").transform;
        CreateCameraZones(zones);

        LevelKit.AssembleSystems(kRanks, "Level_TorreDeBolhas");
        LevelKit.CreateMusic(LevelKit.LightLevelMusic);

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 75f), 90f, 190f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);
        LevelKit.AddSceneToBuildSettings(kScenePath);

        Debug.Log($"Fase criada em {kScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s.");
    }

    // ------------------------------------------------------------------
    // Traçado
    // ------------------------------------------------------------------

    /// <summary>
    /// Abertura, em tres tempos: largada, prensas e helices.
    ///
    /// O chao continua continuo porque o bunny hop precisa de solo para
    /// encadear - uma sequencia de ilhas zeraria o embalo a cada vao. O que
    /// mudou e o que ocupa o ESPACO ACIMA do chao.
    ///
    /// Essa e a licao do simulador: o pulo dura 1,04 s e passa 0,84 s acima de
    /// 1 m, entao quem encadeia hops esta no ar quase o tempo todo. Obstaculo
    /// baixo no chao e invisivel para esse jogador - ele voa por cima sem nem
    /// perceber. Por isso os perigos daqui descem do alto (prensas) ou sao
    /// altos demais para saltar (helices, 4 m contra 3,29 m de pulo).
    /// </summary>
    static void CreateRunway(Transform root)
    {
        // Largada e prensas: 12 m de largura.
        for (float z = 0f; z <= 42f; z += 6f)
        {
            Place(root, "green", "platform_6x6x1", new Vector3(-3f, 8f, z));
            Place(root, "green", "platform_6x6x1", new Vector3(3f, 8f, z));
        }

        // Corredor das helices: 6 m. O estreitamento e o proprio aviso - a
        // pista afunilando diz "aqui cair custa caro" antes de qualquer placa.
        for (float z = 48f; z <= 66f; z += 6f)
            Place(root, "green", "platform_6x6x1", new Vector3(0f, 8f, z));

        CreateStart(root);
        CreateCrushers(root);
        CreateBlades(root);
    }

    /// <summary>
    /// Largada: dois obstaculos baixos em 18 m de pista limpa.
    ///
    /// Ficam faceis de proposito. E onde o ritmo do hop se aprende sem custar
    /// uma vida, e quem chega as prensas sem ter aprendido chega devagar.
    /// </summary>
    static void CreateStart(Transform root)
    {
        foreach (float z in new[] { 8f, 16f })
        {
            for (int i = -1; i <= 1; i++)
                Place(root, "red", "barrier_4x1x1", new Vector3(i * 4f, 9f, z));
        }

        Place(root, "green", "flag_A", new Vector3(0f, 9f, -2f));
        Place(root, "neutral", "signage_arrows_right", new Vector3(5.6f, 9f, 2f), 180f);
    }

    /// <summary>
    /// Prensas: blocos que despencam na pista e sobem de novo.
    ///
    /// Alternam centro e laterais, entao o trajeto e um slalom com prazo. As
    /// fases sao espacadas de 0,25 do ciclo. O slalom obriga a 4 m de lado nos
    /// 7 m entre fileiras, e essa diagonal de 8,06 m leva 0,73 s a 11 m/s - que
    /// e 0,25 do ciclo de 2,9 s. A fase segue o caminho real, nao a distancia
    /// no papel, e por isso e refeita toda vez que a velocidade base muda.
    ///
    /// A janela de passagem e larga de proposito: o bloco so mata enquanto
    /// desce, o que deixa quase 2,4 s de passagem segura num ciclo de 2,9 s -
    /// parado embaixo ele ainda bloqueia a faixa, mas como muro, nao como
    /// armadilha. Como nao posso testar isto correndo, prefiro errar para
    /// o lado generoso - uma fase facil demais se aperta depois, uma impossivel
    /// nao se joga nem uma vez.
    /// </summary>
    static void CreateCrushers(Transform root)
    {
        Prensa(root, -4.5f, 21f, 0.00f);
        Prensa(root, 4.5f, 21f, 0.00f);

        Prensa(root, 0f, 28f, 0.25f);

        Prensa(root, -4.5f, 35f, 0.50f);
        Prensa(root, 4.5f, 35f, 0.50f);

        Prensa(root, 0f, 42f, 0.75f);

        Place(root, "neutral", "cone", new Vector3(-5.2f, 9f, 17f));
        Place(root, "neutral", "cone", new Vector3(5.2f, 9f, 17f));
    }

    /// <summary>
    /// Helices: barras que varrem o corredor estreito.
    ///
    /// Tem 4 m de altura contra 3,29 m de pulo - nao da para saltar por cima, e
    /// essa e a diferenca entre um obstaculo e um enfeite. A unica saida e
    /// passar quando a barra esta alinhada com o corredor.
    ///
    /// A barra nao mata: arremessa. Quem apanha so morre se cair na lava, e com
    /// o pulo duplo ainda da para voltar ao corredor. Isso troca "errou,
    /// morreu" por "errou, se vira" - e num jogo de tempo a recuperacao mal
    /// feita ja custa os segundos que a morte custaria.
    ///
    /// Giram em velocidades diferentes e em sentidos alternados. Tres barras
    /// identicas viram um so problema repetido tres vezes; tres diferentes
    /// obrigam a ler cada uma.
    ///
    /// A barra vai de -2,8 a 2,8 num corredor de -3 a 3. Nao e numero redondo
    /// por acaso: com 2,5 sobrava meio metro de borda, e como o corpo da bruxa
    /// tem 0,42 m de raio, dava uma janela de 8 cm entre a ponta da barra e o
    /// vazio. Larga demais para ser claramente impossivel, estreita demais para
    /// ser jogavel - o tipo de fresta que so gera duvida. Em 2,8 nao sobra
    /// nenhuma, e a unica saida passa a ser o tempo.
    /// </summary>
    static void CreateBlades(Transform root)
    {
        Helice(root, 51f, 65f, 0.00f);
        Helice(root, 58f, -80f, 0.33f);
        Helice(root, 65f, 85f, 0.66f);

        Place(root, "red", "flag_B", new Vector3(-2.6f, 9f, 46f));
        Place(root, "red", "flag_B", new Vector3(2.6f, 9f, 46f));
    }





    /// <summary>
    /// Descida: duas rampas de 34° e uma reta de chegada.
    ///
    /// Aqui a altura vira velocidade. Quem chega com embalo sai ainda mais
    /// rápido, e a reta longa no fim existe para esse excesso ter onde ser
    /// gasto antes das ilhas.
    /// </summary>
    static void CreateDescent(Transform root)
    {
        Place(root, "yellow", "platform_slope_6x6x4", new Vector3(0f, 5f, 72f));
        Place(root, "yellow", "platform_slope_6x6x4", new Vector3(0f, 1f, 78f));

        for (float z = 84f; z <= 102f; z += 6f)
        {
            Place(root, "green", "platform_6x6x1", new Vector3(-3f, 0f, z));
            Place(root, "green", "platform_6x6x1", new Vector3(3f, 0f, z));
        }
    }

    /// <summary>
    /// Ilhas, com a bifurcação da fase.
    ///
    /// O atalho é um vão de 15 m em linha reta. Na velocidade base o pulo
    /// alcança 11,4 m e não passa; com o embalo cheio alcança 18,7 m e passa.
    ///
    /// O vão ficou em 15 m mesmo depois de a velocidade cair de 13 para 11.
    /// Em 14 m também funcionaria, mas 15 deixa 3,6 m de folga em vez de 2,6 -
    /// e esta é a aposta central da fase, a que não deve virar sorte. A rota
    /// segura contorna pela esquerda em quatro pedras - mais lenta, sempre
    /// possível. É a troca que define um jogo de tempo: risco por segundos.
    ///
    /// As pedras da rota segura passaram de 2x2 para 4x4 quando o botão de
    /// correr saiu. Com uma velocidade só, não existe mais chegar devagar: a
    /// 13 m/s a freada ocupa pouco mais de um metro, e numa pedra de 2 m isso
    /// significa escorregar para fora dela. Uma rota chamada "segura" que exige
    /// pouso milimétrico não é uma escolha, é uma armadilha.
    /// </summary>
    static void CreateIslands(Transform root)
    {
        Place(root, "green", "platform_4x4x1", new Vector3(0f, 0f, 111f));

        // Rota segura, pela esquerda.
        Place(root, "blue", "platform_4x4x1", new Vector3(-8f, 0f, 117f));
        Place(root, "blue", "platform_4x4x1", new Vector3(-10f, 0f, 124f));
        Place(root, "blue", "platform_4x4x1", new Vector3(-6f, 0f, 130f));

        Place(root, "green", "platform_4x4x1", new Vector3(0f, 0f, 130f));

        // A placa basta para marcar a bifurcação. Havia uma estrela flutuando no
        // meio do vão, e ela mentia: num jogo com coleta, uma estrela no ar é
        // algo que se pega - aqui não era nada, só enfeite prometendo prêmio.
        Place(root, "neutral", "signage_arrows_left", new Vector3(-2.6f, 1f, 112f));
    }

    /// <summary>Poço de wall jump: sobe de 1 m a 10 m. É o freio da fase.</summary>
    static void CreateShaft(Transform root)
    {
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 0f, 138f));

        foreach (float height in new[] { 1f, 5f })
        {
            Place(root, "blue", "platform_6x2x4", new Vector3(-2f, height, 138f), 90f);
            Place(root, "blue", "platform_6x2x4", new Vector3(2f, height, 138f), 90f);
        }

        Place(root, "green", "platform_4x4x1", new Vector3(0f, 9f, 144f));
    }

    static void CreateFinish(Transform root)
    {
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 9f, 150f));
        Place(root, "green", "flag_A", new Vector3(0f, 10f, 150f));
        Place(root, "neutral", "signage_finish", new Vector3(0f, 10f, 147.5f));

        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 11f, 150f);

        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(5f, 3f, 5f);
        area.isTrigger = true;

        go.AddComponent<LevelFinish>();
    }

    /// <summary>Mesmo ângulo em todas: só altura e distância mudam.</summary>
    static void CreateCameraZones(Transform root)
    {
        LevelKit.Zone(root, "Pista", new Vector3(0f, 12f, 20f), new Vector3(44f, 34f, 56f), 26f, 20f);

        // Camera mais alta e mais longe no corredor: para cronometrar a barra o
        // jogador precisa ve-la inteira, nao so a ponta que chega.
        LevelKit.Zone(root, "Helices", new Vector3(0f, 12f, 58f), new Vector3(30f, 34f, 26f), 32f, 23f);
        LevelKit.Zone(root, "Descida", new Vector3(0f, 6f, 78f), new Vector3(34f, 26f, 22f), 20f, 22f);
        LevelKit.Zone(root, "Reta", new Vector3(0f, 4f, 95f), new Vector3(34f, 22f, 26f), 26f, 20f);
        LevelKit.Zone(root, "Ilhas", new Vector3(-4f, 4f, 120f), new Vector3(36f, 22f, 34f), 42f, 18f);
        LevelKit.Zone(root, "Poco", new Vector3(0f, 7f, 140f), new Vector3(16f, 30f, 14f), 16f, 16f);
        LevelKit.Zone(root, "Chegada", new Vector3(0f, 12f, 150f), new Vector3(22f, 22f, 16f), 32f, 16f);
    }


    // ------------------------------------------------------------------
    // Sistemas
    // ------------------------------------------------------------------







    // ------------------------------------------------------------------
    // Montagem comum
    // ------------------------------------------------------------------

    static GameObject Place(Transform parent, string color, string piece, Vector3 position,
                            float rotationY = 0f) =>
        KayKitPlacer.Place(parent, color, piece, position, rotationY);

    /// <summary>Prensa já amarrada às alturas desta pista.</summary>
    static void Prensa(Transform root, float x, float z, float phase) =>
        LevelKit.Crusher(root, x, z, kCrusherTop, kRunwayTop, phase);

    /// <summary>Hélice no piso desta pista.</summary>
    static void Helice(Transform root, float z, float degreesPerSecond, float phase) =>
        LevelKit.Blade(root, new Vector3(0f, kRunwayTop, z), degreesPerSecond, phase);







}
