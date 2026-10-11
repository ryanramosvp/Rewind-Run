using UnityEngine;

/// <summary>
/// Base para qualquer componente que participa do rewind.
/// Cuida do buffer circular de amostras; a subclasse só diz o que gravar
/// (<see cref="Capture"/>) e como voltar a esse estado (<see cref="Apply"/>).
/// </summary>
/// <typeparam name="TSample">
/// Struct com o estado do objeto. Struct (e não classe) para o buffer ficar
/// num bloco contíguo de memória e não gerar lixo para o GC a cada FixedUpdate.
/// </typeparam>
public abstract class RewindableBehaviour<TSample> : MonoBehaviour, IRewindable
    where TSample : struct
{
    TSample[] _samples;
    int _head;   // índice da próxima escrita
    int _count;  // quantas amostras válidas existem no buffer

    /// <summary>Quantos passos de história este objeto tem guardados.</summary>
    public int SampleCount => _count;

    /// <summary>Lê o estado atual do objeto.</summary>
    protected abstract TSample Capture();

    /// <summary>
    /// Devolve o objeto a um estado entre duas amostras.
    /// <paramref name="t"/> = 0 é <paramref name="older"/>, 1 é <paramref name="newer"/>.
    /// </summary>
    protected abstract void Apply(in TSample older, in TSample newer, float t);

    protected virtual void OnEnable() => RewindManager.Register(this);

    protected virtual void OnDisable() => RewindManager.Unregister(this);

    public void RecordSample()
    {
        EnsureCapacity();

        _samples[_head] = Capture();
        _head = (_head + 1) % _samples.Length;
        if (_count < _samples.Length) _count++;
    }

    public void ApplySample(float stepsBack)
    {
        if (_samples == null || _count == 0) return;

        // A amostra em stepsBack fica entre duas gravações; a de índice menor é
        // a mais nova, porque a contagem anda para trás no tempo.
        float clamped = Mathf.Clamp(stepsBack, 0f, _count - 1);
        int newerIndex = Mathf.FloorToInt(clamped);
        int olderIndex = Mathf.Min(newerIndex + 1, _count - 1);
        float fraction = clamped - newerIndex;

        Apply(SampleAt(olderIndex), SampleAt(newerIndex), 1f - fraction);
    }

    public void DiscardNewer(int stepsBack)
    {
        if (_samples == null || _count == 0) return;

        int drop = Mathf.Clamp(stepsBack, 0, _count);
        _head -= drop;
        if (_head < 0) _head += _samples.Length;
        _count -= drop;
    }

    public void ClearHistory()
    {
        _head = 0;
        _count = 0;
    }

    public virtual void OnRewindBegin() { }

    public virtual void OnRewindEnd() { }

    /// <summary>Amostra a <paramref name="stepsBack"/> passos do presente (0 = a mais recente).</summary>
    protected TSample SampleAt(int stepsBack)
    {
        int index = (_head - 1 - stepsBack) % _samples.Length;
        if (index < 0) index += _samples.Length;
        return _samples[index];
    }

    void EnsureCapacity()
    {
        int capacity = Mathf.Max(1, RewindManager.SampleCapacity);
        if (_samples != null && _samples.Length == capacity) return;

        // A capacidade só muda quando o manager acorda ou quando historySeconds
        // é editado em play mode; nos dois casos recomeçar a história é o certo.
        _samples = new TSample[capacity];
        ClearHistory();
    }
}
