using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerCombat : NetworkBehaviour
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _attackRange = 100f;
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;

        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private Camera _playerCamera;

        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
        }

        // В методе Update добавьте проверку:
        private void Update()
        {
            if (!IsOwner) return;
    
            // Проверка на блокировку инпута от GameManager
            if (GameManager.Instance != null && GameManager.Instance.IsInputBlockedForPlayer())
                return;
    
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (Input.GetKeyDown(_attackKey))
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (!IsOwner || _playerCamera == null) return;

            Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

            if (Physics.Raycast(ray, out RaycastHit hit, _attackRange))
            {
                PlayerNetwork targetPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

                if (targetPlayer != null)
                {
                    if (targetPlayer.NetworkObjectId == _playerNetwork.NetworkObjectId)
                    {
                        Debug.Log("Нельзя атаковать себя!");
                        return;
                    }

                    DealDamageServerRpc(targetPlayer.NetworkObjectId, _damage);
                    Debug.Log($"Атакован игрок {targetPlayer.NetworkObjectId}");
                }
            }
        }

        [ServerRpc]
        private void DealDamageServerRpc(ulong targetObjectId, int damage)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out NetworkObject targetObject))
                return;

            PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();

            if (targetPlayer == null || targetPlayer == _playerNetwork)
                return;

            targetPlayer.TakeDamage(damage, OwnerClientId);

            Debug.Log($"[Server] Игрок {OwnerClientId} нанёс {damage} урона игроку {targetObjectId}");
        }
    }
}