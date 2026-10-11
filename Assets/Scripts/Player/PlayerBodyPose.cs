using UnityEngine;

/// <summary>
/// Dono único da rotação do corpo da bruxa: cambalhota, pose de parede e slide.
///
/// Todas moram aqui porque escrevem no mesmo transform. Separadas em scripts
/// diferentes, uma sobrescreveria a outra a cada frame e o resultado dependeria
/// da ordem de execução - o tipo de bug que só aparece depois de recompilar.
///
/// São procedurais porque o pack não traz clipe de nenhuma delas. Para poses de
/// corpo inteiro, girar o modelo lê tão bem quanto animar osso por osso.
///
/// O transform ligado aqui precisa ter o pivô na altura da cintura: girar no
/// pivô do modelo, que fica nos pés, faria a bruxa orbitar o chão em vez de
/// rodar sobre si mesma.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerBodyPose : MonoBehaviour
{
    [Tooltip("Transform com pivô na cintura. O WitchSetup cria um objeto 'Giro' para isso.")]
    [SerializeField] Transform visual;

    [Header("Cambalhota (pulo duplo)")]
    [Tooltip("Duração do giro completo. Curto demais vira borrão, longo demais atrasa a leitura da queda.")]
    [SerializeField, Min(0.05f)] float flipDuration = 0.45f;

    [Header("Parede")]
    [Tooltip("Inclinação ao escorregar. Negativo joga o peito contra a parede.")]
    [SerializeField, Range(-40f, 40f)] float wallSlideAngle = -18f;

    [Header("Slide")]
    [Tooltip("Reclinação no slide. Negativo joga o tronco para trás e as pernas para a frente, " +
             "que é a pose de escorregar sentada.")]
    [SerializeField, Range(-80f, 0f)] float slideAngle = -50f;

    [Tooltip("Quanto a bruxa desce ao deslizar. Reclinar sozinho a deixaria sentada no ar; " +
             "este recuo encosta o traseiro no chão.")]
    [SerializeField, Range(-1.5f, 0f)] float slideHeightOffset = -0.7f;

    [Header("Transições")]
    [Tooltip("Tempo para entrar e sair de qualquer pose sustentada.")]
    [SerializeField, Min(0.02f)] float blendSeconds = 0.12f;

    PlayerController _player;
    Vector3 _basePosition;
    float _flipElapsed = -1f;   // negativo enquanto não há giro em andamento
    float _poseBlend;
    Quaternion _poseTarget = Quaternion.identity;
    float _poseHeight;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (visual == null) visual = transform;

        _basePosition = visual.localPosition;
    }

    void OnEnable()
    {
        _player.Jumped += OnJumped;
        _player.Landed += OnLanded;
    }

    void OnDisable()
    {
        _player.Jumped -= OnJumped;
        _player.Landed -= OnLanded;

        _flipElapsed = -1f;
        _poseBlend = 0f;
        ResetVisual();
    }

    void ResetVisual()
    {
        if (visual == null) return;

        visual.localRotation = Quaternion.identity;
        visual.localPosition = _basePosition;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding)
        {
            // Durante o rewind a pose acompanha o estado restaurado; um
            // temporizador próprio continuaria correndo para frente.
            _flipElapsed = -1f;
        }

        if (TryGetSustainedPose(out var target, out float height))
        {
            // Poses sustentadas vencem a cambalhota: se as duas disputassem o
            // corpo, o giro ganharia e a pose piscaria por baixo dele.
            _flipElapsed = -1f;
            _poseTarget = target;
            _poseHeight = height;
            _poseBlend = Mathf.MoveTowards(_poseBlend, 1f, dt / blendSeconds);
        }
        else
        {
            _poseBlend = Mathf.MoveTowards(_poseBlend, 0f, dt / blendSeconds);
        }

        if (_poseBlend > 0.001f)
        {
            visual.localRotation = Quaternion.Slerp(Quaternion.identity, _poseTarget, _poseBlend);
            visual.localPosition = _basePosition + Vector3.up * (_poseHeight * _poseBlend);
            return;
        }

        visual.localPosition = _basePosition;
        UpdateFlip(dt);
    }

    /// <summary>
    /// A pose sustentada do estado atual, se houver. A ordem é a prioridade.
    /// </summary>
    bool TryGetSustainedPose(out Quaternion pose, out float heightOffset)
    {
        heightOffset = 0f;

        if (_player.State == PlayerMoveState.Slide)
        {
            pose = Quaternion.Euler(slideAngle, 0f, 0f);
            heightOffset = slideHeightOffset;
            return true;
        }

        if (_player.IsWallSliding)
        {
            pose = Quaternion.Euler(wallSlideAngle, 0f, 0f);
            return true;
        }

        pose = Quaternion.identity;
        return false;
    }

    void UpdateFlip(float dt)
    {
        if (_flipElapsed < 0f)
        {
            visual.localRotation = Quaternion.identity;
            return;
        }

        _flipElapsed += dt;
        float t = Mathf.Clamp01(_flipElapsed / flipDuration);

        // Ease-out: sai rápido e assenta suave, para o giro não terminar num
        // solavanco quando a bruxa volta à posição normal.
        float eased = 1f - (1f - t) * (1f - t);
        visual.localRotation = Quaternion.Euler(360f * eased, 0f, 0f);

        if (t >= 1f) _flipElapsed = -1f;
    }

    void OnJumped(bool isAirJump)
    {
        if (isAirJump && _player.State == PlayerMoveState.Normal) _flipElapsed = 0f;
    }

    // Aterrissar no meio do giro deixaria a bruxa de cabeça para baixo no chão.
    void OnLanded(float fallSpeed) => _flipElapsed = -1f;
}
