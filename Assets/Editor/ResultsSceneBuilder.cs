using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a tela de resultados da campanha, carregada depois da última fase.
///
/// O fundo é o do menu - o vulcão em miniatura que se desfaz e rebobina -,
/// então o jogo termina onde começou. Na frente, um cartão com uma linha por
/// fase (tempo, nota, mortes e o recorde ao lado), o total com a nota final e
/// as duas saídas: jogar de novo ou voltar ao menu. Quem preenche é o
/// <see cref="CampaignResults"/>, lendo o save.
/// </summary>
public static class ResultsSceneBuilder
{
    public const string SceneName = "Resultados";
    public const string ScenePath = "Assets/Scenes/Resultados.unity";

    // Colunas, a partir do centro do cartão.
    const float NameX = -400f, NameWidth = 520f;
    const float TimeX = 30f, TimeWidth = 260f;
    const float RankX = 240f, RankWidth = 120f;
    const float DeathsX = 380f, DeathsWidth = 130f;
    const float BestX = 560f, BestWidth = 230f;

    [MenuItem("Rewind Run/Criar Tela de Resultados", priority = 11)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        MenuSceneBuilder.BuildBackdrop();
        CreateInterface();
        LevelKit.CreateAudio();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        Debug.Log($"Tela de resultados criada em {ScenePath}.");
    }

    static void CreateInterface()
    {
        var theme = ThemedUi.Theme;
        var canvas = UiBuilder.CreateCanvas("Resultados", sortOrder: 10);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var root = canvas.transform;
        var results = canvas.gameObject.AddComponent<CampaignResults>();

        // Escurece sem apagar: o vulcão ainda aparece nas bordas.
        ThemedUi.Backdrop(root, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.6f));

        var card = ThemedUi.Card(root, "Cartao", "CAMPANHA CONCLUÍDA", new Vector2(0f, 40f),
                                 new Vector2(1440f, 900f), out _);

        float headerY = 450f - ThemedUi.CardHeader - 34f;
        Header(card, "FASE", NameX, NameWidth, headerY, TextAlignmentOptions.MidlineLeft);
        Header(card, "TEMPO", TimeX, TimeWidth, headerY);
        Header(card, "NOTA", RankX, RankWidth, headerY);
        Header(card, "MORTES", DeathsX, DeathsWidth, headerY);
        Header(card, "RECORDE", BestX, BestWidth, headerY);

        int count = Campaign.Levels.Length;
        var titles = new TMP_Text[count];
        var times = new TMP_Text[count];
        var ranks = new TMP_Text[count];
        var deaths = new TMP_Text[count];
        var bests = new TMP_Text[count];
        var items = new System.Collections.Generic.List<RectTransform> { card };

        float y = headerY - 72f;
        for (int i = 0; i < count; i++, y -= 104f)
        {
            var row = Row(card, $"Linha_{i + 1}", y, theme.stone, theme.metalDark);
            items.Add(row);

            titles[i] = ThemedUi.Text(row, "Fase", "", 36f, theme.sand, Center, new Vector2(NameX, 0f),
                                      new Vector2(NameWidth, 50f), TextAlignmentOptions.MidlineLeft, spacing: 3f);
            times[i] = ThemedUi.Text(row, "Tempo", "", 40f, theme.highlightText, Center, new Vector2(TimeX, 0f),
                                     new Vector2(TimeWidth, 54f), material: theme.titleMaterial);
            ranks[i] = ThemedUi.Text(row, "Nota", "", 62f, theme.gold, Center, new Vector2(RankX, 0f),
                                     new Vector2(RankWidth, 80f), material: theme.titleMaterial);
            deaths[i] = ThemedUi.Text(row, "Mortes", "", 38f, theme.sand, Center, new Vector2(DeathsX, 0f),
                                      new Vector2(DeathsWidth, 54f));
            bests[i] = ThemedUi.Text(row, "Recorde", "", 22f, theme.ash, Center, new Vector2(BestX, 0f),
                                     new Vector2(BestWidth, 30f), bold: false);
        }

        // Total: placa mais clara e moldura dourada - é a linha que resume.
        y -= 18f;
        var total = Row(card, "Total", y, theme.stoneLit, theme.gold);
        items.Add(total);
        ThemedUi.Text(total, "Rotulo", "TOTAL  ·  NOTA FINAL", 34f, theme.gold, Center, new Vector2(NameX, 0f),
                      new Vector2(NameWidth, 50f), TextAlignmentOptions.MidlineLeft, spacing: 4f);
        var totalTime = ThemedUi.Text(total, "Tempo", "", 44f, theme.highlightText, Center, new Vector2(TimeX, 0f),
                                      new Vector2(TimeWidth, 56f), material: theme.titleMaterial);
        var finalRank = ThemedUi.Text(total, "Nota", "", 76f, theme.gold, Center, new Vector2(RankX, 0f),
                                      new Vector2(RankWidth, 90f), material: theme.titleMaterial);
        var totalDeaths = ThemedUi.Text(total, "Mortes", "", 40f, theme.sand, Center, new Vector2(DeathsX, 0f),
                                        new Vector2(DeathsWidth, 56f));

        // Saídas, lado a lado: esquerda e direita navegam entre elas.
        var again = ThemedUi.MenuButton(card, "JOGAR DE NOVO", new Vector2(-250f, -350f), new Vector2(460f, 88f),
                                        out var againSlot, out _);
        var menu = ThemedUi.MenuButton(card, "MENU", new Vector2(250f, -350f), new Vector2(460f, 88f),
                                       out var menuSlot, out _);
        again.navigation = new Navigation { mode = Navigation.Mode.Horizontal, wrapAround = true };
        menu.navigation = new Navigation { mode = Navigation.Mode.Horizontal, wrapAround = true };
        items.Add(againSlot);
        items.Add(menuSlot);

        UiBuilder.Bind(again, results, nameof(CampaignResults.PlayAgain));
        UiBuilder.Bind(menu, results, nameof(CampaignResults.ToMenu));

        ThemedUi.Text(root, "Dica", "SETAS / ANALÓGICO  escolher      ENTER / A  confirmar      ESC / B  menu", 22f,
                      new Color(theme.ash.r, theme.ash.g, theme.ash.b, 0.8f), new Vector2(0.5f, 0f),
                      new Vector2(0f, 40f), new Vector2(1100f, 30f), bold: false, spacing: 2f);

        ThemedUi.Intro(canvas.GetComponent<RectTransform>(), items.ToArray());

        EditorSetupUtil.SetReferences(results, "titles", titles);
        EditorSetupUtil.SetReferences(results, "times", times);
        EditorSetupUtil.SetReferences(results, "ranks", ranks);
        EditorSetupUtil.SetReferences(results, "deaths", deaths);
        EditorSetupUtil.SetReferences(results, "bests", bests);
        EditorSetupUtil.SetReference(results, "totalTime", totalTime);
        EditorSetupUtil.SetReference(results, "totalDeaths", totalDeaths);
        EditorSetupUtil.SetReference(results, "finalRank", finalRank);
        EditorSetupUtil.SetReference(results, "playAgainButton", again);
        EditorSetupUtil.SetReference(results, "menuButton", menu);
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static RectTransform Row(RectTransform card, string name, float y, Color stone, Color frame)
    {
        var plate = ThemedUi.Plate(card, name, Center, Center, new Vector2(0f, y), new Vector2(1360f, 92f), stone,
                                   out var border);
        border.color = frame;
        return plate.rectTransform;
    }

    static void Header(RectTransform card, string text, float x, float width, float y,
                       TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var theme = ThemedUi.Theme;
        ThemedUi.Text(card, $"Coluna_{text}", text, 22f, theme.ash, Center, new Vector2(x, y),
                      new Vector2(width, 30f), alignment, spacing: 6f);
    }
}
