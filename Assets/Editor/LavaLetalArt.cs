using UnityEditor;
using UnityEngine;

/// <summary>
/// Texturas e materiais da Terra da Lava Letal, gerados por código. A lava
/// daqui é a de todas as fases (ver HazardSetup.ApplyFlowingLava).
///
/// Gerados, e não pintados, pelo mesmo motivo das cenas: o projeto inteiro se
/// reconstrói a partir do código, e uma textura feita à mão seria a única peça
/// que ninguém conseguiria refazer depois.
///
/// Baixa resolução de propósito (64 e 128 px), com filtro bilinear: é o que o
/// Nintendo 64 fazia - pouca textura, esticada e borrada - e é o que dá a cara
/// de jogo de 1996 sem precisar de nenhum shader especial.
///
/// Todo ruído é periódico, então cada textura emenda nela mesma sem costura.
/// E toda semente é fixa: reconstruir a fase gera exatamente os mesmos pixels,
/// em vez de um arquivo novo no git a cada clique.
/// </summary>
public static class LavaLetalArt
{
    const string Folder = "Assets/Art/Generated/LavaLetal";

    /// <summary>Metros de mundo por repetição de textura, por tipo de superfície.</summary>
    public const float LavaTile = 6f;

    /// <summary>
    /// Brilho da lava. Abaixo de 1 de propósito: com emissão forte o vermelho
    /// fundo da textura sumia e a laje inteira virava um laranja estourado.
    /// </summary>
    public static readonly Color LavaGlow = new Color(0.85f, 0.45f, 0.32f);
    public const float RockTile = 3f;
    public const float MetalTile = 4f;

    public static Material Lava { get; private set; }
    public static Material RockSide { get; private set; }
    public static Material RockGray { get; private set; }
    public static Material RockBrown { get; private set; }
    public static Material RockBurnt { get; private set; }
    public static Material Metal { get; private set; }
    public static Material MetalBlue { get; private set; }
    public static Material Hazard { get; private set; }
    public static Material Dark { get; private set; }
    public static Material Ember { get; private set; }
    public static Material GlowRed { get; private set; }
    public static Material GlowYellow { get; private set; }
    public static Material GlowCyan { get; private set; }
    public static Material Sky { get; private set; }

    /// <summary>Cria ou atualiza tudo. Idempotente: rodar de novo só reescreve.</summary>
    public static void Ensure()
    {
        System.IO.Directory.CreateDirectory(Folder);

        var lava = Texture("lava", 128, LavaPixel);
        var rockDark = Texture("rocha_escura", 64, RockDarkPixel);
        var rockLight = Texture("rocha_clara", 64, RockLightPixel);
        var metal = Texture("chapa_xadrez", 64, MetalPixel);
        var hazard = Texture("faixa_perigo", 32, HazardPixel);
        var sky = Texture("ceu_vulcanico", 256, SkyPixel, height: 256, clamp: true);

        // A lava é a única coisa da cena que emite luz de verdade. Base escura
        // e emissão forte: o vermelho fundo vem da base, e o amarelo dos veios
        // só aparece onde a emissão estoura.
        Lava = Lit("Lava", lava, new Color(0.42f, 0.08f, 0.05f), emission: lava,
                   emissionColor: LavaGlow, smoothness: 0.25f);

        RockSide = Lit("RochaLateral", rockDark, Color.white);
        RockGray = Lit("RochaCinza", rockLight, new Color(0.66f, 0.63f, 0.6f));
        RockBrown = Lit("RochaMarrom", rockLight, new Color(0.6f, 0.44f, 0.33f));
        RockBurnt = Lit("RochaQueimada", rockLight, new Color(0.6f, 0.28f, 0.2f));

        Metal = Lit("Chapa", metal, new Color(0.82f, 0.84f, 0.88f), smoothness: 0.35f);
        MetalBlue = Lit("ChapaAzul", metal, new Color(0.62f, 0.76f, 1f), smoothness: 0.35f);
        Hazard = Lit("FaixaDePerigo", hazard, Color.white);
        Dark = Lit("Estrutura", rockDark, new Color(0.55f, 0.5f, 0.55f));

        // O miolo laranja da ilha central: quente, mas claramente chão. Emite
        // pouco e tem textura de pedra, para não ser confundido com a lava.
        Ember = Lit("MioloQuente", rockLight, new Color(0.95f, 0.45f, 0.15f), emission: rockLight,
                    emissionColor: new Color(0.35f, 0.1f, 0.02f));

        GlowRed = Lit("LuzVermelha", null, new Color(1f, 0.2f, 0.15f), emissionColor: new Color(2.2f, 0.3f, 0.2f));
        GlowYellow = Lit("LuzAmarela", null, new Color(1f, 0.85f, 0.2f), emissionColor: new Color(2f, 1.5f, 0.3f));
        GlowCyan = Lit("LuzCiano", null, new Color(0.2f, 0.9f, 1f), emissionColor: new Color(0.3f, 1.6f, 2f));

        Sky = SkyMaterial(sky);

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Céu vermelho, névoa da cor do horizonte e luz ambiente quente, na cena
    /// aberta. Não cria a luz direcional: cada cena mantém a sua.
    ///
    /// A névoa é o que fecha o horizonte: a lava some nela na mesma cor da
    /// faixa acesa do céu, e o fim da laje nunca aparece. Por isso a laje de
    /// cada cena precisa passar do alcance da câmera (ver HazardSetup.MinLavaSize).
    /// </summary>
    public static void ApplyAtmosphere()
    {
        if (Sky == null) Ensure();

        RenderSettings.skybox = Sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.24f, 0.2f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.22f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.75f, 0.22f, 0.06f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.66f, 0.22f, 0.09f);
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
    }

    public const float FogStart = 55f;
    public const float FogEnd = 210f;

    // ------------------------------------------------------------------
    // Pixels
    // ------------------------------------------------------------------

    /// <summary>
    /// Lava: faixas onduladas deformadas por ruído, em quatro tons.
    ///
    /// As faixas dão a direção de escoamento que o olho lê como "corrente";
    /// o ruído quebra a regularidade delas. Os coeficientes do seno são
    /// inteiros, senão a faixa não fecharia na borda e a emenda apareceria.
    /// </summary>
    static Color LavaPixel(float u, float v)
    {
        float warp = Fbm(u, v, 4, 3, 11);
        float bands = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (3f * u + 2f * v) + warp * 7f);
        float heat = 0.55f * Fbm(u, v, 6, 3, 23) + 0.45f * bands * bands * bands;

        var deep = new Color(0.32f, 0.03f, 0.02f);
        var red = new Color(0.78f, 0.09f, 0.03f);
        var orange = new Color(1f, 0.4f, 0.06f);
        var yellow = new Color(1f, 0.88f, 0.3f);

        // A maior parte fica no vermelho fundo; laranja e amarelo só nas
        // cristas. Com as faixas divididas por igual a laje lia como um
        // vermelho-vivo chapado, sem a "crosta" escura que faz a lava parecer
        // grossa.
        if (heat < 0.5f) return Color.Lerp(deep, red, Mathf.Pow(heat / 0.5f, 1.6f));
        if (heat < 0.7f) return Color.Lerp(red, orange, (heat - 0.5f) / 0.2f);
        return Color.Lerp(orange, yellow, Mathf.SmoothStep(0f, 1f, (heat - 0.7f) / 0.16f));
    }

    static Color RockDarkPixel(float u, float v)
    {
        float n = Fbm(u, v, 8, 4, 31);
        float crack = Mathf.Abs(Fbm(u, v, 5, 2, 37) - 0.5f) < 0.025f ? 0.55f : 1f;
        float k = (0.62f + 0.5f * n) * crack;

        return new Color(0.2f * k, 0.15f * k, 0.13f * k);
    }

    /// <summary>Pedra clara, quase cinza puro: a cor vem do material, para um arquivo servir três rochas.</summary>
    static Color RockLightPixel(float u, float v)
    {
        float n = Fbm(u, v, 8, 4, 41);
        float speck = Hash((int)(u * 64), (int)(v * 64), 43) > 0.93f ? 0.82f : 1f;
        float k = (0.74f + 0.3f * n) * speck;

        return new Color(k, k, k);
    }

    /// <summary>Chapa xadrez com frisos e rebites: quatro casas por repetição, de 1 m cada.</summary>
    static Color MetalPixel(float u, float v)
    {
        int cx = Mathf.FloorToInt(u * 4f), cy = Mathf.FloorToInt(v * 4f);
        bool light = ((cx + cy) & 1) == 0;

        float fu = u * 4f - cx, fv = v * 4f - cy;
        bool seam = fu < 0.04f || fv < 0.04f;
        bool rivet = (Mathf.Abs(fu - 0.15f) < 0.05f || Mathf.Abs(fu - 0.85f) < 0.05f)
                     && (Mathf.Abs(fv - 0.15f) < 0.05f || Mathf.Abs(fv - 0.85f) < 0.05f);

        float k = light ? 0.78f : 0.56f;
        if (seam) k = 0.3f;
        if (rivet) k = 0.92f;
        k *= 0.94f + 0.06f * Fbm(u, v, 8, 2, 51);

        return new Color(k, k, k * 1.04f);
    }

    static Color HazardPixel(float u, float v)
    {
        bool stripe = Mathf.Repeat((u + v) * 2f, 1f) < 0.5f;
        return stripe ? new Color(1f, 0.78f, 0.1f) : new Color(0.12f, 0.1f, 0.08f);
    }

    /// <summary>
    /// Céu em faixa vertical, para o skybox panorâmico: escuro em cima, uma
    /// barra de brilho alaranjado no horizonte, e vermelho fundo embaixo.
    /// O brilho no horizonte é o que vende o "inferno" - a luz parece vir da
    /// própria lava ao longe.
    /// </summary>
    static Color SkyPixel(float u, float v)
    {
        var top = new Color(0.14f, 0.03f, 0.04f);
        var high = new Color(0.62f, 0.14f, 0.06f);
        var horizon = new Color(1f, 0.58f, 0.2f);
        var below = new Color(0.4f, 0.08f, 0.03f);

        // Um pouco de fumaça nas faixas altas, só para não ser um degradê liso.
        float smoke = 0.9f + 0.1f * Fbm(u, v, 6, 3, 61);

        if (v < 0.5f) return Color.Lerp(below, horizon, Mathf.Pow(v / 0.5f, 6f));
        float t = (v - 0.5f) / 0.5f;
        if (t < 0.12f) return Color.Lerp(horizon, high, t / 0.12f);
        return Color.Lerp(high, top, Mathf.Pow((t - 0.12f) / 0.88f, 0.7f)) * smoke;
    }

    // ------------------------------------------------------------------
    // Ruído periódico
    // ------------------------------------------------------------------

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    /// <summary>Ruído de valor numa grade de <paramref name="period"/> casas que se repete.</summary>
    static float ValueNoise(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;

        int Wrap(int i) => ((i % period) + period) % period;
        float a = Hash(Wrap(x0), Wrap(y0), seed);
        float b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
        float c = Hash(Wrap(x0), Wrap(y0 + 1), seed);
        float d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);

        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f, amplitude = 0.5f, norm = 0f;
        int period = basePeriod;

        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(u, v, period, seed + i * 101) * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            period *= 2;
        }

        return sum / norm;
    }

    // ------------------------------------------------------------------
    // Assets
    // ------------------------------------------------------------------

    static Texture2D Texture(string name, int width, System.Func<float, float, Color> pixel,
                             int height = 0, bool clamp = false)
    {
        if (height <= 0) height = width;
        string path = $"{Folder}/{name}.png";

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);

        texture.SetPixels(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        // Só reescreve quando mudou: o mesmo arquivo de novo faria o Unity
        // reimportar à toa a cada reconstrução.
        bool changed = !System.IO.File.Exists(path)
                       || !System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(path), png);
        if (changed)
        {
            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool dirty = importer.wrapMode != (clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat)
                         || importer.filterMode != FilterMode.Bilinear
                         || importer.textureCompression != TextureImporterCompression.Uncompressed
                         || importer.maxTextureSize != 256;

            if (dirty)
            {
                importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Lit(string name, Texture2D albedo, Color color, Texture2D emission = null,
                        Color? emissionColor = null, float smoothness = 0.08f)
    {
        var material = LoadOrCreate(name, "Universal Render Pipeline/Lit");
        if (material == null) return null;

        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);

        if (emissionColor.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", emission);
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

    static Material SkyMaterial(Texture2D gradient)
    {
        var material = LoadOrCreate("Ceu", "Skybox/Panoramic");
        if (material == null) return null;

        material.SetTexture("_MainTex", gradient);
        material.SetFloat("_Exposure", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material LoadOrCreate(string name, string shaderName)
    {
        string path = $"{Folder}/{name}.mat";
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{shaderName}' não encontrado; o material {name} ficou de fora.");
            return null;
        }

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
}
