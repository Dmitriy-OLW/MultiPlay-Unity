using FishNet.Object;
using UnityEngine;

namespace Multi.FishNet
{
    public class PlayerInputHandler : NetworkBehaviour
    {
        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private Transform _bulletSpawnPoint;
        
        private bool _isInitialized = false;
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsOwner)
            {
                _isInitialized = true;
            }
        }
        
        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
            
            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();
        }
        
        private void Update()
        {
            if (!IsOwner || !_isInitialized) return;
            if (_playerMovement != null && !_playerMovement.IsCursorLocked()) return;
            
            // Используем .Value для доступа к SyncVar
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            if (Input.GetKeyDown(KeyCode.O))
            {
                _playerNetwork.RequestRandomColorServer();
            }
            
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 shootDirection = Input.GetMouseButton(1) 
                    ? _playerMovement.GetCameraForward() 
                    : transform.forward;
                
                _playerNetwork.ShootServer(_bulletSpawnPoint.position, shootDirection);
            }
        }
    }
}