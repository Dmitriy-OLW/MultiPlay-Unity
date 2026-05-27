using UnityEngine;
using Unity.Netcode;

namespace Multi.PR1
{
    public class CarCameraController : NetworkBehaviour
    {
        [Header("Camera Follow Settings")]
        [SerializeField] private Vector3 _defaultOffset = new Vector3(0f, 2.5f, -6f);
        [SerializeField] private float _followSpeed = 8f;
        [SerializeField] private float _rotationSpeed = 5f;
        
        [Header("Mouse Look Settings")]
        [SerializeField] private float _mouseSensitivityX = 2f;
        [SerializeField] private float _mouseSensitivityY = 1.5f;
        [SerializeField] private float _maxVerticalAngle = 30f;
        [SerializeField] private float _minVerticalAngle = -20f;
        
        [Header("Return To Default Settings")]
        [SerializeField] private float _returnSpeedX = 3f;
        [SerializeField] private float _returnSpeedY = 2f;
        [SerializeField] private float _mouseStopThreshold = 0.01f;
        
        [Header("Camera Collision")]
        [SerializeField] private LayerMask _collisionLayers = -1;
        [SerializeField] private float _collisionRadius = 0.3f;
        [SerializeField] private float _minCameraDistance = 1.5f;
        
        private Camera _mainCamera;
        private Transform _cameraTransform;
        private Transform _carTransform;
        private PlayerNetwork _playerNetwork;
        private NetworkCarController _carController;
        
        // Текущие углы поворота камеры
        private float _currentYaw = 0f;
        private float _currentPitch = 0f;
        
        // Целевые углы (с инерцией)
        private float _targetYaw = 0f;
        private float _targetPitch = 0f;
        
        // Для определения движения мыши
        private float _lastMouseX;
        private float _lastMouseY;
        private bool _isMouseMoving;
        private float _mouseIdleTime;
        private float _mouseIdleThreshold = 0.05f;
        
        // Для плавного возврата
        private float _returnVelocityX;
        private float _returnVelocityY;
        
        // Для сглаживания следования
        private Vector3 _velocityFollowRef = Vector3.zero;
        private Vector3 _currentOffset;
        private Vector3 _targetOffset;
        
        private void Start()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
            _carController = GetComponent<NetworkCarController>();
            _carTransform = transform;
            
            if (IsOwner)
            {
                SetupCamera();
            }
        }
        
        private void SetupCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[CarCameraController] Main camera not found!");
                return;
            }
            
            _cameraTransform = _mainCamera.transform;
            _currentOffset = _defaultOffset;
            _targetOffset = _defaultOffset;
            
            // Скрываем курсор при старте
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Debug.Log($"[CarCameraController] Camera setup complete for player {OwnerClientId}");
        }
        
        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (_mainCamera == null) return;
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            HandleMouseInput();
            HandleCameraFollow();
        }
        
        private void HandleMouseInput()
        {
            // Проверяем состояние курсора
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // Если курсор не заблокирован - не обрабатываем поворот
                return;
            }
            
            // Получаем движение мыши
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Проверяем, двигается ли мышь
            bool isMovingNow = Mathf.Abs(mouseX) > _mouseStopThreshold || Mathf.Abs(mouseY) > _mouseStopThreshold;
            
            if (isMovingNow)
            {
                _isMouseMoving = true;
                _mouseIdleTime = 0f;
                
                // Накопление целевых углов
                _targetYaw += mouseX * _mouseSensitivityX;
                _targetPitch -= mouseY * _mouseSensitivityY;
                
                // Ограничиваем вертикальный угол
                _targetPitch = Mathf.Clamp(_targetPitch, _minVerticalAngle, _maxVerticalAngle);
            }
            else
            {
                if (_isMouseMoving)
                {
                    _mouseIdleTime += Time.deltaTime;
                    if (_mouseIdleTime > _mouseIdleThreshold)
                    {
                        _isMouseMoving = false;
                    }
                }
                
                // Плавный возврат к нулю, если мышь не двигается
                if (!_isMouseMoving)
                {
                    _targetYaw = Mathf.SmoothDamp(_targetYaw, 0f, ref _returnVelocityX, 1f / _returnSpeedX);
                    _targetPitch = Mathf.SmoothDamp(_targetPitch, 0f, ref _returnVelocityY, 1f / _returnSpeedY);
                }
            }
            
            // Плавное применение углов (инерция)
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, Time.deltaTime * _rotationSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * _rotationSpeed);
        }
        
        private void HandleCameraFollow()
        {
            if (_carTransform == null) return;
            
            // Вычисляем желаемую позицию камеры относительно машины
            Quaternion cameraRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            
            // Базовый оффсет с учетом поворота машины
            Vector3 worldOffset = _carTransform.rotation * _defaultOffset;
            
            // Добавляем поворот от мыши
            Vector3 rotatedOffset = cameraRotation * worldOffset;
            
            // Целевая позиция
            Vector3 targetPosition = _carTransform.position + rotatedOffset;
            
            // Проверка на коллизии
            targetPosition = AdjustForCollisions(targetPosition);
            
            // Плавное следование с интерполяцией (убираем дергание)
            Vector3 smoothedPosition = Vector3.SmoothDamp(
                _cameraTransform.position, 
                targetPosition, 
                ref _velocityFollowRef, 
                1f / _followSpeed,
                Mathf.Infinity,
                Time.deltaTime
            );
            
            // Применяем позицию
            _cameraTransform.position = smoothedPosition;
            
            // Камера смотрит на точку над машиной
            Vector3 lookTarget = _carTransform.position + Vector3.up * 1.5f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - _cameraTransform.position);
            
            // Плавный поворот камеры
            _cameraTransform.rotation = Quaternion.Slerp(
                _cameraTransform.rotation, 
                targetRotation, 
                Time.deltaTime * _followSpeed
            );
        }
        
        private Vector3 AdjustForCollisions(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - _carTransform.position;
            float distance = direction.magnitude;
            
            if (distance < _minCameraDistance)
                return _carTransform.position + direction.normalized * _minCameraDistance;
            
            RaycastHit hit;
            if (Physics.SphereCast(_carTransform.position + Vector3.up * 1f, _collisionRadius, direction.normalized, out hit, distance, _collisionLayers))
            {
                float safeDistance = Mathf.Max(hit.distance - _collisionRadius, _minCameraDistance);
                return _carTransform.position + direction.normalized * safeDistance;
            }
            
            return targetPosition;
        }
        
        /// <summary>
        /// Принудительный сброс углов камеры (при респавне)
        /// </summary>
        public void ResetCameraAngles()
        {
            _targetYaw = 0f;
            _targetPitch = 0f;
            _currentYaw = 0f;
            _currentPitch = 0f;
            _isMouseMoving = false;
            _mouseIdleTime = 0f;
            
            Debug.Log($"[CarCameraController] Camera angles reset for player {OwnerClientId}");
        }
        
        /// <summary>
        /// Переключение состояния курсора
        /// </summary>
        public void ToggleCursorLock()
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
        
        private void OnDrawGizmosSelected()
        {
            if (_carTransform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_carTransform.position + Vector3.up * 1f, _collisionRadius);
            }
        }
    }
}