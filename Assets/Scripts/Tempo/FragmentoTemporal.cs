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
///
/// Solta, a peça também reage ao peso, como uma plataforma boiando no
/// espaço: o pouso da bruxa a afunda - mais quanto mais alta a queda -, uma
/// mola a traz de volta balançando, e ela pende para o lado em que a bruxa
/// está. Encaixada, é chão firme: o efeito cresce com o quanto ela está
/// solta. A mola tem estado próprio, então esta peça grava esse estado na
/// história do rewind; o resto da pose continua saindo do ciclo.
/// </summary>
[DefaultExecutionOrder(-200)]
public class FragmentoTemporal : RewindableBehaviour<FragmentoTemporal.Mola>, IPlataformaMovel
{
    /// <summary>Estado da mola do peso: o que o rewind precisa guardar.</summary>
    public struct Mola
    {
        public float Afundamento, Velocidade;
        public Vector2 Inclinacao, Giro;
    }

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

    [Header("Peso (só com a peça solta)")]
    [Tooltip("A peça afunda e pende com a bruxa em cima. Desligado nos destroços e na rampa.")]
    [SerializeField] bool reageAoPeso = true;

    [Tooltip("Força da mola que traz a peça de volta. Mais alto, mais dura e rápida.")]
    [SerializeField, Min(1f)] float rigidez = 18f;

    [Tooltip("Quanto a mola perde a cada balanço. Baixo boia mais; alto assenta de uma vez.")]
    [SerializeField, Min(0f)] float amortecimento = 3.2f;

    [Tooltip("Metros que a peça desce só com a bruxa parada em cima.")]
    [SerializeField, Min(0f)] float afundamentoParada = 0.15f;

    [Tooltip("Velocidade dada à peça no pouso, por metro por segundo de queda.")]
    [SerializeField, Min(0f)] float impactoDoPouso = 0.18f;

    [Tooltip("Afundamento máximo, em metros. A validação dos saltos conta com ele.")]
    [SerializeField, Min(0f)] float afundamentoMaximo = 0.6f;

    [Tooltip("Inclinação máxima para o lado da bruxa, em graus.")]
    [SerializeField, Range(0f, 20f)] float inclinacaoMaxima = 5f;

    static readonly int Emissao = Shader.PropertyToID("_EmissionColor");
    static readonly int Base = Shader.PropertyToID("_BaseColor");

    Vector3 _posicao;
    Quaternion _rotacao;
    float _distancia = -1f;
    float _fase;
    Matrix4x4 _antes, _agora;
    MaterialPropertyBlock _bloco;
    float _ultimaRachadura = -1f;

    Mola _mola;
    PlayerController _bruxa;
    bool _bruxaEmCima;
    float _ultimaQueda;
    int _quadroDaQueda = -10;

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

    void Start()
    {
        if (!reageAoPeso) return;
        _bruxa = FindAnyObjectByType<PlayerController>();
        if (_bruxa != null) _bruxa.Landed += AoPousar;
        if (LevelRespawn.Instance != null) LevelRespawn.Instance.Died += Recomecar;
    }

    void OnDestroy()
    {
        if (_bruxa != null) _bruxa.Landed -= AoPousar;
        if (LevelRespawn.Instance != null) LevelRespawn.Instance.Died -= Recomecar;
    }

    // O aviso de pouso vem antes de se saber em que peça foi; guarda a queda
    // e a peça confere no próprio Update se a bruxa está nela.
    void AoPousar(float queda)
    {
        _ultimaQueda = queda;
        _quadroDaQueda = Time.frameCount;
    }

    void Recomecar() => _mola = default;

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

        if (reageAoPeso) AtualizarMola();

        // A mola só aparece com a peça solta: encaixada, é chão.
        Vector3 peso = Vector3.down * (_mola.Afundamento * Solta);
        Quaternion pende = Quaternion.Euler(_mola.Inclinacao.x * Solta, 0f, _mola.Inclinacao.y * Solta);

        _antes = _agora;
        transform.SetPositionAndRotation(_posicao + (deslocamento + deriva) * Solta + peso,
                                         _rotacao * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(giro) * oscila, Solta) * pende);
        _agora = transform.localToWorldMatrix;

        AcenderRachaduras(ciclo.Rachadura(_distancia));
    }

    /// <summary>
    /// Mola com amortecimento: o pouso dá um empurrão para baixo, o peso
    /// parado a mantém um pouco afundada, e ela volta balançando. A
    /// inclinação é outra mola, puxada para o lado em que a bruxa está.
    /// No rewind quem manda é a história gravada.
    /// </summary>
    void AtualizarMola()
    {
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool emCima = _bruxa != null && _bruxa.IsGrounded && ReferenceEquals(_bruxa.PlataformaSobOsPes, this);

        // Pousou nesta peça agora: o impacto, proporcional à queda.
        if (emCima && !_bruxaEmCima && Time.frameCount - _quadroDaQueda <= 2)
            _mola.Velocidade += _ultimaQueda * impactoDoPouso;
        _bruxaEmCima = emCima;

        float alvo = emCima ? afundamentoParada : 0f;
        float aceleracao = -rigidez * (_mola.Afundamento - alvo) - amortecimento * _mola.Velocidade;
        _mola.Velocidade += aceleracao * dt;
        _mola.Afundamento += _mola.Velocidade * dt;

        // Limite duro: o fundo do afundamento é parte da conta dos saltos.
        if (_mola.Afundamento > afundamentoMaximo)
        {
            _mola.Afundamento = afundamentoMaximo;
            if (_mola.Velocidade > 0f) _mola.Velocidade = 0f;
        }
        else if (_mola.Afundamento < -afundamentoMaximo * 0.5f)
        {
            _mola.Afundamento = -afundamentoMaximo * 0.5f;
            if (_mola.Velocidade < 0f) _mola.Velocidade = 0f;
        }

        // Pende para onde a bruxa está, medido no espaço da peça inteira.
        Vector2 alvoInclinacao = Vector2.zero;
        if (emCima)
        {
            Vector3 local = Quaternion.Inverse(_rotacao) * (_bruxa.transform.position - _posicao);
            const float raio = 3f;
            // Pé à frente (+Z) abaixa a frente: giro positivo em X; pé à
            // direita (+X) abaixa a direita: giro negativo em Z.
            alvoInclinacao = new Vector2(Mathf.Clamp(local.z / raio, -1f, 1f), -Mathf.Clamp(local.x / raio, -1f, 1f))
                             * inclinacaoMaxima;
        }

        var forca = -rigidez * (_mola.Inclinacao - alvoInclinacao) - amortecimento * _mola.Giro;
        _mola.Giro += forca * dt;
        _mola.Inclinacao += _mola.Giro * dt;
    }

    protected override Mola Capture() => _mola;

    protected override void Apply(in Mola older, in Mola newer, float t)
    {
        _mola.Afundamento = Mathf.Lerp(older.Afundamento, newer.Afundamento, t);
        _mola.Velocidade = Mathf.Lerp(older.Velocidade, newer.Velocidade, t);
        _mola.Inclinacao = Vector2.Lerp(older.Inclinacao, newer.Inclinacao, t);
        _mola.Giro = Vector2.Lerp(older.Giro, newer.Giro, t);
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
