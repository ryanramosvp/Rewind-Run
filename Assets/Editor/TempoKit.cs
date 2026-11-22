using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static VolcanicKit;

/// <summary>
/// Peças das duas fases finais, as do tempo: Relojoaria Suspensa e Coração do
/// Tempo.
///
/// Fica separado do <see cref="VolcanicKit"/> porque aqui moram as mecânicas
/// novas (zonas de âmbar, cristais de contratempo, cristais frágeis,
/// plataformas que piscam, engrenagens que giram, pêndulos, pontos de
/// controle) e a arte de relojoaria que só essas fases usam. O que é comum -
/// placas, contornos, a validação dos saltos - continua vindo de lá, para as
/// contas de salto serem uma só no projeto inteiro.
///
/// Cada função devolve a peça já ligada ao seu script, com os campos
/// preenchidos: o construtor da fase só diz onde e quanto.
/// </summary>
public static class TempoKit
{
    const string Folder = "Assets/Art/Generated/Relojoaria";

    public static Material Bronze { get; private set; }
    public static Material BronzeEscuro { get; private set; }
    public static Material Marfim { get; private set; }
    public static Material Ferro { get; private set; }
    public static Material Cristal { get; private set; }
    public static Material CristalFragilMat { get; private set; }
    public static Material Piscante { get; private set; }
    public static Material Ambar { get; private set; }
    public static Material AnelAmbar { get; private set; }
    public static Material Fantasma { get; private set; }
    public static Material Vidro { get; private set; }
    public static Material Areia { get; private set; }
    public static Material LuzAmbar { get; private set; }
    public static Material CeuNoturno { get; private set; }
    public static Material Particula { get; private set; }

    /// <summary>Cria ou atualiza os materiais. Idempotente.</summary>
    public static void Ensure()
    {
        LavaLetalArt.Ensure();
        System.IO.Directory.CreateDirectory(Folder);

        // A chapa xadrez da Lava Letal, tingida: o mesmo mundo, outra liga.
        var chapa = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/LavaLetal/chapa_xadrez.png");
        var rocha = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/LavaLetal/rocha_clara.png");

        Bronze = Lit("Bronze", chapa, new Color(0.86f, 0.62f, 0.32f), smoothness: 0.45f);
        BronzeEscuro = Lit("BronzeEscuro", rocha, new Color(0.42f, 0.3f, 0.22f), smoothness: 0.3f);
        Marfim = Lit("Marfim", null, new Color(0.93f, 0.88f, 0.76f), smoothness: 0.2f);
        Ferro = Lit("Ferro", null, new Color(0.16f, 0.15f, 0.2f), smoothness: 0.5f);

        // Cristais acesos: são o que a bruxa pisa no escuro, e a cor diz o que
        // cada um faz - ciano anda no rewind, rosa quebra, violeta pisca.
        Cristal = Lit("CristalContratempo", null, new Color(0.3f, 0.85f, 1f),
                      emissionColor: new Color(0.25f, 1.1f, 1.5f), smoothness: 0.8f);
        CristalFragilMat = Lit("CristalFragil", null, new Color(1f, 0.55f, 0.75f),
                               emissionColor: new Color(1.1f, 0.35f, 0.6f), smoothness: 0.8f);
        Piscante = Lit("Piscante", null, new Color(0.7f, 0.5f, 1f),
                       emissionColor: new Color(0.8f, 0.45f, 1.6f), smoothness: 0.6f);
        LuzAmbar = Lit("LuzAmbar", null, new Color(1f, 0.7f, 0.25f), emissionColor: new Color(2.2f, 1.3f, 0.35f));
        Areia = Lit("AreiaDoTempo", null, new Color(1f, 0.8f, 0.4f), emissionColor: new Color(1.6f, 1.1f, 0.4f));

        Ambar = Transparente("ZonaAmbar", new Color(1f, 0.65f, 0.15f, 0.16f), aditivo: false);
        AnelAmbar = Transparente("AnelAmbar", new Color(1f, 0.7f, 0.2f, 0.85f), aditivo: true);
        Fantasma = Transparente("FantasmaCristal", new Color(0.35f, 0.9f, 1f, 0.22f), aditivo: true);
        Vidro = Transparente("Vidro", new Color(0.75f, 0.9f, 1f, 0.18f), aditivo: false);

        CeuNoturno = Ceu(Textura("ceu_noturno", 256, 256, PixelDoCeu));

        // Grão de areia: partícula sem luz e somada, para brilhar contra o vidro.
        Particula = Carregar("GraoDeAreia", "Universal Render Pipeline/Particles/Unlit");
        Particula.SetColor("_BaseColor", new Color(1f, 0.82f, 0.45f, 1f));
        Particula.SetFloat("_Surface", 1f);
        Particula.SetFloat("_Blend", 2f);
        Particula.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        Particula.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        Particula.SetFloat("_ZWrite", 0f);
        Particula.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        Particula.SetOverrideTag("RenderType", "Transparent");
        Particula.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(Particula);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Céu violeta de noite, névoa da mesma cor e luz ambiente fria, para a
    /// Relojoaria: a primeira fase fora do vermelho do vulcão. O vulcão ainda
    /// está lá embaixo - a lava acende o chão da névoa - mas a fase acontece
    /// acima da fumaça.
    /// </summary>
    public static void AplicarNoite()
    {
        if (CeuNoturno == null) Ensure();

        RenderSettings.skybox = CeuNoturno;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.3f, 0.6f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.28f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.55f, 0.2f, 0.12f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.27f, 0.17f, 0.38f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 230f;
    }

    /// <summary>Luz da lua: fria e baixa, vinda de trás da câmera inicial.</summary>
    public static void LuzDaLua(float yaw)
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 90f, 0f), Quaternion.Euler(48f, yaw, 0f));

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = new Color(0.85f, 0.84f, 1f);
        light.shadows = LightShadows.Soft;
    }

    // ------------------------------------------------------------------
    // Peças do percurso
    // ------------------------------------------------------------------

    /// <summary>
    /// Ilha-engrenagem: disco dentado de bronze, com o topo em
    /// <paramref name="topo"/>. Registrada para a validação pelo círculo de
    /// dentro dos dentes - o vão medido nunca é menor que o real.
    /// </summary>
    public static GameObject Engrenagem(Transform root, string name, Vector3 topo, float raio, float espessura = 1.2f,
                                        int dentes = 0, Material tampo = null, bool rota = true)
    {
        if (dentes <= 0) dentes = Mathf.Max(8, Mathf.RoundToInt(raio * 3.2f));
        var mesh = MalhaEngrenagem(raio, dentes, -espessura, 0f, name);

        var go = Solid(root, name, mesh, tampo != null ? tampo : Bronze, BronzeEscuro, topo, 0f);
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        // Eixo central escuro, só para ler como peça de relógio.
        var cubo = Solid(go.transform, "Eixo", LowPolyMesh.Prism(LowPolyMesh.Regular(6, raio * 0.18f), 0f, 0.12f, 1f),
                         Ferro, Ferro, Vector3.zero, 0f);
        cubo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (rota) Register(name, topo.y, World(go.transform, LowPolyMesh.Regular(16, raio, 0f)));
        return go;
    }

    /// <summary>Placa de bronze retangular sem faixas nem pés: chão de relojoaria.</summary>
    public static GameObject Placa(Transform root, string name, Vector3 topo, float x, float z, float yaw = 0f,
                                   float espessura = 0.8f)
    {
        return Plate(root, name, topo.x, topo.z, topo.y, x, z, yaw, Bronze, trim: false, supports: false,
                     thickness: espessura);
    }

    /// <summary>
    /// Plataforma que some e volta num ritmo, com o relógio local: numa zona
    /// de âmbar o ritmo inteiro desacelera.
    /// </summary>
    public static GameObject PlataformaPiscante(Transform root, string name, Vector3 topo, float x, float z,
                                                float visivel, float sumida, float fase)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = topo;

        var visual = Solid(go.transform, "Visual", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x, z), -0.5f, 0f, 2f),
                           Piscante, Ferro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(visual, 0);

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.25f, 0f);
        box.size = new Vector3(x, 0.5f, z);

        var relogio = go.AddComponent<RelogioLocal>();
        SetEnum(relogio, "canal", (int)CanalTemporal.Plataformas);

        var piscante = go.AddComponent<PlataformaPiscante>();
        LevelKit.SetFloat(piscante, "segundosVisivel", visivel);
        LevelKit.SetFloat(piscante, "segundosSumida", sumida);
        LevelKit.SetFloat(piscante, "fase", fase);
        EditorSetupUtil.SetReference(piscante, "visual", visual);
        EditorSetupUtil.SetReference(piscante, "solido", box);

        Register(name, topo.y, World(go.transform, LowPolyMesh.Rectangle(x, z)));
        return go;
    }

    /// <summary>
    /// Cristal de contratempo: parado no tempo normal, anda o
    /// <paramref name="percurso"/> só enquanto a bruxa volta no tempo, e fica
    /// onde parou.
    ///
    /// O destino aparece como um fantasma translúcido com uma trilha de
    /// pontos até ele: a pergunta "o que o rewind faz com isto?" fica
    /// respondida antes de se apertar o botão.
    /// </summary>
    public static GameObject Contratempo(Transform root, string name, Vector3 topo, float x, float z,
                                         Vector3 percurso, float segundos, float yaw = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.SetLocalPositionAndRotation(topo, Quaternion.Euler(0f, yaw, 0f));

        var mesh = LowPolyMesh.Prism(LowPolyMesh.Regular(6, 1f, 30f), -0.6f, 0f, 2f);
        var corpo = Solid(go.transform, "Cristal", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x, z), -0.6f, 0f, 2f),
                          Cristal, Ferro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(corpo, 0);

        var brilho = Solid(go.transform, "Brilho", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x + 0.5f, z + 0.5f), -0.75f, 0.06f, 2f),
                           Fantasma, Fantasma, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(brilho, 0);
        var brilhoRenderer = brilho.GetComponent<MeshRenderer>();
        brilhoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        brilhoRenderer.enabled = false;

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.3f, 0f);
        box.size = new Vector3(x, 0.6f, z);
        LevelKit.AddKinematicBody(go);

        var plataforma = go.AddComponent<PlataformaContratempo>();
        LevelKit.SetVector(plataforma, "percurso", percurso);
        LevelKit.SetFloat(plataforma, "segundosDeRewind", segundos);
        EditorSetupUtil.SetReference(plataforma, "brilho", brilhoRenderer);

        // Fantasma e trilha ficam fora do objeto que anda: marcam o mundo.
        var marcas = Group(root, name + "_Destino");
        var fantasma = Solid(marcas, "Fantasma", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x, z), -0.6f, 0f, 2f),
                             Fantasma, Fantasma, topo + percurso, yaw);
        fantasma.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        int pontos = Mathf.Max(2, Mathf.RoundToInt(percurso.magnitude / 1.5f));
        for (int i = 1; i < pontos; i++)
        {
            var p = topo + percurso * (i / (float)pontos) + Vector3.down * 0.3f;
            var ponto = Solid(marcas, "Ponto", mesh, Fantasma, Fantasma, p, 0f);
            ponto.transform.localScale = Vector3.one * 0.18f;
            ponto.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Register(name, topo.y, World(go.transform, LowPolyMesh.Rectangle(x, z)));
        var fim = World(go.transform, LowPolyMesh.Rectangle(x, z));
        for (int i = 0; i < fim.Length; i++) fim[i] += new Vector2(percurso.x, percurso.z);
        FarEnds[name] = (topo.y + percurso.y, fim);
        return go;
    }

    /// <summary>Cristal frágil: quebra pouco depois de pisado e volta com o rewind.</summary>
    public static GameObject Fragil(Transform root, string name, Vector3 topo, float x, float z, float yaw = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.SetLocalPositionAndRotation(topo, Quaternion.Euler(0f, yaw, 0f));

        var visual = Solid(go.transform, "Visual", LowPolyMesh.Prism(LowPolyMesh.Rectangle(x, z), -0.45f, 0f, 2f, 0.9f),
                           CristalFragilMat, CristalFragilMat, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(visual, 0);

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.225f, 0f);
        box.size = new Vector3(x, 0.45f, z);

        var som = Fonte3D(go, 3f, 30f);
        var cristal = go.AddComponent<CristalFragil>();
        EditorSetupUtil.SetReference(cristal, "visual", visual);
        EditorSetupUtil.SetReference(cristal, "solido", box);
        EditorSetupUtil.SetReference(cristal, "som", som);

        Register(name, topo.y, World(go.transform, LowPolyMesh.Rectangle(x, z)));
        return go;
    }

    /// <summary>
    /// Ponteiro de relógio que gira em volta de <paramref name="eixo"/> e
    /// carrega a bruxa: uma barra de <paramref name="comprimento"/> metros que
    /// começa em <paramref name="inicio"/> metros do eixo.
    /// </summary>
    public static GameObject Ponteiro(Transform root, string name, Vector3 eixo, float inicio, float comprimento,
                                      float largura, float grausPorSegundo, float anguloInicial, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = eixo;

        var barra = LowPolyMesh.Prism(new[]
        {
            new Vector2(-largura * 0.5f, inicio), new Vector2(largura * 0.5f, inicio),
            new Vector2(largura * 0.3f, inicio + comprimento), new Vector2(-largura * 0.3f, inicio + comprimento),
        }, -0.4f, 0f, 2f, 1f, name);
        var visual = Solid(go.transform, "Barra", barra, material, Ferro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(visual, 0);

        var collider = visual.AddComponent<MeshCollider>();
        collider.sharedMesh = barra;
        collider.convex = true;
        LevelKit.AddKinematicBody(go);

        go.AddComponent<RelogioLocal>();
        var gira = go.AddComponent<PlataformaGiratoria>();
        LevelKit.SetFloat(gira, "grausPorSegundo", grausPorSegundo);
        LevelKit.SetFloat(gira, "anguloInicial", anguloInicial);
        return go;
    }

    /// <summary>
    /// Engrenagem que gira no lugar e carrega quem está em cima. O relógio é
    /// medido no eixo: dentro de uma zona, a engrenagem inteira desacelera.
    /// </summary>
    public static GameObject EngrenagemGiratoria(Transform root, string name, Vector3 topo, float raio,
                                                 float grausPorSegundo)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = topo;

        var mesh = MalhaEngrenagem(raio, Mathf.Max(8, Mathf.RoundToInt(raio * 3.2f)), -1f, 0f, name);
        var visual = Solid(go.transform, "Disco", mesh, Bronze, BronzeEscuro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(visual, 0);
        var collider = visual.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        // Raios de ferro no tampo: sem eles não se vê a engrenagem girar.
        for (int i = 0; i < 3; i++)
        {
            var raioVisual = Solid(go.transform, "Raio", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.35f, raio * 1.7f), 0f, 0.05f, 1f),
                                   Ferro, Ferro, Vector3.zero, i * 60f);
            GameObjectUtility.SetStaticEditorFlags(raioVisual, 0);
            raioVisual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        LevelKit.AddKinematicBody(go);
        go.AddComponent<RelogioLocal>();
        var gira = go.AddComponent<PlataformaGiratoria>();
        LevelKit.SetFloat(gira, "grausPorSegundo", grausPorSegundo);

        Register(name, topo.y, World(go.transform, LowPolyMesh.Regular(16, raio, 0f)));
        return go;
    }

    /// <summary>
    /// Zona de âmbar: esfera translúcida, três anéis girando e o zumbido.
    /// O tamanho visual é o raio de verdade - o que se vê é onde vale.
    /// </summary>
    public static GameObject Zona(Transform root, string name, Vector3 centro, float raio, float escala,
                                  CanalTemporal canais)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = centro;

        var zona = go.AddComponent<ZonaTemporal>();
        LevelKit.SetFloat(zona, "raio", raio);
        LevelKit.SetFloat(zona, "escala", escala);
        SetEnum(zona, "canais", (int)canais);

        var esfera = new GameObject("Esfera");
        esfera.transform.SetParent(go.transform, false);
        esfera.transform.localScale = Vector3.one * raio * 2f;
        esfera.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        var esferaRenderer = esfera.AddComponent<MeshRenderer>();
        esferaRenderer.sharedMaterial = Ambar;
        esferaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        esferaRenderer.receiveShadows = false;

        var aneis = new List<Transform>();
        var inclinacoes = new[] { new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0f, 0f, 90f) };
        for (int i = 0; i < 3; i++)
        {
            var anel = new GameObject("Anel");
            anel.transform.SetParent(go.transform, false);
            anel.transform.localRotation = Quaternion.Euler(inclinacoes[i]);
            anel.AddComponent<MeshFilter>().sharedMesh = MalhaAnel(raio * (0.98f - i * 0.04f), 0.08f, 40);
            var r = anel.AddComponent<MeshRenderer>();
            r.sharedMaterial = AnelAmbar;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aneis.Add(anel.transform);
        }

        var zumbido = Fonte3D(go, raio * 0.8f, raio * 1.8f);
        var visual = go.AddComponent<VisualZonaTemporal>();
        EditorSetupUtil.SetReferences(visual, "aneis", aneis.ToArray());
        EditorSetupUtil.SetReference(visual, "esfera", esfera.transform);
        EditorSetupUtil.SetReference(visual, "zumbido", zumbido);
        return go;
    }

    /// <summary>
    /// Hélice da fase 1 com relógio local: a zona de âmbar que a cobre a deixa
    /// lenta. O relógio fica na raiz (que não gira) e é medido nela.
    /// </summary>
    public static GameObject HeliceComRelogio(Transform root, string name, Vector3 posicao, float grausPorSegundo,
                                              float fase, int bracos = 2)
    {
        var go = LevelKit.Blade(root, posicao, grausPorSegundo, fase, bracos);
        go.name = name;
        var relogio = go.AddComponent<RelogioLocal>();
        SetEnum(relogio, "canal", (int)CanalTemporal.Obstaculos);
        return go;
    }

    /// <summary>
    /// Pêndulo pendurado num arco de ferro: eixo em <paramref name="pivo"/>,
    /// haste até o peso, que mata. Balança no eixo X do mundo atravessando
    /// um caminho que corre em Z.
    /// </summary>
    public static GameObject Pendulo(Transform root, string name, Vector3 pivo, float comprimento, float amplitude,
                                     float periodo, float fase, float raioDoPeso = 1.2f)
    {
        var suporte = Group(root, name + "_Arco");
        var viga = LowPolyMesh.Prism(LowPolyMesh.Rectangle(10f, 0.6f), -0.6f, 0f, 2f);
        Solid(suporte, "Viga", viga, Ferro, Ferro, pivo + Vector3.up * 0.3f, 0f);

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pivo;

        var haste = Solid(go.transform, "Haste", LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.25f, 0.25f), -comprimento, 0f, 2f),
                          Ferro, Ferro, Vector3.zero, 0f);
        GameObjectUtility.SetStaticEditorFlags(haste, 0);

        var peso = new GameObject("Peso");
        peso.transform.SetParent(go.transform, false);
        peso.transform.localPosition = Vector3.down * comprimento;
        var disco = Solid(peso.transform, "Disco",
                          LowPolyMesh.Prism(LowPolyMesh.Regular(10, raioDoPeso), -0.35f, 0.35f, 2f),
                          Bronze, BronzeEscuro, Vector3.zero, 0f);
        // O disco de pé, de frente para o caminho: o peso de um relógio de parede.
        disco.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObjectUtility.SetStaticEditorFlags(disco, 0);

        var area = peso.AddComponent<SphereCollider>();
        area.radius = raioDoPeso * 0.9f;
        area.isTrigger = true;
        peso.AddComponent<GatilhoDoPendulo>();
        LevelKit.AddKinematicBody(peso);

        var relogio = go.AddComponent<RelogioLocal>();
        SetEnum(relogio, "canal", (int)CanalTemporal.Obstaculos);
        var pendulo = go.AddComponent<Pendulo>();
        LevelKit.SetFloat(pendulo, "amplitude", amplitude);
        LevelKit.SetFloat(pendulo, "periodo", periodo);
        LevelKit.SetFloat(pendulo, "fase", fase);
        LevelKit.SetVector(pendulo, "eixo", Vector3.forward);
        EditorSetupUtil.SetReference(pendulo, "peso", area);
        return go;
    }

    /// <summary>
    /// Ponto de controle: uma ampulheta pequena num pedestal. Ao ser tocada,
    /// vira e acende; a bruxa passa a renascer ali.
    /// </summary>
    public static GameObject PontoDeControle(Transform root, string name, Vector3 chao, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.SetLocalPositionAndRotation(chao, Quaternion.Euler(0f, yaw, 0f));

        Decor(go.transform, "Pedestal", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 0.7f, 22.5f), 0f, 0.3f, 1f, 0.85f),
              BronzeEscuro, Vector3.zero);

        var ampulheta = new GameObject("Ampulheta");
        ampulheta.transform.SetParent(go.transform, false);
        ampulheta.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        MontarAmpulheta(ampulheta.transform, 0.85f);

        var brilho = Solid(go.transform, "Brilho", MalhaAnel(1.1f, 0.12f, 24), LuzAmbar, LuzAmbar,
                           new Vector3(0f, 0.35f, 0f), 0f);
        GameObjectUtility.SetStaticEditorFlags(brilho, 0);
        var brilhoRenderer = brilho.GetComponent<MeshRenderer>();
        brilhoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        brilhoRenderer.enabled = false;

        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 1.5f, 0f);
        area.size = new Vector3(4f, 3f, 4f);

        var som = Fonte3D(go, 4f, 30f);
        var ponto = go.AddComponent<PontoDeControle>();
        EditorSetupUtil.SetReference(ponto, "ampulheta", ampulheta.transform);
        EditorSetupUtil.SetReference(ponto, "brilho", brilhoRenderer);
        EditorSetupUtil.SetReference(ponto, "som", som);
        LevelKit.SetVector(ponto, "pontoDeRenascer", new Vector3(0f, 0.3f, -1.6f));
        return go;
    }

    /// <summary>
    /// Monta uma ampulheta de <paramref name="altura"/> metros de meia altura
    /// em volta da origem: tampas, colunas, vidro e areia.
    /// </summary>
    public static void MontarAmpulheta(Transform pai, float altura)
    {
        float h = altura, r = altura * 0.62f;
        var tampa = LowPolyMesh.Prism(LowPolyMesh.Regular(8, r * 1.15f, 22.5f), -h * 0.1f, h * 0.1f, 1f);
        Parte(pai, "TampaDeCima", tampa, Bronze, new Vector3(0f, h, 0f));
        Parte(pai, "TampaDeBaixo", tampa, Bronze, new Vector3(0f, -h, 0f));

        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f + 0.5f;
            var coluna = LowPolyMesh.Prism(LowPolyMesh.Regular(4, h * 0.05f, 45f), -h, h, 1f);
            Parte(pai, "Coluna", coluna, BronzeEscuro, new Vector3(Mathf.Cos(a) * r * 1.02f, 0f, Mathf.Sin(a) * r * 1.02f));
        }

        var vidro = MalhaTorno(new[]
        {
            new Vector2(r * 0.15f, -h * 0.92f), new Vector2(r * 0.85f, -h * 0.75f), new Vector2(r * 0.9f, -h * 0.4f),
            new Vector2(r * 0.12f, 0f),
            new Vector2(r * 0.9f, h * 0.4f), new Vector2(r * 0.85f, h * 0.75f), new Vector2(r * 0.15f, h * 0.92f),
        }, 10, "Vidro");
        Parte(pai, "Vidro", vidro, Vidro, Vector3.zero);

        var areia = MalhaTorno(new[]
        {
            new Vector2(0.01f, -h * 0.9f), new Vector2(r * 0.78f, -h * 0.74f), new Vector2(r * 0.6f, -h * 0.45f),
            new Vector2(0.01f, -h * 0.3f),
        }, 10, "Areia");
        Parte(pai, "Areia", areia, Areia, Vector3.zero);
    }

    static GameObject Parte(Transform pai, string name, Mesh mesh, Material material, Vector3 posicao)
    {
        var go = new GameObject(name);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posicao;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        if (material == Vidro) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>Gatilho de aviso: mostra a dica uma vez, ao entrar.</summary>
    public static void Aviso(Transform root, string name, Bounds area, string teclado, string controle,
                             float segundos = 5f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = area.center;

        var box = go.AddComponent<BoxCollider>();
        box.size = area.size;
        box.isTrigger = true;

        var aviso = go.AddComponent<AvisoNaFase>();
        EditorSetupUtil.SetString(aviso, "textoTeclado", teclado);
        EditorSetupUtil.SetString(aviso, "textoControle", controle);
        LevelKit.SetFloat(aviso, "segundos", segundos);
    }

    /// <summary>
    /// Fundo do vazio: uma caixa que mata quem cai. As fases do tempo ficam
    /// no alto, e cair até a lava levaria segundos de queda sem nada a fazer.
    /// </summary>
    public static void Vazio(Transform root, Vector3 centro, Vector3 tamanho)
    {
        var go = new GameObject("FundoDoVazio");
        go.transform.SetParent(root, false);
        go.transform.localPosition = centro;
        var box = go.AddComponent<BoxCollider>();
        box.size = tamanho;
        box.isTrigger = true;
        go.AddComponent<KillZone>();
    }

    public static GameObject ZonaDeCamera(Transform root, string name, Bounds area, float yaw, float pitch,
                                          float distancia) =>
        KayKitPlacer.CreateCameraZone(root, "Zona_" + name, area, yaw, pitch, distancia);

    /// <summary>Liga o impulso no ar na bruxa desta cena: só as fases novas o têm.</summary>
    public static void LiberarImpulso(GameObject player)
    {
        if (player == null) return;
        var controller = player.GetComponent<PlayerController>();
        var so = new SerializedObject(controller);
        so.FindProperty("dashLiberado").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static AudioSource Fonte3D(GameObject go, float min, float max)
    {
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = min;
        source.maxDistance = max;
        source.dopplerLevel = 0f;
        return source;
    }

    public static void SetEnum(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Validação com o impulso
    // ------------------------------------------------------------------

    public const float DashSpeed = 24f, DashSeconds = 0.18f, DashExit = 12.5f;

    /// <summary>
    /// Alcance com o impulso disparado no melhor instante, com ou sem o pulo
    /// duplo (disparado a 0,5 s, como na validação comum). Mesma física do
    /// PlayerController: reta a 24 m/s por 0,18 s sem gravidade, e depois
    /// 12,5 m/s caindo.
    /// </summary>
    public static float AlcanceComImpulso(JumpModel jump, float dy, bool duplo)
    {
        float melhor = 0f;
        for (float aos = 0.2f; aos <= 1.6f; aos += 0.05f)
            melhor = Mathf.Max(melhor, Voo(jump, dy, duplo, aos));
        return melhor;
    }

    static float Voo(JumpModel jump, float dy, bool duplo, float impulsoAos)
    {
        const float dt = 1f / 240f;
        float v = Mathf.Sqrt(2f * jump.Gravity * jump.Jump), y = 0f, x = 0f, t = 0f;
        float velocidade = jump.Speed, impulso = -1f;
        bool dobrou = !duplo;

        while (t < 6f)
        {
            if (!dobrou && t >= 0.5f)
            {
                v = Mathf.Sqrt(2f * jump.Gravity * jump.DoubleJump);
                dobrou = true;
            }

            if (impulso < 0f && t >= impulsoAos) impulso = DashSeconds;

            if (impulso > 0f)
            {
                impulso -= dt;
                v = 0f;
                x += DashSpeed * dt;
                if (impulso <= 0f)
                {
                    impulso = 0f;
                    v = -0.5f;
                    velocidade = Mathf.Max(DashExit, jump.Speed);
                }
            }
            else
            {
                float scale = v < 0f ? jump.Fall : 1f;
                if (Mathf.Abs(v) < jump.ApexThreshold) scale *= jump.Apex;
                v -= jump.Gravity * scale * dt;
                x += velocidade * dt;
            }

            y += v * dt;
            t += dt;
            if (v < 0f && y <= dy) return x;
        }

        return 0f;
    }

    /// <summary>
    /// Confere um salto que conta com o impulso. <paramref name="exigido"/>
    /// diz que o vão precisa ser impossível sem ele (o impulso é a lição) e
    /// folgado com ele; senão só precisa fechar com ele.
    /// </summary>
    public static void SaltoComImpulso(JumpReport report, string de, string para, bool exigido)
    {
        if (report == null) return;
        if (!Footprints.TryGetValue(de, out var a) || !Footprints.TryGetValue(para, out var b))
        {
            report.Note($"{de} -> {para}: peça não registrada", true);
            return;
        }

        float melhorGap = float.MaxValue, dy = 0f;
        foreach (var f in Variants(de, a))
        foreach (var t in Variants(para, b))
        {
            float g = Gap(f.poly, t.poly);
            if (g < melhorGap) { melhorGap = g; dy = t.top - f.top; }
        }

        var semImpulso = Judge(report.Jump, melhorGap, dy);
        float alcance = Mathf.Max(AlcanceComImpulso(report.Jump, dy, false), AlcanceComImpulso(report.Jump, dy, true));
        float precisa = melhorGap + 0.8f;
        bool fecha = precisa <= 0.85f * alcance;
        bool ok = fecha && (!exigido || semImpulso.rank >= 3);

        string texto = $"  {de} -> {para}: vão {melhorGap:0.0} m, desnível {dy:+0.0;-0.0;0.0} m; " +
                       $"sem impulso: {semImpulso.text}; com impulso alcança {alcance:0.0} m - " +
                       (ok ? "ok" : !fecha ? "NÃO FECHA COM IMPULSO" : "PASSA SEM O IMPULSO");
        report.Note(texto.TrimStart(), !ok);
    }

    // ------------------------------------------------------------------
    // Malhas
    // ------------------------------------------------------------------

    /// <summary>
    /// Disco dentado. Os dentes ocupam a metade de cada passo e saem 0,45 m.
    /// O anel é estrelado em volta do centro, então o tampo em leque fecha.
    /// </summary>
    public static Mesh MalhaEngrenagem(float raio, int dentes, float baixo, float alto, string name)
    {
        var anel = new List<Vector2>();
        float passo = Mathf.PI * 2f / dentes;
        float dente = 0.45f;
        for (int i = 0; i < dentes; i++)
        {
            float a = i * passo;
            anel.Add(Polar(raio, a));
            anel.Add(Polar(raio + dente, a + passo * 0.12f));
            anel.Add(Polar(raio + dente, a + passo * 0.38f));
            anel.Add(Polar(raio, a + passo * 0.5f));
        }

        var b = new Malha();
        var cTop = new Vector3(0f, alto, 0f);
        var cBottom = new Vector3(0f, baixo, 0f);
        for (int i = 0; i < anel.Count; i++)
        {
            var p0 = anel[i];
            var p1 = anel[(i + 1) % anel.Count];
            var t0 = new Vector3(p0.x, alto, p0.y);
            var t1 = new Vector3(p1.x, alto, p1.y);
            var b0 = new Vector3(p0.x, baixo, p0.y);
            var b1 = new Vector3(p1.x, baixo, p1.y);

            b.Tri(0, cTop, t0, t1, Vector3.up);
            b.Tri(1, cBottom, b0, b1, Vector3.down);

            // Normal de fora pela aresta: o anel cresce no sentido anti-horário
            // em (x, z), então a normal para fora é (dz, -dx).
            var e = p1 - p0;
            var normal = new Vector3(e.y, 0f, -e.x);
            b.Quad(1, b0, b1, t1, t0, normal);
        }

        return b.ToMesh(name);
    }

    static Vector2 Polar(float r, float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

    /// <summary>Anel de seção quadrada deitado no plano XZ.</summary>
    public static Mesh MalhaAnel(float raio, float espessura, int segmentos)
    {
        var b = new Malha();
        float h = espessura * 0.5f;
        for (int i = 0; i < segmentos; i++)
        {
            float a0 = i * Mathf.PI * 2f / segmentos, a1 = (i + 1) * Mathf.PI * 2f / segmentos;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 o0 = d0 * (raio + h), o1 = d1 * (raio + h), i0 = d0 * (raio - h), i1 = d1 * (raio - h);
            var up = Vector3.up * h;
            var mid = (d0 + d1).normalized;

            b.Quad(0, o0 + up, o1 + up, i1 + up, i0 + up, Vector3.up);
            b.Quad(0, o0 - up, o1 - up, i1 - up, i0 - up, Vector3.down);
            b.Quad(0, o0 - up, o1 - up, o1 + up, o0 + up, mid);
            b.Quad(0, i0 - up, i1 - up, i1 + up, i0 + up, -mid);
        }

        return b.ToMesh("Anel", 1);
    }

    /// <summary>
    /// Sólido de revolução em volta de Y, de faces chapadas. O perfil vai de
    /// baixo para cima, em (raio, altura).
    /// </summary>
    public static Mesh MalhaTorno(IList<Vector2> perfil, int lados, string name)
    {
        var b = new Malha();
        for (int s = 0; s < lados; s++)
        {
            float a0 = s * Mathf.PI * 2f / lados, a1 = (s + 1) * Mathf.PI * 2f / lados;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            var meio = (d0 + d1).normalized;

            for (int i = 0; i + 1 < perfil.Count; i++)
            {
                var p = perfil[i];
                var q = perfil[i + 1];
                Vector3 a = d0 * p.x + Vector3.up * p.y, bb = d1 * p.x + Vector3.up * p.y;
                Vector3 c = d1 * q.x + Vector3.up * q.y, d = d0 * q.x + Vector3.up * q.y;

                // Normal do perfil: perpendicular à geratriz, para fora.
                var g = q - p;
                var normal = meio * g.y - Vector3.up * g.x;
                b.Quad(0, a, bb, c, d, normal);
            }

            // Tampas nas pontas.
            var baixo = perfil[0];
            var alto = perfil[perfil.Count - 1];
            b.Tri(0, Vector3.up * baixo.y, d0 * baixo.x + Vector3.up * baixo.y, d1 * baixo.x + Vector3.up * baixo.y, Vector3.down);
            b.Tri(0, Vector3.up * alto.y, d0 * alto.x + Vector3.up * alto.y, d1 * alto.x + Vector3.up * alto.y, Vector3.up);
        }

        return b.ToMesh(name, 1);
    }

    /// <summary>
    /// Rampa em hélice em volta de <paramref name="centro"/>: uma faixa de
    /// <paramref name="largura"/> metros no raio <paramref name="raio"/>,
    /// subindo de <paramref name="y0"/> a <paramref name="y1"/> entre os dois
    /// ângulos (graus, 0 ao sul, crescendo para o leste). Submalha 0 é o piso.
    /// </summary>
    public static Mesh MalhaHelice(Vector3 centro, float raio, float largura, float graus0, float graus1,
                                   float y0, float y1, float espessura, string name)
    {
        var b = new Malha();
        int passos = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(graus1 - graus0) / 3f));
        float ri = raio - largura * 0.5f, re = raio + largura * 0.5f;

        Vector3 Ponto(float graus, float r, float y)
        {
            float a = graus * Mathf.Deg2Rad;
            return new Vector3(centro.x + r * Mathf.Sin(a), y, centro.z - r * Mathf.Cos(a));
        }

        for (int i = 0; i < passos; i++)
        {
            float u0 = i / (float)passos, u1 = (i + 1) / (float)passos;
            float g0 = Mathf.Lerp(graus0, graus1, u0), g1 = Mathf.Lerp(graus0, graus1, u1);
            float h0 = Mathf.Lerp(y0, y1, u0), h1 = Mathf.Lerp(y0, y1, u1);

            Vector3 i0 = Ponto(g0, ri, h0), e0 = Ponto(g0, re, h0), i1 = Ponto(g1, ri, h1), e1 = Ponto(g1, re, h1);
            var baixo = Vector3.down * espessura;
            var cima = Vector3.Cross(e0 - i0, i1 - i0);
            if (cima.y < 0f) cima = -cima;
            var fora = (e0 + e1 - i0 - i1).normalized;

            b.Quad(0, i0, e0, e1, i1, cima);
            b.Quad(1, i0 + baixo, e0 + baixo, e1 + baixo, i1 + baixo, -cima);
            b.Quad(1, e0, e1, e1 + baixo, e0 + baixo, fora);
            b.Quad(1, i0, i1, i1 + baixo, i0 + baixo, -fora);
            if (i == 0) b.Quad(1, i0, e0, e0 + baixo, i0 + baixo, -(i1 - i0));
            if (i == passos - 1) b.Quad(1, i1, e1, e1 + baixo, i1 + baixo, i1 - i0);
        }

        return b.ToMesh(name);
    }

    /// <summary>Malha de faces chapadas, com a ordem dos vértices acertada pela normal pedida.</summary>
    sealed class Malha
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int>[] _t = { new List<int>(), new List<int>() };

        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            Tri(sub, a, b, c, normal);
            Tri(sub, a, c, d, normal);
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, normal) < 0f) (b, c) = (c, b);

            int i = _v.Count;
            foreach (var v in new[] { a, b, c })
            {
                _v.Add(v);
                var an = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                _uv.Add((an.y >= an.x && an.y >= an.z ? new Vector2(v.x, v.z)
                        : an.x >= an.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y)) / LavaLetalArt.MetalTile);
            }

            _t[sub].Add(i);
            _t[sub].Add(i + 1);
            _t[sub].Add(i + 2);
        }

        public Mesh ToMesh(string name, int submalhas = 2)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _uv);
            if (submalhas == 1)
            {
                var todos = new List<int>(_t[0]);
                todos.AddRange(_t[1]);
                mesh.subMeshCount = 1;
                mesh.SetTriangles(todos, 0);
            }
            else
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(_t[0], 0);
                mesh.SetTriangles(_t[1], 1);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ------------------------------------------------------------------
    // Materiais e texturas
    // ------------------------------------------------------------------

    static Material Carregar(string name, string shaderName)
    {
        string path = $"{Folder}/{name}.mat";
        var shader = Shader.Find(shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        return material;
    }

    static Material Lit(string name, Texture2D albedo, Color color, Color? emissionColor = null, float smoothness = 0.1f)
    {
        var material = Carregar(name, "Universal Render Pipeline/Lit");
        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);

        if (emissionColor.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Unlit transparente, as propriedades uma a uma (ver
    /// WitchSetup.RewindGlowMaterial): aditivo para o que é luz, normal para
    /// o que é vidro ou névoa. Sem descarte de faces - a esfera da zona
    /// precisa aparecer também por dentro.
    /// </summary>
    static Material Transparente(string name, Color color, bool aditivo)
    {
        var material = Carregar(name, "Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", aditivo ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One
                                                         : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One
                                                              : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Ceu(Texture2D gradiente)
    {
        var material = Carregar("CeuNoturno", "Skybox/Panoramic");
        material.SetTexture("_MainTex", gradiente);
        material.SetFloat("_Exposure", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Degradê violeta com estrelas na metade de cima e o brilho laranja da
    /// lava no horizonte de baixo. Semente fixa: a mesma noite a cada
    /// reconstrução.
    /// </summary>
    static Color PixelDoCeu(float u, float v)
    {
        var topo = new Color(0.05f, 0.03f, 0.12f);
        var alto = new Color(0.2f, 0.11f, 0.36f);
        var horizonte = new Color(0.55f, 0.28f, 0.42f);
        var baixo = new Color(0.45f, 0.14f, 0.08f);

        Color cor;
        if (v < 0.5f) cor = Color.Lerp(baixo, horizonte, Mathf.Pow(v / 0.5f, 5f));
        else
        {
            float t = (v - 0.5f) / 0.5f;
            cor = t < 0.1f ? Color.Lerp(horizonte, alto, t / 0.1f) : Color.Lerp(alto, topo, Mathf.Pow((t - 0.1f) / 0.9f, 0.6f));
        }

        if (v > 0.56f)
        {
            int x = Mathf.FloorToInt(u * 512f), y = Mathf.FloorToInt(v * 256f);
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            float estrela = (h & 0xffff) / 65535f;
            if (estrela > 0.985f) cor = Color.Lerp(cor, new Color(1f, 0.95f, 0.85f), (estrela - 0.985f) / 0.015f);
        }

        return cor;
    }

    static Texture2D Textura(string name, int largura, int altura, System.Func<float, float, Color> pixel)
    {
        string path = $"{Folder}/{name}.png";
        var texture = new Texture2D(largura, altura, TextureFormat.RGBA32, false);
        var pixels = new Color[largura * altura];
        for (int y = 0; y < altura; y++)
        for (int x = 0; x < largura; x++)
            pixels[y * largura + x] = pixel((x + 0.5f) / largura, (y + 0.5f) / altura);
        texture.SetPixels(pixels);
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        bool mudou = !System.IO.File.Exists(path) ||
                     !System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(path), png);
        if (mudou)
        {
            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp ||
             importer.mipmapEnabled))
        {
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
