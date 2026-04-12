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
        
        [SerializeField] private Transform _transform;
        
        private Camera _mainCamera;
        private float _yaw;
        private float _pitch;
        private float _currentCameraDistance;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector3 _dashDirection;
        private PlayerNetwork _playerNetwork;
        private bool _isInitialized = false;
        
        // Для синхронизации
        private float _lastSendTime;
        [SerializeField] private float _sendInterval = 0.05f;
        
        private void Awake()
        {
            if (_transform == null)
                _transform = transform;
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
                _isInitialized = true;
            }
        }
        
        private void Update()
        {
            if (!IsOwner || !_isInitialized) return;
            
            // Используем .Value для доступа к SyncVar
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
            
            if (_mainCamera == null && IsOwner)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null)
                    _isInitialized = true;
            }
            
            HandleCursorLock();
            HandleCameraInput();
            HandleMovement();
            SendPositionToServer();
        }
        
        private void LateUpdate()
        {
            if (!IsOwner || !_isInitialized) return;
            if (_mainCamera == null) return;
            
            Vector3 targetCenter = _transform.position + Vector3.up * 1.5f;
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
        
        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(horizontal, 0, vertical).normalized;
            
            Vector3 moveDir = Vector3.zero;
            
            if (inputDir.magnitude >= 0.1f && _mainCamera != null)
            {
                Vector3 camForward = _mainCamera.transform.forward;
                Vector3 camRight = _mainCamera.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();
                
                moveDir = camForward * inputDir.z + camRight * inputDir.x;
            }
            
            if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;
            
            if (Input.GetKeyDown(KeyCode.LeftShift) && _dashCooldownTimer <= 0 && moveDir != Vector3.zero &&
                Cursor.lockState == CursorLockMode.Locked)
            {
                _dashTimer = _dashDuration;
                _dashCooldownTimer = _dashCooldown;
                _dashDirection = moveDir;
            }
            
            float currentSpeed = _moveSpeed;
            Vector3 finalMovement = Vector3.zero;
            
            if (_dashTimer > 0)
            {
                _dashTimer -= Time.deltaTime;
                currentSpeed = _dashSpeed;
                finalMovement = _dashDirection * currentSpeed;
            }
            else
            {
                finalMovement = moveDir * currentSpeed;
            }
            
            _transform.position += finalMovement * Time.deltaTime;
            
            if (moveDir != Vector3.zero && _dashTimer <= 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        
        private void SendPositionToServer()
        {
            if (Time.time - _lastSendTime >= _sendInterval)
            {
                UpdatePositionServer(_transform.position, _transform.rotation);
                _lastSendTime = Time.time;
            }
        }
        
        [ServerRpc]
        private void UpdatePositionServer(Vector3 position, Quaternion rotation)
        {
            UpdatePositionObservers(position, rotation);
        }
        
        [ObserversRpc]
        private void UpdatePositionObservers(Vector3 position, Quaternion rotation)
        {
            if (IsOwner) return;
            _transform.position = position;
            _transform.rotation = rotation;
        }
        
        public Vector3 GetCameraForward()
        {
            return _mainCamera != null ? _mainCamera.transform.forward : _transform.forward;
        }
        
        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }
    }
}