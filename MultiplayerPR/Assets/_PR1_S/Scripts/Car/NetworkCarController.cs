using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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
        public Text carSpeedText;

        [Header("Sounds")]
        public bool useSounds = false;
        public AudioSource carEngineSound;
        public AudioSource tireScreechSound;
        float initialCarEngineSoundPitch;

        [Header("Combat - Network Integration")]
        [SerializeField] private PlayerNetwork _playerNetwork;
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _shootPoint;
        [SerializeField] private KeyCode _shootKey = KeyCode.Mouse0;

        // Car data
        [HideInInspector]
        public float carSpeed;
        [HideInInspector]
        public bool isDrifting;
        [HideInInspector]
        public bool isTractionLocked;

        // Private variables
        private Rigidbody carRigidbody;
        private float steeringAxis;
        private float throttleAxis;
        private float driftingAxis;
        private float localVelocityZ;
        private float localVelocityX;
        private bool deceleratingCar;

        // Friction curves storage
        private WheelFrictionCurve FLwheelFriction;
        private float FLWextremumSlip;
        private WheelFrictionCurve FRwheelFriction;
        private float FRWextremumSlip;
        private WheelFrictionCurve RLwheelFriction;
        private float RLWextremumSlip;
        private WheelFrictionCurve RRwheelFriction;
        private float RRWextremumSlip;

        private float _lastShootTime;
        private float _shootCooldown = 0.5f;

        private void Start()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();

            if (IsOwner)
            {
                _playerCamera = Camera.main;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            SetupCarPhysics();
            SetupSounds();
        }

        // Добавьте этот метод в NetworkCarController
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
    
            if (!IsOwner && _playerNetwork != null)
            {
                StartCoroutine(WaitAndApplyPosition());
            }
        }

        private IEnumerator WaitAndApplyPosition()
        {
            yield return new WaitForSeconds(0.1f);
    
            if (_playerNetwork != null)
            {
                if (_playerNetwork.SpawnPosition.Value != Vector3.zero)
                {
                    transform.position = _playerNetwork.SpawnPosition.Value;
                    transform.rotation = _playerNetwork.SpawnRotation.Value;
            
                    // Сбрасываем физику
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
            
                    Debug.Log($"[Car] Applied position: {transform.position}, rotation: {transform.rotation.eulerAngles}");
                }
            }
        }
        
        private void SetupCarPhysics()
        {
            carRigidbody = GetComponent<Rigidbody>();
            if (carRigidbody != null)
            {
                carRigidbody.centerOfMass = bodyMassCenter;
            }

            // Save default friction values
            FLwheelFriction = frontLeftCollider.sidewaysFriction;
            FLWextremumSlip = frontLeftCollider.sidewaysFriction.extremumSlip;
            FRwheelFriction = frontRightCollider.sidewaysFriction;
            FRWextremumSlip = frontRightCollider.sidewaysFriction.extremumSlip;
            RLwheelFriction = rearLeftCollider.sidewaysFriction;
            RLWextremumSlip = rearLeftCollider.sidewaysFriction.extremumSlip;
            RRwheelFriction = rearRightCollider.sidewaysFriction;
            RRWextremumSlip = rearRightCollider.sidewaysFriction.extremumSlip;
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
            if (!IsOwner) return;

            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                HandleCursorUnlock();
                return;
            }

            HandleCarInput();
            HandleShooting();
        }

        private void HandleCursorUnlock()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HandleCarInput()
        {
            // Throttle / Reverse
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

            // Steering
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

            // Handbrake
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
            if (_playerCamera == null) return;
            if (_playerNetwork == null) return;
            if (!_playerNetwork.IsAlive.Value) return;

            if (Input.GetKeyDown(_shootKey))
            {
                TryShoot();
            }
        }

        private void TryShoot()
        {
            Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                PlayerNetwork targetPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

                if (targetPlayer != null && targetPlayer != _playerNetwork)
                {
                    ShootServerRpc(targetPlayer.NetworkObjectId);
                }
            }
        }

        [ServerRpc]
        private void ShootServerRpc(ulong targetObjectId)
        {
            if (!_playerNetwork.IsAlive.Value) return;
            if (Time.time < _lastShootTime + _shootCooldown) return;

            _lastShootTime = Time.time;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out NetworkObject targetObject))
            {
                PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();
                if (targetPlayer != null && targetPlayer != _playerNetwork)
                {
                    targetPlayer.TakeDamage(25, OwnerClientId);
                    Debug.Log($"[Server] Car {OwnerClientId} shot {targetObjectId}");
                }
            }
        }

        private void UpdateCarData()
        {
            carSpeed = (2 * Mathf.PI * frontLeftCollider.radius * frontLeftCollider.rpm * 60) / 1000;
            localVelocityX = transform.InverseTransformDirection(carRigidbody.linearVelocity).x;
            localVelocityZ = transform.InverseTransformDirection(carRigidbody.linearVelocity).z;
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;
            ApplyMotorTorque();
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            AnimateWheelMeshes();
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (_playerCamera == null || !IsOwner) return;
    
            // Камера следует за машиной с учетом её ротации
            Vector3 targetPosition = transform.position - transform.forward * 5f + Vector3.up * 3f;
            _playerCamera.transform.position = Vector3.Lerp(_playerCamera.transform.position, targetPosition, Time.deltaTime * 8f);
            _playerCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
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

            if (carEngineSound != null)
            {
                float engineSoundPitch = initialCarEngineSoundPitch + (Mathf.Abs(carRigidbody.linearVelocity.magnitude) / 25f);
                carEngineSound.pitch = engineSoundPitch;

                if (!carEngineSound.isPlaying && IsOwner)
                    carEngineSound.Play();
            }

            if ((isDrifting || isTractionLocked) && Mathf.Abs(carSpeed) > 12f)
            {
                if (!tireScreechSound.isPlaying && IsOwner)
                    tireScreechSound.Play();
            }
            else
            {
                if (tireScreechSound.isPlaying)
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

        private void AnimateWheelMeshes()
        {
            UpdateWheelPose(frontLeftCollider, frontLeftMesh);
            UpdateWheelPose(frontRightCollider, frontRightMesh);
            UpdateWheelPose(rearLeftCollider, rearLeftMesh);
            UpdateWheelPose(rearRightCollider, rearRightMesh);
        }

        private void UpdateWheelPose(WheelCollider collider, GameObject mesh)
        {
            if (mesh == null || collider == null) return;
            
            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.transform.position = position;
            mesh.transform.rotation = rotation;
        }

        public Vector3 GetCameraForward()
        {
            return _playerCamera != null ? _playerCamera.transform.forward : transform.forward;
        }

        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }

        // Добавленный метод для сброса состояния машины при респавне
        public void ResetCarState()
        {
            // Сброс управления
            steeringAxis = 0f;
            throttleAxis = 0f;
            driftingAxis = 0f;
            deceleratingCar = false;
            isDrifting = false;
            isTractionLocked = false;
            
            // Сброс колес
            ResetSteeringAngle();
            
            // Сброс физики
            if (carRigidbody != null)
            {
                carRigidbody.linearVelocity = Vector3.zero;
                carRigidbody.angularVelocity = Vector3.zero;
            }
            
            // Сброс WheelColliders
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
            
            // Сброс фрикций до стандартных
            ResetFrictionToDefault();
            
            Debug.Log($"[Car] Reset state for {OwnerClientId}");
        }
    }
}