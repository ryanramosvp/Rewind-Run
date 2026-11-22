using UnityEngine;

/// <summary>Estado do cristal na linha do tempo: o instante em que quebrou (NaN: inteiro).</summary>
public struct AmostraCristal
{
    public float QuebrouEm;
}

/// <summary>
/// Plataforma de cristal que racha quando a bruxa pisa e quebra logo depois.
///
/// Voltar no tempo a reconstrói: é um rewind de 1 segundo que devolve o
/// caminho, e a primeira vez que o jogador vê isso entende que o rewind
/// também conserta o mundo, não só a bruxa.
///
/// Também volta sozinha, depois de alguns segundos. Sem isso, um jogador sem
/// carga de rewind ficaria preso do lado errado esperando a carga, o que é
/// castigo, não desafio.
/// </summary>
public class CristalFragil : RewindableBehaviour<AmostraCristal>
{
    [Tooltip("Segundos entre pisar e quebrar.")]
    [SerializeField, Min(0.05f)] float segundosAteQuebrar = 0.45f;

    [Tooltip("Segundos quebrado até voltar sozinho.")]
    [SerializeField, Min(0.5f)] float segundosAteVoltar = 4f;

    [SerializeField] GameObject visual;
    [SerializeField] Collider solido;

    [Tooltip("Áudio do estalo. Opcional.")]
    [SerializeField] AudioSource som;

    float _relogio;
    float _pisadoEm = float.NaN;
    float _quebrouEm = float.NaN;
    Transform _bruxa;
    PlayerController _controller;
    Vector3 _escalaVisual;

    void Awake()
    {
        if (visual != null) _escalaVisual = visual.transform.localScale;
    }

    void Start()
    {
        _controller = FindAnyObjectByType<PlayerController>();
        if (_controller != null) _bruxa = _controller.transform;

        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += Recomecar;
    }

    void OnDestroy()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died -= Recomecar;
    }

    void Recomecar()
    {
        _pisadoEm = float.NaN;
        _quebrouEm = float.NaN;
        Mostrar();
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        bool voltando = manager != null && manager.IsRewinding;

        // O mesmo relógio reversível dos obstáculos: é nele que se mede quando
        // quebrou, então voltar no tempo para antes da quebra a desfaz.
        if (voltando)
        {
            _relogio -= Time.deltaTime * manager.RewindSpeed;
            _pisadoEm = float.NaN;
            Mostrar();
            return;
        }

        _relogio += Time.deltaTime;

        bool quebrado = !float.IsNaN(_quebrouEm);
        if (quebrado && _relogio - _quebrouEm >= segundosAteVoltar && !BruxaPerto()) _quebrouEm = float.NaN;

        if (float.IsNaN(_quebrouEm) && float.IsNaN(_pisadoEm) && BruxaEmCima()) _pisadoEm = _relogio;

        if (!float.IsNaN(_pisadoEm) && _relogio - _pisadoEm >= segundosAteQuebrar)
        {
            _quebrouEm = _relogio;
            _pisadoEm = float.NaN;
            if (som != null) som.Play();
        }

        Mostrar();
    }

    bool BruxaEmCima()
    {
        if (_bruxa == null || _controller == null || !_controller.IsGrounded || solido == null) return false;

        // Os pés dentro do contorno do tampo e na altura dele.
        var limites = solido.bounds;
        Vector3 pe = _bruxa.position;
        return pe.x > limites.min.x - 0.3f && pe.x < limites.max.x + 0.3f &&
               pe.z > limites.min.z - 0.3f && pe.z < limites.max.z + 0.3f &&
               Mathf.Abs(pe.y - limites.max.y) < 0.6f;
    }

    bool BruxaPerto()
    {
        if (_bruxa == null || solido == null) return false;
        var limites = solido.bounds;
        limites.Expand(1.2f);
        return limites.Contains(_bruxa.position);
    }

    /// <summary>Quebrado some; pisado treme, para avisar que vai quebrar.</summary>
    void Mostrar()
    {
        bool inteiro = float.IsNaN(_quebrouEm);
        if (solido != null) solido.enabled = inteiro;
        if (visual == null) return;

        visual.SetActive(inteiro);
        if (!inteiro) return;

        float tremor = float.IsNaN(_pisadoEm) ? 0f : 0.04f * Mathf.Sin(Time.time * 70f);
        visual.transform.localScale = _escalaVisual * (1f + tremor);
    }

    protected override AmostraCristal Capture() => new AmostraCristal { QuebrouEm = _quebrouEm };

    protected override void Apply(in AmostraCristal older, in AmostraCristal newer, float t)
    {
        _quebrouEm = t < 0.5f ? older.QuebrouEm : newer.QuebrouEm;
        Mostrar();
    }
}
