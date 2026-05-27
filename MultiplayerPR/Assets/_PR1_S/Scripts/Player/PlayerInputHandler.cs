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
            
            // Смена цвета (оставляем для совместимости)
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
                // Получаем направление от камеры (центр экрана)
                Vector3 shootDirection = GetCameraForward();
                
                // Стреляем из точки перед камерой
                Vector3 shootPosition = GetShootPosition();
                
                _playerNetwork.ShootServerRpc(shootPosition, shootDirection);
                Debug.Log($"[PlayerInputHandler] Player {OwnerClientId} shot from {shootPosition}");
            }
        }
        
        private bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }
        
        private Vector3 GetCameraForward()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.forward;
            }
            return transform.forward;
        }
        
        private Vector3 GetShootPosition()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && _bulletSpawnPoint == null)
            {
                // Стреляем из центра камеры
                return mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
            }
            
            if (_bulletSpawnPoint != null)
            {
                return _bulletSpawnPoint.position;
            }
            
            // Fallback: стреляем из позиции перед машиной
            return transform.position + transform.forward * 2f + Vector3.up * 1f;
        }
    }
}