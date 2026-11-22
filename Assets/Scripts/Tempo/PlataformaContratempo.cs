using UnityEngine;

/// <summary>
/// Plataforma de cristal que só anda enquanto o tempo volta.
///
/// Durante o rewind ela avança no próprio percurso; ao soltar, fica onde
/// parou. Isso transforma o rewind de correção de erro em ferramenta: para
/// levar a ponte até o outro lado do vão, a bruxa volta no tempo de propósito,
/// e quanto mais tempo volta, mais a ponte anda.
///
/// Por isso ela NÃO entra na história do rewind - se entrasse, voltar no tempo
/// a devolveria ao lugar antigo, o contrário do que ela faz. O estado dela só
/// recomeça com a morte (a fase reinicia) ou com o fim do percurso.
///
/// Um fantasma do cristal marca a ponta do percurso desde o começo, e uma
/// fila de pontos liga as duas pontas: o jogador vê para onde ela vai antes
/// de descobrir como fazê-la ir.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlataformaContratempo : MonoBehaviour, IPlataformaMovel
{
    [Tooltip("Deslocamento até o fim do percurso, a partir da posição inicial.")]
    [SerializeField] Vector3 percurso = new Vector3(0f, 0f, 8f);

    [Tooltip("Segundos de rewind para ir de uma ponta à outra.")]
    [SerializeField, Min(0.1f)] float segundosDeRewind = 1.5f;

    [Tooltip("Brilho que acende enquanto ela anda. Opcional.")]
    [SerializeField] Renderer brilho;

    Vector3 _inicio;
    float _progresso;
    Vector3 _delta;

    /// <summary>De 0 (início) a 1 (fim do percurso).</summary>
    public float Progresso => _progresso;

    void Awake() => _inicio = transform.position;

    void Start()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += Recomecar;
    }

    void OnDestroy()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died -= Recomecar;
    }

    void Recomecar()
    {
        _progresso = 0f;
        transform.position = _inicio;
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        bool voltando = manager != null && manager.IsRewinding;

        Vector3 antes = transform.position;
        if (voltando && _progresso < 1f)
        {
            _progresso = Mathf.Min(1f, _progresso + Time.deltaTime * manager.RewindSpeed / segundosDeRewind);
            transform.position = _inicio + percurso * _progresso;
        }

        _delta = transform.position - antes;
        if (brilho != null) brilho.enabled = voltando && _progresso < 1f;
    }

    // Só anda durante o rewind, quando a bruxa é posicionada pela história e
    // não carregada; fora dele está parada. O deslocamento real ainda é
    // devolvido para o caso de alguém, um dia, mover a plataforma por fora.
    public Vector3 DeslocamentoEm(Vector3 ponto) => _delta;

    public Vector3 VelocidadeEm(Vector3 ponto) => Vector3.zero;

    void OnDrawGizmos()
    {
        Vector3 inicio = Application.isPlaying ? _inicio : transform.position;
        Gizmos.color = new Color(0.4f, 0.85f, 1f);
        Gizmos.DrawLine(inicio, inicio + percurso);
        Gizmos.DrawWireCube(inicio + percurso, Vector3.one);
    }
}
