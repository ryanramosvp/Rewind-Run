using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static VolcanicKit;

/// <summary>
/// Fase 6, a última: Coração do Tempo - a cratera do vulcão, com uma
/// ampulheta gigante no meio e a areia subindo dentro dela.
///
/// É a fase que junta tudo. Começa fora da cratera, numa rampa de deslize
/// com bolas cruzando; cai num patamar sobre a lava e atravessa por bolhas
/// até a base da ampulheta. Dali sobe em espiral em volta dela:
///  - um poço de vidro para subir pulando de parede em parede (com chão
///    firme embaixo - errar custa segundos);
///  - cristais frágeis que quebram logo depois de pisados;
///  - um cristal de contratempo que sobe ao lado de um degrau alto;
///  - a perseguição final: uma bola que sobe a rampa em hélice atrás da
///    bruxa, freada no meio por uma zona de âmbar, e uma última fileira de
///    cristais frágeis antes do cume.
///
/// No cume não há bandeira: tocar o altar começa o encerramento da campanha
/// (<see cref="FimDaCampanha"/>) - a câmera se afasta, a ampulheta vira, o
/// coro entra, a tela clareia e vêm os resultados.
///
/// A câmera olha de fora para dentro da ampulheta (<see cref="RadialCameraFraming"/>),
/// como no tutorial: na espiral, a bruxa fica sempre entre a câmera e a torre.
/// </summary>
public static class CoracaoLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_CoracaoDoTempo.unity";
    public const string SceneName = "Level_CoracaoDoTempo";
    public const string Musica = "Musica/sinfonia4_tchaikovsky.mp3";
    public const string MusicaFinal = "Musica/amen_scarlatti.mp3";

    // Provisórios: rampa, travessia e uma volta e meia de espiral.
    static readonly float[] kRanks = { 55f, 68f, 85f, 110f, 150f };

    /// <summary>Centro da ampulheta, no nível da lava.</summary>
    static readonly Vector3 O = new Vector3(0f, 0f, 40f);
    const float R = 20f;               // raio da espiral
    const float kMeioDaAmpulheta = 24f;
    const float kMeiaAltura = 20f;

    const float kTopoDaLargada = 34f;
    const float kQuedaDaRampa = 22f;

    [MenuItem("Rewind Run/Criar Fase 6: Coração do Tempo", priority = 12)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();
        BeginLevel();
        TempoKit.Ensure();
        CriarAtmosfera();

        var level = new GameObject("Fase").transform;
        CriarRampa(Group(level, "1_Rampa"));
        CriarTravessia(Group(level, "2_Travessia"));
        CriarPoco(Group(level, "3_PocoDeVidro"));
        CriarCristais(Group(level, "4_Cristais"));
        CriarPerseguicao(Group(level, "5_Perseguicao"));
        var ampulheta = CriarAmpulheta(Group(level, "Ampulheta"));
        CriarCratera(Group(level, "Cratera"));

        var player = LevelKit.AssembleSystems(kRanks, ResultsSceneBuilder.SceneName, "ver resultados");
        TempoKit.LiberarImpulso(player);
        LevelKit.CreateMusic(Musica);
        var largada = new Vector3(0f, kTopoDaLargada + 0.3f, -104f);
        if (player != null) player.transform.SetPositionAndRotation(largada, Quaternion.identity);

        CriarCume(Group(level, "6_Cume"), ampulheta);
        CriarCamera(player, largada);

        HazardSetup.EnsureLava(new Vector3(0f, 0f, -10f), 360f, 380f, surfaceHeight: 0f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Validar();
        Debug.Log($"Fase 6 criada em {ScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s (provisórios).");
    }

    // ------------------------------------------------------------------
    // Espiral: posições em volta da ampulheta
    // ------------------------------------------------------------------

    /// <summary>Ponto no ângulo <paramref name="graus"/> (0 ao sul, crescendo para o leste).</summary>
    static Vector3 P(float graus, float raio, float y)
    {
        float a = graus * Mathf.Deg2Rad;
        return new Vector3(O.x + raio * Mathf.Sin(a), y, O.z - raio * Mathf.Cos(a));
    }

    /// <summary>Giro que põe o Z local na tangente da espiral e o X local para fora.</summary>
    static float Rumo(float graus) => 90f - graus;

    /// <summary>Placa na espiral: <paramref name="radial"/> de largura, <paramref name="tangente"/> de comprimento.</summary>
    static GameObject Placa(Transform root, string name, float graus, float y, float radial, float tangente)
    {
        var p = P(graus, R, y);
        return Plate(root, name, p.x, p.z, y, radial, tangente, Rumo(graus), TempoKit.Bronze, trim: false, supports: false);
    }

    // ------------------------------------------------------------------
    // Trechos
    // ------------------------------------------------------------------

    /// <summary>
    /// Largada fora da cratera e a rampa até a beira: duas bolas cruzam a
    /// pista de lado, num ritmo, e o deslize só deixa desviar ou pular.
    /// Elas vêm de lado, e não de trás, para serem vistas a tempo.
    /// </summary>
    static void CriarRampa(Transform root)
    {
        TempoKit.Placa(root, "Largada", new Vector3(0f, kTopoDaLargada, -102f), 12f, 10f);
        Ramp(root, "Rampa", new Vector3(0f, kTopoDaLargada, -97f), 10f, kQuedaDaRampa, 0f);

        float entrada = kRampEntry * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad);
        float AlturaNa(float s) => kTopoDaLargada - (s < kRampEntry ? s * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad)
                                                         : entrada + (s - kRampEntry) * Mathf.Tan(kRampAngle * Mathf.Deg2Rad));

        foreach (var (s, fase) in new[] { (11f, 0f), (23f, 0.5f) })
        {
            float y = AlturaNa(s) + 1.5f;
            float z = -97f + s;
            var caminho = new List<Vector3> { new Vector3(-10f, y, z), new Vector3(10f, y, z), new Vector3(13f, -3f, z) };
            var bola = RampLevelBuilder.Boulder(root, $"Bola_Cruzada{(fase > 0f ? 2 : 1)}", caminho, 1.5f, 13f, 0f, 13f,
                                                true, 2.8f, fase, default);
            var relogio = bola.AddComponent<RelogioLocal>();
            TempoKit.SetEnum(relogio, "canal", (int)CanalTemporal.Bolas);

            // Bocas de pedra de onde a bola sai e para onde ela cai.
            Decor(root, "Boca", LowPolyMesh.Prism(LowPolyMesh.Regular(6, 2.6f), kBedrock - y, 1.2f, 3f, 0.8f),
                  LavaLetalArt.RockBurnt, new Vector3(-11.5f, y - 1.5f, z));
        }

        Plate(root, "Pouso", 0f, -51f, 9f, 12f, 18f, 0f, LavaLetalArt.Metal, trim: true, supports: true);
        TempoKit.PontoDeControle(root, "PontoDeControle1", new Vector3(0f, 9f, -46f), 0f);

        var zona = KayKitPlacer.CreateCameraZone(root, "Zona_Rampa",
                                                 new Bounds(new Vector3(0f, 26f, -82f), new Vector3(16f, 24f, 46f)),
                                                 0f, 48f, 20f);
        zona.name = "Zona_Rampa";
    }

    /// <summary>
    /// Três bolhas sobre a lava até a base da ampulheta. Na segunda ilha um
    /// pêndulo atravessa o pouso, dentro de uma zona que o deixa lento.
    /// </summary>
    static void CriarTravessia(Transform root)
    {
        Bubble(root, "Bolha1", new Vector3(0f, 12f, -36.5f), 0f);
        Rock(root, "Ilha1", 0f, -23.5f, 9f, 2.6f, 9, 61, LavaLetalArt.RockBrown);
        Bubble(root, "Bolha2", new Vector3(0f, 12f, -15.5f), 0.3f);
        Rock(root, "Ilha2", 0f, -3f, 9f, 3.6f, 10, 62, LavaLetalArt.RockBrown);
        TempoKit.Pendulo(root, "Pendulo_Ilha2", new Vector3(0f, 9f + 8.5f, -3f), 7.3f, 55f, 2.4f, 0.25f);
        TempoKit.Zona(root, "Zona_Ilha2", new Vector3(0f, 13f, -3f), 6f, 0.3f, CanalTemporal.Obstaculos);
        Bubble(root, "Bolha3", new Vector3(0f, 12f, 6f), 0.6f);

        Placa(root, "Base", 0f, 9f, 8f, 10f);
        TempoKit.PontoDeControle(root, "PontoDeControle2", P(0f, R, 9f) + Vector3.right * 2.5f, Rumo(0f));
    }

    /// <summary>
    /// Poço de vidro: duas paredes paralelas a 4,5 m, dez metros de altura,
    /// chão firme embaixo. Sobe-se pulando de uma para a outra; no alto, um
    /// patamar logo adiante.
    /// </summary>
    static void CriarPoco(Transform root)
    {
        const float graus = 38f, chao = 9f, topo = 19.5f;
        Placa(root, "ChaoDoPoco", graus, chao, 4.5f, 6f);

        TempoKit.Aviso(root, "Aviso_Poco", new Bounds(P(graus, R, chao + 2f), new Vector3(7f, 4f, 7f)),
                       "Entre duas paredes: pule de uma para a outra para subir.", null, 5f);

        foreach (float raio in new[] { R - 2.5f, R + 2.5f })
        {
            var p = P(graus, raio, chao);
            var parede = Solid(root, "ParedeDeVidro", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.5f, 6f), 0f, topo - chao, 2f),
                               TempoKit.Vidro, TempoKit.Vidro, p, Rumo(graus));
            parede.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var box = parede.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, (topo - chao) * 0.5f, 0f);
            box.size = new Vector3(0.5f, topo - chao, 6f);

            // Moldura de bronze: o vidro sozinho quase não se vê.
            foreach (float lado in new[] { -3f, 3f })
                Decor(parede.transform, "Coluna", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.6f, 0.4f), 0f, topo - chao + 0.3f, 1f),
                      TempoKit.Bronze, new Vector3(0f, 0f, lado));
            Decor(parede.transform, "Travessa", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.6f, 6.4f), topo - chao, topo - chao + 0.3f, 1f),
                  TempoKit.Bronze, Vector3.zero);
        }

        Placa(root, "TopoDoPoco", 56f, 18f, 5f, 5f);
        TempoKit.PontoDeControle(root, "PontoDeControle3", P(58f, R, 18f), Rumo(58f));
    }

    /// <summary>
    /// Quatro cristais frágeis subindo a espiral e, num degrau, um cristal de
    /// contratempo que sobe três metros até a varanda.
    /// </summary>
    static void CriarCristais(Transform root)
    {
        TempoKit.Aviso(root, "Aviso_Fragil", new Bounds(P(60f, R, 19.5f), new Vector3(6f, 4f, 6f)),
                       "Cristais rosa quebram logo depois de pisados. Não pare - ou volte no tempo.", null, 5f);

        float[] graus = { 72f, 87f, 102f, 117f };
        for (int i = 0; i < graus.Length; i++)
        {
            var p = P(graus[i], R, 19f + i);
            TempoKit.Fragil(root, $"Fragil{i + 1}", p, 3f, 3f, Rumo(graus[i]));
        }

        Placa(root, "Degrau", 136f, 22f, 5f, 5f);
        TempoKit.Contratempo(root, "Cristal1", P(148f, R, 22f), 3f, 3f, new Vector3(0f, 3f, 0f), 1.2f, Rumo(148f));

        TempoKit.Aviso(root, "Aviso_Cristal", new Bounds(P(136f, R, 23.5f), new Vector3(6f, 4f, 6f)),
                       "Alto demais? Volte no tempo e o cristal azul sobe.", null, 4f);

        Placa(root, "Varanda", 165f, 28f, 6f, 6f);
        TempoKit.PontoDeControle(root, "PontoDeControle4", P(162f, R, 28f), Rumo(162f));
    }

    const float kHeliceInicio = 174f, kHeliceFim = 290f, kHeliceY0 = 28f, kHeliceY1 = 41f;

    /// <summary>
    /// A perseguição: rampa em hélice de 40 m subindo 13, uma bola rolando
    /// atrás da bruxa e uma zona de âmbar no meio que a freia. No fim, três
    /// cristais frágeis sobre o vazio; a bola segue reto e cai.
    /// </summary>
    static void CriarPerseguicao(Transform root)
    {
        var malha = TempoKit.MalhaHelice(O, R, 5f, kHeliceInicio, kHeliceFim, kHeliceY0, kHeliceY1, 0.8f, "Helice");
        var helice = Solid(root, "RampaEmHelice", malha, TempoKit.Bronze, TempoKit.BronzeEscuro, Vector3.zero, 0f);
        helice.AddComponent<MeshCollider>().sharedMesh = malha;

        // Para a validação: o começo e o fim da rampa como placas.
        RegistrarTrecho("HeliceInicio", kHeliceInicio, kHeliceInicio + 6f, kHeliceY0);
        RegistrarTrecho("HeliceFim", kHeliceFim - 6f, kHeliceFim, kHeliceY1);

        float meio = (kHeliceInicio + kHeliceFim) * 0.5f;
        TempoKit.Zona(root, "Zona_Perseguicao", P(meio, R, (kHeliceY0 + kHeliceY1) * 0.5f + 1.5f), 6.5f, 0.35f,
                      CanalTemporal.Bolas);

        const float r = 1.6f;
        var caminho = new List<Vector3> { P(160f, 26f, 31f), P(168f, 23f, kHeliceY0 + r) };
        for (float g = kHeliceInicio; g <= kHeliceFim + 0.01f; g += 6f)
            caminho.Add(P(g, R, Mathf.Lerp(kHeliceY0, kHeliceY1, (g - kHeliceInicio) / (kHeliceFim - kHeliceInicio)) + r));
        float a = kHeliceFim * Mathf.Deg2Rad;
        var tangente = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        var fim = P(kHeliceFim, R, kHeliceY1 + r);
        caminho.Add(fim + tangente * 5f);
        caminho.Add(fim + tangente * 9f + Vector3.down * 18f);

        var gatilho = new Bounds(P(kHeliceInicio + 5f, R, kHeliceY0 + 2f), new Vector3(8f, 6f, 8f));
        var bola = RampLevelBuilder.Boulder(root, "Bola_Perseguicao", caminho, r, 6f, 2.5f, 10.5f, false, 0f, 0f, gatilho);
        var relogio = bola.AddComponent<RelogioLocal>();
        TempoKit.SetEnum(relogio, "canal", (int)CanalTemporal.Bolas);
        Decor(root, "Poleiro", LowPolyMesh.Prism(LowPolyMesh.Regular(6, 1.8f), -8f, 0f, 3f, 0.7f),
              LavaLetalArt.RockBurnt, P(160f, 26f, 31f - r));

        float[] graus = { 300f, 313f, 326f };
        for (int i = 0; i < graus.Length; i++)
        {
            var p = P(graus[i], R, kHeliceY1);
            TempoKit.Fragil(root, $"FragilFinal{i + 1}", p, 3f, 3f, Rumo(graus[i]));
        }
    }

    static void RegistrarTrecho(string name, float g0, float g1, float y)
    {
        Register(name, y, new[]
        {
            Xz(P(g0, R - 2.5f, y)), Xz(P(g0, R + 2.5f, y)), Xz(P(g1, R + 2.5f, y)), Xz(P(g1, R - 2.5f, y)),
        });
    }

    static Vector2 Xz(Vector3 v) => new Vector2(v.x, v.z);

    /// <summary>
    /// O cume e o encerramento: um altar com uma ampulheta pequena; tocá-lo
    /// dispara o <see cref="FimDaCampanha"/>, que vira a ampulheta gigante.
    /// </summary>
    static void CriarCume(Transform root, Transform ampulheta)
    {
        const float graus = 345f, y = 42f;
        Placa(root, "Cume", graus, y, 8f, 8f);
        var altar = P(graus, R, y);
        Decor(root, "Altar", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 1.3f, 22.5f), 0f, 1f, 1f, 0.8f), TempoKit.BronzeEscuro, altar);
        var pequena = new GameObject("AmpulhetaDoAltar");
        pequena.transform.SetParent(root, false);
        pequena.transform.localPosition = altar + Vector3.up * 2.1f;
        TempoKit.MontarAmpulheta(pequena.transform, 0.9f);

        // Clarão branco por cima de tudo, transparente até o fim da sequência.
        var canvas = UiBuilder.CreateCanvas("Clarao", sortOrder: 60);
        var imagem = new GameObject("Branco").AddComponent<Image>();
        imagem.transform.SetParent(canvas.transform, false);
        var rect = imagem.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        imagem.color = new Color(1f, 1f, 1f, 0f);
        imagem.raycastTarget = false;

        var go = new GameObject("FimDaCampanha");
        go.transform.SetParent(root, false);
        go.transform.position = altar + Vector3.up * 1.5f;
        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(5f, 4f, 5f);

        var fimDaCampanha = go.AddComponent<FimDaCampanha>();
        EditorSetupUtil.SetReference(fimDaCampanha, "ampulheta", ampulheta);
        EditorSetupUtil.SetReference(fimDaCampanha, "foco", ampulheta);
        EditorSetupUtil.SetReference(fimDaCampanha, "musicaFinal", LevelKit.MusicClip(MusicaFinal));
        EditorSetupUtil.SetReference(fimDaCampanha, "clarao", imagem);
        EditorSetupUtil.SetReference(fimDaCampanha, "volumeRewind", Object.FindAnyObjectByType<Volume>());
        EditorSetupUtil.SetString(fimDaCampanha, "cenaDeResultados", ResultsSceneBuilder.SceneName);
        LevelKit.SetFloat(fimDaCampanha, "subida", 30f);
        LevelKit.SetFloat(fimDaCampanha, "afastamento", 70f);
    }

    /// <summary>
    /// A ampulheta gigante: o pivô fica no meio dela, que é onde ela gira no
    /// fim. A areia sobe - é o tempo andando para trás - em partículas que
    /// vivem no espaço da própria ampulheta e viram junto com ela.
    /// </summary>
    static Transform CriarAmpulheta(Transform root)
    {
        Decor(root, "Pedestal", LowPolyMesh.Prism(LowPolyMesh.Regular(12, 15f), kBedrock, kMeioDaAmpulheta - kMeiaAltura * 1.1f,
                                                 LavaLetalArt.RockTile, 0.85f), LavaLetalArt.RockBurnt, O);

        var pivo = new GameObject("AmpulhetaGigante").transform;
        pivo.SetParent(root, false);
        pivo.localPosition = O + Vector3.up * kMeioDaAmpulheta;
        TempoKit.MontarAmpulheta(pivo, kMeiaAltura);

        var areia = new GameObject("AreiaSubindo");
        areia.transform.SetParent(pivo, false);
        areia.transform.localPosition = Vector3.down * kMeiaAltura * 0.55f;
        areia.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var ps = areia.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 3.2f;
        main.startSpeed = 6.5f;
        main.startSize = 0.45f;
        main.startColor = new Color(1f, 0.82f, 0.45f, 0.9f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        var emissao = ps.emission;
        emissao.rateOverTime = 70f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 4f;
        forma.radius = 3f;
        var renderer = areia.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = TempoKit.Particula;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        return pivo;
    }

    /// <summary>
    /// A borda da cratera: rochas altas em anel, abertas no sul, onde a rampa
    /// entra. Longe da rota e sem colisão no caminho de ninguém.
    /// </summary>
    static void CriarCratera(Transform root)
    {
        var random = new System.Random(6060);
        for (int i = 0; i < 30; i++)
        {
            float graus = i * 12f + (float)random.NextDouble() * 6f;
            if (Mathf.Abs(Mathf.DeltaAngle(graus, 0f)) < 16f) continue;
            float raio = 108f + (float)random.NextDouble() * 14f;
            var p = P(graus, raio, 0f);
            float topo = 22f + (float)random.NextDouble() * 26f;
            Rock(root, $"Borda{i}", p.x, p.z, topo, 9f + (float)random.NextDouble() * 6f, 8, 700 + i,
                 LavaLetalArt.RockBurnt, route: false);
        }
    }

    // ------------------------------------------------------------------
    // Ambiente e câmera
    // ------------------------------------------------------------------

    static void CriarAtmosfera()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 90f, 0f), Quaternion.Euler(40f, -35f, 0f));
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.78f, 0.62f);
        light.shadows = LightShadows.Soft;

        LavaLetalArt.ApplyAtmosphere();
    }

    /// <summary>
    /// Fora da cratera, a câmera fixa para +Z e uma zona mais alta sobre a
    /// rampa; perto da ampulheta, a câmera radial olhando para o centro.
    /// </summary>
    static void CriarCamera(GameObject player, Vector3 largada)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;

        const float pitch = 30f, distancia = 19f;
        LevelKit.SetFloat(camera, "fixedYaw", 0f);
        LevelKit.SetFloat(camera, "fixedPitch", pitch);
        LevelKit.SetFloat(camera, "fixedDistance", distancia);
        var rotation = Quaternion.Euler(pitch, 0f, 0f);
        camera.transform.SetPositionAndRotation(largada + Vector3.up * 1.4f - rotation * Vector3.forward * distancia, rotation);

        var radial = new GameObject("CameraRadial").AddComponent<RadialCameraFraming>();
        EditorSetupUtil.SetReference(radial, "cameraRig", camera);
        if (player != null) EditorSetupUtil.SetReference(radial, "player", player.transform);
        LevelKit.SetVector(radial, "center", O);
        var so = new SerializedObject(radial);
        so.FindProperty("sectors").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();
        LevelKit.SetFloat(radial, "pitch", pitch);
        LevelKit.SetFloat(radial, "distance", 21f);
        LevelKit.SetFloat(radial, "raioMaximo", 30f);
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    static void Validar()
    {
        var report = JumpReport.Begin("Coração do Tempo");
        if (report == null) return;
        var jump = report.Jump;

        report.Route("Largada", "Largada");
        report.Section("Saída da rampa (15 m/s)");
        report.Expect("Rampa -> Pouso deslizando", true, FromRamp(jump, "Rampa", "Pouso", 15f, new string[0], 0f, false));
        report.Expect("Rampa -> Pouso pulando", true, FromRamp(jump, "Rampa", "Pouso", 15f, new string[0], 0f, true));

        report.Section("Travessia por bolhas");
        report.Expect("Pouso -> Ilha1 pela bolha", true, FromIsland(jump, "Pouso", "Ilha1", jump.Speed, new[] { "Bolha1" }, 0f));
        report.Expect("Ilha1 -> Ilha2 pela bolha", true, FromIsland(jump, "Ilha1", "Ilha2", jump.Speed, new[] { "Bolha2" }, 0f));
        report.Expect("Ilha2 -> Base pela bolha", true, FromIsland(jump, "Ilha2", "Base", jump.Speed, new[] { "Bolha3" }, 0f));
        report.Expect("Pouso -> Ilha1 sem a bolha", false, FromIsland(jump, "Pouso", "Ilha1", jump.Speed, new string[0], 0f));

        report.Route("Espiral", "Base", "ChaoDoPoco");
        // O Reach do validador não diz quando o desnível passa da altura do
        // pulo; aqui a conta é direta: o duplo sobe no máximo pulo + duplo.
        float poco = Gap(Footprints["ChaoDoPoco"].poly, Footprints["TopoDoPoco"].poly);
        float subida = Footprints["TopoDoPoco"].top - Footprints["ChaoDoPoco"].top;
        bool soPelasParedes = subida > jump.Jump + jump.DoubleJump + 0.5f;
        report.Note($"ChaoDoPoco -> TopoDoPoco: vão {poco:0.0} m, desnível +{subida:0.0} m, pulo duplo sobe " +
                    $"{jump.Jump + jump.DoubleJump:0.0} m - " + (soPelasParedes ? "só pelas paredes (ok)" : "ALCANÇÁVEL SEM AS PAREDES"),
                    !soPelasParedes);

        Register("Cristal1_Alto", FarEnds["Cristal1"].top, FarEnds["Cristal1"].poly);
        report.Route("Cristais", "TopoDoPoco", "Fragil1", "Fragil2", "Fragil3", "Fragil4", "Degrau", "Cristal1_Alto", "Varanda");
        report.Route("Perseguição", "Varanda", "HeliceInicio");
        report.Route("Cristais finais", "HeliceFim", "FragilFinal1", "FragilFinal2", "FragilFinal3", "Cume");
        report.Finish();
    }
}
