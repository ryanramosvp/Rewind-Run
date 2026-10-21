using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstrói o jogo inteiro num clique.
///
/// Todas as cenas são geradas por código, então qualquer mudança em movimento,
/// HUD ou perigos só aparece depois de rodar o construtor de cada uma. Fazer
/// isso à mão, em quatro itens de menu diferentes e na ordem certa, é o tipo de
/// tarefa que se esquece pela metade - e aí se testa uma fase velha achando que
/// é a nova, o que custa mais tempo do que a reconstrução inteira.
///
/// A ordem importa: a bruxa primeiro, porque as fases instanciam o prefab dela
/// e precisam do Animator já atualizado.
/// </summary>
public static class RebuildAll
{
    [MenuItem("Rewind Run/Reconstruir Tudo", priority = -10)]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        WitchSetup.Build();
        MenuSceneBuilder.Build();
        TutorialLevelBuilder.Build();
        SpeedrunLevelBuilder.Build();
        BubbleLevelBuilder.Build();

        Debug.Log("Reconstruído: bruxa, menu, tutorial, Corrida de Lava e Torre de Bolhas. " +
                  "A bancada de testes não entra aqui - ela é separada de propósito, " +
                  "para não sobrescrever o que você estiver conferindo nela.");
    }
}
