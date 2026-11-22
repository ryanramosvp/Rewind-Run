using UnityEngine;

/// <summary>
/// Plataforma que gira em volta de um eixo vertical e carrega a bruxa junto
/// - o ponteiro do relógio gigante.
///
/// Gira com um <see cref="RelogioLocal"/>, então a zona de âmbar no centro do
/// relógio a deixa lenta o bastante para se andar em cima, e o rewind a devolve
/// ao ângulo exato daquele instante.
///
/// Carregar alguém em cima de algo que gira não é o mesmo que numa plataforma
/// que anda: o chão sob os pés descreve um arco, e quanto mais longe do eixo,
/// maior o arco. Por isso o deslocamento é calculado no ponto onde a bruxa
/// está (<see cref="IPlataformaMovel.DeslocamentoEm"/>), e não um valor só.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(RelogioLocal))]
public class PlataformaGiratoria : MonoBehaviour, IPlataformaMovel
{
    [Tooltip("Graus por segundo, fora de qualquer zona. Positivo gira no sentido horário visto de cima.")]
    [SerializeField] float grausPorSegundo = 60f;

    [Tooltip("Ângulo inicial, em graus.")]
    [SerializeField] float anguloInicial;

    RelogioLocal _relogio;
    Quaternion _repouso;
    float _anguloAnterior;
    float _giroDoQuadro;

    void Awake()
    {
        _relogio = GetComponent<RelogioLocal>();
        _repouso = transform.rotation;
        _anguloAnterior = anguloInicial;
        transform.rotation = Quaternion.AngleAxis(anguloInicial, Vector3.up) * _repouso;
    }

    void Update()
    {
        float angulo = anguloInicial + grausPorSegundo * _relogio.Tempo;
        _giroDoQuadro = angulo - _anguloAnterior;
        _anguloAnterior = angulo;
        transform.rotation = Quaternion.AngleAxis(angulo, Vector3.up) * _repouso;
    }

    public Vector3 DeslocamentoEm(Vector3 ponto)
    {
        // No rewind a bruxa é posicionada pela história, não carregada.
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return Vector3.zero;

        Vector3 eixo = transform.position;
        Vector3 relativo = ponto - eixo;
        Vector3 girado = Quaternion.AngleAxis(_giroDoQuadro, Vector3.up) * relativo;
        return new Vector3(girado.x - relativo.x, 0f, girado.z - relativo.z);
    }

    public Vector3 VelocidadeEm(Vector3 ponto) =>
        Time.deltaTime > 0f ? DeslocamentoEm(ponto) / Time.deltaTime : Vector3.zero;
}
