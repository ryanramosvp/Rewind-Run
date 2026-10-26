using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Peças e validação das fases vulcânicas, a começar pela Terra da Lava
/// Letal.
///
/// Nasceram dentro do construtor da fase 3 e saíram dele para servir a
/// próxima fase também: ilhas de pedra, placas, colunas, rampas, bolhas,
/// plataformas móveis, gêiseres - e a conta que mede cada salto contra o pulo
/// real da bruxa. Duas cópias divergiriam no primeiro ajuste.
///
/// Cada construtor chama <see cref="BeginLevel"/> antes de montar: os
/// registros de contorno, bolhas e rampas são estáticos e valem para a fase
/// que está sendo montada.
/// </summary>
public static class VolcanicKit
{
    /// <summary>Zera os registros e garante a arte. Chamar no início de cada construtor.</summary>
    public static void BeginLevel()
    {
        LavaLetalArt.Ensure();
        Footprints.Clear();
        FarEnds.Clear();
        Bubbles.Clear();
        RampLips.Clear();
    }

    /// <summary>
    /// Relatório dos saltos de uma fase.
    ///
    /// Usa os números do prefab da bruxa, e não constantes copiadas: se alguém
    /// mexer na altura do pulo ou na gravidade, a próxima reconstrução diz no
    /// console qual salto deixou de fechar, em vez de isso ser descoberto
    /// jogando.
    ///
    /// O vão é a menor distância entre os contornos, e o critério é
    /// conservador: a bruxa sai 0,3 m antes da beira e precisa pousar 0,5 m
    /// para dentro; o pulo duplo conta disparado no topo do primeiro (0,5 s),
    /// não no instante ideal, que é tarde e quase ninguém acerta.
    /// </summary>
    public sealed class JumpReport
    {
        public JumpModel Jump { get; }
        public int Failures { get; private set; }

        readonly string _level;
        readonly StringBuilder _text;

        JumpReport(string level, JumpModel jump)
        {
            _level = level;
            Jump = jump;
            _text = new StringBuilder($"{level} - saltos validados contra o pulo real " +
                                      $"({jump.Speed} m/s, pulo {jump.Jump} m, duplo {jump.DoubleJump} m):\n");
        }

        /// <summary>Lê o pulo do prefab da bruxa. Nulo, com aviso, se ele não existir.</summary>
        public static JumpReport Begin(string level)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchSetup.PrefabPath);
            var controller = prefab != null ? prefab.GetComponent<PlayerController>() : null;
            if (controller == null)
            {
                Debug.LogWarning($"Sem o prefab da bruxa não dá para validar os saltos de {level}.");
                return null;
            }

            var p = new SerializedObject(controller);
            return new JumpReport(level, new JumpModel
            {
                Speed = p.FindProperty("moveSpeed").floatValue,
                Gravity = p.FindProperty("gravity").floatValue,
                Fall = p.FindProperty("fallGravityMultiplier").floatValue,
                ApexThreshold = p.FindProperty("apexThreshold").floatValue,
                Apex = p.FindProperty("apexGravityMultiplier").floatValue,
                Jump = p.FindProperty("jumpHeight").floatValue,
                DoubleJump = p.FindProperty("doubleJumpHeight").floatValue,
            });
        }

        public void Section(string title) => _text.Append($"\n{title}\n");

        /// <summary>
        /// Confere uma sequência de saltos simples entre peças registradas.
        ///
        /// Plataforma móvel entra com as duas pontas do percurso. O salto
        /// precisa fechar em pelo menos uma - é o momento de pular - e o
        /// relatório mostra também a pior, para se ver quanto o tempo do salto
        /// importa ali.
        /// </summary>
        public void Route(string title, params string[] route)
        {
            Section(title);
            for (int i = 0; i + 1 < route.Length; i++)
            {
                if (!Footprints.TryGetValue(route[i], out var a) || !Footprints.TryGetValue(route[i + 1], out var b))
                {
                    _text.Append($"  {route[i]} -> {route[i + 1]}: peça não registrada\n");
                    Failures++;
                    continue;
                }

                var froms = Variants(route[i], a);
                var tos = Variants(route[i + 1], b);

                (int rank, string text, float gap, float dy, float single, float twin) best = (99, "", 0, 0, 0, 0);
                (int rank, string text, float gap, float dy, float single, float twin) worst = (-1, "", 0, 0, 0, 0);

                foreach (var f in froms)
                foreach (var t in tos)
                {
                    // Mesma classificação desempata pelo vão: o melhor é o mais
                    // curto, o pior é o mais longo.
                    var r = Judge(Jump, Gap(f.poly, t.poly), t.top - f.top);
                    if (r.rank < best.rank || (r.rank == best.rank && r.gap < best.gap)) best = r;
                    if (r.rank > worst.rank || (r.rank == worst.rank && r.gap > worst.gap)) worst = r;
                }

                if (best.rank >= 3) Failures++;

                string line = $"  {route[i]} -> {route[i + 1]}: vão {best.gap:0.0} m, desnível {best.dy:+0.0;-0.0;0.0} m " +
                              $"(alcance {best.single:0.0} / {best.twin:0.0} m) - {best.text}";
                if (froms.Count * tos.Count > 1)
                    line += $"; na pior ponta: vão {worst.gap:0.0} m, desnível {worst.dy:+0.0;-0.0;0.0} m - {worst.text}";
                _text.Append(line + "\n");
            }
        }

        /// <summary>Confere um voo simulado: se pousa, ou se não pousa quando não deveria.</summary>
        public void Expect(string title, bool shouldLand, (bool landed, float d) result)
        {
            bool ok = result.landed == shouldLand;
            if (!ok) Failures++;

            string what = result.landed ? $"pousa a {result.d:0.0} m" : "não alcança";
            string verdict = ok ? (shouldLand ? "ok" : "ok, como pedido") : (shouldLand ? "FALHA" : "PASSA SEM O COMBO");
            _text.Append($"  {title}: {what} - {verdict}\n");
        }

        /// <summary>Linha livre no relatório, para resultados que não são um voo.</summary>
        public void Note(string line, bool failure = false)
        {
            if (failure) Failures++;
            _text.Append($"  {line}\n");
        }

        public void Finish()
        {
            if (Failures > 0)
                Debug.LogWarning(_text.Append($"\n{Failures} salto(s) impossível(is) em {_level}.").ToString());
            else
                Debug.Log(_text.ToString());
        }
    }

    /// <summary>Onde as ilhas começam, abaixo da lava: a base nunca aparece.</summary>
    public const float kBedrock = -1.5f;

    /// <summary>Fração mínima do raio no sorteio das rochas (ver <see cref="LowPolyMesh.Rock"/>).</summary>
    public const float kRockMin = 0.88f;

    /// <summary>Contorno de cada plataforma do percurso, em XZ de mundo, para a validação.</summary>
    public static readonly Dictionary<string, (float top, Vector2[] poly)> Footprints =
        new Dictionary<string, (float, Vector2[])>();

    /// <summary>Contorno das plataformas móveis na outra ponta do percurso.</summary>
    public static readonly Dictionary<string, (float top, Vector2[] poly)> FarEnds =
        new Dictionary<string, (float, Vector2[])>();

    /// <summary>Centro de cada bolha, para a validação dos combos.</summary>
    public static readonly Dictionary<string, Vector3> Bubbles = new Dictionary<string, Vector3>();

    /// <summary>Ponta de baixo de cada rampa: posição, altura e direção da descida.</summary>
    public static readonly Dictionary<string, (Vector2 xz, float y, Vector2 forward)> RampLips =
        new Dictionary<string, (Vector2, float, Vector2)>();

    // ------------------------------------------------------------------
    // Peças
    // ------------------------------------------------------------------

    /// <summary>Quanto a base das ilhas se abre em relação ao topo, perto da lava.</summary>
    public const float kFlare = 1.12f;

    /// <summary>
    /// Ilha de pedra que sobe da lava: polígono irregular, borda escura, topo
    /// na cor pedida.
    ///
    /// A base é 12% mais larga que o topo, e não o contrário: rocha que sai da
    /// lava se alarga embaixo. E é o topo que guarda o tamanho pedido, porque
    /// é nele que se pousa.
    /// </summary>
    public static GameObject Rock(Transform root, string name, float x, float z, float top, float radius,
                           int sides, int seed, Material topMaterial, bool route = true)
    {
        var ring = LowPolyMesh.Rock(sides, radius, kRockMin, new System.Random(seed));
        var mesh = LowPolyMesh.Prism(Flare(ring), kBedrock, top, LavaLetalArt.RockTile, 1f / kFlare, name);

        var go = Solid(root, name, mesh, topMaterial, LavaLetalArt.RockSide, new Vector3(x, 0f, z), 0f);
        AddMeshCollider(go);

        if (route) Register(name, top, World(go.transform, ring));
        return go;
    }

    /// <summary>
    /// Coluna de basalto hexagonal, do tamanho de um bloco de 2 m.
    /// <paramref name="bottom"/> vai mais fundo nas que sobem e descem, para a
    /// base nunca aparecer acima da lava.
    /// </summary>
    public static GameObject Column(Transform root, string name, float x, float z, float top, int seed,
                             float bottom = kBedrock)
    {
        float turn = (float)new System.Random(seed).NextDouble() * 60f;
        var ring = LowPolyMesh.Regular(6, 1.2f, turn);

        var go = Solid(root, name, LowPolyMesh.Prism(ring, bottom, top, LavaLetalArt.RockTile, 1f, name),
                       LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, new Vector3(x, 0f, z), 0f);
        AddMeshCollider(go);
        Register(name, top, World(go.transform, ring));
        return go;
    }

    /// <summary>
    /// Placa metálica xadrez: tampo, faixas de perigo nas bordas e pés até a
    /// lava.
    ///
    /// Só o tampo tem colisão, numa caixa. Faixas e pés são imagem: os pés
    /// ficam embaixo da placa, onde ninguém pisa, e as faixas ficam dentro do
    /// contorno do tampo, então não mudam onde a bruxa pode pousar.
    /// </summary>
    public static GameObject Plate(Transform root, string name, float x, float z, float top, float sizeX, float sizeZ,
                            float yaw, Material material, bool trim, bool supports, float thickness = 0.6f)
    {
        var rectangle = LowPolyMesh.Rectangle(sizeX, sizeZ);
        var go = Solid(root, name, LowPolyMesh.Prism(rectangle, -thickness, 0f, LavaLetalArt.MetalTile, 1f, name),
                       material, LavaLetalArt.Dark, new Vector3(x, top, z), yaw);

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -thickness * 0.5f, 0f);
        box.size = new Vector3(sizeX, thickness, sizeZ);

        if (trim)
        {
            const float w = 0.28f;
            Trim(go.transform, new Vector3(0f, 0f, sizeZ * 0.5f - w * 0.5f), new Vector2(sizeX, w));
            Trim(go.transform, new Vector3(0f, 0f, -sizeZ * 0.5f + w * 0.5f), new Vector2(sizeX, w));
            Trim(go.transform, new Vector3(sizeX * 0.5f - w * 0.5f, 0f, 0f), new Vector2(w, sizeZ - 2f * w));
            Trim(go.transform, new Vector3(-sizeX * 0.5f + w * 0.5f, 0f, 0f), new Vector2(w, sizeZ - 2f * w));
        }

        if (supports)
        {
            float length = top - thickness - kBedrock;
            float ix = Mathf.Max(0f, sizeX * 0.5f - 0.6f), iz = Mathf.Max(0f, sizeZ * 0.5f - 0.6f);
            var leg = LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.4f, 0.4f), -length, 0f, 1f, 1f, "Pe");

            foreach (var corner in new[] { new Vector2(ix, iz), new Vector2(-ix, iz), new Vector2(ix, -iz), new Vector2(-ix, -iz) })
            {
                var foot = Solid(go.transform, "Pe", leg, LavaLetalArt.Dark, LavaLetalArt.Dark,
                                 new Vector3(corner.x, -thickness, corner.y), 0f);
                foot.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        Register(name, top, World(go.transform, rectangle));
        return go;
    }

    /// <summary>
    /// Põe uma peça já montada para ir e voltar, com a MovingPlatform da fase 1.
    ///
    /// É o mesmo componente de lá, então carregar a bruxa e herdar a velocidade
    /// no salto já funcionam; e no rewind o relógio dela anda para trás, então
    /// a plataforma volta para onde estava naquele instante.
    ///
    /// Tira as marcas de estático da peça inteira. Com batching estático a
    /// malha é fundida no início da cena e fica parada no lugar, mesmo com o
    /// transform andando - a colisão iria e a imagem não.
    ///
    /// Registra também o contorno na outra ponta do percurso, para a validação
    /// medir os saltos nas duas posições.
    /// </summary>
    public static void MakeMoving(GameObject go, Vector3 offset, float travelSeconds, float waitSeconds, float phase)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var move = go.AddComponent<MovingPlatform>();
        LevelKit.SetVector(move, "offset", offset);
        LevelKit.SetFloat(move, "travelSeconds", travelSeconds);
        LevelKit.SetFloat(move, "waitSeconds", waitSeconds);
        LevelKit.SetFloat(move, "phase", phase);

        if (!Footprints.TryGetValue(go.name, out var start)) return;

        var shifted = new Vector2[start.poly.Length];
        for (int i = 0; i < shifted.Length; i++)
            shifted[i] = start.poly[i] + new Vector2(offset.x, offset.z);

        FarEnds[go.name] = (start.top + offset.y, shifted);
    }

    /// <summary>
    /// Gêiser de lava: boca de pedra, brasa no fundo, jato e a cúpula do aviso.
    ///
    /// O jato é um prisma de 1 m com o pivô na base; o LavaGeyser estica a
    /// escala Y até a altura do momento. O gatilho letal fica na raiz, com um
    /// corpo cinemático - ele muda de tamanho a cada quadro, e collider sem
    /// corpo é tratado como cenário parado.
    /// </summary>
    public static void Geyser(Transform root, string name, Vector3 basePosition, float idleSeconds, float phase,
                       float jetHeight = 8f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = basePosition;

        Decor(go.transform, "Boca", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 1.6f, 22.5f), 0f, 0.25f, 1f, 0.85f),
              LavaLetalArt.RockSide, Vector3.zero);
        var ember = Solid(go.transform, "Brasa", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 1.1f, 22.5f), 0.2f, 0.27f, 1f),
                          LavaLetalArt.Ember, LavaLetalArt.Ember, Vector3.zero, 0f);

        var jet = new GameObject("Jato").transform;
        jet.SetParent(go.transform, false);
        jet.localPosition = new Vector3(0f, 0.25f, 0f);
        var column = Solid(jet, "Coluna", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 0.9f, 22.5f), 0f, 1f, 2f),
                           LavaLetalArt.Jet, LavaLetalArt.Jet, Vector3.zero, 0f);
        column.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var dome = new GameObject("Bolhas").transform;
        dome.SetParent(go.transform, false);
        dome.localPosition = new Vector3(0f, 0.25f, 0f);
        Solid(dome, "Cupula", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 0.8f, 22.5f), 0f, 0.6f, 1f, 0.45f),
              LavaLetalArt.Jet, LavaLetalArt.Jet, Vector3.zero, 0f);

        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        LevelKit.AddKinematicBody(go);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 45f;

        var geyser = go.AddComponent<LavaGeyser>();
        EditorSetupUtil.SetReference(geyser, "jet", jet);
        EditorSetupUtil.SetReference(geyser, "hazard", area);
        EditorSetupUtil.SetReference(geyser, "vent", ember.GetComponent<MeshRenderer>());
        EditorSetupUtil.SetReference(geyser, "dome", dome);
        EditorSetupUtil.SetReference(geyser, "audioSource", source);
        LevelKit.SetFloat(geyser, "idleSeconds", idleSeconds);
        LevelKit.SetFloat(geyser, "phase", phase);
        LevelKit.SetFloat(geyser, "jetHeight", jetHeight);
    }

    /// <summary>A bolha da Torre de Bolhas, igual: mesmo prefab, mesmo quique, mesmo rewind.</summary>
    public static void Bubble(Transform root, string name, Vector3 center, float bobPhase)
    {
        LevelKit.Bubble(root, center, bobPhase).name = name;
        Bubbles[name] = center;
    }

    // Perfil das rampas: 3 m de entrada a 17 graus, e o resto a 34. A entrada
    // fica abaixo dos 25 graus do deslize - ali a bruxa ainda corre - e é o
    // que impede quem chega a 11 m/s de voar por cima da rampa (ver
    // LowPolyMesh.Ramp). O trecho de 34 é o que dispara o deslize.
    public const float kRampEntry = 3f;
    public const float kRampEntryAngle = 17f;
    public const float kRampAngle = 34f;

    /// <summary>Comprimento total de uma rampa com essa queda, entrada inclusa.</summary>
    public static float RampLength(float drop)
    {
        float entryDrop = kRampEntry * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad);
        return kRampEntry + (drop - entryDrop) / Mathf.Tan(kRampAngle * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Rampa de chapa sobre bloco de pedra, descendo para +Z (girada por
    /// <paramref name="yaw"/>). <paramref name="topEdge"/> é o meio da aresta de
    /// cima; a de baixo termina num degrau sobre a lava.
    ///
    /// Entrando a 11 m/s, a bruxa ganha o impulso de 3 ao começar a deslizar e
    /// chega ao teto de 15 m/s antes do fim do trecho íngreme.
    ///
    /// <paramref name="thickness"/> acima de zero faz uma rampa suspensa, com o
    /// bloco só essa medida abaixo da ponta de baixo: para rampas no alto, em
    /// que um bloco até a lava atravessaria tudo que fica embaixo delas.
    /// </summary>
    public static void Ramp(Transform root, string name, Vector3 topEdge, float width, float drop, float yaw,
                            float thickness = 0f)
    {
        float length = RampLength(drop);
        float entryDrop = kRampEntry * Mathf.Tan(kRampEntryAngle * Mathf.Deg2Rad);
        float bottom = thickness > 0f ? -drop - thickness : kBedrock - topEdge.y;

        var mesh = LowPolyMesh.Ramp(width, length, drop, bottom, LavaLetalArt.MetalTile,
                                    kRampEntry, entryDrop, name);
        var go = Solid(root, name, mesh, LavaLetalArt.Metal, LavaLetalArt.RockSide, topEdge, yaw);
        AddMeshCollider(go);

        // Faixas de perigo nas duas bordas laterais, uma por trecho da descida.
        var corners = new[] { Vector2.zero, new Vector2(kRampEntry, -entryDrop), new Vector2(length, -drop) };
        for (int i = 0; i + 1 < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[i + 1];
            float angle = Mathf.Atan2(a.y - b.y, b.x - a.x) * Mathf.Rad2Deg;
            float span = Vector2.Distance(a, b);
            var mid = (a + b) * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                var strip = new GameObject("Faixa");
                strip.transform.SetParent(go.transform, false);
                strip.transform.SetLocalPositionAndRotation(
                    new Vector3(side * (width * 0.5f - 0.14f), mid.y, mid.x), Quaternion.Euler(angle, 0f, 0f));
                Solid(strip.transform, "Faixa",
                      LowPolyMesh.Prism(LowPolyMesh.Rectangle(0.28f, span), -0.24f, 0.015f, 1f, 1f, "Faixa"),
                      LavaLetalArt.Hazard, LavaLetalArt.Hazard, Vector3.zero, 0f);
            }
        }

        var lip = go.transform.TransformPoint(new Vector3(0f, -drop, length));
        var forward = go.transform.forward;
        RampLips[name] = (new Vector2(lip.x, lip.z), lip.y, new Vector2(forward.x, forward.z).normalized);
    }

    public static void Trim(Transform plate, Vector3 center, Vector2 size)
    {
        var strip = LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.y), -0.24f, 0.015f, 1f, 1f, "Faixa");
        Solid(plate, "Faixa", strip, LavaLetalArt.Hazard, LavaLetalArt.Hazard, center, 0f);
    }

    public static void FloatingBlock(Transform root, string name, Vector3 center, Vector3 size, float yaw)
    {
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.z), -size.y * 0.5f, size.y * 0.5f,
                                     LavaLetalArt.RockTile, 1f, name);
        Solid(root, name, mesh, LavaLetalArt.RockBurnt, LavaLetalArt.Dark, center, yaw);
    }

    /// <summary>
    /// Estrutura escura e retangular com luzes pequenas na face: a "fábrica"
    /// ao longe da referência, reduzida ao que se lê a 80 m de distância.
    /// </summary>
    public static void DarkTower(Transform root, string name, Vector3 basePosition, Vector3 size, float yaw, int seed)
    {
        var tower = Solid(root, name, LowPolyMesh.Prism(LowPolyMesh.Rectangle(size.x, size.z), kBedrock, size.y,
                                                        LavaLetalArt.RockTile * 2f, 1f, name),
                          LavaLetalArt.Dark, LavaLetalArt.Dark, basePosition, yaw);

        var lights = new[] { LavaLetalArt.GlowRed, LavaLetalArt.GlowYellow, LavaLetalArt.GlowCyan };
        // Semente fixa, e não o hash do nome: o hash de string do .NET muda a
        // cada execução, e as luzes trocariam de lugar a cada reconstrução.
        var random = new System.Random(seed);

        for (int i = 0; i < 7; i++)
        {
            float y = size.y * (0.25f + 0.65f * (float)random.NextDouble());
            float x = (float)(random.NextDouble() - 0.5) * size.x * 0.7f;
            var lamp = LowPolyMesh.Prism(LowPolyMesh.Rectangle(1.2f, 0.3f), -0.4f, 0.4f, 1f, 1f, "Luz");

            Solid(tower.transform, "Luz", lamp, lights[i % lights.Length], lights[i % lights.Length],
                  new Vector3(x, y, -size.z * 0.5f - 0.1f), 0f);
        }
    }

    // ------------------------------------------------------------------
    // Montagem
    // ------------------------------------------------------------------

    public static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static GameObject Solid(Transform parent, string name, Mesh mesh, Material top, Material sides,
                            Vector3 localPosition, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, sides };

        // Tudo aqui é cenário que não se mexe: estático para o batching.
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    public static void Decor(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition) =>
        Solid(parent, name, mesh, material, material, localPosition, 0f);

    /// <summary>
    /// Colisor convexo da própria malha. Convexo porque a malha tem uma dúzia
    /// de faces, e o polígono sorteado é quase convexo - a diferença na borda
    /// é de centímetros, e colisor convexo é o barato e o estável.
    /// </summary>
    public static void AddMeshCollider(GameObject go)
    {
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = true;
    }

    public static Vector2[] Flare(Vector2[] ring)
    {
        var wide = new Vector2[ring.Length];
        for (int i = 0; i < ring.Length; i++) wide[i] = ring[i] * kFlare;
        return wide;
    }

    public static Vector2[] World(Transform t, Vector2[] local)
    {
        var world = new Vector2[local.Length];
        for (int i = 0; i < local.Length; i++)
        {
            var p = t.TransformPoint(new Vector3(local[i].x, 0f, local[i].y));
            world[i] = new Vector2(p.x, p.z);
        }

        return world;
    }

    public static Vector2[] Square(float x, float z, float size)
    {
        var ring = LowPolyMesh.Rectangle(size, size);
        for (int i = 0; i < ring.Length; i++) ring[i] += new Vector2(x, z);
        return ring;
    }

    public static void Register(string name, float top, Vector2[] worldPolygon) => Footprints[name] = (top, worldPolygon);

    /// <summary>A peça na posição de construção e, se ela anda, na outra ponta.</summary>
    public static List<(float top, Vector2[] poly)> Variants(string name, (float top, Vector2[] poly) start)
    {
        var list = new List<(float, Vector2[])> { start };
        if (FarEnds.TryGetValue(name, out var far)) list.Add(far);
        return list;
    }

    /// <summary>Classifica um salto: 0 folgado, 1 no limite, 2 pede duplo, 3 impossível.</summary>
    public static (int rank, string text, float gap, float dy, float single, float twin) Judge(JumpModel jump, float gap, float dy)
    {
        float need = gap + 0.8f;
        float single = jump.Reach(dy, null);
        float twin = jump.Reach(dy, 0.5f);

        if (single > 0f && need <= 0.75f * single) return (0, "simples, folgado", gap, dy, single, twin);
        if (single > 0f && need <= 0.95f * single) return (1, "simples, no limite", gap, dy, single, twin);
        if (need <= 0.85f * twin) return (2, "PEDE PULO DUPLO", gap, dy, single, twin);
        return (3, "IMPOSSÍVEL", gap, dy, single, twin);
    }

    /// <summary>Salto que sai da beira de uma ilha, na direção de outra.</summary>
    public static (bool, float) FromIsland(JumpModel jump, string from, string to, float speed,
                                    string[] bubbles, float bob, float? doubleAt = null)
    {
        if (!Footprints.TryGetValue(from, out var a) || !Footprints.TryGetValue(to, out var b))
            return (false, 0f);

        var origin = Centroid(a.poly);
        var direction = (Centroid(b.poly) - origin).normalized;
        var start = origin + direction * RayHits(a.poly, origin, direction).max;

        return Fly(jump, start, a.top, direction, speed, b, bubbles, bob, true, doubleAt);
    }

    /// <summary>
    /// Salto que sai de uma rampa a uma velocidade de deslize, da ponta de
    /// baixo ou <paramref name="early"/> metros antes dela (mais alto, no
    /// trecho de 34 graus).
    /// </summary>
    public static (bool, float) FromRamp(JumpModel jump, string ramp, string to, float speed,
                                  string[] bubbles, float bob, bool jumps, float? doubleAt = null,
                                  float early = 0f)
    {
        if (!RampLips.TryGetValue(ramp, out var lip) || !Footprints.TryGetValue(to, out var b))
            return (false, 0f);

        var start = lip.xz - lip.forward * early;
        float height = lip.y + early * Mathf.Tan(kRampAngle * Mathf.Deg2Rad);
        return Fly(jump, start, height, lip.forward, speed, b, bubbles, bob, jumps, doubleAt);
    }

    /// <summary>
    /// Voa em linha reta a partir de <paramref name="start"/> e diz se pousa
    /// no topo de <paramref name="target"/>. As bolhas valem quando a bruxa
    /// passa a menos do raio do gatilho do centro delas, como no AirBubble.
    /// </summary>
    public static (bool landed, float d) Fly(JumpModel jump, Vector2 start, float y0, Vector2 direction, float speed,
                                      (float top, Vector2[] poly) target, string[] bubbles, float bob,
                                      bool jumps, float? doubleAt)
    {
        const float dt = 1f / 240f;
        const float trigger = LevelKit.BubbleRadius + 0.2f;

        var hits = RayHits(target.poly, start, direction);
        float landFrom = hits.min + 0.3f, landTo = hits.max - 0.3f;

        var points = new List<(float d, float y)>();
        foreach (var name in bubbles)
        {
            if (!Bubbles.TryGetValue(name, out var c)) continue;
            points.Add((Vector2.Dot(new Vector2(c.x, c.z) - start, direction), c.y + bob));
        }

        float v = jumps ? Mathf.Sqrt(2f * jump.Gravity * jump.Jump) : 0f;
        float y = y0, d = 0f, t = 0f;
        var used = new bool[points.Count];
        bool doubled = !doubleAt.HasValue;

        while (t < 8f && y > 0f)
        {
            if (!doubled && t >= doubleAt.Value)
            {
                v = Mathf.Sqrt(2f * jump.Gravity * jump.DoubleJump);
                doubled = true;
            }

            float scale = v < 0f ? jump.Fall : 1f;
            if (Mathf.Abs(v) < jump.ApexThreshold) scale *= jump.Apex;

            v -= jump.Gravity * scale * dt;
            float previous = y;
            y += v * dt;
            d += speed * dt;
            t += dt;

            for (int i = 0; i < points.Count; i++)
            {
                if (used[i] || new Vector2(d - points[i].d, y - points[i].y).magnitude > trigger) continue;

                v = Mathf.Max(v, Mathf.Sqrt(2f * jump.Gravity * BubbleHeight));
                used[i] = true;
            }

            if (v < 0f && previous > target.top && y <= target.top && d >= landFrom && d <= landTo)
                return (true, d);
        }

        return (false, d);
    }

    /// <summary>Altura do quique da bolha, a mesma do AirBubble.</summary>
    public const float BubbleHeight = 4.5f;

    public static Vector2 Centroid(Vector2[] poly)
    {
        var sum = Vector2.zero;
        foreach (var p in poly) sum += p;
        return sum / poly.Length;
    }

    /// <summary>Onde o raio cruza o contorno: a primeira e a última distância.</summary>
    public static (float min, float max) RayHits(Vector2[] poly, Vector2 origin, Vector2 direction)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var e = poly[(i + 1) % poly.Length] - a;
            float denominator = direction.x * e.y - direction.y * e.x;
            if (Mathf.Abs(denominator) < 1e-6f) continue;

            var w = a - origin;
            float t = (w.x * e.y - w.y * e.x) / denominator;
            float u = (w.x * direction.y - w.y * direction.x) / denominator;
            if (u < 0f || u > 1f) continue;

            min = Mathf.Min(min, t);
            max = Mathf.Max(max, t);
        }

        return (min, max);
    }

    /// <summary>Arco do pulo, com a mesma gravidade em faixas do PlayerController.</summary>
    public struct JumpModel
    {
        public float Speed, Gravity, Fall, ApexThreshold, Apex, Jump, DoubleJump;

        /// <summary>Distância horizontal até pousar numa superfície <paramref name="dy"/> acima.</summary>
        public float Reach(float dy, float? secondJumpAt)
        {
            const float dt = 1f / 240f;
            float v = Mathf.Sqrt(2f * Gravity * Jump), y = 0f, t = 0f;
            bool used = false;

            while (t < 6f)
            {
                if (secondJumpAt.HasValue && !used && t >= secondJumpAt.Value)
                {
                    v = Mathf.Sqrt(2f * Gravity * DoubleJump);
                    used = true;
                }

                float scale = v < 0f ? Fall : 1f;
                if (Mathf.Abs(v) < ApexThreshold) scale *= Apex;

                v -= Gravity * scale * dt;
                y += v * dt;
                t += dt;

                if (v < 0f && y <= dy) return Speed * t;
            }

            return 0f;
        }
    }

    public static float Gap(Vector2[] a, Vector2[] b)
    {
        float best = float.MaxValue;
        for (int pass = 0; pass < 2; pass++)
        {
            var from = pass == 0 ? a : b;
            var to = pass == 0 ? b : a;

            foreach (var point in from)
            for (int i = 0; i < to.Length; i++)
                best = Mathf.Min(best, SegmentDistance(point, to[i], to[(i + 1) % to.Length]));
        }

        return best;
    }

    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
