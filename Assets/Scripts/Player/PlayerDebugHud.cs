using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mostra na tela as condições que decidem cada estado de movimento.
///
/// Existe porque diagnosticar movimento de fora do jogo é chute: as condições
/// são encadeadas e o sintoma costuma ser o mesmo em todas. Com os valores à
/// vista dá para apontar qual delas falha.
///
/// F1 liga e desliga. É ferramenta de desenvolvimento - some no build final.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerDebugHud : MonoBehaviour
{
    [SerializeField] bool visible;

    PlayerController _player;
    GUIStyle _style;

    void Awake() => _player = GetComponent<PlayerController>();

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) visible = !visible;
    }

    void OnGUI()
    {
        if (!visible) return;

        _style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white },
        };

        string text =
            $"F1 esconde\n\n" +
            $"estado          {_player.State}\n" +
            $"no chão         {_player.IsGrounded}\n" +
            $"velocidade      {_player.HorizontalVelocity.magnitude:F1} m/s\n" +
            $"vertical        {_player.VerticalVelocity:F1} m/s\n" +
            $"corrente de hop {_player.HopChain}\n\n" +
            $"--- parede ---\n" +
            $"contato         {_player.IsTouchingWall}\n" +
            $"wall slide      {_player.IsWallSliding}\n\n" +
            $"--- chão ---\n" +
            $"chão detectado  {_player.HasGround}\n" +
            $"inclinação      {_player.GroundAngle:F0}°";

        // Fundo escuro atrás do texto, senão some sobre o cenário claro.
        GUI.Box(new Rect(10f, 10f, 260f, 250f), GUIContent.none);
        GUI.Label(new Rect(22f, 18f, 250f, 240f), text, _style);
    }
}
