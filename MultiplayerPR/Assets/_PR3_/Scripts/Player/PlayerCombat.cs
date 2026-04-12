using FishNet.Object;
using UnityEngine;

namespace Multi.FishNet
{
    public class PlayerCombat : NetworkBehaviour
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _attackRange = 100f;
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;
        
        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private Camera _playerCamera;
        
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
        }
        
        private void Update()
        {
            if (!IsOwner || !_isInitialized) return;
            
            // Используем .Value для доступа к SyncVar
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
                    if (targetPlayer == _playerNetwork)
                    {
                        Debug.Log("Нельзя атаковать себя!");
                        return;
                    }
                    
                    DealDamageServer(targetPlayer, _damage);
                    Debug.Log($"Атакован игрок {targetPlayer.Owner.ClientId}");
                }
            }
        }
        
        [ServerRpc]
        private void DealDamageServer(PlayerNetwork targetPlayer, int damage)
        {
            if (targetPlayer == null || targetPlayer == _playerNetwork)
                return;
            
            targetPlayer.TakeDamage(damage, Owner.ClientId);
            Debug.Log($"[Server] Игрок {Owner.ClientId} нанёс {damage} урона игроку {targetPlayer.Owner.ClientId}");
        }
    }
}