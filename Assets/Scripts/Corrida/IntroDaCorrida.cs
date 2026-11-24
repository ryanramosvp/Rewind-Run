using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Abertura da corrida: tela preta e uma frase escrita letra por letra, com
/// um bipe a cada uma, antes de a fase começar.
///
/// O jogo fica parado (timeScale zero) enquanto ela dura: o cronômetro da
/// fase e o relógio do fantasma só andam depois, então ninguém perde tempo
/// lendo. Por isso tudo aqui corre em tempo sem escala, e a pausa fica
/// desligada até o fim - ao fechar, ela devolveria o tempo no meio da frase.
///
/// Confirmar (Enter, Espaço ou A) completa a frase de uma vez e, com ela
/// completa, encerra a espera.
///
/// Só na primeira vez em cada sessão de jogo: perder para o fantasma
/// recarrega a fase, e repetir a abertura a cada tentativa seria esperar à
/// toa justamente quando se quer correr de novo.
/// </summary>
public class IntroDaCorrida : MonoBehaviour
{
    [Tooltip("Tela preta inteira, com o texto dentro.")]
    [SerializeField] CanvasGroup tela;

    [SerializeField] TMP_Text texto;

    [SerializeField] string frase = "Vença a si mesmo";

    [Tooltip("Segundos por letra.")]
    [SerializeField, Min(0.01f)] float porLetra = 0.09f;

    [Tooltip("Segundos com a frase completa na tela.")]
    [SerializeField, Min(0f)] float espera = 1.4f;

    [Tooltip("Segundos do clarear da tela, já com o jogo andando.")]
    [SerializeField, Min(0.05f)] float sumir = 0.7f;

    [SerializeField] AudioSource som;

    PauseMenu[] _pausas;

    // Estático: sobrevive à recarga da cena, que é como a fase recomeça.
    static bool s_JaMostrada;

    void Start()
    {
        if (tela == null || texto == null) { enabled = false; return; }

        if (s_JaMostrada)
        {
            tela.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        s_JaMostrada = true;

        tela.alpha = 1f;
        texto.text = string.Empty;
        if (som != null && som.clip == null) som.clip = SomProcedural.Letra;

        _pausas = FindObjectsByType<PauseMenu>(FindObjectsInactive.Exclude);
        foreach (var pausa in _pausas) pausa.enabled = false;

        Time.timeScale = 0f;
        StartCoroutine(Mostrar());
    }

    static bool Confirmou()
    {
        var teclado = Keyboard.current;
        var controle = Gamepad.current;
        return (teclado != null && (teclado.enterKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame))
               || (controle != null && controle.buttonSouth.wasPressedThisFrame);
    }

    IEnumerator Mostrar()
    {
        // Um instante de preto antes da primeira letra.
        yield return new WaitForSecondsRealtime(0.4f);

        for (int i = 1; i <= frase.Length; i++)
        {
            texto.text = frase.Substring(0, i);
            if (som != null && !char.IsWhiteSpace(frase[i - 1])) som.PlayOneShot(som.clip);

            float t = 0f;
            bool pulou = false;
            while (t < porLetra)
            {
                t += Time.unscaledDeltaTime;
                if (Confirmou()) { pulou = true; break; }
                yield return null;
            }

            if (pulou)
            {
                texto.text = frase;
                yield return null;   // o mesmo aperto não conta para pular a espera
                break;
            }
        }

        float e = 0f;
        while (e < espera)
        {
            e += Time.unscaledDeltaTime;
            if (Confirmou()) break;
            yield return null;
        }

        // Começa a corrida já com a tela clareando.
        Time.timeScale = 1f;
        foreach (var pausa in _pausas)
            if (pausa != null) pausa.enabled = true;

        float s = 0f;
        while (s < sumir)
        {
            s += Time.unscaledDeltaTime;
            tela.alpha = 1f - Mathf.Clamp01(s / sumir);
            yield return null;
        }

        tela.alpha = 0f;
        tela.gameObject.SetActive(false);
    }
}
