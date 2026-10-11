using UnityEngine;

/// <summary>Medalha conquistada pelo tempo da corrida.</summary>
public enum Medal
{
    Nenhuma,
    Bronze,
    Prata,
    Ouro,
}

/// <summary>
/// Placar da tentativa atual: tempo, mortes e medalha.
///
/// "Tentativa" é literal: o tempo mede a corrida em andamento e volta a zero a
/// cada morte. Só as mortes atravessam as tentativas.
///
/// Os tempos de medalha ficam aqui, na cena da fase, e não numa tabela global.
/// Cada fase tem o seu ritmo, e um número central obrigaria a lembrar de
/// atualizá-lo toda vez que a geometria mudasse.
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Tooltip("Identificador da fase para guardar o recorde. Vazio usa o nome da cena.")]
    [SerializeField] string levelId;

    [Header("Tempos das medalhas, em segundos")]
    [SerializeField, Min(1f)] float goldSeconds = 75f;
    [SerializeField, Min(1f)] float silverSeconds = 100f;
    [SerializeField, Min(1f)] float bronzeSeconds = 140f;

    public int Deaths { get; private set; }

    /// <summary>Tempo de jogo. Para de contar quando a fase termina.</summary>
    public float Elapsed => _finished ? _finalTime : Time.time - _startTime;

    public bool Finished => _finished;

    /// <summary>Melhor tempo guardado antes desta corrida. Zero quando não há.</summary>
    public float PreviousBest { get; private set; }

    /// <summary>Esta corrida bateu o recorde.</summary>
    public bool IsNewRecord { get; private set; }

    public string LevelId =>
        string.IsNullOrEmpty(levelId)
            ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            : levelId;

    float _startTime;
    float _finalTime;
    bool _finished;

    void Awake()
    {
        Instance = this;
        _startTime = Time.time;
        PreviousBest = GameSettings.GetBestTime(LevelId);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Conta a morte e zera o relógio da tentativa.
    ///
    /// Morrer recomeça a fase do início, então deixar o cronômetro correndo
    /// mediria um percurso que ninguém fez: a soma de tentativas abandonadas.
    /// O que acumula é o contador de mortes, e é ele que conta quantas
    /// tentativas a corrida custou.
    ///
    /// Não precisa de hora sem escala. A tela de morte põe timeScale em zero, e
    /// Time.time é tempo escalado - ele congela junto, então o tempo parado
    /// lendo a tela não entra na conta.
    /// </summary>
    public void RegisterDeath()
    {
        Deaths++;
        _startTime = Time.time;
    }

    public void Finish()
    {
        if (_finished) return;

        _finalTime = Time.time - _startTime;
        _finished = true;

        IsNewRecord = GameSettings.TrySetBestTime(LevelId, _finalTime);
    }

    public Medal EarnedMedal => MedalFor(_finished ? _finalTime : Elapsed);

    public Medal MedalFor(float seconds)
    {
        if (seconds <= goldSeconds) return Medal.Ouro;
        if (seconds <= silverSeconds) return Medal.Prata;
        if (seconds <= bronzeSeconds) return Medal.Bronze;

        return Medal.Nenhuma;
    }

    /// <summary>Tempo limite da medalha seguinte, para a HUD mostrar o alvo.</summary>
    public float NextTarget(float seconds)
    {
        if (seconds <= goldSeconds) return goldSeconds;
        if (seconds <= silverSeconds) return silverSeconds;

        return bronzeSeconds;
    }

    public static Color ColorOf(Medal medal) => medal switch
    {
        Medal.Ouro => new Color(1f, 0.83f, 0.3f),
        Medal.Prata => new Color(0.82f, 0.85f, 0.9f),
        Medal.Bronze => new Color(0.84f, 0.53f, 0.3f),
        _ => new Color(0.6f, 0.58f, 0.56f),
    };

    /// <summary>Formato mm:ss,cc - centésimos porque platformer de tempo se mede assim.</summary>
    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;

        return $"{minutes:00}:{rest:00.00}".Replace('.', ',');
    }
}
