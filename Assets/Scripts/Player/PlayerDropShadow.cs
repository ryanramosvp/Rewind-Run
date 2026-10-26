using UnityEngine;

/// <summary>
/// Sombra de pouso: um disco escuro no chão, exatamente embaixo da bruxa.
///
/// Opaco e de borda dura, como as sombras do Mario 64: é a forma que se lê
/// a 20 m de distância sobre pedra e chapa, e não depende de transparência.
///
/// A sombra da luz cai inclinada e longe dos pés; no ar ela não diz onde a
/// bruxa vai descer. Este disco diz - é o truque dos plataformas 3D desde o
/// Mario 64. Com ele o vão deixa de ser adivinhado pela perspectiva: basta
/// levar o disco até a plataforma.
///
/// E quando não há chão embaixo, não há disco. Sobre a lava ele some, o que
/// também avisa: soltar ali é cair.
///
/// É calculado a cada quadro a partir da posição da bruxa, sem estado
/// próprio, então segue o rewind sem precisar gravar nada.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerDropShadow : MonoBehaviour
{
    [Tooltip("Material opaco e escuro do disco (o WitchSetup gera).")]
    [SerializeField] Material material;

    [Tooltip("Diâmetro do disco com a bruxa no chão, em metros.")]
    [SerializeField, Min(0.1f)] float size = 1.4f;

    [Tooltip("Altura a partir da qual o disco já está no tamanho mínimo.")]
    [SerializeField, Min(1f)] float shrinkHeight = 12f;

    [SerializeField, Range(0f, 1f)] float minScale = 0.7f;

    [Tooltip("Até onde procurar chão embaixo.")]
    [SerializeField, Min(1f)] float maxDistance = 80f;

    Transform _disc;
    MeshRenderer _renderer;
    readonly RaycastHit[] _hits = new RaycastHit[8];

    void Awake()
    {
        if (material == null)
        {
            enabled = false;
            return;
        }

        // Fora da hierarquia da bruxa: o disco não pode herdar o giro nem o
        // squash dela.
        var go = new GameObject("SombraDePouso");
        _disc = go.transform;
        go.AddComponent<MeshFilter>().sharedMesh = BuildDisc(16);
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = material;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    void OnDestroy()
    {
        if (_disc != null) Destroy(_disc.gameObject);
    }

    void LateUpdate()
    {
        // O raio sai da cintura, para não começar abaixo de uma rampa em que
        // a bruxa está com os pés afundados alguns centímetros.
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, maxDistance,
                                            ~0, QueryTriggerInteraction.Ignore);

        // O mais perto que não seja a própria bruxa.
        int best = -1;
        for (int i = 0; i < count; i++)
        {
            if (_hits[i].collider.transform.IsChildOf(transform)) continue;
            if (best < 0 || _hits[i].distance < _hits[best].distance) best = i;
        }

        if (best < 0)
        {
            _renderer.enabled = false;
            return;
        }

        var hit = _hits[best];
        float height = Mathf.Max(0f, hit.distance - 0.5f);
        float t = Mathf.Clamp01(height / shrinkHeight);

        _renderer.enabled = true;
        // 3 cm acima da superfície, para não brigar com ela no depth buffer.
        _disc.SetPositionAndRotation(hit.point + hit.normal * 0.03f,
                                     Quaternion.FromToRotation(Vector3.up, hit.normal));
        _disc.localScale = Vector3.one * (size * Mathf.Lerp(1f, minScale, t));
    }

    /// <summary>Disco de 1 m de diâmetro no plano XZ, virado para cima.</summary>
    static Mesh BuildDisc(int sides)
    {
        var vertices = new Vector3[sides + 1];
        var triangles = new int[sides * 3];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f;

            // Horário visto de cima: a face fica para +Y.
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % sides + 1;
        }

        var mesh = new Mesh { name = "SombraDePouso", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
