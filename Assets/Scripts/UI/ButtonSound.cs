using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dá som a um botão: um ao passar por ele, outro ao apertar.
///
/// Mora no próprio botão em vez de ser ligado um a um no construtor de cada
/// tela. Assim o <see cref="UiBuilder"/> põe este componente ao criar qualquer
/// botão, e nenhuma tela futura nasce muda por esquecimento.
///
/// Vale para qualquer controle (botão, barra); o som de confirmar só existe
/// onde há clique, nos botões.
///
/// Ouve as duas formas de "passar por cima" porque o jogo tem as duas: o mouse
/// dispara OnPointerEnter, e navegar pelo teclado dispara OnSelect. Só uma
/// delas deixaria metade dos jogadores sem retorno.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    void Awake()
    {
        // No clique, e não no OnPointerClick: assim vale também para quem
        // apertou pelo teclado, que não passa pelo ponteiro.
        if (TryGetComponent<Button>(out var button)) button.onClick.AddListener(UiSounds.Confirm);
    }

    public void OnPointerEnter(PointerEventData eventData) => UiSounds.Select();

    public void OnSelect(BaseEventData eventData) => UiSounds.Select();
}
