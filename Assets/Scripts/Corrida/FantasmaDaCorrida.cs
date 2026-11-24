using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// O fantasma: uma cópia translúcida da bruxa que corre a mesma fase, numa
/// rota desenhada junto com ela.
///
/// A rota vem pronta do construtor, em amostras a cada <see cref="passo"/>
/// segundos (posição e estado: no chão, no ar, de cabeça para baixo). Nada
/// de IA: o fantasma é previsível de propósito - o jogador aprende o ritmo
/// dele como aprende o de uma hélice, e a corrida é justa porque ele nunca
/// acelera para alcançar ninguém.
///
/// Linha do tempo própria: o relógio do fantasma anda com o jogo e NÃO volta
/// no rewind. Voltar no tempo conserta o erro da bruxa, mas o fantasma
/// continua correndo - o rewind custa tempo, e a corrida não fica trivial.
/// Morrer leva os dois ao último ponto de controle: o fantasma volta ao
/// instante em que a bruxa tocou aquele ponto, como numa repetição a partir
/// dali. Se esse instante já não deixar tempo para começar (o ponto foi
/// tocado com o fantasma quase na chegada), os dois recomeçam juntos ali.
///
/// Perder a corrida - o fantasma chegar primeiro - recomeça a fase inteira,
/// do início: a corrida é contra ele de ponta a ponta, e os pontos de
/// controle servem para as quedas, não para dividir a disputa em pedaços.
///
/// Num trecho da fase (<see cref="areaDoEco"/>) ele deixa a rota e passa a
/// repetir o caminho da própria bruxa com um pequeno atraso. Encostar nele
/// ali derruba a bruxa: hesitar é deixar o próprio passado alcançar.
///
/// Se o fantasma chega ao fim primeiro, a bruxa perde e volta ao último
/// ponto de controle.
/// </summary>
public class FantasmaDaCorrida : MonoBehaviour
{
    public static FantasmaDaCorrida Instancia { get; private set; }

    public const byte NoChao = 0, NoAr = 1, Invertido = 2;

    [Header("Rota")]
    [SerializeField] Vector3[] posicoes = new Vector3[0];

    [Tooltip("Estado de cada amostra: 0 no chão, 1 no ar; somado a 2 quando de cabeça para baixo.")]
    [SerializeField] byte[] estados = new byte[0];

    [Tooltip("Segundos entre duas amostras da rota.")]
    [SerializeField, Min(0.01f)] float passo = 0.05f;

    [Header("Eco (o fantasma repete a bruxa)")]
    [Tooltip("Área em que o fantasma passa a repetir os passos da bruxa. Tamanho zero desliga.")]
    [SerializeField] Bounds areaDoEco;

    [Tooltip("Atraso do eco, em segundos.")]
    [SerializeField, Min(0.3f)] float atrasoDoEco = 1.3f;

    [Tooltip("Distância em que o eco alcança a bruxa.")]
    [SerializeField, Min(0.2f)] float raioDeCaptura = 0.9f;

    [Tooltip("O eco só começa com o fantasma a até estes segundos da bruxa na corrida, " +
             "à frente ou atrás. Longe disso ele segue na própria rota: o eco não pode " +
             "trazer de volta um fantasma que já está quase na chegada.")]
    [SerializeField, Min(0.5f)] float distanciaDoEco = 4f;

    [Header("Imagem")]
    [Tooltip("Orientação do modelo: gira para onde ele corre e vira no teto.")]
    [SerializeField] Transform modelo;
    [SerializeField] Animator animator;
    [SerializeField] Renderer[] corpo = new Renderer[0];
    [SerializeField] ParticleSystem rastro;
    [SerializeField] ParticleSystem dissolucao;

    [Tooltip("Segundos com o aviso na tela antes de a fase recomeçar, depois de perder.")]
    [SerializeField, Min(0f)] float esperaDaDerrota = 1.8f;

    [Tooltip("Cor do corpo translúcido.")]
    [SerializeField] Color cor = new Color(0.55f, 0.85f, 1f, 0.45f);

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int Base = Shader.PropertyToID("_BaseColor");

    float[] _distancias;
    float _tempo;
    float _tempoNoPonto;
    float _distanciaDoPonto;
    int _dicaDaBruxa;
    bool _chegou;
    bool _parado;
    bool _noEco;
    float _opacidade = 1f;
    PlayerController _bruxa;
    MaterialPropertyBlock _bloco;
    Vector3 _ultimaPosicao;

    // Passos recentes da bruxa, para o eco: tempo, posição, de cabeça para baixo.
    readonly List<(float t, Vector3 p, bool invertida)> _passos = new List<(float, Vector3, bool)>();

    /// <summary>Segundos desde a largada, no relógio do fantasma.</summary>
    public float TempoDaCorrida => _tempo;
    public float Duracao => Mathf.Max(0f, (posicoes.Length - 1) * passo);
    public float DistanciaTotal => _distancias != null && _distancias.Length > 0 ? _distancias[_distancias.Length - 1] : 0f;
    public bool Parado => _parado;
    public bool NoEco => _noEco;

    /// <summary>Distância percorrida pelo fantasma na rota agora.</summary>
    public float DistanciaDoFantasma => DistanciaNoTempo(_tempo);

    void Awake()
    {
        Instancia = this;
        _bloco = new MaterialPropertyBlock();

        _distancias = new float[posicoes.Length];
        for (int i = 1; i < posicoes.Length; i++)
            _distancias[i] = _distancias[i - 1] + Vector3.Distance(posicoes[i - 1], posicoes[i]);
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Start()
    {
        _bruxa = FindAnyObjectByType<PlayerController>();
        var respawn = LevelRespawn.Instance;
        if (respawn != null)
        {
            respawn.Died += AoMorrer;
            respawn.PontoDeControleDefinido += AoTocarPonto;
        }

        Posicionar(PoseNaRota(0f, out _));
        _ultimaPosicao = transform.position;
    }

    void AoTocarPonto()
    {
        _tempoNoPonto = _tempo;
        int dica = 0;
        _distanciaDoPonto = _bruxa != null ? DistanciaNaRota(_bruxa.transform.position, ref dica) : 0f;
    }

    void AoMorrer()
    {
        if (_parado) return;

        // Largada conjunta no ponto de controle depois de perder, ou quando o
        // instante guardado já não deixaria a bruxa nem começar.
        float juntos = TempoNaDistancia(_distanciaDoPonto);
        bool semChance = _tempoNoPonto >= Duracao - 1f;
        _tempo = semChance ? Mathf.Min(juntos, _tempoNoPonto) : _tempoNoPonto;

        _chegou = false;
        _noEco = false;
        _dicaDaBruxa = 0;
        _passos.Clear();
    }

    void Update()
    {
        if (_parado || posicoes.Length < 2) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Não recua no rewind: a linha do tempo do fantasma é dele.
        _tempo += dt;

        var manager = RewindManager.Instance;
        bool voltando = manager != null && manager.IsRewinding;

        if (_bruxa != null)
        {
            _passos.Add((Time.time, _bruxa.transform.position, _bruxa.GravidadeInvertida));
            while (_passos.Count > 0 && _passos[0].t < Time.time - atrasoDoEco - 1f) _passos.RemoveAt(0);
        }

        bool naArea = areaDoEco.size.sqrMagnitude > 0f && _bruxa != null && areaDoEco.Contains(_bruxa.transform.position);
        bool eco = naArea && (_noEco || PertoDaBruxa());
        if (eco != _noEco)
        {
            _noEco = eco;
            if (dissolucao != null) dissolucao.Emit(40);
        }

        bool invertido;
        Vector3 alvo;
        if (_noEco && TryPassoAtrasado(out var passado, out invertido))
        {
            alvo = passado;
            if (!voltando && _bruxa != null && Vector3.Distance(alvo, _bruxa.transform.position) < raioDeCaptura)
            {
                // O eco alcançou: derruba como uma queda.
                _passos.Clear();
                var respawn = LevelRespawn.Instance;
                if (respawn != null) respawn.RestartLevel();
                return;
            }
        }
        else
        {
            alvo = PoseNaRota(_tempo, out var estado);
            invertido = (estado & Invertido) != 0;
        }

        Posicionar(alvo);
        Animar(alvo, invertido, dt);

        if (!_chegou && _tempo >= Duracao)
        {
            _chegou = true;
            PerdeuACorrida();
        }
    }

    /// <summary>
    /// O fantasma está perto da bruxa na corrida? Medido em tempo: quanto
    /// falta (ou quanto já passou) para ele chegar onde ela está na rota.
    /// Uma vez começado, o eco vai até o fim da área, mesmo que a diferença
    /// mude - sumir e voltar no meio seria pior.
    /// </summary>
    bool PertoDaBruxa()
    {
        float daBruxa = DistanciaNaRota(_bruxa.transform.position, ref _dicaDaBruxa);
        return Mathf.Abs(TempoNaDistancia(daBruxa) - _tempo) <= distanciaDoEco;
    }

    bool TryPassoAtrasado(out Vector3 posicao, out bool invertida)
    {
        float quando = Time.time - atrasoDoEco;
        for (int i = _passos.Count - 1; i >= 0; i--)
        {
            if (_passos[i].t > quando) continue;
            posicao = _passos[i].p;
            invertida = _passos[i].invertida;
            return true;
        }

        posicao = default;
        invertida = false;
        return false;
    }

    void PerdeuACorrida() => StartCoroutine(RecomecarAFase());

    /// <summary>
    /// O fantasma chegou primeiro: aviso curto e a fase recomeça do início,
    /// sem tela de morte no meio - não foi uma queda. A bruxa para, o
    /// fantasma fica na chegada, e o rewind sai de cena para ninguém voltar
    /// no tempo durante a espera.
    /// </summary>
    IEnumerator RecomecarAFase()
    {
        _parado = true;
        PainelDeAvisos.Mostrar("O fantasma chegou primeiro.", null, esperaDaDerrota + 0.5f);

        if (_bruxa != null)
        {
            _bruxa.ResetToIdle();
            _bruxa.enabled = false;
        }

        var rewind = RewindManager.Instance;
        if (rewind != null) rewind.enabled = false;

        // Tempo real: com uma pausa aberta no meio, a espera ainda termina.
        yield return new WaitForSecondsRealtime(esperaDaDerrota);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Posicionar(Vector3 p) => transform.position = p;

    void Animar(Vector3 agora, bool invertido, float dt)
    {
        Vector3 movimento = agora - _ultimaPosicao;
        _ultimaPosicao = agora;
        Vector3 plano = new Vector3(movimento.x, 0f, movimento.z);
        float velocidade = plano.magnitude / dt;

        if (modelo != null)
        {
            var cima = invertido ? Vector3.down : Vector3.up;
            var frente = plano.sqrMagnitude > 0.0001f ? plano.normalized : Vector3.ProjectOnPlane(modelo.forward, Vector3.up).normalized;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            var alvo = Quaternion.LookRotation(frente, cima);
            // A rota marca o ponto da superfície em que ele pisa - no teto, a
            // face de baixo -, então virar em volta dos pés já o pendura certo.
            modelo.rotation = Quaternion.RotateTowards(modelo.rotation, alvo, 720f * dt);
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedId, velocidade);
            bool noChao = Mathf.Abs(movimento.y) < 0.02f * Mathf.Max(1f, velocidade);
            animator.SetBool(GroundedId, noChao);
        }

        Pintar(_opacidade);
    }

    void Pintar(float opacidade)
    {
        var c = cor;
        c.a *= opacidade;
        _bloco.SetColor(Base, c);
        foreach (var r in corpo)
            if (r != null) r.SetPropertyBlock(_bloco);
    }

    // ------------------------------------------------------------------
    // Rota
    // ------------------------------------------------------------------

    /// <summary>Posição na rota num instante, e o estado da amostra.</summary>
    public Vector3 PoseNaRota(float tempo, out byte estado)
    {
        if (posicoes.Length == 0) { estado = 0; return transform.position; }

        float u = Mathf.Clamp(tempo / passo, 0f, posicoes.Length - 1);
        int i = Mathf.Min(Mathf.FloorToInt(u), posicoes.Length - 2);
        if (posicoes.Length == 1) i = 0;
        float f = u - i;
        estado = i < estados.Length ? estados[Mathf.Min(i + (f > 0.5f ? 1 : 0), estados.Length - 1)] : (byte)0;
        return posicoes.Length == 1 ? posicoes[0] : Vector3.Lerp(posicoes[i], posicoes[i + 1], f);
    }

    public float DistanciaNoTempo(float tempo)
    {
        if (_distancias == null || _distancias.Length < 2) return 0f;
        float u = Mathf.Clamp(tempo / passo, 0f, _distancias.Length - 1);
        int i = Mathf.Min(Mathf.FloorToInt(u), _distancias.Length - 2);
        return Mathf.Lerp(_distancias[i], _distancias[i + 1], u - i);
    }

    /// <summary>Em que instante o fantasma passa por esta distância da rota.</summary>
    public float TempoNaDistancia(float distancia)
    {
        if (_distancias == null || _distancias.Length < 2) return 0f;
        int lo = 0, hi = _distancias.Length - 1;
        if (distancia <= 0f) return 0f;
        if (distancia >= _distancias[hi]) return hi * passo;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_distancias[mid] < distancia) lo = mid; else hi = mid;
        }

        float trecho = _distancias[hi] - _distancias[lo];
        float f = trecho > 0.0001f ? (distancia - _distancias[lo]) / trecho : 0f;
        return (lo + f) * passo;
    }

    /// <summary>
    /// Distância na rota do ponto mais perto de <paramref name="ponto"/>,
    /// procurando perto da última resposta (<paramref name="dica"/>): a busca
    /// local evita pular para um trecho da rota que passa por cima ou por
    /// baixo de onde a bruxa está.
    /// </summary>
    public float DistanciaNaRota(Vector3 ponto, ref int dica)
    {
        if (posicoes.Length < 2) return 0f;
        int de = Mathf.Max(0, dica - 40), ate = Mathf.Min(posicoes.Length - 1, dica + 120);
        if (dica <= 0) { de = 0; ate = posicoes.Length - 1; }

        float melhor = float.MaxValue;
        int melhorI = dica;
        for (int i = de; i <= ate; i++)
        {
            float d = (posicoes[i] - ponto).sqrMagnitude;
            if (d < melhor) { melhor = d; melhorI = i; }
        }

        dica = Mathf.Max(1, melhorI);
        return _distancias[melhorI];
    }

    // ------------------------------------------------------------------
    // Fim da corrida
    // ------------------------------------------------------------------

    /// <summary>
    /// A bruxa venceu: o fantasma para, vira para ela e se desfaz em
    /// partículas. Não é um inimigo derrotado - é a própria bruxa, de outro
    /// tempo, ficando para trás.
    /// </summary>
    public void Parar(Vector3 olharPara)
    {
        if (_parado) return;
        _parado = true;
        StartCoroutine(Desfazer(olharPara));
    }

    IEnumerator Desfazer(Vector3 olharPara)
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedId, 0f);
            animator.SetBool(GroundedId, true);
        }

        if (rastro != null) rastro.Stop();

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            if (modelo != null)
            {
                var direcao = Vector3.ProjectOnPlane(olharPara - transform.position, Vector3.up);
                if (direcao.sqrMagnitude > 0.01f)
                    modelo.rotation = Quaternion.RotateTowards(modelo.rotation,
                        Quaternion.LookRotation(direcao.normalized, Vector3.up), 240f * Time.deltaTime);
            }

            yield return null;
        }

        // Um segundo olhando; depois se desfaz de baixo para cima.
        yield return new WaitForSeconds(0.6f);
        if (dissolucao != null) dissolucao.Play();

        float d = 0f;
        while (d < 1f)
        {
            d += Time.deltaTime / 2f;
            _opacidade = 1f - d;
            Pintar(_opacidade);
            if (modelo != null) modelo.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, d);
            yield return null;
        }

        if (modelo != null) modelo.gameObject.SetActive(false);
    }
}
