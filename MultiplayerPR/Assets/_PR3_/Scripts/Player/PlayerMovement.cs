using FishNet.Object;
using UnityEngine;

namespace Multi.FishNet
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _dashSpeed = 20f;
        [SerializeField] private float _dashDuration = 0.2f;
        [SerializeField] private float _dashCooldown = 1.5f;

        [Header("Камера")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _normalCameraDistance = 5f;
        [SerializeField] private float _aimCameraDistance = 2f;
        [SerializeField] private Vector2 _pitchMinMax = new Vector2(-40, 85);

        [Header("Components")]
        [SerializeField] private CharacterController _characterController;
        
        private Camera _mainCamera;
        private float _yaw;
        private float _pitch;
        private float _currentCameraDistance;
        
        // Dash state
        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector3 _dashDirection;
        
        // Input
        private Vector3 _moveDirection;
        private bool _isDashingInput;
        
        // Client prediction
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _lastSendTime;
        [SerializeField] private float _sendInterval = 0.05f;
        
        private PlayerNetwork _playerNetwork;

        private void Awake()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                _mainCamera = Camera.main;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _yaw = transform.eulerAngles.y;
                _currentCameraDistance = _normalCameraDistance;
                _targetPosition = transform.position;
                _targetRotation = transform.rotation;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
            
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;

            HandleCursorLock();
            HandleCameraInput();
            HandleMovementInput();
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            ApplyMovement();
            SendToServer();
        }

        private void LateUpdate()
        {
            if (!IsOwner || _mainCamera == null) return;
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            UpdateCamera();
            
            // Интерполяция для других игроков
            if (!IsOwner)
            {
                transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 15f);
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 15f);
            }
        }

        private void UpdateCamera()
        {
            Vector3 targetCenter = transform.position + Vector3.up * 1.5f;
            Quaternion camRotation = Quaternion.Euler(_pitch, _yaw, 0);
            
            bool isAiming = Input.GetMouseButton(1) && Cursor.lockState == CursorLockMode.Locked;
            float targetDistance = isAiming ? _aimCameraDistance : _normalCameraDistance;
            _currentCameraDistance = Mathf.Lerp(_currentCameraDistance, targetDistance, Time.deltaTime * 10f);
            
            Vector3 camPosition = targetCenter - camRotation * Vector3.forward * _currentCameraDistance;
            
            if (isAiming)
                camPosition += camRotation * Vector3.right * 0.8f;
            
            _mainCamera.transform.position = camPosition;
            _mainCamera.transform.rotation = camRotation;
        }

        private void HandleCursorLock()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void HandleCameraInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            
            _yaw += Input.GetAxis("Mouse X") * _mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, _pitchMinMax.x, _pitchMinMax.y);
        }

        private void HandleMovementInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(horizontal, 0, vertical).normalized;
            
            _moveDirection = Vector3.zero;
            
            if (inputDir.magnitude >= 0.1f && _mainCamera != null)
            {
                Vector3 camForward = _mainCamera.transform.forward;
                Vector3 camRight = _mainCamera.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();
                
                _moveDirection = (camForward * inputDir.z + camRight * inputDir.x).normalized;
            }
            
            _isDashingInput = Input.GetKeyDown(KeyCode.LeftShift) && 
                              _dashCooldownTimer <= 0 && 
                              _moveDirection != Vector3.zero &&
                              Cursor.lockState == CursorLockMode.Locked;
        }

        private void ApplyMovement()
        {
            // Обновляем таймеры
            if (_dashCooldownTimer > 0)
                _dashCooldownTimer -= Time.fixedDeltaTime;
            if (_dashTimer > 0)
                _dashTimer -= Time.fixedDeltaTime;
            
            // Обработка дэша
            float currentSpeed = _moveSpeed;
            
            if (_isDashingInput && _dashTimer <= 0 && _dashCooldownTimer <= 0)
            {
                _dashTimer = _dashDuration;
                _dashCooldownTimer = _dashCooldown;
                _dashDirection = _moveDirection;
            }
            
            Vector3 movement = Vector3.zero;
            
            if (_dashTimer > 0)
            {
                movement = _dashDirection * _dashSpeed * Time.fixedDeltaTime;
            }
            else
            {
                movement = _moveDirection * currentSpeed * Time.fixedDeltaTime;
            }
            
            // Движение с CharacterController (с коллизиями)
            if (_characterController != null)
            {
                _characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }
            
            // Поворот персонажа
            if (_moveDirection != Vector3.zero && _dashTimer <= 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10f);
            }
        }

        private void SendToServer()
        {
            if (Time.time - _lastSendTime >= _sendInterval)
            {
                _lastSendTime = Time.time;
                SendMovementToServer(transform.position, transform.rotation);
            }
        }

        [ServerRpc]
        private void SendMovementToServer(Vector3 position, Quaternion rotation)
        {
            // Серверное подтверждение позиции
            transform.position = position;
            transform.rotation = rotation;
            
            // Рассылаем всем клиентам (включая владельца для сверки)
            ConfirmMovementObservers(position, rotation);
        }

        [ObserversRpc]
        private void ConfirmMovementObservers(Vector3 position, Quaternion rotation)
        {
            if (!IsOwner)
            {
                // Другие игроки: интерполируем
                _targetPosition = position;
                _targetRotation = rotation;
            }
        }

        public Vector3 GetCameraForward()
        {
            return _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
        }

        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }
    }
}