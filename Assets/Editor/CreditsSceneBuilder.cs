using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a tela de créditos: o vulcão do menu ao fundo, escurecido, e uma
/// coluna de texto que sobe devagar até parar no "Obrigado por jogar". Quem
/// anima é o <see cref="TelaDeCreditos"/>.
///
/// O texto mora aqui, numa lista, para ser editado num lugar só. Tudo que é
/// de terceiros vai com o dono e a licença; o que o projeto não registra de
/// onde veio fica marcado [PREENCHER], em vez de inventado.
/// </summary>
public static class CreditsSceneBuilder
{
    public const string SceneName = "Creditos";
    public const string ScenePath = "Assets/Scenes/Creditos.unity";

    enum Tipo { Secao, Nome, Detalhe, Espaco, Fim }

    /// <summary>
    /// O conteúdo, de cima para baixo. O nome do autor pode ser trocado aqui
    /// (ou aparecer em mais funções); o resto segue os arquivos de licença
    /// que vieram com cada pacote.
    /// </summary>
    static readonly (Tipo tipo, string texto)[] Linhas =
    {
        (Tipo.Secao, "REWIND RUN"),
        (Tipo.Espaco, ""),

        (Tipo.Secao, "DESENVOLVIMENTO"),
        (Tipo.Nome, "Ryan Ramos"),
        (Tipo.Espaco, ""),
        (Tipo.Secao, "GAME DESIGN"),
        (Tipo.Nome, "Ryan Ramos"),
        (Tipo.Espaco, ""),
        (Tipo.Secao, "PROGRAMAÇÃO"),
        (Tipo.Nome, "Ryan Ramos"),
        (Tipo.Espaco, ""),

        (Tipo.Secao, "ARTE E RECURSOS"),
        (Tipo.Nome, "KayKit: Platformer Pack 1.0"),
        (Tipo.Detalhe, "Kay Lousberg - www.kaylousberg.com - licença CC0"),
        (Tipo.Nome, "KayKit: Adventurers Character Pack 2.0"),
        (Tipo.Detalhe, "Kay Lousberg - www.kaylousberg.com - licença CC0 (a bruxa é o modelo Mage)"),
        (Tipo.Nome, "Fonte Liberation Sans"),
        (Tipo.Detalhe, "© 2012 Red Hat, Inc. (dados digitalizados © 2010 Google) - SIL Open Font License 1.1"),
        (Tipo.Nome, "Texturas, céus, rochas, relógios e ampulhetas"),
        (Tipo.Detalhe, "gerados por código para este projeto"),
        (Tipo.Espaco, ""),

        (Tipo.Secao, "ÁUDIO E TRILHA"),
        (Tipo.Nome, "Interface Sounds 1.0"),
        (Tipo.Detalhe, "Kenney - www.kenney.nl - licença CC0"),
        (Tipo.Nome, "Efeitos sonoros da bruxa, da vitória e dos portais"),
        (Tipo.Detalhe, "Shapeforms Audio - Free Sound Effects"),
        (Tipo.Nome, "Sons das zonas, do impulso e dos cristais"),
        (Tipo.Detalhe, "sintetizados por código para este projeto"),
        (Tipo.Espaco, ""),
        (Tipo.Nome, "Bach - Badinerie, BWV 1067"),
        (Tipo.Detalhe, "gravação: Gregor Quendel (www.gregorquendel.com) - www.classicals.de"),
        (Tipo.Nome, "Tchaikovsky - Sinfonia nº 4, IV. Finale"),
        (Tipo.Detalhe, "gravação: Gregor Quendel (www.gregorquendel.com) - www.classicals.de"),
        (Tipo.Nome, "Paganini - Sonata nº 33, MS 84"),
        (Tipo.Detalhe, "gravação: Gregor Quendel - www.classicals.de - CC BY-NC 4.0"),
        (Tipo.Nome, "Saint-Saëns - Danse Macabre, Op. 40"),
        (Tipo.Detalhe, "gravação: Kevin MacLeod (www.incompetech.com) - www.classicals.de - CC BY 4.0"),
        (Tipo.Nome, "Debussy - Rêverie, L. 68"),
        (Tipo.Detalhe, "gravação: Gregor Quendel - www.classicals.de - CC BY-NC 4.0"),
        (Tipo.Nome, "Domenico Scarlatti - Stabat Mater, Amen"),
        (Tipo.Detalhe, "The Tudor Consort (www.tudor-consort.org.nz) - CC BY 4.0 - final cortado (aplausos)"),
        (Tipo.Nome, "Casual Soldiers, Ironworks e Homecoming"),
        (Tipo.Detalhe, "Andrea Baroni - The Free Music Pack - andreabaroni.com"),
        (Tipo.Espaco, ""),

        (Tipo.Secao, "FERRAMENTAS"),
        (Tipo.Nome, "Unity 6 - Unity Technologies"),
        (Tipo.Espaco, ""),

        (Tipo.Secao, "AGRADECIMENTOS"),
        (Tipo.Nome, "A todos que jogaram"),
        (Tipo.Espaco, ""),
        (Tipo.Espaco, ""),
        (Tipo.Fim, "Obrigado por jogar"),
    };

    [MenuItem("Rewind Run/Criar Tela de Créditos", priority = 12)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();

        MenuSceneBuilder.BuildBackdrop();
        CriarInterface();
        LevelKit.CreateAudio();
        LevelKit.CreateMusic(LevelKit.CreditsMusic);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);
        Debug.Log($"Tela de créditos criada em {ScenePath}.");
    }

    static void CriarInterface()
    {
        var theme = ThemedUi.Theme;
        var canvas = UiBuilder.CreateCanvas("Creditos", sortOrder: 10);
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var root = canvas.transform;

        var entrada = canvas.gameObject.AddComponent<CanvasGroup>();
        ThemedUi.Backdrop(root, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.78f));

        // Faixa central mais escura atrás do texto: o vulcão continua nas bordas.
        var faixa = ThemedUi.Stretched(root, "Faixa", theme.shade, new Color(theme.basalt.r, theme.basalt.g, theme.basalt.b, 0.6f));
        faixa.type = Image.Type.Simple;
        faixa.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        faixa.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        faixa.rectTransform.offsetMin = new Vector2(-620f, 0f);
        faixa.rectTransform.offsetMax = new Vector2(620f, 0f);

        // A coluna: âncora no meio da tela, pivô no topo. Começa com o topo
        // logo abaixo da borda de baixo (a tela de referência tem 1080).
        var coluna = UiBuilder.CreateRect(root, "Coluna", new Vector2(0.5f, 0.5f), new Vector2(0f, -560f),
                                          new Vector2(1200f, 100f));
        coluna.pivot = new Vector2(0.5f, 1f);

        float y = 0f, ultimo = 0f;
        foreach (var (tipo, texto) in Linhas)
        {
            switch (tipo)
            {
                case Tipo.Espaco:
                    y += 70f;
                    break;
                case Tipo.Secao:
                    y += 20f;
                    Linha(coluna, texto, ref y, 46f, theme.gold, 64f, theme.titleMaterial, 8f);
                    y += 10f;
                    break;
                case Tipo.Nome:
                    Linha(coluna, texto, ref y, 36f, theme.sand, 50f, null, 2f);
                    break;
                case Tipo.Detalhe:
                    Linha(coluna, texto, ref y, 24f, theme.ash, 36f, null, 1f, bold: false);
                    y += 8f;
                    break;
                case Tipo.Fim:
                    ultimo = y + 50f;
                    Linha(coluna, texto, ref y, 72f, theme.gold, 100f, theme.titleMaterial, 6f);
                    break;
            }
        }

        coluna.sizeDelta = new Vector2(1200f, y);

        // Para quando o "Obrigado por jogar" chega ao meio da tela.
        float parada = ultimo;

        var voltar = ThemedUi.MenuButton(root, "VOLTAR AO MENU", Vector2.zero, new Vector2(400f, 76f), out var slot, out _);
        var rect = slot;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        // Só no mouse: Enter e A aceleram a rolagem, e com o botão escolhido
        // acelerar sairia da tela (ver TelaDeCreditos).
        voltar.navigation = new Navigation { mode = Navigation.Mode.None };

        var dica = ThemedUi.Text(root, "Dica", "SEGURE ENTER / A  acelerar      ESC / B  menu", 22f,
                      new Color(theme.ash.r, theme.ash.g, theme.ash.b, 0.8f), new Vector2(0f, 0f),
                      new Vector2(380f, 40f), new Vector2(700f, 30f), TextAlignmentOptions.MidlineLeft,
                      bold: false, spacing: 2f);

        var tela = canvas.gameObject.AddComponent<TelaDeCreditos>();
        UiBuilder.Bind(voltar, tela, nameof(TelaDeCreditos.VoltarAoMenu));
        EditorSetupUtil.SetReference(tela, "coluna", coluna);
        EditorSetupUtil.SetReference(tela, "entrada", entrada);
        EditorSetupUtil.SetReference(tela, "voltar", voltar);
        EditorSetupUtil.SetReference(tela, "dica", dica);
        EditorSetupUtil.SetReference(tela, "rotuloDoBotao", voltar.transform.Find("Rotulo").GetComponent<TMP_Text>());
        LevelKit.SetFloat(tela, "paradaFinal", parada);
        LevelKit.SetFloat(tela, "velocidade", 50f);
    }

    static void Linha(RectTransform coluna, string texto, ref float y, float tamanho, Color cor, float altura,
                      Material material, float espaco, bool bold = true)
    {
        var t = ThemedUi.Text(coluna, "Linha", texto, tamanho, cor, new Vector2(0.5f, 1f),
                              new Vector2(0f, -y - altura * 0.5f), new Vector2(1160f, altura),
                              material: material, spacing: espaco, bold: bold);
        t.enableAutoSizing = true;
        y += altura;
    }
}
