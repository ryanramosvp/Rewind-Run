using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reinicia a fase sem recarregar a cena: devolve a bruxa ao ponto de partida,
/// põe os props de volta no lugar e limpa a linha do tempo do rewind.
///
/// Recarregar a cena seria uma linha só, mas dá um engasgo de carregamento a
/// cada queda - e queda vai ser a coisa mais comum do jogo. Reposicionar em
/// memória é instantâneo e já é a base pronta para os checkpoints.
/// </summary>
[DefaultExecutionOrder(-90)]
public class LevelRespawn : MonoBehaviour
{
    public static LevelRespawn Instance { get; private set; }

    /// <summary>Disparado quando a fase recomeca por morte.</summary>
    public event System.Action Died;

    [SerializeField] PlayerController player;

    [Tooltip("Onde a bruxa reaparece. Vazio usa a posição em que ela começou a fase.")]
    [SerializeField] Transform spawnPoint;

    Vector3 _spawnPosition;
    Quaternion _spawnRotation;

    readonly List<RewindableTransform> _props = new List<RewindableTransform>();
    readonly List<Vector3> _propPositions = new List<Vector3>();
    readonly List<Quaternion> _propRotations = new List<Quaternion>();

    void Awake()
    {
        Instance = this;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    void Start()
    {
        // Start, e não Awake: assim os props já estão no lugar que o level
        // builder definiu quando fotografamos o estado inicial da fase.
        if (player != null)
        {
            var reference = spawnPoint != null ? spawnPoint : player.transform;
            _spawnPosition = reference.position;
            _spawnRotation = reference.rotation;
        }

        foreach (var prop in FindObjectsByType<RewindableTransform>(FindObjectsInactive.Include))
        {
            _props.Add(prop);
            _propPositions.Add(prop.transform.position);
            _propRotations.Add(prop.transform.rotation);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Recomeça a fase do início.</summary>
    public void RestartLevel()
    {
        Died?.Invoke();
        RestorePlayer();

        for (int i = 0; i < _props.Count; i++)
            RestoreProp(i);

        // A história de antes da morte não serve mais para nada, e deixá-la no
        // buffer permitiria voltar no tempo para dentro da lava.
        var rewind = RewindManager.Instance;
        if (rewind != null)
        {
            rewind.ResetHistory();
            rewind.RefillCharge();
        }
    }

    /// <summary>Devolve um prop ao lugar de origem, sem reiniciar a fase inteira.</summary>
    public void ResetProp(RewindableTransform prop)
    {
        int index = _props.IndexOf(prop);
        if (index >= 0) RestoreProp(index);
    }

    void RestorePlayer()
    {
        if (player == null) return;

        // O CharacterController ignora escritas diretas no transform enquanto
        // está ativo, então ele sai do caminho para o teleporte.
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);

        if (controller != null) controller.enabled = true;

        player.ResetToIdle();
    }

    void RestoreProp(int index)
    {
        var prop = _props[index];
        if (prop == null) return;

        if (prop.TryGetComponent<Rigidbody>(out var body))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        prop.transform.SetPositionAndRotation(_propPositions[index], _propRotations[index]);
        prop.ClearHistory();
    }
}
