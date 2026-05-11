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
    [SerializeField] private float _teleportTolerance = 0.5f; // Допуск для телепортации

    private CharacterController _cc;
    private float _verticalVelocity;
    private PlayerNetwork _playerNetwork;
    private float _horizontalRotation = 0f;
    
    // Для интерполяции на клиентах
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private float _smoothTime = 0.1f;
    private Vector3 _positionVelocity;
    private float _rotationVelocity;
    
    // Флаг принудительной телепортации
    private bool _forceTeleport = false;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerNetwork = GetComponent<PlayerNetwork>();
        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
    }

    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += OnTick;
        
        if (base.Owner.IsLocalClient)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Для не-владельцев отключаем CharacterController для плавной интерполяции
            _cc.enabled = false;
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
        
        if (!base.Owner.IsLocalClient && _cc != null)
        {
            _cc.enabled = true;
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
        if (Vector3.Distance(transform.position, rd.Position) > _teleportTolerance)
        {
            // Телепортация при большом расхождении
            Teleport(rd.Position, rd.Rotation, rd.VerticalVelocity);
        }
        else
        {
            // Плавная коррекция
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
            _verticalVelocity = rd.VerticalVelocity;
            
            // Перезапускаем CharacterController для применения изменений
            _cc.enabled = false;
            _cc.enabled = true;
        }
    }

    private void Update()
    {
        // Для не-владельцев выполняем плавную интерполяцию
        if (!base.Owner.IsLocalClient && isActiveAndEnabled)
        {
            if (_forceTeleport)
            {
                // Принудительная телепортация без интерполяции
                transform.position = _targetPosition;
                transform.rotation = _targetRotation;
                _forceTeleport = false;
            }
            else
            {
                // Плавная интерполяция
                transform.position = Vector3.SmoothDamp(
                    transform.position, 
                    _targetPosition, 
                    ref _positionVelocity, 
                    _smoothTime
                );
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    _targetRotation, 
                    Time.deltaTime / _smoothTime
                );
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTeleportServerRpc(Vector3 position, Quaternion rotation)
    {
        TeleportObservers(position, rotation);
        Teleport(position, rotation, 0f);
    }
    
    [ObserversRpc]
    public void TeleportObservers(Vector3 position, Quaternion rotation)
    {
        if (!base.IsOwner)
        {
            // Устанавливаем целевую позицию для интерполяции
            _targetPosition = position;
            _targetRotation = rotation;
            _forceTeleport = true; // Мгновенная телепортация для наблюдателей
        }
    }
    
    // Публичный метод для принудительной телепортации
    public void Teleport(Vector3 position, Quaternion rotation, float verticalVelocity = 0f)
    {
        if (base.IsOwner)
        {
            // Владелец - мгновенная телепортация
            _cc.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            _cc.enabled = true;
            _verticalVelocity = verticalVelocity;
            
            // Уведомляем всех наблюдателей
            TeleportObservers(position, rotation);
        }
        else
        {
            // Наблюдатель - плавная телепортация
            _targetPosition = position;
            _targetRotation = rotation;
            _forceTeleport = true;
        }
    }
}