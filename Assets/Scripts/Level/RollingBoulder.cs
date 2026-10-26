using UnityEngine;

/// <summary>
/// Bola gigante de pedra que desce por uma trajetória fixa.
///
/// Não usa física. A bruxa é um CharacterController, que não é empurrado
/// por corpos rígidos, e uma bola de Rigidbody numa pista estreita trava em
/// barreiras, quica em emendas e sai do percurso de um jeito diferente a cada
/// tentativa - e numa fase de speedrun o jogador precisa poder decorar o
/// padrão. Aqui a posição é uma função do relógio, como na MovingPlatform:
/// a bola percorre os pontos do caminho (já colados na superfície da pista)
/// com velocidade e aceleração dadas, e a rotação visual vem da distância
/// rolada, então ela parece rolar sem nunca escorregar.
///
/// O relógio anda para trás no rewind, então voltar no tempo devolve a bola
/// ao ponto daquele instante. Nada precisa ser gravado.
///
/// Dois modos:
///  - em ciclo: sai do início a cada <see cref="period"/> segundos, com
///    defasagem; passado o fim do caminho, some até o próximo ciclo;
///  - perseguição: espera parada no início até a bruxa entrar na área de
///    disparo, desce uma vez e some no fim. Morrer devolve a bola ao início,
///    e voltar no tempo para antes do disparo também.
/// </summary>
public class RollingBoulder : MonoBehaviour
{
    [Tooltip("Pontos do caminho, em coordenadas de mundo, já na altura do centro da bola.")]
    [SerializeField] Vector3[] path = new Vector3[0];

    [SerializeField, Min(0.2f)] float radius = 1.6f;

    [Tooltip("Velocidade na saída, em m/s.")]
    [SerializeField, Min(0f)] float speed = 7f;

    [Tooltip("Aceleração ao longo do caminho, em m/s². Zero mantém a velocidade.")]
    [SerializeField] float acceleration;

    [Tooltip("Teto de velocidade, para a aceleração não crescer sem fim.")]
    [SerializeField, Min(0.1f)] float maxSpeed = 16f;

    [Header("Ciclo")]
    [Tooltip("Desligado: a bola só desce quando a bruxa entra na área de disparo (perseguição).")]
    [SerializeField] bool looping = true;

    [Tooltip("Segundos entre uma saída e a próxima.")]
    [SerializeField, Min(0.5f)] float period = 4f;

    [SerializeField, Range(0f, 1f)] float phase;

    [Header("Perseguição")]
    [Tooltip("Área que dispara a bola quando a bruxa entra.")]
    [SerializeField] Bounds trigger = new Bounds(Vector3.zero, Vector3.zero);

    [Header("Partes")]
    [SerializeField] Transform visual;
    [SerializeField] Collider hazard;

    float[] _lengths;
    float _total;
    float _time;
    float _startTime = float.NaN;
    int _lastKillFrame = -1;
    bool _visible = true;
    Transform _player;

    /// <summary>Comprimento do caminho, em metros.</summary>
    public float Length => _total;

    void Awake()
    {
        BuildLengths();
        if (hazard != null) hazard.isTrigger = true;
        _time = phase * period;
    }

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) _player = player.transform;

        if (LevelRespawn.Instance != null) LevelRespawn.Instance.Died += OnDied;
        Apply();
    }

    void OnDestroy()
    {
        if (LevelRespawn.Instance != null) LevelRespawn.Instance.Died -= OnDied;
    }

    /// <summary>Morrer recomeça a perseguição do início.</summary>
    void OnDied() => _startTime = float.NaN;

    void Update()
    {
        var manager = RewindManager.Instance;
        bool rewinding = manager != null && manager.IsRewinding;

        if (rewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        if (!looping)
        {
            // Voltar no tempo para antes do disparo desarma a bola: ela volta
            // a esperar a bruxa no início.
            if (!float.IsNaN(_startTime) && _time < _startTime) _startTime = float.NaN;

            if (float.IsNaN(_startTime) && !rewinding && _player != null && trigger.size.sqrMagnitude > 0f
                && trigger.Contains(_player.position))
            {
                _startTime = _time;
            }
        }

        Apply();
    }

    /// <summary>Distância rolada desde a saída, conforme o modo.</summary>
    float Travelled()
    {
        float t;
        if (looping) t = Mathf.Repeat(_time, period);
        else if (float.IsNaN(_startTime)) return 0f;
        else t = _time - _startTime;

        if (acceleration <= 0f || speed >= maxSpeed) return Mathf.Min(speed, maxSpeed) * t;

        // Acelera até o teto, depois segue nele.
        float rampTime = (maxSpeed - speed) / acceleration;
        if (t <= rampTime) return speed * t + 0.5f * acceleration * t * t;
        return speed * rampTime + 0.5f * acceleration * rampTime * rampTime + maxSpeed * (t - rampTime);
    }

    void Apply()
    {
        if (path == null || path.Length < 2 || _total <= 0f) return;

        float s = Travelled();
        bool visible = s <= _total;
        SetVisible(visible);
        if (!visible) return;

        Vector3 point = PointAt(s, out Vector3 direction);
        transform.position = point;

        if (visual != null)
        {
            // Rola sem escorregar: o ângulo é a distância dividida pelo raio,
            // em volta do eixo deitado perpendicular ao movimento.
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 axis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            visual.rotation = Quaternion.AngleAxis(s / radius * Mathf.Rad2Deg, axis);
        }
    }

    void SetVisible(bool visible)
    {
        if (visible == _visible) return;
        _visible = visible;
        if (visual != null) visual.gameObject.SetActive(visible);
        if (hazard != null) hazard.enabled = visible;
    }

    Vector3 PointAt(float s, out Vector3 direction)
    {
        for (int i = 0; i < _lengths.Length; i++)
        {
            if (s <= _lengths[i] || i == _lengths.Length - 1)
            {
                Vector3 a = path[i], b = path[i + 1];
                float start = i == 0 ? 0f : _lengths[i - 1];
                float segment = _lengths[i] - start;
                direction = (b - a).normalized;
                return Vector3.Lerp(a, b, segment > 0f ? Mathf.Clamp01((s - start) / segment) : 0f);
            }
        }

        direction = Vector3.forward;
        return path[path.Length - 1];
    }

    void BuildLengths()
    {
        int count = path != null ? Mathf.Max(0, path.Length - 1) : 0;
        _lengths = new float[count];
        _total = 0f;
        for (int i = 0; i < count; i++)
        {
            _total += Vector3.Distance(path[i], path[i + 1]);
            _lengths[i] = _total;
        }
    }

    void OnTriggerEnter(Collider other) => TryKill(other);
    void OnTriggerStay(Collider other) => TryKill(other);

    void TryKill(Collider other)
    {
        if (!_visible || _lastKillFrame == Time.frameCount) return;

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        if (other.GetComponentInParent<PlayerController>() == null) return;

        var respawn = LevelRespawn.Instance;
        if (respawn == null) return;

        // Uma morte por quadro, mesmo que vários colliders entrem juntos.
        _lastKillFrame = Time.frameCount;
        respawn.RestartLevel();
    }

    void OnDrawGizmosSelected()
    {
        if (path == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.1f);
        for (int i = 0; i + 1 < path.Length; i++) Gizmos.DrawLine(path[i], path[i + 1]);
        if (!looping) Gizmos.DrawWireCube(trigger.center, trigger.size);
    }
}
