using UnityEngine;

/// <summary>As etapas do ciclo da onda, na ordem em que acontecem.</summary>
public enum FaseDoColapso
{
    Repouso,
    Preparacao,
    Expansao,
    Fragmentado,
    Retorno,
    Reconstrucao,
    Encerrado,
}

/// <summary>
/// O coração da última fase: a ampulheta solta, de tempos em tempos, uma onda
/// que desmonta o cenário ao passar e o remonta ao voltar.
///
/// Tudo sai de um número só, o tempo do ciclo. A onda, a ampulheta e cada
/// fragmento calculam o próprio estado a partir dele e da distância até o
/// centro - nenhum guarda posição própria nem usa física. É isso que faz o
/// rewind funcionar sem esforço: este componente grava o tempo do ciclo na
/// história, e voltar no tempo devolve a onda, a ampulheta e cada pedaço ao
/// lugar exato daquele instante, sem duplicar nada nem teletransportar ninguém.
///
/// Por isso também a reconstrução do ciclo e o rewind não brigam: a
/// reconstrução é só uma parte da conta (o tempo andando para a frente), e o
/// rewind é o tempo andando para trás na mesma conta.
///
/// Só o cenário é afetado. A bruxa corre, pula e cai como sempre; os
/// fragmentos é que a carregam quando ela está em cima (ver
/// <see cref="FragmentoTemporal"/>).
///
/// Roda antes de todos os fragmentos e da bruxa, para eles lerem o tempo
/// deste quadro.
/// </summary>
[DefaultExecutionOrder(-260)]
public class CicloDoColapso : RewindableBehaviour<float>
{
    public static CicloDoColapso Instancia { get; private set; }

    [Header("Tempos do ciclo (segundos)")]
    [Tooltip("Cenário inteiro e parado, a ampulheta acumulando energia.")]
    [SerializeField, Min(0f)] float repouso = 7f;

    [Tooltip("A ampulheta acende, as rachaduras brilham e o som anuncia a onda.")]
    [SerializeField, Min(0.1f)] float preparacao = 3f;

    [Tooltip("Quanto o cenário fica desmontado depois que a onda chega ao limite, antes de voltar.")]
    [SerializeField, Min(0f)] float fragmentado = 6f;

    [Tooltip("Pausa no fim, com tudo remontado, antes de o ciclo recomeçar.")]
    [SerializeField, Min(0f)] float pausaFinal = 1f;

    [Header("Onda")]
    [Tooltip("Metros por segundo da onda saindo da ampulheta.")]
    [SerializeField, Min(1f)] float velocidadeDeExpansao = 16f;

    [Tooltip("Metros por segundo da onda voltando para a ampulheta.")]
    [SerializeField, Min(1f)] float velocidadeDeRetorno = 24f;

    [Tooltip("Até onde a onda vai: o limite da fase.")]
    [SerializeField, Min(5f)] float raioMaximo = 100f;

    [Tooltip("Força do brilho da onda, das rachaduras e da ampulheta.")]
    [SerializeField, Range(0f, 2f)] float intensidade = 1f;

    [Header("Fragmentos")]
    [Tooltip("Segundos que cada peça leva para se soltar por inteiro depois que a onda passa por ela.")]
    [SerializeField, Min(0.05f)] float tempoDeFragmentacao = 1.4f;

    [Tooltip("Segundos que cada peça leva para voltar ao lugar depois que a onda de volta passa.")]
    [SerializeField, Min(0.05f)] float tempoDeReconstrucao = 1.2f;

    [Header("Som")]
    [SerializeField] AudioSource som;

    [Tooltip("Defasagem inicial, em segundos dentro do ciclo.")]
    [SerializeField, Min(0f)] float inicio;

    float _tempo;           // tempo total do ciclo desde o início, sem dar a volta
    float _encerrandoDesde = -1f;
    FaseDoColapso _faseAnterior;

    /// <summary>Velocidade das peças em relação ao tempo de soltura: >1 é mais rápido.</summary>
    public float Intensidade => intensidade;
    public float RaioMaximo => raioMaximo;
    public float VelocidadeDeExpansao => velocidadeDeExpansao;

    // Marcos dentro do ciclo.
    float InicioDaExpansao => repouso + preparacao;
    float FimDaExpansao => InicioDaExpansao + raioMaximo / velocidadeDeExpansao;
    float InicioDoRetorno => FimDaExpansao + fragmentado;
    float FimDoRetorno => InicioDoRetorno + raioMaximo / velocidadeDeRetorno;
    public float Periodo => FimDoRetorno + tempoDeReconstrucao + pausaFinal;

    /// <summary>Tempo dentro do ciclo atual, de 0 ao período.</summary>
    public float TempoNoCiclo => Mathf.Repeat(_tempo, Periodo);

    /// <summary>Tempo total, para balanços que precisam ser os mesmos no rewind.</summary>
    public float TempoTotal => _tempo;

    public bool Encerrado => _encerrandoDesde >= 0f;

    void Awake()
    {
        Instancia = this;
        _tempo = inicio;
        _faseAnterior = Fase;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        if (Encerrado)
        {
            _encerrandoDesde += Time.deltaTime;
            return;
        }

        _tempo += Time.deltaTime;

        // Sons só andando para a frente: no rewind a fase volta em silêncio
        // (o som do rewind já está tocando).
        var fase = Fase;
        if (fase != _faseAnterior && som != null)
        {
            if (fase == FaseDoColapso.Preparacao) som.PlayOneShot(SomProcedural.Carga, 0.9f * intensidade);
            else if (fase == FaseDoColapso.Expansao) som.PlayOneShot(SomProcedural.Onda, intensidade);
            else if (fase == FaseDoColapso.Retorno) som.PlayOneShot(SomProcedural.Retorno, intensidade);
        }

        _faseAnterior = fase;
    }

    /// <summary>
    /// Para o ciclo de vez: a bruxa chegou ao núcleo. A onda some e tudo se
    /// remonta em dois segundos, e fica assim - é o fim do colapso.
    /// </summary>
    public void Encerrar()
    {
        if (Encerrado) return;
        _encerrandoDesde = 0f;
        if (som != null) som.PlayOneShot(SomProcedural.Retorno, intensidade);
    }

    /// <summary>Quanto do encerramento falta: 1 no instante em que começa, 0 dois segundos depois.</summary>
    float Restante => Encerrado ? 1f - Mathf.Clamp01(_encerrandoDesde / 2f) : 1f;

    public FaseDoColapso Fase
    {
        get
        {
            if (Encerrado) return FaseDoColapso.Encerrado;
            float t = TempoNoCiclo;
            if (t < repouso) return FaseDoColapso.Repouso;
            if (t < InicioDaExpansao) return FaseDoColapso.Preparacao;
            if (t < FimDaExpansao) return FaseDoColapso.Expansao;
            if (t < InicioDoRetorno) return FaseDoColapso.Fragmentado;
            if (t < FimDoRetorno) return FaseDoColapso.Retorno;
            return FaseDoColapso.Reconstrucao;
        }
    }

    /// <summary>Raio da onda agora: cresce, para no limite e volta. Zero com o cenário inteiro.</summary>
    public float RaioDaOnda
    {
        get
        {
            float t = TempoNoCiclo;
            float r;
            if (t < InicioDaExpansao) r = 0f;
            else if (t < FimDaExpansao) r = (t - InicioDaExpansao) * velocidadeDeExpansao;
            else if (t < InicioDoRetorno) r = raioMaximo;
            else if (t < FimDoRetorno) r = raioMaximo - (t - InicioDoRetorno) * velocidadeDeRetorno;
            else r = 0f;
            return r * Restante;
        }
    }

    /// <summary>De 0 a 1 durante a preparação; 1 enquanto a onda existe; cai a 0 no fim.</summary>
    public float Carga
    {
        get
        {
            float t = TempoNoCiclo;
            float c;
            if (t < repouso) c = 0.12f * Mathf.Clamp01(t / Mathf.Max(0.01f, repouso));   // acúmulo sutil
            else if (t < InicioDaExpansao) c = Mathf.Lerp(0.12f, 1f, (t - repouso) / preparacao);
            else if (t < FimDoRetorno) c = 1f;
            else c = 1f - Mathf.Clamp01((t - FimDoRetorno) / Mathf.Max(0.01f, tempoDeReconstrucao));
            return c * Restante;
        }
    }

    /// <summary>
    /// Quanto uma peça a <paramref name="distancia"/> metros do centro está
    /// solta agora: 0 inteira no lugar, 1 solta por completo. Sobe quando a
    /// onda passa por ela e desce quando a onda de volta passa.
    /// </summary>
    public float Desmontagem(float distancia)
    {
        if (Encerrado) return Mathf.Min(DesmontagemNoCiclo(distancia, TempoNoCiclo), Restante);
        return DesmontagemNoCiclo(distancia, TempoNoCiclo);
    }

    float DesmontagemNoCiclo(float distancia, float t)
    {
        distancia = Mathf.Clamp(distancia, 0f, raioMaximo);
        float chega = InicioDaExpansao + distancia / velocidadeDeExpansao;
        float volta = InicioDoRetorno + (raioMaximo - distancia) / velocidadeDeRetorno;

        float solta = Suave((t - chega) / tempoDeFragmentacao);
        float remonta = Suave((t - volta) / tempoDeReconstrucao);
        return solta * (1f - remonta);
    }

    /// <summary>Brilho das rachaduras de uma peça: acende na preparação e na passagem da onda.</summary>
    public float Rachadura(float distancia)
    {
        float frente = RaioDaOnda;
        float naFrente = frente > 0f ? Mathf.Clamp01(1f - Mathf.Abs(frente - distancia) / 6f) : 0f;
        float preparo = Fase == FaseDoColapso.Preparacao ? Carga : 0f;
        return Mathf.Max(naFrente, preparo * 0.7f) * intensidade;
    }

    /// <summary>
    /// Distância de um ponto ao centro da ampulheta. Em três dimensões, e não
    /// só no chão: a onda é uma esfera, e uma peça no alto é alcançada depois
    /// de uma embaixo dela.
    /// </summary>
    public float DistanciaAte(Vector3 ponto) => Vector3.Distance(ponto, transform.position);

    static float Suave(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    protected override float Capture() => _tempo;

    protected override void Apply(in float older, in float newer, float t) => _tempo = Mathf.Lerp(older, newer, t);
}
