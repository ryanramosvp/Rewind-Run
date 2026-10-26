using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cria ou atualiza o asset do tema (<see cref="UiTheme"/>) e os dois
/// materiais de texto.
///
/// A fonte é a LiberationSans dos recursos essenciais do TextMeshPro: vem
/// com o pacote de UI do Unity, então não é dependência nova nem arquivo de
/// licença duvidosa. A cara própria vem do material, não da fonte - contorno
/// grosso, sombra deslocada e degradê de cor dão ao logotipo o peso de letra
/// entalhada que uma fonte comum não tem sozinha.
///
/// As cores ficam no asset e não são sobrescritas aqui depois de criadas:
/// quem ajustou o tema no Inspector não perde o ajuste ao reconstruir.
/// </summary>
public static class UiThemeSetup
{
    public const string ThemePath = "Assets/Art/UI/RewindRunTheme.asset";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string MaterialFolder = "Assets/Art/UI/Fonts";

    /// <summary>O tema pronto para uso, com arte e materiais gerados se faltarem.</summary>
    public static UiTheme Theme
    {
        get
        {
            UiArtGenerator.EnsureGenerated();

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                EditorSetupUtil.EnsureFolder(ThemePath);
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            Fill(theme);
            return theme;
        }
    }

    [MenuItem("Rewind Run/Interface/Atualizar Tema", priority = 31)]
    static void Refresh()
    {
        UiArtGenerator.GenerateAll();
        Selection.activeObject = Theme;
    }

    static void Fill(UiTheme theme)
    {
        theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (theme.font == null)
        {
            Debug.LogError("Fonte do TextMeshPro não encontrada. Importe os recursos essenciais em " +
                           "Window > TextMeshPro > Import TMP Essential Resources.");
            return;
        }

        theme.titleMaterial = TextMaterial(theme.font, "Titulo", outline: 0.22f,
                                           outlineColor: new Color(0.10f, 0.05f, 0.03f),
                                           shadow: new Vector2(0.9f, -0.9f), shadowAlpha: 0.75f, softness: 0.25f);
        theme.labelMaterial = TextMaterial(theme.font, "Rotulo", outline: 0.08f,
                                           outlineColor: new Color(0.08f, 0.05f, 0.04f),
                                           shadow: new Vector2(0.5f, -0.6f), shadowAlpha: 0.6f, softness: 0.35f);

        theme.stonePanel = UiArtGenerator.Load(UiArtGenerator.StonePath);
        theme.metalFrame = UiArtGenerator.Load(UiArtGenerator.FramePath);
        theme.glow = UiArtGenerator.Load(UiArtGenerator.GlowPath);
        theme.shade = UiArtGenerator.Load(UiArtGenerator.ShadePath);
        theme.rewindIcon = UiArtGenerator.Load(UiArtGenerator.RewindPath);
        theme.diamond = UiArtGenerator.Load(UiArtGenerator.DiamondPath);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Material de texto: cópia do material da fonte, com contorno e sombra.
    /// Precisa ser cópia - é o que o mantém preso ao atlas da fonte - e
    /// precisa ser asset, senão cada texto teria um material solto na cena.
    /// </summary>
    static Material TextMaterial(TMP_FontAsset font, string name, float outline, Color outlineColor,
                                 Vector2 shadow, float shadowAlpha, float softness)
    {
        string path = $"{MaterialFolder}/LiberationSans SDF - {name}.mat";
        EditorSetupUtil.EnsureFolder(path);

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material) { name = $"LiberationSans SDF - {name}" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = font.material.shader;
        material.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.6f);

        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, shadowAlpha));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadow.x);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadow.y);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);

        EditorUtility.SetDirty(material);
        return material;
    }
}
