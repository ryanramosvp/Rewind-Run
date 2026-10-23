using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o piso de lava e o sistema de reinício da fase.
/// Substitui o SafetyFloor cinza que existia só para a queda ter fim.
/// </summary>
public static class HazardSetup
{
    public const string MaterialPath = "Assets/Art/Materials/Lava.mat";

    const string kLavaName = "Lava";
    const string kRespawnName = "LevelRespawn";
    const string kOldFloorName = "SafetyFloor";

    /// <summary>
    /// Altura da superfície da lava. Fica bem abaixo do percurso de propósito:
    /// a queda precisa durar cerca de um segundo para o jogador ter tempo de
    /// reagir e apertar o rewind. Morte instantânea tiraria o sentido da
    /// mecânica principal do jogo.
    /// </summary>
    const float kSurfaceHeight = -10f;

    // Medidas da laje de quem chama EnsureLava() sem argumentos.
    const float kVisibleWidth = 70f;
    const float kVisibleDepth = 115f;

    /// <summary>
    /// Lado mínimo da laje, em metros.
    ///
    /// As lajes já foram enxutas de propósito: o shader do pack calculava
    /// ruído por pixel, e lava fora do caminho era GPU jogada fora. A lava de
    /// hoje é URP Lit com uma textura de 128 px, e o custo de cobrir mais área
    /// é desprezível. O que pesa agora é o contrário: com o céu vermelho, uma
    /// borda de laje à vista corta o horizonte.
    ///
    /// 1200 m: as câmeras enxergam até 400 m e as fases têm até ~100 m do
    /// centro à ponta, então a borda fica além do alcance da câmera de qualquer
    /// ponto. Com 600 m ela ficava além da névoa, mas a quina da laje ainda
    /// dobrava a linha do horizonte nas laterais da tela.
    /// </summary>
    public const float MinLavaSize = 1200f;
    const float kThickness = 4f;

    /// <summary>
    /// O volume que mata é separado do visual e bem maior: se o jogador escapar
    /// para fora da lava visível, ele ainda precisa morrer em vez de cair para
    /// sempre. Como é invisível, custa nada.
    /// </summary>
    const float kKillVolumeSize = 600f;

    const string kKillZoneName = "FallKillZone";

    /// <summary>
    /// Tamanho de um tile da textura de lava no mundo. Referência: a bruxa tem
    /// 2,16 m, então tiles de 5 m dão cerca de dois personagens por padrão -
    /// grande o bastante para o ruído fino do pack virar massa fluindo em vez
    /// de granulado, que é o que brigaria com o cenário low-poly.
    /// </summary>
    const float kMetersPerTile = 5f;

    /// <summary>Material de lava do pack de shaders, se estiver no projeto.</summary>
    const string kPackMaterialPath = "Assets/Shaders/Lava/Lava.mat";

    const string kFlatShaderName = "Universal Render Pipeline/Lit";

    /// <summary>
    /// Qual lava o setup usa por padrão. Fica no chapado de propósito.
    ///
    /// O shader do pack é feito para rios de lava vistos de perto. Num piso
    /// enorme visto de cima e de longe, a textura de alta frequência produz
    /// moiré e cintilação nas bordas da tela, e os valores HDR dele (de 5 a 8)
    /// estouram. Somado ao custo por pixel de Voronoi e ruído procedural, ele
    /// custa caro para entregar um resultado pior neste caso.
    ///
    /// Hoje o chapado só sobra como reserva: a laje é vestida com a lava da
    /// fase 3 em <see cref="ApplyFlowingLava"/>. O menu que alternava os dois
    /// saiu junto - ele trocava um material que a laje não usa mais.
    /// </summary>
    const bool kUseDetailedShader = false;

    // Cores retiradas da faixa laranja do próprio atlas do KayKit, para a lava
    // não destoar da paleta do cenário.
    static readonly Color kBaseColor = new Color(0.941f, 0.349f, 0.145f);      // #F05925
    static readonly Color kEmissionColor = new Color(0.965f, 0.549f, 0.122f);  // #F68C1F

    [MenuItem("Rewind Run/Adicionar Lava e Reinício na Cena", priority = 2)]
    public static void AddToOpenScene()
    {
        var lava = EnsureLava();
        EnsureRespawn();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lava.scene);
        Debug.Log("Lava e reinício de fase adicionados. Salve a cena para manter.");
    }

    /// <summary>
    /// Cria a laje de lava, ou atualiza a que já existe na cena.
    /// É idempotente de propósito: dá para rodar de novo depois de trocar o
    /// material sem duplicar objeto nem perder o que já estava montado.
    /// </summary>
    public static GameObject EnsureLava() =>
        EnsureLava(new Vector3(1f, 0f, 37f), kVisibleWidth, kVisibleDepth);

    /// <summary>
    /// Cria ou atualiza a laje de lava com as medidas de uma fase específica.
    /// O tamanho é argumento porque o custo do shader acompanha a área que a
    /// lava ocupa na tela - cobrir muito além da fase é GPU jogada fora.
    /// </summary>
    /// <param name="surfaceHeight">
    /// Altura da superfície. As fases correm bem acima dela (ver
    /// <see cref="kSurfaceHeight"/>); a Terra da Lava Letal pede a lava rente
    /// às ilhas, como no jogo que a inspira.
    /// </param>
    public static GameObject EnsureLava(Vector3 center, float width, float depth,
                                        float surfaceHeight = kSurfaceHeight)
    {
        var lava = FindExistingLava() ?? CreateLavaSlab();

        // Nunca menor que o mínimo: com o céu vermelho, a borda da laje só
        // some se cair além do fim da névoa, de qualquer ponto da fase.
        width = Mathf.Max(width, MinLavaSize);
        depth = Mathf.Max(depth, MinLavaSize);

        // Reaplica tamanho e posição: uma laje criada por uma versão anterior
        // deste script continuaria com as medidas antigas.
        lava.transform.localPosition = new Vector3(center.x, surfaceHeight - kThickness * 0.5f, center.z);
        lava.transform.localScale = new Vector3(width, kThickness, depth);

        var material = CreateOrLoadMaterial();
        var renderer = lava.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;

        // A lava não tem nada abaixo nem ao lado para sombrear, e receber sombra
        // numa superfície que emite luz não faz sentido. Os dois passes saem de
        // graça, sem diferença visual.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (!lava.TryGetComponent<KillZone>(out _)) lava.AddComponent<KillZone>();
        if (lava.TryGetComponent<BoxCollider>(out var area)) area.isTrigger = true;

        SyncPulseComponent(lava, material);
        ApplyFlowingLava(lava, width, depth);
        EnsureFallKillZone(lava.transform.parent, center, surfaceHeight);

        EditorUtility.SetDirty(lava);
        return lava;
    }

    /// <summary>
    /// Veste a laje com a lava da Terra da Lava Letal: textura de veios em
    /// baixa resolução, escorrendo, e o pulso só mexendo no brilho.
    ///
    /// Nasceu para a fase 3 e passou a ser a lava de todas. Ela resolve o que
    /// o material chapado não resolvia - laranja uniforme lê como chão pintado
    /// - sem o custo do shader do pack: é URP Lit comum com uma textura de
    /// 128 px, e o movimento é só deslocamento de UV.
    ///
    /// Se a geração da arte falhar, a laje fica com o material chapado de
    /// antes. Feia, mas a fase continua montada e letal.
    /// </summary>
    static void ApplyFlowingLava(GameObject lava, float width, float depth)
    {
        if (LavaLetalArt.Lava == null) LavaLetalArt.Ensure();
        if (LavaLetalArt.Lava == null) return;

        lava.GetComponent<Renderer>().sharedMaterial = LavaLetalArt.Lava;

        var flow = lava.GetComponent<LavaFlow>();
        if (flow == null) flow = lava.AddComponent<LavaFlow>();

        // Repetições derivadas do tamanho da laje, para cada uma ocupar sempre
        // os mesmos metros - numa laje de 70 m e numa de 230 m.
        var serialized = new SerializedObject(flow);
        serialized.FindProperty("tiling").vector2Value =
            new Vector2(width / LavaLetalArt.LavaTile, depth / LavaLetalArt.LavaTile);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // O pulso padrão tinge a emissão de laranja chapado; com a textura a
        // cor vem dela, e o pulso só mexe no brilho.
        var pulse = lava.GetComponent<LavaSurface>();
        if (pulse == null) pulse = lava.AddComponent<LavaSurface>();

        var p = new SerializedObject(pulse);
        p.FindProperty("emissionColor").colorValue = LavaLetalArt.LavaGlow;
        p.FindProperty("minIntensity").floatValue = 0.8f;
        p.FindProperty("maxIntensity").floatValue = 1.25f;
        p.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Rede de segurança invisível, para quem cair fora da lava visível.</summary>
    static void EnsureFallKillZone(Transform parent, Vector3 center, float surfaceHeight)
    {
        var existing = GameObject.Find(kKillZoneName);
        if (existing == null)
        {
            existing = new GameObject(kKillZoneName);
            if (parent != null) existing.transform.SetParent(parent, false);
        }

        // Um pouco abaixo da lava visível: quem cai dentro dela morre lá primeiro,
        // e este volume só pega quem passou por fora.
        existing.transform.localPosition = new Vector3(center.x, surfaceHeight - 6f, center.z);

        var area = existing.GetComponent<BoxCollider>();
        if (area == null) area = existing.AddComponent<BoxCollider>();
        area.size = new Vector3(kKillVolumeSize, 4f, kKillVolumeSize);
        area.isTrigger = true;

        if (!existing.TryGetComponent<KillZone>(out _)) existing.AddComponent<KillZone>();

        EditorUtility.SetDirty(existing);
    }

    /// <summary>
    /// Acha a laje visível. Procura por Renderer junto com o KillZone de
    /// propósito: o volume de segurança invisível também tem KillZone, e
    /// confundir os dois faria este setup tentar pintar um objeto sem malha.
    /// </summary>
    static GameObject FindExistingLava()
    {
        var byScript = Object.FindAnyObjectByType<LavaSurface>();
        if (byScript != null) return byScript.gameObject;

        foreach (var zone in Object.FindObjectsByType<KillZone>(FindObjectsInactive.Include))
        {
            if (zone.GetComponent<Renderer>() != null) return zone.gameObject;
        }

        return null;
    }

    static GameObject CreateLavaSlab()
    {
        // O chão cinza antigo não serve mais: agora quem segura a queda é a lava.
        var oldFloor = GameObject.Find(kOldFloorName);
        Transform parent = null;
        if (oldFloor != null)
        {
            parent = oldFloor.transform.parent;
            Object.DestroyImmediate(oldFloor);
        }

        // Cubo achatado e não Plane: Plane usa MeshCollider, e MeshCollider não
        // convexo não funciona como trigger - a bruxa pousaria em cima da lava
        // em vez de afundar nela.
        var lava = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lava.name = kLavaName;
        if (parent != null) lava.transform.SetParent(parent, false);

        return lava;
    }

    /// <summary>
    /// O pulso de emissão existe só para o material chapado de emergência. O
    /// shader do pack já anima a superfície sozinho, e nem expõe _EmissionColor
    /// - deixar o script ali seria código rodando todo frame sem efeito nenhum.
    /// </summary>
    static void SyncPulseComponent(GameObject lava, Material material)
    {
        bool usesLavaShader = material != null && material.HasProperty("_LavaColor");
        var pulse = lava.GetComponent<LavaSurface>();

        if (usesLavaShader)
        {
            if (pulse != null) Object.DestroyImmediate(pulse);
        }
        else if (pulse == null)
        {
            lava.AddComponent<LavaSurface>();
        }
    }

    public static LevelRespawn EnsureRespawn()
    {
        var existing = Object.FindAnyObjectByType<LevelRespawn>();
        if (existing != null) return existing;

        var go = new GameObject(kRespawnName);
        var respawn = go.AddComponent<LevelRespawn>();

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) EditorSetupUtil.SetReference(respawn, "player", player);

        return respawn;
    }

    /// <summary>
    /// Garante que o material da lava está no modo desejado, criando-o se preciso.
    ///
    /// Converte no lugar em vez de criar outro asset: a cena guarda a referência
    /// por GUID, e trocar de arquivo deixaria a laje apontando para o material
    /// antigo.
    /// </summary>
    static Material CreateOrLoadMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            var shader = Shader.Find(kFlatShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{kFlatShaderName}' não encontrado. O projeto está em URP?");
                return null;
            }

            EditorSetupUtil.EnsureFolder(MaterialPath);
            material = new Material(shader) { name = "Lava" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        bool packAvailable = AssetDatabase.LoadAssetAtPath<Material>(kPackMaterialPath) != null;
        bool wantsDetailed = kUseDetailedShader && packAvailable;

        if (wantsDetailed != IsDetailed(material))
            ApplyMode(material, wantsDetailed);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static bool IsDetailed(Material material) => material.HasProperty("_LavaColor");

    /// <summary>Converte o material entre o shader detalhado do pack e o chapado.</summary>
    static void ApplyMode(Material material, bool detailed)
    {
        if (detailed)
        {
            var pack = AssetDatabase.LoadAssetAtPath<Material>(kPackMaterialPath);
            if (pack == null)
            {
                Debug.LogWarning($"O pack de shaders não está em {kPackMaterialPath}.");
                return;
            }

            material.shader = pack.shader;
            material.CopyPropertiesFromMaterial(pack);
            ApplySurfaceScale(material);
            return;
        }

        var flatShader = Shader.Find(kFlatShaderName);
        if (flatShader == null)
        {
            Debug.LogError($"Shader '{kFlatShaderName}' não encontrado.");
            return;
        }

        material.shader = flatShader;
        material.SetColor("_BaseColor", kBaseColor);
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Metallic", 0f);

        // A emissão é o que faz a lava parecer quente em vez de plástico laranja.
        // Precisa da keyword ligada, senão o URP ignora a cor.
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", kEmissionColor);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    /// <summary>
    /// Ajusta o tiling e a velocidade do shader.
    ///
    /// Tiling: o grafo amostra a textura por UV de malha, e um cubo tem UV de 0
    /// a 1 por face. A escala precisa ser derivada do tamanho real da laje, ou
    /// cada tile fica esticado por dezenas de metros e a lava vira um borrão.
    /// Como a laje não é quadrada, X e Z recebem contagens diferentes - assim o
    /// tile continua quadrado em vez de espremido num dos eixos.
    /// </summary>
    static void ApplySurfaceScale(Material material)
    {
        var scale = new Vector4(kVisibleWidth / kMetersPerTile, kVisibleDepth / kMetersPerTile, 0f, 0f);

        if (material.HasProperty("_LavaScale")) material.SetVector("_LavaScale", scale);
        if (material.HasProperty("_LavaScaleNormal")) material.SetVector("_LavaScaleNormal", scale);

        ApplyMotionSpeed(material);
    }

    /// <summary>
    /// Deixa a lava se mover mais devagar.
    ///
    /// Isto é escolha visual, não otimização: a velocidade é só um multiplicador
    /// de tempo dentro do shader, e a mesma conta roda a cada frame independente
    /// dela. O ganho aqui é de leitura - massa derretida se move devagar, e num
    /// cenário estilizado o movimento lento chama menos atenção para o ruído da
    /// textura, que é a parte que destoa do resto.
    /// </summary>
    static void ApplyMotionSpeed(Material material)
    {
        SetVectorIfPresent(material, "_LavaSpeed", new Vector4(0.035f, 0f, 0f, 0f));
        SetVectorIfPresent(material, "_VoronoiSpeed", new Vector4(0f, 0.004f, 0f, 0f));
        SetVectorIfPresent(material, "_NoiseSpeed", new Vector4(0f, 0.015f, 0f, 0f));
        SetVectorIfPresent(material, "_LavaNormalSpeed", new Vector4(0.05f, 0f, 0f, 0f));
    }

    static void SetVectorIfPresent(Material material, string property, Vector4 value)
    {
        if (material.HasProperty(property)) material.SetVector(property, value);
    }

}
