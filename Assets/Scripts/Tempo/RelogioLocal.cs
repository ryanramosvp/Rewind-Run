using UnityEngine;

/// <summary>
/// O relógio próprio de um elemento do cenário, que as zonas de âmbar podem
/// desacelerar.
///
/// Os obstáculos do jogo já eram função do tempo (a posição da plataforma, o
/// ângulo da hélice), e no rewind andavam o relógio para trás na mesma
/// velocidade em que ele tinha andado para frente. Com zonas isso deixa de
/// ser verdade: uma plataforma que cruza a borda de uma zona andou a 25% num
/// trecho e a 100% no outro, e voltar "na conta" com a velocidade de agora
/// erraria um pouco a cada passagem. Por isso este relógio grava o próprio
/// valor na história do rewind e, ao voltar, lê o valor exato daquele
/// instante. Nenhum elemento fica fora do lugar depois de um rewind.
///
/// Roda antes das plataformas (execution order), para elas lerem o tempo
/// deste quadro, e não o do anterior.
///
/// Quem não tem este componente continua com o relógio de antes: as fases
/// antigas não mudam.
/// </summary>
[DefaultExecutionOrder(-250)]
public class RelogioLocal : RewindableBehaviour<float>
{
    [Tooltip("O que este elemento é, para as zonas: uma zona só de obstáculos não desacelera plataformas.")]
    [SerializeField] CanalTemporal canal = CanalTemporal.Plataformas;

    [Tooltip("Ponto onde a zona é medida. Vazio usa o próprio objeto; numa hélice " +
             "vale o eixo, que não sai do lugar.")]
    [SerializeField] Transform referencia;

    /// <summary>Tempo deste elemento, em segundos, desde o início da fase.</summary>
    public float Tempo { get; private set; }

    /// <summary>Velocidade do tempo aqui agora: 1 normal, menos dentro de uma zona.</summary>
    public float Escala { get; private set; } = 1f;

    void Awake()
    {
        if (referencia == null) referencia = transform;
    }

    void Update()
    {
        var manager = RewindManager.Instance;

        // No rewind quem manda é a história gravada (Apply); integrar aqui
        // somaria um tempo que não existiu.
        if (manager != null && manager.IsRewinding) return;

        Escala = ZonaTemporal.EscalaEm(referencia.position, canal);
        Tempo += Time.deltaTime * Escala;
    }

    protected override float Capture() => Tempo;

    protected override void Apply(in float older, in float newer, float t) => Tempo = Mathf.Lerp(older, newer, t);
}
