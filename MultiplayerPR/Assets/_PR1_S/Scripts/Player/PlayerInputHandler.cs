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
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (!_playerMovement.IsCursorLocked()) return;

            if (Input.GetKeyDown(KeyCode.C))
            {
                _playerNetwork.RequestRandomColorServerRpc();
            }
            
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 shootDirection =
                    Input.GetMouseButton(1) ? _playerMovement.GetCameraForward() : transform.forward;

                _playerNetwork.ShootServerRpc(_bulletSpawnPoint.position, shootDirection);
            }
        }
    }
}