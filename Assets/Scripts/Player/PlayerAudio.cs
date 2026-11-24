using UnityEngine;

/// <summary>
/// Sons da bruxa: pulo, impulso, salto de parede, aterrissagem, quique na
/// bolha, morte e o rewind.
///
/// Escuta os eventos do <see cref="PlayerController"/> em vez de olhar o
/// estado a cada frame: o evento já diz o instante exato e, de graça, se foi o
/// pulo do ar - e é isso que permite dar um tom diferente ao segundo pulo sem
/// precisar de um segundo arquivo de som.
///
/// O AudioSource fica em 2D. A bruxa é o centro da tela o tempo todo, então
/// espacializar o som dela só serviria para baixá-lo quando a câmera se afasta,
/// que é o contrário do que se quer do retorno da própria ação.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerAudio : MonoBehaviour
{
    [SerializeField] AudioClip jump;

    [Tooltip("Salto de parede: um balanço mais largo que o pulo.")]
    [SerializeField] AudioClip wallJump;

    [Tooltip("Aterrissagem. Só toca em quedas de verdade, não em cada degrau.")]
    [SerializeField] AudioClip land;

    [Tooltip("Quique numa bolha.")]
    [SerializeField] AudioClip bounce;

    [Tooltip("Morte: a fita parando.")]
    [SerializeField] AudioClip death;

    [Tooltip("Começo do rewind: a fita voltando.")]
    [SerializeField] AudioClip rewind;

    [SerializeField, Range(0f, 1f)] float volume = 0.45f;

    [Tooltip("Tom do pulo duplo. Acima de 1 soa mais agudo, o que o olho lê " +
             "como 'de novo, mais leve' sem precisar de outro som.")]
    [SerializeField, Range(0.5f, 2f)] float airJumpPitch = 1.2f;

    [Tooltip("Velocidade de queda, em m/s, a partir da qual a aterrissagem soa. " +
             "Abaixo disso é um passo, e um som a cada passo cansaria.")]
    [SerializeField, Min(0f)] float quedaMinima = 9f;

    PlayerController _player;
    AudioSource _source;
    bool _voltando;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _source = GetComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
    }

    void OnEnable()
    {
        if (_player == null) return;
        _player.Jumped += OnJumped;
        _player.Dashed += OnDashed;
        _player.WallJumped += OnWallJumped;
        _player.Landed += OnLanded;
        _player.Quicou += OnBounced;
    }

    void OnDisable()
    {
        if (_player == null) return;
        _player.Jumped -= OnJumped;
        _player.Dashed -= OnDashed;
        _player.WallJumped -= OnWallJumped;
        _player.Landed -= OnLanded;
        _player.Quicou -= OnBounced;

        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died -= OnDied;
    }

    void Start()
    {
        // No Start, e não no OnEnable: o LevelRespawn pode nascer depois da bruxa.
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += OnDied;
    }

    void Update()
    {
        // O rewind não tem evento: o som sai na virada para "voltando".
        var manager = RewindManager.Instance;
        bool voltando = manager != null && manager.IsRewinding;
        if (voltando && !_voltando) Tocar(rewind, 0.8f);
        _voltando = voltando;
    }

    void Tocar(AudioClip clip, float escala = 1f, float pitch = 1f)
    {
        if (clip == null || _source == null) return;
        _source.pitch = pitch;
        _source.PlayOneShot(clip, volume * escala);
    }

    /// <summary>O impulso usa um som gerado por código (ver <see cref="SomProcedural"/>).</summary>
    void OnDashed() => Tocar(SomProcedural.Impulso);

    void OnJumped(bool isAirJump) => Tocar(jump, 1f, isAirJump ? airJumpPitch : 1f);

    void OnWallJumped() => Tocar(wallJump, 0.9f);

    void OnBounced() => Tocar(bounce, 0.8f);

    void OnDied() => Tocar(death, 1f);

    void OnLanded(float queda)
    {
        if (queda < quedaMinima) return;
        // Mais forte quanto maior a queda, até o dobro do mínimo.
        Tocar(land, Mathf.Lerp(0.4f, 0.9f, Mathf.InverseLerp(quedaMinima, quedaMinima * 2.5f, queda)));
    }
}
