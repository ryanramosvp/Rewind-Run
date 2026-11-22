using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static VolcanicKit;

/// <summary>
/// Fase 5: Relojoaria Suspensa - um relógio desmontado flutuando no céu da
/// noite, acima da fumaça do vulcão.
///
/// É a fase que apresenta as três ideias novas, uma de cada vez e sempre com
/// rede antes de cobrar:
///  1. Impulso no ar: o primeiro vão de impulso tem uma rede embaixo, com
///     degraus de volta; só o segundo, depois de um ponto de controle, cobra
///     o impulso de verdade (20 m, impossível sem ele).
///  2. Zonas de âmbar: uma hélice rápida demais numa ponte estreita, dentro de
///     uma zona que a deixa lenta. A dica diz a regra; a hélice a prova.
///     Depois, plataformas que piscam: uma fileira dentro de zonas (lenta,
///     segura) e outra fora (rápida, com vãos maiores).
///  3. Contratempo: cristais que só andam no rewind. O primeiro sobe três
///     metros sobre chão firme; o segundo atravessa um vazio de 27 m, longe
///     demais até para o impulso.
///
/// Depois vêm engrenagens que giram, pêndulos (um dentro de zona, outro
/// fora) e a peça de assinatura: um mostrador de relógio deitado sobre o
/// vazio, com doze ilhas nos números e ponteiros que se pode montar. A borda
/// é a rota segura; o centro, por pulo duplo ou pelos ponteiros, é o atalho.
///
/// Três pontos de controle: com um deles ativo a morte não zera o tempo.
/// </summary>
public static class RelojoariaLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_RelojoariaSuspensa.unity";
    public const string SceneName = "Level_RelojoariaSuspensa";
    public const string Musica = "Musica/badinerie_bach.mp3";

    // Provisórios: ~300 m de percurso, mais as esperas dos cristais e das
    // plataformas que piscam. Recalibrar lendo o save depois de jogar.
    static readonly float[] kRanks = { 50f, 62f, 78f, 100f, 135f };

    const float H = 40f;      // altura do primeiro trecho
    const float H2 = 45.5f;   // depois do patamar do contratempo

    static readonly Vector3 Relogio = new Vector3(9f, H2, 245f);
    const float kAnelDoRelogio = 16f;

    [MenuItem("Rewind Run/Criar Fase 5: Relojoaria Suspensa", priority = 11)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();
        BeginLevel();
        TempoKit.Ensure();

        TempoKit.LuzDaLua(-25f);
        TempoKit.AplicarNoite();

        var level = new GameObject("Fase").transform;
        CriarLargada(Group(level, "1_Largada"));
        CriarImpulso(Group(level, "2_Impulso"));
        CriarZona(Group(level, "3_PrimeiraZona"));
        CriarPiscantes(Group(level, "4_Piscantes"));
        CriarContratempo(Group(level, "5_Contratempo"));
        CriarEngrenagens(Group(level, "6_EngrenagensEPendulos"));
        CriarMostrador(Group(level, "7_Mostrador"));
        CriarTorre(Group(level, "8_Torre"));
        CriarCenario(Group(level, "Cenario"));

        var player = LevelKit.AssembleSystems(kRanks, CoracaoLevelBuilder.SceneName, null);
        TempoKit.LiberarImpulso(player);
        LevelKit.CreateMusic(Musica);
        if (player != null)
            player.transform.SetPositionAndRotation(new Vector3(0f, H + 0.3f, -3f), Quaternion.identity);
        CriarCamera(new GameObject("ZonasDeCamera").transform);

        TempoKit.Vazio(level, new Vector3(5f, H - 16f, 150f), new Vector3(260f, 4f, 420f));
        HazardSetup.EnsureLava(new Vector3(5f, 0f, 150f), 420f, 520f, surfaceHeight: 0f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Validar();
        Debug.Log($"Fase 5 criada em {ScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s (provisórios).");
    }

    // ------------------------------------------------------------------
    // Trechos
    // ------------------------------------------------------------------

    static void CriarLargada(Transform root)
    {
        TempoKit.Engrenagem(root, "Largada", new Vector3(0f, H, 0f), 7f, 2f);
        TempoKit.Placa(root, "Plataforma1", new Vector3(0f, H, 14f), 6f, 6f);
    }

    /// <summary>
    /// O primeiro vão de impulso (14 m) tem uma rede seis metros abaixo e
    /// degraus de volta: errar custa alguns segundos, não a tentativa. O
    /// segundo (20 m) é o de verdade, depois do primeiro ponto de controle.
    /// </summary>
    static void CriarImpulso(Transform root)
    {
        TempoKit.Aviso(root, "Aviso_Impulso", new Bounds(new Vector3(0f, H + 1.5f, 13f), new Vector3(8f, 4f, 6f)),
                       "No ar, aperte SHIFT para dar um impulso. Um por salto - volta ao tocar o chão, uma bolha ou uma parede.",
                       "No ar, aperte RB para dar um impulso. Um por salto - volta ao tocar o chão, uma bolha ou uma parede.");

        TempoKit.Placa(root, "Rede", new Vector3(0f, H - 6f, 24f), 8f, 12f);
        TempoKit.Placa(root, "Degrau1", new Vector3(5.5f, H - 4f, 21f), 3f, 3f);
        TempoKit.Placa(root, "Degrau2", new Vector3(5.5f, H - 2f, 26.5f), 3f, 3f);

        TempoKit.Placa(root, "Plataforma2", new Vector3(0f, H, 34f), 6f, 6f);
        TempoKit.PontoDeControle(root, "PontoDeControle1", new Vector3(0f, H, 35f), 0f);

        TempoKit.Aviso(root, "Aviso_ImpulsoLongo", new Bounds(new Vector3(0f, H + 1.5f, 35f), new Vector3(6f, 4f, 4f)),
                       "Pulo, pulo duplo e só então o impulso: é assim que se vai mais longe.", null, 4f);

        TempoKit.Placa(root, "Plataforma3", new Vector3(0f, H, 60f), 6f, 6f);
    }

    /// <summary>
    /// Ponte estreita com uma hélice de 240 graus por segundo - impossível de
    /// atravessar no tempo normal - dentro de uma zona que a põe a 60.
    /// </summary>
    static void CriarZona(Transform root)
    {
        TempoKit.Aviso(root, "Aviso_Zona", new Bounds(new Vector3(0f, H + 1.5f, 60f), new Vector3(6f, 4f, 6f)),
                       "Zonas de âmbar desaceleram o mundo - mas não você.", null);

        TempoKit.Placa(root, "Ponte", new Vector3(0f, H, 72f), 4.4f, 16f);
        TempoKit.HeliceComRelogio(root, "Helice_DaZona", new Vector3(0f, H, 72f), 240f, 0f);
        TempoKit.Zona(root, "Zona_Ponte", new Vector3(0f, H + 1.5f, 72f), 6.5f, 0.25f, CanalTemporal.Obstaculos);
    }

    /// <summary>
    /// Duas fileiras até o mesmo patamar. A da esquerda fica em zonas: as
    /// plataformas piscam a um terço da velocidade, e os vãos são de 4 m. A
    /// da direita fica fora: ritmo normal, uma plataforma a menos, vãos de 7.
    /// </summary>
    static void CriarPiscantes(Transform root)
    {
        float[] esquerda = { 86f, 94f, 102f, 110f };
        for (int i = 0; i < esquerda.Length; i++)
            TempoKit.PlataformaPiscante(root, $"PiscaE{i + 1}", new Vector3(-5f, H, esquerda[i]), 4f, 4f,
                                        1.5f, 1.5f, i * 0.25f);
        TempoKit.Zona(root, "Zona_Piscantes1", new Vector3(-5f, H + 1f, 90f), 6.5f, 0.3f, CanalTemporal.Plataformas);
        TempoKit.Zona(root, "Zona_Piscantes2", new Vector3(-5f, H + 1f, 106f), 6.5f, 0.3f, CanalTemporal.Plataformas);

        float[] direita = { 87f, 98f, 109f };
        for (int i = 0; i < direita.Length; i++)
            TempoKit.PlataformaPiscante(root, $"PiscaD{i + 1}", new Vector3(5.5f, H, direita[i]), 4f, 4f,
                                        1.6f, 1.0f, i * 0.3f);

        TempoKit.Placa(root, "Plataforma4", new Vector3(0f, H, 118f), 14f, 6f);
        TempoKit.PontoDeControle(root, "PontoDeControle2", new Vector3(-4f, H, 118.5f), 0f);
    }

    /// <summary>
    /// Primeiro cristal: sobe 2,75 m ao lado de um patamar alto, sobre chão
    /// firme - errar não custa nada. Segundo: atravessa 12 dos 27 m de vazio
    /// até o outro lado; os dois saltos que sobram pedem duplo ou impulso.
    /// </summary>
    static void CriarContratempo(Transform root)
    {
        TempoKit.Aviso(root, "Aviso_Contratempo", new Bounds(new Vector3(0f, H + 1.5f, 118f), new Vector3(14f, 4f, 6f)),
                       "Cristais azuis só andam enquanto você volta no tempo. Segure o BOTÃO DIREITO e veja.",
                       "Cristais azuis só andam enquanto você volta no tempo. Segure LB / LT e veja.", 6f);

        TempoKit.Contratempo(root, "Cristal1", new Vector3(5.5f, H, 124.5f), 3f, 3f, new Vector3(0f, H2 - H - 2.75f, 0f), 1.2f);
        TempoKit.Engrenagem(root, "Patamar", new Vector3(0f, H2, 128.5f), 4f, 12f);

        TempoKit.Contratempo(root, "Cristal2", new Vector3(0f, H2, 134.5f), 4f, 4f, new Vector3(0f, 0f, 12f), 1.5f);
        TempoKit.Placa(root, "Plataforma6", new Vector3(0f, H2, 166f), 12f, 12f);
        TempoKit.PontoDeControle(root, "PontoDeControle3", new Vector3(0f, H2, 166f), 0f);
    }

    /// <summary>
    /// Duas engrenagens girando em sentidos opostos e um corredor com dois
    /// pêndulos: o primeiro dentro de uma zona (lento), o segundo fora.
    /// </summary>
    static void CriarEngrenagens(Transform root)
    {
        TempoKit.EngrenagemGiratoria(root, "Giratoria1", new Vector3(0f, H2, 181f), 5f, 40f);
        TempoKit.EngrenagemGiratoria(root, "Giratoria2", new Vector3(9f, H2, 194f), 5f, -45f);

        TempoKit.Placa(root, "Corredor", new Vector3(9f, H2, 212f), 6f, 18f);
        TempoKit.Pendulo(root, "Pendulo1", new Vector3(9f, H2 + 8.5f, 208f), 7.3f, 55f, 2.2f, 0f);
        TempoKit.Pendulo(root, "Pendulo2", new Vector3(9f, H2 + 8.5f, 215.5f), 7.3f, 55f, 2.2f, 0.5f);
        TempoKit.Zona(root, "Zona_Pendulo", new Vector3(9f, H2 + 4.5f, 207f), 6f, 0.3f, CanalTemporal.Obstaculos);
    }

    /// <summary>
    /// O mostrador: doze ilhas na borda, um centro 0,7 m abaixo delas e dois
    /// ponteiros no nível do centro, que passam rente às ilhas. A zona do
    /// centro segura os ponteiros - a 35% da velocidade dá para montar neles.
    /// </summary>
    static void CriarMostrador(Transform root)
    {
        for (int k = 0; k < 12; k++)
        {
            float a = (90f - k * 30f) * Mathf.Deg2Rad;
            var p = Relogio + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * kAnelDoRelogio;
            var ilha = TempoKit.Engrenagem(root, $"Hora{(k == 0 ? 12 : k)}", p, 1.9f, 0.6f, 8,
                                           k % 3 == 0 ? TempoKit.Marfim : null);
            // Marca da hora: um traço de ferro voltado para o centro.
            Decor(ilha.transform, "Marca", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.35f, 1.4f), 0f, 0.04f, 1f),
                  TempoKit.Ferro, Vector3.zero);
            ilha.transform.GetChild(ilha.transform.childCount - 1).localRotation =
                Quaternion.Euler(0f, -(90f - k * 30f) + 90f, 0f);
        }

        var centro = Relogio + Vector3.down * 0.7f;
        TempoKit.Engrenagem(root, "Centro", centro, 3f, 1.5f, 12, TempoKit.Marfim);
        TempoKit.Ponteiro(root, "PonteiroMinutos", centro, 3f, 11f, 1.6f, 50f, 180f, TempoKit.Bronze);
        TempoKit.Ponteiro(root, "PonteiroHoras", centro, 3f, 8.5f, 2.2f, 18f, 60f, TempoKit.BronzeEscuro);
        TempoKit.Zona(root, "Zona_Centro", centro + Vector3.up * 0.5f, 9f, 0.35f, CanalTemporal.Plataformas);

        // O mostrador de verdade, lá embaixo: só imagem.
        var fundo = Relogio + Vector3.down * 7.5f;
        Decor(root, "Mostrador", LowPolyMesh.Prism(LowPolyMesh.Regular(36, 20f), -0.6f, 0f, 4f), TempoKit.Marfim, fundo);
        Decor(root, "Aro", TempoKit.MalhaAnel(20.3f, 0.8f, 48), TempoKit.Bronze, fundo + Vector3.up * 0.1f);
        for (int k = 0; k < 12; k++)
        {
            float a = (90f - k * 30f) * Mathf.Deg2Rad;
            var p = fundo + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 17.5f;
            var marca = Solid(root, "Numeral", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.8f, k % 3 == 0 ? 3.2f : 2f), 0f, 0.05f, 1f),
                              TempoKit.Ferro, TempoKit.Ferro, p, -(90f - k * 30f) + 90f);
            marca.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// Torre de engrenagens empilhadas até a chegada, com um relógio de pé
    /// ao fundo: a vista de cima mostra o mostrador inteiro que se cruzou.
    /// </summary>
    static void CriarTorre(Transform root)
    {
        TempoKit.Engrenagem(root, "Torre1", new Vector3(9f, H2 + 2.5f, 267f), 2.6f, 3f);
        TempoKit.Engrenagem(root, "Torre2", new Vector3(3f, H2 + 5f, 272f), 2.6f, 5.5f);
        TempoKit.Engrenagem(root, "Torre3", new Vector3(9f, H2 + 7.5f, 278f), 2.6f, 8f);
        TempoKit.Placa(root, "Chegada", new Vector3(9f, H2 + 9f, 287f), 10f, 8f, 0f, 1.2f);

        var finish = new Vector3(9f, H2 + 9f, 288f);
        LevelKit.Place(root, "green", "flag_A", finish + new Vector3(3.5f, 0f, 2.5f), 180f);
        LevelKit.Place(root, "neutral", "signage_finish", finish + new Vector3(-3.5f, 0f, 2.5f), 180f);

        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.position = finish + Vector3.up * 1.5f;
        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(10f, 3f, 5f);
        area.isTrigger = true;
        go.AddComponent<LevelFinish>();

        // Relógio de pé ao fundo, de frente para quem chega.
        var torre = Group(root, "RelogioDePe");
        torre.position = new Vector3(9f, H2 + 9f, 302f);
        Decor(torre, "Caixa", LowPolyMesh.Prism(LowPolyMesh.Rectangle(18f, 4f), -30f, 26f, 4f), TempoKit.BronzeEscuro, Vector3.zero);
        var face = Solid(torre, "Face", LowPolyMesh.Prism(LowPolyMesh.Regular(32, 8f), 0f, 0.4f, 4f), TempoKit.Marfim,
                         TempoKit.Bronze, new Vector3(0f, 14f, -2.1f), 0f);
        face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var centroDaFace = new Vector3(0f, 14f, -2.6f);
        foreach (var (angulo, tamanho) in new[] { (0f, 6.5f), (-110f, 4.5f) })
        {
            var giro = Quaternion.Euler(0f, 0f, angulo);
            var ponteiro = Solid(torre, "Ponteiro", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.6f, tamanho), 0f, 0.3f, 1f),
                                 TempoKit.Ferro, TempoKit.Ferro, Vector3.zero, 0f);
            ponteiro.transform.localRotation = giro * Quaternion.Euler(-90f, 0f, 0f);
            ponteiro.transform.localPosition = centroDaFace + giro * Vector3.up * (tamanho * 0.5f - 0.3f);
        }

        Decor(torre, "Lampiao", LowPolyMesh.Prism(LowPolyMesh.Regular(6, 1f), 26f, 28f, 1f, 0.6f), TempoKit.LuzAmbar, Vector3.zero);
    }

    /// <summary>
    /// Engrenagens soltas ao longe, de lado e inclinadas: a oficina se
    /// desmanchando no céu. Longe da rota e sem colisão.
    /// </summary>
    static void CriarCenario(Transform root)
    {
        var random = new System.Random(5151);
        for (int i = 0; i < 16; i++)
        {
            float lado = i % 2 == 0 ? -1f : 1f;
            float x = 5f + lado * (38f + (float)random.NextDouble() * 40f);
            float z = -20f + i * 21f + (float)random.NextDouble() * 10f;
            float y = H - 12f + (float)random.NextDouble() * 34f;
            float raio = 3f + (float)random.NextDouble() * 9f;

            var malha = TempoKit.MalhaEngrenagem(raio, Mathf.RoundToInt(raio * 3f), -1.2f, 0f, "EngrenagemSolta");
            var go = Solid(root, "EngrenagemSolta", malha, i % 3 == 0 ? TempoKit.Marfim : TempoKit.Bronze,
                           TempoKit.BronzeEscuro, new Vector3(x, y, z), 0f);
            go.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 70f - 35f,
                                                          (float)random.NextDouble() * 360f,
                                                          (float)random.NextDouble() * 70f - 35f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // ------------------------------------------------------------------
    // Câmera
    // ------------------------------------------------------------------

    const float kPitch = 30f, kDistancia = 18f;

    /// <summary>
    /// Câmera fixa olhando para +Z, como nas fases planas. No mostrador ela
    /// sobe e se afasta para mostrar o relógio inteiro - a escolha entre a
    /// borda e o centro só existe se as duas aparecem.
    /// </summary>
    static void CriarCamera(Transform zonas)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;

        LevelKit.SetFloat(camera, "fixedYaw", 0f);
        LevelKit.SetFloat(camera, "fixedPitch", kPitch);
        LevelKit.SetFloat(camera, "fixedDistance", kDistancia);
        var rotation = Quaternion.Euler(kPitch, 0f, 0f);
        camera.transform.SetPositionAndRotation(new Vector3(0f, H + 1.4f, -3f) - rotation * Vector3.forward * kDistancia,
                                                rotation);

        TempoKit.ZonaDeCamera(zonas, "Rede", new Bounds(new Vector3(0f, H - 3f, 24f), new Vector3(16f, 8f, 14f)),
                              0f, 38f, 18f);
        TempoKit.ZonaDeCamera(zonas, "Contratempo", new Bounds(new Vector3(0f, H + 3f, 130f), new Vector3(20f, 12f, 30f)),
                              0f, 26f, 20f);
        TempoKit.ZonaDeCamera(zonas, "Mostrador", new Bounds(Relogio + Vector3.up * 2f, new Vector3(44f, 16f, 40f)),
                              0f, 50f, 28f);
        TempoKit.ZonaDeCamera(zonas, "Torre", new Bounds(new Vector3(6f, H2 + 6f, 280f), new Vector3(24f, 20f, 24f)),
                              0f, 34f, 22f);
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    static void Validar()
    {
        var report = JumpReport.Begin("Relojoaria Suspensa");
        if (report == null) return;

        report.Route("Largada", "Largada", "Plataforma1");
        report.Section("Impulso");
        TempoKit.SaltoComImpulso(report, "Plataforma1", "Plataforma2", exigido: false);
        report.Route("Rede de volta", "Rede", "Degrau1", "Degrau2", "Plataforma2");
        TempoKit.SaltoComImpulso(report, "Plataforma2", "Plataforma3", exigido: true);

        report.Route("Zona e ponte", "Plataforma3", "Ponte");
        report.Route("Piscantes na zona", "Ponte", "PiscaE1", "PiscaE2", "PiscaE3", "PiscaE4", "Plataforma4");
        report.Route("Piscantes fora", "Ponte", "PiscaD1", "PiscaD2", "PiscaD3", "Plataforma4");

        // Os cristais contam na posição depois do rewind: é dali que se pula.
        Register("Cristal1_Alto", FarEnds["Cristal1"].top, FarEnds["Cristal1"].poly);
        Register("Cristal2_Fim", FarEnds["Cristal2"].top, FarEnds["Cristal2"].poly);
        report.Route("Contratempo vertical", "Plataforma4", "Cristal1_Alto", "Patamar");
        report.Route("Contratempo no vazio", "Patamar", "Cristal2_Fim", "Plataforma6");
        float direto = Gap(Footprints["Patamar"].poly, Footprints["Plataforma6"].poly);
        report.Note(direto > 24f
                        ? $"Patamar -> Plataforma6 direto: {direto:0.0} m, longe demais até com impulso - ok"
                        : $"Patamar -> Plataforma6 direto: {direto:0.0} m, ALCANÇÁVEL SEM O CRISTAL",
                    direto <= 24f);

        report.Route("Engrenagens", "Plataforma6", "Giratoria1", "Giratoria2", "Corredor");
        report.Route("Até o mostrador", "Corredor", "Hora6");
        report.Route("Borda do mostrador", "Hora6", "Hora5", "Hora4", "Hora3", "Hora2", "Hora1", "Hora12");
        report.Route("Atalho pelo centro", "Hora6", "Centro", "Hora12");
        report.Route("Torre", "Hora12", "Torre1", "Torre2", "Torre3", "Chegada");
        report.Finish();
    }
}
