using UnityEngine;

namespace Multi.PR1
{
    public class BillboardUI : MonoBehaviour
    {
        private Camera _mainCamera;

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null) return;

            transform.LookAt(
                transform.position + _mainCamera.transform.rotation * Vector3.forward,
                _mainCamera.transform.rotation * Vector3.up
            );
        }
    }
}