using UnityEngine;

/// <summary>
/// Anel que vira a gravidade da bruxa ao ser atravessado: para o teto ou de
/// volta para o chão.
///
/// Cada portal tem um destino fixo ("teto" ou "chão"), e não alterna: passar
/// duas vezes pelo mesmo anel, ou voltar por ele no rewind, nunca deixa a
/// gravidade do lado errado. A velocidade do instante é mantida no mundo
/// (ver <see cref="PlayerController.DefinirGravidade"/>), então atravessar
/// um portal no meio de um salto só curva o salto - não tira o controle.
///
/// A cor diz o destino: violeta leva ao teto, ciano devolve ao chão.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PortalDeGravidade : MonoBehaviour
{
    [Tooltip("Ligado: a gravidade passa a puxar para o teto. Desligado: volta ao chão.")]
    [SerializeField] bool paraOTeto = true;

    [Tooltip("Anel que gira, só imagem.")]
    [SerializeField] Transform anel;

    [Tooltip("Graus por segundo do giro do anel.")]
    [SerializeField] float giro = 90f;

    [SerializeField] AudioSource som;

    void Reset()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void Awake()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
        if (som != null && som.clip == null) som.clip = SomProcedural.Inversao;
    }

    void Update()
    {
        if (anel != null) anel.Rotate(Vector3.forward, giro * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        var bruxa = other.GetComponentInParent<PlayerController>();
        if (bruxa == null || bruxa.GravidadeInvertida == paraOTeto) return;

        bruxa.DefinirGravidade(paraOTeto);
        if (som != null) som.Play();
    }
}
