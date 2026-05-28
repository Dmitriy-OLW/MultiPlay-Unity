using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerInputHandler : NetworkBehaviour
    {
        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private CarCameraController _cameraController;
        [SerializeField] private NetworkCarController _carController;
        [SerializeField] private Transform _bulletSpawnPoint;
        
        [Header("Shooting Cone")]
        [SerializeField] private float _maxHorizontalAngle = 60f; // Максимальный угол отклонения в градусах

        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();

            if (_cameraController == null)
                _cameraController = GetComponent<CarCameraController>();
                
            if (_carController == null)
                _carController = GetComponent<NetworkCarController>();
                
            // Автоматический поиск точки спавна пули, если не назначена
            if (_bulletSpawnPoint == null)
            {
                GameObject spawnPointObj = GameObject.FindGameObjectWithTag("ShootPoint");
                if (spawnPointObj != null)
                    _bulletSpawnPoint = spawnPointObj.transform;
                else
                    Debug.LogWarning($"[PlayerInputHandler] Bullet spawn point not found for player {OwnerClientId}");
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            // Проверяем состояние курсора через CarCameraController
            if (_cameraController != null && !IsCursorLocked()) return;
            
            // Смена цвета
            if (Input.GetKeyDown(KeyCode.P))
            {
                _playerNetwork.RequestRandomColorServerRpc();
            }
            
            // Циклическая смена скина
            if (Input.GetKeyDown(KeyCode.O))
            {
                _playerNetwork.CycleSkin();
                Debug.Log($"[PlayerInputHandler] Cycling skin for player {OwnerClientId}");
            }
    
            // Стрельба
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 shootDirection = GetShootDirection();
                Vector3 shootPosition = GetShootPosition();
                
                _playerNetwork.ShootServerRpc(shootPosition, shootDirection);
                Debug.Log($"[PlayerInputHandler] Player {OwnerClientId} shot with direction {shootDirection}");
            }
        }
        
        private bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }
        
        private Vector3 GetShootDirection()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return transform.forward;
            
            // Получаем направление камеры
            Vector3 cameraForward = mainCamera.transform.forward;
            
            // Получаем направление машины (только горизонтальная составляющая)
            Vector3 carForward = transform.forward;
            carForward.y = 0;
            carForward.Normalize();
            
            // Вычисляем угол между направлением камеры и направлением машины
            Vector3 cameraHorizontal = cameraForward;
            cameraHorizontal.y = 0;
            cameraHorizontal.Normalize();
            
            float angleToCar = Vector3.SignedAngle(carForward, cameraHorizontal, Vector3.up);
            
            // Ограничиваем угол конусом
            float clampedAngle = Mathf.Clamp(angleToCar, -_maxHorizontalAngle, _maxHorizontalAngle);
            
            // Создаём новое направление: поворачиваем направление машины на ограниченный угол
            Quaternion horizontalRotation = Quaternion.AngleAxis(clampedAngle, Vector3.up);
            Vector3 finalDirection = horizontalRotation * carForward;
            
            // Сохраняем оригинальный Y компонент (стреляем ровно по горизонтали)
            finalDirection.y = 0;
            finalDirection.Normalize();
            
            return finalDirection;
        }
        
        private Vector3 GetShootPosition()
        {
            if (_bulletSpawnPoint != null)
            {
                return _bulletSpawnPoint.position;
            }
            
            // Fallback: стреляем из позиции перед машиной на высоте 1 метр
            return transform.position + transform.forward * 2f + Vector3.up * 1f;
        }
    }
}