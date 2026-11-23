using UnityEngine;

/// <summary>
/// Efeitos sonoros gerados por código: zumbido das zonas de âmbar, impulso,
/// cristal e ponto de controle.
///
/// São sons simples - senos, ruído filtrado, envelopes - e não pediam
/// arquivos. Gerá-los aqui evita depender de um pacote de sons para quatro
/// efeitos curtos, e mantém o timbre combinado entre eles: todos sobre a mesma
/// escala, todos com o mesmo tipo de cauda.
///
/// Cada som é gerado uma vez só e guardado.
/// </summary>
public static class SomProcedural
{
    const int Taxa = 44100;

    static AudioClip _zumbido, _impulso, _cristal, _ponto, _estalo;

    /// <summary>
    /// Zumbido grave e pulsante, em laço, para dentro das zonas. Lento de
    /// propósito: é o som do tempo engrossando, o oposto do chiado de fita
    /// acelerada do rewind.
    /// </summary>
    public static AudioClip Zumbido => _zumbido ??= Gerar("ZumbidoDeAmbar", 2f, t =>
    {
        // 2 s exatos e frequências que fecham ciclos inteiros nesse tempo: o
        // laço emenda sem estalo.
        float pulso = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 1f * t);
        float som = 0.55f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                    + 0.3f * Mathf.Sin(2f * Mathf.PI * 110f * t + 0.6f)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 165f * t + 1.3f);
        return som * pulso * 0.6f;
    });

    /// <summary>Sopro curto que cai de tom: o impulso no ar.</summary>
    public static AudioClip Impulso => _impulso ??= Gerar("Impulso", 0.3f, RuidoDescendo(0.3f, 0.7f));

    /// <summary>Tilintar de vidro: cristal rachando.</summary>
    public static AudioClip Cristal => _cristal ??= Gerar("Cristal", 0.6f, t =>
    {
        float env = Mathf.Exp(-t * 7f);
        return env * (0.5f * Mathf.Sin(2f * Mathf.PI * 1318.5f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 1975.5f * t)
                      + 0.2f * Mathf.Sin(2f * Mathf.PI * 2637f * t)) * 0.7f;
    });

    /// <summary>Três notas subindo (lá, dó#, mi): ponto de controle ativado.</summary>
    public static AudioClip PontoDeControle => _ponto ??= Gerar("PontoDeControle", 0.9f, t =>
    {
        float soma = 0f;
        float[] notas = { 880f, 1108.7f, 1318.5f };
        for (int i = 0; i < notas.Length; i++)
        {
            float inicio = i * 0.12f;
            if (t < inicio) continue;
            float local = t - inicio;
            soma += Mathf.Exp(-local * 5f) * Mathf.Sin(2f * Mathf.PI * notas[i] * local);
        }

        return soma * 0.35f;
    });

    /// <summary>Estalo seco: o cristal quebrando.</summary>
    public static AudioClip Estalo => _estalo ??= Gerar("Estalo", 0.35f, RuidoDescendo(0.35f, 1f));

    static AudioClip _carga, _onda, _retorno;

    /// <summary>
    /// A ampulheta carregando: um grave que sobe de tom e de volume por três
    /// segundos, com um tremor cada vez mais rápido. Anuncia a onda.
    /// </summary>
    public static AudioClip Carga => _carga ??= Gerar("CargaDaOnda", 3f, t =>
    {
        float u = t / 3f;
        float f = Mathf.Lerp(70f, 220f, u * u);
        // Fase integrada à mão: a frequência muda, e sin(2πft) daria um
        // chiado de varredura.
        float fase = 2f * Mathf.PI * (70f * t + (220f - 70f) * t * u * u / 3f);
        float tremor = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(3f, 18f, u) * t);
        return (0.6f * Mathf.Sin(fase) + 0.25f * Mathf.Sin(fase * 2.01f)) * tremor * u * 0.7f;
    });

    /// <summary>A onda saindo: um baque grave e um sopro largo que se afasta.</summary>
    public static AudioClip Onda => _onda ??= Gerar("OndaTemporal", 2.2f, Expansao(false));

    /// <summary>A onda voltando: o mesmo som ao contrário, como fita rebobinando.</summary>
    public static AudioClip Retorno => _retorno ??= Gerar("RetornoDaOnda", 2.2f, Expansao(true));

    static System.Func<float, float> Expansao(bool aoContrario)
    {
        const float duracao = 2.2f;
        var aleatorio = new System.Random(11);
        var ruido = new float[Mathf.CeilToInt(duracao * Taxa) + 1];
        float filtrado = 0f;
        for (int i = 0; i < ruido.Length; i++)
        {
            filtrado += ((float)aleatorio.NextDouble() * 2f - 1f - filtrado) * 0.08f;
            ruido[i] = filtrado;
        }

        return t =>
        {
            float u = aoContrario ? duracao - t : t;
            int i = Mathf.Clamp(Mathf.RoundToInt(u * Taxa), 0, ruido.Length - 1);
            float baque = Mathf.Exp(-u * 3f) * Mathf.Sin(2f * Mathf.PI * (48f - 10f * u) * u);
            float sopro = ruido[i] * 3f * Mathf.Exp(-u * 1.4f) * Mathf.Clamp01(u * 8f);
            return (0.7f * baque + 0.5f * sopro) * 0.9f;
        };
    }

    static System.Func<float, float> RuidoDescendo(float duracao, float volume)
    {
        // Ruído com semente fixa: o mesmo som a cada execução.
        var aleatorio = new System.Random(7);
        float filtrado = 0f;
        return t =>
        {
            float bruto = (float)aleatorio.NextDouble() * 2f - 1f;
            // Passa-baixa que fecha com o tempo: o "fuu" que escurece.
            float abertura = Mathf.Lerp(0.6f, 0.05f, t / duracao);
            filtrado += (bruto - filtrado) * abertura;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duracao)) * Mathf.Exp(-t * 4f);
            return filtrado * env * volume;
        };
    }

    static AudioClip Gerar(string nome, float segundos, System.Func<float, float> amostra)
    {
        int n = Mathf.CeilToInt(segundos * Taxa);
        var dados = new float[n];
        for (int i = 0; i < n; i++) dados[i] = Mathf.Clamp(amostra(i / (float)Taxa), -1f, 1f);

        var clip = AudioClip.Create(nome, n, 1, Taxa, false);
        clip.SetData(dados, 0);
        return clip;
    }
}
