using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Malhas low-poly geradas por código: prismas de base poligonal.
///
/// Existe porque as peças do KayKit são todas retângulos, e uma ilha vulcânica
/// precisa de silhueta irregular - cinco, sete lados, raios desiguais. Um
/// prisma cobre ilha, coluna, placa (com quatro lados) e até o vulcão do fundo
/// (com o topo estreitado).
///
/// Duas submalhas: 0 é o topo, 1 são as laterais. É o que permite a borda de
/// pedra escura com o topo em outra cor usando um objeto só.
///
/// UV em metros, não de 0 a 1 por face: com o tamanho real no UV, a textura
/// fica do mesmo tamanho numa ilha de 3 m e numa de 20 m, em vez de esticar.
/// Arestas duplicadas de propósito, para cada face sair com a normal própria -
/// o sombreado facetado é o que dá o ar de Nintendo 64.
/// </summary>
public static class LowPolyMesh
{
    /// <summary>
    /// Prisma com base no polígono dado (em XZ, ao redor da origem).
    /// </summary>
    /// <param name="ring">Vértices da base, em ordem ao redor do centro.</param>
    /// <param name="bottom">Altura da base.</param>
    /// <param name="top">Altura do topo.</param>
    /// <param name="metersPerTile">Tamanho de uma repetição da textura.</param>
    /// <param name="topScale">Abaixo de 1 estreita o topo, como num cone.</param>
    public static Mesh Prism(IList<Vector2> ring, float bottom, float top, float metersPerTile,
                             float topScale = 1f, string name = "Prisma")
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var topTris = new List<int>();
        var sideTris = new List<int>();

        Vector2 center = Vector2.zero;
        foreach (var p in ring) center += p;
        center /= ring.Count;

        Vector2 TopPoint(Vector2 p) => center + (p - center) * topScale;

        // --- topo: leque a partir do centro ---------------------------------
        int centerIndex = vertices.Count;
        vertices.Add(new Vector3(center.x, top, center.y));
        uvs.Add(center / metersPerTile);

        for (int i = 0; i < ring.Count; i++)
        {
            var p = TopPoint(ring[i]);
            vertices.Add(new Vector3(p.x, top, p.y));
            uvs.Add(p / metersPerTile);
        }

        for (int i = 0; i < ring.Count; i++)
        {
            int a = centerIndex + 1 + i;
            int b = centerIndex + 1 + (i + 1) % ring.Count;
            Triangle(topTris, vertices, centerIndex, a, b, Vector3.up);
        }

        // --- laterais: um quadrilátero por aresta ---------------------------
        float perimeter = 0f;
        for (int i = 0; i < ring.Count; i++)
        {
            var p0 = ring[i];
            var p1 = ring[(i + 1) % ring.Count];
            var q0 = TopPoint(p0);
            var q1 = TopPoint(p1);

            float length = Vector2.Distance(p0, p1);
            int start = vertices.Count;

            vertices.Add(new Vector3(p0.x, bottom, p0.y));
            vertices.Add(new Vector3(p1.x, bottom, p1.y));
            vertices.Add(new Vector3(q1.x, top, q1.y));
            vertices.Add(new Vector3(q0.x, top, q0.y));

            float u0 = perimeter / metersPerTile;
            float u1 = (perimeter + length) / metersPerTile;
            uvs.Add(new Vector2(u0, bottom / metersPerTile));
            uvs.Add(new Vector2(u1, bottom / metersPerTile));
            uvs.Add(new Vector2(u1, top / metersPerTile));
            uvs.Add(new Vector2(u0, top / metersPerTile));
            perimeter += length;

            var mid = (p0 + p1) * 0.5f - center;
            var outward = new Vector3(mid.x, 0f, mid.y);

            Triangle(sideTris, vertices, start, start + 3, start + 2, outward);
            Triangle(sideTris, vertices, start, start + 2, start + 1, outward);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTris, 0);
        mesh.SetTriangles(sideTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Retângulo centrado na origem, para placas e blocos.</summary>
    public static Vector2[] Rectangle(float sizeX, float sizeZ)
    {
        float x = sizeX * 0.5f, z = sizeZ * 0.5f;
        return new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(x, z), new Vector2(-x, z) };
    }

    /// <summary>Polígono regular, com um giro inicial em graus.</summary>
    public static Vector2[] Regular(int sides, float radius, float rotationDegrees = 0f)
    {
        var ring = new Vector2[sides];
        float start = rotationDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < sides; i++)
        {
            float a = start + i * Mathf.PI * 2f / sides;
            ring[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        return ring;
    }

    /// <summary>
    /// Polígono de rocha: ângulos regulares, raios sorteados entre
    /// <paramref name="minFraction"/> e 1.
    ///
    /// Só o raio varia, nunca o ângulo. Assim o polígono sempre contém o
    /// polígono regular no raio mínimo - e é esse limite que a validação dos
    /// saltos usa, então o vão medido nunca é menor que o real.
    /// </summary>
    public static Vector2[] Rock(int sides, float radius, float minFraction, System.Random random)
    {
        var ring = new Vector2[sides];
        float start = (float)random.NextDouble() * Mathf.PI * 2f;

        for (int i = 0; i < sides; i++)
        {
            float a = start + i * Mathf.PI * 2f / sides;
            float r = radius * Mathf.Lerp(minFraction, 1f, (float)random.NextDouble());
            ring[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        return ring;
    }

    /// <summary>
    /// Acrescenta o triângulo com a face virada para <paramref name="wanted"/>.
    ///
    /// Decidir a ordem dos vértices pela normal desejada, em vez de confiar na
    /// ordem do polígono, é o que deixa as funções acima aceitarem anéis em
    /// qualquer sentido sem sair com faces invisíveis.
    /// </summary>
    static void Triangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 wanted)
    {
        var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);

        triangles.Add(a);
        if (Vector3.Dot(normal, wanted) >= 0f)
        {
            triangles.Add(b);
            triangles.Add(c);
        }
        else
        {
            triangles.Add(c);
            triangles.Add(b);
        }
    }
}
