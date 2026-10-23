using UnityEngine;

/// <summary>
/// Gêiser de lava: um jato que sobe em ciclos e mata enquanto está de pé.
///
/// O ciclo tem quatro estados - repouso, aviso, erupção e resfriamento - e o
/// estado é função pura de um relógio, como a hélice e a plataforma móvel. No
/// rewind o relógio anda para trás, então o gêiser volta exatamente ao estado
/// daquele instante: um jato que estava apagado volta apagado, e nunca fica
/// letal num momento em que deveria estar desligado.
///
/// O perigo acompanha o que se vê. O gatilho letal tem a largura do jato e a
/// altura atual dele, e só existe na erupção e no resfriamento; no repouso e no
/// aviso ele está desligado. Um jato que mata acima de onde ele aparece seria
/// injusto, e o aviso existe justamente para ser um momento seguro de ler.
/// </summary>
[DefaultExecutionOrder(-200)]
public class LavaGeyser : MonoBehaviour
{
    public enum Stage { Repouso, Aviso, Erupcao, Resfriamento }

    [Header("Ciclo, em segundos")]
    [SerializeField, Min(0f)] float idleSeconds = 3.2f;
    [SerializeField, Min(0f)] float warningSeconds = 0.8f;
    [SerializeField, Min(0.1f)] float eruptSeconds = 1.1f;
    [SerializeField, Min(0.1f)] float coolSeconds = 0.7f;

    [Tooltip("Quanto da erupção o jato leva para chegar ao topo. Rápido de " +
             "propósito: o aviso já avisou, a subida é o castigo.")]
    [SerializeField, Min(0.01f)] float riseSeconds = 0.15f;

    [Tooltip("Defasagem inicial, de 0 a 1 do ciclo, para dois gêiseres não " +
             "entrarem em erupção juntos.")]
    [SerializeField, Range(0f, 1f)] float phase;

    [Header("Jato")]
    [SerializeField, Min(0.5f)] float jetHeight = 8f;
    [SerializeField, Min(0.1f)] float jetRadius = 0.9f;

    [Tooltip("Abaixo desta altura o jato não mata: é o fio que sobra no fim do " +
             "resfriamento, baixo demais para atingir alguém de pé.")]
    [SerializeField, Min(0f)] float lethalMinHeight = 0.4f;

    [Header("Peças")]
    [Tooltip("Coluna do jato, com o pivô na base; a escala Y vira a altura.")]
    [SerializeField] Transform jet;

    [Tooltip("Gatilho letal. Acompanha a altura do jato.")]
    [SerializeField] BoxCollider hazard;

    [Tooltip("Disco aceso no fundo da boca. Pisca forte no aviso.")]
    [SerializeField] Renderer vent;

    [Tooltip("Cúpula que borbulha no aviso, para o aviso ser lido de longe.")]
    [SerializeField] Transform dome;

    [SerializeField] AudioSource audioSource;

    [Header("Aviso")]
    [SerializeField] Color ventIdle = new Color(0.35f, 0.1f, 0.02f);
    [SerializeField] Color ventWarning = new Color(3f, 1.2f, 0.25f);

    public Stage Current { get; private set; }

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static AudioClip s_Rumble;
    static AudioClip s_Burst;

    MaterialPropertyBlock _block;
    float _time;
    Stage _previous;
    int _lastKillFrame = -1;

    float CycleSeconds => idleSeconds + warningSeconds + eruptSeconds + coolSeconds;

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        _time = phase * CycleSeconds;

        if (hazard != null) hazard.isTrigger = true;
        if (audioSource != null) audioSource.playOnAwake = false;

        EnsureClips();
        var stage = Evaluate(_time, out float height, out float warning);
        Apply(stage, height, warning);
        _previous = Current;
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        bool rewinding = manager != null && manager.IsRewinding;

        if (rewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        var stage = Evaluate(_time, out float height, out float warning);
        Apply(stage, height, warning);

        // Som só andando para frente: o ronco tocado de trás para frente no
        // rewind seria barulho sem significado.
        if (!rewinding && Current != _previous && audioSource != null)
        {
            if (Current == Stage.Aviso && s_Rumble != null) audioSource.PlayOneShot(s_Rumble, 0.8f);
            if (Current == Stage.Erupcao && s_Burst != null) audioSource.PlayOneShot(s_Burst, 1f);
        }

        _previous = Current;
    }

    /// <summary>Estado, altura do jato e intensidade do aviso num instante do ciclo.</summary>
    Stage Evaluate(float time, out float height, out float warning)
    {
        float t = Mathf.Repeat(time, CycleSeconds);
        height = 0f;
        warning = 0f;

        if (t < idleSeconds) return Stage.Repouso;
        t -= idleSeconds;

        if (t < warningSeconds)
        {
            // Pisca acelerando: o ritmo diz quanto falta.
            float k = t / Mathf.Max(0.01f, warningSeconds);
            warning = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(14f, 40f, k));
            return Stage.Aviso;
        }
        t -= warningSeconds;

        if (t < eruptSeconds)
        {
            height = jetHeight * Mathf.Clamp01(t / riseSeconds);
            return Stage.Erupcao;
        }
        t -= eruptSeconds;

        float fall = 1f - t / coolSeconds;
        height = jetHeight * fall * fall;
        return Stage.Resfriamento;
    }

    void Apply(Stage stage, float height, float warning)
    {
        Current = stage;

        if (jet != null)
        {
            jet.gameObject.SetActive(height > 0.02f);
            jet.localScale = new Vector3(1f, Mathf.Max(0.02f, height), 1f);
        }

        if (hazard != null)
        {
            bool lethal = (stage == Stage.Erupcao || stage == Stage.Resfriamento) && height >= lethalMinHeight;
            hazard.enabled = lethal;
            hazard.size = new Vector3(jetRadius * 2f, Mathf.Max(0.1f, height), jetRadius * 2f);
            hazard.center = new Vector3(0f, height * 0.5f, 0f);
        }

        if (dome != null)
        {
            bool bubbling = stage == Stage.Aviso;
            dome.gameObject.SetActive(bubbling);
            if (bubbling) dome.localScale = new Vector3(1f, 0.4f + 0.6f * warning, 1f);
        }

        if (vent != null)
        {
            var color = stage switch
            {
                Stage.Aviso => Color.Lerp(ventIdle, ventWarning, warning),
                Stage.Erupcao => ventWarning,
                Stage.Resfriamento => Color.Lerp(ventIdle, ventWarning, height / jetHeight),
                _ => ventIdle,
            };

            vent.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, color);
            vent.SetPropertyBlock(_block);
        }
    }

    void OnTriggerEnter(Collider other) => TryKill(other);

    // O jato cresce por cima de quem já estava parado em cima da boca: nesse
    // caso não há entrada, só permanência.
    void OnTriggerStay(Collider other) => TryKill(other);

    void TryKill(Collider other)
    {
        if (_lastKillFrame == Time.frameCount) return;

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        if (other.GetComponentInParent<PlayerController>() == null) return;

        var respawn = LevelRespawn.Instance;
        if (respawn == null) return;

        // Uma morte por quadro, mesmo que vários colliders da bruxa entrem no
        // jato ao mesmo tempo.
        _lastKillFrame = Time.frameCount;
        respawn.RestartLevel();
    }

    /// <summary>
    /// Ronco e estouro, gerados uma vez por sessão.
    ///
    /// O projeto não tem nenhum som grave de lava; gerar é melhor que tocar um
    /// som de interface fora de lugar. Ruído marrom (acumulado) dá o ronco
    /// grave do aviso, e ruído branco decaindo dá o chiado da erupção.
    /// </summary>
    static void EnsureClips()
    {
        if (s_Rumble != null && s_Burst != null) return;

        const int rate = 22050;
        var random = new System.Random(5);

        var rumble = new float[(int)(rate * 0.9f)];
        float brown = 0f;
        for (int i = 0; i < rumble.Length; i++)
        {
            brown = Mathf.Clamp(brown + ((float)random.NextDouble() * 2f - 1f) * 0.06f, -1f, 1f);
            float t = (float)i / rumble.Length;
            float envelope = Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 40f));
            rumble[i] = brown * envelope * 0.9f;
        }

        var burst = new float[(int)(rate * 0.8f)];
        float low = 0f;
        for (int i = 0; i < burst.Length; i++)
        {
            float white = (float)random.NextDouble() * 2f - 1f;
            low = Mathf.Lerp(low, white, 0.25f);
            float t = (float)i / burst.Length;
            burst[i] = low * Mathf.Exp(-t * 4f) * Mathf.Min(1f, t * 40f) * 0.9f;
        }

        s_Rumble = AudioClip.Create("GeyserRonco", rumble.Length, 1, rate, false);
        s_Rumble.SetData(rumble, 0);
        s_Burst = AudioClip.Create("GeyserEstouro", burst.Length, 1, rate, false);
        s_Burst.SetData(burst, 0);
    }
}
