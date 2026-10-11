using UnityEngine;

/// <summary>
/// Squash and stretch da bruxa: ela achata ao aterrissar e estica ao pular.
///
/// É uma mola e não uma curva de animação de propósito - assim a intensidade
/// responde à velocidade real da queda (um degrau quase não deforma, uma queda
/// longa achata forte) e nunca briga com o clipe que o Animator está tocando,
/// porque mexe só na escala do modelo, não nos ossos.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerSquash : MonoBehaviour
{
    [Tooltip("O modelo que deforma. Normalmente o mesmo objeto ligado no PlayerController.")]
    [SerializeField] Transform visual;

    [Header("Aterrissagem")]
    [Tooltip("Achatamento máximo, como fração da altura. 0.28 = 28% mais baixa.")]
    [SerializeField, Range(0f, 0.6f)] float maxLandSquash = 0.28f;

    [Tooltip("Velocidade de queda que já produz o achatamento máximo, em m/s.")]
    [SerializeField, Min(1f)] float squashAtSpeed = 18f;

    [Header("Pulo")]
    [SerializeField, Range(0f, 0.6f)] float jumpStretch = 0.16f;
    [SerializeField, Range(0f, 0.6f)] float doubleJumpStretch = 0.24f;

    [Header("Mola")]
    [Tooltip("Quanto maior, mais rápido volta ao normal.")]
    [SerializeField, Min(1f)] float stiffness = 220f;

    [Tooltip("Quanto menor, mais a deformação balança antes de assentar.")]
    [SerializeField, Min(0f)] float damping = 20f;

    PlayerController _player;
    Vector3 _baseScale;
    float _offset;    // acima de 0 esticado, abaixo achatado
    float _velocity;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (visual == null) visual = transform;

        _baseScale = visual.localScale;
    }

    void OnEnable()
    {
        _player.Landed += OnLanded;
        _player.Jumped += OnJumped;
    }

    void OnDisable()
    {
        _player.Landed -= OnLanded;
        _player.Jumped -= OnJumped;

        visual.localScale = _baseScale;
    }

    void LateUpdate()
    {
        // Um frame muito longo (troca de cena, carregamento) faria a integração
        // explodir e a bruxa virar do avesso.
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        float acceleration = -stiffness * _offset - damping * _velocity;
        _velocity += acceleration * dt;
        _offset = Mathf.Clamp(_offset + _velocity * dt, -0.6f, 0.6f);

        // O volume se mantém: o que a altura perde, a largura ganha. É o que
        // separa um squash de um redimensionamento.
        float height = 1f + _offset;
        float width = 1f / Mathf.Sqrt(Mathf.Max(height, 0.01f));

        visual.localScale = Vector3.Scale(_baseScale, new Vector3(width, height, width));
    }

    void OnLanded(float fallSpeed)
    {
        float strength = Mathf.Clamp01(fallSpeed / squashAtSpeed);
        Impulse(-maxLandSquash * strength);
    }

    void OnJumped(bool isAirJump) => Impulse(isAirJump ? doubleJumpStretch : jumpStretch);

    /// <summary>
    /// Deforma na hora e deixa a mola trazer de volta. O impacto é instantâneo
    /// porque é assim que a leitura funciona: o olho precisa ver a batida no
    /// frame em que ela acontece.
    /// </summary>
    void Impulse(float amount)
    {
        _offset = amount;
        _velocity = 0f;
    }
}
