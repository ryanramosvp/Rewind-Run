using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Blocos de interface no estilo do jogo: texto TMP, placa de pedra com
/// moldura de metal, botão de menu e logotipo.
///
/// É a camada de cima do <see cref="UiBuilder"/>, que continua cuidando do
/// estrutural (canvas, retângulos, vínculos de botão). As telas antigas ainda
/// usam o UiBuilder direto; ao passarem para o tema novo, passam a usar isto.
/// </summary>
public static class ThemedUi
{
    public static UiTheme Theme => UiThemeSetup.Theme;

    // ------------------------------------------------------------------
    // Texto
    // ------------------------------------------------------------------

    /// <summary>
    /// Texto TMP que encolhe para caber, mas nunca abaixo de 60% do tamanho
    /// pedido, e nunca quebra linha - num botão ou num placar, uma palavra
    /// partida em duas linhas é pior que uma letra menor.
    /// </summary>
    public static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
                                       Vector2 anchor, Vector2 position, Vector2 box,
                                       TextAlignmentOptions alignment = TextAlignmentOptions.Center,
                                       bool bold = true, Material material = null, float spacing = 0f)
    {
        var theme = Theme;
        var rect = UiBuilder.CreateRect(parent, name, anchor, position, box);

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = theme.font;
        text.fontSharedMaterial = material != null ? material : theme.labelMaterial;
        text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Round(size * 0.6f);
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.characterSpacing = spacing;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    // ------------------------------------------------------------------
    // Placas
    // ------------------------------------------------------------------

    /// <summary>
    /// Placa de pedra com moldura de metal por cima. Devolve a pedra; a
    /// moldura sai em <paramref name="frame"/> para quem quiser pintá-la.
    /// </summary>
    public static Image Plate(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position,
                              Vector2 size, Color stone, out Image frame)
    {
        var theme = Theme;
        var rect = UiBuilder.CreateRect(parent, name, anchor, position, size);
        rect.pivot = pivot;

        var plate = rect.gameObject.AddComponent<Image>();
        plate.sprite = theme.stonePanel;
        plate.type = Image.Type.Sliced;
        plate.color = stone;
        plate.raycastTarget = false;

        frame = Stretched(rect, "Moldura", theme.metalFrame, theme.metal);
        return plate;
    }

    /// <summary>Imagem que ocupa o pai inteiro, com uma folga opcional para fora.</summary>
    public static Image Stretched(Transform parent, string name, Sprite sprite, Color color, float outset = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = -Vector2.one * outset;
        rect.offsetMax = Vector2.one * outset;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image Icon(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor,
                             Vector2 position, Vector2 size)
    {
        var rect = UiBuilder.CreateRect(parent, name, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // ------------------------------------------------------------------
    // Botão de menu
    // ------------------------------------------------------------------

    /// <summary>
    /// Botão de menu dentro de um contêiner próprio.
    ///
    /// O contêiner (<paramref name="slot"/>) é o que a entrada em cascata
    /// anima; o botão, por dentro, é o que cresce e brilha ao ser escolhido.
    /// Separados, a entrada e o estado do botão nunca disputam a mesma
    /// posição nem a mesma transparência.
    ///
    /// O brilho fica no contêiner, atrás do botão: dentro dele, desenharia
    /// por cima da pedra.
    /// </summary>
    public static Button MenuButton(Transform parent, string label, Vector2 position, Vector2 size,
                                    out RectTransform slot, out TextMeshProUGUI detail, bool withDetail = false)
    {
        var theme = Theme;

        slot = UiBuilder.CreateRect(parent, $"Slot_{label}", new Vector2(0.5f, 0.5f), position, size);

        var glow = Stretched(slot, "Brilho", theme.glow, new Color(1f, 1f, 1f, 0f), outset: 28f);

        var plate = Stretched(slot, $"Botao_{label}", theme.stonePanel, theme.stone);
        plate.raycastTarget = true;
        var root = plate.transform;

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Vertical, wrapAround = true };
        plate.gameObject.AddComponent<ButtonSound>();
        var group = plate.gameObject.AddComponent<CanvasGroup>();

        var frame = Stretched(root, "Moldura", theme.metalFrame, theme.metal);

        var marker = Icon(root, "Marcador", theme.diamond, theme.molten, new Vector2(0f, 0.5f),
                          new Vector2(34f, 0f), new Vector2(26f, 26f));

        float textLeft = 62f;
        float textWidth = size.x - textLeft - 28f;
        float labelY = withDetail ? 11f : 0f;

        var text = Text(root, "Rotulo", label, 40f, theme.sand, new Vector2(0f, 0.5f),
                        new Vector2(textLeft + textWidth * 0.5f, labelY), new Vector2(textWidth, 52f),
                        TextAlignmentOptions.MidlineLeft, spacing: 6f);

        detail = null;
        if (withDetail)
        {
            detail = Text(root, "Detalhe", "", 21f, theme.ash, new Vector2(0f, 0.5f),
                          new Vector2(textLeft + textWidth * 0.5f, -22f), new Vector2(textWidth, 26f),
                          TextAlignmentOptions.MidlineLeft, spacing: 4f);
        }

        var view = plate.gameObject.AddComponent<MenuButtonView>();
        EditorSetupUtil.SetReference(view, "theme", theme);
        EditorSetupUtil.SetReference(view, "plate", plate);
        EditorSetupUtil.SetReference(view, "frame", frame);
        EditorSetupUtil.SetReference(view, "glow", glow);
        EditorSetupUtil.SetReference(view, "marker", marker.rectTransform);
        EditorSetupUtil.SetReference(view, "label", text);
        EditorSetupUtil.SetReference(view, "detail", detail);
        EditorSetupUtil.SetReference(view, "group", group);

        // O estado inicial já desenhado, para a cena parecer certa no Editor.
        marker.rectTransform.localScale = Vector3.zero;

        return button;
    }

    // ------------------------------------------------------------------
    // Telas
    // ------------------------------------------------------------------

    /// <summary>Fundo de tela inteira, para escurecer o jogo atrás de um painel.</summary>
    public static Image Backdrop(Transform parent, Color color)
    {
        var image = UiBuilder.CreateFullScreen(parent, "Fundo", color);
        image.raycastTarget = true;   // bloqueia cliques no que está atrás
        return image;
    }

    /// <summary>
    /// Cartão de painel: placa de basalto com moldura de metal, uma faixa de
    /// pedra no alto com o título em ouro e um fio dourado embaixo dela.
    ///
    /// O conteúdo começa a <see cref="CardHeader"/> px do topo.
    /// </summary>
    public static RectTransform Card(Transform parent, string name, string title, Vector2 position, Vector2 size,
                                     out TextMeshProUGUI titleText)
    {
        var theme = Theme;
        var plate = Plate(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size,
                          new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.97f), out var frame);
        var card = plate.rectTransform;

        var band = UiBuilder.CreateRect(card, "Faixa", new Vector2(0.5f, 1f), new Vector2(0f, -14f - 39f),
                                        new Vector2(size.x - 28f, 78f));
        var bandImage = band.gameObject.AddComponent<Image>();
        bandImage.sprite = theme.stonePanel;
        bandImage.type = Image.Type.Sliced;
        bandImage.pixelsPerUnitMultiplier = 2f;
        bandImage.color = theme.stoneLit;
        bandImage.raycastTarget = false;

        var line = UiBuilder.CreateRect(card, "Fio", new Vector2(0.5f, 1f), new Vector2(0f, -14f - 80f),
                                        new Vector2(size.x - 60f, 3f)).gameObject.AddComponent<Image>();
        line.color = theme.gold;
        line.raycastTarget = false;

        titleText = Text(band, "Titulo", title, 48f, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                         new Vector2(size.x - 100f, 64f), material: theme.titleMaterial, spacing: 8f);
        titleText.enableVertexGradient = true;
        titleText.colorGradient = new VertexGradient(new Color(1f, 0.90f, 0.62f), new Color(1f, 0.90f, 0.62f),
                                                     theme.gold, theme.gold);

        // A moldura vai por cima da faixa, como numa placa de verdade.
        frame.transform.SetAsLastSibling();
        return card;
    }

    /// <summary>Altura ocupada pela faixa do título, a partir do topo do cartão.</summary>
    public const float CardHeader = 110f;

    /// <summary>Entrada de um painel: os itens sobem um pouco e aparecem, em cascata.</summary>
    public static UiIntroSequence Intro(Component holder, params RectTransform[] items)
    {
        var intro = holder.gameObject.AddComponent<UiIntroSequence>();
        EditorSetupUtil.SetReferences(intro, "items", items);

        var serialized = new SerializedObject(intro);
        serialized.FindProperty("offset").vector2Value = new Vector2(0f, -36f);
        serialized.FindProperty("startDelay").floatValue = 0f;
        serialized.FindProperty("stagger").floatValue = 0.05f;
        serialized.FindProperty("duration").floatValue = 0.3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return intro;
    }

    /// <summary>Bloco de número: rótulo pequeno em cima e o valor grande.</summary>
    public static TextMeshProUGUI Stat(Transform parent, string name, string label, Vector2 position, Vector2 size,
                                       Color valueColor)
    {
        var theme = Theme;
        var plate = Plate(parent, $"Bloco_{name}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                          size, theme.stone, out var frame);
        frame.color = theme.metalDark;

        Text(plate.transform, "Rotulo", label, 22f, theme.ash, new Vector2(0.5f, 1f),
             new Vector2(0f, -30f), new Vector2(size.x - 40f, 28f), spacing: 6f);

        return Text(plate.transform, "Valor", "--", 56f, theme.sand, new Vector2(0.5f, 0f),
                    new Vector2(0f, size.y * 0.5f - 22f), new Vector2(size.x - 40f, 70f),
                    material: theme.titleMaterial);
    }

    /// <summary>Linha de comando: a tecla numa plaquinha de pedra, a descrição ao lado.</summary>
    public static void KeyRow(Transform parent, string key, string description, float y, float width)
    {
        var theme = Theme;
        const float chipWidth = 250f;
        float chipX = -width * 0.5f + 40f + chipWidth * 0.5f;

        var chip = Plate(parent, $"Tecla_{key}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                         new Vector2(chipX, y), new Vector2(chipWidth, 64f), theme.stoneLit, out var frame);
        frame.color = theme.metal;
        chip.pixelsPerUnitMultiplier = 1.4f;
        frame.pixelsPerUnitMultiplier = 1.4f;

        Text(chip.transform, "Rotulo", key, 30f, theme.highlightText, new Vector2(0.5f, 0.5f), Vector2.zero,
             new Vector2(chipWidth - 30f, 50f), spacing: 3f);

        float left = chipX + chipWidth * 0.5f + 30f;
        float right = width * 0.5f - 40f;
        Text(parent, $"Descricao_{key}", description, 32f, theme.sand, new Vector2(0.5f, 0.5f),
             new Vector2((left + right) * 0.5f, y), new Vector2(right - left, 56f),
             TextAlignmentOptions.MidlineLeft, bold: false);
    }

    /// <summary>
    /// Linha de liga/desliga: um botão de menu com uma pastilha de estado à
    /// direita. A pastilha (cor e texto) é de quem usa - o MainMenu pinta.
    /// </summary>
    public static Button ToggleRow(Transform parent, string label, Vector2 position, Vector2 size,
                                   out TextMeshProUGUI state, out Image pill, out RectTransform slot)
    {
        var theme = Theme;
        var button = MenuButton(parent, label, position, size, out slot, out _);

        const float pillWidth = 210f;
        var rotulo = (RectTransform)button.transform.Find("Rotulo");
        float textWidth = size.x - 62f - 28f - pillWidth - 20f;
        rotulo.sizeDelta = new Vector2(textWidth, rotulo.sizeDelta.y);
        rotulo.anchoredPosition = new Vector2(62f + textWidth * 0.5f, rotulo.anchoredPosition.y);

        var pillRect = UiBuilder.CreateRect(button.transform, "Pastilha", new Vector2(1f, 0.5f),
                                            new Vector2(-28f - pillWidth * 0.5f, 0f), new Vector2(pillWidth, 52f));
        pill = pillRect.gameObject.AddComponent<Image>();
        pill.sprite = theme.stonePanel;
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 2.5f;
        pill.color = UiBuilder.Leaf;
        pill.raycastTarget = false;

        state = Text(pillRect, "Estado", "LIGADO", 26f, theme.highlightText, new Vector2(0.5f, 0.5f), Vector2.zero,
                     new Vector2(pillWidth - 24f, 40f), spacing: 4f);
        return button;
    }

    /// <summary>
    /// Linha com barra (volume): mesma placa dos botões, nome à esquerda,
    /// trilho no meio e o valor à direita. Escolhida, as setas para os lados
    /// mexem no valor e as de cima e baixo trocam de linha.
    /// </summary>
    public static Slider SliderRow(Transform parent, string label, Vector2 position, Vector2 size,
                                   out TextMeshProUGUI value, out RectTransform slot)
    {
        var theme = Theme;
        slot = UiBuilder.CreateRect(parent, $"Slot_{label}", new Vector2(0.5f, 0.5f), position, size);

        var glow = Stretched(slot, "Brilho", theme.glow, new Color(1f, 1f, 1f, 0f), outset: 28f);
        var plate = Stretched(slot, $"Linha_{label}", theme.stonePanel, theme.stone);
        var row = plate.transform;
        var group = plate.gameObject.AddComponent<CanvasGroup>();
        var frame = Stretched(row, "Moldura", theme.metalFrame, theme.metal);

        var marker = Icon(row, "Marcador", theme.diamond, theme.molten, new Vector2(0f, 0.5f),
                          new Vector2(34f, 0f), new Vector2(26f, 26f));
        marker.rectTransform.localScale = Vector3.zero;

        const float trackWidth = 360f, valueWidth = 110f;
        float nameWidth = size.x - 62f - trackWidth - valueWidth - 70f;
        var name = Text(row, "Rotulo", label, 40f, theme.sand, new Vector2(0f, 0.5f),
                        new Vector2(62f + nameWidth * 0.5f, 0f), new Vector2(nameWidth, 52f),
                        TextAlignmentOptions.MidlineLeft, spacing: 6f);

        // O trilho é o Slider: é nele que o clique e o arraste valem.
        var track = UiBuilder.CreateRect(row, "Trilho", new Vector2(1f, 0.5f),
                                         new Vector2(-28f - valueWidth - 20f - trackWidth * 0.5f, 0f),
                                         new Vector2(trackWidth, 22f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = theme.stonePanel;
        trackImage.type = Image.Type.Sliced;
        trackImage.pixelsPerUnitMultiplier = 3f;
        trackImage.color = theme.basalt;

        var fillArea = UiBuilder.CreateStretched(track, "AreaDePreenchimento");
        fillArea.offsetMin = new Vector2(5f, 5f);
        fillArea.offsetMax = new Vector2(-5f, -5f);
        var fill = Stretched(fillArea, "Preenchimento", null, theme.ember);
        fill.type = Image.Type.Simple;

        var handleArea = UiBuilder.CreateStretched(track, "AreaDoPunho");
        handleArea.offsetMin = new Vector2(12f, 0f);
        handleArea.offsetMax = new Vector2(-12f, 0f);
        var handle = Icon(handleArea, "Punho", theme.diamond, theme.gold, new Vector2(0.5f, 0.5f), Vector2.zero,
                          new Vector2(40f, 40f));
        handle.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(40f, 18f);

        var slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = trackImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.Vertical, wrapAround = true };
        track.gameObject.AddComponent<ButtonSound>();

        value = Text(row, "Valor", "80%", 34f, theme.gold, new Vector2(1f, 0.5f),
                     new Vector2(-28f - valueWidth * 0.5f, 0f), new Vector2(valueWidth, 52f),
                     material: theme.titleMaterial);

        var view = track.gameObject.AddComponent<MenuButtonView>();
        EditorSetupUtil.SetReference(view, "theme", theme);
        EditorSetupUtil.SetReference(view, "plate", plate);
        EditorSetupUtil.SetReference(view, "frame", frame);
        EditorSetupUtil.SetReference(view, "glow", glow);
        EditorSetupUtil.SetReference(view, "marker", marker.rectTransform);
        EditorSetupUtil.SetReference(view, "label", name);
        EditorSetupUtil.SetReference(view, "group", group);
        EditorSetupUtil.SetReference(view, "scaleTarget", row);

        return slider;
    }

    // ------------------------------------------------------------------
    // Logotipo
    // ------------------------------------------------------------------

    /// <summary>
    /// REWIND em ouro sobre laranja, e na linha de baixo o símbolo de voltar
    /// a fita colado no RUN em brasa: a mecânica do jogo dentro do nome. Uma
    /// barra de metal com fio dourado fecha o bloco.
    /// </summary>
    public static RectTransform Logo(Transform parent, Vector2 anchor, Vector2 position)
    {
        var theme = Theme;
        var logo = UiBuilder.CreateRect(parent, "Logo", anchor, position, new Vector2(780f, 300f));

        var rewind = Text(logo, "Rewind", "REWIND", 150f, Color.white, new Vector2(0.5f, 0.5f),
                          new Vector2(0f, 70f), new Vector2(780f, 160f), TextAlignmentOptions.MidlineLeft,
                          material: theme.titleMaterial, spacing: 4f);
        rewind.enableVertexGradient = true;
        rewind.colorGradient = new VertexGradient(new Color(1f, 0.90f, 0.62f), new Color(1f, 0.90f, 0.62f),
                                                  theme.molten, theme.ember);

        Icon(logo, "Simbolo", theme.rewindIcon, theme.ember, new Vector2(0.5f, 0.5f),
             new Vector2(-322f, -62f), new Vector2(116f, 116f));

        var run = Text(logo, "Run", "RUN", 150f, Color.white, new Vector2(0.5f, 0.5f),
                       new Vector2(70f, -62f), new Vector2(640f, 160f), TextAlignmentOptions.MidlineLeft,
                       material: theme.titleMaterial, spacing: 4f);
        run.enableVertexGradient = true;
        run.colorGradient = new VertexGradient(theme.molten, theme.molten, theme.ember, theme.danger);

        var bar = Plate(logo, "Barra", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(-390f, -146f), new Vector2(560f, 22f), theme.metalDark, out var barFrame);
        barFrame.color = theme.metal;

        // Placa fina: as bordas do 9-slice (32 px) não cabem em 22 de altura
        // e precisam encolher junto.
        bar.pixelsPerUnitMultiplier = 4f;
        barFrame.pixelsPerUnitMultiplier = 4f;

        var line = UiBuilder.CreateRect(bar.transform, "Fio", new Vector2(0f, 0.5f), new Vector2(280f, 0f),
                                        new Vector2(520f, 4f)).gameObject.AddComponent<Image>();
        line.color = theme.gold;
        line.raycastTarget = false;

        return logo;
    }
}
