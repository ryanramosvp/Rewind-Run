using UnityEngine;

/// <summary>
/// Anomalia gravitacional: puxa a bruxa para o centro, mais forte quanto mais
/// perto.
///
/// Não é só obstáculo. A atração estica um salto que passa por cima dela,
/// curva a trajetória no ar e puxa de lado quem caminha por perto - e o
/// impulso, que ignora forças externas enquanto dura (ver
/// <see cref="PlayerController.AplicarAceleracao"/>), é a saída quando ela
/// já está ganhando. Só o miolo mata, e ele é pequeno e escuro, fácil de ver.
///
/// A força é função da distância e nada mais: sem estado próprio, o rewind
/// da bruxa basta para desfazer o que ela fez.
/// </summary>
public class BuracoNegro : MonoBehaviour
{
    [Tooltip("Até onde a atração alcança, em metros.")]
    [SerializeField, Min(1f)] float raioDeInfluencia = 14f;

    [Tooltip("Aceleração no limite do miolo, em m/s². A gravidade da bruxa é 28.")]
    [SerializeField, Min(0f)] float aceleracaoMaxima = 34f;

    [Tooltip("Expoente da curva: 1 cresce em linha reta para o centro; 2 quase não puxa longe e puxa muito perto.")]
    [SerializeField, Range(0.5f, 3f)] float curva = 1.6f;

    [Tooltip("Raio do miolo que mata, em metros. Zero não mata.")]
    [SerializeField, Min(0f)] float raioMortal = 1.6f;

    [Header("Imagem")]
    [SerializeField] Transform disco;
    [SerializeField] Transform lente;
    [SerializeField, Min(0f)] float giroDoDisco = 70f;
    [SerializeField] AudioSource zumbido;

    PlayerController _bruxa;
    Vector3 _escalaDaLente;

    void Start()
    {
        _bruxa = FindAnyObjectByType<PlayerController>();
        if (lente != null) _escalaDaLente = lente.localScale;

        if (zumbido != null)
        {
            zumbido.clip = SomProcedural.BuracoNegro;
            zumbido.loop = true;
            zumbido.spatialBlend = 1f;
            zumbido.rolloffMode = AudioRolloffMode.Linear;
            zumbido.minDistance = raioDeInfluencia * 0.4f;
            zumbido.maxDistance = raioDeInfluencia * 1.6f;
            zumbido.dopplerLevel = 0f;
            zumbido.Play();
        }
    }

    void Update()
    {
        if (disco != null) disco.Rotate(Vector3.up, giroDoDisco * Time.deltaTime, Space.Self);
        if (lente != null)
            lente.localScale = _escalaDaLente * (1f + 0.04f * Mathf.Sin(Time.time * 2.3f));

        if (_bruxa == null) return;
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        // Medido no meio do corpo, e não nos pés.
        Vector3 corpo = _bruxa.transform.position + (_bruxa.GravidadeInvertida ? Vector3.down : Vector3.up) * 0.9f;
        Vector3 paraOCentro = transform.position - corpo;
        float d = paraOCentro.magnitude;
        if (d > raioDeInfluencia || d < 0.001f) return;

        if (d < raioMortal)
        {
            var respawn = LevelRespawn.Instance;
            if (respawn != null) respawn.RestartLevel();
            return;
        }

        float perto = 1f - d / raioDeInfluencia;
        _bruxa.AplicarAceleracao(paraOCentro / d * (aceleracaoMaxima * Mathf.Pow(perto, curva)));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioDeInfluencia);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioMortal);
    }
}
