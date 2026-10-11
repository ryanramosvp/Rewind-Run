using UnityEngine;

/// <summary>Como a câmera se comporta.</summary>
public enum CameraMode
{
    /// <summary>Orbita em volta do jogador e se realinha sozinha, estilo Mario 64.</summary>
    Orbita,

    /// <summary>Ângulo e distância fixos; só acompanha a posição do jogador.</summary>
    Fixa,
}

/// <summary>
/// Câmera de acompanhamento do jogador, em dois modos.
///
/// O padrão é Fixa, estilo Crash Bandicoot: ângulo e distância constantes, sem
/// mouse. Num platformer de precisão, administrar a câmera enquanto se mira um
/// salto são duas tarefas ao mesmo tempo, e a câmera perde - o jogador erra o
/// pulo porque estava girando a visão. Com a câmera fixa, o mesmo trecho sempre
/// aparece igual, o que também ajuda a repetir tentativas depois de um rewind.
///
/// O modo Órbita continua disponível para quem quiser o controle manual.
///
/// O nome da classe é histórico: ela começou só orbital.
/// </summary>
[DefaultExecutionOrder(100)]
public class OrbitCamera : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] PlayerInputReader input;
    [SerializeField] Transform target;

    [Tooltip("Deslocamento do ponto observado. Sobe a mira do pé para o peito do personagem.")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Modo")]
    [Tooltip("Fixa mantém sempre o mesmo ângulo e distância, sem mouse; Órbita dá o controle manual.")]
    [SerializeField] CameraMode mode = CameraMode.Fixa;

    [Tooltip("Modo Fixa: direção de onde a câmera olha, em graus.")]
    [SerializeField] float fixedYaw;

    [Tooltip("Modo Fixa: quanto olha de cima.")]
    [SerializeField, Range(0f, 80f)] float fixedPitch = 28f;

    [Tooltip("Modo Fixa: distância constante. Bem maior que a da órbita, para mostrar o cenário.")]
    [SerializeField, Min(1f)] float fixedDistance = 15f;

    [Tooltip("Modo Fixa: deixar o cenário aproximar a câmera. Desligado mantém o enquadramento " +
             "sempre igual, que é o motivo de existir uma câmera fixa.")]
    [SerializeField] bool fixedModeCollision;

    [Tooltip("Modo Fixa: o quanto o foco se adianta na direção do movimento, em metros. " +
             "Abre campo de visão à frente sem tirar o personagem do quadro.")]
    [SerializeField, Range(0f, 6f)] float lookAhead = 2f;

    [Tooltip("Modo Fixa: suavização vertical. Bem menor que a horizontal de propósito - " +
             "se a câmera seguisse cada pulo na mesma velocidade, a tela balançaria sem parar.")]
    [SerializeField, Min(0.1f)] float verticalSharpness = 3.5f;

    [Header("Enquadramento")]
    [SerializeField, Min(1f)] float distance = 11f;

    [Tooltip("Abaixo de zero a câmera desce para baixo do jogador, entra no chão e a colisão a puxa " +
             "para cima dele. Manter acima de zero é o que evita a maioria dos encostões.")]
    [SerializeField, Range(-20f, 40f)] float minPitch = 2f;

    [SerializeField] float maxPitch = 65f;

    [Tooltip("Quanto maior, mais colada a câmera fica no jogador.")]
    [SerializeField, Min(0.1f)] float followSharpness = 12f;

    [Header("Sensibilidade")]
    [Tooltip("Graus por pixel de movimento do mouse.")]
    [SerializeField, Min(0f)] float mouseSensitivity = 0.12f;

    [Tooltip("Graus por segundo com o analógico no talo.")]
    [SerializeField, Min(0f)] float stickSensitivity = 180f;

    [SerializeField] bool invertY;

    [Header("Câmera automática (estilo Mario 64)")]
    [Tooltip("A câmera volta sozinha para trás da bruxa quando ela corre.")]
    [SerializeField] bool autoFollow = true;

    [Tooltip("Espera depois do último toque manual antes de a câmera reassumir.")]
    [SerializeField, Min(0f)] float autoFollowDelay = 1f;

    [Tooltip("Velocidade do realinhamento, em graus por segundo.")]
    [SerializeField, Min(0f)] float autoFollowSpeed = 110f;

    [Tooltip("Abaixo desta velocidade a bruxa está parada demais para definir uma direção.")]
    [SerializeField, Min(0f)] float autoFollowMinSpeed = 2.5f;

    [Tooltip("Inclinação para a qual a câmera volta sozinha.")]
    [SerializeField] float restingPitch = 18f;

    [SerializeField, Min(0f)] float pitchRecenterSpeed = 30f;

    [Header("Colisão")]
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField, Min(0.01f)] float collisionRadius = 0.25f;
    [SerializeField, Min(0f)] float collisionPadding = 0.2f;

    [Tooltip("Distância mínima. Abaixo disto a câmera entra no personagem e some o cenário.")]
    [SerializeField, Min(0.5f)] float minCollisionDistance = 3f;

    [Tooltip("Velocidade com que a câmera volta à distância cheia depois de desviar de algo.")]
    [SerializeField, Min(0.1f)] float collisionReturnSpeed = 6f;

    [Header("Cursor")]
    [SerializeField] bool lockCursor = true;

    /// <summary>
    /// Enquadramento pedido pela região atual. Começa nos valores do Inspector
    /// e muda quando o jogador entra numa <see cref="CameraZone"/>.
    /// </summary>
    float _targetYaw, _targetPitch, _targetDistance;
    float _framingBlend = 999f;   // grande = já assentado

    readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
    PlayerController _player;
    float _currentDistance = -1f;
    Vector3 _lookAheadOffset;
    float _yaw;
    float _pitch = 20f;
    float _timeSinceManualLook;
    Vector3 _focus;

    void Awake()
    {
        if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }

        if (target != null) _player = target.GetComponent<PlayerController>();

        if (target != null) _focus = target.position + targetOffset;

        var angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = NormalizePitch(angles.x);

        ResetFraming();
    }

    /// <summary>
    /// Pede um enquadramento novo, com transição.
    ///
    /// É o que permite uma fase aberta com câmera fixa: cada região escolhe de
    /// onde é vista, e o jogador nunca precisa administrar a câmera. Sem isto,
    /// um trecho que corre no eixo errado apareceria de costas.
    /// </summary>
    public void SetFixedFraming(float yaw, float pitch, float distance, float blendSeconds)
    {
        _targetYaw = yaw;
        _targetPitch = pitch;
        _targetDistance = distance;
        _framingBlend = Mathf.Max(0.05f, blendSeconds);
    }

    /// <summary>
    /// Está mudando de enquadramento agora.
    ///
    /// O jogador lê isto para não girar junto: como o movimento é relativo à
    /// câmera, um yaw que muda enquanto a direção está segurada faria a bruxa
    /// virar sozinha no meio da corrida.
    /// </summary>
    public bool IsReframing =>
        mode == CameraMode.Fixa && Mathf.Abs(Mathf.DeltaAngle(_yaw, _targetYaw)) > 0.5f;

    /// <summary>Volta ao enquadramento padrão do Inspector.</summary>
    public void ResetFraming()
    {
        _targetYaw = fixedYaw;
        _targetPitch = fixedPitch;
        _targetDistance = fixedDistance;
    }

    /// <summary>
    /// Devolve o cursor ao estado que esta câmera pede.
    ///
    /// É pública porque a câmera é a única que sabe a resposta: no modo fixo
    /// não há mira, e prender o cursor só atrapalharia. Quem fecha uma tela
    /// (pausa, morte, vitória) chama isto em vez de travar o cursor por conta
    /// própria - era assim que a tela de fim acabava prendendo o cursor numa
    /// câmera fixa, que nunca o quis preso.
    /// </summary>
    public void ApplyCursorMode()
    {
        bool locked = lockCursor && mode == CameraMode.Orbita;

        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>Solta o cursor enquanto uma tela estiver aberta.</summary>
    public static void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Devolve o cursor ao que a câmera da cena pede.</summary>
    public static void RestoreCursor()
    {
        var camera = FindAnyObjectByType<OrbitCamera>();
        if (camera != null) camera.ApplyCursorMode();
        else FreeCursor();
    }

    void OnEnable() => ApplyCursorMode();

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        if (mode == CameraMode.Orbita)
        {
            UpdateAngles(dt);
        }
        else
        {
            // Aproximação gradual: trocar de ângulo num corte deixaria o
            // jogador perdido no meio de um salto.
            float rate = 1f / _framingBlend;
            _yaw = Mathf.MoveTowardsAngle(_yaw, _targetYaw, 180f * rate * dt);
            _pitch = Mathf.MoveTowards(_pitch, _targetPitch, 90f * rate * dt);
        }

        UpdateFocus(dt);

        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 back = rotation * Vector3.back;

        transform.SetPositionAndRotation(_focus + back * ResolveSmoothedDistance(back, dt), rotation);
    }

    /// <summary>
    /// Distância do frame, já suavizada.
    ///
    /// Aproximar é imediato e afastar é gradual, de propósito. Se a volta também
    /// fosse instantânea, raspar de leve num canto faria a câmera saltar para a
    /// frente e para trás; e se a aproximação fosse gradual, a câmera atravessaria
    /// a parede antes de reagir.
    /// </summary>
    float ResolveSmoothedDistance(Vector3 back, float dt)
    {
        float target = mode == CameraMode.Fixa ? _targetDistance : distance;

        // No modo fixo a colisão é opcional e vem desligada: uma câmera que
        // aproxima em corredores deixa de ser fixa, que é o motivo de escolhê-la.
        bool useCollision = mode == CameraMode.Orbita || fixedModeCollision;
        float desired = useCollision ? ResolveDistance(back, target) : target;

        if (_currentDistance < 0f) _currentDistance = desired;   // primeiro frame

        _currentDistance = desired < _currentDistance
            ? desired
            : Mathf.MoveTowards(_currentDistance, desired, collisionReturnSpeed * dt);

        return _currentDistance;
    }

    /// <summary>
    /// Move o ponto observado.
    ///
    /// Horizontal e vertical usam velocidades diferentes de propósito. Num
    /// platformer o personagem passa o tempo subindo e descendo, e uma câmera
    /// que acompanha cada pulo na mesma velocidade que o avanço deixa a tela
    /// balançando sem parar. Seguindo devagar no eixo Y, pulos pequenos mal
    /// mexem no quadro e só quedas ou subidas de verdade reposicionam a câmera.
    /// </summary>
    void UpdateFocus(float dt)
    {
        Vector3 desired = target.position + targetOffset;

        if (mode == CameraMode.Fixa && lookAhead > 0f && _player != null)
        {
            // Adianta o foco para onde a bruxa vai, para sobrar tela à frente.
            // A meta é suavizada, senão o quadro daria um tranco a cada inversão.
            Vector3 velocity = _player.HorizontalVelocity;
            Vector3 wanted = velocity.sqrMagnitude > 1f
                ? velocity.normalized * lookAhead
                : Vector3.zero;

            _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, wanted, 1f - Mathf.Exp(-3f * dt));
            desired += _lookAheadOffset;
        }

        float horizontalBlend = 1f - Mathf.Exp(-followSharpness * dt);
        float verticalBlend = 1f - Mathf.Exp(-(mode == CameraMode.Fixa ? verticalSharpness : followSharpness) * dt);

        _focus = new Vector3(
            Mathf.Lerp(_focus.x, desired.x, horizontalBlend),
            Mathf.Lerp(_focus.y, desired.y, verticalBlend),
            Mathf.Lerp(_focus.z, desired.z, horizontalBlend));
    }

    void UpdateAngles(float dt)
    {
        if (input == null) return;

        Vector2 look = input.Look;
        float yawDelta, pitchDelta;

        if (input.LookIsGamepad)
        {
            // Analógico entrega uma posição sustentada, então vira grau por segundo.
            yawDelta = look.x * stickSensitivity * dt;
            pitchDelta = look.y * stickSensitivity * dt;
        }
        else
        {
            // Mouse já entrega o deslocamento do frame; multiplicar por dt aqui
            // faria a mira mudar de sensibilidade conforme o FPS.
            yawDelta = look.x * mouseSensitivity;
            pitchDelta = look.y * mouseSensitivity;
        }

        _yaw += yawDelta;
        _pitch = Mathf.Clamp(_pitch + (invertY ? pitchDelta : -pitchDelta), minPitch, maxPitch);

        // Qualquer toque no olhar devolve o controle ao jogador e reinicia a espera.
        bool manualLook = look.sqrMagnitude > 0.0001f;
        _timeSinceManualLook = manualLook ? 0f : _timeSinceManualLook + dt;

        if (autoFollow && !manualLook && _timeSinceManualLook >= autoFollowDelay)
            ApplyAutoFollow(dt);
    }

    /// <summary>
    /// O jeito do Lakitu no Mario 64: enquanto a bruxa corre, a câmera desliza
    /// para trás dela e volta à inclinação de descanso. Só age depois de um
    /// tempo sem input manual, para não brigar com quem está mirando a câmera.
    /// </summary>
    void ApplyAutoFollow(float dt)
    {
        if (_player == null) return;

        Vector3 velocity = _player.HorizontalVelocity;
        if (velocity.sqrMagnitude < autoFollowMinSpeed * autoFollowMinSpeed) return;

        float desiredYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
        _yaw = Mathf.MoveTowardsAngle(_yaw, desiredYaw, autoFollowSpeed * dt);
        _pitch = Mathf.MoveTowards(_pitch, restingPitch, pitchRecenterSpeed * dt);
    }

    /// <summary>Encurta a distância se houver cenário entre a câmera e o jogador.</summary>
    float ResolveDistance(Vector3 back, float maxDistance)
    {
        int count = Physics.SphereCastNonAlloc(_focus, collisionRadius, back, _hitBuffer, maxDistance,
                                               collisionMask, QueryTriggerInteraction.Ignore);
        float closest = maxDistance;

        for (int i = 0; i < count; i++)
        {
            ref var hit = ref _hitBuffer[i];

            // O cast começa dentro do próprio jogador, e colisores sobrepostos à
            // origem voltam com distância 0. Ignorar ambos evita a câmera colar
            // na nuca do personagem sem motivo.
            if (hit.distance <= 0.01f) continue;
            if (hit.transform.IsChildOf(target)) continue;

            closest = Mathf.Min(closest, hit.distance - collisionPadding);
        }

        return Mathf.Max(minCollisionDistance, closest);
    }

    static float NormalizePitch(float pitch) => pitch > 180f ? pitch - 360f : pitch;
}
