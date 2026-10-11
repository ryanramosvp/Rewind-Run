using UnityEngine;

/// <summary>Estado de um objeto qualquer do cenário na linha do tempo.</summary>
public struct TransformSample
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
}

/// <summary>
/// Faz um objeto de cenário obedecer ao rewind: plataformas móveis, caixas com
/// Rigidbody, obstáculos. Basta jogar este componente no objeto.
/// </summary>
[DisallowMultipleComponent]
public class RewindableTransform : RewindableBehaviour<TransformSample>
{
    Rigidbody _body;
    bool _wasKinematic;
    TransformSample _lastApplied;

    void Awake() => _body = GetComponent<Rigidbody>();

    protected override TransformSample Capture() => new TransformSample
    {
        Position = transform.position,
        Rotation = transform.rotation,
        Velocity = _body != null ? _body.linearVelocity : Vector3.zero,
        AngularVelocity = _body != null ? _body.angularVelocity : Vector3.zero,
    };

    protected override void Apply(in TransformSample older, in TransformSample newer, float t)
    {
        _lastApplied = new TransformSample
        {
            Position = Vector3.Lerp(older.Position, newer.Position, t),
            Rotation = Quaternion.Slerp(older.Rotation, newer.Rotation, t),
            Velocity = Vector3.Lerp(older.Velocity, newer.Velocity, t),
            AngularVelocity = Vector3.Lerp(older.AngularVelocity, newer.AngularVelocity, t),
        };

        // O corpo está kinematic durante o rewind, então escrever no transform
        // direto é seguro e não briga com a simulação de física.
        transform.SetPositionAndRotation(_lastApplied.Position, _lastApplied.Rotation);
    }

    public override void OnRewindBegin()
    {
        if (_body == null) return;

        _wasKinematic = _body.isKinematic;
        _body.isKinematic = true;
    }

    public override void OnRewindEnd()
    {
        if (_body == null) return;

        _body.isKinematic = _wasKinematic;
        if (_body.isKinematic) return;

        // Devolve o movimento que o objeto tinha naquele instante do passado -
        // sem isso a caixa cairia do zero e o rewind pareceria um teleporte.
        _body.linearVelocity = _lastApplied.Velocity;
        _body.angularVelocity = _lastApplied.AngularVelocity;
    }
}
