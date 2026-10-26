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

    /// <summary>
    /// Limites de S a D da fase, copiados da cena na última vez que ela foi
    /// concluída. É o que deixa o menu e a tela de resultados darem nota sem
    /// abrir a cena. Guarda-se a tabela, e não o rank: recalibrada a fase,
    /// a próxima conclusão traz a tabela nova e as notas acompanham.
    /// </summary>
    public float[] rankLimits = new float[0];

    /// <summary>A nota de um tempo nesta fase, ou nulo sem tabela ou sem tempo.</summary>
    public Rank? RankOf(float seconds) =>
        seconds > 0f && Ranks.IsTable(rankLimits) ? Ranks.For(seconds, rankLimits) : (Rank?)null;

    /// <summary>A corrida concluída mais recente, ou nula.</summary>
    public RunEntry LastRun => recentRuns != null && recentRuns.Count > 0 ? recentRuns[0] : null;
}

/// <summary>
/// Onde o jogador está na campanha.
///
/// Fica separado dos recordes de propósito: o Novo jogo troca este objeto
/// inteiro e não encosta nos recordes, que são do jogador e não da campanha.
/// </summary>
[System.Serializable]
public class CampaignProgress
{
    /// <summary>A fase que o Continuar abre. Sempre uma fase da <see cref="Campaign"/>.</summary>
    public string currentLevel;

    /// <summary>Já entrou em alguma fase desde o último Novo jogo. Sem isso, não há o que continuar.</summary>
    public bool started;

    public List<string> unlockedLevels = new List<string>();
    public List<string> completedLevels = new List<string>();
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
/// save, e o save anterior vira o backup. Se o jogo fechar no meio, sobra o
/// save antigo inteiro em vez de um arquivo cortado; e se o save ficar
/// ilegível, a leitura cai no backup antes de começar do zero.
///
/// Grava só nos momentos que mudam alguma coisa - entrar numa fase, concluir,
/// Novo jogo, apagar - e nunca por quadro nem dependendo do jogo fechar
/// direito: o que importa já está em disco quando acontece.
/// </summary>
public static class SaveSystem
{
    // 2: campanha (fase atual, desbloqueadas, concluídas).
    const int kVersion = 2;
    const int kRecentRuns = 5;
    const string kFileName = "rewindrun_save.json";

    [System.Serializable]
    class SaveFile
    {
        public int version = kVersion;
        public CampaignProgress progress = new CampaignProgress();
        public List<LevelRecord> levels = new List<LevelRecord>();
    }

    static SaveFile _data;
    static bool _initialized;

    public static string FilePath => Path.Combine(Application.persistentDataPath, kFileName);
    static string BackupPath => FilePath + ".bak";
    static string TempPath => FilePath + ".tmp";

    /// <summary>
    /// Lê o save e grava de volta já normalizado. Chamada pelo menu ao abrir.
    ///
    /// Gravar logo de cara é o que cria o save na primeira execução e
    /// regrava no formato novo um save antigo ou consertado - assim o arquivo
    /// em disco nunca fica para trás do que o jogo está usando. Uma vez por
    /// execução: voltar ao menu não regrava nada.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Load();
        Write();
    }

    /// <summary>
    /// Esquece o que está em memória; a próxima leitura volta ao disco. É o
    /// que acontece ao reabrir o jogo, e é como os testes simulam isso.
    /// </summary>
    public static void Reload()
    {
        _data = null;
        _initialized = false;
    }

    // ------------------------------------------------------------------
    // Recordes
    // ------------------------------------------------------------------

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
        float legacy = PlayerPrefs.GetFloat(LegacyKey(levelId), 0f);
        if (IsValidTime(legacy)) created.bestTime = legacy;

        _data.levels.Add(created);
        return created;
    }

    /// <summary>
    /// Registra uma corrida concluída e grava, numa escrita só: a fase fica
    /// concluída, a seguinte desbloqueada e, se for o caso, o recorde novo.
    /// Devolve true quando ela bateu o recorde.
    ///
    /// O recorde só cai com um tempo válido e menor que o guardado. As mortes
    /// do recorde andam junto com ele: uma corrida mais lenta conta nas
    /// estatísticas, mas não mexe no par tempo-mortes do melhor resultado.
    /// </summary>
    public static bool RecordRun(string levelId, float time, int deaths, float[] rankLimits = null)
    {
        var record = Get(levelId);
        MarkCompleted(levelId);

        if (Ranks.IsTable(rankLimits)) record.rankLimits = (float[])rankLimits.Clone();

        if (!IsValidTime(time))
        {
            Debug.LogWarning($"Tempo inválido na fase {levelId} ({time}); recorde não alterado.");
            Write();
            return false;
        }

        deaths = Mathf.Max(0, deaths);

        bool isRecord = !IsValidTime(record.bestTime) || time < record.bestTime;
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

    // ------------------------------------------------------------------
    // Campanha
    // ------------------------------------------------------------------

    /// <summary>A fase que o Continuar abre.</summary>
    public static string CurrentLevel
    {
        get
        {
            Load();
            return _data.progress.currentLevel;
        }
    }

    /// <summary>Há uma campanha em andamento para continuar.</summary>
    public static bool HasProgress
    {
        get
        {
            Load();
            return _data.progress.started || _data.progress.completedLevels.Count > 0;
        }
    }

    public static bool IsUnlocked(string levelId)
    {
        Load();
        return _data.progress.unlockedLevels.Contains(levelId);
    }

    public static bool IsCompleted(string levelId)
    {
        Load();
        return _data.progress.completedLevels.Contains(levelId);
    }

    /// <summary>
    /// A bruxa entrou numa fase: ela vira a fase atual. Só a fase, sem
    /// posição nem estado de obstáculo - o Continuar sempre recomeça do
    /// início dela.
    ///
    /// Rejogar uma fase antiga também a torna a atual, mas não tira nada:
    /// desbloqueios, conclusões e recordes ficam como estavam.
    ///
    /// A tabela de ranks vem junto: assim um recorde feito antes de o save
    /// guardar tabelas ganha nota assim que a fase é aberta de novo.
    /// </summary>
    public static void EnterLevel(string levelId, float[] rankLimits = null)
    {
        if (!Campaign.Contains(levelId)) return;

        Load();
        var progress = _data.progress;

        bool changed = progress.currentLevel != levelId || !progress.started;

        var record = Get(levelId);
        if (Ranks.IsTable(rankLimits) && !SameTable(record.rankLimits, rankLimits))
        {
            record.rankLimits = (float[])rankLimits.Clone();
            changed = true;
        }

        progress.currentLevel = levelId;
        progress.started = true;

        // Chegou aqui, então está aberta - vale também para quem entra pela
        // tela de vitória da fase anterior ou direto pelo Editor.
        changed |= AddOnce(progress.unlockedLevels, levelId);

        if (changed) Write();
    }

    /// <summary>
    /// Recomeça a campanha do zero, preservando os recordes.
    /// A primeira fase continua aberta; as outras voltam a ser bloqueadas.
    /// </summary>
    public static void NewGame()
    {
        Load();
        _data.progress = new CampaignProgress();
        Normalize(_data);
        Write();
    }

    /// <summary>
    /// Apaga tudo: campanha e recordes. Leva junto o backup e os recordes do
    /// sistema antigo no PlayerPrefs - senão o próximo Get herdaria de lá o
    /// melhor tempo que acabou de ser apagado.
    /// </summary>
    public static void DeleteAll()
    {
        Load();

        foreach (var record in _data.levels) PlayerPrefs.DeleteKey(LegacyKey(record.levelId));
        foreach (var level in Campaign.Levels) PlayerPrefs.DeleteKey(LegacyKey(level.Id));
        PlayerPrefs.Save();

        _data = new SaveFile();
        Normalize(_data);
        Write();

        // Depois de gravar, e não antes: a gravação põe o save anterior - com
        // os recordes - no backup, e é dele que uma recuperação os traria de
        // volta.
        TryDelete(BackupPath);
    }

    static void MarkCompleted(string levelId)
    {
        if (!Campaign.Contains(levelId)) return;

        var progress = _data.progress;
        AddOnce(progress.completedLevels, levelId);
        AddOnce(progress.unlockedLevels, levelId);

        // A última fase não tem seguinte: ela continua sendo a atual, e o
        // Continuar a repete.
        string next = Campaign.NextOf(levelId);
        if (next == null) return;

        AddOnce(progress.unlockedLevels, next);

        // Concluir a fase atual avança a campanha. Concluir uma fase antiga,
        // rejogada pela seleção, não puxa a campanha de volta para trás.
        if (progress.currentLevel == levelId) progress.currentLevel = next;
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    static void Load()
    {
        if (_data != null) return;

        if (File.Exists(FilePath))
        {
            if (!TryRead(FilePath, out _data))
            {
                // Save ilegível não pode impedir o jogo de abrir. Ele é
                // guardado de lado, para dar para recuperar à mão, e a leitura
                // tenta o backup - a gravação anterior, que estava inteira.
                TryCopy(FilePath, FilePath + ".corrompido");

                // Sai do lugar: na próxima gravação, o File.Replace faria do
                // arquivo ilegível o novo backup, por cima do backup bom.
                TryDelete(FilePath);

                if (TryRead(BackupPath, out _data))
                    Debug.LogWarning("Save ilegível; usando o backup da gravação anterior.");
                else
                    Debug.LogWarning("Save ilegível e sem backup válido; começando do zero.");
            }
        }
        else if (TryRead(BackupPath, out _data))
        {
            // O jogo caiu entre apagar o save e pôr o novo no lugar.
            Debug.LogWarning("Save ausente; usando o backup da gravação anterior.");
        }

        if (_data == null) _data = new SaveFile();
        Normalize(_data);
    }

    static bool TryRead(string path, out SaveFile data)
    {
        data = null;

        try
        {
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;

            data = JsonUtility.FromJson<SaveFile>(json);
            return data != null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Não consegui ler {path}: {e.Message}");
            data = null;
            return false;
        }
    }

    /// <summary>
    /// Conserta o que um save antigo, editado à mão ou meio corrompido pode
    /// trazer: listas nulas, fases que não existem, recordes negativos, fase
    /// atual bloqueada. Nada aqui lança erro; o pior caso vira um valor padrão.
    /// </summary>
    static void Normalize(SaveFile data)
    {
        if (data.levels == null) data.levels = new List<LevelRecord>();
        data.levels.RemoveAll(record => record == null || string.IsNullOrEmpty(record.levelId));

        foreach (var record in data.levels)
        {
            if (record.recentRuns == null) record.recentRuns = new List<RunEntry>();
            if (record.rankLimits == null) record.rankLimits = new float[0];
            record.recentRuns.RemoveAll(run => run == null || !IsValidTime(run.time));

            if (!IsValidTime(record.bestTime))
            {
                record.bestTime = 0f;
                record.bestRunDeaths = 0;
            }

            record.bestRunDeaths = Mathf.Max(0, record.bestRunDeaths);
            record.completions = Mathf.Max(0, record.completions);
            record.totalDeaths = Mathf.Max(0, record.totalDeaths);
        }

        bool fromVersion1 = data.version < 2 || data.progress == null;

        var progress = data.progress ?? new CampaignProgress();
        data.progress = progress;
        if (progress.unlockedLevels == null) progress.unlockedLevels = new List<string>();
        if (progress.completedLevels == null) progress.completedLevels = new List<string>();

        progress.unlockedLevels.RemoveAll(id => !Campaign.Contains(id));
        progress.completedLevels.RemoveAll(id => !Campaign.Contains(id));

        // O save da versão 1 não tinha campanha. Quem já tinha concluído uma
        // fase não pode perder o acesso a ela e à seguinte na atualização.
        if (fromVersion1)
        {
            foreach (var record in data.levels)
            {
                if (record.completions > 0 && Campaign.Contains(record.levelId))
                    AddOnce(progress.completedLevels, record.levelId);
            }
        }

        AddOnce(progress.unlockedLevels, Campaign.FirstLevel);
        foreach (var id in progress.completedLevels.ToArray())
        {
            AddOnce(progress.unlockedLevels, id);
            string next = Campaign.NextOf(id);
            if (next != null) AddOnce(progress.unlockedLevels, next);
        }

        // A fase atual precisa existir e estar aberta. Senão, a mais adiantada
        // das abertas - é onde a campanha parou.
        if (!progress.unlockedLevels.Contains(progress.currentLevel))
        {
            progress.currentLevel = Campaign.FirstLevel;
            for (int i = Campaign.Levels.Length - 1; i >= 0; i--)
            {
                if (progress.unlockedLevels.Contains(Campaign.Levels[i].Id))
                {
                    progress.currentLevel = Campaign.Levels[i].Id;
                    break;
                }
            }
        }

        if (fromVersion1 && progress.completedLevels.Count > 0) progress.started = true;

        data.version = kVersion;
    }

    static void Write()
    {
        if (_data == null) return;

        try
        {
            File.WriteAllText(TempPath, JsonUtility.ToJson(_data, prettyPrint: true));

            // O save atual vira o backup na mesma operação que põe o novo no
            // lugar.
            if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, BackupPath);
            else File.Move(TempPath, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Não consegui gravar o save em {FilePath}: {e.Message}");
        }
    }

    // ------------------------------------------------------------------

    static bool IsValidTime(float seconds) =>
        seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);

    static string LegacyKey(string levelId) => $"rr.best.{levelId}";

    static bool SameTable(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!Mathf.Approximately(a[i], b[i])) return false;
        }

        return true;
    }

    static bool AddOnce(List<string> list, string id)
    {
        if (string.IsNullOrEmpty(id) || list.Contains(id)) return false;
        list.Add(id);
        return true;
    }

    static void TryCopy(string from, string to)
    {
        try
        {
            File.Copy(from, to, overwrite: true);
        }
        catch (System.Exception)
        {
            // Sem cópia, segue sem ela: o importante é o jogo abrir.
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Não consegui apagar {path}: {e.Message}");
        }
    }
}
