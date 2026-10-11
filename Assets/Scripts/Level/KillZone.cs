using UnityEngine;

/// <summary>
/// Volume que mata quem entra. Na bruxa reinicia a fase; num prop com física,
/// devolve só aquele prop ao lugar de origem - senão as bolas cairiam na lava
/// no primeiro minuto e a fase ficaria sem elas para sempre.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    void Reset()
    {
        // Precisa ser trigger: a bruxa tem que afundar na lava, não pousar nela.
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var respawn = LevelRespawn.Instance;
        if (respawn == null) return;

        // GetComponentInParent e não GetComponent: quem encosta é o collider da
        // bruxa ou a malha do prop, e os scripts moram na raiz do objeto.
        if (other.GetComponentInParent<PlayerController>() != null)
        {
            respawn.RestartLevel();
            return;
        }

        var prop = other.GetComponentInParent<RewindableTransform>();
        if (prop != null) respawn.ResetProp(prop);
    }
}
