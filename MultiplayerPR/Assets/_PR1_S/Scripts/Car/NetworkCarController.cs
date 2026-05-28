using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

namespace Multi.PR1
{
    public class NetworkCarController : NetworkBehaviour
    {
        [Header("CAR SETUP")]
        [Range(20, 190)]
        public int maxSpeed = 90;
        [Range(10, 120)]
        public int maxReverseSpeed = 45;
        [Range(1, 10)]
        public int accelerationMultiplier = 2;
        [Space(10)]
        [Range(10, 45)]
        public int maxSteeringAngle = 27;
        [Range(0.1f, 1f)]
        public float steeringSpeed = 0.5f;
        [Space(10)]
        [Range(100, 600)]
        public int brakeForce = 350;
        [Range(1, 10)]
        public int decelerationMultiplier = 2;
        [Range(1, 10)]
        public int handbrakeDriftMultiplier = 5;
        [Space(10)]
        public Vector3 bodyMassCenter;

        [Header("WHEELS")]
        public GameObject frontLeftMesh;
        public WheelCollider frontLeftCollider;
        public GameObject frontRightMesh;
        public WheelCollider frontRightCollider;
        public GameObject rearLeftMesh;
        public WheelCollider rearLeftCollider;
        public GameObject rearRightMesh;
        public WheelCollider rearRightCollider;

        [Header("EFFECTS")]
        public bool useEffects = false;
        public ParticleSystem RLWParticleSystem;
        public ParticleSystem RRWParticleSystem;
        public TrailRenderer RLWTireSkid;
        public TrailRenderer RRWTireSkid;

        [Header("UI")]
        public bool useUI = false;
        public TMP_Text carSpeedText;

        [Header("Sounds")]
        public bool useSounds = false;
        public AudioSource carEngineSound;
        public AudioSource tireScreechSound;
        private float initialCarEngineSoundPitch;

        [Header("Combat")]
        [SerializeField] private GameObject _bulletPrefab;
        [SerializeField] private Transform _shootPoint;
        [SerializeField] private KeyCode _shootKey = KeyCode.Mouse0;
        [SerializeField] private float _shootCooldown = 0.5f;

        [Header("Camera")]
        [SerializeField] private CarCameraController _cameraController;
        
        [Header("Collision Damage")]
        [SerializeField] private float _damageMultiplier = 0.1f;
        [SerializeField] private float _minDamageSpeed = 10f; // 10 км/ч
        [SerializeField] private float _maxDamageSpeed = 100f; // 100 км/ч
        [SerializeField] private int _maxDamage = 50;
        
        [Header("Respawn")]
        [SerializeField] private KeyCode _respawnKey = KeyCode.R;
        [SerializeField] private float _respawnCooldown = 3f;
        [SerializeField] private float _respawnHeightOffset = 3f;
        
        [Header("Collision")]
        [SerializeField] private float _collisionCooldown = 0.5f;

        [HideInInspector]
        public float carSpeed;
        [HideInInspector]
        public bool isDrifting;
        [HideInInspector]
        public bool isTractionLocked;

        private PlayerNetwork _playerNetwork;
        private Rigidbody carRigidbody;
        private float steeringAxis;
        private float throttleAxis;
        private float driftingAxis;
        private float localVelocityZ;
        private float localVelocityX;
        private bool deceleratingCar;
        private float _lastShootTime;
        private float _startupTimer = 0f;
        private bool _isInterpolationEnabled = false;
        
        private float _lastCollisionTime;
        private float _lastRespawnTime;
        
        private float _lastDamageSyncTime;
        private float _damageSyncRate = 0.1f;

        private WheelFrictionCurve FLwheelFriction;
        private float FLWextremumSlip;
        private WheelFrictionCurve FRwheelFriction;
        private float FRWextremumSlip;
        private WheelFrictionCurve RLwheelFriction;
        private float RLWextremumSlip;
        private WheelFrictionCurve RRwheelFriction;
        private float RRWextremumSlip;
        
        private float _syncedSteeringAngle;
        private float _syncedWheelRPM;
        private bool _syncedIsDrifting;
        private bool _syncedIsTractionLocked;
        private Vector3 _syncedVelocity;
        private Vector3 _syncedPosition;
        private Quaternion _syncedRotation;
        private float _syncedCarSpeed;
        
        private float _wheelSpinX = 0f;
        private float _currentSteerAngle = 0f;
        
        private float _lastSyncTime;
        private float _syncRate = 0.066f;

        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
            
            carRigidbody = GetComponent<Rigidbody>();
            
            if (IsOwner)
            {
                if (_cameraController == null)
                {
                    _cameraController = GetComponent<CarCameraController>();
                    if (_cameraController == null)
                    {
                        _cameraController = gameObject.AddComponent<CarCameraController>();
                    }
                }
                
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                
                Debug.Log($"[CarController] Start - I AM OWNER! ClientId: {OwnerClientId}");
            }
            else
            {
                Debug.Log($"[CarController] Start - I am NOT owner. ClientId: {OwnerClientId}");
                _startupTimer = 2f;
                
                if (carRigidbody != null)
                {
                    carRigidbody.isKinematic = true;
                }
            }

            SetupCarPhysics();
            SetupSounds();
        }
        
        private void SetupCarPhysics()
        {
            if (carRigidbody != null)
            {
                carRigidbody.centerOfMass = bodyMassCenter;
                if (IsOwner)
                {
                    carRigidbody.isKinematic = false;
                    carRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }

            if (frontLeftCollider != null)
            {
                FLwheelFriction = frontLeftCollider.sidewaysFriction;
                FLWextremumSlip = frontLeftCollider.sidewaysFriction.extremumSlip;
                FRwheelFriction = frontRightCollider.sidewaysFriction;
                FRWextremumSlip = frontRightCollider.sidewaysFriction.extremumSlip;
                RLwheelFriction = rearLeftCollider.sidewaysFriction;
                RLWextremumSlip = rearLeftCollider.sidewaysFriction.extremumSlip;
                RRwheelFriction = rearRightCollider.sidewaysFriction;
                RRWextremumSlip = rearRightCollider.sidewaysFriction.extremumSlip;
            }
        }

        private void SetupSounds()
        {
            if (carEngineSound != null)
            {
                initialCarEngineSoundPitch = carEngineSound.pitch;
            }

            if (useUI)
            {
                InvokeRepeating("CarSpeedUI", 0f, 0.1f);
            }

            if (useSounds && IsOwner)
            {
                InvokeRepeating("CarSounds", 0f, 0.1f);
            }
        }

        private void Update()
        {
            if (!IsOwner && _startupTimer > 0)
            {
                _startupTimer -= Time.deltaTime;
                if (_startupTimer <= 0)
                {
                    _isInterpolationEnabled = true;
                    Debug.Log($"[CarController] Interpolation enabled for client {OwnerClientId}");
                }
            }
            
            if (!IsOwner) 
            {
                ApplySyncedVisuals();
                ApplyPositionInterpolation();
                return;
            }
            
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_cameraController != null)
                {
                    _cameraController.ToggleCursorLock();
                }
            }
            
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            HandleCarInput();
            HandleShooting();
            HandleRespawn();
            
            if (Time.time - _lastSyncTime > _syncRate)
            {
                SendCarStateToServer();
                _lastSyncTime = Time.time;
            }
        }
        
        private void HandleRespawn()
        {
            if (Input.GetKeyDown(_respawnKey))
            {
                if (Time.time - _lastRespawnTime >= _respawnCooldown)
                {
                    _lastRespawnTime = Time.time;
                    RespawnCar();
                }
            }
        }
        
        private void RespawnCar()
        {
            if (carRigidbody == null) return;
            
            Vector3 respawnPosition = transform.position;
            respawnPosition.y += _respawnHeightOffset;
            
            Quaternion respawnRotation = Quaternion.identity;
            
            transform.position = respawnPosition;
            transform.rotation = respawnRotation;
            
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            
            if (frontLeftCollider != null)
            {
                frontLeftCollider.motorTorque = 0;
                frontLeftCollider.brakeTorque = 0;
                frontLeftCollider.steerAngle = 0;
            }
            if (frontRightCollider != null)
            {
                frontRightCollider.motorTorque = 0;
                frontRightCollider.brakeTorque = 0;
                frontRightCollider.steerAngle = 0;
            }
            if (rearLeftCollider != null)
            {
                rearLeftCollider.motorTorque = 0;
                rearLeftCollider.brakeTorque = 0;
                rearLeftCollider.steerAngle = 0;
            }
            if (rearRightCollider != null)
            {
                rearRightCollider.motorTorque = 0;
                rearRightCollider.brakeTorque = 0;
                rearRightCollider.steerAngle = 0;
            }
            
            steeringAxis = 0f;
            throttleAxis = 0f;
            driftingAxis = 0f;
            isDrifting = false;
            isTractionLocked = false;
            
            ResetFrictionToDefault();
            
            Debug.Log($"[CarController] Car respawned at {respawnPosition}");
        }
        
        private void ApplyPositionInterpolation()
        {
            if (!_isInterpolationEnabled) return;
            if (_syncedPosition == Vector3.zero) return;
            
            transform.position = Vector3.Lerp(transform.position, _syncedPosition, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(transform.rotation, _syncedRotation, Time.deltaTime * 15f);
        }
        
        private void SendCarStateToServer()
        {
            if (!IsOwner) return;
            if (_playerNetwork == null) return;
            
            float steering = GetCurrentSteeringAngle();
            float rpm = GetAverageWheelRPM();
            bool drifting = isDrifting;
            bool traction = isTractionLocked;
            Vector3 velocity = GetVelocity();
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            float speed = carSpeed;
            
            _playerNetwork.SendCarStateServerRpc(steering, rpm, drifting, traction, velocity, position, rotation, speed);
        }
        
        public void ApplySyncedCarState(float steeringAngle, float wheelRPM, bool drifting, bool tractionLocked, Vector3 velocity, Vector3 position, Quaternion rotation, float speed)
        {
            if (IsOwner) return;
            
            _syncedSteeringAngle = steeringAngle;
            _syncedWheelRPM = wheelRPM;
            _syncedIsDrifting = drifting;
            _syncedIsTractionLocked = tractionLocked;
            _syncedVelocity = velocity;
            _syncedPosition = position;
            _syncedRotation = rotation;
            _syncedCarSpeed = speed;
            
            if (carRigidbody != null)
            {
                carRigidbody.linearVelocity = velocity;
            }
            
            isDrifting = drifting;
            isTractionLocked = tractionLocked;
            carSpeed = speed;
            
            DriftCarPS();
            
            if (useUI && carSpeedText != null)
            {
                carSpeedText.text = Mathf.RoundToInt(Mathf.Abs(speed)).ToString();
            }
        }

        private void HandleCarInput()
        {
            if (Input.GetKey(KeyCode.W))
            {
                CancelInvoke("DecelerateCar");
                deceleratingCar = false;
                GoForward();
            }
            else if (Input.GetKey(KeyCode.S))
            {
                CancelInvoke("DecelerateCar");
                deceleratingCar = false;
                GoReverse();
            }
            else
            {
                ThrottleOff();
                if (!deceleratingCar)
                {
                    InvokeRepeating("DecelerateCar", 0f, 0.1f);
                    deceleratingCar = true;
                }
            }

            if (Input.GetKey(KeyCode.A))
            {
                TurnLeft();
            }
            else if (Input.GetKey(KeyCode.D))
            {
                TurnRight();
            }
            else if (steeringAxis != 0f)
            {
                ResetSteeringAngle();
            }

            if (Input.GetKey(KeyCode.Space))
            {
                CancelInvoke("DecelerateCar");
                deceleratingCar = false;
                Handbrake();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                RecoverTraction();
            }

            UpdateCarData();
        }

        private void HandleShooting()
        {
            if (_playerNetwork == null) return;
            if (!_playerNetwork.IsAlive.Value) return;
            if (_bulletPrefab == null) return;

            if (Input.GetKeyDown(_shootKey))
            {
                TryShoot();
            }
        }

        private void TryShoot()
        {
            Camera playerCamera = Camera.main;
            if (playerCamera == null) return;
            
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            Vector3 shootPosition = _shootPoint != null ? _shootPoint.position : transform.position + transform.forward * 2f;
            
            ShootServerRpc(shootPosition, ray.direction);
        }

        [ServerRpc]
        private void ShootServerRpc(Vector3 spawnPos, Vector3 direction)
        {
            if (!_playerNetwork.IsAlive.Value) return;
            if (Time.time < _lastShootTime + _shootCooldown) return;
            
            _lastShootTime = Time.time;

            GameObject bullet = Instantiate(_bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            BulletNetwork bulletScript = bullet.GetComponent<BulletNetwork>();
            if (bulletScript != null)
            {
                bulletScript.OwnerId = OwnerClientId;
            }
            
            bullet.GetComponent<NetworkObject>().Spawn();
        }

        private void UpdateCarData()
        {
            if (frontLeftCollider != null)
            {
                carSpeed = (2 * Mathf.PI * frontLeftCollider.radius * frontLeftCollider.rpm * 60) / 1000;
            }
            
            if (carRigidbody != null)
            {
                localVelocityX = transform.InverseTransformDirection(carRigidbody.linearVelocity).x;
                localVelocityZ = transform.InverseTransformDirection(carRigidbody.linearVelocity).z;
            }
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
            if (carRigidbody == null) return;
            
            ApplyMotorTorque();
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            AnimateWheelMeshes();
        }
        
        // ==================== COLLISION DAMAGE ====================
        
        private void OnCollisionEnter(Collision collision)
        {
            // Проверяем кулдаун столкновения
            if (Time.time - _lastCollisionTime < _collisionCooldown) return;
            
            // Получаем скорость столкновения в м/с
            float collisionSpeedMS = collision.relativeVelocity.magnitude;
            
            // Переводим в км/ч для удобства расчёта
            float speedKmh = collisionSpeedMS * 3.6f;
            
            if (speedKmh >= _minDamageSpeed)
            {
                _lastCollisionTime = Time.time;
                
                // Расчёт урона: линейная зависимость от скорости
                float damagePercent = Mathf.Clamp01((speedKmh - _minDamageSpeed) / (_maxDamageSpeed - _minDamageSpeed));
                int damage = Mathf.RoundToInt(damagePercent * _maxDamage);
                damage = Mathf.Max(1, damage);
                
                Debug.Log($"[CarController] Collision! Speed: {speedKmh:F1} km/h, Calculated Damage: {damage}");
                
                // Отправляем урон на сервер
                SendCollisionDamageServerRpc(damage);
            }
        }
        
        [ServerRpc]
        private void SendCollisionDamageServerRpc(int damage)
        {
            if (_playerNetwork == null) return;
            if (!_playerNetwork.IsAlive.Value) return;
            
            Debug.Log($"[Server] Applying collision damage {damage} to player {OwnerClientId}");
            
            // Применяем урон от столкновения (урон самому себе)
            _playerNetwork.TakeDamage(damage, OwnerClientId);
        }
        
        // ==================== SYNC WHEELS ====================
        
        private void ApplySyncedVisuals()
        {
            _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, _syncedSteeringAngle, Time.deltaTime * 15f);
            
            if (Mathf.Abs(_syncedWheelRPM) > 0.1f)
            {
                float spinDelta = _syncedWheelRPM * 360f * Time.deltaTime / 60f;
                _wheelSpinX += spinDelta;
                
                if (_wheelSpinX > 360f) _wheelSpinX -= 360f;
                if (_wheelSpinX < -360f) _wheelSpinX += 360f;
            }
            
            ApplyWheelTransform(frontLeftMesh, true, _currentSteerAngle);
            ApplyWheelTransform(frontRightMesh, true, _currentSteerAngle);
            ApplyWheelTransform(rearLeftMesh, false, 0f);
            ApplyWheelTransform(rearRightMesh, false, 0f);
        }
        
        private void ApplyWheelTransform(GameObject wheelMesh, bool isFront, float steerAngle)
        {
            if (wheelMesh == null) return;
            
            if (isFront)
            {
                Quaternion spinRotation = Quaternion.Euler(_wheelSpinX, 0, 0);
                Quaternion steerRotation = Quaternion.Euler(0, steerAngle, 0);
                wheelMesh.transform.localRotation = steerRotation * spinRotation;
            }
            else
            {
                wheelMesh.transform.localRotation = Quaternion.Euler(_wheelSpinX, 0, 0);
            }
        }

        private void AnimateWheelMeshes()
        {
            if (IsOwner)
            {
                UpdateWheelPoseSimple(frontLeftCollider, frontLeftMesh);
                UpdateWheelPoseSimple(frontRightCollider, frontRightMesh);
                UpdateWheelPoseSimple(rearLeftCollider, rearLeftMesh);
                UpdateWheelPoseSimple(rearRightCollider, rearRightMesh);
            }
            else
            {
                UpdateWheelPositionOnly(frontLeftCollider, frontLeftMesh);
                UpdateWheelPositionOnly(frontRightCollider, frontRightMesh);
                UpdateWheelPositionOnly(rearLeftCollider, rearLeftMesh);
                UpdateWheelPositionOnly(rearRightCollider, rearRightMesh);
            }
        }
        
        private void UpdateWheelPoseSimple(WheelCollider collider, GameObject mesh)
        {
            if (mesh == null || collider == null) return;
            
            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.transform.position = position;
            mesh.transform.rotation = rotation;
        }
        
        private void UpdateWheelPositionOnly(WheelCollider collider, GameObject mesh)
        {
            if (mesh == null || collider == null) return;
            
            collider.GetWorldPose(out Vector3 position, out Quaternion _);
            mesh.transform.position = position;
        }

        public void CarSpeedUI()
        {
            if (useUI && carSpeedText != null && IsOwner)
            {
                carSpeedText.text = Mathf.RoundToInt(Mathf.Abs(carSpeed)).ToString();
            }
        }

        public void CarSounds()
        {
            if (!useSounds) return;
            if (carRigidbody == null) return;

            if (carEngineSound != null)
            {
                float engineSoundPitch = initialCarEngineSoundPitch + (Mathf.Abs(carRigidbody.linearVelocity.magnitude) / 25f);
                carEngineSound.pitch = engineSoundPitch;

                if (!carEngineSound.isPlaying && IsOwner)
                    carEngineSound.Play();
            }

            if ((isDrifting || isTractionLocked) && Mathf.Abs(carSpeed) > 12f)
            {
                if (tireScreechSound != null && !tireScreechSound.isPlaying && IsOwner)
                    tireScreechSound.Play();
            }
            else
            {
                if (tireScreechSound != null && tireScreechSound.isPlaying)
                    tireScreechSound.Stop();
            }
        }

        public void TurnLeft()
        {
            steeringAxis -= Time.deltaTime * 10f * steeringSpeed;
            steeringAxis = Mathf.Clamp(steeringAxis, -1f, 1f);
            ApplySteeringAngle();
        }

        public void TurnRight()
        {
            steeringAxis += Time.deltaTime * 10f * steeringSpeed;
            steeringAxis = Mathf.Clamp(steeringAxis, -1f, 1f);
            ApplySteeringAngle();
        }

        public void ResetSteeringAngle()
        {
            steeringAxis = Mathf.MoveTowards(steeringAxis, 0f, Time.deltaTime * 10f * steeringSpeed);
            ApplySteeringAngle();
        }

        private void ApplySteeringAngle()
        {
            if (frontLeftCollider == null || frontRightCollider == null) return;
            
            float steeringAngle = steeringAxis * maxSteeringAngle;
            frontLeftCollider.steerAngle = Mathf.Lerp(frontLeftCollider.steerAngle, steeringAngle, steeringSpeed);
            frontRightCollider.steerAngle = Mathf.Lerp(frontRightCollider.steerAngle, steeringAngle, steeringSpeed);
        }

        public void GoForward()
        {
            throttleAxis = Mathf.Min(throttleAxis + Time.deltaTime * 3f, 1f);
            isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
            DriftCarPS();
        }

        public void GoReverse()
        {
            throttleAxis = Mathf.Max(throttleAxis - Time.deltaTime * 3f, -1f);
            isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
            DriftCarPS();
        }

        public void ThrottleOff()
        {
            throttleAxis = Mathf.MoveTowards(throttleAxis, 0f, Time.deltaTime * 10f);
        }

        public void DecelerateCar()
        {
            if (carRigidbody == null) return;
            
            isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
            DriftCarPS();

            throttleAxis = Mathf.MoveTowards(throttleAxis, 0f, Time.deltaTime * 10f);
            carRigidbody.linearVelocity *= (1f / (1f + (0.025f * decelerationMultiplier)));

            if (carRigidbody.linearVelocity.magnitude < 0.25f)
            {
                carRigidbody.linearVelocity = Vector3.zero;
                CancelInvoke("DecelerateCar");
            }
        }

        public void Brakes()
        {
            if (frontLeftCollider == null) return;
            
            frontLeftCollider.brakeTorque = brakeForce;
            frontRightCollider.brakeTorque = brakeForce;
            rearLeftCollider.brakeTorque = brakeForce;
            rearRightCollider.brakeTorque = brakeForce;
        }

        public void Handbrake()
        {
            CancelInvoke("RecoverTraction");
            driftingAxis = Mathf.Min(driftingAxis + Time.deltaTime, 1f);
            isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
            isTractionLocked = true;

            UpdateDriftFriction();
            DriftCarPS();
        }

        public void RecoverTraction()
        {
            isTractionLocked = false;
            driftingAxis = Mathf.Max(driftingAxis - Time.deltaTime / 1.5f, 0f);
            UpdateDriftFriction();

            if (driftingAxis <= 0f)
            {
                ResetFrictionToDefault();
            }
            else
            {
                Invoke("RecoverTraction", Time.deltaTime);
            }
        }

        private void UpdateDriftFriction()
        {
            if (frontLeftCollider == null) return;
            
            float driftValue = FLWextremumSlip * handbrakeDriftMultiplier * driftingAxis;

            FLwheelFriction.extremumSlip = driftValue;
            frontLeftCollider.sidewaysFriction = FLwheelFriction;

            FRwheelFriction.extremumSlip = driftValue;
            frontRightCollider.sidewaysFriction = FRwheelFriction;

            RLwheelFriction.extremumSlip = driftValue;
            rearLeftCollider.sidewaysFriction = RLwheelFriction;

            RRwheelFriction.extremumSlip = driftValue;
            rearRightCollider.sidewaysFriction = RRwheelFriction;
        }

        private void ResetFrictionToDefault()
        {
            if (frontLeftCollider == null) return;
            
            FLwheelFriction.extremumSlip = FLWextremumSlip;
            frontLeftCollider.sidewaysFriction = FLwheelFriction;

            FRwheelFriction.extremumSlip = FRWextremumSlip;
            frontRightCollider.sidewaysFriction = FRwheelFriction;

            RLwheelFriction.extremumSlip = RLWextremumSlip;
            rearLeftCollider.sidewaysFriction = RLwheelFriction;

            RRwheelFriction.extremumSlip = RRWextremumSlip;
            rearRightCollider.sidewaysFriction = RRwheelFriction;
        }

        private void ApplyMotorTorque()
        {
            if (frontLeftCollider == null) return;
            
            if (localVelocityZ < -1f && throttleAxis > 0f)
            {
                Brakes();
                return;
            }

            if (localVelocityZ > 1f && throttleAxis < 0f)
            {
                Brakes();
                return;
            }

            float currentMaxSpeed = throttleAxis > 0f ? maxSpeed : maxReverseSpeed;
            
            if (Mathf.Abs(carSpeed) < currentMaxSpeed || Mathf.Sign(carSpeed) != Mathf.Sign(throttleAxis))
            {
                float torque = accelerationMultiplier * 50f * throttleAxis;
                frontLeftCollider.motorTorque = torque;
                frontRightCollider.motorTorque = torque;
                rearLeftCollider.motorTorque = torque;
                rearRightCollider.motorTorque = torque;
                frontLeftCollider.brakeTorque = 0;
                frontRightCollider.brakeTorque = 0;
                rearLeftCollider.brakeTorque = 0;
                rearRightCollider.brakeTorque = 0;
            }
            else
            {
                frontLeftCollider.motorTorque = 0;
                frontRightCollider.motorTorque = 0;
                rearLeftCollider.motorTorque = 0;
                rearRightCollider.motorTorque = 0;
            }
        }

        private void DriftCarPS()
        {
            if (!useEffects) return;

            if (isDrifting)
            {
                if (RLWParticleSystem != null && !RLWParticleSystem.isPlaying) RLWParticleSystem.Play();
                if (RRWParticleSystem != null && !RRWParticleSystem.isPlaying) RRWParticleSystem.Play();
            }
            else
            {
                if (RLWParticleSystem != null && RLWParticleSystem.isPlaying) RLWParticleSystem.Stop();
                if (RRWParticleSystem != null && RRWParticleSystem.isPlaying) RRWParticleSystem.Stop();
            }

            if ((isTractionLocked || Mathf.Abs(localVelocityX) > 5f) && Mathf.Abs(carSpeed) > 12f)
            {
                if (RLWTireSkid != null) RLWTireSkid.emitting = true;
                if (RRWTireSkid != null) RRWTireSkid.emitting = true;
            }
            else
            {
                if (RLWTireSkid != null) RLWTireSkid.emitting = false;
                if (RRWTireSkid != null) RRWTireSkid.emitting = false;
            }
        }

        public float GetCurrentSteeringAngle()
        {
            return frontLeftCollider != null ? frontLeftCollider.steerAngle : 0f;
        }
        
        public float GetAverageWheelRPM()
        {
            if (frontLeftCollider == null) return 0f;
            return (frontLeftCollider.rpm + frontRightCollider.rpm + rearLeftCollider.rpm + rearRightCollider.rpm) / 4f;
        }
        
        public Vector3 GetVelocity()
        {
            return carRigidbody != null ? carRigidbody.linearVelocity : Vector3.zero;
        }
        
        public Vector3 GetPosition()
        {
            return transform.position;
        }
        
        public Quaternion GetRotation()
        {
            return transform.rotation;
        }
        
        public void ResetCarState()
        {
            steeringAxis = 0f;
            throttleAxis = 0f;
            driftingAxis = 0f;
            deceleratingCar = false;
            isDrifting = false;
            isTractionLocked = false;
            _syncedSteeringAngle = 0f;
            _syncedWheelRPM = 0f;
            _syncedIsDrifting = false;
            _syncedIsTractionLocked = false;
            _syncedVelocity = Vector3.zero;
            _syncedCarSpeed = 0f;
            _wheelSpinX = 0f;
            _currentSteerAngle = 0f;
            
            ResetSteeringAngle();
            
            if (carRigidbody != null && IsOwner)
            {
                carRigidbody.linearVelocity = Vector3.zero;
                carRigidbody.angularVelocity = Vector3.zero;
            }
            
            if (frontLeftCollider != null)
            {
                frontLeftCollider.motorTorque = 0;
                frontLeftCollider.brakeTorque = 0;
                frontLeftCollider.steerAngle = 0;
            }
            if (frontRightCollider != null)
            {
                frontRightCollider.motorTorque = 0;
                frontRightCollider.brakeTorque = 0;
                frontRightCollider.steerAngle = 0;
            }
            if (rearLeftCollider != null)
            {
                rearLeftCollider.motorTorque = 0;
                rearLeftCollider.brakeTorque = 0;
                rearLeftCollider.steerAngle = 0;
            }
            if (rearRightCollider != null)
            {
                rearRightCollider.motorTorque = 0;
                rearRightCollider.brakeTorque = 0;
                rearRightCollider.steerAngle = 0;
            }
            
            ResetFrictionToDefault();
            
            if (_cameraController != null && IsOwner)
            {
                _cameraController.ResetCameraAngles();
            }
        }
    }
}