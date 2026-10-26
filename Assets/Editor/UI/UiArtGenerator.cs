using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Desenha as peças da interface: placa de pedra, moldura de metal com
/// rebites, brilho, sombra lateral, ícone do rewind e losango.
///
/// São peças, não telas. Cada uma é cinza ou branca e ganha a cor na hora,
/// pelo Image.color, e as placas são 9-slice: a mesma moldura serve para um
/// botão de 560 px e para o placar de 150. Por isso se desenham à mão aqui,
/// em código, e não como imagens prontas - uma tela inteira pintada não
/// mudaria de tamanho nem de cor.
///
/// Tudo é determinístico (o ruído vem de um hash, não de Random), então gerar
/// de novo dá os mesmos arquivos e o git não acusa mudança à toa.
/// </summary>
public static class UiArtGenerator
{
    public const string Folder = "Assets/Art/UI/Generated";

    public const string StonePath = Folder + "/Pedra.png";
    public const string FramePath = Folder + "/Moldura.png";
    public const string GlowPath = Folder + "/Brilho.png";
    public const string ShadePath = Folder + "/Sombra.png";
    public const string RewindPath = Folder + "/Rewind.png";
    public const string DiamondPath = Folder + "/Losango.png";

    const int Samples = 4;   // supersampling por eixo, para as bordas saírem lisas

    [MenuItem("Rewind Run/Interface/Gerar Arte da Interface", priority = 30)]
    public static void GenerateAll()
    {
        EditorSetupUtil.EnsureFolder(StonePath);

        Write(StonePath, Stone(128), new Vector4(32, 32, 32, 32));
        Write(FramePath, Frame(128), new Vector4(36, 36, 36, 36));
        Write(GlowPath, Glow(128), new Vector4(56, 56, 56, 56));
        Write(ShadePath, Shade(256, 4), Vector4.zero);
        Write(RewindPath, RewindIcon(128), Vector4.zero);
        Write(DiamondPath, Diamond(64), Vector4.zero);
    }

    /// <summary>Gera só o que ainda não existe. Os construtores de cena chamam isto.</summary>
    public static void EnsureGenerated()
    {
        foreach (var path in new[] { StonePath, FramePath, GlowPath, ShadePath, RewindPath, DiamondPath })
        {
            if (!File.Exists(path))
            {
                GenerateAll();
                return;
            }
        }
    }

    public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    // ------------------------------------------------------------------
    // Peças
    // ------------------------------------------------------------------

    /// <summary>
    /// Placa de basalto: cantos arredondados, granulado leve, mais clara no
    /// alto e um chanfro - luz na borda de cima, sombra na de baixo.
    /// </summary>
    static Color[] Stone(int size)
    {
        const float radius = 18f, bevel = 5f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float coverage = Coverage(x, y, (px, py) => RoundRect(px, py, size, size, 1f, radius) <= 0f);
            if (coverage <= 0f) continue;

            float px0 = x + 0.5f, py0 = y + 0.5f;
            float inside = -RoundRect(px0, py0, size, size, 1f, radius);   // distância até a borda

            float v = 0.62f + (Noise(px0 / 9f, py0 / 9f) - 0.5f) * 0.16f
                            + (Noise(px0 / 3f, py0 / 3f) - 0.5f) * 0.06f
                            + (py0 / size - 0.5f) * 0.14f;

            if (inside < bevel)
            {
                // Qual borda está mais perto decide se é luz ou sombra.
                float k = 1f - inside / bevel;
                float vertical = py0 > size * 0.5f ? 1f : -1f;
                bool nearTopOrBottom = Mathf.Min(py0, size - py0) < Mathf.Min(px0, size - px0);
                float light = nearTopOrBottom ? vertical : (px0 < size * 0.5f ? 0.4f : -0.4f);
                v += light * 0.22f * k;
            }

            if (inside < 1.6f) v *= 0.55f;   // contorno escuro

            pixels[y * size + x] = new Color(v, v, v, coverage);
        }

        return pixels;
    }

    /// <summary>
    /// Moldura de metal: um aro chanfrado (luz vinda do alto à esquerda),
    /// riscos de desgaste e um rebite em cada canto. O meio é vazio - ela vai
    /// por cima da placa.
    /// </summary>
    static Color[] Frame(int size)
    {
        const float outerRadius = 20f, inset = 10f, innerRadius = 11f;
        var light = new Vector2(-0.55f, 0.83f);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float coverage = Coverage(x, y, (px, py) =>
                RoundRect(px, py, size, size, 0.5f, outerRadius) <= 0f &&
                RoundRect(px, py, size, size, inset, innerRadius) > 0f);

            float px0 = x + 0.5f, py0 = y + 0.5f;

            // Rebites nos cantos, sobre o aro.
            float rivet = RivetShade(px0, py0, size, light, out float rivetCoverage);
            if (coverage <= 0f && rivetCoverage <= 0f) continue;

            float outer = -RoundRect(px0, py0, size, size, 0.5f, outerRadius);
            float across = Mathf.Clamp01(outer / (inset - 0.5f));   // 0 na borda de fora, 1 na de dentro

            // Normal do chanfro: para fora na metade de fora, para dentro na de dentro.
            Vector2 normal = Gradient(px0, py0, size, outerRadius);
            float facing = across < 0.4f ? 1f : across > 0.6f ? -1f : 0f;
            float v = 0.66f + 0.3f * facing * Vector2.Dot(normal, light);

            v += (Noise(px0 * 0.9f, py0 * 0.12f) - 0.5f) * 0.12f;   // riscos alongados
            if (across < 0.12f || across > 0.9f) v *= 0.6f;          // fio escuro nas duas bordas

            if (rivetCoverage > 0f)
            {
                v = Mathf.Lerp(v, rivet, rivetCoverage);
                coverage = Mathf.Max(coverage, rivetCoverage);
            }

            pixels[y * size + x] = new Color(v, v, v, coverage);
        }

        return pixels;
    }

    static float RivetShade(float px, float py, int size, Vector2 light, out float coverage)
    {
        const float r = 3.6f, at = 9.5f;
        coverage = 0f;

        foreach (var c in new[] { new Vector2(at, at), new Vector2(size - at, at),
                                  new Vector2(at, size - at), new Vector2(size - at, size - at) })
        {
            var d = new Vector2(px, py) - c;
            if (d.magnitude > r + 1f) continue;

            coverage = Mathf.Clamp01(r + 0.5f - d.magnitude);

            // Meia esfera: a normal sai do centro.
            var n = d / r;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - n.sqrMagnitude));
            return Mathf.Clamp01(0.45f + 0.5f * Vector2.Dot(n, light) + 0.25f * z);
        }

        return 0f;
    }

    /// <summary>
    /// Brilho em volta de um retângulo: o miolo cheio e um halo que some em
    /// 28 px. O miolo coincide com o retângulo de dentro do 9-slice, então
    /// basta o Image do brilho ser 56 px maior que o botão.
    /// </summary>
    static Color[] Glow(int size)
    {
        const float inset = 28f, radius = 16f, spread = 28f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundRect(x + 0.5f, y + 0.5f, size, size, inset, radius);
            float a = d <= 0f ? 1f : Mathf.Pow(1f - Mathf.Clamp01(d / spread), 2.2f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }

        return pixels;
    }

    /// <summary>Degradê horizontal, cheio à esquerda: escurece o lado do menu.</summary>
    static Color[] Shade(int width, int height)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float t = (x + 0.5f) / width;
            float a = 1f - Mathf.SmoothStep(0f, 1f, t);
            pixels[y * width + x] = new Color(1f, 1f, 1f, a);
        }

        return pixels;
    }

    /// <summary>Dois triângulos para a esquerda: o símbolo de voltar a fita.</summary>
    static Color[] RewindIcon(int size)
    {
        var pixels = new Color[size * size];
        float s = size / 128f;

        var a1 = new Vector2(14, 64) * s; var b1 = new Vector2(62, 100) * s; var c1 = new Vector2(62, 28) * s;
        var a2 = new Vector2(62, 64) * s; var b2 = new Vector2(110, 100) * s; var c2 = new Vector2(110, 28) * s;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float coverage = Coverage(x, y, (px, py) =>
                InTriangle(new Vector2(px, py), a1, b1, c1) || InTriangle(new Vector2(px, py), a2, b2, c2));
            if (coverage > 0f) pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
        }

        return pixels;
    }

    static Color[] Diamond(int size)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f, reach = size * 0.42f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float coverage = Coverage(x, y, (px, py) => Mathf.Abs(px - half) + Mathf.Abs(py - half) <= reach);
            if (coverage > 0f) pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
        }

        return pixels;
    }

    // ------------------------------------------------------------------
    // Geometria
    // ------------------------------------------------------------------

    /// <summary>
    /// Distância com sinal até um retângulo de cantos arredondados, recuado
    /// <paramref name="inset"/> das bordas da textura. Negativa dentro.
    /// </summary>
    static float RoundRect(float px, float py, int width, int height, float inset, float radius)
    {
        float hx = width * 0.5f - inset - radius, hy = height * 0.5f - inset - radius;
        float qx = Mathf.Abs(px - width * 0.5f) - hx;
        float qy = Mathf.Abs(py - height * 0.5f) - hy;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    /// <summary>Direção para fora do aro, pela variação da distância.</summary>
    static Vector2 Gradient(float px, float py, int size, float radius)
    {
        const float e = 0.5f;
        float dx = RoundRect(px + e, py, size, size, 0.5f, radius) - RoundRect(px - e, py, size, size, 0.5f, radius);
        float dy = RoundRect(px, py + e, size, size, 0.5f, radius) - RoundRect(px, py - e, size, size, 0.5f, radius);
        var g = new Vector2(dx, dy);
        return g.sqrMagnitude > 1e-6f ? g.normalized : Vector2.zero;
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(negative && positive);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

    /// <summary>Fração do pixel coberta pela forma, amostrada numa grade.</summary>
    static float Coverage(int x, int y, System.Func<float, float, bool> inside)
    {
        int hits = 0;
        for (int sy = 0; sy < Samples; sy++)
        for (int sx = 0; sx < Samples; sx++)
        {
            if (inside(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples)) hits++;
        }

        return hits / (float)(Samples * Samples);
    }

    /// <summary>Ruído de valor suave, de um hash inteiro: o mesmo a cada geração.</summary>
    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float tx = x - xi, ty = y - yi;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    static void Write(string path, Color[] pixels, Vector4 border)
    {
        int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        int width = size, height = size;

        // A sombra é a única que não é quadrada.
        if (path == ShadePath)
        {
            width = 256;
            height = pixels.Length / width;
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = border;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
