using UnityEngine;

/// <summary>
/// Região que define de onde a câmera olha.
///
/// É o que torna possível uma fase aberta com câmera fixa. Sem zonas, um
/// trecho que corre num eixo diferente do ângulo global apareceria de lado ou
/// de costas - e a alternativa seria devolver a câmera ao mouse, que é
/// justamente o que queremos evitar num platformer de precisão.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CameraZone : MonoBehaviour
{
    [Tooltip("Direção de onde a câmera olha nesta região, em graus.")]
    [SerializeField] float yaw;

    [Tooltip("Quanto a câmera olha de cima. Mais alto em trechos de precisão, " +
             "mais baixo quando a altura das plataformas é que importa.")]
    [SerializeField, Range(5f, 70f)] float pitch = 28f;

    [SerializeField, Min(3f)] float distance = 15f;

    [Tooltip("Tempo da transição. Curto demais vira corte e desorienta no meio de um salto.")]
    [SerializeField, Min(0.1f)] float blendSeconds = 0.9f;

    static OrbitCamera s_Camera;

    void Reset()
    {
        if (TryGetComponent<BoxCollider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (s_Camera == null) s_Camera = FindAnyObjectByType<OrbitCamera>();
        if (s_Camera == null) return;

        s_Camera.SetFixedFraming(yaw, pitch, distance, blendSeconds);
    }
}
