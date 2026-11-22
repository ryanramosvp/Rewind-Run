using UnityEngine;

/// <summary>
/// Ponto de controle: uma pequena ampulheta no chão. Tocada, a bruxa passa a
/// renascer ali.
///
/// Só nas fases longas. Nas curtas, recomeçar do início faz parte do
/// speedrun; numa fase de vários trechos, voltar ao começo por um erro no
/// último seria repetir o que já se provou saber fazer.
///
/// A ampulheta vira e acende ao ser tocada - a mesma imagem do fim do jogo,
/// em pequeno.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PontoDeControle : MonoBehaviour
{
    [Tooltip("Parte que vira e acende. Gira em volta do próprio X ao ser ativada.")]
    [SerializeField] Transform ampulheta;

    [Tooltip("Luz que acende quando este é o ponto ativo. Opcional.")]
    [SerializeField] Renderer brilho;

    [Tooltip("Onde a bruxa renasce, relativo a este objeto.")]
    [SerializeField] Vector3 pontoDeRenascer = new Vector3(0f, 0.2f, 0f);

    [SerializeField] AudioSource som;

    float _giro = 1f;

    void Reset()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void Awake()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
        if (som != null && som.clip == null) som.clip = SomProcedural.PontoDeControle;
    }

    void OnTriggerEnter(Collider other)
    {
        var bruxa = other.GetComponentInParent<PlayerController>();
        var respawn = LevelRespawn.Instance;
        if (bruxa == null || respawn == null) return;

        var olhar = Quaternion.Euler(0f, bruxa.transform.eulerAngles.y, 0f);
        if (!respawn.DefinirPontoDeControle(this, transform.TransformPoint(pontoDeRenascer), olhar)) return;

        _giro = 0f;
        if (som != null) som.Play();
    }

    void Update()
    {
        var respawn = LevelRespawn.Instance;
        bool ativo = respawn != null && respawn.EhPontoAtual(this);
        if (brilho != null) brilho.enabled = ativo;

        if (ampulheta == null || _giro >= 1f) return;

        // Meia volta em 0,6 s, assentando no fim.
        _giro = Mathf.Min(1f, _giro + Time.deltaTime / 0.6f);
        float suave = 1f - Mathf.Pow(1f - _giro, 3f);
        ampulheta.localRotation = Quaternion.Euler(180f * suave, 0f, 0f);
    }
}
