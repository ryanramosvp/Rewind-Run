using UnityEngine;

/// <summary>Estado da bolha na linha do tempo: quando foi estourada (NaN: inteira).</summary>
public struct BubbleSample
{
    public float PoppedAt;
}

/// <summary>
/// Bolha flutuante: quem cai em cima ganha impulso e um pulo novo, e ela some.
///
/// É o que permite um trecho sem chão nenhum. O pulo duplo sozinho dá 6,1 m de
/// altura e acaba ali; com bolhas, cada toque devolve o recurso, e a altura
/// passa a depender de quantas se consegue encadear em vez de um teto fixo.
///
/// Some ao ser usada e renasce alguns segundos depois. Some porque uma bolha
/// permanente seria um degrau: dava para quicar na mesma até chegar onde
/// quisesse. Renasce porque quem errou a sequência e caiu num chão lá embaixo
/// precisa poder tentar de novo sem voltar no tempo ou morrer - e a espera
/// (<see cref="respawnSeconds"/>) é longa demais para quicar duas vezes na
/// mesma no meio de um encadeamento.
///
/// Tudo isso segue o rewind. O estado guardado é o instante em que ela
/// estourou, medido no relógio da própria bolha - o mesmo que anda para trás
/// no rewind -, e "estourada" ou "já de volta" sai da conta entre os dois.
/// Voltar para antes do estouro traz a bolha de volta; voltar para o meio da
/// espera a esconde de novo. A habilidade central do jogo continua sendo a
/// rede de segurança da mecânica.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class AirBubble : RewindableBehaviour<BubbleSample>
{
    [Tooltip("Raio da esfera visível. O gatilho acompanha, com uma folga.")]
    [SerializeField, Min(0.1f)] float radius = 1.4f;

    [Tooltip("Altura do quique. Para comparar: o pulo normal sobe 3,2 m.")]
    [SerializeField, Min(0f)] float bounceHeight = 4.5f;

    [Tooltip("Segundos até a bolha renascer depois de usada.")]
    [SerializeField, Min(0.1f)] float respawnSeconds = 3f;

    [Tooltip("Quanto ela leva para crescer de volta ao renascer.")]
    [SerializeField, Min(0.01f)] float regrowSeconds = 0.3f;

    [Tooltip("Parte visível, desligada quando a bolha estoura. Nunca o objeto " +
             "raiz - ver o comentário em SetVisible.")]
    [SerializeField] GameObject visual;

    [Tooltip("Balanço vertical, só para a bolha não parecer pregada no ar.")]
    [SerializeField, Min(0f)] float bobHeight = 0.3f;

    [SerializeField, Min(0.1f)] float bobSeconds = 2.4f;

    [SerializeField, Range(0f, 1f)] float bobPhase;

    SphereCollider _area;
    Vector3 _anchor;
    float _time;
    float _poppedAt = float.NaN;
    bool _visible = true;
    Transform _player;

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

        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) _player = player.transform;
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
        bool rewinding = manager != null && manager.IsRewinding;
        if (rewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        float angle = (bobPhase + _time / bobSeconds) * Mathf.PI * 2f;
        transform.position = _anchor + Vector3.up * (Mathf.Sin(angle) * bobHeight);

        // Voltou para antes do estouro: ela nunca foi usada.
        if (!float.IsNaN(_poppedAt) && _time < _poppedAt) _poppedAt = float.NaN;

        // Não renasce em cima da bruxa: com ela dentro, o gatilho religado
        // a arremessaria sem ela ter pedido. Espera ela sair.
        if (!rewinding && IsWaitingToReturn() && PlayerInside()) _poppedAt += Time.deltaTime;

        Refresh();
    }

    bool IsPopped => !float.IsNaN(_poppedAt) && _time - _poppedAt < respawnSeconds;

    bool IsWaitingToReturn() =>
        !float.IsNaN(_poppedAt) && _time - _poppedAt >= respawnSeconds - 0.05f
                                && _time - _poppedAt < respawnSeconds;

    bool PlayerInside() =>
        _player != null && (_player.position + Vector3.up - transform.position).magnitude < radius + 1.4f;

    void OnTriggerEnter(Collider other)
    {
        if (IsPopped) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // Encostar de qualquer lado basta. A versão anterior exigia vir de cima
        // e estar caindo, que é o que um trampolim pediria - mas uma bolha no
        // meio do nada é um alvo pequeno visto de uma câmera a vinte metros, e
        // cobrar o ângulo da aproximação punia a mira, não a decisão. O que a
        // fase quer testar é se o jogador escolheu a bolha certa, não se bateu
        // nela pelo lado certo.
        player.Bounce(bounceHeight);
        _poppedAt = _time;
        Refresh();
    }

    void Restore()
    {
        _poppedAt = float.NaN;
        Refresh();
    }

    /// <summary>Aparece, some e cresce de volta conforme o relógio.</summary>
    void Refresh()
    {
        SetVisible(!IsPopped);
        if (!_visible || visual == null) return;

        // Renascendo: cresce do nada em vez de surgir de estalo, para quem
        // está olhando entender que ela voltou.
        float grow = 1f;
        if (!float.IsNaN(_poppedAt))
        {
            float t = Mathf.Clamp01((_time - _poppedAt - respawnSeconds) / regrowSeconds);
            grow = t * t * (3f - 2f * t);
        }

        visual.transform.localScale = Vector3.one * (radius * grow);
    }

    /// <summary>
    /// Esconde ou devolve a bolha.
    ///
    /// Desliga o visual e o collider, e nunca o objeto raiz: a
    /// <see cref="RewindableBehaviour{T}"/> se desregistra do manager no
    /// OnDisable, então um SetActive(false) apagaria a história da bolha e ela
    /// não voltaria no rewind - justo o que ela mais precisa fazer.
    /// </summary>
    void SetVisible(bool visible)
    {
        if (visible == _visible) return;
        _visible = visible;

        if (_area != null) _area.enabled = visible;
        if (visual != null) visual.SetActive(visible);
    }

    protected override BubbleSample Capture() => new BubbleSample { PoppedAt = _poppedAt };

    protected override void Apply(in BubbleSample older, in BubbleSample newer, float t)
    {
        // Instante de estouro não tem meio-termo: vale a amostra mais próxima.
        _poppedAt = t < 0.5f ? older.PoppedAt : newer.PoppedAt;
        Refresh();
    }
}
