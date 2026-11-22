using UnityEngine;

/// <summary>
/// Área que mostra uma dica no <see cref="PainelDeAvisos"/> quando a bruxa
/// entra. Fica posta logo antes do primeiro uso de cada mecânica nova.
///
/// Mostra uma vez por fase: na segunda passagem - depois de uma morte, com o
/// ponto de controle ali perto - o jogador já leu.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AvisoNaFase : MonoBehaviour
{
    [TextArea] [SerializeField] string textoTeclado;
    [TextArea] [SerializeField] string textoControle;
    [SerializeField, Min(0.5f)] float segundos = 4.5f;

    bool _mostrado;

    void Awake()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_mostrado || other.GetComponentInParent<PlayerController>() == null) return;

        _mostrado = true;
        PainelDeAvisos.Mostrar(textoTeclado, textoControle, segundos);
    }
}
