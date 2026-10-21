using UnityEngine;

/// <summary>
/// Desmancha o cenário do menu aos poucos e o remonta quando o tempo volta.
///
/// Cada peça tem um caminho de fuga fixo - para fora do centro, um pouco para
/// cima, girando devagar - e a posição dela é função de um único número, o
/// progresso de 0 (inteiro) a 1 (desfeito). Quem dirige esse número é o
/// <see cref="MenuDiorama"/>.
///
/// Ser função pura do progresso é o que faz o rewind sair exato e de graça:
/// rodar o número para trás refaz o mesmo caminho ao contrário, peça por peça,
/// sem gravar nada. É o mesmo princípio das hélices da fase.
///
/// As peças não saem juntas. As mais altas se soltam primeiro e a base por
/// último, então o vulcão se desfaz de cima para baixo - e, no rewind, se
/// reconstrói de baixo para cima, como uma obra sendo erguida.
/// </summary>
public class DioramaDissolve : MonoBehaviour
{
    [Tooltip("Filhos com estes nomes ficam parados. O alvo da câmera precisa " +
             "ficar, senão a órbita sairia correndo atrás dele.")]
    [SerializeField] string[] keepStill = { "FocoDaOrbita" };

    [Tooltip("Fração da linha do tempo que cada peça leva para se soltar. O " +
             "restante vira escalonamento entre a primeira e a última.")]
    [SerializeField, Range(0.1f, 0.9f)] float pieceSpan = 0.45f;

    [Header("Fuga de cada peça")]
    [SerializeField, Min(0f)] float minDrift = 5f;
    [SerializeField, Min(0f)] float maxDrift = 13f;
    [SerializeField, Min(0f)] float minLift = 3f;
    [SerializeField, Min(0f)] float maxLift = 9f;
    [SerializeField, Range(0f, 360f)] float maxTumble = 140f;

    [Tooltip("Semente do sorteio. A mesma semente dá o mesmo desmanche em toda " +
             "volta do ciclo - é sempre o mesmo instante sendo rebobinado.")]
    [SerializeField] int seed = 7;

    struct Piece
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 move;
        public Vector3 axis;
        public float angle;
        public float delay;
    }

    Piece[] _pieces = new Piece[0];

    void Awake()
    {
        var lista = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (System.Array.IndexOf(keepStill, child.name) >= 0) continue;

            // A lava é o chão do mundo, não parte da obra: se ela flutuasse
            // junto, o menu perderia a única referência fixa.
            if (child.GetComponentInChildren<LavaSurface>() != null) continue;
            if (child.GetComponentInChildren<KillZone>() != null) continue;

            lista.Add(child);
        }

        if (lista.Count == 0) return;

        float minY = float.MaxValue, maxY = float.MinValue;
        Vector3 centro = Vector3.zero;
        foreach (var t in lista)
        {
            centro += t.localPosition;
            minY = Mathf.Min(minY, t.localPosition.y);
            maxY = Mathf.Max(maxY, t.localPosition.y);
        }
        centro /= lista.Count;
        float faixaY = Mathf.Max(0.001f, maxY - minY);

        var rng = new System.Random(seed);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        _pieces = new Piece[lista.Count];
        float ultimoInicio = 1f - pieceSpan;

        for (int i = 0; i < lista.Count; i++)
        {
            var t = lista[i];

            Vector3 fora = t.localPosition - centro;
            fora.y = 0f;

            // As peças da torre estão no próprio centro, onde "para fora" não
            // tem direção. Sorteia uma, senão subiriam empilhadas e encaixadas.
            if (fora.sqrMagnitude < 0.25f)
            {
                float ang = R(0f, Mathf.PI * 2f);
                fora = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            }
            fora.Normalize();

            // Um desvio lateral pequeno, para as peças de uma mesma fileira
            // não saírem em paralelo como uma formação.
            fora = Quaternion.Euler(0f, R(-25f, 25f), 0f) * fora;

            float altura01 = (t.localPosition.y - minY) / faixaY;
            float atraso = (1f - altura01) * ultimoInicio + R(-0.06f, 0.06f);

            _pieces[i] = new Piece
            {
                t = t,
                pos = t.localPosition,
                rot = t.localRotation,
                move = fora * R(minDrift, maxDrift) + Vector3.up * R(minLift, maxLift),
                axis = new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)).normalized,
                angle = R(maxTumble * 0.35f, maxTumble) * (rng.NextDouble() < 0.5 ? -1f : 1f),
                delay = Mathf.Clamp(atraso, 0f, ultimoInicio),
            };
        }
    }

    /// <summary>Põe o cenário no ponto <paramref name="u"/>: 0 inteiro, 1 desfeito.</summary>
    public void SetProgress(float u)
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            var p = _pieces[i];
            if (p.t == null) continue;

            float k = Mathf.Clamp01((u - p.delay) / pieceSpan);
            k = k * k * (3f - 2f * k);          // sai e chega devagar, sem tranco

            p.t.SetLocalPositionAndRotation(
                p.pos + p.move * k,
                p.rot * Quaternion.AngleAxis(p.angle * k, p.axis));
        }
    }
}
