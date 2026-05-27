using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerInputHandler : NetworkBehaviour
    {
        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private Transform _bulletSpawnPoint;

        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();

            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();
                
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
            
            // Защита от NullReferenceException
            if (_playerMovement == null)
            {
                _playerMovement = GetComponent<PlayerMovement>();
                if (_playerMovement == null) return;
            }
            
            if (!_playerMovement.IsCursorLocked()) return;
            
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
    
            if (Input.GetKeyDown(KeyCode.O))
            {
                _playerNetwork.RequestRandomColorServerRpc();
            }
    
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 shootDirection = Input.GetMouseButton(1) 
                    ? _playerMovement.GetCameraForward() 
                    : transform.forward;

                if (_bulletSpawnPoint != null)
                {
                    _playerNetwork.ShootServerRpc(_bulletSpawnPoint.position, shootDirection);
                }
                else
                {
                    // Fallback: стреляем из позиции перед игроком
                    Vector3 fallbackSpawnPos = transform.position + transform.forward * 2f + Vector3.up * 1f;
                    _playerNetwork.ShootServerRpc(fallbackSpawnPos, shootDirection);
                }
            }
        }
    }
}