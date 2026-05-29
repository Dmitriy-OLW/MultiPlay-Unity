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
        [SerializeField] private GameObject[] _skinObjects;
        [SerializeField] private bool _disableDefaultRenderer = true;
        
        [Header("Sync Settings")]
        [SerializeField] private float _syncRate = 0.05f;
        
        [Header("References")]
        [SerializeField] private Transform[] _spawnPoints;
        
        [Header("Spawn Teleport Settings")]
        [SerializeField] private float _spawnDistanceThreshold = 3f;
        [SerializeField] private float _spawnForceDuration = 1f;
        
        [Header("Sounds")]
        [SerializeField] private AudioSource _damageSound;
        [SerializeField] private AudioSource _pickupSound;
        [SerializeField] private float _minDamageVolume = 0.5f;
        [SerializeField] private float _maxDamageVolume = 1f;
        
        private Renderer _renderer;
        private Coroutine _respawnCoroutine;
        private Coroutine _forceSpawnSyncCoroutine;
        private PlayerMovement _playerMovement;
        private PlayerCombat _playerCombat;
        private PlayerInputHandler _playerInput;
        private NetworkCarController _carController;
        private CheckpointManager _checkpointManager;
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
            Debug.Log($"[PlayerNetwork] OnNetworkSpawn - ClientId: {OwnerClientId}, IsOwner: {IsOwner}, IsServer: {IsServer}");
            
            // Находим CheckpointManager
            if (_checkpointManager == null)
            {
                _checkpointManager = FindObjectOfType<CheckpointManager>();
            }
            
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
                StartCoroutine(SetInitialSpawnPointAndSkin());
            }
            
            if (!IsAlive.Value && IsServer)
            {
                SetDeadColorClientRpc();
            }
            
            // Подписываемся на событие финиша гонки
            if (IsOwner && _checkpointManager != null)
            {
                _checkpointManager.OnPlayerFinished += OnPlayerFinished;
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
            
            if (IsServer && PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.ReleaseSpawnPoint(OwnerClientId);
            }
            
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
                
            if (_forceSpawnSyncCoroutine != null)
                StopCoroutine(_forceSpawnSyncCoroutine);
            
            // Отписываемся от события финиша
            if (_checkpointManager != null)
            {
                _checkpointManager.OnPlayerFinished -= OnPlayerFinished;
            }
        }
        
        // ==================== RACE FINISH ====================
        
        private void OnPlayerFinished(PlayerNetwork player)
        {
            if (player == this && IsOwner)
            {
                RequestFinishRaceServerRpc();
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void RequestFinishRaceServerRpc()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestFinishRaceServerRpc(OwnerClientId);
            }
        }

        // ==================== CAR SYNC - CLIENTRPC METHODS ====================
        
        [ServerRpc(RequireOwnership = true)]
        public void SendCarStateServerRpc(float steeringAngle, float wheelRPM, bool isDrifting, bool isTractionLocked, Vector3 velocity, Vector3 position, Quaternion rotation, float carSpeed)
        {
            if (!IsServer) return;
    
            SyncCarStateClientRpc(OwnerClientId, steeringAngle, wheelRPM, isDrifting, isTractionLocked, velocity, position, rotation, carSpeed);
        }

        [ClientRpc]
        private void SyncCarStateClientRpc(ulong sourceClientId, float steeringAngle, float wheelRPM, bool isDrifting, bool isTractionLocked, Vector3 velocity, Vector3 position, Quaternion rotation, float carSpeed)
        {
            if (IsOwner && OwnerClientId == sourceClientId) return;
    
            if (_carController != null)
            {
                _carController.ApplySyncedCarState(steeringAngle, wheelRPM, isDrifting, isTractionLocked, velocity, position, rotation, carSpeed);
            }
        }

        public void BroadcastCarState(float steeringAngle, float wheelRPM, bool isDrifting, bool isTractionLocked, Vector3 velocity, Vector3 position, Quaternion rotation, float carSpeed)
        {
            if (!IsServer) return;
    
            SyncCarStateClientRpc(OwnerClientId, steeringAngle, wheelRPM, isDrifting, isTractionLocked, velocity, position, rotation, carSpeed);
        }
        
        // ==================== OTHER NETWORK METHODS ====================
        
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
        
        public void CycleSkin()
        {
            if (!IsOwner) return;
            
            int nextSkin = (SkinIndex.Value + 1) % _skinObjects.Length;
            RequestSkinChangeServerRpc(nextSkin);
            Debug.Log($"[PlayerNetwork] Requesting skin change from {SkinIndex.Value} to {nextSkin}");
        }
        
        [ServerRpc]
        public void RequestSkinChangeServerRpc(int newSkinIndex)
        {
            if (newSkinIndex >= 0 && newSkinIndex < _skinObjects.Length)
            {
                SkinIndex.Value = newSkinIndex;
                Debug.Log($"[PlayerNetwork] Skin changed to {newSkinIndex} for player {OwnerClientId}");
            }
        }
        
        private void ApplySpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (_isPositionSynced) return;
            
            ForceTeleportToPosition(position, rotation);
            _isPositionSynced = true;
            Debug.Log($"[PlayerNetwork] Applied spawn position: {position} for player {OwnerClientId}");
        }
        
        private void ForceTeleportToPosition(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (rb.isKinematic)
                {
                    rb.MovePosition(position);
                    rb.MoveRotation(rotation);
                }
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
            
            Debug.Log($"[PlayerNetwork] Force teleported to position: {position}");
        }
        
        private IEnumerator ForceSpawnSyncCoroutine(Vector3 targetPosition, Quaternion targetRotation)
        {
            float startTime = Time.time;
            float endTime = startTime + _spawnForceDuration;
            int retryCount = 0;
            
            ForceTeleportToPosition(targetPosition, targetRotation);
            yield return null;
            
            while (Time.time < endTime)
            {
                float distance = Vector3.Distance(transform.position, targetPosition);
                
                if (distance > _spawnDistanceThreshold)
                {
                    retryCount++;
                    Debug.LogWarning($"[PlayerNetwork] Player {OwnerClientId} is {distance:F2}m from spawn point. Force teleporting again. Retry #{retryCount}");
                    ForceTeleportToPosition(targetPosition, targetRotation);
                }
                else if (retryCount > 0)
                {
                    Debug.Log($"[PlayerNetwork] Player {OwnerClientId} successfully synced to spawn point (distance: {distance:F2}m)");
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            float finalDistance = Vector3.Distance(transform.position, targetPosition);
            if (finalDistance > _spawnDistanceThreshold)
            {
                Debug.LogWarning($"[PlayerNetwork] Final force teleport for player {OwnerClientId} (distance: {finalDistance:F2}m)");
                ForceTeleportToPosition(targetPosition, targetRotation);
            }
            
            _forceSpawnSyncCoroutine = null;
            Debug.Log($"[PlayerNetwork] Spawn sync completed for player {OwnerClientId}");
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
                Vector3 targetPosition = spawnPoint.position;
                Quaternion targetRotation = spawnPoint.rotation;
                
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                
                SpawnPosition.Value = targetPosition;
                SpawnRotation.Value = targetRotation;
                
                TeleportToSpawnPointClientRpc(targetPosition, targetRotation);
                
                if (_forceSpawnSyncCoroutine != null)
                    StopCoroutine(_forceSpawnSyncCoroutine);
                _forceSpawnSyncCoroutine = StartCoroutine(ForceSpawnSyncCoroutine(targetPosition, targetRotation));
                
                Debug.Log($"[PlayerNetwork] Player {OwnerClientId} initial spawn at {targetPosition}");
            }
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
            
            ForceTeleportToPosition(position, rotation);
            
            if (_forceSpawnSyncCoroutine != null)
                StopCoroutine(_forceSpawnSyncCoroutine);
            _forceSpawnSyncCoroutine = StartCoroutine(ForceSpawnSyncCoroutine(position, rotation));
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
                Vector3 targetPosition = spawnPoint.position;
                Quaternion targetRotation = spawnPoint.rotation;
                
                ForceTeleportToPosition(targetPosition, targetRotation);
                
                SpawnPosition.Value = targetPosition;
                SpawnRotation.Value = targetRotation;
                
                TeleportToSpawnPointClientRpc(targetPosition, targetRotation);
                
                if (_forceSpawnSyncCoroutine != null)
                    StopCoroutine(_forceSpawnSyncCoroutine);
                _forceSpawnSyncCoroutine = StartCoroutine(ForceSpawnSyncCoroutine(targetPosition, targetRotation));
            }
            
            Health.Value = 100;
            Ammo.Value = 10;
            IsAlive.Value = true;
            _lastShootTime = 0;
            
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

            int oldHealth = Health.Value;
            Health.Value = Mathf.Max(0, Health.Value - damage);
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} took {damage} damage, HP: {Health.Value}");
            
            // Воспроизводим звук получения урона на клиенте
            PlayDamageSoundClientRpc(damage);
            
            if (Health.Value <= 0)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterId, out var shooterClient))
                {
                    if (shooterClient.PlayerObject != null)
                    {
                        PlayerNetwork shooter = shooterClient.PlayerObject.GetComponent<PlayerNetwork>();
                        if (shooter != null && shooter != this)
                        {
                            shooter.Score.Value += 5;
                            Debug.Log($"[PlayerNetwork] Player {shooterId} scored! Total: {shooter.Score.Value}");
                        }
                    }
                }
            }
        }
        
        [ClientRpc]
        private void PlayDamageSoundClientRpc(int damage)
        {
            if (_damageSound == null)
            {
                // Пробуем найти AudioSource
                _damageSound = GetComponent<AudioSource>();
                if (_damageSound == null)
                {
                    _damageSound = GetComponentInChildren<AudioSource>();
                    if (_damageSound == null)
                    {
                        Debug.LogWarning("[PlayerNetwork] No damage sound AudioSource found!");
                        return;
                    }
                }
            }
            
            // Расчёт громкости от полученного урона
            float damagePercent = Mathf.Clamp01((float)damage / 100f);
            float volume = _minDamageVolume + damagePercent * (_maxDamageVolume - _minDamageVolume);
            
            _damageSound.volume = volume;
            _damageSound.pitch = Random.Range(0.9f, 1.1f);
            _damageSound.Play();
            
            Debug.Log($"[PlayerNetwork] Playing damage sound with volume: {volume:F2} (damage: {damage})");
        }
        
        public void Heal(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            int newHealth = Mathf.Min(100, Health.Value + amount);
            Health.Value = newHealth;
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} healed by {amount}. New HP: {newHealth}");
            
            // Воспроизводим звук подбора аптечки
            PlayPickupSoundClientRpc("health");
        }
        
        public void AddAmmo(int amount)
        {
            if (!IsServer) return;
            if (!IsAlive.Value) return;
            
            int newAmmo = Mathf.Min(10, Ammo.Value + amount);
            Ammo.Value = newAmmo;
            
            Debug.Log($"[PlayerNetwork] Player {OwnerClientId} got {amount} ammo. New ammo: {newAmmo}");
            
            // Воспроизводим звук подбора патронов
            PlayPickupSoundClientRpc("ammo");
        }
        
        [ClientRpc]
        private void PlayPickupSoundClientRpc(string pickupType)
        {
            if (_pickupSound == null)
            {
                // Пробуем найти AudioSource
                _pickupSound = GetComponent<AudioSource>();
                if (_pickupSound == null)
                {
                    _pickupSound = GetComponentInChildren<AudioSource>();
                    if (_pickupSound == null)
                    {
                        Debug.LogWarning("[PlayerNetwork] No pickup sound AudioSource found!");
                        return;
                    }
                }
            }
            
            _pickupSound.volume = 0.7f;
            _pickupSound.pitch = pickupType == "health" ? 1.0f : 1.2f;
            _pickupSound.Play();
            
            Debug.Log($"[PlayerNetwork] Playing pickup sound for: {pickupType}");
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