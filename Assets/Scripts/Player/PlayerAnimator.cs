using UnityEngine;

/// <summary>
/// Liga o estado do <see cref="PlayerController"/> aos parâmetros do Animator.
/// Durante o rewind toca a animação de trás para frente, em vez de congelar a
/// bruxa deslizando - é o que faz a mecânica parecer voltar no tempo de verdade.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;

    [Tooltip("Suavização da velocidade enviada ao blend tree. Evita tremer entre andar e correr.")]
    [SerializeField, Min(0.1f)] float speedSharpness = 14f;

    /// <summary>Nome do estado de ar no Bruxa.controller, criado pelo WitchSetup.</summary>
    public const string AirStateName = "NoAr";

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");

    /// <summary>Nome do parâmetro de estado no Bruxa.controller.</summary>
    public const string StateParameter = "MoveState";

    static readonly int StateId = Animator.StringToHash(StateParameter);
    static readonly int AirStateId = Animator.StringToHash(AirStateName);

    PlayerController _player;
    float _displayedSpeed;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // O movimento vem do código; deixar a animação empurrar o transform
        // faria a bruxa escorregar sozinha pelo cenário.
        if (animator != null) animator.applyRootMotion = false;
    }

    void OnEnable() => _player.Jumped += OnJumped;

    void OnDisable() => _player.Jumped -= OnJumped;

    /// <summary>
    /// Reinicia a animação de ar a cada pulo. Sem isso o pulo duplo não teria
    /// leitura nenhuma: a bruxa já estaria no meio do clipe de queda e o
    /// segundo salto passaria despercebido.
    /// </summary>
    void OnJumped(bool isAirJump)
    {
        if (animator != null) animator.Play(AirStateId, 0, 0f);
    }

    void Update()
    {
        if (animator == null) return;

        var manager = RewindManager.Instance;
        bool rewinding = manager != null && manager.IsRewinding;

        animator.speed = rewinding ? -1f : 1f;
        if (rewinding) return;

        float speed = _player.HorizontalVelocity.magnitude;
        float blend = 1f - Mathf.Exp(-speedSharpness * Time.deltaTime);
        _displayedSpeed = Mathf.Lerp(_displayedSpeed, speed, blend);

        animator.SetFloat(SpeedId, _displayedSpeed);
        animator.SetBool(GroundedId, _player.IsGrounded);
        animator.SetInteger(StateId, (int)_player.State);
    }
}
