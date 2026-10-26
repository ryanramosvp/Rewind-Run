using UnityEngine;

/// <summary>
/// Rank da corrida, do mais rápido (S) ao mais lento (F).
///
/// A ordem do enum é a ordem da escada: S vale 0, e cada passo para baixo é um
/// rank pior. É isso que deixa o <see cref="GameSession"/> usar o rank como
/// índice direto na tabela de tempos limite.
/// </summary>
public enum Rank
{
    S,
    A,
    B,
    C,
    D,
    F,
}

public static class Ranks
{
    /// <summary>Quantos ranks têm tempo limite. O F não tem: é tudo acima do D.</summary>
    public const int Timed = 5;

    /// <summary>A tabela tem um limite para cada rank com tempo (S a D)?</summary>
    public static bool IsTable(float[] limits) => limits != null && limits.Length >= Timed;

    /// <summary>
    /// O rank de um tempo numa tabela de limites (S, A, B, C, D, crescendo).
    /// Acima do último limite, ou sem tabela, é F.
    /// </summary>
    public static Rank For(float seconds, float[] limits)
    {
        for (int i = 0; limits != null && i < Timed && i < limits.Length; i++)
        {
            if (seconds <= limits[i]) return (Rank)i;
        }

        return Rank.F;
    }

    /// <summary>
    /// Cor de cada rank.
    ///
    /// Vai do ouro ao vermelho passando por cores que se distinguem de relance,
    /// e não por tons de uma cor só: na escada da tela final o olho precisa
    /// achar o próprio rank entre seis sem ler as letras.
    /// </summary>
    public static Color ColorOf(Rank rank) => rank switch
    {
        Rank.S => new Color(1f, 0.82f, 0.3f),
        Rank.A => new Color(0.42f, 0.86f, 0.52f),
        Rank.B => new Color(0.42f, 0.7f, 1f),
        Rank.C => new Color(0.82f, 0.84f, 0.88f),
        Rank.D => new Color(0.86f, 0.55f, 0.3f),
        _ => new Color(0.88f, 0.28f, 0.24f),
    };
}
