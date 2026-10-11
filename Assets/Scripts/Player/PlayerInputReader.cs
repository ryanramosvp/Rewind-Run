using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dono único das InputActions do jogador. O controller e a câmera leem daqui
/// em vez de cada um habilitar o mesmo action map - assim ninguém desliga o
/// input do outro ao ser desabilitado.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("O asset InputSystem_Actions do projeto.")]
    [SerializeField] InputActionAsset inputActions;

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool RewindHeld { get; private set; }

    /// <summary>
    /// Qual dispositivo mexeu a câmera por último. Mouse entrega pixels de
    /// deslocamento e analógico entrega -1..1, então a sensibilidade dos dois
    /// não pode ser a mesma.
    /// </summary>
    public bool LookIsGamepad { get; private set; }

    InputActionMap _playerMap;
    InputAction _move, _look, _jump, _rewind;

    void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError($"{nameof(PlayerInputReader)} sem InputActionAsset. " +
                           "Arraste Assets/InputSystem_Actions no Inspector.", this);
            enabled = false;
            return;
        }

        _playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        _move = _playerMap.FindAction("Move", throwIfNotFound: true);
        _look = _playerMap.FindAction("Look", throwIfNotFound: true);
        _jump = _playerMap.FindAction("Jump", throwIfNotFound: true);
        _rewind = _playerMap.FindAction("Rewind", throwIfNotFound: true);
    }

    void OnEnable() => _playerMap?.Enable();

    void OnDisable() => _playerMap?.Disable();

    void Update()
    {
        Move = _move.ReadValue<Vector2>();
        Look = _look.ReadValue<Vector2>();
        JumpHeld = _jump.IsPressed();
        JumpPressedThisFrame = _jump.WasPressedThisFrame();
        RewindHeld = _rewind.IsPressed();

        // activeControl fica null quando o analógico volta ao centro, então só
        // atualizamos quando há um controle ativo de verdade.
        var control = _look.activeControl;
        if (control != null) LookIsGamepad = control.device is Gamepad;
    }
}
