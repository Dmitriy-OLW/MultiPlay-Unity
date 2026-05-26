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
        
        // NetworkVariable для синхронизации позиции
        public NetworkVariable<Vector3> SpawnPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<Quaternion> SpawnRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        // NetworkVariable для синхронизации индекса скина
        public NetworkVariable<int> SkinIndex = new(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        [Header("Combat Settings")]
        [SerializeField] private GameObject _bulletPrefab;
        [SerializeField] private float _shootCooldown = 1f;
        
        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Color _deadColor = Color.red;
        
        [Header("Skins")]
        [SerializeField] private GameObject[] _skinObjects; // Массив объектов скинов
        [SerializeField] private bool _disableDefaultRenderer = true; // Отключать ли стандартный рендерер
        
        [Header("References")]
        [SerializeField] private Transform[] _spawnPoints; 
        
        private Renderer _renderer;
        private Coroutine _respawnCoroutine;
        private PlayerMovement _playerMovement;
        private PlayerCombat _playerCombat;
        private PlayerInputHandler _playerInput;
        private NetworkCarController _carController;
        private float _lastShootTime;
        private Color _originalColor;
        private Material _material;
        private bool _isPositionSynced = false;
        private int _currentActiveSkin = -1;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            
            if (_renderer != null)
            {
                _material = _renderer.material;
            }
            
            _playerMovement = GetComponent<PlayerMovement>();
            _playerCombat = GetComponent<PlayerCombat>();
            _playerInput = GetComponent<PlayerInputHandler>();
            _carController = GetComponent<NetworkCarController>();
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
            SpawnPosition.OnValueChanged += OnSpawnPositionChanged;
            SpawnRotation.OnValueChanged += OnSpawnRotationChanged;
            SkinIndex.OnValueChanged += OnSkinIndexChanged;
            
            if (_material != null)
            {
                _material.color = PlayerColor.Value;
                _originalColor = PlayerColor.Value;
            }
            
            UpdateComponentsState(IsAlive.Value);
            
            // Если скин уже установлен, применяем его
            if (SkinIndex.Value >= 0 && SkinIndex.Value < _skinObjects.Length)
            {
                ApplySkin(SkinIndex.Value);
            }
            
            // Для клиента - подписываемся на изменение позиции
            if (!IsServer && IsAlive.Value)
            {
                // Если позиция уже установлена, применяем её
                if (SpawnPosition.Value != Vector3.zero)
                {
                    ApplySpawnPosition(SpawnPosition.Value, SpawnRotation.Value);
                }
            }
            
            // Только для сервера - устанавливаем начальную позицию и скин
            if (IsServer && IsAlive.Value)
            {
                StartCoroutine(SetInitialSpawnPointAndSkin());
            }
            
            if (!IsAlive.Value && IsServer)
            {
                SetDeadColorClientRpc();
            }
        }
        
        private void OnSpawnPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            if (!IsServer && newValue != Vector3.zero)
            {
                ApplySpawnPosition(newValue, SpawnRotation.Value);
            }
        }
        
        private void OnSpawnRotationChanged(Quaternion oldValue, Quaternion newValue)
        {
            if (!IsServer && newValue != Quaternion.identity)
            {
                ApplySpawnPosition(SpawnPosition.Value, newValue);
            }
        }
        
        private void OnSkinIndexChanged(int oldValue, int newValue)
        {
            // При изменении индекса скина применяем его на всех клиентах
            if (newValue >= 0 && newValue < _skinObjects.Length)
            {
                ApplySkin(newValue);
                Debug.Log($"[Network] Skin changed for player {OwnerClientId} from {oldValue} to {newValue}");
            }
        }
        
        private void ApplySkin(int skinIndex)
        {
            // Если уже активен этот скин, ничего не делаем
            if (_currentActiveSkin == skinIndex) return;
            
            // Отключаем все скины
            for (int i = 0; i < _skinObjects.Length; i++)
            {
                if (_skinObjects[i] != null)
                {
                    _skinObjects[i].SetActive(false);
                }
            }
            
            // Включаем выбранный скин
            if (skinIndex >= 0 && skinIndex < _skinObjects.Length && _skinObjects[skinIndex] != null)
            {
                _skinObjects[skinIndex].SetActive(true);
                _currentActiveSkin = skinIndex;
                
                // Если нужно отключить стандартный рендерер
                if (_disableDefaultRenderer && _renderer != null)
                {
                    _renderer.enabled = false;
                }
                
                Debug.Log($"[Network] Applied skin {skinIndex} for player {OwnerClientId}");
            }
        }
        
        private void ApplySpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (_isPositionSynced) return;
            
            transform.position = position;
            transform.rotation = rotation;
            
            // Сбрасываем физику
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Сбрасываем колёса
            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>();
            foreach (var wheel in wheels)
            {
                wheel.motorTorque = 0;
                wheel.brakeTorque = 0;
                wheel.steerAngle = 0;
            }
            
            if (_carController != null)
            {
                _carController.ResetCarState();
            }
            
            _isPositionSynced = true;
            Debug.Log($"[Client] Applied spawn position: {position} for player {OwnerClientId}");
        }
        
        private IEnumerator SetInitialSpawnPointAndSkin()
        {
            // Ждём один кадр для полной инициализации
            yield return null;
            
            // Получаем точку спавна
            Transform spawnPoint = null;
            if (PlayerSpawner.Instance != null)
            {
                spawnPoint = PlayerSpawner.Instance.GetFreeSpawnPoint(OwnerClientId);
            }
            
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
            {
                spawnPoint = _spawnPoints[0];
            }
            
            // Выбираем случайный скин, если он ещё не выбран
            if (SkinIndex.Value == -1 && _skinObjects.Length > 0)
            {
                int randomSkinIndex = Random.Range(0, _skinObjects.Length);
                SkinIndex.Value = randomSkinIndex;
                Debug.Log($"[Server] Random skin {randomSkinIndex} selected for player {OwnerClientId}");
            }
            
            if (spawnPoint != null)
            {
                // Устанавливаем позицию на сервере
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
                
                // Сохраняем в NetworkVariable для клиентов
                SpawnPosition.Value = spawnPoint.position;
                SpawnRotation.Value = spawnPoint.rotation;
                
                // Отправляем ClientRpc для немедленной синхронизации
                TeleportToSpawnPointClientRpc(spawnPoint.position, spawnPoint.rotation);
                
                Debug.Log($"[Server] Player {OwnerClientId} initial spawn at {spawnPoint.position}");
            }
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorChanged;
            Health.OnValueChanged -= OnHealthChanged;
            IsAlive.OnValueChanged -= OnIsAliveChanged;
            Ammo.OnValueChanged -= OnAmmoChanged;
            SpawnPosition.OnValueChanged -= OnSpawnPositionChanged;
            SpawnRotation.OnValueChanged -= OnSpawnRotationChanged;
            SkinIndex.OnValueChanged -= OnSkinIndexChanged;
            
            // Освобождаем точку при деспавне игрока
            if (IsServer && PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.ReleaseSpawnPoint(OwnerClientId);
            }
            
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
                _isPositionSynced = false;
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
            if (_material != null && IsAlive.Value)
            {
                _material.color = newValue;
                _originalColor = newValue;
            }
        }

        [ClientRpc]
        private void SetDeadColorClientRpc()
        {
            if (_material != null)
            {
                _material.color = _deadColor;
            }
        }
        
        [ClientRpc]
        private void SetNormalColorClientRpc()
        {
            if (_material != null)
            {
                _material.color = _originalColor;
            }
        }
        
        [ClientRpc]
        private void TeleportToSpawnPointClientRpc(Vector3 position, Quaternion rotation)
        {
            // Принудительно применяем позицию для всех клиентов
            transform.position = position;
            transform.rotation = rotation;
            
            // Для машины - сбрасываем физику
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Сбрасываем состояние коллайдеров колёс (для машины)
            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>();
            foreach (var wheel in wheels)
            {
                wheel.motorTorque = 0;
                wheel.brakeTorque = 0;
                wheel.steerAngle = 0;
            }
            
            // Сбрасываем состояние машины
            if (_carController != null)
            {
                _carController.ResetCarState();
            }
            
            // Обновляем NetworkVariable для клиентов
            if (!IsServer)
            {
                SpawnPosition.Value = position;
                SpawnRotation.Value = rotation;
            }
            
            Debug.Log($"[ClientRpc] Teleported {OwnerClientId} to {position}");
        }

        private void UpdateComponentsState(bool isAlive)
        {
            if (_playerMovement != null)
                _playerMovement.enabled = isAlive;
            
            if (_playerCombat != null)
                _playerCombat.enabled = isAlive;
            
            if (_playerInput != null)
                _playerInput.enabled = isAlive;
                
            if (_carController != null)
                _carController.enabled = isAlive;
        }

        private void Die()
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            // Освобождаем точку спавна перед смертью
            if (PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.ReleaseSpawnPoint(OwnerClientId);
            }
            
            IsAlive.Value = false;
            
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
            
            _respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
        
        private bool IsPointOccupiedByOtherPlayer(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, 2f);
            foreach (var collider in colliders)
            {
                PlayerNetwork otherPlayer = collider.GetComponentInParent<PlayerNetwork>();
                if (otherPlayer != null && otherPlayer != this && otherPlayer.IsAlive.Value)
                {
                    return true;
                }
            }
            return false;
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);
            
            Transform spawnPoint = null;
            if (PlayerSpawner.Instance != null)
            {
                spawnPoint = PlayerSpawner.Instance.GetFreeSpawnPoint(OwnerClientId);
            }
            
            // Если спавнер не настроен, используем локальные точки
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
            {
                foreach (var point in _spawnPoints)
                {
                    if (!IsPointOccupiedByOtherPlayer(point.position))
                    {
                        spawnPoint = point;
                        break;
                    }
                }
                
                if (spawnPoint == null)
                    spawnPoint = _spawnPoints[0];
            }
            
            // При респавне НЕ меняем скин, оставляем тот же
            // Скин остаётся таким же, как был при первом спавне
            
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
                
                SpawnPosition.Value = spawnPoint.position;
                SpawnRotation.Value = spawnPoint.rotation;
                
                TeleportToSpawnPointClientRpc(spawnPoint.position, spawnPoint.rotation);
            }
            
            // Сбрасываем состояние игрока
            Health.Value = 100;
            Ammo.Value = 10;
            IsAlive.Value = true;
            _lastShootTime = 0;
            _isPositionSynced = true;
            
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
        public void RequestRandomSkinServerRpc()
        {
            if (_skinObjects.Length > 0)
            {
                int randomSkinIndex = Random.Range(0, _skinObjects.Length);
                SkinIndex.Value = randomSkinIndex;
                Debug.Log($"[Server] Random skin {randomSkinIndex} requested for player {OwnerClientId}");
            }
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
        
        // Геттер для текущего индекса скина
        public int GetCurrentSkinIndex()
        {
            return SkinIndex.Value;
        }
    }
}