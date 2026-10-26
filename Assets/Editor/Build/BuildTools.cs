using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Prepara e gera a build do jogo.
///
/// "Otimizar Configurações" aplica, num lugar só, cada ajuste que deixa a
/// build menor ou o jogo mais leve sem mudar o que se vê ou se ouve. Fica em
/// código, e não só clicado nas janelas do Unity, por dois motivos: cada
/// escolha vem com o porquê ao lado, e "Gerar Build" a reaplica antes de
/// compilar - uma configuração desfeita sem querer não chega ao executável.
///
/// O que fica de fora de propósito:
///  - IL2CPP: mais rápido que o Mono, mas exige as ferramentas de C++ do
///    Visual Studio instaladas; sem elas a build simplesmente falha.
///  - Sombras: quatro cascatas a 50 m são o que mantém nítida a sombra das
///    plataformas lá na frente, que o jogador usa para medir saltos.
///  - Pastas fora do jogo (_Recovery, shaders de teste): só entra na build o
///    que as cenas usam, então elas não pesam no executável.
/// </summary>
public static class BuildTools
{
    const string BuildFolder = "Builds/Windows";
    const string ExecutableName = "Rewind Run.exe";

    /// <summary>Qualidade Vorbis da música: 0,5 fica perto de 160 kbps, transparente para trilha de fundo.</summary>
    public const float MusicQuality = 0.5f;

    [MenuItem("Rewind Run/Build/Otimizar Configurações", priority = 40)]
    public static void Optimize()
    {
        var report = new System.Text.StringBuilder("Otimização aplicada:\n");

        CompressAtlases(report);
        CompressMusic(report);
        TuneRendering(report);
        TunePlayer(report);

        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }

    [MenuItem("Rewind Run/Build/Gerar Build Windows", priority = 41)]
    public static void BuildWindows()
    {
        Optimize();

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string path = Path.Combine(BuildFolder, ExecutableName);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            // LZ4HC: arquivos de dados bem menores e carregamento tão rápido
            // quanto sem compressão - descomprimir LZ4 custa quase nada.
            options = BuildOptions.CompressWithLz4HC,
        };

        var result = BuildPipeline.BuildPlayer(options);
        var summary = result.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            // Símbolos de depuração do Burst: o próprio nome pede para não ir
            // junto com o jogo.
            foreach (var folder in Directory.GetDirectories(BuildFolder, "*_DoNotShip"))
                Directory.Delete(folder, true);

            long bytes = Directory.GetFiles(BuildFolder, "*", SearchOption.AllDirectories)
                                  .Sum(f => new FileInfo(f).Length);
            Debug.Log($"Build pronta em {Path.GetFullPath(BuildFolder)}: {bytes / 1048576f:0.0} MB, " +
                      $"{summary.totalTime.TotalSeconds:0} s.");
            EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError($"A build falhou ({summary.result}, {summary.totalErrors} erros). Veja o Console.");
        }
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Os atlas do KayKit (plataformas e bruxa) em BC7.
    ///
    /// Iam sem compressão porque o DXT, o formato comum, faz degraus nos
    /// gradientes do atlas. O BC7 não: guarda gradiente suave quase igual ao
    /// original em um quarto do tamanho - 1 MB em vez de 4 por atlas, mais os
    /// mipmaps. São a maior parte da arte da build.
    /// </summary>
    static void CompressAtlases(System.Text.StringBuilder report)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/KayKit_Platformer", "Assets/Art/KayKit_Adventurers" });
        int changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            if (importer.textureCompression == TextureImporterCompression.CompressedHQ) continue;

            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            changed++;
        }

        report.AppendLine($"- {changed} textura(s) do KayKit passaram para BC7 ({guids.Length} no total).");
    }

    /// <summary>Música em streaming e Vorbis 0,5. Os efeitos curtos ficam como estão.</summary>
    static void CompressMusic(System.Text.StringBuilder report)
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Musica" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;

            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming &&
                settings.compressionFormat == AudioCompressionFormat.Vorbis &&
                Mathf.Approximately(settings.quality, MusicQuality)) continue;

            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = MusicQuality;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
            changed++;
        }

        report.AppendLine($"- {changed} música(s) recomprimida(s) em Vorbis {MusicQuality:0.0}.");
    }

    /// <summary>
    /// Dois cortes no que a GPU faz por quadro, sem mudar a imagem:
    ///  - Opaque Texture desligada: era uma cópia da tela inteira a cada
    ///    quadro, e nenhum shader do jogo a lê;
    ///  - SSAO em meia resolução: a sombra de canto é suave por natureza, e
    ///    calculada em metade da resolução custa um quarto e fica igual a olho.
    /// </summary>
    static void TuneRendering(System.Text.StringBuilder report)
    {
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp)) continue;

            if (urp.supportsCameraOpaqueTexture)
            {
                urp.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(urp);
                report.AppendLine($"- {urp.name}: Opaque Texture desligada.");
            }

            var renderers = new SerializedObject(urp).FindProperty("m_RendererDataList");
            for (int r = 0; r < renderers.arraySize; r++)
            {
                if (!(renderers.GetArrayElementAtIndex(r).objectReferenceValue is ScriptableRendererData data)) continue;

                foreach (var feature in data.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;

                    var serialized = new SerializedObject(feature);
                    var downsample = serialized.FindProperty("m_Settings.Downsample");
                    if (downsample == null || downsample.boolValue) continue;

                    downsample.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    report.AppendLine($"- {data.name}: SSAO em meia resolução.");
                }
            }
        }

        // VSync no nível de PC: sem ele o jogo roda solto, a centenas de
        // quadros por segundo no menu, esquentando a placa para nada - e
        // rasgando a imagem na tela.
        int pc = System.Array.IndexOf(QualitySettings.names, "PC");
        if (pc >= 0)
        {
            int current = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(pc, false);
            if (QualitySettings.vSyncCount != 1)
            {
                QualitySettings.vSyncCount = 1;
                report.AppendLine("- PC: VSync ligado.");
            }
            QualitySettings.SetQualityLevel(current, false);
        }
    }

    /// <summary>
    ///  - Stripping Low: tira do executável o código de biblioteca que o jogo
    ///    nunca chama. É o nível conservador - não mexe em nada usado por
    ///    reflexão, como o JsonUtility do save ou o Input System;
    ///  - pilha de chamadas só em erros: guardar a pilha de todo Log e
    ///    Warning custa a cada mensagem e não serve a quem joga.
    /// </summary>
    static void TunePlayer(System.Text.StringBuilder report)
    {
        var target = NamedBuildTarget.Standalone;
        if (PlayerSettings.GetManagedStrippingLevel(target) == ManagedStrippingLevel.Disabled)
        {
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            report.AppendLine("- Stripping de código: Low.");
        }

        if (PlayerSettings.GetStackTraceLogType(LogType.Log) != StackTraceLogType.None)
        {
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            report.AppendLine("- Pilha de chamadas só em erros.");
        }
    }
}
