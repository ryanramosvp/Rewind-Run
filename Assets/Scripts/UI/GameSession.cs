using UnityEngine;

/// <summary>
/// Placar da tentativa atual: tempo, mortes e rank.
///
/// "Tentativa" é literal: o tempo mede a corrida em andamento e volta a zero a
/// cada morte. Só as mortes atravessam as tentativas.
///
/// Os tempos de rank ficam aqui, na cena da fase, e não numa tabela global.
/// Cada fase tem o seu ritmo, e um número central obrigaria a lembrar de
/// atualizá-lo toda vez que a geometria mudasse.
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Tooltip("Identificador da fase para guardar o recorde. Vazio usa o nome da cena.")]
    [SerializeField] string levelId;

    [Tooltip("Tempo limite, em segundos, de cada rank: S, A, B, C e D, nessa ordem e " +
             "crescendo. Acima do último é F.")]
    [SerializeField] float[] rankSeconds = { 20f, 25f, 31f, 40f, 55f };

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

    /// <summary>O que o save sabe desta fase, já com a corrida atual depois do fim.</summary>
    public LevelRecord Record => SaveSystem.Get(LevelId);

    float _startTime;
    float _finalTime;
    bool _finished;

    void Awake()
    {
        Instance = this;
        _startTime = Time.time;
        PreviousBest = SaveSystem.Get(LevelId).bestTime;

        // Entrar já é progresso: é a fase que o Continuar vai abrir.
        SaveSystem.EnterLevel(LevelId);
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

        IsNewRecord = SaveSystem.RecordRun(LevelId, _finalTime, Deaths);
    }

    public Rank EarnedRank => RankFor(Elapsed);

    public Rank RankFor(float seconds)
    {
        for (int i = 0; i < Ranks.Timed && i < rankSeconds.Length; i++)
        {
            if (seconds <= rankSeconds[i]) return (Rank)i;
        }

        return Rank.F;
    }

    /// <summary>
    /// Tempo limite de um rank. O F não tem limite próprio e devolve o do D,
    /// que é a linha que ele não alcançou.
    /// </summary>
    public float LimitOf(Rank rank)
    {
        if (rankSeconds.Length == 0) return 0f;

        int index = Mathf.Min((int)rank, Ranks.Timed - 1);
        return rankSeconds[Mathf.Min(index, rankSeconds.Length - 1)];
    }

    /// <summary>Formato mm:ss,cc - centésimos porque platformer de tempo se mede assim.</summary>
    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;

        return $"{minutes:00}:{rest:00.00}".Replace('.', ',');
    }

    /// <summary>
    /// Diferença com sinal: "-1,23" quando se chegou antes do limite, "+4,56"
    /// quando depois. Acima de um minuto passa para mm:ss,cc, senão o número
    /// vira uma contagem de segundos difícil de ler.
    /// </summary>
    public static string FormatDelta(float seconds)
    {
        string sign = seconds <= 0f ? "-" : "+";
        float magnitude = Mathf.Abs(seconds);

        string body = magnitude >= 60f
            ? FormatTime(magnitude)
            : magnitude.ToString("0.00").Replace('.', ',');

        return sign + body;
    }
}
