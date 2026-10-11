using UnityEngine;

/// <summary>Estado da bolha na linha do tempo: existe ou já foi estourada.</summary>
public struct BubbleSample
{
    public bool Popped;
}

/// <summary>
/// Bolha flutuante: quem cai em cima ganha impulso e um pulo novo, e ela some.
///
/// É o que permite um trecho sem chão nenhum. O pulo duplo sozinho dá 6,1 m de
/// altura e acaba ali; com bolhas, cada toque devolve o recurso, e a altura
/// passa a depender de quantas se consegue encadear em vez de um teto fixo.
///
/// Some em vez de ficar, porque uma bolha permanente seria um degrau: dava para
/// quicar na mesma até chegar onde quisesse, e o trecho vertical viraria uma
/// escada sem risco. Sumindo, cada uma é uma decisão de uma vez só.
///
/// E some de um jeito que o rewind desfaz. Essa é a parte que amarra a mecânica
/// ao resto do jogo: errar a sequência não obriga a repetir a fase inteira,
/// voltar o tempo traz as bolhas de volta junto. A habilidade central do jogo
/// vira a rede de segurança da mecânica nova.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class AirBubble : RewindableBehaviour<BubbleSample>
{
    [Tooltip("Raio da esfera visível. O gatilho acompanha, com uma folga.")]
    [SerializeField, Min(0.1f)] float radius = 1.4f;

    [Tooltip("Altura do quique. Para comparar: o pulo normal sobe 3,2 m.")]
    [SerializeField, Min(0f)] float bounceHeight = 4.5f;

    [Tooltip("Parte visível, desligada quando a bolha estoura. Nunca o objeto " +
             "raiz - ver o comentário em SetPopped.")]
    [SerializeField] GameObject visual;

    [Tooltip("Balanço vertical, só para a bolha não parecer pregada no ar.")]
    [SerializeField, Min(0f)] float bobHeight = 0.3f;

    [SerializeField, Min(0.1f)] float bobSeconds = 2.4f;

    [SerializeField, Range(0f, 1f)] float bobPhase;

    SphereCollider _area;
    Vector3 _anchor;
    bool _popped;
    float _time;

    void Awake()
    {
        _area = GetComponent<SphereCollider>();
        _area.isTrigger = true;
        _area.radius = radius + 0.2f;

        // A malha do pack tem 1 m de raio; a escala leva ao raio pedido.
        if (visual != null) visual.transform.localScale = Vector3.one * radius;
        _anchor = transform.position;
    }

    void Start()
    {
        // Em Start e não em Awake: o LevelRespawn define a própria Instance no
        // Awake dele, e a ordem entre dois Awake não é garantida.
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += Restore;
    }

    void OnDestroy()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died -= Restore;
    }

    void Update()
    {
        // Mesmo relógio reversível da RotatingHazard: o balanço é função pura
        // do tempo, então andar o tempo para trás já o desenha para trás.
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        float angle = (bobPhase + _time / bobSeconds) * Mathf.PI * 2f;
        transform.position = _anchor + Vector3.up * (Mathf.Sin(angle) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_popped) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // Encostar de qualquer lado basta. A versão anterior exigia vir de cima
        // e estar caindo, que é o que um trampolim pediria - mas uma bolha no
        // meio do nada é um alvo pequeno visto de uma câmera a vinte metros, e
        // cobrar o ângulo da aproximação punia a mira, não a decisão. O que a
        // fase quer testar é se o jogador escolheu a bolha certa, não se bateu
        // nela pelo lado certo.
        player.Bounce(bounceHeight);
        SetPopped(true);
    }

    void Restore() => SetPopped(false);

    /// <summary>
    /// Esconde ou devolve a bolha.
    ///
    /// Desliga o visual e o collider, e nunca o objeto raiz: a
    /// <see cref="RewindableBehaviour{T}"/> se desregistra do manager no
    /// OnDisable, então um SetActive(false) apagaria a história da bolha e ela
    /// não voltaria no rewind - justo o que ela mais precisa fazer.
    /// </summary>
    void SetPopped(bool popped)
    {
        _popped = popped;

        if (_area != null) _area.enabled = !popped;
        if (visual != null) visual.SetActive(!popped);
    }

    protected override BubbleSample Capture() => new BubbleSample { Popped = _popped };

    protected override void Apply(in BubbleSample older, in BubbleSample newer, float t)
    {
        // Booleano não tem meio-termo: vale a amostra mais próxima no tempo.
        bool popped = t < 0.5f ? older.Popped : newer.Popped;
        if (popped != _popped) SetPopped(popped);
    }
}
