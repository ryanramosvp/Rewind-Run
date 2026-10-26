using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O que o rewind mostra na própria bruxa: um rastro de ecos azuis da pose,
/// um véu de luz no corpo enquanto o tempo volta e um anel nos pés ao começar
/// e ao soltar.
///
/// Só aparência. Quem volta no tempo é o <see cref="PlayerRewind"/>; quem
/// toca a animação de trás para frente é o <see cref="PlayerAnimator"/>. Este
/// componente só olha o <see cref="RewindManager"/> e desenha.
///
/// O rastro é o centro da ideia. Sem ele a bruxa só desliza de volta pelo
/// caminho, e deslizar parece um bug de física; com cópias da pose ficando
/// para trás e se apagando, o caminho desfeito fica escrito na tela - a
/// mesma leitura de Prince of Persia e Braid. Os ecos são fotos da malha
/// animada (BakeMesh), não partículas: cada um tem a pose daquele instante,
/// então a sequência mostra o movimento voltando.
///
/// Ao soltar, um eco e um anel dourados marcam o ponto em que o tempo volta
/// a correr - é dali que a corrida continua.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerRewindFx : MonoBehaviour
{
    [Tooltip("Material aditivo dos ecos, do véu e dos anéis (o WitchSetup gera).")]
    [SerializeField] Material glowMaterial;

    [Header("Cores")]
    [SerializeField] Color rewindColor = new Color(0.35f, 0.78f, 1f, 1f);
    [SerializeField] Color resumeColor = new Color(1f, 0.78f, 0.32f, 1f);

    [Header("Rastro")]
    [Tooltip("Intervalo entre ecos, em segundos.")]
    [SerializeField, Min(0.02f)] float ghostInterval = 0.09f;
    [Tooltip("Quanto um eco leva para sumir.")]
    [SerializeField, Min(0.05f)] float ghostLifetime = 0.65f;
    [Tooltip("Distância mínima entre dois ecos. Parada no lugar, a bruxa não empilha cópias.")]
    [SerializeField, Min(0f)] float ghostSpacing = 0.6f;
    [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.6f;
    [SerializeField, Min(1)] int maxGhosts = 12;

    [Header("Véu")]
    [SerializeField, Range(0f, 1f)] float veilAlpha = 0.35f;
    [SerializeField, Min(0f)] float veilPulseSpeed = 14f;

    [Header("Anel")]
    [SerializeField, Min(0.1f)] float ringSeconds = 0.4f;
    [SerializeField, Min(0.1f)] float ringRadius = 3f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    class Ghost
    {
        public GameObject Root;
        public MeshFilter[] Filters;
        public MeshRenderer[] Renderers;
        public Mesh[] Meshes;
        public Color Color;
        public float Age = float.MaxValue;
        public float Life = 1f;
    }

    class Ring
    {
        public GameObject Root;
        public MeshRenderer Renderer;
        public Color Color;
        public float Age = float.MaxValue;
    }

    SkinnedMeshRenderer[] _parts;
    Material[][] _originalMaterials;
    Material _veil;
    readonly List<Ghost> _ghosts = new List<Ghost>();
    readonly Ring[] _rings = new Ring[2];
    MaterialPropertyBlock _block;
    Transform _fxRoot;

    bool _wasRewinding;
    bool _veilOn;
    float _veilWeight;
    float _sinceGhost;
    Vector3 _lastGhostPosition;
    int _nextGhost;

    void Awake()
    {
        if (glowMaterial == null)
        {
            enabled = false;
            return;
        }

        _parts = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _originalMaterials = new Material[_parts.Length][];
        for (int i = 0; i < _parts.Length; i++) _originalMaterials[i] = _parts[i].sharedMaterials;

        // Um material próprio para o véu: a cor dele pulsa, e mexer no asset
        // mexeria nos ecos também.
        _veil = new Material(glowMaterial) { name = "VeuDoRewind" };
        _block = new MaterialPropertyBlock();

        // Fora da hierarquia da bruxa: os ecos ficam onde nasceram, não
        // seguem quem os deixou.
        _fxRoot = new GameObject("EfeitosDoRewind").transform;

        for (int i = 0; i < maxGhosts; i++) _ghosts.Add(CreateGhost(i));
        for (int i = 0; i < _rings.Length; i++) _rings[i] = CreateRing(i);
    }

    void OnDestroy()
    {
        if (_fxRoot != null) Destroy(_fxRoot.gameObject);
        if (_veil != null) Destroy(_veil);
        foreach (var ghost in _ghosts)
        {
            if (ghost.Meshes == null) continue;
            foreach (var mesh in ghost.Meshes) if (mesh != null) Destroy(mesh);
        }
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        bool rewinding = manager != null && manager.IsRewinding;
        float dt = Time.deltaTime;

        if (rewinding && !_wasRewinding) OnBegin();
        else if (!rewinding && _wasRewinding) OnEnd();
        _wasRewinding = rewinding;

        if (rewinding)
        {
            _sinceGhost += dt;
            bool moved = (transform.position - _lastGhostPosition).sqrMagnitude >= ghostSpacing * ghostSpacing;
            if (_sinceGhost >= ghostInterval && moved) SpawnGhost(rewindColor, ghostLifetime, ghostAlpha);
        }

        UpdateVeil(rewinding, dt);
        UpdateGhosts(dt);
        UpdateRings(dt);
    }

    void OnBegin()
    {
        _sinceGhost = ghostInterval;   // o primeiro eco sai já no primeiro quadro
        _lastGhostPosition = transform.position + Vector3.one * 100f;
        PlayRing(0, rewindColor);
    }

    void OnEnd()
    {
        // O ponto de retomada: um eco dourado mais demorado e o anel.
        SpawnGhost(resumeColor, ghostLifetime * 1.4f, ghostAlpha);
        PlayRing(1, resumeColor);
    }

    // ------------------------------------------------------------------
    // Véu
    // ------------------------------------------------------------------

    /// <summary>
    /// Uma segunda passada de cada parte do corpo com o material aditivo, por
    /// cima da primeira: a bruxa acende em azul sem perder a própria cor.
    /// </summary>
    void UpdateVeil(bool rewinding, float dt)
    {
        _veilWeight = Mathf.MoveTowards(_veilWeight, rewinding ? 1f : 0f, dt / (rewinding ? 0.08f : 0.25f));

        bool on = _veilWeight > 0.001f;
        if (on != _veilOn) SetVeil(on);
        if (!on) return;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * veilPulseSpeed);
        var color = Color.Lerp(resumeColor, rewindColor, rewinding ? 1f : _veilWeight);
        color.a = veilAlpha * _veilWeight * pulse;
        _veil.SetColor(BaseColorId, color);
    }

    void SetVeil(bool on)
    {
        _veilOn = on;
        for (int i = 0; i < _parts.Length; i++)
        {
            if (_parts[i] == null) continue;

            if (!on)
            {
                _parts[i].sharedMaterials = _originalMaterials[i];
                continue;
            }

            var original = _originalMaterials[i];
            var withVeil = new Material[original.Length + 1];
            original.CopyTo(withVeil, 0);
            withVeil[original.Length] = _veil;
            _parts[i].sharedMaterials = withVeil;
        }
    }

    // ------------------------------------------------------------------
    // Ecos
    // ------------------------------------------------------------------

    Ghost CreateGhost(int index)
    {
        var ghost = new Ghost
        {
            Root = new GameObject($"Eco_{index}"),
            Filters = new MeshFilter[_parts.Length],
            Renderers = new MeshRenderer[_parts.Length],
            Meshes = new Mesh[_parts.Length],
        };
        ghost.Root.transform.SetParent(_fxRoot, false);

        for (int i = 0; i < _parts.Length; i++)
        {
            var part = new GameObject(_parts[i].name);
            part.transform.SetParent(ghost.Root.transform, false);

            ghost.Meshes[i] = new Mesh { name = $"Eco_{_parts[i].name}" };
            ghost.Filters[i] = part.AddComponent<MeshFilter>();
            ghost.Filters[i].sharedMesh = ghost.Meshes[i];

            ghost.Renderers[i] = part.AddComponent<MeshRenderer>();
            ghost.Renderers[i].sharedMaterial = glowMaterial;
            ghost.Renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghost.Renderers[i].receiveShadows = false;
        }

        ghost.Root.SetActive(false);
        return ghost;
    }

    void SpawnGhost(Color color, float life, float alpha)
    {
        var ghost = _ghosts[_nextGhost];
        _nextGhost = (_nextGhost + 1) % _ghosts.Count;

        for (int i = 0; i < _parts.Length; i++)
        {
            var part = _parts[i];
            bool visible = part != null && part.enabled && part.gameObject.activeInHierarchy;
            ghost.Renderers[i].enabled = visible;
            if (!visible) continue;

            // Malha na pose deste instante, no espaço da parte, com a escala
            // já aplicada: o pedaço do eco vai na posição e no giro dela.
            part.BakeMesh(ghost.Meshes[i], true);
            ghost.Filters[i].transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
        }

        ghost.Color = color;
        ghost.Color.a = alpha;
        ghost.Age = 0f;
        ghost.Life = life;
        ghost.Root.SetActive(true);
        Tint(ghost.Renderers, ghost.Color);

        _sinceGhost = 0f;
        _lastGhostPosition = transform.position;
    }

    void UpdateGhosts(float dt)
    {
        foreach (var ghost in _ghosts)
        {
            if (!ghost.Root.activeSelf) continue;

            ghost.Age += dt;
            float t = ghost.Age / ghost.Life;
            if (t >= 1f)
            {
                ghost.Root.SetActive(false);
                continue;
            }

            var color = ghost.Color;
            color.a *= (1f - t) * (1f - t);
            Tint(ghost.Renderers, color);
        }
    }

    // ------------------------------------------------------------------
    // Anéis
    // ------------------------------------------------------------------

    Ring CreateRing(int index)
    {
        var ring = new Ring { Root = new GameObject($"Anel_{index}") };
        ring.Root.transform.SetParent(_fxRoot, false);
        ring.Root.AddComponent<MeshFilter>().sharedMesh = RingMesh(48, 0.82f);
        ring.Renderer = ring.Root.AddComponent<MeshRenderer>();
        ring.Renderer.sharedMaterial = glowMaterial;
        ring.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.Root.SetActive(false);
        return ring;
    }

    void PlayRing(int index, Color color)
    {
        var ring = _rings[index];
        ring.Color = color;
        ring.Age = 0f;
        ring.Root.transform.position = transform.position + Vector3.up * 0.08f;
        ring.Root.SetActive(true);
    }

    void UpdateRings(float dt)
    {
        foreach (var ring in _rings)
        {
            if (!ring.Root.activeSelf) continue;

            ring.Age += dt;
            float t = ring.Age / ringSeconds;
            if (t >= 1f)
            {
                ring.Root.SetActive(false);
                continue;
            }

            // Abre rápido e apaga devagar.
            float open = 1f - (1f - t) * (1f - t) * (1f - t);
            ring.Root.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, ringRadius, open);

            var color = ring.Color;
            color.a = 0.8f * (1f - t);
            Tint(ring.Renderer, color);
        }
    }

    // ------------------------------------------------------------------

    void Tint(MeshRenderer[] renderers, Color color)
    {
        _block.SetColor(BaseColorId, color);
        foreach (var renderer in renderers) renderer.SetPropertyBlock(_block);
    }

    void Tint(MeshRenderer renderer, Color color)
    {
        _block.SetColor(BaseColorId, color);
        renderer.SetPropertyBlock(_block);
    }

    /// <summary>Anel plano de raio 1, deitado no chão, com a borda de dentro em <paramref name="inner"/>.</summary>
    static Mesh RingMesh(int segments, float inner)
    {
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices[i * 2] = dir * inner;
            vertices[i * 2 + 1] = dir;

            int next = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = i * 2 + 1;
            triangles[t + 2] = next * 2 + 1;
            triangles[t + 3] = i * 2;
            triangles[t + 4] = next * 2 + 1;
            triangles[t + 5] = next * 2;
        }

        var mesh = new Mesh { name = "AnelDoRewind", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
