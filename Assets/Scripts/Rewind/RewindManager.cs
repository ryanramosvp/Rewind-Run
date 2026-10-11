using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cérebro da mecânica de rewind: grava a história de todos os
/// <see cref="IRewindable"/> da cena e a reproduz de trás para frente.
///
/// A gravação acontece no FixedUpdate, então cada amostra representa sempre o
/// mesmo intervalo de tempo (Time.fixedDeltaTime) e o rewind não muda de
/// velocidade conforme o FPS do jogador.
/// </summary>
[DefaultExecutionOrder(-100)]
public class RewindManager : MonoBehaviour
{
    public static RewindManager Instance { get; private set; }

    [Header("Memória")]
    [Tooltip("Quantos segundos de história ficam guardados por objeto.")]
    [SerializeField, Min(0.5f)] float historySeconds = 6f;

    [Header("Carga")]
    [Tooltip("Quantos segundos de rewind cabem na carga cheia.")]
    [SerializeField, Min(0.1f)] float maxCharge = 3f;

    [Tooltip("Quanto de carga volta por segundo enquanto o rewind está desligado.")]
    [SerializeField, Min(0f)] float rechargePerSecond = 0.5f;

    [Tooltip("Espera antes da carga começar a voltar, para não dar rewind em sequência.")]
    [SerializeField, Min(0f)] float rechargeDelay = 0.75f;

    [Header("Reprodução")]
    [Tooltip("Multiplicador da velocidade do rewind. 1 = volta na mesma velocidade em que aconteceu.\n\n" +
             "Cuidado: este valor decide quanto passado cabe numa carga. Com carga de 3 s, " +
             "velocidade 1 desfaz 3 s de jogo e velocidade 2 desfaz 6 s.")]
    [SerializeField, Min(0.1f)] float rewindSpeed = 1f;

    // Estático porque um IRewindable pode acordar antes do manager (ordem de
    // OnEnable entre objetos da cena não é garantida).
    static readonly List<IRewindable> s_Rewindables = new();

    /// <summary>Tamanho do buffer que cada <see cref="RewindableBehaviour{T}"/> deve alocar.</summary>
    public static int SampleCapacity { get; private set; } = 300;

    public bool IsRewinding { get; private set; }

    /// <summary>
    /// Quantos passos de história são consumidos por passo de física. Quem não
    /// guarda amostras - um perigo cujo estado é função pura do tempo - usa
    /// isto para andar o próprio relógio para trás no mesmo compasso.
    /// </summary>
    public float RewindSpeed => rewindSpeed;

    /// <summary>Carga restante, de 0 a 1. É o que a HUD desenha.</summary>
    public float ChargeNormalized => maxCharge <= 0f ? 0f : _charge / maxCharge;

    /// <summary>Existe história e carga suficientes para começar um rewind?</summary>
    public bool CanRewind => _charge > 0f && _recordedSteps > 0;

    float _charge;
    float _timeSinceRewind;

    /// <summary>Passos gravados disponíveis para voltar (limite do playhead).</summary>
    int _recordedSteps;

    /// <summary>Quantos passos atrás no tempo o jogo está agora. 0 = presente.</summary>
    float _playhead;

    public static void Register(IRewindable rewindable)
    {
        if (!s_Rewindables.Contains(rewindable))
            s_Rewindables.Add(rewindable);
    }

    public static void Unregister(IRewindable rewindable) => s_Rewindables.Remove(rewindable);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Já existe um RewindManager em '{Instance.name}'. Removendo o de '{name}'.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        _charge = maxCharge;
        RecalculateCapacity();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void FixedUpdate()
    {
        if (IsRewinding) StepRewind();
        else StepRecord();
    }

    /// <summary>Liga o rewind. Ignorado se não há carga ou história.</summary>
    public void BeginRewind()
    {
        if (IsRewinding || !CanRewind) return;

        IsRewinding = true;
        _playhead = 0f;

        for (int i = 0; i < s_Rewindables.Count; i++)
            s_Rewindables[i].OnRewindBegin();
    }

    /// <summary>
    /// Desliga o rewind e corta da história tudo que foi desfeito - daqui para
    /// frente aquele trecho da linha do tempo não aconteceu.
    /// </summary>
    public void EndRewind()
    {
        if (!IsRewinding) return;

        IsRewinding = false;

        int consumed = Mathf.Clamp(Mathf.RoundToInt(_playhead), 0, _recordedSteps);
        for (int i = 0; i < s_Rewindables.Count; i++)
        {
            s_Rewindables[i].DiscardNewer(consumed);
            s_Rewindables[i].OnRewindEnd();
        }

        _recordedSteps -= consumed;
        _playhead = 0f;
        _timeSinceRewind = 0f;
    }

    /// <summary>Apaga a história de todo mundo (checkpoint, respawn, início de fase).</summary>
    public void ResetHistory()
    {
        EndRewind();

        for (int i = 0; i < s_Rewindables.Count; i++)
            s_Rewindables[i].ClearHistory();

        _recordedSteps = 0;
        _playhead = 0f;
    }

    /// <summary>Devolve a carga cheia (pickup de recarga, checkpoint).</summary>
    public void RefillCharge() => _charge = maxCharge;

    void StepRecord()
    {
        for (int i = 0; i < s_Rewindables.Count; i++)
            s_Rewindables[i].RecordSample();

        // O buffer é circular: passado o limite, cada gravação nova come a mais antiga.
        _recordedSteps = Mathf.Min(_recordedSteps + 1, SampleCapacity - 1);

        _timeSinceRewind += Time.fixedDeltaTime;
        if (_timeSinceRewind >= rechargeDelay)
            _charge = Mathf.Min(maxCharge, _charge + rechargePerSecond * Time.fixedDeltaTime);
    }

    void StepRewind()
    {
        _charge = Mathf.Max(0f, _charge - Time.fixedDeltaTime);
        _playhead += rewindSpeed;

        // Aplica o último estado alcançado antes de desligar, senão o rewind
        // pararia um passo antes do ponto que o jogador viu.
        bool exhausted = _charge <= 0f || _playhead >= _recordedSteps;
        _playhead = Mathf.Min(_playhead, _recordedSteps);

        for (int i = 0; i < s_Rewindables.Count; i++)
            s_Rewindables[i].ApplySample(_playhead);

        if (exhausted) EndRewind();
    }

    void RecalculateCapacity()
    {
        int capacity = Mathf.CeilToInt(historySeconds / Time.fixedDeltaTime) + 1;
        if (capacity == SampleCapacity) return;

        SampleCapacity = capacity;
        ResetHistory();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && Instance == this) RecalculateCapacity();
    }
#endif
}
