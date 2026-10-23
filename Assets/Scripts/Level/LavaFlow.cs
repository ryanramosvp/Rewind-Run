using UnityEngine;

/// <summary>
/// Faz a textura da lava escorrer devagar.
///
/// Só desloca o UV - nada de shader próprio nem simulação. É o mesmo truque
/// dos jogos de Nintendo 64: uma textura parada lê como chão pintado, e basta
/// ela andar meio metro por segundo para o olho aceitar que é líquido.
///
/// Usa MaterialPropertyBlock, como a <see cref="LavaSurface"/>: os dois
/// convivem no mesmo objeto (um mexe no UV, o outro no brilho) sem instanciar
/// uma cópia do material a cada Play.
///
/// Roda também fora do Play. Sem isso a janela de cena mostrava a textura
/// esticada sobre a laje inteira (o material não sabe o tamanho dela), e a
/// lava do editor não era a lava do jogo.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class LavaFlow : MonoBehaviour
{
    [Tooltip("Repetições da textura na laje. Derivado do tamanho dela pelo builder, " +
             "para cada repetição ocupar sempre os mesmos metros.")]
    [SerializeField] Vector2 tiling = new Vector2(20f, 20f);

    [Tooltip("Repetições por segundo. Valores pequenos: a lava é espessa.")]
    [SerializeField] Vector2 speed = new Vector2(0.012f, 0.005f);

    static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

    Renderer _renderer;
    MaterialPropertyBlock _block;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        // Fora do Play o Awake pode não ter rodado depois de uma recompilação.
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_block == null) _block = new MaterialPropertyBlock();

        // Repeat mantém o deslocamento pequeno. Somado sem limite, depois de
        // uma hora de jogo o UV perderia precisão e a textura começaria a
        // tremer.
        Vector2 offset = new Vector2(Mathf.Repeat(speed.x * Time.time, 1f),
                                     Mathf.Repeat(speed.y * Time.time, 1f));

        _renderer.GetPropertyBlock(_block);
        _block.SetVector(BaseMapSt, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
        _renderer.SetPropertyBlock(_block);
    }
}
