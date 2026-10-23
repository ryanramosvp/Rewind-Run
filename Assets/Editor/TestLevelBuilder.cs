using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Bancada de testes: todas as mecânicas numa cena só, lado a lado.
///
/// Não é uma fase. É o lugar de conferir uma mecânica sem atravessar um nível
/// inteiro para chegar nela - as estações ficam a 14 m uma da outra, em linha
/// reta, então voltar a qualquer uma custa alguns segundos.
///
/// Por isso também não entra no build. Ela existe para o desenvolvimento, e
/// uma cena de testes dentro do jogo entregue é sujeira.
///
/// O chão fica a 9 m de altura com lava embaixo, igual às fases: cair tem que
/// doer aqui também, senão a bancada mente sobre como o jogo se comporta.
/// </summary>
public static class TestLevelBuilder
{
    const string kScenePath = "Assets/Scenes/Level_Test.unity";
    const float kFloor = 8f;        // pivô das peças
    const float kTop = 9f;          // superfície onde se anda

    [MenuItem("Rewind Run/Criar Bancada de Testes", priority = 0)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        LevelKit.CreateLighting();

        var level = new GameObject("Bancada").transform;
        CreateStart(level);
        CreateSlopes(level);
        CreateWallShaft(level);
        CreateMovingGap(level);
        CreateCrusher(level);
        CreateBlade(level);
        CreateBubbles(level);
        CreateFinish(level);

        var zones = new GameObject("ZonasDeCamera").transform;
        LevelKit.Zone(zones, "Bancada", new Vector3(0f, 16f, 50f), new Vector3(50f, 48f, 130f),
                      22f, 22f);

        LevelKit.AssembleSystems(new[] { 40f, 60f, 90f, 120f, 150f });

        HazardSetup.EnsureLava(new Vector3(0f, 0f, 50f), 60f, 150f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, kScenePath);

        Debug.Log("Bancada criada em " + kScenePath + ". Estações a cada 14 m: " +
                  "hop, rampa, parede, plataforma móvel, prensa, hélice, bolhas. " +
                  "Não entra no build de propósito.");
    }

    // ------------------------------------------------------------------

    /// <summary>Estação 1: pista limpa com dois obstáculos, para sentir o hop.</summary>
    static void CreateStart(Transform root)
    {
        Floor(root, 0f, 12f);

        foreach (float z in new[] { 5f, 10f })
        {
            for (int i = -1; i <= 1; i++)
                LevelKit.Place(root, "red", "barrier_4x1x1", new Vector3(i * 4f, kTop, z));
        }

        Marker(root, "green", 0f, -2f);
    }

    /// <summary>Estação 2: rampa de descida e de subida, para o slide automático.</summary>
    static void CreateSlopes(Transform root)
    {
        LevelKit.Place(root, "yellow", "platform_slope_6x6x4", new Vector3(0f, kFloor - 3f, 18f));
        Floor(root, 22f, 26f, kFloor - 4f);
        LevelKit.Place(root, "yellow", "platform_slope_6x6x4", new Vector3(0f, kFloor - 3f, 30f), 180f);

        Marker(root, "yellow", 0f, 16f);
    }

    /// <summary>Estação 3: poço de paredes opostas.</summary>
    static void CreateWallShaft(Transform root)
    {
        Floor(root, 34f, 38f);

        foreach (float height in new[] { 1f, 5f })
        {
            LevelKit.Place(root, "blue", "platform_6x2x4", new Vector3(-2f, kTop + height, 36f), 90f);
            LevelKit.Place(root, "blue", "platform_6x2x4", new Vector3(2f, kTop + height, 36f), 90f);
        }

        Marker(root, "blue", 0f, 33f);
    }

    /// <summary>Estação 4: vão de 10 m com uma plataforma indo e vindo.</summary>
    static void CreateMovingGap(Transform root)
    {
        Floor(root, 42f, 44f);

        LevelKit.Platform(root, "yellow", "platform_4x4x1", new Vector3(0f, kFloor, 48f),
                          new Vector3(8f, 0f, 0f), travelSeconds: 2.5f, waitSeconds: 0.8f);

        Floor(root, 54f, 56f);
        Marker(root, "yellow", 0f, 41f);
    }

    /// <summary>Estação 5: uma prensa, com espaço de sobra para errar.</summary>
    static void CreateCrusher(Transform root)
    {
        Floor(root, 60f, 68f);

        LevelKit.Crusher(root, 0f, 64f, kTop + 8f, kTop, 0f);
        Marker(root, "red", 0f, 59f);
    }

    /// <summary>Estação 6: uma hélice no corredor estreito.</summary>
    static void CreateBlade(Transform root)
    {
        for (float z = 72f; z <= 80f; z += 6f)
            LevelKit.Place(root, "green", "platform_6x6x1", new Vector3(0f, kFloor, z));

        LevelKit.Blade(root, new Vector3(0f, kTop, 76f), 70f, 0f);
        Marker(root, "red", 0f, 70f);
    }

    /// <summary>Estação 7: três bolhas subindo, sem chão embaixo.</summary>
    static void CreateBubbles(Transform root)
    {
        Floor(root, 86f, 88f);

        LevelKit.Bubble(root, new Vector3(0f, kTop + 1.5f, 96f), 0f);
        LevelKit.Bubble(root, new Vector3(-2.5f, kTop + 4.5f, 104f), 0.33f);
        LevelKit.Bubble(root, new Vector3(2.5f, kTop + 7.5f, 112f), 0.66f);

        Marker(root, "blue", 0f, 85f);
    }

    static void CreateFinish(Transform root)
    {
        LevelKit.Place(root, "green", "platform_6x6x1", new Vector3(0f, kTop + 9f, 120f));
        LevelKit.Place(root, "green", "flag_A", new Vector3(0f, kTop + 10f, 120f));
        LevelKit.Place(root, "neutral", "signage_finish", new Vector3(0f, kTop + 10f, 117.5f));

        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, kTop + 11f, 120f);

        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(5f, 3f, 5f);
        area.isTrigger = true;

        go.AddComponent<LevelFinish>();
    }

    // ------------------------------------------------------------------

    /// <summary>Faixa de chão de 12 m de largura, de z inicial a z final.</summary>
    static void Floor(Transform root, float from, float to, float y = kFloor)
    {
        for (float z = from; z <= to; z += 6f)
        {
            LevelKit.Place(root, "green", "platform_6x6x1", new Vector3(-3f, y, z));
            LevelKit.Place(root, "green", "platform_6x6x1", new Vector3(3f, y, z));
        }
    }

    /// <summary>Bandeira na entrada de cada estação, para achar onde se está.</summary>
    static void Marker(Transform root, string color, float x, float z) =>
        LevelKit.Place(root, color, "flag_C", new Vector3(x, kTop, z));
}
