using UnityEngine;

/// <summary>Em que modo de movimento o jogador está.</summary>
public enum PlayerMoveState
{
    /// <summary>Corrida, pulo e queda normais.</summary>
    Normal,

    /// <summary>Deslizando no chão, ganhando velocidade em descidas.</summary>
    Slide,
}

/// <summary>
/// Movimentação de plataforma 3D relativa à câmera, com CharacterController.
/// Também é quem lê o botão de rewind e liga/desliga o <see cref="RewindManager"/>;
/// enquanto o rewind roda, quem dirige o transform é o <see cref="PlayerRewind"/>.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] PlayerInputReader input;

    [Tooltip("A direção do movimento é relativa a esta câmera. Vazio usa a Camera.main.")]
    [SerializeField] Transform cameraTransform;

    [Tooltip("Modelo que gira para a direção do movimento. Vazio gira o próprio objeto.")]
    [SerializeField] Transform visual;

    [Header("Movimento")]
    [Tooltip("Velocidade no chão. Única: não há botão de correr.")]
    [SerializeField, Min(0f)] float moveSpeed = 11f;
    [SerializeField, Min(0f)] float groundAcceleration = 70f;
    [Tooltip("Freada ao soltar o direcional no chão.")]
    [SerializeField, Min(0f)] float groundDeceleration = 80f;

    [Tooltip("Aceleração no ar. Menor que a do chão dá peso ao pulo sem tirar o controle. " +
             "Com 40 a bruxa chegava à velocidade máxima em 0,28 s no ar e parecia sem peso; " +
             "com 22 leva meio segundo, metade de um pulo.")]
    [SerializeField, Min(0f)] float airAcceleration = 22f;

    [Tooltip("Graus por segundo que a velocidade pode girar no ar. É o que " +
             "separa curvar de derrapar na diagonal. Com 360 dava para dar meia-volta " +
             "no ar em 0,33 s sem perder velocidade nenhuma.")]
    [SerializeField, Min(0f)] float airTurnSpeed = 180f;

    [Tooltip("Freada no ar quando o direcional aponta contra o embalo, em m/s².")]
    [SerializeField, Min(0f)] float airBrake = 24f;

    [Tooltip("A partir de quantos graus entre o embalo e o direcional o ar freia em vez de girar.")]
    [SerializeField, Range(90f, 180f)] float airReverseAngle = 120f;

    [SerializeField, Min(0f)] float turnSpeed = 900f;

    [Header("Pulo")]
    [Tooltip("Altura em metros de um pulo com o botão segurado até o fim.")]
    [SerializeField, Min(0f)] float jumpHeight = 3.2f;

    [Tooltip("Altura do pulo duplo. Um pouco menor que o primeiro, para o segundo ser um ajuste e não um atalho.")]
    [SerializeField, Min(0f)] float doubleJumpHeight = 2.8f;

    [Tooltip("Quantos pulos extras cabem no ar. 1 = pulo duplo.")]
    [SerializeField, Min(0)] int airJumps = 1;

    [SerializeField, Min(0f)] float gravity = 28f;

    [Tooltip("Gravidade extra na descida. Acima de 1 deixa a queda mais rápida que a subida.")]
    [SerializeField, Min(1f)] float fallGravityMultiplier = 1.7f;

    [Tooltip("Faixa de velocidade vertical, para cima ou para baixo, que conta como topo do pulo.")]
    [SerializeField, Min(0f)] float apexThreshold = 4f;

    [Tooltip("Gravidade no topo do pulo. Abaixo de 1 segura a bruxa no ar por um instante.")]
    [SerializeField, Range(0.1f, 1f)] float apexGravityMultiplier = 0.55f;

    [Tooltip("Janela para pular depois de já ter saído da plataforma.")]
    [SerializeField, Min(0f)] float coyoteTime = 0.12f;

    [Tooltip("Janela para o pulo apertado um pouco antes de encostar no chão valer.")]
    [SerializeField, Min(0f)] float jumpBufferTime = 0.12f;

    [Tooltip("Quanto da subida sobra ao soltar o botão. Menor = mais controle sobre a altura.")]
    [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.45f;

    [SerializeField] float terminalVelocity = -40f;

    [Header("Bunny hop")]
    [Tooltip("Janela depois de encostar no chao para o pulo contar como encadeado.")]
    [SerializeField, Min(0f)] float hopWindow = 0.16f;

    [Tooltip("Velocidade ganha por pulo encadeado.")]
    [SerializeField, Min(0f)] float hopGain = 1.2f;

    [Tooltip("Teto do bonus acumulado. Somado a corrida de 11 m/s, e a velocidade maxima do jogo.")]
    [SerializeField, Min(0f)] float maxHopBonus = 7f;

    [Tooltip("Perda do bonus por segundo enquanto a bruxa fica no chao sem pular.")]
    [SerializeField, Min(0f)] float hopDecay = 12f;

    [Header("Slide")]
    [Tooltip("Inclinação a partir da qual a bruxa escorrega sozinha. O slide não tem botão: " +
             "é a rampa que decide, como em qualquer ladeira de verdade.")]
    [SerializeField, Range(10f, 60f)] float autoSlideAngle = 25f;

    [Tooltip("Velocidade mínima rampa abaixo para o slide começar. É o que permite subir uma " +
             "rampa andando: só escorrega quem já está descendo.")]
    [SerializeField, Min(0f)] float minDownhillSpeed = 1.5f;

    [Tooltip("Tempo que a rampa precisa ser lida seguidamente antes de o slide começar. " +
             "Uma leitura isolada pode ser ruído de geometria; uma rampa de verdade dura.")]
    [SerializeField, Min(0f)] float slopeConfirmSeconds = 0.08f;

    [Tooltip("Empurrão de velocidade ao entrar no slide.")]
    [SerializeField, Min(0f)] float slideBoost = 3f;

    [Tooltip("Teto de velocidade do slide. É o que impede uma descida longa de virar um canhão. " +
             "Acima da corrida (11 m/s) para o slide compensar, sem virar outro jogo.")]
    [SerializeField, Min(1f)] float maxSlideSpeed = 15f;

    [Tooltip("Desaceleração do slide em chão plano, em m/s².")]
    [SerializeField, Min(0f)] float slideFriction = 6f;

    [Tooltip("Velocidade abaixo da qual a bruxa se levanta.")]
    [SerializeField, Min(0f)] float slideExitSpeed = 3.5f;

    [Tooltip("Quanto o slide pode ser direcionado, em graus por segundo.")]
    [SerializeField, Min(0f)] float slideSteerSpeed = 90f;

    [Header("Rampas")]
    [Tooltip("Quanto da gravidade puxa o slide rampa abaixo. 1 = física realista.")]
    [SerializeField, Min(0f)] float slopeAcceleration = 1f;

    [Tooltip("Inclinação em que a fricção do slide chega ao mínimo.")]
    [SerializeField, Range(5f, 89f)] float frictionFadeAngle = 40f;

    [Tooltip("Fração da fricção que sobra na rampa mais íngreme. Zero faria o slide acelerar sem fim.")]
    [SerializeField, Range(0.05f, 1f)] float minFrictionScale = 0.15f;

    [Tooltip("Camadas que contam como chão para a sonda de rampa.")]
    [SerializeField] LayerMask groundMask = ~0;

    [Tooltip("Distância máxima que o slide desce para continuar colado à rampa.")]
    [SerializeField, Min(0f)] float groundSnapDistance = 0.5f;

    [Header("Wall jump")]
    [Tooltip("Inclinação mínima para uma superfície contar como parede, em graus a partir do chão.")]
    [SerializeField, Range(45f, 89f)] float minWallAngle = 70f;

    [Tooltip("Velocidade máxima de descida agarrada na parede. Menor = escorrega mais devagar.")]
    [SerializeField, Min(0f)] float wallSlideSpeed = 4f;

    [Tooltip("Altura do salto de parede.")]
    [SerializeField, Min(0f)] float wallJumpHeight = 2.8f;

    [Tooltip("Velocidade de afastamento da parede ao saltar.")]
    [SerializeField, Min(0f)] float wallJumpAway = 9f;

    [Tooltip("Tempo sem controle horizontal depois do salto. Sem isto o jogador gruda de volta na mesma parede.")]
    [SerializeField, Min(0f)] float wallJumpLockTime = 0.18f;

    [Tooltip("Devolver o pulo duplo ao saltar de uma parede.")]
    [SerializeField] bool wallJumpRestoresAirJump = true;

    [Tooltip("Quanto duas paredes precisam diferir de ângulo para valerem como paredes distintas. " +
             "É o que impede subir sem fim numa parede só: dois saltos seguidos exigem superfícies " +
             "diferentes, e um poço de paredes opostas continua funcionando normalmente.")]
    [SerializeField, Range(10f, 120f)] float distinctWallAngle = 40f;

    /// <summary>Disparado a cada pulo. O parâmetro diz se foi um pulo no ar.</summary>
    public event System.Action<bool> Jumped;

    /// <summary>
    /// Disparado a cada salto de parede, além do <see cref="Jumped"/>. O
    /// tutorial precisa distinguir os dois, e o Jumped sozinho não distingue.
    /// </summary>
    public event System.Action WallJumped;

    /// <summary>Disparado ao tocar o chão, com a velocidade da queda em m/s.</summary>
    public event System.Action<float> Landed;

    public Vector3 HorizontalVelocity => _horizontalVelocity;
    public float VerticalVelocity => _verticalVelocity;
    public bool IsGrounded => _controller.isGrounded;
    public int AirJumpsUsed => _airJumpsUsed;

    public PlayerMoveState State { get; private set; }

    /// <summary>Velocidade extra acumulada por pulos encadeados.</summary>
    public float HopBonus => _hopBonus;

    /// <summary>Quantos pulos precisos seguidos. Zera ao perder o ritmo.</summary>
    public int HopChain { get; private set; }

    /// <summary>Velocidade maxima possivel agora, para a HUD medir o quanto falta.</summary>
    public float TopSpeed => moveSpeed + maxHopBonus;

    /// <summary>Está escorregando agarrada numa parede neste instante.</summary>
    public bool IsWallSliding { get; private set; }

    /// <summary>Normal da superfície sob os pés. Vale Vector3.up quando não há chão perto.</summary>
    public Vector3 GroundNormal => _groundNormal;

    /// <summary>Normal da parede em contato. Aponta da parede para a bruxa.</summary>
    public Vector3 WallNormal => _wallNormal;

    // --- leitura para diagnóstico ---
    public float GroundAngle => _groundAngle;
    public bool HasGround => _hasGround;
    public bool IsTouchingWall => TouchingWall;
    public bool WallJumpAvailable => CanWallJump();

    readonly RaycastHit[] _groundHits = new RaycastHit[8];

    CharacterController _controller;
    Vector3 _horizontalVelocity;
    float _verticalVelocity;
    float _coyoteTimer;
    float _jumpBufferTimer;
    bool _isJumping;
    bool _wasGrounded;
    int _airJumpsUsed;
    Vector3 _wallNormal;
    Vector3 _lastWallJumpNormal;
    bool _hasWallJumped;
    int _wallContactFrame = -10;
    float _wallControlLock;
    float _movementYaw;
    bool _basisLocked;
    Vector3 _groundNormal = Vector3.up;
    float _groundAngle;
    bool _hasGround;
    float _slopeTime;
    float _hopBonus;
    float _lastLandTime = -99f;
    float _lastJumpPressTime = -99f;
    MovingPlatform _platform;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (visual == null) visual = transform;

        if (input == null)
            Debug.LogError($"{nameof(PlayerController)} não achou um {nameof(PlayerInputReader)} na cena.", this);
    }

    void Update()
    {
        if (input == null) return;

        HandleRewindInput();

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        float dt = Time.deltaTime;

        UpdateMovementBasis();
        ProbeGround();

        // Acumula enquanto a leitura de rampa se sustenta; zera ao primeiro
        // frame em terreno plano.
        _slopeTime = IsOnSlideSlope ? _slopeTime + dt : 0f;

        UpdateGroundState(dt);
        UpdateWallState(dt);
        UpdateStateTransitions();

        if (State == PlayerMoveState.Slide)
        {
            UpdateSlide(dt);
        }
        else
        {
            UpdateHorizontal(dt);
            UpdateVertical(dt);
        }

        // O deslocamento da plataforma entra somado, não como velocidade: ele
        // já é a distância deste frame, e o CharacterController não carrega
        // ninguém sozinho.
        Vector3 carry = _platform != null && _controller.isGrounded ? _platform.Delta : Vector3.zero;
        _controller.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * dt + carry);

        if (State == PlayerMoveState.Slide) StickToSlope();
    }

    /// <summary>
    /// Devolve o jogador ao estado de um instante do passado. Usado pelo
    /// <see cref="PlayerRewind"/> ao entregar o controle de volta.
    ///
    /// O pulo duplo entra aqui porque o rewind existe para desfazer erros, e
    /// gastar o pulo do ar na hora errada é um deles: voltar no tempo sem
    /// devolver o pulo deixaria o jogador preso no mesmo beco.
    /// </summary>
    public void RestoreState(Vector3 horizontal, float vertical, int airJumpsUsed,
                             PlayerMoveState state = PlayerMoveState.Normal, float hopBonus = 0f)
    {
        _horizontalVelocity = horizontal;
        _verticalVelocity = vertical;
        _airJumpsUsed = Mathf.Clamp(airJumpsUsed, 0, airJumps);
        _isJumping = vertical > 0f;

        State = state;

        // O embalo volta junto: num jogo de tempo, perder a velocidade
        // acumulada seria uma punicao escondida dentro da mecanica que existe
        // justamente para desfazer punicoes.
        _hopBonus = Mathf.Clamp(hopBonus, 0f, maxHopBonus);
    }

    /// <summary>Zera o movimento e o pulo duplo. Usado ao renascer.</summary>
    public void ResetToIdle()
    {
        State = PlayerMoveState.Normal;
        _slopeTime = 0f;
        HopChain = 0;
        RestoreState(Vector3.zero, 0f, 0);
    }

    // ------------------------------------------------------------------
    // Parede
    // ------------------------------------------------------------------

    /// <summary>
    /// Registra encostões em parede.
    ///
    /// Vem do callback de colisão do CharacterController em vez de um raycast
    /// próprio: o motor já calculou esse contato ao mover, com a normal exata
    /// da superfície. Um cast extra custaria mais e ainda poderia discordar do
    /// que a física de fato resolveu.
    ///
    /// O preço é que só há contato enquanto a bruxa empurra contra a parede -
    /// que é justamente a condição que queremos para o wall slide.
    /// </summary>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Vector3.Angle(hit.normal, Vector3.up) < minWallAngle) return;

        _wallNormal = hit.normal;
        _wallContactFrame = Time.frameCount;
    }

    /// <summary>O contato vem do Move do frame anterior, daí a tolerância de um frame.</summary>
    bool TouchingWall => Time.frameCount - _wallContactFrame <= 1;

    void UpdateWallState(float dt)
    {
        _wallControlLock = Mathf.Max(0f, _wallControlLock - dt);

        // O contato só existe enquanto a bruxa empurra contra a parede, então a
        // intenção de agarrar já está implícita em haver contato.
        IsWallSliding = !_controller.isGrounded
                        && TouchingWall
                        && _verticalVelocity < 0f;

        if (!IsWallSliding) return;

        // Sem isto a velocidade contra a parede se acumula sem efeito, e ao
        // saltar a bruxa dispararia de volta para dentro dela.
        Vector3 intoWall = Vector3.Project(_horizontalVelocity, -_wallNormal);
        if (Vector3.Dot(intoWall, -_wallNormal) > 0f) _horizontalVelocity -= intoWall;
    }

    void HandleRewindInput()
    {
        var manager = RewindManager.Instance;
        if (manager == null) return;

        if (input.RewindHeld) manager.BeginRewind();
        else manager.EndRewind();
    }

    void UpdateGroundState(float dt)
    {
        bool grounded = _controller.isGrounded;

        if (grounded)
        {
            // O aviso de aterrissagem sai antes de zerar a velocidade, senão
            // quem escuta receberia sempre uma queda de zero.
            if (!_wasGrounded)
            {
                _lastLandTime = Time.time;
                Landed?.Invoke(Mathf.Abs(_verticalVelocity));
            }

            _coyoteTimer = coyoteTime;
            _isJumping = false;
            _airJumpsUsed = 0;
            _hasWallJumped = false;

            // Um empurrãozinho para baixo mantém o isGrounded estável em rampas
            // e degraus - sem isso o CharacterController fica piscando no ar.
            if (_verticalVelocity < 0f) _verticalVelocity = -2f;
        }
        else
        {
            _coyoteTimer -= dt;
        }

        _wasGrounded = grounded;

        if (input.JumpPressedThisFrame)
        {
            _jumpBufferTimer = jumpBufferTime;
            _lastJumpPressTime = Time.time;
        }
        else
        {
            _jumpBufferTimer -= dt;
        }

        // Parada no chao alem da janela: o embalo escorre.
        if (grounded && Time.time - _lastLandTime > hopWindow)
        {
            _hopBonus = Mathf.MoveTowards(_hopBonus, 0f, hopDecay * dt);
            if (_hopBonus <= 0.01f) HopChain = 0;
        }
    }

    // ------------------------------------------------------------------
    // Slide
    // ------------------------------------------------------------------

    void UpdateStateTransitions()
    {
        if (State == PlayerMoveState.Normal)
        {
            if (CanStartSlide()) StartSlide();
            return;
        }

        // Continua deslizando enquanto a rampa segurar, ou enquanto sobrar
        // velocidade no plano - é isso que deixa a bruxa sair da descida e
        // seguir escorregando até a fricção comer tudo.
        bool stillSliding = _controller.isGrounded
                            && (IsOnSlideSlope || _horizontalVelocity.magnitude > slideExitSpeed);

        if (!stillSliding) State = PlayerMoveState.Normal;
    }

    /// <summary>A rampa sob os pés é íngreme o bastante para escorregar.</summary>
    bool IsOnSlideSlope => _hasGround && _groundAngle >= autoSlideAngle;

    /// <summary>Direção horizontal para onde a rampa desce. Zero em chão plano.</summary>
    Vector3 DownhillDirection
    {
        get
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            downhill.y = 0f;

            return downhill.sqrMagnitude > 0.0001f ? downhill.normalized : Vector3.zero;
        }
    }

    /// <summary>
    /// Entra no slide só descendo.
    ///
    /// Todas as rampas do pack têm 34° ou 45°, acima de qualquer limiar
    /// razoável - se bastasse estar na rampa, nenhuma rampa do jogo poderia ser
    /// subida a pé. Exigindo movimento rampa abaixo, subir continua possível e
    /// descer escorrega sozinho, que é o que se espera de uma ladeira.
    /// </summary>
    bool CanStartSlide()
    {
        if (!_controller.isGrounded || !IsOnSlideSlope) return false;
        if (_slopeTime < slopeConfirmSeconds) return false;

        return Vector3.Dot(_horizontalVelocity, DownhillDirection) >= minDownhillSpeed;
    }

    void StartSlide()
    {
        State = PlayerMoveState.Slide;

        if (_horizontalVelocity.sqrMagnitude > 0.01f)
        {
            _horizontalVelocity = Vector3.ClampMagnitude(
                _horizontalVelocity + _horizontalVelocity.normalized * slideBoost, maxSlideSpeed);
        }
    }

    /// <summary>
    /// Slide: pouca fricção, rampa manda no resto.
    ///
    /// A gravidade projetada na superfície acelera descendo e freia subindo, e
    /// a mesma conta dá o sinal certo nos dois casos. A fricção cai com a
    /// inclinação mas nunca a zero - sem um piso, a aceleração da rampa não
    /// teria nada para equilibrá-la numa descida longa.
    /// </summary>
    void UpdateSlide(float dt)
    {
        if (_hasGround && _groundAngle > 0.5f)
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            var horizontal = new Vector3(downhill.x, 0f, downhill.z);
            _horizontalVelocity += horizontal * (gravity * slopeAcceleration * dt);
        }

        float steepness = Mathf.Clamp01(_groundAngle / frictionFadeAngle);
        float friction = slideFriction * Mathf.Lerp(1f, minFrictionScale, steepness);
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, friction * dt);

        // Direcionar gira a velocidade em vez de somar a ela: o slide não deve
        // ser uma forma de acelerar de lado, só de escolher para onde vai.
        Vector3 wish = CameraRelative(input.Move);
        if (wish.sqrMagnitude > 0.001f && _horizontalVelocity.sqrMagnitude > 0.01f)
        {
            _horizontalVelocity = Vector3.RotateTowards(
                _horizontalVelocity, wish * _horizontalVelocity.magnitude,
                slideSteerSpeed * Mathf.Deg2Rad * dt, 0f);
        }

        float speed = _horizontalVelocity.magnitude;
        if (speed > maxSlideSpeed) _horizontalVelocity *= maxSlideSpeed / speed;

        // Pular do slide guarda a velocidade: é a recompensa de ter deslizado.
        if (_jumpBufferTimer > 0f)
        {
            State = PlayerMoveState.Normal;
            Jump(jumpHeight, false);
            return;
        }

        if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        else _verticalVelocity = Mathf.Max(_verticalVelocity - gravity * fallGravityMultiplier * dt, terminalVelocity);

        UpdateFacing(dt);
    }

    // ------------------------------------------------------------------
    // Movimento normal
    // ------------------------------------------------------------------

    void UpdateHorizontal(float dt)
    {
        // Logo depois de saltar de uma parede o input é ignorado. Sem essa
        // janela, segurar a direção da parede colaria a bruxa de volta nela no
        // frame seguinte e o salto não levaria a lugar nenhum.
        if (_wallControlLock <= 0f)
        {
            Vector3 wishDirection = CameraRelative(input.Move);
            float targetSpeed = moveSpeed + _hopBonus;

            if (_controller.isGrounded)
            {
                Vector3 targetVelocity = wishDirection * targetSpeed;

                float acceleration = wishDirection.sqrMagnitude > 0.001f
                    ? groundAcceleration
                    : groundDeceleration;

                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity,
                                                          acceleration * dt);
            }
            else
            {
                _horizontalVelocity = AirControl(_horizontalVelocity, wishDirection, targetSpeed, dt);
            }
        }

        UpdateFacing(dt);
    }

    /// <summary>
    /// Controle no ar: gira o embalo, depois acelera. Só freia quando o
    /// direcional aponta contra ele.
    ///
    /// São dois passos porque um só não dá conta. Somar na direção pedida, que
    /// era o modelo anterior, preserva o embalo mas não curva: voando a 11 m/s
    /// e segurando para o lado, a bruxa chegava a 45 graus do rumo e o módulo
    /// SUBIA para 15,6 m/s. Ela não virava - deslizava na diagonal, acelerando.
    /// Daí a sensação estranha.
    ///
    /// Girar primeiro resolve isso sem reintroduzir a freada: o vetor roda para
    /// o lado pedido mantendo o módulo, então mudar de ideia no ar custa tempo,
    /// não velocidade. A soma depois ainda existe para quem salta parado ou
    /// vinha devagar.
    ///
    /// Soltar o direcional continua não tirando nada: o excesso que vier do
    /// hop, de uma bolha ou de uma plataforma sobrevive ao voo.
    ///
    /// O que mudou foi a meia-volta. Girar sempre preservando o módulo deixava
    /// a bruxa inverter o sentido no ar em 0,33 s a 11 m/s, sem perder nada -
    /// o embalo inteiro mudava de lado como se não tivesse massa, e numa
    /// correção pequena ela respondia demais. Agora, apontar CONTRA o embalo
    /// freia primeiro; só depois de parar ela acelera para o outro lado, e a
    /// meia-volta leva cerca de um segundo. Curvas abaixo do ângulo de
    /// inversão seguem girando como antes, só mais devagar.
    /// </summary>
    Vector3 AirControl(Vector3 current, Vector3 wish, float targetSpeed, float dt)
    {
        if (wish.sqrMagnitude < 0.0001f) return current;

        float speed = current.magnitude;

        // Contra o embalo: freia, e não acelera no mesmo quadro. Somar o
        // passo 2 aqui empurraria para trás enquanto o freio ainda trabalha,
        // e a meia-volta voltaria a ser instantânea.
        if (speed > 0.01f && Vector3.Angle(current, wish) > airReverseAngle)
            return Vector3.MoveTowards(current, Vector3.zero, airBrake * dt);

        // Passo 1: girar o que já existe, preservando o módulo. O zero no
        // último argumento é o que garante isso - RotateTowards mexeria no
        // comprimento se ele fosse maior.
        if (speed > 0.01f)
        {
            current = Vector3.RotateTowards(current, wish * speed,
                                            airTurnSpeed * Mathf.Deg2Rad * dt, 0f);
        }

        // Passo 2: completar até o alvo, e só para frente.
        float alongWish = Vector3.Dot(current, wish);
        float missing = targetSpeed - alongWish;
        if (missing > 0f) current += wish * Mathf.Min(airAcceleration * dt, missing);

        return current;
    }

    /// <summary>
    /// Para onde a bruxa olha. Agarrada na parede ela encara a parede, e não a
    /// direção do movimento: escorregando, a velocidade é quase toda vertical e
    /// a orientação ficaria travada no que sobrou do movimento anterior.
    /// </summary>
    void UpdateFacing(float dt)
    {
        Vector3 lookDirection;

        if (IsWallSliding)
        {
            lookDirection = Vector3.ProjectOnPlane(-_wallNormal, Vector3.up);
        }
        else
        {
            if (_horizontalVelocity.sqrMagnitude <= 0.01f) return;
            lookDirection = _horizontalVelocity;
        }

        if (lookDirection.sqrMagnitude < 0.001f) return;

        var look = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        visual.rotation = Quaternion.RotateTowards(visual.rotation, look, turnSpeed * dt);
    }

    void UpdateVertical(float dt)
    {
        if (_jumpBufferTimer > 0f)
        {
            // A parede vem antes do pulo do ar: com os dois disponíveis, gastar
            // o pulo duplo num salto que a parede daria de graça é sempre pior.
            if (CanWallJump()) WallJump();
            else if (_coyoteTimer > 0f) Jump(jumpHeight, false);
            else if (_airJumpsUsed < airJumps) Jump(doubleJumpHeight, true);
        }

        // Soltar o botão no meio da subida corta o pulo: pulinho curto para
        // ajuste fino, pulo cheio para os saltos longos.
        if (_isJumping && !input.JumpHeld && _verticalVelocity > 0f)
        {
            _verticalVelocity *= jumpCutMultiplier;
            _isJumping = false;
        }

        // Escorregar é uma queda freada, não uma queda livre.
        if (IsWallSliding && _verticalVelocity < -wallSlideSpeed)
            _verticalVelocity = -wallSlideSpeed;

        _verticalVelocity = Mathf.Max(_verticalVelocity - gravity * GravityScale() * dt, terminalVelocity);
    }

    void Jump(float height, bool isAirJump)
    {
        if (isAirJump) _airJumpsUsed++;
        else EvaluateHop();

        // Saltar de uma plataforma em movimento leva o embalo dela. Sem isto a
        // bruxa perderia a velocidade no instante do salto e cairia atrás da
        // plataforma, que é o contrário do que o olho espera.
        if (!isAirJump && _platform != null && _controller.isGrounded)
        {
            Vector3 platformSpeed = _platform.Velocity;
            _horizontalVelocity += new Vector3(platformSpeed.x, 0f, platformSpeed.z);
        }

        // v = sqrt(2 * g * h): a velocidade exata para o topo do pulo bater na
        // altura pedida no Inspector. Sobrescreve a velocidade em vez de somar,
        // então o pulo duplo tem sempre a mesma altura, subindo ou caindo.
        _verticalVelocity = Mathf.Sqrt(2f * gravity * height);
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        _isJumping = true;

        Jumped?.Invoke(isAirJump);
    }

    /// <summary>
    /// Só vale saltar de uma parede diferente da anterior.
    ///
    /// Sem essa regra, uma parede sozinha vira escada infinita: o salto devolve
    /// altura e o pulo duplo, o controle aéreo traz a bruxa de volta, e o ciclo
    /// se repete para sempre - plataforma nenhuma seria necessária. Exigir
    /// superfícies distintas mantém o poço de paredes opostas funcionando e
    /// mata a subida numa parede só.
    ///
    /// A memória zera ao tocar o chão: uma aproximação nova merece um salto novo.
    /// </summary>
    bool CanWallJump()
    {
        if (!IsWallSliding) return false;
        if (!_hasWallJumped) return true;

        return Vector3.Angle(_wallNormal, _lastWallJumpNormal) >= distinctWallAngle;
    }

    /// <summary>
    /// Decide se o pulo que acabou de sair foi preciso, e ajusta o embalo.
    ///
    /// Duas condicoes, e a segunda e a que transforma isso em habilidade: o
    /// pulo precisa sair dentro da janela depois de encostar no chao, E o
    /// aperto precisa ter acontecido DEPOIS do toque. Sem a segunda, bastaria
    /// apertar antes de aterrissar e deixar o buffer de pulo resolver - o
    /// jogador ganharia velocidade martelando o botao, sem nenhuma precisao.
    ///
    /// O buffer continua existindo e perdoando o pulo; ele so nao rende bonus.
    /// Errar o tempo custa velocidade, nao o salto.
    /// </summary>
    void EvaluateHop()
    {
        bool insideWindow = Time.time - _lastLandTime <= hopWindow;
        bool pressedAfterLanding = _lastJumpPressTime >= _lastLandTime;

        if (insideWindow && pressedAfterLanding)
        {
            HopChain++;
            _hopBonus = Mathf.Min(_hopBonus + hopGain, maxHopBonus);
            return;
        }

        HopChain = 0;
        _hopBonus = 0f;
    }

    void WallJump()
    {
        _lastWallJumpNormal = _wallNormal;
        _hasWallJumped = true;

        Vector3 away = Vector3.ProjectOnPlane(_wallNormal, Vector3.up).normalized;

        _horizontalVelocity = away * wallJumpAway;
        _verticalVelocity = Mathf.Sqrt(2f * gravity * wallJumpHeight);

        _wallControlLock = wallJumpLockTime;
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        _isJumping = true;
        IsWallSliding = false;

        // Sem devolver o pulo do ar, uma sequência de paredes viraria uma
        // escada de mão única: erra um salto e não sobra nada para corrigir.
        if (wallJumpRestoresAirJump) _airJumpsUsed = 0;

        Jumped?.Invoke(false);
        WallJumped?.Invoke();
    }

    /// <summary>
    /// Impulso para cima de uma bolha, com o pulo do ar devolvido.
    ///
    /// Devolver o pulo é o que faz a mecânica render: sem isso a bolha seria só
    /// um trampolim e a altura continuaria limitada ao que um pulo duplo
    /// alcança. Com ele, o teto passa a ser quantas bolhas se encadeia.
    ///
    /// Mantém o controle - ao contrário do <see cref="Knockback"/>. Quicar é
    /// algo que o jogador fez de propósito, e tirar a direção no meio de uma
    /// sequência de bolhas puniria justo quem acertou.
    /// </summary>
    public void Bounce(float height)
    {
        State = PlayerMoveState.Normal;

        // Max, e não atribuição direta. Agora que a bolha estoura ao toque de
        // qualquer lado, dá para encostar nela já subindo depressa - e
        // sobrescrever a velocidade ali FREARIA quem estava subindo mais rápido
        // que o quique. Uma bolha nunca pode tirar altura de ninguém.
        _verticalVelocity = Mathf.Max(_verticalVelocity,
                                      Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height)));
        _airJumpsUsed = 0;

        // A bolha conta como contato novo: libera a parede de onde já se saltou,
        // senão uma bolha ao lado de uma parede não ajudaria a subir por ela.
        _hasWallJumped = false;

        // Falso pelo mesmo motivo do arremesso: com ele ligado, soltar a tecla
        // de pulo cortaria a subida de um quique que ninguém pediu com a tecla.
        _isJumping = false;

        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        IsWallSliding = false;
    }

    /// <summary>
    /// Arremessa a bruxa e tira o controle por um instante.
    ///
    /// A janela sem controle é o que faz a pancada existir. Sem ela, segurar a
    /// direção contrária cancelaria o empurrão no frame seguinte e a hélice
    /// viraria um cutucão - o mesmo motivo pelo qual o salto de parede trava o
    /// input, então reaproveita a mesma trava.
    ///
    /// Não devolve nem consome o pulo do ar de propósito: quem é arremessado
    /// sobre a lava ainda tem o pulo duplo para se salvar, e essa chance de
    /// recuperação é justamente o que separa ser empurrado de ser morto.
    /// </summary>
    public void Knockback(Vector3 horizontal, float riseHeight, float lockSeconds)
    {
        State = PlayerMoveState.Normal;

        _horizontalVelocity = horizontal;
        _verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, riseHeight));

        _wallControlLock = Mathf.Max(_wallControlLock, lockSeconds);

        // Falso, e não verdadeiro: com _isJumping ligado, soltar a tecla de
        // pulo cortaria a subida do arremesso. A pancada não é um pulo e não
        // deve obedecer ao botão.
        _isJumping = false;

        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        IsWallSliding = false;

        // Levar pancada zera a corrente: o embalo é prêmio por precisão, e ser
        // arremessado é o oposto disso.
        HopChain = 0;
        _hopBonus = 0f;
    }

    /// <summary>
    /// Gravidade do frame. A queda é mais pesada que a subida, e perto do topo
    /// do pulo ela afrouxa: é a "flutuada" no ápice que dá tempo de mirar a
    /// próxima plataforma, o que faz o pulo parecer generoso sem ser mais alto.
    /// </summary>
    float GravityScale()
    {
        float scale = _verticalVelocity < 0f ? fallGravityMultiplier : 1f;
        if (Mathf.Abs(_verticalVelocity) < apexThreshold) scale *= apexGravityMultiplier;

        return scale;
    }

    // ------------------------------------------------------------------
    // Chão e rampas
    // ------------------------------------------------------------------

    /// <summary>
    /// Lê a inclinação da superfície sob os pés.
    ///
    /// Raio, e não esfera. Uma esfera encosta na ARESTA da plataforma quando a
    /// bruxa chega perto da borda, e o normal de uma aresta é a direção dela
    /// até o centro da esfera - ou seja, qualquer ângulo entre 0° e 90°. Caindo
    /// na faixa do slide, a bruxa escorregava da borda e caía do mapa. Um raio
    /// só lê a face sob os pés, que é o que a inclinação significa.
    /// </summary>
    void ProbeGround()
    {
        _groundNormal = Vector3.up;
        _groundAngle = 0f;
        _hasGround = false;
        _platform = null;

        Vector3 origin = transform.position + _controller.center;
        float distance = _controller.height * 0.5f + 0.6f;

        if (!Physics.Raycast(origin, Vector3.down, out var hit, distance,
                             groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (hit.transform.IsChildOf(transform)) return;

        // Acima do limite de inclinação é parede, não chão.
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        if (angle > _controller.slopeLimit) return;

        _groundNormal = hit.normal;
        _groundAngle = angle;
        _hasGround = true;

        // A mesma sonda que lê a inclinação diz em cima de quem a bruxa está.
        _platform = hit.transform.GetComponentInParent<MovingPlatform>();
    }

    /// <summary>
    /// Puxa a bruxa de volta para a rampa depois de mover.
    ///
    /// Sem isto, deslizar rampa abaixo vira uma sequência de pulinhos: o
    /// movimento é reto, a rampa desce, e a cada frame ela sai do chão e cai de
    /// novo. Só age descendo, para não grudar a bruxa quando ela deveria
    /// decolar no fim de uma rampa de subida.
    /// </summary>
    void StickToSlope()
    {
        if (_controller.isGrounded || _verticalVelocity > 0f) return;

        float closest = float.MaxValue;
        int count = CastDown(groundSnapDistance);

        for (int i = 0; i < count; i++)
        {
            ref var hit = ref _groundHits[i];
            if (!IsUsableGroundHit(hit)) continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > _controller.slopeLimit) continue;

            closest = Mathf.Min(closest, hit.distance);
        }

        if (closest < float.MaxValue) _controller.Move(Vector3.down * closest);
    }

    int CastDown(float extraDistance)
    {
        Vector3 origin = transform.position + _controller.center;
        float radius = Mathf.Max(0.01f, _controller.radius * 0.9f);
        float distance = _controller.height * 0.5f - _controller.radius + extraDistance;

        return Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHits,
                                          distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// O cast começa dentro da própria cápsula: colisores sobrepostos à origem
    /// voltam com distância 0, e o collider do jogador entra na lista.
    /// </summary>
    bool IsUsableGroundHit(in RaycastHit hit) =>
        hit.distance > 0.001f && !hit.transform.IsChildOf(transform);

    /// <summary>
    /// Mantém a direção do input estável quando a câmera troca de ângulo.
    ///
    /// O movimento é relativo à câmera, então mudar o yaw no meio de uma
    /// corrida redefine o que "para frente" significa e a bruxa vira sozinha.
    /// A base fica congelada desde o início da transição até o jogador soltar a
    /// direção - aí ela adota o ângulo novo, que é quando ele está olhando e
    /// decidindo para onde ir.
    ///
    /// Soltar não é obrigatório para jogar: a base só trava em transição, e
    /// segurar em frente continua indo em frente.
    /// </summary>
    void UpdateMovementBasis()
    {
        float cameraYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
        bool holdingDirection = input.Move.sqrMagnitude > 0.01f;

        if (_basisLocked)
        {
            if (holdingDirection) return;
            _basisLocked = false;
        }
        else if (holdingDirection && IsCameraReframing())
        {
            _basisLocked = true;
            return;
        }

        _movementYaw = cameraYaw;
    }

    bool IsCameraReframing()
    {
        if (cameraTransform == null) return false;

        return cameraTransform.TryGetComponent<OrbitCamera>(out var orbit) && orbit.IsReframing;
    }

    /// <summary>Converte o input 2D para uma direção no plano do chão, na base atual.</summary>
    Vector3 CameraRelative(Vector2 move)
    {
        if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

        var basis = Quaternion.Euler(0f, _movementYaw, 0f);
        Vector3 direction = basis * new Vector3(move.x, 0f, move.y);

        return Vector3.ClampMagnitude(direction, 1f);
    }
}
