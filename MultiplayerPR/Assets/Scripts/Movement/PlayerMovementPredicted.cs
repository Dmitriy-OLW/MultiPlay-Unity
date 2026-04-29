using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

public struct PlayerMoveData : IReplicateData
{
    public float Horizontal;
    public float Vertical;

    private uint _tick;

    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

public struct PlayerReconcileData : IReconcileData
{
    public Vector3 Position;
    public float VerticalVelocity;

    private uint _tick;

    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementPredicted : NetworkBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _gravity = -9.81f;

    private CharacterController _cc;
    private float _verticalVelocity;
    private PlayerNetwork _playerNetwork;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerNetwork = GetComponent<PlayerNetwork>();
    }

    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        if (base.TimeManager != null)
            base.TimeManager.OnTick -= OnTick;
    }

    private void OnTick()
    {
        // Проверка на жизнь
        if (_playerNetwork != null && !_playerNetwork.IsAlive.Value)
            return;

        if (base.IsOwner)
        {
            PlayerMoveData moveData = new PlayerMoveData
            {
                Horizontal = Input.GetAxisRaw("Horizontal"),
                Vertical = Input.GetAxisRaw("Vertical")
            };
            Replicate(moveData);
        }
    }

    public override void CreateReconcile()
    {
        PlayerReconcileData rd = new PlayerReconcileData
        {
            Position = transform.position,
            VerticalVelocity = _verticalVelocity
        };
        Reconcile(rd);
    }

    [Replicate]
    private void Replicate(PlayerMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        Vector3 move = new Vector3(md.Horizontal, 0f, md.Vertical).normalized;
        move *= _speed;

        _verticalVelocity += _gravity * (float)base.TimeManager.TickDelta;
        move.y = _verticalVelocity;

        _cc.Move(move * (float)base.TimeManager.TickDelta);

        if (_cc.isGrounded)
            _verticalVelocity = 0f;
    }

    [Reconcile]
    private void Reconcile(PlayerReconcileData rd, Channel channel = Channel.Unreliable)
    {
        if (base.IsOwner)
        {
            Debug.Log($"Reconcile: position={rd.Position}, my position={transform.position}");
        }

        transform.position = rd.Position;
        _verticalVelocity = rd.VerticalVelocity;

        // Сброс CharacterController для предотвращения рывков
        _cc.enabled = false;
        _cc.enabled = true;
    }
}