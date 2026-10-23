using UnityEngine;

/// <summary>
/// Plataforma que vai e volta entre dois pontos, com espera nas pontas.
///
/// Anda em velocidade constante em vez de suavizar as pontas: para cronometrar
/// um salto o jogador precisa prever onde a plataforma vai estar, e aceleração
/// variável torna isso adivinhação. As esperas nas pontas é que dão o compasso.
///
/// Roda antes do jogador (execution order) porque ele lê o deslocamento dela
/// para ser carregado junto - se a ordem fosse a inversa, ele leria o valor do
/// frame anterior e escorregaria um frame atrás da plataforma.
///
/// No rewind o relógio anda para trás, como o da <see cref="RotatingHazard"/>.
/// A posição é função pura do tempo, então inverter o relógio devolve a
/// plataforma exatamente onde ela estava naquele instante. Antes ela congelava
/// durante o rewind: a bruxa voltava três segundos e a plataforma não, e o
/// jogador podia ser devolvido a um ponto onde a plataforma já não estava.
/// </summary>
[DefaultExecutionOrder(-200)]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Deslocamento em relação à posição inicial, no fim do percurso.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0f, 8f);

    [Tooltip("Tempo da ida, só a parte em movimento.")]
    [SerializeField, Min(0.1f)] float travelSeconds = 3f;

    [Tooltip("Parada na ponta de partida. É o que dá tempo de embarcar sem correria.")]
    [SerializeField, Min(0f)] float waitSeconds = 0.8f;

    [Tooltip("Tempo da volta. Zero usa o mesmo da ida. A prensa usa uma volta " +
             "mais lenta: cai depressa e sobe devagar, para a janela de passagem " +
             "ser lida.")]
    [SerializeField, Min(0f)] float returnSeconds;

    [Tooltip("Parada na ponta de chegada. Negativo usa a mesma da partida.")]
    [SerializeField] float endWaitSeconds = -1f;

    [Tooltip("Defasagem inicial, de 0 a 1 do ciclo. Serve para várias plataformas " +
             "não se moverem em bloco.")]
    [SerializeField, Range(0f, 1f)] float phase;

    /// <summary>Quanto a plataforma andou neste frame. O jogador soma isto ao próprio movimento.</summary>
    public Vector3 Delta { get; private set; }

    /// <summary>Velocidade atual, para o jogador herdar ao saltar.</summary>
    public Vector3 Velocity { get; private set; }

    Vector3 _start;
    float _time;

    void Awake()
    {
        _start = transform.position;
        _time = phase * CycleSeconds;
    }

    float ReturnSeconds => returnSeconds > 0f ? returnSeconds : travelSeconds;
    float EndWaitSeconds => endWaitSeconds >= 0f ? endWaitSeconds : waitSeconds;

    float CycleSeconds => waitSeconds + travelSeconds + EndWaitSeconds + ReturnSeconds;

    /// <summary>Está voltando no tempo agora. Quem mata pelo movimento consulta isto.</summary>
    public bool IsRewinding { get; private set; }

    void Update()
    {
        var manager = RewindManager.Instance;
        IsRewinding = manager != null && manager.IsRewinding;

        if (IsRewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        Vector3 next = _start + offset * Progress(Mathf.Repeat(_time, CycleSeconds));

        Delta = next - transform.position;
        Velocity = Time.deltaTime > 0f ? Delta / Time.deltaTime : Vector3.zero;

        transform.position = next;
    }

    /// <summary>Posição no ciclo, de 0 a 1: espera, ida, espera, volta.</summary>
    float Progress(float t)
    {
        if (t < waitSeconds) return 0f;
        t -= waitSeconds;

        if (t < travelSeconds) return t / travelSeconds;
        t -= travelSeconds;

        if (t < EndWaitSeconds) return 1f;
        t -= EndWaitSeconds;

        return 1f - t / ReturnSeconds;
    }
}
