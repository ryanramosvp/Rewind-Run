using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fase 2: Torre de Bolhas.
///
/// A primeira fase corre no plano; esta sobe. O que permite a diferença é a
/// <see cref="AirBubble"/>: o pulo duplo sozinho dá 6,1 m e acaba ali, então
/// sem bolha qualquer trecho vertical vira uma escada de plataformas. Com ela,
/// a altura deixa de ter teto e passa a depender de quantas se encadeia.
///
/// Por isso dois dos quatro trechos não têm chão nenhum. Não é enfeite: é o
/// único jeito de a bolha importar. Com piso embaixo ela seria um atalho
/// opcional; sem piso, ela é o caminho.
///
/// Os números vêm da simulação do arco de salto. Quicando 4,5 m a bruxa sobe
/// 4,53 m e fica 1,18 s no ar, o que dá 13 m correndo. Daí os passos: 3 m para
/// cima entre bolhas (1,5 m de margem até o pico) e 8 m na horizontal, com os
/// vãos de 11 m guardados para o trecho que cobra velocidade.
/// </summary>
public static class BubbleLevelBuilder
{
    const string kScenePath = "Assets/Scenes/Level_TorreDeBolhas.unity";

    // Limites de S, A, B, C e D, em segundos. Acima de D é F.
    // Mesma regra da Corrida de Lava: o S fica logo acima do melhor tempo
    // gravado no save (17,28 s) e os degraus abrem na mesma proporção.
    static readonly float[] kRanks = { 18f, 22f, 27f, 35f, 49f };

    [MenuItem("Rewind Run/Criar Fase 2: Torre de Bolhas", priority = 8)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        LevelKit.CreateLighting();

        var level = new GameObject("Fase").transform;
        CreateStart(level);
        CreateStaircase(level);
        CreateCrossing(level);
        CreateColumn(level);
        CreateFinish(level);

        var zones = new GameObject("ZonasDeCamera").transform;
        CreateCameraZones(zones);

        LevelKit.AssembleSystems(kRanks, "Level_LavaLetal");
        LevelKit.CreateMusic(LevelKit.LightLevelMusic);

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 65f), 70f, 170f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);
        LevelKit.AddSceneToBuildSettings(kScenePath);

        Debug.Log($"Fase 2 criada em {kScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s.");
    }

    // ------------------------------------------------------------------
    // Traçado
    // ------------------------------------------------------------------

    /// <summary>
    /// Largada: chão largo e uma bolha só.
    ///
    /// A primeira bolha fica baixa e com plataforma dos dois lados. Errar aqui
    /// custa um pulo, não uma vida - e é o único lugar da fase onde isso vale,
    /// porque é onde se descobre que a bolha some depois de usada.
    /// </summary>
    static void CreateStart(Transform root)
    {
        for (float z = 3f; z <= 9f; z += 6f)
        {
            Place(root, "green", "platform_6x6x1", new Vector3(-3f, 5f, z));
            Place(root, "green", "platform_6x6x1", new Vector3(3f, 5f, z));
        }

        Place(root, "green", "flag_A", new Vector3(0f, 6f, 1f));
        Place(root, "neutral", "signage_arrows_right", new Vector3(5.6f, 6f, 5f), 180f);

        Bubble(root, new Vector3(0f, 7.5f, 20f), 0f);

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 9f, 28f));
    }

    /// <summary>
    /// Escada de bolhas, sem chão: sobe de 10 m para 23 m em cinco quiques.
    ///
    /// Zigue-zagueia em X de propósito. Em linha reta o jogador só precisaria
    /// segurar para frente; alternando os lados, cada quique exige mirar o
    /// próximo enquanto ainda está subindo do anterior.
    ///
    /// O desvio é de 2,5 m, e não mais. Com 4 m o salto entre as duas bolhas
    /// opostas somava 8 m de lado aos 8 m para frente, e os 11,3 m de diagonal
    /// pediam 11,5 m/s - acima dos 11 m/s que correr entrega. Ficaria um passo
    /// obrigatório que só o bunny hop venceria, num trecho onde não há chão
    /// para encadear hop nenhum.
    /// </summary>
    static void CreateStaircase(Transform root)
    {
        Bubble(root, new Vector3(0f, 11.5f, 36f), 0.15f);
        Bubble(root, new Vector3(-2.5f, 14.5f, 44f), 0.3f);
        Bubble(root, new Vector3(2.5f, 17.5f, 51f), 0.45f);
        Bubble(root, new Vector3(0f, 20.5f, 59f), 0.6f);

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 22f, 67f));
    }

    /// <summary>
    /// Travessia: três bolhas na mesma altura, com 11 m entre elas.
    ///
    /// É o trecho que cobra velocidade. O quique dá 1,18 s no ar, que a 11 m/s
    /// vale 13 m - então 11 m passa correndo e não passa andando. Quem chega
    /// sem embalo descobre no ar, o que é tarde: daí o rewind existir.
    /// </summary>
    static void CreateCrossing(Transform root)
    {
        Bubble(root, new Vector3(0f, 24f, 78f), 0f);
        Bubble(root, new Vector3(0f, 24f, 89f), 0.33f);
        Bubble(root, new Vector3(0f, 24f, 100f), 0.66f);

        Place(root, "green", "platform_6x6x1", new Vector3(0f, 25f, 110f));
    }

    /// <summary>
    /// Coluna final: sobe 9 m quase parado no lugar.
    ///
    /// As bolhas giram entre TRÊS pontos, não dois. Com dois, as bolhas de
    /// mesma vertical ficavam 6 m uma da outra - e como o quique sobe 4,53 m e
    /// o raio de captura alcança 2,02 m, a subida encostava na bolha dois
    /// degraus acima e pulava a do meio. Ou seja: o trecho se resolvia sozinho
    /// subindo em linha reta, que é o contrário do que ele existe para pedir.
    ///
    /// Com três pontos, a bolha seguinte na mesma vertical fica 9 m acima, bem
    /// fora do alcance do pico. Cada degrau volta a ser uma escolha de direção.
    /// </summary>
    static void CreateColumn(Transform root)
    {
        Bubble(root, new Vector3(-2.5f, 28f, 121.5f), 0f);
        Bubble(root, new Vector3(2.5f, 31f, 121.5f), 0.25f);
        Bubble(root, new Vector3(0f, 34f, 125f), 0.5f);
        Bubble(root, new Vector3(-2.5f, 37f, 121.5f), 0.75f);
    }

    static void CreateFinish(Transform root)
    {
        Place(root, "green", "platform_6x6x1", new Vector3(0f, 39f, 128f));
        Place(root, "green", "flag_A", new Vector3(0f, 40f, 128f));
        Place(root, "neutral", "signage_finish", new Vector3(0f, 40f, 125.5f));

        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 41f, 128f);

        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(5f, 3f, 5f);
        area.isTrigger = true;

        go.AddComponent<LevelFinish>();
    }

    /// <summary>
    /// Câmeras altas, para o olho medir a distância do salto.
    ///
    /// A primeira versão fazia o contrário: ângulos de 10 a 22 graus, com a
    /// ideia de que olhar de cima esconderia a bolha seguinte. Um teste com
    /// jogadores derrubou isso - ninguém conseguia julgar o tamanho do pulo.
    ///
    /// A conta explica. Com a câmera quase na horizontal, duas bolhas a 8 e a
    /// 11 m à frente ficam a 0,64 grau uma da outra na tela: a travessia lia
    /// como uma pilha, não como três saltos longos. A 40 graus essa diferença
    /// passa de 2,6 graus, e aparece a lava embaixo de cada vão, que é o que de
    /// fato comunica o tamanho do salto.
    ///
    /// E o receio original não se sustentava. A 24 m de distância, mesmo a 40
    /// graus a câmera ainda mostra 12,5 m acima da bruxa - quase o mesmo que a
    /// 10 graus. A bolha seguinte da coluna fica a 3 m.
    ///
    /// Os ângulos variam por trecho: o mais alto onde o salto é horizontal
    /// (travessia), um pouco menos onde ele também sobe (coluna), para a
    /// chegada continuar inteira no quadro.
    /// </summary>
    static void CreateCameraZones(Transform root)
    {
        Zone(root, "Largada", new Vector3(0f, 12f, 14f), new Vector3(40f, 32f, 34f), 32f, 21f);
        Zone(root, "Escada", new Vector3(0f, 18f, 50f), new Vector3(40f, 40f, 40f), 36f, 24f);
        Zone(root, "Travessia", new Vector3(0f, 26f, 90f), new Vector3(40f, 36f, 44f), 40f, 24f);
        Zone(root, "Coluna", new Vector3(0f, 34f, 122f), new Vector3(34f, 40f, 32f), 30f, 24f);
    }

    // ------------------------------------------------------------------
    // Montagem
    // ------------------------------------------------------------------

    static void Bubble(Transform root, Vector3 position, float bobPhase) =>
        LevelKit.Bubble(root, position, bobPhase);

    static GameObject Place(Transform parent, string color, string piece, Vector3 position,
                            float rotationY = 0f) =>
        KayKitPlacer.Place(parent, color, piece, position, rotationY);

    static void Zone(Transform root, string name, Vector3 center, Vector3 size,
                     float pitch, float distance) =>
        LevelKit.Zone(root, name, center, size, pitch, distance);
}
