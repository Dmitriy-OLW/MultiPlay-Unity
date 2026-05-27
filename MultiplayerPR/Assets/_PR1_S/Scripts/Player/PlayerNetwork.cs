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
        
        // НОВЫЕ NetworkVariable для синхронизации параметров машины
        public NetworkVariable<float> NetworkedCarSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<float> NetworkedSteeringAngle = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<float> NetworkedWheelRPM = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<bool> NetworkedIsDrifting = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<bool> NetworkedIsTractionLocked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public NetworkVariable<Vector3> NetworkedVelocity = new(
            Vector3.zero,
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
        [SerializeField] private GameObject[] _skinObjects;
        [SerializeField] private bool _disableDefaultRenderer = true;
        
        [Header("Sync Settings")]
        [SerializeField] private float _syncRate = 0.05f;
        
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
        private float _lastSyncTime;

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
            Debug.Log($"[PlayerNetwork] OnNetworkSpawn - ClientId: {OwnerClientId}, IsOwner: {IsOwner}, IsServer: {IsServer}, IsClient: {IsClient}");
            
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
            
            // Подписываемся на синхронизацию машины (для не-владельцев)
            if (!IsOwner)
            {
                Debug.Log($"[PlayerNetwork] Subscribing to car sync events for client {OwnerClientId}");
                NetworkedSteeringAngle.OnValueChanged += OnSteeringAngleSynced;
                NetworkedWheelRPM.OnValueChanged += OnWheelRPMSynced;
                NetworkedIsDrifting.OnValueChanged += OnDriftingStateSynced;
                NetworkedIsTractionLocked.OnValueChanged += OnTractionStateSynced;
                NetworkedVelocity.OnValueChanged += OnVelocitySynced;
            }
            
            if (_material != null)
            {
                _material.color = PlayerColor.Value;
                _originalColor = PlayerColor.Value;
            }
            
            UpdateComponentsState(IsAlive.Value);
            
            if (SkinIndex.Value >= 0 && SkinIndex.Value < _skinObjects.Length)
            {
                ApplySkin(SkinIndex.Value);
            }
            
            if (!IsServer && IsAlive.Value)
            {
                if (SpawnPosition.Value != Vector3.zero)
                {
                    ApplySpawnPosition(SpawnPosition.Value, SpawnRotation.Value);
                }
            }
            
            if (IsServer && IsAlive.Value)
            {
                // Инициализируем NetworkVariable начальными значениями
                NetworkedSteeringAngle.Value = 0f;
                NetworkedWheelRPM.Value = 0f;
                NetworkedIsDrifting.Value = false;
                NetworkedIsTractionLocked.Value = false;
                NetworkedVelocity.Value = Vector3.zero;
                NetworkedCarSpeed.Value = 0f;
                
                Debug.Log($"[PlayerNetwork] NetworkVariables initialized on server for {OwnerClientId}");
                StartCoroutine(SetInitialSpawnPointAndSkin());
            }
            
            if (!IsAlive.Value && IsServer)
            {
                SetDeadColorClientRpc();
            }
        }
        
        private void Update()
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            // Синхронизация параметров машины с сервера на клиенты
            if (Time.time - _lastSyncTime > _syncRate)
            {
                SyncCarParameters();
                _lastSyncTime = Time.time;
            }
        }
        
        private void SyncCarParameters()
        {
            if (_carController == null)
            {
                Debug.LogWarning($"[PlayerNetwork] SyncCarParameters - CarController is NULL for {OwnerClientId}");
                return;
            }
            
            if (!IsAlive.Value)
            {
                return;
            }
            
            float newSteering = _carController.GetCurrentSteeringAngle();
            float newRPM = _carController.GetAverageWheelRPM();
            float newCarSpeed = _carController.carSpeed;
            bool newDrifting = _carController.isDrifting;
            bool newTraction = _carController.isTractionLocked;
            Vector3 newVelocity = _carController.GetVelocity();
            
            // Логируем изменения
            if (Mathf.Abs(NetworkedSteeringAngle.Value - newSteering) > 0.5f)
            {
                Debug.Log($"[PlayerNetwork] Sync - Player {OwnerClientId} steering: {NetworkedSteeringAngle.Value:F1} -> {newSteering:F1}");
            }
            
            if (NetworkedIsDrifting.Value != newDrifting)
            {
                Debug.Log($"[PlayerNetwork] Sync - Player {OwnerClientId} drifting: {NetworkedIsDrifting.Value} -> {newDrifting}");
            }
            
            // Обновляем NetworkVariable
            NetworkedSteeringAngle.Value = newSteering;
            NetworkedWheelRPM.Value = newRPM;
            NetworkedIsDrifting.Value = newDrifting;
            NetworkedIsTractionLocked.Value = newTraction;
            NetworkedVelocity.Value = newVelocity;
            NetworkedCarSpeed.Value = newCarSpeed;
        }
        
        // Колбэки синхронизации для клиентов
        private void OnSteeringAngleSynced(float oldValue, float newValue)
        {
            Debug.Log($"[PlayerNetwork] OnSteeringAngleSynced - Player {OwnerClientId}, IsOwner: {IsOwner}, old: {oldValue:F1}, new: {newValue:F1}");
            
            if (IsOwner) return;
            if (_carController == null) return;
            
            _carController.ApplySyncedSteering(newValue);
        }
        
        private void OnWheelRPMSynced(float oldValue, float newValue)
        {
            if (IsOwner) return;
            if (_carController == null) return;
            
            _carController.ApplySyncedWheelRPM(newValue);
        }
        
        private void OnDriftingStateSynced(bool oldValue, bool newValue)
        {
            Debug.Log($"[PlayerNetwork] OnDriftingStateSynced - Player {OwnerClientId}, old: {oldValue}, new: {newValue}");
            
            if (IsOwner) return;
            if (_carController == null) return;
            
            _carController.SetDriftingState(newValue);
        }
        
        private void OnTractionStateSynced(bool oldValue, bool newValue)
        {
            Debug.Log($"[PlayerNetwork] OnTractionStateSynced - Player {OwnerClientId}, old: {oldValue}, new: {newValue}");
            
            if (IsOwner) return;
            if (_carController == null) return;
            
            _carController.SetTractionState(newValue);
        }
        
        private void OnVelocitySynced(Vector3 oldValue, Vector3 newValue)
        {
            if (IsOwner) return;
            if (_carController == null) return;
            
            _carController.ApplySyncedVelocity(newValue);
        }
        
        private void OnSpawnPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            Debug.Log($"[PlayerNetwork] OnSpawnPositionChanged - Player {OwnerClientId}, newValue: {newValue}");
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
            Debug.Log($"[PlayerNetwork] OnSkinIndexChanged - Player {OwnerClientId}, old: {oldValue}, new: {newValue}");
            
            if (newValue >= 0 && newValue < _skinObjects.Length)
            {
                ApplySkin(newValue);
            }
        }
        
        private void ApplySkin(int skinIndex)
        {
            if (_currentActiveSkin == skinIndex) return;
            
            for (int i = 0; i < _skinObjects.Length; i++)
            {
                if (_skinObjects[i] != null)
                {
                    _skinObjects[i].SetActive(false);
                }
            }
            
            if (skinIndex >= 0 && skinIndex < _skinObjects.Length && _skinObjects[skinIndex] != null)
            {
                _skinObjects[skinIndex].SetActive(true);
                _currentActiveSkin = skinIndex;
                
                if (_disableDefaultRenderer && _renderer != null)
                {
                    _renderer.enabled = false;
                }
                
                Debug.Log($"[PlayerNetwork] Applied skin {skinIndex} for player {OwnerClientId}");
            }
        }
        
        private void ApplySpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (_isPositionSynced) return;
            
            transform.position = position;
            transform.rotation = rotation;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
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
            Debug.Log($"[PlayerNetwork] Applied spawn position: {position} for player {OwnerClientId}");
        }
        
        private IEnumerator SetInitialSpawnPointAndSkin()
        {
            yield return null;
            
            Transform spawnPoint = null;
            if (PlayerSpawner.Instance != null)
            {
                spawnPoint = PlayerSpawner.Instance.GetFreeSpawnPoint(OwnerClientId);
            }
            
            if (spawnPoint == null && _spawnPoints != null && _spawnPoints.Length > 0)
            {
                spawnPoint = _spawnPoints[0];
            }
            
            if (SkinIndex.Value == -1 && _skinObjects.Length > 0)
            {
                int randomSkinIndex = Random.Range(0, _skinObjects.Length);
                SkinIndex.Value = randomSkinIndex;
                Debug.Log($"[PlayerNetwork] Random skin {randomSkinIndex} selected for player {OwnerClientId}");
            }
            
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
                
                SpawnPosition.Value = spawnPoint.position;
                SpawnRotation.Value = spawnPoint.rotation;
                
                TeleportToSpawnPointClientRpc(spawnPoint.position, spawnPoint.rotation);
                
                Debug.Log($"[PlayerNetwork] Player {OwnerClientId} initial spawn at {spawnPoint.position}");
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
            
            if (!IsOwner)
            {
                NetworkedSteeringAngle.OnValueChanged -= OnSteeringAngleSynced;
                NetworkedWheelRPM.OnValueChanged -= OnWheelRPMSynced;
                NetworkedIsDrifting.OnValueChanged -= OnDriftingStateSynced;
                NetworkedIsTractionLocked.OnValueChanged -= OnTractionStateSynced;
                NetworkedVelocity.OnValueChanged -= OnVelocitySynced;
            }
            
            if (IsServer && PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.ReleaseSpawnPoint(OwnerClientId);
            }
            
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            Debug.Log($"[PlayerNetwork] OnHealthChanged - Player {OwnerClientId}, {oldValue} -> {newValue}");
            if (!IsServer) return;
            
            if (newValue <= 0 && IsAlive.Value)
            {
                Die();
            }
        }

        private void OnIsAliveChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"[PlayerNetwork] OnIsAliveChanged - Player {OwnerClientId}, {oldValue} -> {newValue}");
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
            Debug.Log($"[PlayerNetwork] TeleportToSpawnPointClientRpc - Player {OwnerClientId} to {position}");
            
            transform.position = position;
            transform.rotation = rotation;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
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
            
            if (!IsServer)
            {
                SpawnPosition.Value = position;
                SpawnRotation.Value = rotation;
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
                
            if (_carController != null)
                _carController.enabled = isAlive;
        }

        private void Die()
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} died!");
            
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
            
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
                
                SpawnPosition.Value = spawnPoint.position;
                SpawnRotation.Value = spawnPoint.rotation;
                
                TeleportToSpawnPointClientRpc(spawnPoint.position, spawnPoint.rotation);
            }
            
            Health.Value = 100;
            Ammo.Value = 10;
            IsAlive.Value = true;
            _lastShootTime = 0;
            _isPositionSynced = true;
            
            _respawnCoroutine = null;
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} respawned at {transform.position}");
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
                Debug.Log($"[PlayerNetwork] Random skin {randomSkinIndex} requested for player {OwnerClientId}");
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
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} shot. Ammo left: {Ammo.Value}");
        }
        
        public void TakeDamage(int damage, ulong shooterId)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} took {damage} damage, HP: {Health.Value}");

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
                            Debug.Log($"[PlayerNetwork] Player {shooterId} scored! Total: {shooter.Score.Value}");
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
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} healed by {amount}. New HP: {newHealth}");
        }
        
        public void AddAmmo(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            int newAmmo = Mathf.Min(10, Ammo.Value + amount);
            Ammo.Value = newAmmo;
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} got {amount} ammo. New ammo: {newAmmo}");
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
        
        public int GetCurrentSkinIndex()
        {
            return SkinIndex.Value;
        }
    }
}