using UnityEngine;

/// <summary>
/// Fim da fase. Entrega a vitória para a <see cref="EndScreen"/>, que é quem
/// sabe mostrar placar e oferecer as saídas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelFinish : MonoBehaviour
{
    [SerializeField] EndScreen endScreen;

    bool _done;

    void Reset()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void Awake()
    {
        if (endScreen == null) endScreen = FindAnyObjectByType<EndScreen>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_done) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _done = true;
        if (endScreen != null) endScreen.ShowVictory();
    }
}
