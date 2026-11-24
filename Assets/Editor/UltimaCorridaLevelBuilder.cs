using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static VolcanicKit;

/// <summary>
/// Fase 7, a última: A Última Corrida - do outro lado da ampulheta, uma
/// dimensão sem chão, e a bruxa correndo contra o próprio fantasma.
///
/// O percurso corre em +Z, em seis trechos:
///  1. O encontro: ilhas largas, o fantasma ao lado, a corrida começa.
///  2. Gravidade invertida: portais violeta levam ao teto, ciano devolvem
///     ao chão; termina numa alternância chão-teto-chão sobre o vazio, um
///     portal no meio de cada salto. Todo trecho de troca de gravidade é
///     visto de lado, em 2D: de lado, chão e teto ficam os dois na tela, e a
///     virada lê como subir ou descer, não como a câmera perdendo o rumo.
///  3. A dimensão lateral: a câmera gira de lado e o movimento fica preso
///     num plano; chão e teto em 2D, com uma plataforma que pisca.
///  4. Anomalias: um buraco negro que estica o salto de impulso, outro que
///     puxa de lado numa passarela estreita, e um terceiro com plataformas
///     em órbita - a volta segura, ou o impulso por cima, arriscando o miolo.
///  5. A perseguição: o fantasma deixa a rota e repete os passos da bruxa
///     com 1,3 s de atraso, sobre cristais que quebram. Parar é ser pego.
///  6. A última corrida: teto, buraco negro, uma trilha que só aparece
///     depois que o fantasma pisa nela (ou o desvio), e o impulso final.
/// Na chegada o fantasma se desfaz e abre um portal para o jardim do
/// epílogo, onde a campanha termina.
///
/// A rota do fantasma é gerada aqui, junto com as plataformas, a partir dos
/// mesmos pontos: ele nunca pisa onde não há chão, e mexer numa plataforma
/// move o caminho dele junto.
/// </summary>
public static class UltimaCorridaLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_UltimaCorrida.unity";
    public const string SceneName = "Level_UltimaCorrida";

    // Faixas mandadas pelo autor; enquanto não chegam, as provisórias abaixo.
    public const string Musica = "Musica/ultima_corrida.mp3";
    public const string MusicaDaVitoria = "Musica/vitoria_final.mp3";
    const string kMusicaProvisoria = "Musica/baroni_ironworks.mp3";
    // Homecoming (Andrea Baroni): a volta para casa, no jardim do fim.
    const string kVitoriaProvisoria = "Musica/baroni_homecoming.mp3";

    // Provisórios. Só termina a fase quem chega antes do fantasma (~56 s), mas
    // o tempo da fase segue correndo nas mortes; o S pede vencê-lo com folga.
    static readonly float[] kRanks = { 50f, 57f, 70f, 90f, 125f };

    const float Y = 30f;            // altura do chão da dimensão
    const float Teto = 40f;         // altura dos tetos (face de baixo)
    static readonly Vector3 kEpilogo = new Vector3(0f, Y, 900f);

    static Rota s_Rota;
    static readonly List<(PlataformaRevelada plataforma, float tempo)> s_Reveladas =
        new List<(PlataformaRevelada, float)>();

    [MenuItem("Rewind Run/Criar Fase 7: A Última Corrida", priority = 13)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();
        BeginLevel();
        TempoKit.Ensure();
        s_Reveladas.Clear();

        var luz = new GameObject("Directional Light").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        luz.intensity = 0.9f;
        luz.color = new Color(0.75f, 0.78f, 1f);
        luz.shadows = LightShadows.Soft;
        TempoKit.AplicarCosmos();

        var fase = new GameObject("Fase").transform;
        s_Rota = new Rota(new Vector3(2.5f, Y, -3f));

        Encontro(Group(fase, "1_Encontro"));
        Gravidade(Group(fase, "2_Gravidade"));
        Lateral(Group(fase, "3_DimensaoLateral"));
        Anomalias(Group(fase, "4_Anomalias"));
        var eco = Perseguicao(Group(fase, "5_Perseguicao"));
        var chegada = UltimaCorrida(Group(fase, "6_UltimaCorrida"));
        var destino = Epilogo(Group(fase, "Epilogo"));
        Cenario(Group(fase, "Cenario"));

        var player = LevelKit.AssembleSystems(kRanks, ResultsSceneBuilder.SceneName, "ver resultados");
        LevelKit.CreateMusic(System.IO.File.Exists("Assets/Audio/" + Musica) ? Musica : kMusicaProvisoria);
        var largada = new Vector3(-2.5f, Y + 0.3f, -3f);
        if (player != null) player.transform.SetPositionAndRotation(largada, Quaternion.identity);

        CriarChegada(fase, chegada, destino);
        CriarFantasma(fase, eco);
        CriarMedidor();
        CriarCamera(largada);

        CriarAbertura();

        TempoKit.Vazio(fase, new Vector3(0f, 6f, 450f), new Vector3(500f, 8f, 1300f));
        TempoKit.Vazio(fase, new Vector3(0f, 62f, 450f), new Vector3(500f, 8f, 1300f));
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Validar();
        Debug.Log($"Fase 7 criada em {ScenePath}. Fantasma: {s_Rota.Tempo:0.0} s. Ranks S-D: {string.Join(" / ", kRanks)} s (provisórios).");
    }

    // ------------------------------------------------------------------
    // Rota do fantasma
    // ------------------------------------------------------------------

    /// <summary>
    /// Constrói a rota do fantasma em amostras de 0,05 s: corre em linha
    /// reta, salta em arco, vira de chão para teto no meio de um salto. As
    /// velocidades ficam um pouco abaixo das da bruxa (corrida 11 m/s): ele
    /// ganha de quem erra, não de quem corre bem.
    /// </summary>
    sealed class Rota
    {
        public const float Passo = 0.05f;
        public readonly List<Vector3> Posicoes = new List<Vector3>();
        public readonly List<byte> Estados = new List<byte>();
        Vector3 _atual;
        bool _invertido;

        public Rota(Vector3 inicio)
        {
            _atual = inicio;
            Posicoes.Add(inicio);
            Estados.Add(FantasmaDaCorrida.NoChao);
        }

        public float Tempo => (Posicoes.Count - 1) * Passo;
        public Vector3 Atual => _atual;

        byte Estado(bool noAr) => (byte)((noAr ? FantasmaDaCorrida.NoAr : FantasmaDaCorrida.NoChao) |
                                         (_invertido ? FantasmaDaCorrida.Invertido : 0));

        void Amostrar(float duracao, System.Func<float, Vector3> posicao, bool noAr)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(duracao / Passo));
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                Posicoes.Add(posicao(u));
                Estados.Add(Estado(noAr && i < n));
            }

            _atual = posicao(1f);
        }

        public void Correr(Vector3 ate, float velocidade = 9.2f)
        {
            var de = _atual;
            Amostrar(Vector3.Distance(de, ate) / velocidade, u => Vector3.Lerp(de, ate, u), false);
        }

        /// <summary>Salto em arco; no teto o arco é para baixo.</summary>
        public void Saltar(Vector3 ate, float velocidade = 9.6f, float altura = 1.8f)
        {
            var de = _atual;
            float d = Vector2.Distance(new Vector2(de.x, de.z), new Vector2(ate.x, ate.z));
            float sinal = _invertido ? -1f : 1f;
            Amostrar(Mathf.Max(0.45f, d / velocidade), u =>
            {
                var p = Vector3.Lerp(de, ate, u);
                p.y += sinal * altura * 4f * u * (1f - u);
                return p;
            }, true);
        }

        public void Impulso(Vector3 ate) => Saltar(ate, 15f, 2.4f);

        /// <summary>Salto que atravessa um portal: sai de um lado e pousa no outro.</summary>
        public void Virar(Vector3 ate, bool paraOTeto)
        {
            var de = _atual;
            float d = Vector2.Distance(new Vector2(de.x, de.z), new Vector2(ate.x, ate.z));
            float duracao = Mathf.Max(0.7f, d / 10f);
            int n = Mathf.Max(1, Mathf.RoundToInt(duracao / Passo));
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                if (u >= 0.5f) _invertido = paraOTeto;
                var p = Vector3.Lerp(de, ate, u);
                p.y = Mathf.Lerp(de.y, ate.y, u * u * (3f - 2f * u));
                Posicoes.Add(p);
                Estados.Add(Estado(i < n));
            }

            _atual = ate;
            _invertido = paraOTeto;
        }
    }

    // ------------------------------------------------------------------
    // Peças
    // ------------------------------------------------------------------

    /// <summary>
    /// Bloco de pedra cósmica com a face de andar contornada de néon: ciano no
    /// chão, violeta no teto - a cor diz de que lado se pisa. No teto,
    /// <paramref name="superficie"/> é a face de baixo.
    /// </summary>
    static GameObject Bloco(Transform root, string name, Vector3 superficie, float x, float z, bool teto = false,
                            float espessura = 1.2f, bool registrar = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = superficie + (teto ? Vector3.up : Vector3.down) * (espessura * 0.5f);
        go.transform.localScale = new Vector3(x, espessura, z);
        go.GetComponent<MeshRenderer>().sharedMaterial = TempoKit.PedraCosmica;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

        var neon = teto ? TempoKit.NeonDoTeto : TempoKit.NeonDoChao;
        float face = superficie.y + (teto ? -0.02f : 0.02f);
        foreach (var (cx, cz, sx, sz) in new[]
                 {
                     (0f, z * 0.5f - 0.1f, x, 0.2f), (0f, -z * 0.5f + 0.1f, x, 0.2f),
                     (x * 0.5f - 0.1f, 0f, 0.2f, z), (-x * 0.5f + 0.1f, 0f, 0.2f, z),
                 })
        {
            var tira = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(tira.GetComponent<Collider>());
            tira.name = "Neon";
            tira.transform.SetParent(go.transform, true);
            tira.transform.position = new Vector3(superficie.x + cx, face, superficie.z + cz);
            tira.transform.localScale = new Vector3(sx / x, 0.04f / espessura, sz / z);
            var r = tira.GetComponent<MeshRenderer>();
            r.sharedMaterial = neon;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        if (registrar)
        {
            var poly = Square(superficie.x, superficie.z, 1f);
            poly = new[]
            {
                new Vector2(superficie.x - x * 0.5f, superficie.z - z * 0.5f), new Vector2(superficie.x + x * 0.5f, superficie.z - z * 0.5f),
                new Vector2(superficie.x + x * 0.5f, superficie.z + z * 0.5f), new Vector2(superficie.x - x * 0.5f, superficie.z + z * 0.5f),
            };
            // O teto entra espelhado: a validação mede o desnível no sentido
            // em que a gravidade puxa ali.
            Register(name, teto ? -superficie.y : superficie.y, poly);
        }

        return go;
    }

    /// <summary>Anel que vira a gravidade, em pé, atravessado no sentido +Z.</summary>
    static void Portal(Transform root, string name, Vector3 centro, bool paraOTeto)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = centro;

        var anel = new GameObject("Anel");
        anel.transform.SetParent(go.transform, false);
        anel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        anel.AddComponent<MeshFilter>().sharedMesh = TempoKit.MalhaAnel(2.4f, 0.28f, 40);
        var r = anel.AddComponent<MeshRenderer>();
        r.sharedMaterial = paraOTeto ? TempoKit.AnelVioleta : TempoKit.AnelCiano;
        r.shadowCastingMode = ShadowCastingMode.Off;

        var veu = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(veu.GetComponent<Collider>());
        veu.name = "Veu";
        veu.transform.SetParent(go.transform, false);
        veu.transform.localScale = Vector3.one * 4.6f;
        veu.GetComponent<MeshRenderer>().sharedMaterial = TempoKit.LenteRoxa;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4.8f, 4.8f, 1.4f);

        var som = go.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.spatialBlend = 0.6f;
        som.minDistance = 6f;
        som.maxDistance = 40f;

        var portal = go.AddComponent<PortalDeGravidade>();
        var so = new SerializedObject(portal);
        so.FindProperty("paraOTeto").boolValue = paraOTeto;
        so.FindProperty("anel").objectReferenceValue = anel.transform;
        so.FindProperty("som").objectReferenceValue = som;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Buraco negro: miolo escuro, disco de acreção girando, uma lente roxa do
    /// tamanho do alcance e partículas caindo para dentro.
    /// </summary>
    static GameObject Buraco(Transform root, string name, Vector3 centro, float alcance, float aceleracao, float mortal)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = centro;

        var miolo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(miolo.GetComponent<Collider>());
        miolo.name = "Miolo";
        miolo.transform.SetParent(go.transform, false);
        miolo.transform.localScale = Vector3.one * Mathf.Max(1.2f, mortal * 2f);
        miolo.GetComponent<MeshRenderer>().sharedMaterial = TempoKit.MioloEscuro;

        var disco = new GameObject("Disco");
        disco.transform.SetParent(go.transform, false);
        disco.transform.localRotation = Quaternion.Euler(18f, 0f, 8f);
        for (int i = 0; i < 3; i++)
        {
            var anel = new GameObject("Anel");
            anel.transform.SetParent(disco.transform, false);
            anel.AddComponent<MeshFilter>().sharedMesh = TempoKit.MalhaAnel(mortal * 1.6f + i * 0.9f, 0.35f - i * 0.08f, 48);
            var r = anel.AddComponent<MeshRenderer>();
            r.sharedMaterial = i == 1 ? TempoKit.AnelDaOnda : TempoKit.AnelVioleta;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        var lente = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(lente.GetComponent<Collider>());
        lente.name = "Lente";
        lente.transform.SetParent(go.transform, false);
        lente.transform.localScale = Vector3.one * alcance * 1.2f;
        var lr = lente.GetComponent<MeshRenderer>();
        lr.sharedMaterial = TempoKit.LenteRoxa;
        lr.shadowCastingMode = ShadowCastingMode.Off;

        var particulas = new GameObject("Particulas");
        particulas.transform.SetParent(go.transform, false);
        var ps = particulas.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.4f;
        main.startSpeed = -alcance * 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.6f, 1f), new Color(1f, 0.85f, 0.5f));
        main.maxParticles = 300;
        var emissao = ps.emission;
        emissao.rateOverTime = 90f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = alcance * 0.85f;
        forma.radiusThickness = 0f;
        var pr = particulas.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = TempoKit.Particula;
        pr.shadowCastingMode = ShadowCastingMode.Off;

        var zumbido = go.AddComponent<AudioSource>();
        zumbido.playOnAwake = false;

        var buraco = go.AddComponent<BuracoNegro>();
        LevelKit.SetFloat(buraco, "raioDeInfluencia", alcance);
        LevelKit.SetFloat(buraco, "aceleracaoMaxima", aceleracao);
        LevelKit.SetFloat(buraco, "raioMortal", mortal);
        EditorSetupUtil.SetReference(buraco, "disco", disco.transform);
        EditorSetupUtil.SetReference(buraco, "lente", lente.transform);
        EditorSetupUtil.SetReference(buraco, "zumbido", zumbido);
        return go;
    }

    /// <summary>
    /// Trecho visto de lado (2D), de <paramref name="zInicio"/> a
    /// <paramref name="zFim"/>, no plano x = 0 em que corre o percurso.
    /// </summary>
    static void ZonaLateral(Transform root, string name, float zInicio, float zFim)
    {
        var zona = new GameObject(name).AddComponent<ZonaLateral>();
        zona.transform.SetParent(root, false);
        var so = new SerializedObject(zona);
        so.FindProperty("area").boundsValue = new Bounds(new Vector3(0f, 36f, (zInicio + zFim) * 0.5f),
                                                         new Vector3(16f, 40f, zFim - zInicio));
        so.FindProperty("normalDoPlano").vector3Value = Vector3.right;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Ponto de controle no meio do caminho (x = 0), e não ao lado: nos
    /// trechos em 2D a bruxa fica presa nesse plano, e um ponto deslocado
    /// para o lado nunca seria tocado. A ampulheta não tem colisão, então não
    /// atrapalha quem passa por ela.
    /// </summary>
    static void PontoDeControle(Transform root, string name, Vector3 chao) =>
        TempoKit.PontoDeControle(root, name, new Vector3(0f, chao.y, chao.z), 0f);

    // ------------------------------------------------------------------
    // Segmentos
    // ------------------------------------------------------------------

    static void Encontro(Transform root)
    {
        Bloco(root, "Inicio", new Vector3(0f, Y, 0f), 14f, 14f, espessura: 3f);
        Bloco(root, "Passo1", new Vector3(0f, Y, 15f), 6f, 6f);
        Bloco(root, "Passo2", new Vector3(0f, Y + 1f, 25f), 5f, 5f);
        Bloco(root, "Passo3", new Vector3(0f, Y + 2f, 35f), 8f, 8f);

        s_Rota.Correr(new Vector3(1.5f, Y, 6.5f));
        s_Rota.Saltar(new Vector3(1f, Y, 13.5f));
        s_Rota.Correr(new Vector3(1f, Y, 17.5f));
        s_Rota.Saltar(new Vector3(0.5f, Y + 1f, 23.5f));
        s_Rota.Correr(new Vector3(0.5f, Y + 1f, 27f));
        s_Rota.Saltar(new Vector3(0f, Y + 2f, 32f));
        s_Rota.Correr(new Vector3(0f, Y + 2f, 38.5f));
    }

    static void Gravidade(Transform root)
    {
        Bloco(root, "Virada", new Vector3(0f, Y + 2f, 47f), 8f, 8f);
        // Na altura do corpo de quem corre: o anel encosta no chão da Virada.
        Portal(root, "Portal_Teto1", new Vector3(0f, Y + 2f + 2.4f, 50.5f), true);
        Bloco(root, "Teto1", new Vector3(0f, Teto, 59.5f), 6f, 21f, teto: true);
        Bloco(root, "Teto2", new Vector3(0f, Teto, 77f), 6f, 6f, teto: true);
        Bloco(root, "Teto3", new Vector3(0f, Teto - 1f, 88f), 6f, 8f, teto: true);
        Portal(root, "Portal_Chao1", new Vector3(0f, Teto - 4f, 94f), false);
        Bloco(root, "Alterna1", new Vector3(0f, Y, 100f), 6f, 8f);

        // Alternância: um portal no meio de cada salto.
        Portal(root, "Portal_Teto2", new Vector3(0f, Y + 4f, 107f), true);
        Bloco(root, "Alterna2", new Vector3(0f, Teto, 113f), 6f, 6f, teto: true);
        Portal(root, "Portal_Chao2", new Vector3(0f, Teto - 4f, 119f), false);
        Bloco(root, "Alterna3", new Vector3(0f, Y, 125f), 6f, 6f);
        Portal(root, "Portal_Teto3", new Vector3(0f, Y + 4f, 131f), true);
        Bloco(root, "Alterna4", new Vector3(0f, Teto, 137f), 6f, 6f, teto: true);
        Portal(root, "Portal_Chao3", new Vector3(0f, Teto - 4f, 143f), false);
        Bloco(root, "Pouso1", new Vector3(0f, Y, 150f), 10f, 10f);
        PontoDeControle(root, "PontoDeControle1", new Vector3(0f, Y, 150f));

        s_Rota.Saltar(new Vector3(0f, Y + 2f, 44f));
        s_Rota.Correr(new Vector3(0f, Y + 2f, 50f));
        s_Rota.Virar(new Vector3(0f, Teto, 55f), true);
        s_Rota.Correr(new Vector3(0f, Teto, 69f));
        s_Rota.Saltar(new Vector3(0f, Teto, 75.5f));
        s_Rota.Correr(new Vector3(0f, Teto, 79f));
        s_Rota.Saltar(new Vector3(0f, Teto - 1f, 85.5f));
        s_Rota.Correr(new Vector3(0f, Teto - 1f, 91.5f));
        s_Rota.Virar(new Vector3(0f, Y, 99f), false);
        s_Rota.Correr(new Vector3(0f, Y, 103.5f));
        s_Rota.Virar(new Vector3(0f, Teto, 113f), true);
        s_Rota.Correr(new Vector3(0f, Teto, 115.5f));
        s_Rota.Virar(new Vector3(0f, Y, 125f), false);
        s_Rota.Correr(new Vector3(0f, Y, 127.5f));
        s_Rota.Virar(new Vector3(0f, Teto, 137f), true);
        s_Rota.Correr(new Vector3(0f, Teto, 139.5f));
        s_Rota.Virar(new Vector3(0f, Y, 149f), false);
        s_Rota.Correr(new Vector3(0f, Y, 154f));
    }

    /// <summary>
    /// Trecho em 2D: tudo no plano x = 0, câmera do lado +X. Chão e teto se
    /// alternam por portais, com um vão no teto e uma plataforma que pisca.
    /// </summary>
    static void Lateral(Transform root)
    {
        // Uma zona só, emendando a gravidade invertida do trecho 2 com este:
        // duas zonas coladas fariam a câmera girar para o 3D e voltar.
        ZonaLateral(root, "ZonaLateral", 42.5f, 240f);

        Bloco(root, "Lado1", new Vector3(0f, Y, 163f), 4f, 10f);
        Portal(root, "Portal_TetoLado1", new Vector3(0f, Y + 3.5f, 170.5f), true);
        Bloco(root, "LadoTeto1", new Vector3(0f, Teto + 2f, 175f), 4f, 6f, teto: true);
        Bloco(root, "LadoTeto2", new Vector3(0f, Teto + 2f, 183.5f), 4f, 5f, teto: true);
        Portal(root, "Portal_ChaoLado1", new Vector3(0f, Teto - 1f, 188.5f), false);
        Bloco(root, "Lado2", new Vector3(0f, Y, 195f), 4f, 8f);
        TempoKit.PlataformaPiscante(root, "LadoPisca", new Vector3(0f, Y + 1f, 205f), 4f, 6f, 1.4f, 0.9f, 0f);
        Bloco(root, "Lado3", new Vector3(0f, Y + 2f, 214f), 4f, 6f);
        Portal(root, "Portal_TetoLado2", new Vector3(0f, Y + 5.5f, 219.5f), true);
        Bloco(root, "LadoTeto3", new Vector3(0f, Teto + 2f, 228f), 4f, 12f, teto: true);
        Portal(root, "Portal_ChaoLado2", new Vector3(0f, Teto - 1f, 236f), false);
        Bloco(root, "Lado4", new Vector3(0f, Y, 243f), 8f, 8f);
        PontoDeControle(root, "PontoDeControle2", new Vector3(0f, Y, 244f));

        s_Rota.Saltar(new Vector3(0f, Y, 159f));
        s_Rota.Correr(new Vector3(0f, Y, 167.5f));
        s_Rota.Virar(new Vector3(0f, Teto + 2f, 176f), true);
        s_Rota.Correr(new Vector3(0f, Teto + 2f, 177.5f));
        s_Rota.Saltar(new Vector3(0f, Teto + 2f, 182f));
        s_Rota.Correr(new Vector3(0f, Teto + 2f, 185.5f));
        s_Rota.Virar(new Vector3(0f, Y, 194f), false);
        s_Rota.Correr(new Vector3(0f, Y, 198.5f));
        s_Rota.Saltar(new Vector3(0f, Y + 1f, 203f));
        s_Rota.Saltar(new Vector3(0f, Y + 2f, 212f));
        s_Rota.Correr(new Vector3(0f, Y + 2f, 216.5f));
        s_Rota.Virar(new Vector3(0f, Teto + 2f, 226f), true);
        s_Rota.Correr(new Vector3(0f, Teto + 2f, 233.5f));
        s_Rota.Virar(new Vector3(0f, Y, 242f), false);
        s_Rota.Correr(new Vector3(0f, Y, 246.5f));
    }

    /// <summary>
    /// Três buracos negros: o primeiro, acima do vão longo, puxa para a
    /// frente e para cima e estica o salto de impulso; o segundo puxa de lado
    /// numa passarela estreita; o terceiro segura plataformas em órbita - a
    /// volta é segura, o impulso direto por cima é o atalho arriscado.
    /// </summary>
    static void Anomalias(Transform root)
    {
        Bloco(root, "Ilha1", new Vector3(0f, Y, 254f), 8f, 8f);
        Buraco(root, "Buraco_Estilingue", new Vector3(0f, Y + 6.5f, 271f), 12f, 26f, 1.5f);
        Bloco(root, "Ilha2", new Vector3(0f, Y, 281f), 8f, 8f);
        Bloco(root, "Passarela", new Vector3(0f, Y, 297.5f), 2.5f, 16f);
        Buraco(root, "Buraco_Lateral", new Vector3(7f, Y + 0.8f, 297.5f), 11f, 30f, 1.5f);
        Bloco(root, "Ilha3", new Vector3(0f, Y, 314f), 8f, 8f);

        // Órbita: plataformas presas a um pivô no centro do buraco.
        var centro = new Vector3(0f, Y, 329f);
        Buraco(root, "Buraco_Orbita", centro + Vector3.up * 0.5f, 8f, 18f, 1.6f);
        var pivo = new GameObject("Orbita");
        pivo.transform.SetParent(root, false);
        pivo.transform.position = centro;
        LevelKit.AddKinematicBody(pivo);
        pivo.AddComponent<RelogioLocal>();
        var gira = pivo.AddComponent<PlataformaGiratoria>();
        LevelKit.SetFloat(gira, "grausPorSegundo", 32f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f - 90f) * Mathf.Deg2Rad;
            var p = centro + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 7f;
            var bloco = Bloco(pivo.transform, $"Orbitante{i + 1}", p, 4f, 4f, registrar: false);
            foreach (var t in bloco.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        Bloco(root, "Ilha4", new Vector3(0f, Y, 343f), 8f, 8f);
        PontoDeControle(root, "PontoDeControle3", new Vector3(0f, Y, 343f));

        s_Rota.Saltar(new Vector3(0f, Y, 251f));
        s_Rota.Correr(new Vector3(0f, Y, 257.5f));
        s_Rota.Impulso(new Vector3(0f, Y, 278.5f));
        s_Rota.Correr(new Vector3(0f, Y, 284.5f));
        s_Rota.Saltar(new Vector3(0f, Y, 290.5f));
        s_Rota.Correr(new Vector3(-0.3f, Y, 305f));
        s_Rota.Saltar(new Vector3(0f, Y, 311f));
        s_Rota.Correr(new Vector3(0f, Y, 317.5f));
        // O fantasma arrisca o atalho: mostra que ele existe.
        s_Rota.Impulso(new Vector3(0f, Y, 340.5f));
        s_Rota.Correr(new Vector3(0f, Y, 346.5f));
    }

    /// <summary>
    /// A perseguição: degraus curtos sobre o vazio, dois deles de cristal
    /// frágil. Dentro desta área o fantasma repete os passos da bruxa com
    /// atraso; a rota dele aqui só serve para o medidor.
    /// </summary>
    static Bounds Perseguicao(Transform root)
    {
        float[] zs = { 354f, 361f, 368f, 375f, 382f, 389f, 396f, 403f };
        for (int i = 0; i < zs.Length; i++)
        {
            var p = new Vector3(i % 2 == 0 ? -1f : 1f, Y + (i % 3) * 0.5f, zs[i]);
            if (i == 2 || i == 5) TempoKit.Fragil(root, $"Eco{i + 1}", p, 4f, 4f);
            else Bloco(root, $"Eco{i + 1}", p, 4f, 4f);
            s_Rota.Saltar(p + Vector3.back * 1f);
            s_Rota.Correr(p + Vector3.forward * 1.5f);
        }

        Bloco(root, "Saida", new Vector3(0f, Y, 414f), 8f, 8f);
        PontoDeControle(root, "PontoDeControle4", new Vector3(0f, Y, 415f));
        s_Rota.Saltar(new Vector3(0f, Y, 411f));
        s_Rota.Correr(new Vector3(0f, Y, 417.5f));

        return new Bounds(new Vector3(0f, Y + 4f, 381f), new Vector3(14f, 16f, 54f));
    }

    /// <summary>
    /// A última corrida: teto sobre um buraco negro que puxa para longe dele,
    /// a trilha revelada pelo fantasma (ou o desvio à direita), o impulso
    /// final e a chegada.
    /// </summary>
    static Vector3 UltimaCorrida(Transform root)
    {
        ZonaLateral(root, "ZonaLateralFinal", 421f, 474f);
        Bloco(root, "Final1", new Vector3(0f, Y, 425f), 6f, 6f);
        Portal(root, "Portal_TetoFinal", new Vector3(0f, Y + 4f, 431f), true);
        Bloco(root, "TetoFinal1", new Vector3(0f, Teto, 440f), 6f, 12f, teto: true);
        Buraco(root, "Buraco_Final", new Vector3(0f, Y + 3f, 452f), 9f, 24f, 1.5f);
        Bloco(root, "TetoFinal2", new Vector3(0f, Teto, 456f), 6f, 8f, teto: true);
        Portal(root, "Portal_ChaoFinal", new Vector3(0f, Teto - 4f, 463f), false);
        Bloco(root, "Final2", new Vector3(0f, Y, 469f), 6f, 8f);

        s_Rota.Saltar(new Vector3(0f, Y, 423f));
        s_Rota.Correr(new Vector3(0f, Y, 427.5f));
        s_Rota.Virar(new Vector3(0f, Teto, 438f), true);
        s_Rota.Correr(new Vector3(0f, Teto, 445.5f));
        s_Rota.Saltar(new Vector3(0f, Teto, 453.5f));
        s_Rota.Correr(new Vector3(0f, Teto, 459.5f));
        s_Rota.Virar(new Vector3(0f, Y, 468f), false);
        s_Rota.Correr(new Vector3(0f, Y, 472.5f));

        // Trilha revelada: só existe logo depois que o fantasma pisa.
        float[] rz = { 479f, 486f, 493f };
        for (int i = 0; i < rz.Length; i++)
        {
            var p = new Vector3(0f, Y + i * 0.5f, rz[i]);
            s_Rota.Saltar(p + Vector3.back * 1f);
            var plataforma = Revelada(root, $"Revelada{i + 1}", p, s_Rota.Tempo);
            s_Rota.Correr(p + Vector3.forward * 1.5f);
            Register($"Revelada{i + 1}", p.y, Square(p.x, p.z, 4f));
        }

        // O desvio, sempre sólido: mais saltos, mais longe.
        Bloco(root, "Desvio1", new Vector3(7f, Y, 478f), 4f, 4f);
        Bloco(root, "Desvio2", new Vector3(9f, Y + 1f, 485f), 4f, 4f);
        Bloco(root, "Desvio3", new Vector3(8f, Y + 2f, 492f), 4f, 4f);
        Bloco(root, "Desvio4", new Vector3(4f, Y + 2f, 498f), 4f, 4f);

        Bloco(root, "Final3", new Vector3(0f, Y + 2f, 503f), 8f, 8f);
        Bloco(root, "Final4", new Vector3(0f, Y + 3f, 527f), 6f, 6f);
        Bloco(root, "Chegada", new Vector3(0f, Y + 4f, 540f), 12f, 10f, espessura: 3f);

        s_Rota.Saltar(new Vector3(0f, Y + 2f, 500f));
        s_Rota.Correr(new Vector3(0f, Y + 2f, 506.5f));
        s_Rota.Impulso(new Vector3(0f, Y + 3f, 525f));
        s_Rota.Correr(new Vector3(0f, Y + 3f, 529f));
        s_Rota.Saltar(new Vector3(0f, Y + 4f, 536f));
        s_Rota.Correr(new Vector3(0f, Y + 4f, 540f));

        return new Vector3(0f, Y + 4f, 540f);
    }

    static PlataformaRevelada Revelada(Transform root, string name, Vector3 superficie, float tempo)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = superficie + Vector3.down * 0.4f;
        go.transform.localScale = new Vector3(4f, 0.8f, 4f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = TempoKit.Revelada;
        r.shadowCastingMode = ShadowCastingMode.Off;

        var p = go.AddComponent<PlataformaRevelada>();
        var so = new SerializedObject(p);
        so.FindProperty("tempoDePassagem").floatValue = tempo;
        so.FindProperty("solido").objectReferenceValue = go.GetComponent<Collider>();
        so.FindProperty("imagem").objectReferenceValue = r;
        so.ApplyModifiedPropertiesWithoutUndo();
        return p;
    }

    /// <summary>
    /// O jardim do outro lado do portal: uma ilha calma de grama clara,
    /// arcos verdes e uma ampulheta pequena num altar. Tocar o altar encerra
    /// a campanha.
    /// </summary>
    static Transform Epilogo(Transform root)
    {
        var centro = kEpilogo;
        Decor(root, "Jardim", LowPolyMesh.Prism(LowPolyMesh.Regular(20, 16f), -4f, 0f, 4f, 0.8f), TempoKit.Grama, centro);
        var ilha = new GameObject("ColisaoDoJardim");
        ilha.transform.SetParent(root, false);
        ilha.transform.position = centro + Vector3.down * 2f;
        var box = ilha.AddComponent<BoxCollider>();
        box.size = new Vector3(30f, 4f, 30f);

        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            LevelKit.Place(root, "green", "arch_tall", centro + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 11f, i * 60f);
        }

        // Árvores de cone, só imagem.
        var random = new System.Random(77);
        for (int i = 0; i < 10; i++)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            float r = 6f + (float)random.NextDouble() * 7f;
            var p = centro + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            if (Mathf.Abs(p.x - centro.x) < 2.5f) continue;
            float h = 2.5f + (float)random.NextDouble() * 2f;
            Decor(root, "Arvore", TempoKit.MalhaTorno(new[]
            {
                new Vector2(0.01f, 0f), new Vector2(0.25f, 0f), new Vector2(0.25f, h * 0.35f), new Vector2(1.4f, h * 0.35f),
                new Vector2(0.01f, h),
            }, 7, "Arvore"), TempoKit.Grama, p);
        }

        var altar = centro + new Vector3(0f, 0f, 6f);
        Decor(root, "Altar", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 1.3f, 22.5f), 0f, 1f, 1f, 0.8f), TempoKit.Marfim, altar);
        var pequena = new GameObject("AmpulhetaDoAltar");
        pequena.transform.SetParent(root, false);
        pequena.transform.position = altar + Vector3.up * 2.1f;
        TempoKit.MontarAmpulheta(pequena.transform, 0.9f);

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

        var fimGo = new GameObject("FimDaCampanha");
        fimGo.transform.SetParent(root, false);
        fimGo.transform.position = altar + Vector3.up * 1.5f;
        var area = fimGo.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(5f, 4f, 5f);
        var fim = fimGo.AddComponent<FimDaCampanha>();
        EditorSetupUtil.SetReference(fim, "ampulheta", pequena.transform);
        EditorSetupUtil.SetReference(fim, "foco", pequena.transform);
        EditorSetupUtil.SetReference(fim, "clarao", imagem);
        EditorSetupUtil.SetString(fim, "cenaDosCreditos", CreditsSceneBuilder.SceneName);
        EditorSetupUtil.SetString(fim, "cenaDeResultados", ResultsSceneBuilder.SceneName);
        LevelKit.SetFloat(fim, "subida", 14f);
        LevelKit.SetFloat(fim, "afastamento", 26f);
        LevelKit.SetFloat(fim, "duracao", 9f);

        var destino = new GameObject("ChegadaNoJardim").transform;
        destino.SetParent(root, false);
        destino.SetPositionAndRotation(centro + new Vector3(0f, 0.3f, -10f), Quaternion.identity);
        return destino;
    }

    static void CriarChegada(Transform root, Vector3 chegada, Transform destino)
    {
        var portalGo = new GameObject("PortalDeSaida");
        portalGo.transform.SetParent(root, false);
        portalGo.transform.position = chegada + new Vector3(0f, 2.6f, 4f);
        var anel = new GameObject("Anel");
        anel.transform.SetParent(portalGo.transform, false);
        anel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        anel.AddComponent<MeshFilter>().sharedMesh = TempoKit.MalhaAnel(2.6f, 0.35f, 48);
        anel.AddComponent<MeshRenderer>().sharedMaterial = TempoKit.AnelDaOnda;
        var veu = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(veu.GetComponent<Collider>());
        veu.transform.SetParent(portalGo.transform, false);
        veu.transform.localScale = Vector3.one * 5f;
        veu.GetComponent<MeshRenderer>().sharedMaterial = TempoKit.CascaDaOnda;
        var box = portalGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(5f, 5f, 1.5f);
        var somDaTravessia = portalGo.AddComponent<AudioSource>();
        somDaTravessia.playOnAwake = false;
        somDaTravessia.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shapeforms/travessia.wav");
        var travessia = portalGo.AddComponent<PortalDeTravessia>();
        EditorSetupUtil.SetReference(travessia, "som", somDaTravessia);
        EditorSetupUtil.SetReference(travessia, "destino", destino);
        EditorSetupUtil.SetReference(travessia, "anel", anel.transform);

        var go = new GameObject("ChegadaDaCorrida");
        go.transform.SetParent(root, false);
        go.transform.position = chegada + Vector3.up * 1.5f;
        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(12f, 4f, 3f);
        var som = go.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shapeforms/fantasma_some.wav");
        var chegadaDaCorrida = go.AddComponent<ChegadaDaCorrida>();
        EditorSetupUtil.SetReference(chegadaDaCorrida, "portal", portalGo);
        EditorSetupUtil.SetReference(chegadaDaCorrida, "som", som);
        string vitoria = System.IO.File.Exists("Assets/Audio/" + MusicaDaVitoria) ? MusicaDaVitoria : kVitoriaProvisoria;
        EditorSetupUtil.SetReference(chegadaDaCorrida, "musicaDaVitoria", LevelKit.MusicClip(vitoria));

        LevelKit.Place(root, "blue", "flag_A", chegada + new Vector3(-5f, 0f, 0f), 0f);
        LevelKit.Place(root, "blue", "flag_A", chegada + new Vector3(5f, 0f, 0f), 0f);
    }

    /// <summary>
    /// O fantasma: o mesmo modelo da bruxa, com o animador dela, num corpo
    /// translúcido, com rastro e a nuvem de partículas da dissolução.
    /// </summary>
    static void CriarFantasma(Transform root, Bounds eco)
    {
        var go = new GameObject("Fantasma");
        go.transform.SetParent(root, false);
        go.transform.position = s_Rota.Posicoes[0];

        var orientacao = new GameObject("Orientacao").transform;
        orientacao.SetParent(go.transform, false);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchSetup.PrefabPath);
        var animadorDaBruxa = prefab != null ? prefab.GetComponentInChildren<Animator>() : null;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitCharacterPostprocessor.kCharactersFolder + "/Mage.fbx");
        Animator animator = null;
        var corpo = new List<Renderer>();
        if (fbx != null)
        {
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx, orientacao);
            modelo.name = "Modelo";
            animator = modelo.GetComponent<Animator>() ?? modelo.AddComponent<Animator>();
            if (animadorDaBruxa != null)
            {
                animator.avatar = animadorDaBruxa.avatar;
                animator.runtimeAnimatorController = animadorDaBruxa.runtimeAnimatorController;
            }

            animator.applyRootMotion = false;
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            {
                var materiais = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materiais.Length; i++) materiais[i] = TempoKit.CorpoFantasma;
                r.sharedMaterials = materiais;
                r.shadowCastingMode = ShadowCastingMode.Off;
                corpo.Add(r);
            }
        }

        var rastro = Particulas(go.transform, "Rastro", true);
        var dissolucao = Particulas(go.transform, "Dissolucao", false);

        var fantasma = go.AddComponent<FantasmaDaCorrida>();
        var so = new SerializedObject(fantasma);
        var posicoes = so.FindProperty("posicoes");
        var estados = so.FindProperty("estados");
        posicoes.arraySize = s_Rota.Posicoes.Count;
        estados.arraySize = s_Rota.Estados.Count;
        for (int i = 0; i < s_Rota.Posicoes.Count; i++)
        {
            posicoes.GetArrayElementAtIndex(i).vector3Value = s_Rota.Posicoes[i];
            estados.GetArrayElementAtIndex(i).intValue = s_Rota.Estados[i];
        }

        so.FindProperty("passo").floatValue = Rota.Passo;
        so.FindProperty("supernova").objectReferenceValue = CriarSupernova(root);
        so.FindProperty("areaDoEco").boundsValue = eco;
        so.FindProperty("modelo").objectReferenceValue = orientacao;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("rastro").objectReferenceValue = rastro;
        so.FindProperty("dissolucao").objectReferenceValue = dissolucao;
        var lista = so.FindProperty("corpo");
        lista.arraySize = corpo.Count;
        for (int i = 0; i < corpo.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = corpo[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// A supernova da derrota, pronta e desligada: núcleo, onda de choque,
    /// faíscas, luz, som e o branco de tela inteira (ver <see cref="Supernova"/>).
    /// </summary>
    static Supernova CriarSupernova(Transform root)
    {
        var go = new GameObject("Supernova");
        go.transform.SetParent(root, false);

        var nucleo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(nucleo.GetComponent<Collider>());
        nucleo.name = "Nucleo";
        nucleo.transform.SetParent(go.transform, false);
        nucleo.GetComponent<MeshRenderer>().sharedMaterial = TempoKit.Areia;

        var onda = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(onda.GetComponent<Collider>());
        onda.name = "Onda";
        onda.transform.SetParent(go.transform, false);
        var ondaRenderer = onda.GetComponent<MeshRenderer>();
        ondaRenderer.sharedMaterial = TempoKit.CascaDaOnda;
        ondaRenderer.shadowCastingMode = ShadowCastingMode.Off;

        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(go.transform, false);
        luz.type = LightType.Point;
        luz.color = new Color(0.85f, 0.92f, 1f);
        luz.range = 60f;
        luz.intensity = 2f;

        var faiscasGo = new GameObject("Faiscas");
        faiscasGo.transform.SetParent(go.transform, false);
        var faiscas = faiscasGo.AddComponent<ParticleSystem>();
        var main = faiscas.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.95f, 1f), new Color(1f, 0.8f, 0.5f));
        main.maxParticles = 1500;
        main.useUnscaledTime = true;
        var emissao = faiscas.emission;
        emissao.rateOverTime = 0f;
        emissao.SetBursts(new[] { new ParticleSystem.Burst(0f, 1200) });
        var forma = faiscas.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 1f;
        var pr = faiscasGo.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = TempoKit.Particula;
        pr.shadowCastingMode = ShadowCastingMode.Off;

        var som = go.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.spatialBlend = 0f;

        var canvas = UiBuilder.CreateCanvas("BrancoDaSupernova", sortOrder: 90);
        var branco = new GameObject("Branco").AddComponent<Image>();
        branco.transform.SetParent(canvas.transform, false);
        var rect = branco.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        branco.color = new Color(1f, 1f, 1f, 0f);
        branco.raycastTarget = false;

        var supernova = go.AddComponent<Supernova>();
        EditorSetupUtil.SetReference(supernova, "nucleo", nucleo.transform);
        EditorSetupUtil.SetReference(supernova, "onda", onda.transform);
        EditorSetupUtil.SetReference(supernova, "ondaRenderer", ondaRenderer);
        EditorSetupUtil.SetReference(supernova, "faiscas", faiscas);
        EditorSetupUtil.SetReference(supernova, "luz", luz);
        EditorSetupUtil.SetReference(supernova, "clarao", branco);
        EditorSetupUtil.SetReference(supernova, "som", som);
        go.SetActive(false);
        return supernova;
    }

    static ParticleSystem Particulas(Transform pai, string nome, bool rastro)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = Vector3.up * 0.9f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = rastro;
        main.playOnAwake = rastro;
        main.duration = rastro ? 5f : 2f;
        main.startLifetime = rastro ? 0.8f : 1.6f;
        main.startSpeed = rastro ? 0.3f : 2.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, rastro ? 0.22f : 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.9f, 1f), new Color(0.85f, 0.6f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = rastro ? 0f : -0.15f;
        main.maxParticles = 400;
        var emissao = ps.emission;
        emissao.rateOverTime = rastro ? 30f : 0f;
        if (!rastro) emissao.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });
        var forma = ps.shape;
        forma.shapeType = rastro ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Box;
        forma.radius = 0.4f;
        if (!rastro) forma.scale = new Vector3(0.8f, 1.8f, 0.8f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = TempoKit.Particula;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    /// <summary>
    /// Medidor da corrida, no alto da tela abaixo do cartão de avisos: um
    /// trilho com a bruxa (areia) e o fantasma (ciano), e a vantagem em
    /// segundos.
    /// </summary>
    static void CriarMedidor()
    {
        var theme = ThemedUi.Theme;
        var canvas = UiBuilder.CreateCanvas("Corrida", sortOrder: 6);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        var stone = new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.85f);
        var placa = ThemedUi.Plate(canvas.transform, "Placa", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   new Vector2(0f, -126f), new Vector2(760f, 92f), stone, out var moldura);
        moldura.color = theme.metalDark;

        var trilho = UiBuilder.CreateRect(placa.transform, "Trilho", new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                                          new Vector2(660f, 10f));
        var trilhoImg = trilho.gameObject.AddComponent<Image>();
        trilhoImg.color = new Color(theme.sand.r, theme.sand.g, theme.sand.b, 0.25f);
        trilhoImg.raycastTarget = false;

        RectTransform Marca(string nome, Color cor)
        {
            var m = UiBuilder.CreateRect(trilho, nome, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, 30f));
            var img = m.gameObject.AddComponent<Image>();
            img.color = cor;
            img.raycastTarget = false;
            return m;
        }

        var fantasma = Marca("Fantasma", new Color(0.5f, 0.9f, 1f, 0.9f));
        var bruxa = Marca("Bruxa", theme.gold);

        var texto = ThemedUi.Text(placa.transform, "Diferenca", "", 26f, theme.sand, new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, -22f), new Vector2(640f, 32f));
        ThemedUi.Text(placa.transform, "Legenda", "VOCÊ  ·  FANTASMA", 16f, theme.ash, new Vector2(1f, 0.5f),
                      new Vector2(-70f, -22f), new Vector2(200f, 24f), bold: false);

        var hud = canvas.gameObject.AddComponent<CorridaHud>();
        EditorSetupUtil.SetReference(hud, "trilho", trilho);
        EditorSetupUtil.SetReference(hud, "marcaDaBruxa", bruxa);
        EditorSetupUtil.SetReference(hud, "marcaDoFantasma", fantasma);
        EditorSetupUtil.SetReference(hud, "diferenca", texto);
        EditorSetupUtil.SetReference(hud, "grupo", grupo);
    }

    /// <summary>
    /// Abertura: tela preta por cima de tudo e "Vença a si mesmo" escrito
    /// letra por letra (ver <see cref="IntroDaCorrida"/>).
    /// </summary>
    static void CriarAbertura()
    {
        var canvas = UiBuilder.CreateCanvas("Abertura", sortOrder: 70);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var tela = UiBuilder.CreateStretched(canvas.transform, "TelaPreta");
        var fundo = tela.gameObject.AddComponent<Image>();
        fundo.color = Color.black;
        var grupo = tela.gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;

        var texto = ThemedUi.Text(tela, "Frase", "", 64f, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                                  new Vector2(1400f, 120f), spacing: 6f);
        texto.fontSizeMin = 64f;

        var som = canvas.gameObject.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.spatialBlend = 0f;
        som.ignoreListenerPause = true;

        var intro = canvas.gameObject.AddComponent<IntroDaCorrida>();
        EditorSetupUtil.SetReference(intro, "tela", grupo);
        EditorSetupUtil.SetReference(intro, "texto", texto);
        EditorSetupUtil.SetReference(intro, "som", som);
    }

    /// <summary>Ilhas geométricas e anéis ao longe, sem colisão: o espaço em volta da corrida.</summary>
    static void Cenario(Transform root)
    {
        var random = new System.Random(808);
        for (int i = 0; i < 40; i++)
        {
            float z = -40f + i * 15f + (float)random.NextDouble() * 10f;
            float lado = i % 2 == 0 ? -1f : 1f;
            float x = lado * (30f + (float)random.NextDouble() * 60f);
            float y = Y - 25f + (float)random.NextDouble() * 60f;
            float s = 3f + (float)random.NextDouble() * 10f;

            if (i % 4 == 0)
            {
                var anel = new GameObject("AnelDistante");
                anel.transform.SetParent(root, false);
                anel.transform.SetPositionAndRotation(new Vector3(x, y, z),
                    Quaternion.Euler((float)random.NextDouble() * 180f, (float)random.NextDouble() * 180f, 0f));
                anel.AddComponent<MeshFilter>().sharedMesh = TempoKit.MalhaAnel(s, 0.3f, 40);
                anel.AddComponent<MeshRenderer>().sharedMaterial = i % 8 == 0 ? TempoKit.AnelVioleta : TempoKit.AnelCiano;
                continue;
            }

            var pedra = Solid(root, "IlhaDistante",
                              LowPolyMesh.Prism(LowPolyMesh.Rock(6, s, 0.6f, random), -s * 1.4f, 0f, 4f, 0.3f),
                              TempoKit.PedraCosmica, TempoKit.PedraCosmica, new Vector3(x, y, z), (float)random.NextDouble() * 360f);
            pedra.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (random.NextDouble() < 0.35)
                pedra.transform.rotation = Quaternion.Euler(180f, (float)random.NextDouble() * 360f, 0f);
        }
    }

    static void CriarCamera(Vector3 largada)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;
        const float pitch = 26f, distancia = 18f;
        LevelKit.SetFloat(camera, "fixedYaw", 0f);
        LevelKit.SetFloat(camera, "fixedPitch", pitch);
        LevelKit.SetFloat(camera, "fixedDistance", distancia);
        var rotation = Quaternion.Euler(pitch, 0f, 0f);
        camera.transform.SetPositionAndRotation(largada + Vector3.up * 1.4f - rotation * Vector3.forward * distancia, rotation);
        var cam = camera.GetComponent<Camera>();
        if (cam != null) cam.farClipPlane = 500f;
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    static void Validar()
    {
        var report = JumpReport.Begin("A Última Corrida");
        if (report == null) return;

        report.Route("Encontro", "Inicio", "Passo1", "Passo2", "Passo3", "Virada");
        report.Route("Teto (medido no sentido da gravidade)", "Teto1", "Teto2", "Teto3");
        report.Route("Lateral, chão", "Lado2", "LadoPisca", "Lado3");
        report.Route("Lateral, teto", "LadoTeto1", "LadoTeto2");
        report.Route("Anomalias", "Ilha2", "Passarela", "Ilha3");
        TempoKit.SaltoComImpulso(report, "Ilha1", "Ilha2", exigido: false);
        TempoKit.SaltoComImpulso(report, "Ilha3", "Ilha4", exigido: false);
        report.Route("Perseguição", "Ilha4", "Eco1", "Eco2", "Eco3", "Eco4", "Eco5", "Eco6", "Eco7", "Eco8", "Saida");
        report.Route("Trilha revelada", "Final2", "Revelada1", "Revelada2", "Revelada3", "Final3");
        report.Route("Desvio", "Final2", "Desvio1", "Desvio2", "Desvio3", "Desvio4", "Final3");
        report.Route("Teto final", "TetoFinal1", "TetoFinal2");
        TempoKit.SaltoComImpulso(report, "Final3", "Final4", exigido: false);
        report.Route("Chegada", "Final4", "Chegada");
        report.Note($"Fantasma: {s_Rota.Tempo:0.0} s de rota, {s_Rota.Posicoes.Count} amostras");
        report.Finish();
    }
}
