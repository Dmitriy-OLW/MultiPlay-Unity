using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

public struct PlayerMoveData : IReplicateData
{
    public float Horizontal;
    public float Vertical;
    public float MouseX;

    private uint _tick;

    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

public struct PlayerReconcileData : IReconcileData
{
    public Vector3 Position;
    public Quaternion Rotation;
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
    [SerializeField] private float _mouseSensitivity = 2f;

    private CharacterController _cc;
    private float _verticalVelocity;
    private PlayerNetwork _playerNetwork;
    private float _horizontalRotation = 0f;
    
    private bool _forceTeleport = false;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerNetwork = GetComponent<PlayerNetwork>();
    }

    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += OnTick;
        
        if (base.Owner.IsLocalClient)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void OnStopNetwork()
    {
        if (base.TimeManager != null)
            base.TimeManager.OnTick -= OnTick;
            
        if (base.Owner.IsLocalClient)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnTick()
    {
        if (_playerNetwork != null && !_playerNetwork.IsAlive.Value)
            return;

        if (base.IsOwner)
        {
            PlayerMoveData moveData = new PlayerMoveData
            {
                Horizontal = Input.GetAxisRaw("Horizontal"),
                Vertical = Input.GetAxisRaw("Vertical"),
                MouseX = Input.GetAxis("Mouse X") * _mouseSensitivity
            };
            Replicate(moveData);
        }
        
        if (_forceTeleport && base.IsOwner)
        {
            ApplyTeleport(_targetPosition, _targetRotation);
            _forceTeleport = false;
        }
    }

    public override void CreateReconcile()
    {
        PlayerReconcileData rd = new PlayerReconcileData
        {
            Position = transform.position,
            Rotation = transform.rotation,
            VerticalVelocity = _verticalVelocity
        };
        Reconcile(rd);
    }

    [Replicate]
    private void Replicate(PlayerMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        _horizontalRotation += md.MouseX;
        transform.rotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
        
        Vector3 move = (transform.right * md.Horizontal + transform.forward * md.Vertical).normalized;
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
        float distance = Vector3.Distance(transform.position, rd.Position);
        if (distance > 1f) 
        {
            Teleport(rd.Position, rd.Rotation);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, rd.Position, 0.5f);
            transform.rotation = rd.Rotation;
        }
        _verticalVelocity = rd.VerticalVelocity;
        
        _cc.enabled = false;
        _cc.enabled = true;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void RequestTeleportServerRpc(Vector3 newPosition, Quaternion newRotation)
    {
        if (!base.IsServerInitialized) return;
        
        Teleport(newPosition, newRotation);
        
        TeleportObserversRpc(newPosition, newRotation);
    }
    
    [ObserversRpc]
    private void TeleportObserversRpc(Vector3 newPosition, Quaternion newRotation)
    {
        if (base.IsOwner)
            return;
            
        Teleport(newPosition, newRotation);
    }
    
    public void Teleport(Vector3 newPosition, Quaternion newRotation)
    {
        if (_cc == null)
            _cc = GetComponent<CharacterController>();
        
        _cc.enabled = false;
        
        transform.position = newPosition;
        transform.rotation = newRotation;
        
        _horizontalRotation = newRotation.eulerAngles.y;
        
        _verticalVelocity = 0f;
        
        _cc.enabled = true;
        
        if (base.IsOwner)
        {
            _targetPosition = newPosition;
            _targetRotation = newRotation;
            _forceTeleport = true;
        }
        
        Debug.Log($"[Teleport] {gameObject.name} teleported to {newPosition}");
    }
    
    private void ApplyTeleport(Vector3 newPosition, Quaternion newRotation)
    {
        if (_cc == null)
            _cc = GetComponent<CharacterController>();
        
        _cc.enabled = false;
        transform.position = newPosition;
        transform.rotation = newRotation;
        _horizontalRotation = newRotation.eulerAngles.y;
        _verticalVelocity = 0f;
        _cc.enabled = true;
    }
}