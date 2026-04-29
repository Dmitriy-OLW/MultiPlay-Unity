using FishNet.Object;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Vector3 _offset = new(0f, 4f, -2f);
    [SerializeField] private GameObject _cameraView;

    [Header("Mouse Look")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _minVerticalAngle = -30f;
    [SerializeField] private float _maxVerticalAngle = 60f;

    private Camera _cam;
    private float _verticalRotation = 0f;
    private float _horizontalRotation = 0f;

    public override void OnStartNetwork()
    {
        if (!base.Owner.IsLocalClient)
        {
            enabled = false;
            return;
        }

        _cam = Camera.main;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;
        if (!base.Owner.IsLocalClient) return;
        
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        _horizontalRotation += mouseX;
        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, _minVerticalAngle, _maxVerticalAngle);
        
        Quaternion rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0);
        Vector3 rotatedOffset = rotation * _offset;

        _cam.transform.position = transform.position + rotatedOffset;
        _cam.transform.LookAt(_cameraView.transform.position);
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}