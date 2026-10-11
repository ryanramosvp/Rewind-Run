using UnityEngine;

/// <summary>
/// Pulsa o brilho da lava.
///
/// Uma superfície laranja parada lê como placeholder; é o movimento que faz o
/// olho aceitar como lava. Usa MaterialPropertyBlock para não instanciar uma
/// cópia do material a cada Play.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class LavaSurface : MonoBehaviour
{
    [Tooltip("Cor da emissão. O padrão vem da faixa laranja do atlas do KayKit.")]
    [SerializeField] Color emissionColor = new Color(0.965f, 0.549f, 0.122f);

    [SerializeField, Min(0f)] float minIntensity = 0.5f;
    [SerializeField, Min(0f)] float maxIntensity = 1.5f;

    [Tooltip("Ciclos por segundo. Devagar parece massa derretida; rápido parece pisca-pisca.")]
    [SerializeField, Min(0.01f)] float pulsesPerSecond = 0.25f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    Renderer _renderer;
    MaterialPropertyBlock _block;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float wave = (Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, wave);

        _renderer.GetPropertyBlock(_block);
        _block.SetColor(EmissionColorId, emissionColor * intensity);
        _renderer.SetPropertyBlock(_block);
    }
}
