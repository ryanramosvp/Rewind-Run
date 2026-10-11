using UnityEngine;

/// <summary>
/// Sons da bruxa. Hoje, o pulo.
///
/// Escuta o evento <see cref="PlayerController.Jumped"/> em vez de olhar o
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

    [SerializeField, Range(0f, 1f)] float volume = 0.45f;

    [Tooltip("Tom do pulo duplo. Acima de 1 soa mais agudo, o que o olho lê " +
             "como 'de novo, mais leve' sem precisar de outro som.")]
    [SerializeField, Range(0.5f, 2f)] float airJumpPitch = 1.2f;

    PlayerController _player;
    AudioSource _source;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _source = GetComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
    }

    void OnEnable()
    {
        if (_player != null) _player.Jumped += OnJumped;
    }

    void OnDisable()
    {
        if (_player != null) _player.Jumped -= OnJumped;
    }

    void OnJumped(bool isAirJump)
    {
        if (jump == null || _source == null) return;

        _source.pitch = isAirJump ? airJumpPitch : 1f;
        _source.PlayOneShot(jump, volume);
    }
}
