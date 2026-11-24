using UnityEngine;

/// <summary>
/// Plataforma dimensional que só existe logo depois que o fantasma pisa
/// nela: um contorno apagado até ele passar, sólida e acesa por alguns
/// segundos depois.
///
/// É um atalho de observação. Quem está colado no fantasma vê o caminho
/// aparecer à frente e o segue; quem está muito atrás o encontra apagado de
/// novo e toma o desvio. O tempo de passagem vem do construtor, que conhece
/// a rota; o relógio é o do fantasma, então o rewind não a reacende.
/// </summary>
public class PlataformaRevelada : MonoBehaviour
{
    [Tooltip("Instante, no relógio da corrida, em que o fantasma passa por aqui.")]
    [SerializeField] float tempoDePassagem;

    [Tooltip("Segundos que ela fica sólida depois da passagem.")]
    [SerializeField, Min(0.5f)] float duracao = 6f;

    [SerializeField] Collider solido;
    [SerializeField] Renderer imagem;

    [SerializeField, ColorUsage(true, true)] Color acesa = new Color(0.6f, 1.4f, 2.2f, 1f);
    [SerializeField] Color apagada = new Color(0.4f, 0.6f, 1f, 0.12f);

    static readonly int Base = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock _bloco;
    bool _estado = true;

    void Update()
    {
        var fantasma = FantasmaDaCorrida.Instancia;
        float t = fantasma != null ? fantasma.TempoDaCorrida : -1f;
        bool existe = t >= tempoDePassagem - 0.4f && t <= tempoDePassagem + duracao;

        // Aviso de que vai sumir: pisca no último segundo.
        bool aviso = existe && t > tempoDePassagem + duracao - 1f && Mathf.Repeat(t * 8f, 1f) < 0.5f;

        if (existe != _estado)
        {
            _estado = existe;
            if (solido != null) solido.enabled = existe;
        }

        if (imagem == null) return;
        _bloco ??= new MaterialPropertyBlock();
        _bloco.SetColor(Base, existe && !aviso ? acesa : apagada);
        imagem.SetPropertyBlock(_bloco);
    }
}
