using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

namespace Multi.PR1
{
    public class PlayerNetwork : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> Nickname = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Health = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<Color> PlayerColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<bool> IsAlive = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Ammo = new(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        [Header("Combat Settings")]
        [SerializeField] private GameObject _bulletPrefab;
        [SerializeField] private float _shootCooldown = 1f;
        
        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Color _deadColor = Color.red;
        
        [Header("References")]
        [SerializeField] private Transform[] _spawnPoints; 
        
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private Coroutine _respawnCoroutine;
        private PlayerMovement _playerMovement;
        private PlayerCombat _playerCombat;
        private PlayerInputHandler _playerInput;
        private float _lastShootTime;
        private Color _originalColor;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            
            _playerMovement = GetComponent<PlayerMovement>();
            _playerCombat = GetComponent<PlayerCombat>();
            _playerInput = GetComponent<PlayerInputHandler>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
            }

            PlayerColor.OnValueChanged += OnColorChanged;
            Health.OnValueChanged += OnHealthChanged;
            IsAlive.OnValueChanged += OnIsAliveChanged;
            Ammo.OnValueChanged += OnAmmoChanged;
            
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", PlayerColor.Value);
                _renderer.SetPropertyBlock(_propBlock);
                _originalColor = PlayerColor.Value;
            }
            
            UpdateComponentsState(IsAlive.Value);
            
            if (!IsAlive.Value && IsServer)
            {
                SetDeadColorClientRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorChanged;
            Health.OnValueChanged -= OnHealthChanged;
            IsAlive.OnValueChanged -= OnIsAliveChanged;
            Ammo.OnValueChanged -= OnAmmoChanged;
            
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            if (!IsServer) return;
            
            if (newValue <= 0 && IsAlive.Value)
            {
                Die();
            }
        }

        private void OnIsAliveChanged(bool oldValue, bool newValue)
        {
            UpdateComponentsState(newValue);
            
            if (newValue)
            {
                SetNormalColorClientRpc();
                TeleportToSpawnPointClientRpc();
            }
            else
            {
                SetDeadColorClientRpc();
            }
        }
        
        private void OnAmmoChanged(int oldValue, int newValue)
        {
            if (IsOwner && newValue > oldValue)
            {
                Debug.Log($"Ammo increased: {oldValue} -> {newValue}");
            }
        }

        private void OnColorChanged(Color oldValue, Color newValue)
        {
            if (_renderer != null && IsAlive.Value)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", newValue);
                _renderer.SetPropertyBlock(_propBlock);
                _originalColor = newValue;
            }
        }

        [ClientRpc]
        private void SetDeadColorClientRpc()
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", _deadColor);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
        
        [ClientRpc]
        private void SetNormalColorClientRpc()
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", _originalColor);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
        
        [ClientRpc]
        private void TeleportToSpawnPointClientRpc()
        {
            if (IsOwner)
            {
                Transform spawnPoint = null;
                
                if (PlayerSpawner.Instance != null)
                {
                    spawnPoint = PlayerSpawner.Instance.GetSpawnPoint();
                }
                
                if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
                {
                    int idx = Random.Range(0, _spawnPoints.Length);
                    spawnPoint = _spawnPoints[idx];
                }
                
                if (spawnPoint != null)
                {
                    transform.position = spawnPoint.position;
                    transform.rotation = spawnPoint.rotation;
                }
            }
        }

        private void UpdateComponentsState(bool isAlive)
        {
            if (_playerMovement != null)
                _playerMovement.enabled = isAlive;
            
            if (_playerCombat != null)
                _playerCombat.enabled = isAlive;
            
            if (_playerInput != null)
                _playerInput.enabled = isAlive;
        }

        private void Die()
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            IsAlive.Value = false;
            
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
            
            _respawnCoroutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);
            
            Transform spawnPoint = null;
            
            if (PlayerSpawner.Instance != null)
            {
                spawnPoint = PlayerSpawner.Instance.GetSpawnPoint();
            }
            
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
            {
                int idx = Random.Range(0, _spawnPoints.Length);
                spawnPoint = _spawnPoints[idx];
            }
            
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }
            
            Health.Value = 100;
            Ammo.Value = 10;
            IsAlive.Value = true;
            _lastShootTime = 0;
            
            _respawnCoroutine = null;
            
            Debug.Log($"[Server] Player {OwnerClientId} respawned at {transform.position}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitNicknameServerRpc(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{OwnerClientId}" : nickname.Trim();
            Nickname.Value = safeValue;

            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
            _originalColor = PlayerColor.Value;
        }

        [ServerRpc]
        public void RequestRandomColorServerRpc()
        {
            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
        }

        [ServerRpc]
        public void ShootServerRpc(Vector3 spawnPos, Vector3 direction)
        {
            if (!IsAlive.Value) return;
            
            if (Ammo.Value <= 0) return;
            
            if (Time.time < _lastShootTime + _shootCooldown) return;
            
            _lastShootTime = Time.time;
            
            Ammo.Value -= 1;

            GameObject bullet = Instantiate(_bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            BulletNetwork bulletScript = bullet.GetComponent<BulletNetwork>();
            if (bulletScript != null)
                bulletScript.OwnerId = OwnerClientId;
            bullet.GetComponent<NetworkObject>().Spawn();
            
            Debug.Log($"[Server] Player {OwnerClientId} shot. Ammo left: {Ammo.Value}");
        }
        
        public void TakeDamage(int damage, ulong shooterId)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);
            
            Debug.Log($"[Server] Player {OwnerClientId} took {damage} damage, HP: {Health.Value}");

            if (Health.Value <= 0)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterId, out var shooterClient))
                {
                    if (shooterClient.PlayerObject != null)
                    {
                        PlayerNetwork shooter = shooterClient.PlayerObject.GetComponent<PlayerNetwork>();
                        if (shooter != null && shooter != this)
                        {
                            shooter.Score.Value += 1;
                            Debug.Log($"[Server] Player {shooterId} scored! Total: {shooter.Score.Value}");
                        }
                    }
                }
            }
        }
        
        public void Heal(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            int newHealth = Mathf.Min(100, Health.Value + amount);
            Health.Value = newHealth;
            
            Debug.Log($"[Server] Player {OwnerClientId} healed by {amount}. New HP: {newHealth}");
        }
        
        public void AddAmmo(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            int newAmmo = Mathf.Min(10, Ammo.Value + amount);
            Ammo.Value = newAmmo;
            
            Debug.Log($"[Server] Player {OwnerClientId} got {amount} ammo. New ammo: {newAmmo}");
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void ForceRespawnServerRpc()
        {
            if (!IsAlive.Value)
            {
                if (_respawnCoroutine != null)
                    StopCoroutine(_respawnCoroutine);
                
                _respawnCoroutine = StartCoroutine(RespawnRoutine());
            }
        }
    }
}