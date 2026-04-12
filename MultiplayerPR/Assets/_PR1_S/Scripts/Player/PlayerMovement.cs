using UnityEngine;
using Unity.Netcode;

namespace Multi.PR1
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Движение")] 
        [SerializeField] private float MoveSpeed = 5f;
        
        [SerializeField] private float DashSpeed = 20f;
        [SerializeField] private float DashDuration = 0.2f;
        [SerializeField] private float DashCooldown = 1.5f;

        [Header("Камера")] public float MouseSensitivity = 3f;
        [SerializeField] private float NormalCameraDistance = 5f;
        [SerializeField] private float AimCameraDistance = 2f;
        [SerializeField] private Vector2 PitchMinMax = new Vector2(-40, 85);

        [SerializeField] private Transform _transform;
        
        private Camera _mainCamera;
        private float _yaw;
        private float _pitch;
        private float _currentCameraDistance;

        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector3 _dashDirection;
        
        private PlayerNetwork _playerNetwork;

        private void Start()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
    
            if (IsOwner)
            {
                _mainCamera = Camera.main;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _yaw = transform.eulerAngles.y;
                _currentCameraDistance = NormalCameraDistance;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            
            if (_playerNetwork != null && !_playerNetwork.IsAlive.Value) return;
    
            HandleCursorLock();
            HandleCameraInput();
            HandleMovement();
        }

        private void LateUpdate()
        {
            if (_mainCamera == null) return;

            Vector3 targetCenter = _transform.position + Vector3.up * 1.5f;
            Quaternion camRotation = Quaternion.Euler(_pitch, _yaw, 0);

            bool isAiming = Input.GetMouseButton(1) && Cursor.lockState == CursorLockMode.Locked;

            float targetDistance = isAiming ? AimCameraDistance : NormalCameraDistance;
            _currentCameraDistance = Mathf.Lerp(_currentCameraDistance, targetDistance, Time.deltaTime * 10f);

            Vector3 camPosition = targetCenter - camRotation * Vector3.forward * _currentCameraDistance;

            if (isAiming)
            {
                camPosition += camRotation * Vector3.right * 0.8f;
            }

            _mainCamera.transform.position = camPosition;
            _mainCamera.transform.rotation = camRotation;
        }

        private void HandleCursorLock()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void HandleCameraInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, PitchMinMax.x, PitchMinMax.y);
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(horizontal, 0, vertical).normalized;

            Vector3 moveDir = Vector3.zero;

            if (inputDir.magnitude >= 0.1f)
            {
                Vector3 camForward = _mainCamera.transform.forward;
                Vector3 camRight = _mainCamera.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();

                moveDir = camForward * inputDir.z + camRight * inputDir.x;
            }

            if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.LeftShift) && _dashCooldownTimer <= 0 && moveDir != Vector3.zero &&
                Cursor.lockState == CursorLockMode.Locked)
            {
                _dashTimer = DashDuration;
                _dashCooldownTimer = DashCooldown;
                _dashDirection = moveDir;
            }

            float currentSpeed = MoveSpeed;
            Vector3 finalMovement = Vector3.zero;

            if (_dashTimer > 0)
            {
                _dashTimer -= Time.deltaTime;
                currentSpeed = DashSpeed;
                finalMovement = _dashDirection * currentSpeed;
            }
            else
            {
                finalMovement = moveDir * currentSpeed;
            }
            
            _transform.position += finalMovement * Time.deltaTime;

            if (moveDir != Vector3.zero && _dashTimer <= 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        public Vector3 GetCameraForward()
        {
            return _mainCamera != null ? _mainCamera.transform.forward : _transform.forward;
        }

        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }
    }
}