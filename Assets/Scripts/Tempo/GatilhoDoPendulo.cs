using UnityEngine;

/// <summary>
/// Fica no peso do pêndulo e repassa o toque para o <see cref="Pendulo"/> no
/// eixo. O gatilho precisa estar no peso, que é o que se move; o pêndulo, no
/// eixo, que é o que gira.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GatilhoDoPendulo : MonoBehaviour
{
    Pendulo _pendulo;

    void Awake() => _pendulo = GetComponentInParent<Pendulo>();

    void OnTriggerEnter(Collider other) => _pendulo?.Atingiu(other);

    void OnTriggerStay(Collider other) => _pendulo?.Atingiu(other);
}
