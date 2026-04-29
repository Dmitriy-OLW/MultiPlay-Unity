using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections;
using Multi.PR1.FishNet;  

namespace Multi.FishNet
{
    public class PlayerNetwork : NetworkBehaviour
    {
        // Используем SyncVar<T> без атрибутов
        public readonly SyncVar<string> Nickname = new SyncVar<string>();
        public readonly SyncVar<int> Health = new SyncVar<int>(100);
        public readonly SyncVar<int> Score = new SyncVar<int>();
        public readonly SyncVar<Color> PlayerColor = new SyncVar<Color>(Color.white);
        public readonly SyncVar<bool> IsAlive = new SyncVar<bool>(true);
        public readonly SyncVar<int> Ammo = new SyncVar<int>(10);
        
        [Header("Combat Settings")]
        [SerializeField] private GameObject _bulletPrefab;
        [SerializeField] private float _shootCooldown = 1f;
        
        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Color _deadColor = Color.red;
        
        [Header("References")]
        [SerializeField] private Transform[] _spawnPoints;
        
        private Renderer _renderer;
        private Material _material;
        private Color _originalColor;
        private float _lastShootTime;
        private Coroutine _respawnCoroutine;
        private PlayerMovement _playerMovement;
        private PlayerCombat _playerCombat;
        private PlayerInputHandler _playerInput;
        
        // События для UI
        public System.Action<string> OnNicknameChangedUI;
        public System.Action<int> OnHealthChangedUI;
        public System.Action<int> OnScoreChangedUI;
        public System.Action<Color> OnColorChangedUI;
        public System.Action<bool> OnIsAliveChangedUI;
        public System.Action<int> OnAmmoChangedUI;
        
        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _material = _renderer.material;
            
            _playerMovement = GetComponent<PlayerMovement>();
            _playerCombat = GetComponent<PlayerCombat>();
            _playerInput = GetComponent<PlayerInputHandler>();
            
            // Подписка на изменения SyncVar (правильная сигнатура для v4)
            Nickname.OnChange += OnNicknameChanged;
            Health.OnChange += OnHealthChanged;
            Score.OnChange += OnScoreChanged;
            PlayerColor.OnChange += OnPlayerColorChanged;
            IsAlive.OnChange += OnIsAliveChanged;
            Ammo.OnChange += OnAmmoChanged;
        }
        
        private void OnDestroy()
        {
            Nickname.OnChange -= OnNicknameChanged;
            Health.OnChange -= OnHealthChanged;
            Score.OnChange -= OnScoreChanged;
            PlayerColor.OnChange -= OnPlayerColorChanged;
            IsAlive.OnChange -= OnIsAliveChanged;
            Ammo.OnChange -= OnAmmoChanged;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsOwner)
            {
                SubmitNicknameServer(ConnectionUI.PlayerNickname);
            }
            
            UpdateComponentsState(IsAlive.Value);
            
            if (!IsAlive.Value && base.IsServer)
            {
                SetDeadColor();
            }
            else if (_material != null && IsAlive.Value)
            {
                _material.color = PlayerColor.Value;
                _originalColor = PlayerColor.Value;
            }
            
            // Инициализация UI начальными значениями
            OnNicknameChangedUI?.Invoke(Nickname.Value);
            OnHealthChangedUI?.Invoke(Health.Value);
            OnScoreChangedUI?.Invoke(Score.Value);
            OnColorChangedUI?.Invoke(PlayerColor.Value);
            OnIsAliveChangedUI?.Invoke(IsAlive.Value);
            OnAmmoChangedUI?.Invoke(Ammo.Value);
        }
        
        // SyncVar хуки - правильная сигнатура для FishNet v4
        private void OnNicknameChanged(string oldValue, string newValue, bool asServer)
        {
            OnNicknameChangedUI?.Invoke(newValue);
        }
        
        private void OnHealthChanged(int oldValue, int newValue, bool asServer)
        {
            OnHealthChangedUI?.Invoke(newValue);
            
            if (base.IsServer && newValue <= 0 && IsAlive.Value)
            {
                Die();
            }
        }
        
        private void OnScoreChanged(int oldValue, int newValue, bool asServer)
        {
            OnScoreChangedUI?.Invoke(newValue);
        }
        
        private void OnPlayerColorChanged(Color oldValue, Color newValue, bool asServer)
        {
            OnColorChangedUI?.Invoke(newValue);
            
            if (_material != null && IsAlive.Value)
            {
                _material.color = newValue;
                _originalColor = newValue;
            }
        }
        
        private void OnIsAliveChanged(bool oldValue, bool newValue, bool asServer)
        {
            OnIsAliveChangedUI?.Invoke(newValue);
            UpdateComponentsState(newValue);
            
            if (newValue)
            {
                SetNormalColor();
                TeleportToSpawnPoint();
            }
            else
            {
                SetDeadColor();
            }
        }
        
        private void OnAmmoChanged(int oldValue, int newValue, bool asServer)
        {
            OnAmmoChangedUI?.Invoke(newValue);
            
            if (IsOwner && newValue > oldValue)
            {
                Debug.Log($"Ammo increased: {oldValue} -> {newValue}");
            }
        }
        
        private void SetDeadColor()
        {
            if (_material != null) _material.color = _deadColor;
        }
        
        private void SetNormalColor()
        {
            if (_material != null) _material.color = _originalColor;
        }
        
        [ObserversRpc]
        private void TeleportToSpawnPoint()
        {
            if (!IsOwner) return;
            
            Transform spawnPoint = null;
            
            if (PlayerSpawner.Instance != null)
                spawnPoint = PlayerSpawner.Instance.GetSpawnPoint();
            
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
                spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }
        }
        
        private void UpdateComponentsState(bool alive)
        {
            if (_playerMovement != null) _playerMovement.enabled = alive;
            if (_playerCombat != null) _playerCombat.enabled = alive;
            if (_playerInput != null) _playerInput.enabled = alive;
        }
        
        private void Die()
        {
            if (!base.IsServer) return;
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
                spawnPoint = PlayerSpawner.Instance.GetSpawnPoint();
            
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
                spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            
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
            
            Debug.Log($"[Server] Player {Owner.ClientId} respawned at {transform.position}");
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void SubmitNicknameServer(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname) 
                ? $"Player_{Owner.ClientId}" 
                : nickname.Trim();
            
            Nickname.Value = safeValue;
            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
            _originalColor = PlayerColor.Value;
        }
        
        [ServerRpc]
        public void RequestRandomColorServer()
        {
            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
        }
        
        [ServerRpc]
        public void ShootServer(Vector3 spawnPos, Vector3 direction)
        {
            if (!IsAlive.Value) return;
            if (Ammo.Value <= 0) return;
            if (Time.time < _lastShootTime + _shootCooldown) return;
            
            _lastShootTime = Time.time;
            Ammo.Value--;
            
            GameObject bullet = Instantiate(_bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            BulletNetwork bulletScript = bullet.GetComponent<BulletNetwork>();
            if (bulletScript != null)
                bulletScript.OwnerId = Owner.ClientId;
            
            base.Spawn(bullet);
            
            Debug.Log($"[Server] Player {Owner.ClientId} shot. Ammo left: {Ammo.Value}");
        }
        
        public void TakeDamage(int damage, int shooterId)
        {
            if (!base.IsServer) return;
            if (!IsAlive.Value) return;
            
            Health.Value = Mathf.Max(0, Health.Value - damage);
            
            Debug.Log($"[Server] Player {Owner.ClientId} took {damage} damage, HP: {Health.Value}");
            
            if (Health.Value <= 0 && shooterId != Owner.ClientId)
            {
                foreach (var client in base.ServerManager.Clients)
                {
                    if (client.Key == shooterId && client.Value != null)
                    {
                        PlayerNetwork shooter = client.Value.FirstObject.GetComponent<PlayerNetwork>();
                        if (shooter != null && shooter != this)
                        {
                            Score.Value++;
                            Debug.Log($"[Server] Player {shooterId} scored! Total: {Score.Value}");
                        }
                        break;
                    }
                }
            }
        }
        
        public void Heal(int amount)
        {
            if (!base.IsServer) return;
            if (!IsAlive.Value) return;
            
            Health.Value = Mathf.Min(100, Health.Value + amount);
            Debug.Log($"[Server] Player {Owner.ClientId} healed by {amount}. New HP: {Health.Value}");
        }
        
        public void AddAmmo(int amount)
        {
            if (!base.IsServer) return;
            if (!IsAlive.Value) return;
            
            Ammo.Value = Mathf.Min(10, Ammo.Value + amount);
            Debug.Log($"[Server] Player {Owner.ClientId} got {amount} ammo. New ammo: {Ammo.Value}");
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void ForceRespawnServer()
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