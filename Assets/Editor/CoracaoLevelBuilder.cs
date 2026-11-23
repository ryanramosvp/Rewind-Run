using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static VolcanicKit;

/// <summary>
/// Fase 6, a última: Coração do Tempo - colapso temporal.
///
/// No centro da cratera, a ampulheta gigante solta de tempos em tempos uma
/// onda que desmonta o cenário ao passar e o remonta ao voltar (ver
/// <see cref="CicloDoColapso"/>). A bruxa não é afetada: quem muda é o
/// mundo, e a fase é sobre ler a ampulheta e usar o cenário enquanto ele se
/// desfaz.
///
/// Quase todo o percurso é feito de fragmentos: a onda os tira do lugar -
/// uns sobem, os vizinhos descem, escorregam de lado e tombam um pouco - e
/// o caminho continua passável em qualquer momento do ciclo. A onda é um
/// dificultador, não uma porta: num jogo de tempo, ninguém precisa esperá-la.
///
/// O percurso dá uma volta em torno dela, de fora para dentro e para cima:
///  1. Primeira região (sul): uma linha de lajes até o centro, subindo.
///  2. Região intermediária (leste): uma torre que já é escada de blocos
///     soltos, uma laje inclinada que a onda inclina mais, e um poço de
///     paredes inteiras para subir pulando de uma para a outra.
///  3. Região superior: o Observatório, de onde se vê a ampulheta inteira.
///  4. Região externa (norte): lajes que se afastam umas das outras com a
///     onda e um vão de impulso (que a onda encurta).
///  5. Sequência final: uma ponte de peças descendo da Torre do Fim até o
///     núcleo, a mais agitada de todas quando a onda passa.
/// Tocar a ilha do núcleo para o ciclo e começa o encerramento da campanha.
///
/// Sem textos de explicação: a fase ensina pelo desenho, à vista da
/// ampulheta. Os pontos de controle ficam em chão fixo, para nunca se
/// renascer numa peça solta.
///
/// Cada peça que se mexe é um <see cref="FragmentoTemporal"/> com pose
/// calculada a partir do tempo do ciclo, então o rewind devolve tudo ao
/// lugar exato. Nada é criado nem destruído durante o jogo.
/// </summary>
public static class CoracaoLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_CoracaoDoTempo.unity";
    public const string SceneName = "Level_CoracaoDoTempo";
    public const string Musica = "Musica/sinfonia4_tchaikovsky.mp3";
    public const string MusicaFinal = "Musica/amen_scarlatti.mp3";

    // Provisórios: ~350 m de percurso sem esperas, mais o poço. Recalibrar
    // lendo o save depois de jogar.
    static readonly float[] kRanks = { 60f, 72f, 90f, 115f, 155f };

    /// <summary>Centro da cratera, no nível da lava.</summary>
    static readonly Vector3 O = new Vector3(0f, 0f, 40f);
    const float kMeioDaAmpulheta = 34f;
    const float kMeiaAltura = 20f;
    const float kIlhaDoNucleo = 12f;     // topo da ilha central
    const float kRaioDaIlha = 20f;

    // Alturas dos trechos.
    const float H1 = 14f, H2 = 21f;

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
        var ciclo = CriarCiclo(level);
        CriarPrimeiraRegiao(Group(level, "1_PrimeiraRegiao"));
        CriarRegiaoIntermediaria(Group(level, "2_RegiaoIntermediaria"));
        CriarRegiaoExterna(Group(level, "3_RegiaoExterna"));
        CriarPonteFinal(Group(level, "4_PonteFinal"));
        var ampulheta = CriarAmpulheta(Group(level, "Ampulheta"), ciclo);
        CriarDestrocos(Group(level, "Destrocos"));
        CriarCratera(Group(level, "Cratera"));

        var player = LevelKit.AssembleSystems(kRanks, ResultsSceneBuilder.SceneName, "ver resultados");
        TempoKit.LiberarImpulso(player);
        LevelKit.CreateMusic(Musica);
        var largada = P(0f, 95f, H1 + 0.3f);
        if (player != null) player.transform.SetPositionAndRotation(largada, Quaternion.identity);

        CriarNucleo(Group(level, "5_Nucleo"), ampulheta);
        CriarCamera(player, largada);

        TempoKit.Vazio(level, new Vector3(O.x, 5f, O.z), new Vector3(280f, 4f, 280f));
        HazardSetup.EnsureLava(new Vector3(O.x, 0f, O.z), 360f, 360f, surfaceHeight: 0f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Validar();
        Debug.Log($"Fase 6 criada em {ScenePath}. Ranks S-D: {string.Join(" / ", kRanks)} s (provisórios).");
    }

    // ------------------------------------------------------------------
    // Geometria em volta da ampulheta
    // ------------------------------------------------------------------

    /// <summary>Ponto no ângulo <paramref name="graus"/> (0 ao sul, crescendo para o leste).</summary>
    static Vector3 P(float graus, float raio, float y)
    {
        float a = graus * Mathf.Deg2Rad;
        return new Vector3(O.x + raio * Mathf.Sin(a), y, O.z - raio * Mathf.Cos(a));
    }

    /// <summary>Direção de quem anda em volta do centro, no sentido em que o ângulo cresce.</summary>
    static Vector3 Tangente(float graus)
    {
        float a = graus * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    /// <summary>Direção para fora, a partir do centro.</summary>
    static Vector3 ParaFora(float graus)
    {
        float a = graus * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
    }

    /// <summary>Giro que põe o Z local na tangente e o X local para fora.</summary>
    static float Rumo(float graus) => 90f - graus;

    /// <summary>Giro que põe o Z local apontando para o centro.</summary>
    static float RumoParaDentro(float graus) => -graus;

    /// <summary>Placa fixa: <paramref name="radial"/> de largura, <paramref name="tangente"/> de comprimento.</summary>
    static GameObject Placa(Transform root, string name, Vector3 topo, float graus, float radial, float tangente)
    {
        return Plate(root, name, topo.x, topo.z, topo.y, radial, tangente, Rumo(graus), TempoKit.Bronze,
                     trim: false, supports: false);
    }

    static Vector2 Xz(Vector3 v) => new Vector2(v.x, v.z);

    /// <summary>
    /// Fragmento com chão: uma laje que a onda move. <paramref name="topo"/> é
    /// a pose inteira (pivô no meio do tampo); a solta é ela mais
    /// <paramref name="deslocamento"/> e <paramref name="giro"/>.
    /// <paramref name="registrar"/> diz em qual pose a laje entra na
    /// validação dos saltos: a inteira, a solta, ou as duas (null).
    /// </summary>
    static GameObject Fragmento(Transform root, string name, Vector3 topo, float yaw, float x, float z,
                                Vector3 deslocamento, Vector3 giro, bool? registrarSolta, float balanco = 0.08f,
                                float inclinacaoInicial = 0f, Material tampo = null, float espessura = 0.9f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(topo, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(inclinacaoInicial, 0f, 0f));

        var laje = Solid(go.transform, "Laje", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x, z), -espessura, 0f, LavaLetalArt.MetalTile),
                         tampo != null ? tampo : TempoKit.Bronze, TempoKit.BronzeEscuro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(laje, 0);
        var box = laje.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -espessura * 0.5f, 0f);
        box.size = new Vector3(x, espessura, z);

        var rachaduras = Rachaduras(go.transform, x, z, name);
        LevelKit.AddKinematicBody(go);

        var fragmento = go.AddComponent<FragmentoTemporal>();
        LevelKit.SetVector(fragmento, "deslocamento", deslocamento);
        LevelKit.SetVector(fragmento, "giro", giro);
        LevelKit.SetFloat(fragmento, "balanco", balanco);
        EditorSetupUtil.SetReferences(fragmento, "rachaduras", rachaduras);

        var retangulo = LowPolyMesh.Rectangle(x, z);
        var inteira = new Vector2[4];
        var solta = new Vector2[4];
        var rotacaoInteira = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < 4; i++)
        {
            var p = rotacaoInteira * new Vector3(retangulo[i].x, 0f, retangulo[i].y);
            inteira[i] = Xz(topo + p);
            solta[i] = Xz(topo + p + deslocamento);
        }

        if (registrarSolta == true) Register(name, topo.y + deslocamento.y, solta);
        else
        {
            Register(name, topo.y, inteira);
            if (registrarSolta == null) FarEnds[name] = (topo.y + deslocamento.y, solta);
        }

        return go;
    }

    /// <summary>Riscos finos no tampo, que acendem antes da peça se soltar.</summary>
    static Renderer[] Rachaduras(Transform pai, float x, float z, string semente)
    {
        int s = 17;
        foreach (char c in semente) s = s * 31 + c;
        var random = new System.Random(s & 0x7fffffff);

        var lista = new List<Renderer>();
        for (int i = 0; i < 3; i++)
        {
            float comprimento = Mathf.Min(x, z) * (0.5f + 0.4f * (float)random.NextDouble());
            var risco = LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.14f, comprimento), 0f, 0.03f, 1f);
            var posicao = new Vector3(((float)random.NextDouble() - 0.5f) * x * 0.5f, 0.005f,
                                      ((float)random.NextDouble() - 0.5f) * z * 0.5f);
            var go = Solid(pai, "Rachadura", risco, TempoKit.Rachadura, TempoKit.Rachadura, posicao,
                           (float)random.NextDouble() * 180f);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            lista.Add(r);
        }

        return lista.ToArray();
    }

    // ------------------------------------------------------------------
    // Ciclo e ampulheta
    // ------------------------------------------------------------------

    /// <summary>
    /// O ciclo e a imagem da onda moram num objeto próprio no meio da
    /// ampulheta, e não nela: no fim a ampulheta vira, e a onda não pode
    /// virar junto.
    /// </summary>
    static CicloDoColapso CriarCiclo(Transform root)
    {
        var go = new GameObject("CicloDoColapso");
        go.transform.SetParent(root, false);
        go.transform.position = O + Vector3.up * kMeioDaAmpulheta;

        var som = go.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.spatialBlend = 0.35f;
        som.minDistance = 40f;
        som.maxDistance = 220f;
        som.rolloffMode = AudioRolloffMode.Linear;

        var ciclo = go.AddComponent<CicloDoColapso>();
        EditorSetupUtil.SetReference(ciclo, "som", som);

        // Casca: a esfera da onda.
        var casca = new GameObject("Casca");
        casca.transform.SetParent(go.transform, false);
        casca.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        var cascaRenderer = casca.AddComponent<MeshRenderer>();
        cascaRenderer.sharedMaterial = TempoKit.CascaDaOnda;
        cascaRenderer.shadowCastingMode = ShadowCastingMode.Off;
        cascaRenderer.receiveShadows = false;

        // Anéis onde a esfera corta as alturas em que se joga.
        float[] alturas = { H1 - kMeioDaAmpulheta, H2 - kMeioDaAmpulheta, 0f, 13f };
        var aneis = new Transform[alturas.Length];
        var malha = TempoKit.MalhaAnel(1f, 0.012f, 96);
        for (int i = 0; i < alturas.Length; i++)
        {
            var anel = new GameObject("Anel");
            anel.transform.SetParent(go.transform, false);
            anel.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = anel.AddComponent<MeshRenderer>();
            r.sharedMaterial = TempoKit.AnelDaOnda;
            r.shadowCastingMode = ShadowCastingMode.Off;
            aneis[i] = anel.transform;
        }

        // Partículas na superfície da esfera.
        var particulas = new GameObject("Particulas");
        particulas.transform.SetParent(go.transform, false);
        var ps = particulas.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 2.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.9f, 1f, 0.9f), new Color(1f, 0.82f, 0.4f, 0.9f));
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emissao = ps.emission;
        emissao.rateOverTime = 0f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 1f;
        forma.radiusThickness = 0f;
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var degrade = new Gradient();
        degrade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        cor.color = degrade;
        var psRenderer = particulas.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = TempoKit.Particula;
        psRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // Luz do núcleo: acende com a carga.
        var luz = new GameObject("LuzDoNucleo").AddComponent<Light>();
        luz.transform.SetParent(go.transform, false);
        luz.type = LightType.Point;
        luz.range = 60f;
        luz.color = new Color(0.65f, 0.88f, 1f);
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;

        var visual = go.AddComponent<VisualDaOnda>();
        EditorSetupUtil.SetReference(visual, "casca", casca.transform);
        EditorSetupUtil.SetReferences(visual, "aneis", aneis);
        EditorSetupUtil.SetFloats(visual, "alturasDosAneis", alturas);
        EditorSetupUtil.SetReference(visual, "particulas", ps);
        EditorSetupUtil.SetReference(visual, "luz", luz);
        return ciclo;
    }

    /// <summary>
    /// A ampulheta gigante, sobre a ilha do núcleo. O pivô fica no meio dela,
    /// que é onde ela gira no fim. Um núcleo aceso no gargalo pulsa com a
    /// carga do ciclo; a areia sobe por dentro.
    /// </summary>
    static Transform CriarAmpulheta(Transform root, CicloDoColapso ciclo)
    {
        var pivo = new GameObject("AmpulhetaGigante").transform;
        pivo.SetParent(root, false);
        pivo.localPosition = O + Vector3.up * kMeioDaAmpulheta;
        TempoKit.MontarAmpulheta(pivo, kMeiaAltura);

        var nucleo = new GameObject("Nucleo");
        nucleo.transform.SetParent(pivo, false);
        nucleo.transform.localScale = Vector3.one * 4.5f;
        nucleo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        var nucleoRenderer = nucleo.AddComponent<MeshRenderer>();
        nucleoRenderer.sharedMaterial = TempoKit.Areia;
        nucleoRenderer.shadowCastingMode = ShadowCastingMode.Off;
        EditorSetupUtil.SetReference(ciclo.GetComponent<VisualDaOnda>(), "nucleo", nucleoRenderer);

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

    // ------------------------------------------------------------------
    // Regiões
    // ------------------------------------------------------------------

    /// <summary>
    /// Peça de percurso que a onda tira do lugar: sobe ou desce, escorrega de
    /// lado e tomba um pouco. As vizinhas vão para lados opostos
    /// (<paramref name="indice"/> par ou ímpar), e é isso que faz o caminho
    /// "trocar de lugar" - mas os deslocamentos são pequenos de propósito:
    /// o caminho continua passável parado, solto e no meio do movimento. A
    /// onda dificulta; nunca obriga a esperar.
    /// </summary>
    /// <param name="caminho">Direção em que se anda ali. O escorregão é de lado a ela, para não abrir o vão do salto.</param>
    static GameObject Peca(Transform root, string name, Vector3 topo, float yaw, float x, float z, Vector3 caminho,
                           int indice, float forca = 1f, Material tampo = null, float espessura = 0.9f)
    {
        bool par = indice % 2 == 0;
        var lado = Vector3.Cross(Vector3.up, caminho.normalized);
        var deslocamento = Vector3.up * ((par ? 1.6f : -0.8f) * forca) + lado * ((par ? 1.5f : -1.5f) * forca);
        var giro = new Vector3(0f, 0f, (par ? 6f : -6f) * forca);
        return Fragmento(root, name, topo, yaw, x, z, deslocamento, giro, registrarSolta: null,
                         balanco: 0.12f * forca, tampo: tampo, espessura: espessura);
    }

    /// <summary>
    /// Primeira região, em linha reta para o centro e subindo devagar. Tudo
    /// nela flutua com a onda, uma peça para cima e a seguinte para baixo;
    /// os vãos são curtos para fechar em qualquer combinação.
    /// </summary>
    static void CriarPrimeiraRegiao(Transform root)
    {
        var dentro = -ParaFora(0f);
        float rumo = Rumo(0f);

        Placa(root, "Largada", P(0f, 95f, H1), 0f, 10f, 10f);
        Peca(root, "Entrada", P(0f, 82f, H1), rumo, 12f, 8f, dentro, 0, 0.6f);
        Peca(root, "Laje1", P(0f, 71.5f, H1 + 0.5f), rumo, 5f, 6f, dentro, 1);
        Peca(root, "Laje2", P(0f, 65f, H1 + 1f), rumo, 5f, 6f, dentro, 2);
        Peca(root, "Margem", P(0f, 56f, H1 + 1.5f), rumo, 10f, 10f, dentro, 3, 0.8f);
        Peca(root, "Degrau1", P(0f, 47f, H1 + 3f), rumo, 4f, 4f, dentro, 4);
        Peca(root, "Degrau2", P(0f, 42f, H1 + 4.8f), rumo, 4f, 4f, dentro, 5);

        // Ponto de controle sempre em chão fixo: renascer numa peça solta
        // poria a bruxa no ar.
        Placa(root, "Degrau", P(0f, 35.5f, H2 - 0.5f), 0f, 6f, 8f);
        TempoKit.PontoDeControle(root, "PontoDeControle1", P(0f, 35.5f, H2 - 0.5f), Rumo(0f));
    }

    // Torre, rampa e poço: tudo alinhado numa reta que sai da torre na
    // direção da tangente a 36 graus.
    const float kGrausDaTorre = 36f;
    static Vector3 BaseDaTorre => P(kGrausDaTorre, 47.5f, 0f);
    static Vector3 Adiante(float metros, float y) => BaseDaTorre + Tangente(kGrausDaTorre) * metros + Vector3.up * y;

    /// <summary>
    /// Região intermediária. A torre já é uma escada de blocos soltos no ar,
    /// e a onda os embaralha para cima e para baixo. A laje antes do Mirante
    /// está inclinada 10 graus e a onda a inclina até 20: a ponta passa
    /// acima do Mirante e se desce nele. O poço tem paredes inteiras - a onda
    /// não as toca -, e com a onda o chão dele sobe um pouco.
    /// </summary>
    static void CriarRegiaoIntermediaria(Transform root)
    {
        float rumo = Rumo(kGrausDaTorre);
        var t = Tangente(kGrausDaTorre);

        Peca(root, "Caminho", P(18f, 40f, H2), Rumo(18f), 6f, 8f, Tangente(18f), 0);
        Peca(root, "PeDaTorre", P(kGrausDaTorre, 40f, H2), rumo, 8f, 8f, Tangente(kGrausDaTorre), 1, 0.8f);

        for (int i = 0; i < 3; i++)
        {
            float topo = H2 + 2.5f + i * 2.5f;
            Peca(root, $"BlocoDaTorre{i + 1}", BaseDaTorre + t * (6f * (i + 1)) + Vector3.up * topo, rumo, 5f, 5f, t,
                 i + 1, 0.8f, espessura: 2.5f);
        }

        float patamar = H2 + 10f;
        Placa(root, "Patamar", Adiante(26f, patamar), kGrausDaTorre, 8f, 8f);
        TempoKit.PontoDeControle(root, "PontoDeControle2", Adiante(26f, patamar), rumo);

        // Laje que inclina: pivô na borda de perto. Já parada sobe 10 graus;
        // a onda a leva a 20 (abaixo dos 25 do deslize: sobe-se correndo).
        const float comprimento = 16f;
        var pivo = new GameObject("RampaQueInclina");
        pivo.transform.SetParent(root, false);
        pivo.transform.SetPositionAndRotation(Adiante(30f, patamar), Quaternion.Euler(0f, rumo, 0f) * Quaternion.Euler(-10f, 0f, 0f));
        var laje = Solid(pivo.transform, "Laje", LowPolyMesh.Prism(LowPolyMesh.Rectangle(5f, comprimento), -0.9f, 0f, LavaLetalArt.MetalTile),
                         TempoKit.Bronze, TempoKit.BronzeEscuro, new Vector3(0f, 0f, comprimento * 0.5f), 0f);
        GameObjectUtility.SetStaticEditorFlags(laje, 0);
        var box = laje.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.45f, 0f);
        box.size = new Vector3(5f, 0.9f, comprimento);
        LevelKit.AddKinematicBody(pivo);
        var rachas = Rachaduras(laje.transform, 5f, comprimento, "Rampa");
        var fragmento = pivo.AddComponent<FragmentoTemporal>();
        LevelKit.SetVector(fragmento, "deslocamento", Vector3.zero);
        LevelKit.SetVector(fragmento, "giro", new Vector3(-10f, 0f, 0f));
        LevelKit.SetFloat(fragmento, "balanco", 0f);
        EditorSetupUtil.SetReferences(fragmento, "rachaduras", rachas);

        s_PontaParada = patamar + comprimento * Mathf.Sin(10f * Mathf.Deg2Rad);
        s_PontaSolta = patamar + comprimento * Mathf.Sin(20f * Mathf.Deg2Rad);
        float mirante = s_PontaParada + 0.5f;
        Placa(root, "Mirante", Adiante(30f + comprimento + 1f + 4f, mirante), kGrausDaTorre, 8f, 8f);

        // Poço: duas paredes inteiras, paralelas ao caminho, a 4,5 m uma da outra.
        float inicio = 30f + comprimento + 1f + 8f + 1f;
        var chao = Adiante(inicio + 2.5f, mirante);
        Placa(root, "ChaoDoPoco", chao, kGrausDaTorre, 4.5f, 5f);
        float altura = 13f;
        foreach (float lado in new[] { -2.5f, 2.5f })
        {
            var p = chao + ParaFora(kGrausDaTorre) * lado;
            var parede = Solid(root, "ParedeInteira", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.6f, 7f), 0f, altura, 2f),
                               TempoKit.BronzeEscuro, TempoKit.Bronze, p, rumo);
            var colisor = parede.AddComponent<BoxCollider>();
            colisor.center = new Vector3(0f, altura * 0.5f, 0f);
            colisor.size = new Vector3(0.6f, altura, 7f);
        }

        // A câmera radial olharia de fora, através de uma das paredes. Aqui
        // ela vai para as costas da bruxa, olhando na direção do caminho, um
        // pouco acima: as paredes ficam à esquerda e à direita.
        s_ExcecaoDoPoco = new RadialCameraFraming.Excecao
        {
            area = new Bounds(chao + Vector3.up * 6f, new Vector3(13f, 16f, 13f)),
            yaw = rumo,
            pitch = 38f,
            distance = 15f,
        };

        var observatorio = Adiante(inicio + 5f + 1f + 5f, mirante + 9.5f);
        Placa(root, "Observatorio", observatorio, kGrausDaTorre, 10f, 10f);
        TempoKit.PontoDeControle(root, "PontoDeControle3", observatorio, rumo);
        s_Observatorio = observatorio;
    }

    static float s_PontaParada, s_PontaSolta;
    static Vector3 s_Observatorio;
    static RadialCameraFraming.Excecao s_ExcecaoDoPoco;

    const float kRaioExterno = 80f;

    /// <summary>
    /// Ilhas de fora, na altura do Observatório. Lajes que se afastam umas
    /// das outras quando a onda chega (os vãos vão de 4,4 a 6,9 m) e um vão de
    /// 19 m que pede impulso - ou, com a onda, só 9, porque a última laje
    /// deriva na direção da ilha do outro lado.
    /// </summary>
    static void CriarRegiaoExterna(Transform root)
    {
        float y = s_Observatorio.y;
        float grausDoObservatorio = Mathf.Atan2(s_Observatorio.x - O.x, -(s_Observatorio.z - O.z)) * Mathf.Rad2Deg;
        var ilha1 = P(grausDoObservatorio + 12f, kRaioExterno, y);
        Rock(root, "Ilha1", ilha1.x, ilha1.z, y, 4f, 9, 81, LavaLetalArt.RockBrown);

        float[] graus = { 8f, 14f, 20f, 26f };
        for (int i = 0; i < graus.Length; i++)
        {
            float g = grausDoObservatorio + 12f + graus[i];
            Fragmento(root, $"Deriva{i + 1}", P(g, kRaioExterno, y), Rumo(g), 4f, 4f,
                      Tangente(g) * (2.5f * (i + 1)) + Vector3.up * (i % 2 == 0 ? 1f : -0.8f),
                      new Vector3(0f, 0f, 6f), registrarSolta: null, balanco: 0.08f);
        }

        float ultimo = grausDoObservatorio + 12f + graus[graus.Length - 1];
        float metrosPorGrau = kRaioExterno * Mathf.Deg2Rad;
        float grausDaIlha = ultimo + (2f + 19f + 4f) / metrosPorGrau;
        var ilha = P(grausDaIlha, kRaioExterno, y);
        Rock(root, "IlhaDoImpulso", ilha.x, ilha.z, y, 4f, 9, 82, LavaLetalArt.RockBrown);

        s_GrausDaTorreDoFim = grausDaIlha + (4f + 6f + 5f) / metrosPorGrau;
        var torre = P(s_GrausDaTorreDoFim, kRaioExterno, y);
        Placa(root, "TorreDoFim", torre, s_GrausDaTorreDoFim, 10f, 10f);
        Decor(root, "BaseDaTorreDoFim", LowPolyMesh.Prism(LowPolyMesh.Rectangle(8f, 8f), kBedrock - y, -0.8f, LavaLetalArt.RockTile, 0.8f),
              LavaLetalArt.RockBurnt, torre);
        TempoKit.PontoDeControle(root, "PontoDeControle4", torre, RumoParaDentro(s_GrausDaTorreDoFim));
    }

    static float s_GrausDaTorreDoFim;

    /// <summary>
    /// A sequência final: seis peças descendo da Torre do Fim até a ilha do
    /// núcleo. Paradas, são uma ponte; com a onda, sobem e descem alternadas,
    /// escorregam de lado e tombam - o trecho mais agitado do jogo, mas
    /// sempre passável.
    /// </summary>
    static void CriarPonteFinal(Transform root)
    {
        float g = s_GrausDaTorreDoFim;
        float y0 = s_Observatorio.y;
        float rumo = RumoParaDentro(g);
        var dentro = -ParaFora(g);
        float[] raios = { 70.5f, 63.5f, 56.5f, 49f, 41f, 33f };
        for (int i = 0; i < raios.Length; i++)
            Peca(root, $"PecaFinal{i + 1}", P(g, raios[i], y0 - 3f - i * 3.2f), rumo, 5f, 5f, dentro, i, 1.15f);
    }

    /// <summary>
    /// A ilha do núcleo, embaixo da ampulheta: o miolo é fixo, a borda são
    /// oito lascas que se soltam com a onda. Tocar a ilha encerra a campanha.
    /// </summary>
    static void CriarNucleo(Transform root, Transform ampulheta)
    {
        var centro = O + Vector3.up * kIlhaDoNucleo;
        var ilha = Solid(root, "IlhaDoNucleo", LowPolyMesh.Prism(LowPolyMesh.Regular(12, 15f), kBedrock - kIlhaDoNucleo, 0f,
                                                               LavaLetalArt.RockTile, 1f / kFlare),
                         TempoKit.Marfim, LavaLetalArt.RockSide, centro, 0f);
        AddMeshCollider(ilha);

        // Para a validação vale a borda inteira, com as lascas no lugar.
        var borda = new Vector2[16];
        for (int i = 0; i < 16; i++) borda[i] = Xz(P(i * 22.5f, kRaioDaIlha, 0f));
        Register("IlhaDoNucleo", kIlhaDoNucleo, borda);

        for (int i = 0; i < 8; i++)
        {
            float g = i * 45f + 22.5f;
            var meio = P(g, (15f + kRaioDaIlha) * 0.5f, kIlhaDoNucleo);
            Fragmento(root, $"Lasca{i + 1}", meio, Rumo(g), kRaioDaIlha - 15f + 0.4f, 2f * kRaioDaIlha * Mathf.Sin(22.5f * Mathf.Deg2Rad),
                      ParaFora(g) * 2.5f + Vector3.down * 1.5f, new Vector3(0f, 0f, 12f), registrarSolta: false,
                      balanco: 0.15f, tampo: TempoKit.Marfim, espessura: 2.5f);
        }

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
        go.transform.position = centro + Vector3.up * 3f;
        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(kRaioDaIlha * 2f, 8f, kRaioDaIlha * 2f);

        var fim = go.AddComponent<FimDaCampanha>();
        EditorSetupUtil.SetReference(fim, "ampulheta", ampulheta);
        EditorSetupUtil.SetReference(fim, "foco", ampulheta);
        EditorSetupUtil.SetReference(fim, "musicaFinal", LevelKit.MusicClip(MusicaFinal));
        EditorSetupUtil.SetReference(fim, "clarao", imagem);
        EditorSetupUtil.SetReference(fim, "volumeRewind", Object.FindAnyObjectByType<Volume>());
        EditorSetupUtil.SetString(fim, "cenaDosCreditos", CreditsSceneBuilder.SceneName);
        EditorSetupUtil.SetString(fim, "cenaDeResultados", ResultsSceneBuilder.SceneName);
        LevelKit.SetFloat(fim, "subida", 30f);
        LevelKit.SetFloat(fim, "afastamento", 80f);
    }

    /// <summary>
    /// Destroços soltos pela arena, sem colisão: rochas e lascas de bronze que
    /// a onda levanta e gira. São o espetáculo do colapso, longe do caminho.
    /// </summary>
    static void CriarDestrocos(Transform root)
    {
        var random = new System.Random(4242);
        for (int i = 0; i < 48; i++)
        {
            float g = (float)random.NextDouble() * 360f;
            float r = 28f + (float)random.NextDouble() * 70f;
            float y = 4f + (float)random.NextDouble() * 50f;
            float tamanho = 1.2f + (float)random.NextDouble() * 3.5f;
            var p = P(g, r, y);

            var go = new GameObject("Destroco");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler((float)random.NextDouble() * 360f,
                                                                    (float)random.NextDouble() * 360f, 0f));
            var malha = i % 3 == 0
                ? TempoKit.MalhaEngrenagem(tamanho, 8, -0.5f, 0f, "Lasca")
                : LowPolyMesh.Prism(LowPolyMesh.Rock(6, tamanho, 0.7f, random), -tamanho, 0f, LavaLetalArt.RockTile, 0.6f);
            var visual = Solid(go.transform, "Visual", malha, i % 3 == 0 ? TempoKit.Bronze : LavaLetalArt.RockBurnt,
                               i % 3 == 0 ? TempoKit.BronzeEscuro : LavaLetalArt.RockSide, Vector3.zero, 0f);
            GameObjectUtility.SetStaticEditorFlags(visual, 0);
            visual.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var f = go.AddComponent<FragmentoTemporal>();
            LevelKit.SetVector(f, "deslocamento", ParaFora(g) * (4f + (float)random.NextDouble() * 8f) +
                                                  Vector3.up * (3f + (float)random.NextDouble() * 9f));
            LevelKit.SetVector(f, "giro", new Vector3((float)random.NextDouble() * 160f - 80f,
                                                      (float)random.NextDouble() * 160f - 80f, 0f));
            LevelKit.SetFloat(f, "balanco", 0.6f);
        }
    }

    /// <summary>A borda da cratera: rochas altas em anel, além do alcance da onda.</summary>
    static void CriarCratera(Transform root)
    {
        var random = new System.Random(6060);
        for (int i = 0; i < 30; i++)
        {
            float graus = i * 12f + (float)random.NextDouble() * 6f;
            float raio = 118f + (float)random.NextDouble() * 14f;
            var p = P(graus, raio, 0f);
            float topo = 26f + (float)random.NextDouble() * 30f;
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
    /// Câmera radial em toda a fase: sempre de fora para dentro, então a
    /// ampulheta - o relógio do ciclo - fica sempre ao fundo da bruxa.
    /// </summary>
    static void CriarCamera(GameObject player, Vector3 largada)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;

        const float pitch = 28f, distancia = 22f;
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
        so.FindProperty("sectors").intValue = 12;
        so.ApplyModifiedPropertiesWithoutUndo();
        LevelKit.SetFloat(radial, "pitch", pitch);
        LevelKit.SetFloat(radial, "distance", distancia);
        LevelKit.SetFloat(radial, "raioMaximo", 0f);

        var excecoes = new SerializedObject(radial);
        var lista = excecoes.FindProperty("excecoes");
        lista.arraySize = 1;
        var item = lista.GetArrayElementAtIndex(0);
        item.FindPropertyRelative("area").boundsValue = s_ExcecaoDoPoco.area;
        item.FindPropertyRelative("yaw").floatValue = s_ExcecaoDoPoco.yaw;
        item.FindPropertyRelative("pitch").floatValue = s_ExcecaoDoPoco.pitch;
        item.FindPropertyRelative("distance").floatValue = s_ExcecaoDoPoco.distance;
        excecoes.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    static void Validar()
    {
        var report = JumpReport.Begin("Coração do Tempo");
        if (report == null) return;
        var jump = report.Jump;

        // Toda peça que a onda move entra com as duas poses, e cada salto é
        // conferido em todas as combinações: parada-parada, solta-solta e as
        // misturadas (uma já solta, a outra ainda não). Nenhuma pode ser
        // impossível - a onda dificulta, mas o caminho nunca fecha.
        Rota(report, "Primeira região", "Largada", "Entrada", "Laje1", "Laje2", "Margem", "Degrau1", "Degrau2", "Degrau");
        Rota(report, "Torre", "Degrau", "Caminho", "PeDaTorre", "BlocoDaTorre1", "BlocoDaTorre2", "BlocoDaTorre3", "Patamar");
        report.Note($"Rampa: ponta a {s_PontaParada:0.0} m parada e {s_PontaSolta:0.0} m com a onda; " +
                    $"Mirante a {Footprints["Mirante"].top:0.0} m");
        Rota(report, "Poço", "Mirante", "ChaoDoPoco");
        float subida = Footprints["Observatorio"].top - Footprints["ChaoDoPoco"].top;
        report.Note($"ChaoDoPoco -> Observatorio: +{subida:0.0} m - " +
                    (subida > jump.Jump + jump.DoubleJump + 0.5f ? "só pelas paredes (ok)" : "ALCANÇÁVEL SEM AS PAREDES"),
                    subida <= jump.Jump + jump.DoubleJump + 0.5f);

        Rota(report, "Região externa", "Observatorio", "Ilha1", "Deriva1", "Deriva2", "Deriva3", "Deriva4");
        TempoKit.SaltoComImpulso(report, "Deriva4", "IlhaDoImpulso", exigido: false);
        Rota(report, "Até a Torre do Fim", "IlhaDoImpulso", "TorreDoFim");
        Rota(report, "Ponte final", "TorreDoFim", "PecaFinal1", "PecaFinal2", "PecaFinal3", "PecaFinal4",
             "PecaFinal5", "PecaFinal6", "IlhaDoNucleo");
        report.Finish();
    }

    /// <summary>
    /// Confere uma sequência em todas as combinações de pose. Falha se alguma
    /// for impossível; avisa quando a pior pede pulo duplo.
    /// </summary>
    static float DistanciaDaOnda((float top, Vector2[] poly) peca)
    {
        var c = Centroid(peca.poly);
        return Vector3.Distance(new Vector3(c.x, peca.top, c.y), O + Vector3.up * kMeioDaAmpulheta);
    }

    static void Rota(JumpReport report, string titulo, params string[] pecas)
    {
        report.Section(titulo);
        for (int i = 0; i + 1 < pecas.Length; i++)
        {
            var a = Footprints[pecas[i]];
            var b = Footprints[pecas[i + 1]];
            (int rank, string text, float gap, float dy, float single, float twin) pior = (-1, "", 0, 0, 0, 0);
            var de = Variants(pecas[i], a);
            var para = Variants(pecas[i + 1], b);

            // Pose misturada (uma solta, a outra não) só acontece se a onda
            // chega às duas em momentos diferentes - à mesma distância do
            // centro elas se soltam juntas.
            bool misturadas = Mathf.Abs(DistanciaDaOnda(a) - DistanciaDaOnda(b)) > 2f;
            for (int x = 0; x < de.Count; x++)
            for (int y = 0; y < para.Count; y++)
            {
                if (!misturadas && de.Count > 1 && para.Count > 1 && x != y) continue;
                var f = de[x];
                var t = para[y];
                var r = Judge(report.Jump, Gap(f.poly, t.poly), t.top - f.top);
                if (r.rank > pior.rank || (r.rank == pior.rank && r.gap > pior.gap)) pior = r;
            }

            report.Note($"{pecas[i]} -> {pecas[i + 1]}: pior caso vão {pior.gap:0.0} m, desnível {pior.dy:+0.0;-0.0;0.0} m - {pior.text}",
                        pior.rank >= 3);
        }
    }
}
