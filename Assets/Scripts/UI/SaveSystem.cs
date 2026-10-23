using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Uma corrida concluída.</summary>
[System.Serializable]
public class RunEntry
{
    public float time;
    public int deaths;

    /// <summary>Data e hora no formato ISO, para ordenar e exibir sem ambiguidade.</summary>
    public string date;
}

/// <summary>Tudo que o jogo lembra de uma fase.</summary>
[System.Serializable]
public class LevelRecord
{
    public string levelId;

    /// <summary>Melhor tempo, em segundos. Zero quando a fase nunca foi concluída.</summary>
    public float bestTime;

    /// <summary>Mortes na corrida que fez o melhor tempo.</summary>
    public int bestRunDeaths;

    public int completions;

    /// <summary>Mortes somadas de todas as corridas concluídas.</summary>
    public int totalDeaths;

    /// <summary>As últimas corridas, da mais nova para a mais antiga.</summary>
    public List<RunEntry> recentRuns = new List<RunEntry>();
}

/// <summary>
/// Save do jogo: um arquivo JSON por jogador.
///
/// Substitui o melhor tempo que ficava no PlayerPrefs. Um número por fase
/// cabia lá; histórico de corridas, mortes e vitórias por fase é dado
/// estruturado, e o PlayerPrefs no Windows mora no registro - onde ninguém
/// acha, copia ou apaga para recomeçar.
///
/// O rank NÃO é gravado, só o tempo. O rank é derivado da tabela de tempos da
/// fase, e essa tabela muda quando a fase é recalibrada; gravado, ele ficaria
/// dizendo "S" para um tempo que a tabela nova chama de A.
///
/// A escrita é atômica: vai para um arquivo temporário e só então substitui o
/// save. Se o jogo fechar no meio, sobra o save antigo inteiro em vez de um
/// arquivo cortado que apagaria todos os recordes.
/// </summary>
public static class SaveSystem
{
    const int kVersion = 1;
    const int kRecentRuns = 5;
    const string kFileName = "rewindrun_save.json";

    [System.Serializable]
    class SaveFile
    {
        public int version = kVersion;
        public List<LevelRecord> levels = new List<LevelRecord>();
    }

    static SaveFile _data;

    public static string FilePath => Path.Combine(Application.persistentDataPath, kFileName);

    /// <summary>O registro de uma fase. Nunca nulo: fase nova volta zerada.</summary>
    public static LevelRecord Get(string levelId)
    {
        Load();

        foreach (var record in _data.levels)
        {
            if (record.levelId == levelId) return record;
        }

        var created = new LevelRecord { levelId = levelId };

        // Herda o melhor tempo do sistema antigo, para quem já jogou não
        // perder o recorde na troca. As outras contagens não existiam lá e
        // começam do zero.
        float legacy = PlayerPrefs.GetFloat($"rr.best.{levelId}", 0f);
        if (legacy > 0f) created.bestTime = legacy;

        _data.levels.Add(created);
        return created;
    }

    /// <summary>
    /// Registra uma corrida concluída e grava.
    /// Devolve true quando ela bateu o recorde.
    /// </summary>
    public static bool RecordRun(string levelId, float time, int deaths)
    {
        var record = Get(levelId);

        bool isRecord = record.bestTime <= 0f || time < record.bestTime;
        if (isRecord)
        {
            record.bestTime = time;
            record.bestRunDeaths = deaths;
        }

        record.completions++;
        record.totalDeaths += deaths;

        record.recentRuns.Insert(0, new RunEntry
        {
            time = time,
            deaths = deaths,
            date = System.DateTime.Now.ToString("s"),
        });

        if (record.recentRuns.Count > kRecentRuns)
            record.recentRuns.RemoveRange(kRecentRuns, record.recentRuns.Count - kRecentRuns);

        Write();
        return isRecord;
    }

    static void Load()
    {
        if (_data != null) return;

        try
        {
            if (File.Exists(FilePath))
                _data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
        }
        catch (System.Exception e)
        {
            // Save ilegível não pode impedir o jogo de abrir. Ele é guardado
            // de lado em vez de sobrescrito, para dar para recuperar à mão.
            Debug.LogWarning($"Save ilegível, começando do zero: {e.Message}");
            TryKeepCorrupted();
        }

        if (_data == null || _data.levels == null) _data = new SaveFile();
    }

    static void Write()
    {
        try
        {
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(_data, prettyPrint: true));

            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Não consegui gravar o save em {FilePath}: {e.Message}");
        }
    }

    static void TryKeepCorrupted()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".corrompido", overwrite: true);
        }
        catch (System.Exception)
        {
            // Sem cópia, segue sem ela: o importante é o jogo abrir.
        }
    }
}
