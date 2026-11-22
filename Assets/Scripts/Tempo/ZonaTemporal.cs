using System.Collections.Generic;
using UnityEngine;

/// <summary>Que tipo de elemento uma zona temporal desacelera.</summary>
[System.Flags]
public enum CanalTemporal
{
    Nenhum = 0,
    Plataformas = 1 << 0,
    Obstaculos = 1 << 1,
    Bolas = 1 << 2,
    Tudo = ~0,
}

/// <summary>
/// Zona de Âmbar: uma esfera onde o tempo do mundo corre mais devagar.
///
/// Desacelera o cenário, não a bruxa. É essa diferença que vira ferramenta:
/// uma hélice rápida demais para passar fica lenta dentro da zona, e a bruxa,
/// na velocidade dela, atravessa. Se a zona desacelerasse a bruxa junto, nada
/// mudaria na relação entre os dois.
///
/// Não mexe no Time.timeScale. Cada elemento que pode ser desacelerado tem um
/// <see cref="RelogioLocal"/>, que pergunta aqui quanto o tempo vale onde ele
/// está. Assim pausa, rewind e zonas não brigam: a pausa zera o deltaTime de
/// todos, o rewind toca a história gravada de cada relógio, e a zona só muda
/// o passo de quem está dentro dela.
///
/// A identidade visual é oposta à do rewind de propósito. O rewind é azul,
/// rápido, com a imagem distorcida; a zona é âmbar - a cor do inseto preso no
/// tempo -, imóvel, com anéis girando devagar. São duas mecânicas diferentes e
/// o jogador precisa distinguir de relance qual está vendo.
///
/// A borda tem uma faixa de transição: quem cruza a zona muda de velocidade
/// aos poucos, e uma plataforma que entra nela não dá um tranco.
/// </summary>
public class ZonaTemporal : MonoBehaviour
{
    [Tooltip("Raio da esfera de influência, em metros.")]
    [SerializeField, Min(0.5f)] float raio = 6f;

    [Tooltip("Quanto o tempo vale dentro da zona: 0,25 é um quarto da velocidade normal.")]
    [SerializeField, Range(0.05f, 1f)] float escala = 0.25f;

    [Tooltip("O que esta zona desacelera. Uma zona só de obstáculos deixa as plataformas na velocidade normal.")]
    [SerializeField] CanalTemporal canais = CanalTemporal.Tudo;

    [Tooltip("Largura da faixa da borda onde a velocidade muda aos poucos, em metros.")]
    [SerializeField, Min(0f)] float transicao = 1.2f;

    static readonly List<ZonaTemporal> s_Zonas = new List<ZonaTemporal>();

    public float Raio => raio;
    public float Escala => escala;

    void OnEnable() => s_Zonas.Add(this);

    void OnDisable() => s_Zonas.Remove(this);

    /// <summary>
    /// Quanto o tempo vale num ponto para um tipo de elemento. Com várias
    /// zonas sobrepostas vale a mais lenta: somar desacelerações daria um
    /// tempo quase parado que ninguém pôs ali de propósito.
    /// </summary>
    public static float EscalaEm(Vector3 ponto, CanalTemporal canal)
    {
        float resultado = 1f;
        for (int i = 0; i < s_Zonas.Count; i++)
        {
            var zona = s_Zonas[i];
            if ((zona.canais & canal) == 0) continue;

            float distancia = Vector3.Distance(ponto, zona.transform.position);
            if (distancia >= zona.raio) continue;

            // 0 no miolo, 1 na borda de fora.
            float borda = zona.transicao > 0f
                ? Mathf.Clamp01((distancia - (zona.raio - zona.transicao)) / zona.transicao)
                : 0f;
            resultado = Mathf.Min(resultado, Mathf.Lerp(zona.escala, 1f, borda * borda * (3f - 2f * borda)));
        }

        return resultado;
    }

    /// <summary>O ponto está dentro de alguma zona? O som e o visual da bruxa consultam isto.</summary>
    public static ZonaTemporal ZonaEm(Vector3 ponto)
    {
        for (int i = 0; i < s_Zonas.Count; i++)
        {
            if (Vector3.Distance(ponto, s_Zonas[i].transform.position) < s_Zonas[i].raio) return s_Zonas[i];
        }

        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, raio);
    }
}
