using UnityEngine;

/// <summary>
/// Portal que leva a bruxa a outro lugar da cena: da chegada da corrida ao
/// jardim do epílogo.
///
/// Uma travessia de verdade, e não um carregamento de cena: a música segue
/// sem corte, e o epílogo não precisa de outra cena só para alguns passos.
/// A história do rewind é apagada do outro lado - voltar no tempo para
/// dentro da corrida, depois de ela terminar, não faria sentido.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PortalDeTravessia : MonoBehaviour
{
    [Tooltip("Onde a bruxa aparece do outro lado.")]
    [SerializeField] Transform destino;

    [Tooltip("Anel do portal, que gira.")]
    [SerializeField] Transform anel;

    [SerializeField] AudioSource som;

    bool _usado;

    void Awake()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void Update()
    {
        if (anel != null) anel.Rotate(Vector3.forward, 60f * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_usado || destino == null) return;
        var bruxa = other.GetComponentInParent<PlayerController>();
        if (bruxa == null) return;
        _usado = true;

        if (som != null) som.Play();

        var corpo = bruxa.GetComponent<CharacterController>();
        if (corpo != null) corpo.enabled = false;
        bruxa.transform.SetPositionAndRotation(destino.position, destino.rotation);
        if (corpo != null) corpo.enabled = true;
        bruxa.SoltarDoPlano();
        bruxa.ResetToIdle();

        var rewind = RewindManager.Instance;
        if (rewind != null) rewind.ResetHistory();

        var camera = FindAnyObjectByType<OrbitCamera>();
        if (camera != null) camera.LiberarEnquadramento();
    }
}
