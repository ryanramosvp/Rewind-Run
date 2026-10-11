/// <summary>
/// Contrato entre o <see cref="RewindManager"/> e qualquer objeto que participa
/// da linha do tempo. O manager não sabe o que cada objeto guarda - só manda
/// gravar, aplicar e descartar amostras.
/// </summary>
public interface IRewindable
{
    /// <summary>Grava o estado atual como a amostra mais recente.</summary>
    void RecordSample();

    /// <summary>
    /// Aplica o estado de <paramref name="stepsBack"/> passos atrás.
    /// Aceita valor fracionário: o objeto interpola entre as duas amostras
    /// vizinhas para o rewind não ficar travando de amostra em amostra.
    /// </summary>
    void ApplySample(float stepsBack);

    /// <summary>
    /// Joga fora as <paramref name="stepsBack"/> amostras mais recentes.
    /// O que o rewind desfez deixa de existir na história do objeto.
    /// </summary>
    void DiscardNewer(int stepsBack);

    /// <summary>Esquece a história inteira (troca de fase, respawn, checkpoint).</summary>
    void ClearHistory();

    void OnRewindBegin();

    void OnRewindEnd();
}
