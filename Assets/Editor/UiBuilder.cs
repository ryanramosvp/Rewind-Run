using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Monta telas de uGUI por código.
///
/// A paleta e os blocos de cor vêm do próprio atlas do KayKit, por RawImage
/// recortando uma célula dele. Isso resolve dois problemas de uma vez: a UI
/// passa a pertencer ao mesmo mundo do cenário em vez de parecer um menu
/// genérico colado por cima, e cada bloco ganha o leve gradiente que o atlas já
/// tem - que é o que separa uma superfície chapada de uma que parece material.
///
/// Usa o Text legado e a fonte embutida da engine de propósito: o TextMeshPro
/// exige um passo manual de importação antes de desenhar qualquer letra, e uma
/// cena gerada por menu não pode depender de alguém lembrar disso.
/// </summary>
public static class UiBuilder
{
    // ------------------------------------------------------------------
    // Paleta, amostrada do atlas do pack
    // ------------------------------------------------------------------

    public static readonly Color Ember = new Color(0.953f, 0.420f, 0.133f);   // #F36B22
    public static readonly Color Gold = new Color(0.816f, 0.592f, 0.271f);    // #D09745
    public static readonly Color Sand = new Color(0.855f, 0.682f, 0.490f);    // #DAAE7D
    public static readonly Color Clay = new Color(0.694f, 0.435f, 0.322f);    // #B16F52
    public static readonly Color Ash = new Color(0.831f, 0.859f, 0.871f);     // #D4DBDE
    public static readonly Color Ink = new Color(0.075f, 0.098f, 0.106f);     // #13191B
    public static readonly Color Panel = new Color(0.075f, 0.098f, 0.106f, 0.92f);
    public static readonly Color Leaf = new Color(0.0f, 0.518f, 0.329f);      // #008454

    /// <summary>Células do atlas, em coluna e linha. O atlas tem 8 x 4.</summary>
    public enum Swatch
    {
        Areia, Terracota, Marrom, Ardosia, Grafite,
        Laranja, Brasa, Ouro,
        Azul, Vermelho, Verde,
    }

    static Font _font;
    static Texture2D _atlas;

    static Font Font
    {
        get
        {
            if (_font != null) return _font;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            return _font;
        }
    }

    static Texture2D Atlas
    {
        get
        {
            if (_atlas == null)
            {
                _atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Art/KayKit_Platformer/platformer_texture.png");
            }

            return _atlas;
        }
    }

    static Sprite Sprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    // ------------------------------------------------------------------
    // Estrutura
    // ------------------------------------------------------------------

    public static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem),
                       typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor,
                                           Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return rect;
    }

    public static RectTransform CreateStretched(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // ------------------------------------------------------------------
    // Blocos
    // ------------------------------------------------------------------

    /// <summary>
    /// Bloco de cor recortado do atlas do jogo.
    ///
    /// O recorte traz junto o gradiente vertical que a célula tem no atlas, e é
    /// ele que impede o bloco de parecer um retângulo chapado de Photoshop.
    /// </summary>
    public static RawImage CreateBlock(Transform parent, string name, Swatch swatch,
                                       Vector2 anchor, Vector2 position, Vector2 size,
                                       float alpha = 1f)
    {
        var rect = CreateRect(parent, name, anchor, position, size);

        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = Atlas;
        image.uvRect = UvOf(swatch);
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;

        return image;
    }

    /// <summary>Retângulo arredondado de cor sólida, para fundos e molduras.</summary>
    public static Image CreatePanel(Transform parent, string name, Color color,
                                    Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = CreateRect(parent, name, anchor, position, size);

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = Sprite;
        image.type = Image.Type.Sliced;
        image.color = color;

        return image;
    }

    public static Image CreateFullScreen(Transform parent, string name, Color color)
    {
        var rect = CreateStretched(parent, name);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        return image;
    }

    /// <summary>
    /// Cartão: fundo escuro com uma faixa de cor no topo e o título dentro dela.
    ///
    /// A faixa é o que organiza a tela. Sem ela o conteúdo flutua no meio do
    /// nada; com ela cada bloco tem começo, e o olho sabe onde uma seção
    /// termina e a outra começa.
    /// </summary>
    public static RectTransform CreateCard(Transform parent, string name, string title,
                                           Swatch headerSwatch, Vector2 anchor,
                                           Vector2 position, Vector2 size, int titleSize = 54)
    {
        var card = CreateRect(parent, name, anchor, position, size);

        var body = card.gameObject.AddComponent<Image>();
        body.sprite = Sprite;
        body.type = Image.Type.Sliced;
        body.color = Panel;

        const float headerHeight = 86f;
        CreateBlock(card, "Faixa", headerSwatch, new Vector2(0.5f, 1f),
                    new Vector2(0f, -headerHeight * 0.5f), new Vector2(size.x - 16f, headerHeight));

        if (!string.IsNullOrEmpty(title))
        {
            CreateText(card, "Titulo", title, titleSize, Ink, new Vector2(0.5f, 1f),
                       new Vector2(0f, -headerHeight * 0.5f),
                       new Vector2(size.x - 80f, headerHeight - 14f),
                       style: FontStyle.Bold);
        }

        return card;
    }

    /// <summary>
    /// Texto que nunca sai da caixa.
    ///
    /// Duas travas, porque uma sozinha nao basta. A quebra resolve a largura,
    /// mas texto longo demais passa a transbordar por baixo; o melhor ajuste
    /// resolve a altura, mas sem quebra ele encolhe a fonte ate caber numa
    /// linha so, o que fica ilegivel. Juntos: quebra primeiro, e so encolhe o
    /// que ainda nao couber.
    ///
    /// O tamanho pedido passa a ser o TETO, nao o valor fixo. Texto curto sai
    /// no tamanho desenhado, e so o que estoura e reduzido - entao o visual
    /// planejado se mantem e o caso ruim degrada em vez de vazar.
    /// </summary>
    public static Text CreateText(Transform parent, string name, string content, int size,
                                  Color color, Vector2 anchor, Vector2 position, Vector2 box,
                                  TextAnchor alignment = TextAnchor.MiddleCenter,
                                  FontStyle style = FontStyle.Normal)
    {
        var rect = CreateRect(parent, name, anchor, position, box);

        var text = rect.gameObject.AddComponent<Text>();
        text.font = Font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = size;
        text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(size * 0.55f));
        text.raycastTarget = false;

        return text;
    }

    /// <summary>
    /// Linha de tecla: a tecla num bloco de cor, a descrição ao lado.
    ///
    /// Separar os dois em colunas resolve o alinhamento que uma lista de texto
    /// com espaços nunca acerta - a fonte não é monoespaçada, então "WASD" e
    /// "SHIFT" terminam em pontos diferentes e a coluna sai torta.
    /// </summary>
    public static void CreateKeyRow(Transform parent, string key, string description,
                                    float y, float width)
    {
        const float margin = 20f;
        const float gap = 30f;
        const float chipWidth = 220f;

        float half = width * 0.5f;
        float chipX = -half + margin + chipWidth * 0.5f;

        // A descricao ocupa o vao que sobra entre o chip e a margem direita,
        // medido em vez de chutado. O pivo e o centro, entao uma caixa larga
        // demais alinhada a esquerda comeca FORA do cartao - era o que fazia o
        // texto desta tela escapar pela lateral.
        float descLeft = chipX + chipWidth * 0.5f + gap;
        float descRight = half - margin;

        CreateBlock(parent, $"Tecla_{key}", Swatch.Ardosia, new Vector2(0.5f, 0.5f),
                    new Vector2(chipX, y), new Vector2(chipWidth, 64f));

        CreateText(parent, $"Rotulo_{key}", key, 34, Sand, new Vector2(0.5f, 0.5f),
                   new Vector2(chipX, y), new Vector2(chipWidth - 20f, 56f),
                   style: FontStyle.Bold);

        CreateText(parent, $"Descricao_{key}", description, 34, Ash, new Vector2(0.5f, 0.5f),
                   new Vector2((descLeft + descRight) * 0.5f, y),
                   new Vector2(descRight - descLeft, 64f), TextAnchor.MiddleLeft);
    }

    /// <summary>Número grande com um rótulo pequeno em cima, dentro de um bloco.</summary>
    public static Text CreateStat(Transform parent, string name, string label, Swatch swatch,
                                  Vector2 position, Vector2 size)
    {
        CreateBlock(parent, $"Bloco_{name}", swatch, new Vector2(0.5f, 0.5f),
                    position, size, alpha: 0.9f);

        CreateText(parent, $"Rotulo_{name}", label, 26, Ink, new Vector2(0.5f, 0.5f),
                   position + new Vector2(0f, size.y * 0.5f - 26f), new Vector2(size.x - 24f, 36f),
                   style: FontStyle.Bold);

        return CreateText(parent, $"Valor_{name}", "--", 46, Ink, new Vector2(0.5f, 0.5f),
                          position - new Vector2(0f, 14f), new Vector2(size.x - 24f, 66f),
                          style: FontStyle.Bold);
    }

    // ------------------------------------------------------------------
    // Interativos
    // ------------------------------------------------------------------

    /// <summary>Botão com uma barra de cor na borda esquerda, para não ser só um retângulo.</summary>
    public static Button CreateButton(Transform parent, string label, Vector2 anchor,
                                      Vector2 position, Vector2 size, Swatch accent = Swatch.Laranja)
    {
        var image = CreatePanel(parent, $"Botao_{label}", new Color(0.11f, 0.09f, 0.09f, 0.95f),
                                anchor, position, size);

        var button = image.gameObject.AddComponent<Button>();
        button.gameObject.AddComponent<ButtonSound>();

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.78f, 0.5f);
        colors.selectedColor = new Color(1f, 0.78f, 0.5f);
        colors.pressedColor = new Color(0.78f, 0.42f, 0.18f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CreateBlock(image.transform, "Realce", accent, new Vector2(0f, 0.5f),
                    new Vector2(14f, 0f), new Vector2(12f, size.y - 24f));

        // A caixa comeca depois da barra de realce e para antes da borda
        // direita. Usar o tamanho cheio do botao, como era antes, deixava a
        // caixa 14px mais larga que o proprio botao.
        float textLeft = 40f;
        float textWidth = Mathf.Max(60f, size.x - textLeft - 24f);

        CreateText(image.transform, "Rotulo", label, 40, Sand, new Vector2(0f, 0.5f),
                   new Vector2(textLeft + textWidth * 0.5f, 0f),
                   new Vector2(textWidth, size.y - 18f), style: FontStyle.Bold);

        return button;
    }

    /// <summary>
    /// Linha de opção: nome à esquerda, pastilha de estado à direita.
    ///
    /// A pastilha troca de cor e de texto. Verde ligado e cinza desligado se lê
    /// antes da palavra - a cor chega ao olho primeiro que a leitura.
    /// </summary>
    public static Button CreateToggleRow(Transform parent, string label, Vector2 position,
                                         Vector2 size, out Text stateLabel, out Image statePill)
    {
        var image = CreatePanel(parent, $"Opcao_{label}", new Color(0.11f, 0.09f, 0.09f, 0.95f),
                                new Vector2(0.5f, 0.5f), position, size);

        var button = image.gameObject.AddComponent<Button>();
        button.gameObject.AddComponent<ButtonSound>();

        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.78f, 0.5f);
        colors.selectedColor = new Color(1f, 0.78f, 0.5f);
        button.colors = colors;

        // O nome vai do recuo esquerdo ate onde a pastilha comeca. Antes a
        // caixa tinha 60% da largura mas estava ancorada na borda esquerda com
        // pivo central, entao metade dela ficava fora do painel.
        const float pillWidth = 220f;
        const float pillMargin = 20f;

        float nameLeft = 36f;
        float nameWidth = size.x - nameLeft - pillWidth - pillMargin * 2f;

        CreateText(image.transform, "Nome", label, 36, Sand, new Vector2(0f, 0.5f),
                   new Vector2(nameLeft + nameWidth * 0.5f, 0f),
                   new Vector2(nameWidth, size.y - 16f),
                   TextAnchor.MiddleLeft, FontStyle.Bold);

        statePill = CreatePanel(image.transform, "Pastilha", Leaf, new Vector2(1f, 0.5f),
                                new Vector2(-(pillWidth * 0.5f + pillMargin), 0f),
                                new Vector2(pillWidth, 56f));
        statePill.raycastTarget = false;

        stateLabel = CreateText(statePill.transform, "Estado", "LIGADO", 30, Ink,
                                new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(pillWidth - 20f, 48f),
                                style: FontStyle.Bold);

        return button;
    }

    /// <summary>
    /// Linha de ajuste contínuo: nome à esquerda, barra no meio, valor à direita.
    ///
    /// Barra e não botões de mais e menos porque volume é grandeza contínua, e
    /// o jogador quer parar onde achar bom, não percorrer degraus até lá.
    ///
    /// A montagem é a do Slider do uGUI, que exige a hierarquia certa para
    /// funcionar: o preenchimento tem de viver dentro de um recipiente próprio,
    /// porque é a âncora dele que o Slider move - não o tamanho. Fora dessa
    /// estrutura o componente aceita o valor e não desenha nada.
    /// </summary>
    public static Slider CreateSliderRow(Transform parent, string label, Vector2 position,
                                         Vector2 size, out Text valueLabel)
    {
        var panel = CreatePanel(parent, $"Opcao_{label}", new Color(0.11f, 0.09f, 0.09f, 0.95f),
                                new Vector2(0.5f, 0.5f), position, size);

        const float trackWidth = 380f;
        const float valueWidth = 150f;

        float nameLeft = 36f;
        float nameWidth = size.x - nameLeft - trackWidth - valueWidth - 70f;

        CreateText(panel.transform, "Nome", label, 36, Sand, new Vector2(0f, 0.5f),
                   new Vector2(nameLeft + nameWidth * 0.5f, 0f),
                   new Vector2(nameWidth, size.y - 16f),
                   TextAnchor.MiddleLeft, FontStyle.Bold);

        var slider = CreateSlider(panel.transform,
                                  new Vector2(-(valueWidth + trackWidth * 0.5f + 20f), 0f),
                                  new Vector2(trackWidth, 32f));

        valueLabel = CreateText(panel.transform, "Valor", "80%", 32, Gold, new Vector2(1f, 0.5f),
                                new Vector2(-valueWidth * 0.5f - 20f, 0f),
                                new Vector2(valueWidth, size.y - 20f));

        return slider;
    }

    /// <summary>A barra em si, com trilho, preenchimento e punho.</summary>
    static Slider CreateSlider(Transform parent, Vector2 position, Vector2 size)
    {
        var rect = CreateRect(parent, "Barra", new Vector2(1f, 0.5f), position, size);
        var slider = rect.gameObject.AddComponent<Slider>();

        var track = CreatePanel(rect, "Trilho", new Color(0.05f, 0.05f, 0.05f, 0.9f),
                                new Vector2(0.5f, 0.5f), Vector2.zero, size);
        track.raycastTarget = false;

        // Recipiente esticado: o Slider mexe na ancora do filho, e a ancora só
        // significa alguma coisa em relacao a um pai que ocupe a barra inteira.
        var fillArea = CreateStretched(rect, "AreaDePreenchimento");
        fillArea.offsetMin = new Vector2(4f, 4f);
        fillArea.offsetMax = new Vector2(-4f, -4f);

        var fill = new GameObject("Preenchimento", typeof(RectTransform));
        fill.transform.SetParent(fillArea, false);

        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        var fillImage = fill.AddComponent<Image>();
        fillImage.sprite = Sprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = Ember;
        fillImage.raycastTarget = false;

        var handleArea = CreateStretched(rect, "AreaDoPunho");
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);

        var handle = CreatePanel(handleArea, "Punho", Sand, new Vector2(0.5f, 0.5f),
                                 Vector2.zero, new Vector2(28f, size.y + 14f));

        var handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0.5f, 0f);
        handleRect.anchorMax = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(28f, 14f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        var colors = slider.colors;
        colors.highlightedColor = new Color(1f, 0.78f, 0.5f);
        colors.selectedColor = new Color(1f, 0.78f, 0.5f);
        slider.colors = colors;

        return slider;
    }

    /// <summary>Liga a barra a um método que recebe float, como o Bind faz com botões.</summary>
    public static void BindFloat(Slider slider, Object target, string methodName)
    {
        var action = System.Delegate.CreateDelegate(
                         typeof(UnityEngine.Events.UnityAction<float>),
                         target, methodName, false, false)
                     as UnityEngine.Events.UnityAction<float>;

        if (action == null)
        {
            Debug.LogWarning($"Método '{methodName}' não encontrado em {target.GetType().Name}.");
            return;
        }

        UnityEditor.Events.UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
    }

    /// <summary>Liga o botão a um método que recebe int, com o valor fixo gravado no vínculo.</summary>
    public static void BindInt(Button button, Object target, string methodName, int argument)
    {
        var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<int>),
                                                    target, methodName, false, false)
                     as UnityEngine.Events.UnityAction<int>;

        if (action == null)
        {
            Debug.LogWarning($"Método '{methodName}' não encontrado em {target.GetType().Name}.");
            return;
        }

        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
    }

    public static void Bind(Button button, Object target, string methodName)
    {
        var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction),
                                                    target, methodName, false, false)
                     as UnityEngine.Events.UnityAction;

        if (action == null)
        {
            Debug.LogWarning($"Método '{methodName}' não encontrado em {target.GetType().Name}.");
            return;
        }

        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(button.onClick, action);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Recorte da célula no atlas. A margem de meio por cento evita que o
    /// filtro puxe um fio da célula vizinha para dentro do bloco.
    /// </summary>
    static Rect UvOf(Swatch swatch)
    {
        (int column, int row) = swatch switch
        {
            Swatch.Areia => (7, 0),
            Swatch.Terracota => (5, 0),
            Swatch.Marrom => (6, 0),
            Swatch.Ardosia => (3, 0),
            Swatch.Grafite => (4, 0),
            Swatch.Brasa => (0, 1),
            Swatch.Laranja => (1, 1),
            Swatch.Ouro => (2, 1),
            Swatch.Azul => (0, 3),
            Swatch.Vermelho => (1, 3),
            Swatch.Verde => (3, 3),
            _ => (7, 0),
        };

        const float inset = 0.005f;
        float u = column * 0.125f + inset;
        float v = 1f - (row + 1) * 0.25f + inset;

        return new Rect(u, v, 0.125f - inset * 2f, 0.25f - inset * 2f);
    }
}
