using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static VolcanicKit;

/// <summary>
/// Fase 4, a última: Rampa Vulcânica - um escorregador de pedra sobre a lava,
/// no espírito do escorregador de gelo do Super Mario 64.
///
/// A pista é uma faixa contínua gerada ao longo de um traçado: retas e curvas,
/// cada uma com inclinação e largura próprias, e vãos onde a superfície some de
/// verdade. As transições de inclinação e largura são graduais (3 m), para não
/// haver degrau nem quina que lance a bruxa sem querer.
///
/// Quase tudo é deslize. De 25 graus para cima a bruxa escorrega sozinha até
/// 15 m/s e só consegue virar e pular; a pista fica entre 28 e 30 graus do
/// começo ao fim, inclusive os pousos depois dos vãos - quem pula cai já
/// escorregando de novo. As exceções são o patamar da largada, as bordas
/// dos vãos (5 a 6 graus: o deslize continua nelas, mas a beira fica no
/// nível certo para o salto) e a chegada.
///
/// 28 graus, e não 26, por margem: o deslize acaba no primeiro quadro fora do
/// chão (um esbarrão na mureta, uma emenda) e só recomeça lendo 25 graus ou
/// mais sob os pés. Nas curvas a malha torce um pouco entre uma amostra e a
/// outra, e a 26 a leitura oscilava em volta do limite - num teste a bruxa
/// saiu do deslize e terminou a rota correndo.
///
/// Sem poder frear, cada perigo é lido de longe e respondido com o rumo ou
/// com um pulo: pilares e barreiras baixas no meio da pista, bolas mais
/// lentas para ultrapassar, uma bola que cruza o corredor sempre no mesmo
/// instante da passagem e outra que persegue a bruxa no fim.
///
/// Cinco segmentos: largada, primeiros buracos, corredor das bolas,
/// bifurcação (rota segura em S ou atalho reto com dois vãos e uma hélice) e
/// a descida final com uma bola em perseguição até a chegada.
/// </summary>
public static class RampLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_RampaVulcanica.unity";
    public const string SceneName = "Level_RampaVulcanica";

    // Provisórios: ~300 m de pista quase toda a 15 m/s, uns 21 s sem erro.
    // Recalibrar lendo o save depois de jogar.
    static readonly float[] kRanks = { 28f, 35f, 44f, 58f, 80f };

    const float kStartHeight = 192f;
    const float kThickness = 1.2f;
    const float kRimHeight = 1f;
    const float kRimWidth = 0.6f;
    const float kStep = 0.5f;

    // ------------------------------------------------------------------
    // Traçado
    // ------------------------------------------------------------------

    enum Kind { Straight, Arc, Gap }

    /// <summary>Um trecho do traçado. Ângulo positivo vira para a direita.</summary>
    struct Piece
    {
        public string Name;
        public Kind Kind;
        public float Length, Radius, Angle, Slope, Width, Drop;

        public static Piece Straight(string name, float length, float slope, float width) =>
            new Piece { Name = name, Kind = Kind.Straight, Length = length, Slope = slope, Width = width };

        public static Piece Arc(string name, float radius, float angle, float slope, float width) =>
            new Piece { Name = name, Kind = Kind.Arc, Radius = radius, Angle = angle, Slope = slope, Width = width,
                        Length = radius * Mathf.Abs(angle) * Mathf.Deg2Rad };

        public static Piece Gap(float length, float drop) =>
            new Piece { Kind = Kind.Gap, Length = length, Drop = drop };
    }

    struct Sample
    {
        public Vector3 Position;   // meio da pista, na superfície
        public float Heading, Width, Slope, S;

        public Vector3 Forward => Dir(Heading);
        public Vector3 Right => RightOf(Heading);
        public Vector3 At(float lateral) => Position + Right * lateral;
    }

    sealed class Run
    {
        public string Name;
        public bool EndsAtGap, StartsAfterGap;
        public readonly List<Sample> Samples = new List<Sample>();
        public float Length => Samples.Count > 0 ? Samples[Samples.Count - 1].S : 0f;
        public Sample First => Samples[0];
        public Sample Last => Samples[Samples.Count - 1];
    }

    struct Cursor
    {
        public Vector3 Position;
        public float Heading, Slope, Width;
    }

    /// <summary>Onde cada trecho com nome caiu: em que faixa e entre que distâncias.</summary>
    static readonly Dictionary<string, (Run run, float s0, float s1)> s_Pieces =
        new Dictionary<string, (Run, float, float)>();

    static readonly List<Run> s_Runs = new List<Run>();

    static Vector3 Dir(float heading)
    {
        float a = heading * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
    }

    static Vector3 RightOf(float heading)
    {
        float a = heading * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
    }

    /// <summary>
    /// Assenta os trechos a partir do cursor e devolve o cursor no fim.
    ///
    /// Inclinação e largura chegam ao valor do trecho em 3 m, partindo do que
    /// vinha antes. Um vão encerra a faixa atual; a seguinte começa do outro
    /// lado, mais baixa.
    /// </summary>
    static Cursor Lay(Cursor cursor, params Piece[] pieces)
    {
        Run run = null;
        bool afterGap = false;

        foreach (var piece in pieces)
        {
            if (piece.Kind == Kind.Gap)
            {
                cursor.Position += Dir(cursor.Heading) * piece.Length + Vector3.down * piece.Drop;
                if (run != null) run.EndsAtGap = true;
                run = null;
                afterGap = true;
                continue;
            }

            if (run == null)
            {
                run = new Run { Name = piece.Name, StartsAfterGap = afterGap };
                run.Samples.Add(new Sample { Position = cursor.Position, Heading = cursor.Heading,
                                             Width = piece.Width, Slope = cursor.Slope, S = 0f });
                s_Runs.Add(run);
                // Uma faixa nova começa já na largura do trecho: não há de onde estreitar.
                cursor.Width = piece.Width;
            }

            float startSlope = cursor.Slope, startWidth = cursor.Width, s0 = run.Length;
            int steps = Mathf.Max(1, Mathf.CeilToInt(piece.Length / kStep));
            float ds = piece.Length / steps;
            float turn = piece.Kind == Kind.Arc ? piece.Angle / steps : 0f;

            for (int k = 1; k <= steps; k++)
            {
                float blend = Mathf.Clamp01(k * ds / 3f);
                cursor.Slope = Mathf.Lerp(startSlope, piece.Slope, blend);
                cursor.Width = Mathf.Lerp(startWidth, piece.Width, blend);

                float mid = cursor.Heading + turn * 0.5f;
                cursor.Heading += turn;
                cursor.Position += Dir(mid) * ds + Vector3.down * (Mathf.Tan(cursor.Slope * Mathf.Deg2Rad) * ds);

                run.Samples.Add(new Sample { Position = cursor.Position, Heading = cursor.Heading,
                                             Width = cursor.Width, Slope = cursor.Slope, S = run.Length + ds });
            }

            if (!string.IsNullOrEmpty(piece.Name)) s_Pieces[piece.Name] = (run, s0, run.Length);
        }

        return cursor;
    }

    /// <summary>
    /// Assenta uma rota que precisa terminar numa altura exata - os dois
    /// ramos da bifurcação, que se reencontram no mesmo patamar. Acha a
    /// inclinação por bissecção, e só então assenta de verdade.
    /// </summary>
    static Cursor LayToHeight(Cursor cursor, float targetY, System.Func<float, Piece[]> pieces)
    {
        float low = 0f, high = 55f;
        for (int i = 0; i < 30; i++)
        {
            float mid = (low + high) * 0.5f;
            float y = Simulate(cursor, pieces(mid));
            if (y > targetY) low = mid; else high = mid;
        }

        return Lay(cursor, pieces((low + high) * 0.5f));
    }

    static float Simulate(Cursor cursor, Piece[] pieces)
    {
        int runs = s_Runs.Count;
        var saved = new Dictionary<string, (Run, float, float)>(s_Pieces);
        var end = Lay(cursor, pieces);
        s_Runs.RemoveRange(runs, s_Runs.Count - runs);
        s_Pieces.Clear();
        foreach (var pair in saved) s_Pieces[pair.Key] = pair.Value;
        return end.Position.y;
    }

    /// <summary>Ponto da pista num trecho com nome, a uma distância dentro dele e a um afastamento do meio.</summary>
    static Sample At(string piece, float distance)
    {
        var (run, s0, s1) = s_Pieces[piece];
        float s = Mathf.Clamp(s0 + distance, 0f, run.Length);

        var samples = run.Samples;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].S < s) continue;
            var a = samples[i - 1];
            var b = samples[i];
            float t = Mathf.InverseLerp(a.S, b.S, s);
            return new Sample
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Heading = Mathf.LerpAngle(a.Heading, b.Heading, t),
                Width = Mathf.Lerp(a.Width, b.Width, t),
                Slope = Mathf.Lerp(a.Slope, b.Slope, t),
                S = s,
            };
        }

        return samples[samples.Count - 1];
    }

    static float LengthOf(string piece) => s_Pieces[piece].s1 - s_Pieces[piece].s0;

    // ------------------------------------------------------------------
    // Construção
    // ------------------------------------------------------------------

    /// <summary>Trechos sem borda: (trecho, lado -1 esquerdo / +1 direito, de, até).</summary>
    static readonly List<(string piece, int side, float from, float to)> s_RimGaps =
        new List<(string, int, float, float)>();

    [MenuItem("Rewind Run/Criar Fase 4: Rampa Vulcânica", priority = 10)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        KayKitMaterialSetup.Apply();
        BeginLevel();
        s_Runs.Clear();
        s_Pieces.Clear();
        s_RimGaps.Clear();

        CreateAtmosphere();
        LayCourse();

        var level = new GameObject("Fase").transform;
        DeclareRimGaps();
        CreateTrack(Group(level, "Pista"));
        CreateStart(Group(level, "1_Largada"));
        CreateHoles(Group(level, "2_PrimeirosBuracos"));
        CreateBubbleCrossing(Group(level, "2b_TravessiaDeBolhas"));
        CreateCorridor(Group(level, "3_CorredorDasBolas"));
        CreateFork(Group(level, "4_Bifurcacao"));
        CreateFinal(Group(level, "5_RetaFinal"));
        CreateScenery(Group(level, "Cenario"));

        var spawn = At("Largada", 4f);
        var player = LevelKit.AssembleSystems(kRanks);
        if (player != null)
            player.transform.SetPositionAndRotation(spawn.Position + Vector3.up * 0.3f,
                                                    Quaternion.Euler(0f, spawn.Heading, 0f));
        CreateCamera(spawn, new GameObject("ZonasDeCamera").transform);

        var middle = (s_Runs[0].First.Position + s_Runs[s_Runs.Count - 1].Last.Position) * 0.5f;
        HazardSetup.EnsureLava(new Vector3(middle.x, 0f, middle.z), 400f, 500f, surfaceHeight: 0f);
        HazardSetup.EnsureRespawn();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        LevelKit.AddSceneToBuildSettings(ScenePath);

        ValidateJumps();
        Debug.Log($"Fase 4 criada em {ScenePath}. Pista de {TotalLength():0} m (rota segura), " +
                  $"da altura {kStartHeight:0} até {At("Chegada", 0f).Position.y:0.0}. " +
                  $"Ranks S-D: {string.Join(" / ", kRanks)} s (provisórios).");
    }

    static float TotalLength()
    {
        float total = 0f;
        foreach (var run in s_Runs)
            if (!run.Name.StartsWith("Atalho")) total += run.Length;
        return total;
    }

    /// <summary>
    /// O traçado inteiro. Inclinação em graus; largura em metros.
    ///
    /// Os vãos são medidos contra o pulo real em <see cref="ValidateJumps"/>:
    /// os de 5, 7,5 e 8 m pedem pulo (deslizando para fora sem pular, a bruxa
    /// bate na face do outro lado), e o de 16 m do atalho, que cai 10 m, pede
    /// o pulo do deslize bem dado ou o duplo.
    /// </summary>
    static void LayCourse()
    {
        var cursor = new Cursor { Position = new Vector3(0f, kStartHeight, 0f), Heading = 0f, Slope = 0f, Width = 12f };

        cursor = Lay(cursor,
            // 1 - Início: só o patamar é plano; o deslize começa logo.
            Piece.Straight("Largada", 10f, 0f, 12f),
            Piece.Straight("Inicio", 12f, 29f, 12f),
            Piece.Arc("CurvaInicial", 30f, 30f, CurveSlope(30f, 12f), 12f),
            Piece.Straight("PrimeiroDeslize", 12f, 30f, 11f),

            // 2 - Primeiros buracos. Os pousos são rampa: cai-se deslizando.
            Piece.Straight("Borda1", 5f, 6f, 11f),
            Piece.Gap(5f, 1f),
            Piece.Straight("Pouso1", 8f, 28f, 13f),
            Piece.Arc("CurvaDosPilares", 24f, -50f, CurveSlope(24f, 11f), 11f),
            Piece.Straight("RetaDosPilares", 10f, 30f, 10f),
            Piece.Straight("Borda2", 5f, 5f, 10f),
            Piece.Gap(7.5f, 1.2f),
            Piece.Straight("Pouso2", 8f, 28f, 14f),

            // Travessia de bolhas: sem chão, quicando de bolha em bolha.
            Piece.Straight("BordaBolhas", 5f, 5f, 14f),
            Piece.Gap(kBubbleGap, kBubbleDrop),
            Piece.Straight("PousoBolhas", 10f, 28f, 14f),

            // 3 - Corredor das bolas, deslizando.
            Piece.Straight("Corredor", 40f, 28f, 15f),

            // 4 - Patamar da bifurcação, ainda na descida.
            Piece.Straight("Bifurcacao", 7f, 28f, 18f));

        // Os dois ramos saem do patamar, 4,5 m para cada lado do meio, e se
        // reencontram no mesmo ponto à frente, na altura que o S largo atinge
        // na inclinação de curva. O atalho, mais curto, desce o mesmo tanto: perde 20 m nos
        // dois vãos - o segundo é um salto para baixo - e o resto numa calha
        // mais íngreme que a pista.
        const float offset = 4.5f, radius = 18f, swing = 65f;
        float safeLength = radius * 4f * swing * Mathf.Deg2Rad;
        float drop = safeLength * Mathf.Tan(CurveSlope(radius, 7f) * Mathf.Deg2Rad);
        float targetY = cursor.Position.y - drop;
        float reach = radius * Mathf.Sin(swing * Mathf.Deg2Rad) * 4f;   // avanço do S

        var right = RightOf(cursor.Heading);
        var safeStart = cursor;
        safeStart.Position += right * offset;
        safeStart.Width = 7f;

        LayToHeight(safeStart, targetY, slope => new[]
        {
            // Rota segura: um S largo para a direita, mais longo e com um
            // gêiser no meio.
            Piece.Arc("Segura1", radius, swing, slope, 7f),
            Piece.Arc("Segura2", radius, -2f * swing, slope, 7f),
            Piece.Arc("Segura3", radius, swing, slope, 7f),
        });

        var shortStart = cursor;
        shortStart.Position -= right * offset;
        shortStart.Width = 6f;

        var shortEnd = LayToHeight(shortStart, targetY, slope => new[]
        {
            // Atalho: calha reta, estreita e íngreme, com dois vãos e uma
            // hélice entre eles.
            Piece.Straight("Atalho1", 10f, slope, 6f),
            Piece.Straight("AtalhoBorda1", 4f, 6f, 6f),
            Piece.Gap(6f, 4f),
            Piece.Straight("Atalho2", 9f, slope, 6f),
            Piece.Straight("AtalhoBorda2", 4f, 6f, 6f),
            Piece.Gap(16f, 16f),
            Piece.Straight("Atalho3", reach - 49f, slope, 6f),
        });

        var merge = shortEnd;
        merge.Position += right * offset;

        Lay(merge,
            // 5 - Reta final: deslize, curva com pilares, último vão e chegada.
            Piece.Straight("Reencontro", 7f, 28f, 18f),
            Piece.Straight("DescidaFinal", 15f, 30f, 11f),
            Piece.Arc("CurvaFinal", 26f, 40f, CurveSlope(26f, 11f), 11f),
            Piece.Straight("Borda3", 5f, 5f, 11f),
            Piece.Gap(8f, 1.5f),
            Piece.Straight("Chegada", 16f, 0f, 18f));
    }

    /// <summary>
    /// Inclinação de uma curva para a beira de fora não ficar abaixo de
    /// <paramref name="minOuter"/> graus.
    ///
    /// Numa curva a pista é uma rampa em espiral: a altura cai igual dos dois
    /// lados, mas a beira de dentro percorre menos chão que a de fora. Dentro
    /// fica mais íngreme, fora mais suave. Na S da rota segura, a 28 graus no
    /// meio, a beira de fora dava 24 - abaixo dos 25 do deslize - e quem
    /// saía do deslize por ali não voltava mais a escorregar.
    /// </summary>
    static float CurveSlope(float radius, float width, float minOuter = 27f)
    {
        float outer = radius + width * 0.5f;
        return Mathf.Atan(Mathf.Tan(minOuter * Mathf.Deg2Rad) * outer / radius) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Onde a borda some: nas entradas e saídas das bolas, que atravessam a
    /// pista vindas de fora dela.
    /// </summary>
    static void DeclareRimGaps()
    {
        s_RimGaps.Add(("Corredor", -1, 0f, 12f));     // calha das bolas de faixa
        s_RimGaps.Add(("Corredor", -1, 34f, 40f));    // saída da faixa esquerda
        s_RimGaps.Add(("Corredor", 1, 34f, 40f));     // saída da faixa direita
        s_RimGaps.Add(("Corredor", 1, 20f, 27f));     // entrada da bola que cruza
        s_RimGaps.Add(("Corredor", -1, 24f, 33f));    // saída da bola que cruza
    }

    // ------------------------------------------------------------------
    // Pista
    // ------------------------------------------------------------------

    static void CreateTrack(Transform root)
    {
        foreach (var run in s_Runs)
        {
            var go = Solid(root, run.Name, BuildRibbon(run), LavaLetalArt.RockBrown, LavaLetalArt.RockSide,
                           Vector3.zero, 0f);
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

            foreach (int side in new[] { -1, 1 })
            {
                var rim = BuildRim(run, side);
                if (rim == null) continue;
                var r = Solid(go.transform, side < 0 ? "BordaEsquerda" : "BordaDireita", rim,
                              LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, Vector3.zero, 0f);
                r.AddComponent<MeshCollider>().sharedMesh = rim;
            }

            // Faixas de perigo nas duas beiras de cada vão.
            if (run.EndsAtGap) Stripe(go.transform, run.Last, -0.35f);
            if (run.StartsAfterGap) Stripe(go.transform, run.First, 0.35f);

            Supports(go.transform, run);
        }
    }

    /// <summary>
    /// A faixa da pista: tampo, laterais até a espessura e fundo. O corte é
    /// horizontal (sem inclinação lateral), então a bruxa nunca escorrega de
    /// lado - só para a frente, para onde a pista desce.
    /// </summary>
    static Mesh BuildRibbon(Run run)
    {
        var builder = new MeshBuilder();
        var down = Vector3.down * kThickness;
        var samples = run.Samples;

        for (int i = 0; i + 1 < samples.Count; i++)
        {
            var a = samples[i];
            var b = samples[i + 1];
            Vector3 al = a.At(-a.Width * 0.5f), ar = a.At(a.Width * 0.5f);
            Vector3 bl = b.At(-b.Width * 0.5f), br = b.At(b.Width * 0.5f);

            builder.Quad(0, al, bl, br, ar, Vector3.up);
            builder.Quad(1, al, al + down, bl + down, bl, -a.Right);
            builder.Quad(1, ar, br, br + down, ar + down, a.Right);
            builder.Quad(1, al + down, ar + down, br + down, bl + down, Vector3.down);
        }

        var f = samples[0];
        var l = samples[samples.Count - 1];
        builder.Quad(1, f.At(-f.Width * 0.5f), f.At(f.Width * 0.5f), f.At(f.Width * 0.5f) + down,
                     f.At(-f.Width * 0.5f) + down, -f.Forward);
        builder.Quad(1, l.At(-l.Width * 0.5f), l.At(-l.Width * 0.5f) + down, l.At(l.Width * 0.5f) + down,
                     l.At(l.Width * 0.5f), l.Forward);

        return builder.ToMesh(run.Name);
    }

    /// <summary>Mureta de pedra numa lateral da faixa, com as aberturas declaradas.</summary>
    static Mesh BuildRim(Run run, int side)
    {
        var builder = new MeshBuilder();
        var samples = run.Samples;
        var up = Vector3.up * kRimHeight;
        var down = Vector3.down * kThickness;
        bool any = false;
        int start = -1;

        for (int i = 0; i < samples.Count; i++)
        {
            bool open = IsRimOpen(run, side, samples[i].S);
            if (!open && start < 0) start = i;

            bool close = start >= 0 && (open || i == samples.Count - 1);
            if (!close) continue;

            int end = open ? i - 1 : i;
            if (end > start)
            {
                any = true;
                for (int k = start; k < end; k++)
                {
                    var a = samples[k];
                    var b = samples[k + 1];
                    Vector3 ai = a.At(side * a.Width * 0.5f), bi = b.At(side * b.Width * 0.5f);
                    Vector3 ao = ai + a.Right * (side * kRimWidth), bo = bi + b.Right * (side * kRimWidth);
                    Vector3 outward = a.Right * side;

                    builder.Quad(0, ai + up, bi + up, bo + up, ao + up, Vector3.up);
                    builder.Quad(1, ai, bi, bi + up, ai + up, -outward);
                    builder.Quad(1, ao + down, ao + up, bo + up, bo + down, outward);
                }

                // Tampas nas pontas de cada trecho de mureta.
                foreach (var (k, facing) in new[] { (start, -1f), (end, 1f) })
                {
                    var c = samples[k];
                    Vector3 ci = c.At(side * c.Width * 0.5f);
                    Vector3 co = ci + c.Right * (side * kRimWidth);
                    builder.Quad(1, ci, ci + up, co + up, co + down, c.Forward * facing);
                }
            }

            start = -1;
        }

        return any ? builder.ToMesh(run.Name + "_Borda") : null;
    }

    static bool IsRimOpen(Run run, int side, float s)
    {
        foreach (var gap in s_RimGaps)
        {
            if (gap.side != side || !s_Pieces.TryGetValue(gap.piece, out var piece) || piece.run != run) continue;
            if (s >= piece.s0 + gap.from && s <= piece.s0 + gap.to) return true;
        }

        return false;
    }

    /// <summary>Faixa de perigo atravessada na pista, deitada na inclinação dela.</summary>
    static void Stripe(Transform parent, Sample at, float along)
    {
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Rectangle(at.Width, 0.5f), -0.05f, 0.02f, 1f, 1f, "Faixa");
        var go = Solid(parent, "FaixaDoVao", mesh, LavaLetalArt.Hazard, LavaLetalArt.Hazard,
                       at.Position + at.Forward * along, 0f);
        go.transform.rotation = Quaternion.Euler(at.Slope, at.Heading, 0f);
    }

    /// <summary>Colunas de basalto sustentando a pista, da lava até embaixo dela.</summary>
    static void Supports(Transform parent, Run run)
    {
        for (float s = 6f; s < run.Length - 2f; s += 14f)
        {
            var at = SampleOf(run, s);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = at.At(side * (at.Width * 0.5f - 1.2f));
                float top = p.y - kThickness;
                if (top < 2f) continue;

                var mesh = LowPolyMesh.Prism(LowPolyMesh.Regular(6, 0.9f, 30f), kBedrock, top, LavaLetalArt.RockTile, 0.85f, "Coluna");
                var column = Solid(parent, "Coluna", mesh, LavaLetalArt.RockSide, LavaLetalArt.RockSide,
                                   new Vector3(p.x, 0f, p.z), 0f);
                column.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    static Sample SampleOf(Run run, float s)
    {
        foreach (var pair in s_Pieces)
            if (pair.Value.run == run) return At(pair.Key, s - pair.Value.s0);
        return run.First;
    }

    // ------------------------------------------------------------------
    // Segmentos
    // ------------------------------------------------------------------

    /// <summary>
    /// 1 - Início: blocos baixos (0,9 m) alternando de lado já no primeiro
    /// deslize. Deslizando não dá para frear: ou se mira o vão entre eles, ou
    /// se pula - pular do deslize guarda a velocidade.
    ///
    /// Pular um bloco leva a bruxa uns 13 a 15 m adiante. Todo bloco que corta
    /// o meio da pista fica a pelo menos 18 m da beira do vão seguinte: perto
    /// disso, quem pulasse o bloco cairia direto no buraco.
    /// </summary>
    static void CreateStart(Transform root)
    {
        LowBlock(root, "Bloco1", At("Inicio", 8f), -3.5f, 4f);
        LowBlock(root, "Bloco2", At("CurvaInicial", 4f), 3.5f, 4f);
        LowBlock(root, "Bloco3", At("CurvaInicial", 12f), -2.5f, 5f);

        LevelKit.Place(root, "neutral", "signage_arrows_left", At("Largada", 8f).At(5f), At("Largada", 8f).Heading + 180f);
    }

    /// <summary>
    /// 2 - Primeiros buracos: um vão de 5 m que cai numa rampa, a curva com
    /// três pilares desencontrados (deslizando, só dá para virar) e o vão de
    /// 7,5 m. Sem bloco na reta: ela é curta demais antes do vão.
    /// </summary>
    static void CreateHoles(Transform root)
    {
        Pillar(root, "Pilar1", At("CurvaDosPilares", 7f), -2.5f);
        Pillar(root, "Pilar2", At("CurvaDosPilares", 13f), 2.5f);
        Pillar(root, "Pilar3", At("CurvaDosPilares", 19f), -1.5f);
    }

    /// <summary>
    /// Bolhas da travessia: metros à frente da borda, desvio lateral e altura
    /// do centro em relação à borda.
    /// </summary>
    static readonly Vector3[] kBubbles =
    {
        new Vector3(0f, 1f, 10f),
        new Vector3(2.5f, -2f, 21f),
        new Vector3(-2.5f, -5f, 32f),
        new Vector3(2.5f, -8f, 43f),
        new Vector3(0f, -11f, 54f),
    };

    // Da borda ao pouso: 9 m depois da última bolha e 5,5 m abaixo dela.
    const float kBubbleGap = 63f, kBubbleDrop = 16.5f;

    /// <summary>
    /// Travessia de bolhas: a mecânica da Torre de Bolhas, descendo.
    ///
    /// A rampa acaba numa borda e do outro lado só há lava, 63 m de vão e
    /// cinco bolhas em zigue-zague. Nem o pulo duplo alcança o pouso; só
    /// encadeando os quiques. Cada bolha fica 11 m à frente e 3 m abaixo da
    /// anterior: quicando, a bruxa sobe 4,5 m e cai 7,5, o que dá 1,13 s no
    /// ar - 12,4 m correndo a 11 m/s e 15 m embalada pelo deslize. A conta
    /// fecha com folga para quem corre e pede frear no ar para quem vem
    /// rápido demais: a travessia cobra o controle fino do salto, não
    /// velocidade.
    ///
    /// A primeira bolha está 10 m depois da borda e um pouco acima dela, ao
    /// alcance do pulo saindo do deslize. Errou, volta no tempo: as bolhas
    /// estouradas voltam junto. Depois da última, o pouso é rampa de novo, e
    /// o deslize recomeça direto no corredor.
    /// </summary>
    static void CreateBubbleCrossing(Transform root)
    {
        var lip = At("BordaBolhas", LengthOf("BordaBolhas"));
        for (int i = 0; i < kBubbles.Length; i++)
        {
            var b = kBubbles[i];
            var position = lip.Position + lip.Forward * b.z + lip.Right * b.x + Vector3.up * b.y;
            LevelKit.Bubble(root, position, i * 0.2f).name = $"Bolha_Travessia{i + 1}";
        }

        LevelKit.Place(root, "neutral", "signage_arrows_left", lip.At(-6f) + lip.Forward * -2f, lip.Heading + 180f);
    }

    /// <summary>
    /// 3 - Corredor das bolas: 40 m a 28 graus, 15 m de largura, deslizando.
    ///
    /// Duas bolas descem por faixas, saindo de uma calha à esquerda do topo,
    /// mais devagar que o deslize (5 m/s acelerando até 9): a bruxa vem atrás
    /// e as ultrapassa, escolhendo o lado - pelo meio sempre sobra espaço.
    ///
    /// A terceira cruza o corredor na diagonal, da direita para a esquerda,
    /// disparada pela própria bruxa ao entrar nele. Como a velocidade do
    /// deslize é sempre a mesma, ela chega sempre no mesmo instante: entra pela
    /// direita bem quando a bruxa passa por ali. Pela esquerda passa-se na
    /// frente dela; pela direita, só pulando. Errou, volta no tempo e tenta
    /// pelo outro lado - o encontro se repete igual.
    ///
    /// A calha fica à vista desde o patamar de pouso: a bola aparece na boca
    /// da caverna, rola pela calha e só então entra na pista.
    /// </summary>
    static void CreateCorridor(Transform root)
    {
        float w = At("Corredor", 0f).Width;
        const float r = 1.6f;

        foreach (var (name, lane, phase) in new[] { ("Bola_FaixaEsquerda", -4f, 0f), ("Bola_FaixaDireita", 4f, 0.5f) })
        {
            float exitSide = Mathf.Sign(lane);
            var path = new List<Vector3>
            {
                Surface(At("Corredor", 1f), -w * 0.5f - 10f, r) + Vector3.up * 5f,
                Surface(At("Corredor", 6f), -w * 0.5f - 1f, r),
                Surface(At("Corredor", 10f), lane, r),
                Surface(At("Corredor", 34f), lane, r),
                Surface(At("Corredor", 38f), exitSide * (w * 0.5f + 2f), r),
                Surface(At("Corredor", 40f), exitSide * (w * 0.5f + 4f), r) + Vector3.down * 10f,
            };
            Boulder(root, name, path, r, 5f, 1.5f, 9f, true, 5f, phase, default);
        }

        Chute(root, "Calha", Surface(At("Corredor", 1f), -w * 0.5f - 10f, r) + Vector3.up * 5f,
              Surface(At("Corredor", 6f), -w * 0.5f - 1f, r), r);
        Cave(root, "Caverna", Surface(At("Corredor", 0f), -w * 0.5f - 12f, 0f) + Vector3.up * 4f, At("Corredor", 0f).Heading + 90f);

        var cross = new List<Vector3>
        {
            Surface(At("Corredor", 18f), w * 0.5f + 9f, r) + Vector3.up * 4f,
            Surface(At("Corredor", 23f), w * 0.5f, r),
            Surface(At("Corredor", 29f), -w * 0.5f, r),
            Surface(At("Corredor", 31f), -w * 0.5f - 4f, r) + Vector3.down * 10f,
        };
        var entry = At("Corredor", 6f);
        var crossTrigger = new Bounds(entry.Position + Vector3.up * 2f, new Vector3(w + 6f, 9f, 6f));
        Boulder(root, "Bola_Cruzada", cross, 1.8f, 9f, 0f, 9f, false, 0f, 0f, crossTrigger);
        Chute(root, "CalhaCruzada", cross[0], cross[1], 1.8f);
        Cave(root, "CavernaCruzada", Surface(At("Corredor", 17f), w * 0.5f + 11f, 0f) + Vector3.up * 3f,
             At("Corredor", 17f).Heading - 90f);
    }

    /// <summary>
    /// 4 - Bifurcação. Uma rocha no meio do patamar separa as duas entradas.
    ///
    /// Rota segura (direita): um S largo, mais longo e com um gêiser no meio
    /// dele - o gêiser deixa passar por um lado ou pede esperar o ciclo.
    ///
    /// Atalho (esquerda): calha reta, estreita (6 m) e mais íngreme que a pista,
    /// com um vão de 6 m, uma hélice e um salto de 16 m para baixo (cai 10 m),
    /// que só fecha com o pulo do deslize bem dado ou o duplo. Corta uns 16 m
    /// de pista e o gêiser.
    /// </summary>
    static void CreateFork(Transform root)
    {
        var hub = At("Bifurcacao", LengthOf("Bifurcacao"));
        Divider(root, "Divisor", hub, -0.25f);
        var middle = At("Segura2", LengthOf("Segura2") * 0.5f);
        Geyser(root, "Geiser_RotaSegura", middle.At(-1.2f) + Vector3.up * 0.05f, idleSeconds: 2.4f, phase: 0f);

        var blade = At("Atalho2", 4.5f);
        LevelKit.Blade(root, blade.Position, 70f, 0f).name = "Helice_Atalho";

        LevelKit.Place(root, "neutral", "signage_arrows_left", hub.At(-7f) + hub.Forward * -1.5f, hub.Heading + 180f);
    }

    /// <summary>
    /// 5 - Reta final. Ao atravessar o patamar do reencontro, a bola que
    /// estava parada no meio dele começa a descer atrás da bruxa (6 m/s,
    /// acelerando até 12,5 - abaixo dos 15 do deslize, e tão perto dos 11 da
    /// corrida que quem caiu do deslize ainda chega antes dela). Quem segue em frente
    /// abre distância; quem bate numa barreira, ou erra o último vão e volta
    /// no tempo, sente ela chegar. Uma barreira na descida, dois pilares na
    /// curva e, no fim, o vão de 8 m e a chegada larga com a bandeira.
    /// </summary>
    static void CreateFinal(Transform root)
    {
        const float r = 1.7f;
        // Espera num poleiro atrás do patamar, entre as chegadas dos dois
        // ramos - fora do caminho. Parada no meio da pista, ela matava quem
        // cortava para o centro depois da bifurcação, antes mesmo de rolar.
        var mergeStart = At("Reencontro", 0f);
        var perch = Surface(mergeStart, 0f, r) - mergeStart.Forward * 3.5f + Vector3.up * 2.5f;
        var path = new List<Vector3> { perch };
        for (float s = 1.5f; s <= LengthOf("Reencontro"); s += 2f) path.Add(Surface(At("Reencontro", s), 0f, r));
        for (float s = 0f; s <= LengthOf("DescidaFinal"); s += 3f) path.Add(Surface(At("DescidaFinal", s), 0f, r));
        for (float s = 0f; s <= LengthOf("CurvaFinal"); s += 2f) path.Add(Surface(At("CurvaFinal", s), 0f, r));
        for (float s = 0f; s <= LengthOf("Borda3"); s += 2.5f) path.Add(Surface(At("Borda3", s), 0f, r));
        var lip = At("Borda3", LengthOf("Borda3"));
        path.Add(Surface(lip, 0f, r) + lip.Forward * 3f + Vector3.down * 2f);
        path.Add(Surface(lip, 0f, r) + lip.Forward * 6f + Vector3.down * 12f);

        var merge = At("Reencontro", 0f);
        var center = At("Reencontro", 6f).Position + Vector3.up * 2f;
        var trigger = new Bounds(center, new Vector3(merge.Width + 4f, 6f, 7f));
        // A caixa é alinhada aos eixos; o patamar corre quase em +Z, então
        // basta girar o tamanho se ele estiver de lado.
        if (Mathf.Abs(Mathf.Sin(merge.Heading * Mathf.Deg2Rad)) > 0.7f)
            trigger.size = new Vector3(trigger.size.z, trigger.size.y, trigger.size.x);

        Boulder(root, "Bola_Perseguicao", path, r, 6f, 3f, 12.5f, false, 0f, 0f, trigger);
        Decor(root, "Poleiro", LowPolyMesh.Prism(LowPolyMesh.Regular(6, 1.8f, 0f), -6f, 0f, LavaLetalArt.RockTile, 0.7f),
              LavaLetalArt.RockBurnt, perch + Vector3.down * r);

        LowBlock(root, "Bloco6", At("DescidaFinal", 8f), -2f, 6f);
        Pillar(root, "PilarFinal1", At("CurvaFinal", 7f), 3f);
        Pillar(root, "PilarFinal2", At("CurvaFinal", 14f), -3f);

        var finish = At("Chegada", 9f);
        LevelKit.Place(root, "green", "flag_A", finish.Position + finish.Forward * 4f, finish.Heading);
        LevelKit.Place(root, "neutral", "signage_finish", finish.At(-4f) + finish.Forward * -3f, finish.Heading + 180f);
        LevelKit.Place(root, "neutral", "pillar_2x2x8", finish.At(6f) + finish.Forward * 4f);
        LevelKit.Place(root, "neutral", "pillar_2x2x8", finish.At(6f) + finish.Forward * 4f + Vector3.up * 8f);
        LevelKit.Place(root, "green", "flag_A", finish.At(6f) + finish.Forward * 4f + Vector3.up * 16f);
        Decor(root, "Farol", LowPolyMesh.Prism(LowPolyMesh.Regular(4, 0.9f, 45f), 16f, 17.4f, 1f),
              LavaLetalArt.GlowYellow, finish.At(6f) + finish.Forward * 4f);

        var go = new GameObject("FimDaFase");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(finish.Position + Vector3.up * 1.5f, Quaternion.Euler(0f, finish.Heading, 0f));
        var area = go.AddComponent<BoxCollider>();
        area.size = new Vector3(finish.Width, 3f, 12f);
        area.isTrigger = true;
        go.AddComponent<LevelFinish>();
    }

    /// <summary>
    /// Cenário: rochas baixas na lava, longe da pista, e o vulcão ao fundo.
    /// Nada alto perto: a câmera anda atrás e acima da bruxa.
    /// </summary>
    static void CreateScenery(Transform root)
    {
        var a = s_Runs[0].First.Position;
        var b = s_Runs[s_Runs.Count - 1].Last.Position;
        var side = Vector3.Cross(Vector3.up, (b - a).normalized);

        Rock(root, "Solta_A", (a + side * 45f).x, (a + side * 45f).z, 3f, 6f, 7, 501, LavaLetalArt.RockBrown, route: false);
        Rock(root, "Solta_B", (Vector3.Lerp(a, b, 0.4f) - side * 50f).x, (Vector3.Lerp(a, b, 0.4f) - side * 50f).z,
             2.5f, 5f, 6, 502, LavaLetalArt.RockBurnt, route: false);
        Rock(root, "Solta_C", (Vector3.Lerp(a, b, 0.7f) + side * 55f).x, (Vector3.Lerp(a, b, 0.7f) + side * 55f).z,
             3.5f, 4.5f, 6, 503, LavaLetalArt.RockGray, route: false);

        // O vulcão de onde a pista parece descer: atrás da largada.
        var behind = a - (b - a).normalized * 70f;
        var volcano = Solid(root, "Vulcao", LowPolyMesh.Prism(LowPolyMesh.Regular(9, 45f, 10f), kBedrock, 75f,
                                                           LavaLetalArt.RockTile * 3f, 0.3f, "Vulcao"),
                            LavaLetalArt.Ember, LavaLetalArt.RockSide, new Vector3(behind.x, 0f, behind.z), 0f);
        volcano.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var towerA = Vector3.Lerp(a, b, 0.55f) + side * 80f;
        var towerB = Vector3.Lerp(a, b, 0.25f) - side * 85f;
        DarkTower(root, "Torre_Escura_A", new Vector3(towerA.x, 0f, towerA.z), new Vector3(12f, 30f, 10f), 20f, 521);
        DarkTower(root, "Torre_Escura_B", new Vector3(towerB.x, 0f, towerB.z), new Vector3(10f, 24f, 14f), -15f, 522);
    }

    // ------------------------------------------------------------------
    // Peças
    // ------------------------------------------------------------------

    /// <summary>Centro de uma bola encostada na pista naquele ponto.</summary>
    static Vector3 Surface(Sample at, float lateral, float radius) =>
        at.At(lateral) + Vector3.up * (radius / Mathf.Cos(at.Slope * Mathf.Deg2Rad));

    /// <summary>Bloco baixo (0,9 m), atravessado na pista e deitado na inclinação: pula-se ou contorna-se.</summary>
    static void LowBlock(Transform root, string name, Sample at, float lateral, float width)
    {
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Rectangle(width, 1.2f), -0.4f, 0.9f, LavaLetalArt.RockTile, 1f, name);
        var go = Solid(root, name, mesh, LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, at.At(lateral), 0f);
        go.transform.rotation = Quaternion.Euler(at.Slope, at.Heading, 0f);
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.25f, 0f);
        box.size = new Vector3(width, 1.3f, 1.2f);
    }

    /// <summary>Pilar de pedra em pé na pista: 3,5 m de altura, 1 m de raio.</summary>
    static void Pillar(Transform root, string name, Sample at, float lateral)
    {
        var p = at.At(lateral);
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Regular(6, 1f, 30f), -1.5f, 3.5f, LavaLetalArt.RockTile, 0.8f, name);
        var go = Solid(root, name, mesh, LavaLetalArt.RockGray, LavaLetalArt.RockSide, p, 0f);
        AddMeshCollider(go);
    }

    /// <summary>Rocha que separa as duas bocas da bifurcação.</summary>
    static void Divider(Transform root, string name, Sample at, float lateral)
    {
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Regular(5, 1.5f, 0f), -2f, 2.2f, LavaLetalArt.RockTile, 0.7f, name);
        var go = Solid(root, name, mesh, LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, at.At(lateral), at.Heading);
        AddMeshCollider(go);
    }

    /// <summary>Calha de pedra por onde a bola chega à pista. Só imagem.</summary>
    static void Chute(Transform root, string name, Vector3 from, Vector3 to, float radius)
    {
        var a = from + Vector3.down * radius;
        var b = to + Vector3.down * radius;
        float length = Vector3.Distance(a, b);
        var mesh = LowPolyMesh.Prism(LowPolyMesh.Rectangle(radius * 2.4f, length), -0.8f, 0f, LavaLetalArt.RockTile, 1f, name);
        var go = Solid(root, name, mesh, LavaLetalArt.RockBurnt, LavaLetalArt.RockSide, (a + b) * 0.5f, 0f);
        go.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
    }

    /// <summary>Boca de caverna de onde as bolas saem: um bloco escuro com a abertura acesa.</summary>
    static void Cave(Transform root, string name, Vector3 position, float yaw)
    {
        FloatingBlock(root, name, position, new Vector3(7f, 7f, 6f), yaw);
        Decor(root, name + "_Boca", LowPolyMesh.Prism(LowPolyMesh.Regular(8, 2.2f, 22.5f), -0.1f, 0.1f, 1f),
              LavaLetalArt.Ember, position + Vector3.down * 3.45f);
    }

    /// <summary>
    /// Uma bola gigante: rocha poliédrica com veios de brasa, gatilho letal
    /// esférico (85% do raio, para raspão não matar) e o RollingBoulder.
    /// </summary>
    static void Boulder(Transform root, string name, List<Vector3> path, float radius, float speed, float acceleration,
                        float maxSpeed, bool looping, float period, float phase, Bounds trigger)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = path[0];

        var visual = Solid(go.transform, "Rocha", BoulderMesh(radius, Seed(name)), LavaLetalArt.RockBurnt,
                           LavaLetalArt.Ember, Vector3.zero, 0f);
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var area = go.AddComponent<SphereCollider>();
        area.radius = radius * 0.85f;
        area.isTrigger = true;
        LevelKit.AddKinematicBody(go);

        var boulder = go.AddComponent<RollingBoulder>();
        var so = new SerializedObject(boulder);
        var points = so.FindProperty("path");
        points.arraySize = path.Count;
        for (int i = 0; i < path.Count; i++) points.GetArrayElementAtIndex(i).vector3Value = path[i];
        so.FindProperty("radius").floatValue = radius;
        so.FindProperty("speed").floatValue = speed;
        so.FindProperty("acceleration").floatValue = acceleration;
        so.FindProperty("maxSpeed").floatValue = maxSpeed;
        so.FindProperty("looping").boolValue = looping;
        so.FindProperty("period").floatValue = Mathf.Max(0.5f, period);
        so.FindProperty("phase").floatValue = phase;
        so.FindProperty("trigger").boundsValue = trigger;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("hazard").objectReferenceValue = area;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Semente fixa a partir do nome. O GetHashCode de string do .NET muda a
    /// cada execução, e a bola mudaria de forma a cada reconstrução.
    /// </summary>
    static int Seed(string name)
    {
        int seed = 17;
        foreach (char c in name) seed = seed * 31 + c;
        return seed & 0x7fffffff;
    }

    /// <summary>
    /// Rocha redonda low poly: icosaedro subdividido uma vez, vértices
    /// sorteados entre 90% e 100% do raio e faces planas. Uma face em cada
    /// seis vai para a submalha de brasa.
    /// </summary>
    static Mesh BoulderMesh(float radius, int seed)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var points = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        var faces = new List<int[]>
        {
            new[] {0, 11, 5}, new[] {0, 5, 1}, new[] {0, 1, 7}, new[] {0, 7, 10}, new[] {0, 10, 11},
            new[] {1, 5, 9}, new[] {5, 11, 4}, new[] {11, 10, 2}, new[] {10, 7, 6}, new[] {7, 1, 8},
            new[] {3, 9, 4}, new[] {3, 4, 2}, new[] {3, 2, 6}, new[] {3, 6, 8}, new[] {3, 8, 9},
            new[] {4, 9, 5}, new[] {2, 4, 11}, new[] {6, 2, 10}, new[] {8, 6, 7}, new[] {9, 8, 1},
        };

        var cache = new Dictionary<long, int>();
        int Middle(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add((points[a] + points[b]) * 0.5f);
            cache[key] = points.Count - 1;
            return points.Count - 1;
        }

        var split = new List<int[]>();
        foreach (var f in faces)
        {
            int ab = Middle(f[0], f[1]), bc = Middle(f[1], f[2]), ca = Middle(f[2], f[0]);
            split.Add(new[] { f[0], ab, ca });
            split.Add(new[] { f[1], bc, ab });
            split.Add(new[] { f[2], ca, bc });
            split.Add(new[] { ab, bc, ca });
        }

        var random = new System.Random(seed);
        for (int i = 0; i < points.Count; i++)
            points[i] = points[i].normalized * (radius * (0.9f + 0.1f * (float)random.NextDouble()));

        var builder = new MeshBuilder();
        for (int i = 0; i < split.Count; i++)
        {
            var f = split[i];
            Vector3 a = points[f[0]], b = points[f[1]], c = points[f[2]];
            builder.Triangle(i % 6 == 0 ? 1 : 0, a, b, c, (a + b + c).normalized);
        }

        return builder.ToMesh("Bola");
    }

    // ------------------------------------------------------------------
    // Ambiente
    // ------------------------------------------------------------------

    static void CreateAtmosphere()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetPositionAndRotation(new Vector3(0f, 90f, 0f), Quaternion.Euler(42f, -30f, 0f));

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.74f, 0.55f);
        light.shadows = LightShadows.Soft;

        LavaLetalArt.ApplyAtmosphere();
    }

    const float kCameraPitch = 50f, kCameraDistance = 20f;

    /// <summary>
    /// A mesma câmera das outras fases: o modo Fixa, sem mouse, com ângulo e
    /// distância constantes. Como a pista vira, zonas de câmera ao longo dela
    /// giram a vista para continuar olhando morro abaixo - uma a cada trecho
    /// em que o rumo muda 20 graus. O ângulo é mais alto que os 26-34 das
    /// fases planas porque a pista desce a 30 graus: com 34, a câmera ficaria
    /// a 4 graus da superfície, rente ao chão; com 50, fica a 20 acima dela,
    /// como nas fases planas.
    /// </summary>
    static void CreateCamera(Sample spawn, Transform zones)
    {
        var camera = Object.FindAnyObjectByType<OrbitCamera>();
        if (camera == null) return;

        LevelKit.SetFloat(camera, "fixedYaw", spawn.Heading);
        LevelKit.SetFloat(camera, "fixedPitch", kCameraPitch);
        LevelKit.SetFloat(camera, "fixedDistance", kCameraDistance);

        var rotation = Quaternion.Euler(kCameraPitch, spawn.Heading, 0f);
        camera.transform.SetPositionAndRotation(
            spawn.Position + Vector3.up * 1.4f - rotation * Vector3.forward * kCameraDistance, rotation);

        int count = 0;
        foreach (var run in s_Runs)
        {
            int first = 0;
            for (int i = 1; i < run.Samples.Count; i++)
            {
                bool last = i == run.Samples.Count - 1;
                if (!last && Mathf.Abs(Mathf.DeltaAngle(run.Samples[first].Heading, run.Samples[i].Heading)) < 20f) continue;
                CameraZoneAlong(zones, $"{run.Name}_{count++}", run.Samples[first], run.Samples[i]);
                first = i;
            }
        }
    }

    /// <summary>Zona que cobre a pista entre duas amostras, olhando no rumo médio delas.</summary>
    static void CameraZoneAlong(Transform root, string name, Sample from, Sample to)
    {
        float yaw = Mathf.LerpAngle(from.Heading, to.Heading, 0.5f);
        var a = from.Position;
        var b = to.Position;
        var flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
        float width = Mathf.Max(from.Width, to.Width) + 2f;

        var center = (a + b) * 0.5f + Vector3.up * 3f;
        var size = new Vector3(width, Mathf.Abs(a.y - b.y) + 10f, flat.magnitude + 1f);
        var zone = KayKitPlacer.CreateCameraZone(root, $"Zona_{name}", new Bounds(center, size),
                                                 yaw, kCameraPitch, kCameraDistance);
        if (flat.sqrMagnitude > 0.0001f) zone.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    // ------------------------------------------------------------------
    // Validação dos saltos
    // ------------------------------------------------------------------

    /// <summary>
    /// Cada vão contra o pulo real da bruxa (ver
    /// <see cref="VolcanicKit.JumpReport"/>): saindo da borda na direção da
    /// pista, pousando nos primeiros 16 m do outro lado.
    ///
    /// 13,4 m/s é o deslize de 15 depois dos 5 m de borda plana (a fricção
    /// tira uns 5 m/s por segundo ali); 11 é correndo.
    /// </summary>
    static void ValidateJumps()
    {
        var report = JumpReport.Begin("Fase 4");
        if (report == null) return;
        var jump = report.Jump;

        report.Section("Vãos da rota principal");
        foreach (var (lip, landing) in new[] { ("Borda1", "Pouso1"), ("Borda2", "Pouso2"), ("Borda3", "Chegada") })
        {
            report.Expect($"{lip} -> {landing} pulando a 13,4 m/s", true, Leap(jump, lip, landing, 13.4f, true, null));
            report.Expect($"{lip} -> {landing} pulando correndo (11 m/s)", true, Leap(jump, lip, landing, jump.Speed, true, null));
            report.Expect($"{lip} -> {landing} deslizando para fora sem pular", false, Leap(jump, lip, landing, 13.4f, false, null));
        }

        report.Expect("BordaBolhas -> PousoBolhas sem as bolhas, nem com o duplo", false,
                      Leap(jump, "BordaBolhas", "PousoBolhas", 13.4f, true, 0.5f));

        report.Section("Atalho da bifurcação");
        report.Expect("AtalhoBorda1 -> Atalho2 (6 m) deslizando", true, Leap(jump, "AtalhoBorda1", "Atalho2", 13.4f, true, null));
        report.Expect("AtalhoBorda1 -> Atalho2 sem pular", false, Leap(jump, "AtalhoBorda1", "Atalho2", 13.4f, false, null));
        report.Expect("AtalhoBorda2 -> Atalho3 (16 m, cai 10) deslizando, com pulo duplo", true,
                      Leap(jump, "AtalhoBorda2", "Atalho3", 13.4f, true, 0.5f));
        report.Expect("AtalhoBorda2 -> Atalho3 deslizando para fora sem pular", false,
                      Leap(jump, "AtalhoBorda2", "Atalho3", 13.4f, false, null));
        var single = Leap(jump, "AtalhoBorda2", "Atalho3", 13.4f, true, null);
        report.Note($"AtalhoBorda2 -> Atalho3 só com o pulo simples, deslizando: " +
                    (single.landed ? $"pousa a {single.d:0.0} m (no limite)" : "não alcança - pede o duplo"));
        report.Note($"Inclinação da calha do atalho: {At("Atalho2", 4f).Slope:0} graus; da rota segura: {At("Segura2", 4f).Slope:0}",
                    At("Atalho2", 4f).Slope > 42f);

        var safe = LengthOf("Segura1") + LengthOf("Segura2") + LengthOf("Segura3");
        var shortcut = LengthOf("Atalho1") + LengthOf("AtalhoBorda1") + LengthOf("Atalho2") + LengthOf("AtalhoBorda2")
                       + LengthOf("Atalho3") + 22f;
        report.Note($"Rota segura {safe:0.0} m, atalho {shortcut:0.0} m: o atalho corta {safe - shortcut:0.0} m");

        // Inclinação das duas beiras de cada trecho de deslize, medida na
        // própria malha: nas curvas a de fora é mais suave que o meio.
        report.Section("Deslize");
        var sliding = new[] { "Inicio", "CurvaInicial", "PrimeiroDeslize", "Pouso1", "CurvaDosPilares", "RetaDosPilares",
                              "Pouso2", "PousoBolhas", "Corredor", "Bifurcacao", "Segura1", "Segura2", "Segura3", "Atalho1", "Atalho2",
                              "Atalho3", "Reencontro", "DescidaFinal", "CurvaFinal" };
        float gentlest = 90f; string where = "";
        foreach (var piece in sliding)
        {
            // Os primeiros 3 m de cada trecho são a transição do anterior.
            for (float d = 3f; d + 0.5f <= LengthOf(piece); d += 0.5f)
            {
                var a = At(piece, d);
                var b = At(piece, d + 0.5f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 pa = a.At(side * a.Width * 0.5f), pb = b.At(side * b.Width * 0.5f);
                    float run = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
                    float slope = Mathf.Atan2(pa.y - pb.y, Mathf.Max(run, 0.001f)) * Mathf.Rad2Deg;
                    if (slope < gentlest) { gentlest = slope; where = $"{piece} a {d:0} m, beira {(side < 0 ? "esquerda" : "direita")}"; }
                }
            }
        }
        report.Note($"Beira mais suave de um trecho de deslize: {gentlest:0.0} graus ({where}); o deslize pede 25",
                    gentlest < 25.5f);

        // Os dois ramos precisam acabar exatamente na boca do patamar, cada um
        // do seu lado: um degrau ou uma fresta ali seria uma queda sem motivo.
        var reencontro = At("Reencontro", 0f);
        foreach (var (branch, side) in new[] { ("Segura3", 1f), ("Atalho3", -1f) })
        {
            var end = At(branch, LengthOf(branch)).Position;
            var expected = reencontro.At(side * 4.5f);
            float miss = Vector3.Distance(end, expected);
            report.Note($"{branch} chega ao reencontro a {miss * 100f:0} cm do lugar certo", miss > 0.15f);
        }

        report.Finish();
    }

    /// <summary>Salto da borda de um trecho para o começo do seguinte.</summary>
    static (bool landed, float d) Leap(JumpModel jump, string lipPiece, string landingPiece, float speed, bool jumps,
                                       float? doubleAt)
    {
        var lip = At(lipPiece, LengthOf(lipPiece));
        // Pouso nos primeiros 16 m da faixa do outro lado - passar do primeiro
        // trecho e cair no seguinte, que continua a pista, também é pousar.
        var land = At(landingPiece, 0f);
        var run = s_Pieces[landingPiece].run;
        var far = SampleOf(run, Mathf.Min(s_Pieces[landingPiece].s0 + 16f, run.Length));

        var poly = new[]
        {
            Flat(land.At(-land.Width * 0.5f)), Flat(far.At(-far.Width * 0.5f)),
            Flat(far.At(far.Width * 0.5f)), Flat(land.At(land.Width * 0.5f)),
        };

        var forward = Flat(lip.Forward).normalized;
        var start = Flat(lip.Position) - forward * 0.3f;
        return Fly(jump, start, lip.Position.y, forward, speed, (land.Position.y, poly), new string[0], 0f, jumps, doubleAt);
    }

    static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    // ------------------------------------------------------------------
    // Teste
    // ------------------------------------------------------------------

    /// <summary>
    /// O meio da rota, ponto a ponto (1 m), para um teste automatizado
    /// descer a fase: em cada ponto, se é a borda de um vão (pular ali) e se
    /// o vão pede o pulo duplo. Não monta nada na cena.
    /// </summary>
    public static List<(Vector3 point, bool jump, bool twin)> TestRoute(bool shortcut)
    {
        s_Runs.Clear();
        s_Pieces.Clear();
        LayCourse();

        var order = new List<string> { "Largada", "Inicio", "CurvaInicial", "PrimeiroDeslize", "Borda1", "Pouso1",
                                       "CurvaDosPilares", "RetaDosPilares", "Borda2", "Pouso2", "BordaBolhas", "PousoBolhas",
                                       "Corredor",
                                       "Bifurcacao" };
        order.AddRange(shortcut ? new[] { "Atalho1", "AtalhoBorda1", "Atalho2", "AtalhoBorda2", "Atalho3" }
                                : new[] { "Segura1", "Segura2", "Segura3" });
        order.AddRange(new[] { "Reencontro", "DescidaFinal", "CurvaFinal", "Borda3", "Chegada" });

        // Barreiras que cruzam o meio da pista: o teste pula 2 m antes delas.
        var hurdles = new[] { ("CurvaInicial", 12f), ("DescidaFinal", 8f) };

        var route = new List<(Vector3, bool, bool)>();
        foreach (var piece in order)
        {
            float length = LengthOf(piece);
            bool lip = piece.StartsWith("Borda") || piece.StartsWith("AtalhoBorda");
            for (float d = 0f; d < length; d += 1f)
            {
                bool hurdle = System.Array.Exists(hurdles, h => h.Item1 == piece && Mathf.Abs(h.Item2 - 2f - d) < 0.5f);
                route.Add((At(piece, d).Position, hurdle, false));
            }
            route.Add((At(piece, length).Position, lip, piece == "AtalhoBorda2"));
        }

        return route;
    }

    // ------------------------------------------------------------------
    // Malha
    // ------------------------------------------------------------------

    /// <summary>
    /// Monta malhas de faces planas em duas submalhas. Cada face recebe os
    /// próprios vértices (sombreamento chapado, como o resto do jogo) e a
    /// ordem dos vértices é corrigida pela normal pedida, para a face nunca
    /// sair virada para dentro.
    /// </summary>
    sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int>[] _triangles = { new List<int>(), new List<int>() };

        public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            Triangle(submesh, a, b, c, normal);
            Triangle(submesh, a, c, d, normal);
        }

        public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-8f) return;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) (b, c) = (c, b);

            int index = _vertices.Count;
            foreach (var v in new[] { a, b, c })
            {
                _vertices.Add(v);
                _uvs.Add(Project(v, normal) / LavaLetalArt.RockTile);
            }

            _triangles[submesh].Add(index);
            _triangles[submesh].Add(index + 1);
            _triangles[submesh].Add(index + 2);
        }

        static Vector2 Project(Vector3 v, Vector3 n)
        {
            n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (n.y >= n.x && n.y >= n.z) return new Vector2(v.x, v.z);
            return n.x >= n.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(_triangles[0], 0);
            mesh.SetTriangles(_triangles[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
