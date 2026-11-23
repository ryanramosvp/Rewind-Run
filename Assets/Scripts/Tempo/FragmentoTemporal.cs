using UnityEngine;

/// <summary>
/// Uma peça do cenário que a onda do <see cref="CicloDoColapso"/> desmonta.
///
/// A peça tem duas poses: a inteira (onde foi construída) e a solta (a
/// inteira mais um deslocamento e um giro). Entre as duas ela anda conforme o
/// ciclo diz o quanto o cenário naquela distância está solto. Com isso se
/// faz cada comportamento pedido sem um script para cada um:
///  - plataforma que se solta e flutua: deslocamento para cima;
///  - pedaço que escorrega de lado: deslocamento horizontal;
///  - torre que se divide: um fragmento por bloco, cada um com o seu;
///  - laje que inclina e vira rampa: giro em volta da borda (o pivô);
///  - estrutura que muda de orientação: giro grande;
///  - peça que se afasta e volta: é o próprio ciclo.
/// Solta, ela ainda balança devagar, para ler como coisa à deriva.
///
/// A pose é uma função do tempo do ciclo, e não física: o rewind volta a
/// peça exatamente para onde estava, e duas peças nunca se empurram. Quem
/// está em cima é carregado como numa plataforma móvel (<see cref="IPlataformaMovel"/>).
///
/// Rachaduras, se houver, acendem na preparação e quando a frente da onda
/// passa: o aviso de que esta peça vai se soltar.
/// </summary>
[DefaultExecutionOrder(-200)]
public class FragmentoTemporal : MonoBehaviour, IPlataformaMovel
{
    [Tooltip("Para onde a peça vai quando solta, a partir da pose inteira, em metros do mundo.")]
    [SerializeField] Vector3 deslocamento = new Vector3(0f, 3f, 0f);

    [Tooltip("Giro da peça solta, em graus, nos eixos dela. Gira em volta do pivô do objeto.")]
    [SerializeField] Vector3 giro;

    [Tooltip("Balanço da peça solta, em metros. Zero para peças de percurso que precisam ficar firmes.")]
    [SerializeField, Min(0f)] float balanco = 0.25f;

    [Tooltip("Metros somados à distância até o centro: atrasa (positivo) ou adianta a soltura.")]
    [SerializeField] float atraso;

    [Tooltip("Rachaduras que acendem antes de a peça se soltar. Opcional.")]
    [SerializeField] Renderer[] rachaduras = new Renderer[0];

    [Tooltip("Cor das rachaduras acesas.")]
    [SerializeField, ColorUsage(false, true)] Color corDaRachadura = new Color(0.6f, 1.6f, 2.4f);

    static readonly int Emissao = Shader.PropertyToID("_EmissionColor");
    static readonly int Base = Shader.PropertyToID("_BaseColor");

    Vector3 _posicao;
    Quaternion _rotacao;
    float _distancia = -1f;
    float _fase;
    Matrix4x4 _antes, _agora;
    MaterialPropertyBlock _bloco;
    float _ultimaRachadura = -1f;

    /// <summary>Quanto a peça está solta agora, de 0 a 1.</summary>
    public float Solta { get; private set; }

    void Awake()
    {
        _posicao = transform.position;
        _rotacao = transform.rotation;
        _agora = _antes = transform.localToWorldMatrix;

        // Fase do balanço a partir da posição: estável entre execuções, e
        // vizinhas não balançam juntas.
        _fase = Mathf.Repeat(_posicao.x * 0.37f + _posicao.z * 0.61f, Mathf.PI * 2f);
    }

    void Update()
    {
        var ciclo = CicloDoColapso.Instancia;
        if (ciclo == null) return;
        if (_distancia < 0f) _distancia = ciclo.DistanciaAte(_posicao) + atraso;

        Solta = ciclo.Desmontagem(_distancia);

        // Balanço preso ao tempo do ciclo, e não ao relógio do jogo: assim o
        // rewind o desfaz junto com o resto.
        float onda = ciclo.TempoTotal * 1.3f + _fase;
        Vector3 deriva = new Vector3(0f, Mathf.Sin(onda) * balanco, 0f);
        Quaternion oscila = Quaternion.Euler(Mathf.Sin(onda * 0.7f) * balanco * 4f, 0f, Mathf.Cos(onda * 0.6f) * balanco * 4f);

        _antes = _agora;
        transform.SetPositionAndRotation(_posicao + (deslocamento + deriva) * Solta,
                                         _rotacao * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(giro) * oscila, Solta));
        _agora = transform.localToWorldMatrix;

        AcenderRachaduras(ciclo.Rachadura(_distancia));
    }

    void AcenderRachaduras(float brilho)
    {
        if (rachaduras.Length == 0) return;
        // Só reescreve quando muda o bastante: dezenas de peças, todo quadro.
        if (Mathf.Abs(brilho - _ultimaRachadura) < 0.02f) return;
        _ultimaRachadura = brilho;

        _bloco ??= new MaterialPropertyBlock();
        _bloco.SetColor(Emissao, corDaRachadura * brilho);
        _bloco.SetColor(Base, Color.Lerp(new Color(0.1f, 0.12f, 0.16f), new Color(0.7f, 0.95f, 1f), Mathf.Clamp01(brilho)));
        foreach (var r in rachaduras)
            if (r != null) r.SetPropertyBlock(_bloco);
    }

    public Vector3 DeslocamentoEm(Vector3 ponto)
    {
        // No rewind a bruxa é posicionada pela história, não carregada.
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return Vector3.zero;

        // Onde o ponto estaria se tivesse andado junto com a peça neste quadro.
        Vector3 local = _antes.inverse.MultiplyPoint3x4(ponto);
        return _agora.MultiplyPoint3x4(local) - ponto;
    }

    public Vector3 VelocidadeEm(Vector3 ponto) =>
        Time.deltaTime > 0f ? DeslocamentoEm(ponto) / Time.deltaTime : Vector3.zero;

    void OnDrawGizmosSelected()
    {
        // A pose solta, para quem monta a fase ver para onde a peça vai.
        Vector3 inicio = Application.isPlaying ? _posicao : transform.position;
        Gizmos.color = new Color(0.4f, 0.85f, 1f);
        Gizmos.DrawLine(inicio, inicio + deslocamento);
        Gizmos.DrawWireSphere(inicio + deslocamento, 0.5f);
    }
}
