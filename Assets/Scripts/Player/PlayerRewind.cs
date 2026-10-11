using UnityEngine;

/// <summary>Estado do jogador na linha do tempo.</summary>
public struct PlayerSample
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 HorizontalVelocity;
    public float VerticalVelocity;
    public int AirJumpsUsed;
    public PlayerMoveState State;
    public float HopBonus;
}

/// <summary>
/// Coloca o jogador na linha do tempo do <see cref="RewindManager"/>.
/// Guarda também a velocidade, para que ao soltar o rewind o personagem volte
/// a se mover como se mexia naquele instante em vez de despencar parado.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerController))]
public class PlayerRewind : RewindableBehaviour<PlayerSample>
{
    CharacterController _controller;
    PlayerController _player;
    PlayerSample _lastApplied;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
    }

    protected override PlayerSample Capture() => new PlayerSample
    {
        Position = transform.position,
        Rotation = transform.rotation,
        HorizontalVelocity = _player.HorizontalVelocity,
        VerticalVelocity = _player.VerticalVelocity,
        AirJumpsUsed = _player.AirJumpsUsed,
        State = _player.State,
        HopBonus = _player.HopBonus,
    };

    protected override void Apply(in PlayerSample older, in PlayerSample newer, float t)
    {
        _lastApplied = new PlayerSample
        {
            Position = Vector3.Lerp(older.Position, newer.Position, t),
            Rotation = Quaternion.Slerp(older.Rotation, newer.Rotation, t),
            HorizontalVelocity = Vector3.Lerp(older.HorizontalVelocity, newer.HorizontalVelocity, t),
            VerticalVelocity = Mathf.Lerp(older.VerticalVelocity, newer.VerticalVelocity, t),

            // Contagem e estado não se interpolam: ficam com a amostra mais próxima.
            AirJumpsUsed = t >= 0.5f ? newer.AirJumpsUsed : older.AirJumpsUsed,
            State = t >= 0.5f ? newer.State : older.State,
            HopBonus = Mathf.Lerp(older.HopBonus, newer.HopBonus, t),
        };

        transform.SetPositionAndRotation(_lastApplied.Position, _lastApplied.Rotation);
    }

    // O CharacterController sobrescreve escritas diretas no transform, então ele
    // sai de cena enquanto o rewind está no comando.
    public override void OnRewindBegin() => _controller.enabled = false;

    public override void OnRewindEnd()
    {
        _controller.enabled = true;
        _player.RestoreState(_lastApplied.HorizontalVelocity,
                             _lastApplied.VerticalVelocity,
                             _lastApplied.AirJumpsUsed,
                             _lastApplied.State,
                             _lastApplied.HopBonus);
    }
}
