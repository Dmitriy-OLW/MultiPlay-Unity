using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Multi.FishNet
{
    public class ClientNetworkTransform : NetworkBehaviour
    {
        [SerializeField] private Transform _transform;
        [SerializeField] private float _interpolationSpeed = 15f;
        
        // Синхронизируемая позиция
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        
        // Для клиентского предсказания
        private Vector3 _lastSentPosition;
        private float _lastSendTime;
        [SerializeField] private float _sendInterval = 0.05f; // 20 Hz
        
        private void Awake()
        {
            if (_transform == null)
                _transform = transform;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
            {
                _targetPosition = _transform.position;
                _targetRotation = _transform.rotation;
            }
        }
        
        private void Update()
        {
            if (IsOwner)
            {
                // Владелец: отправляем позицию на сервер
                if (Time.time - _lastSendTime >= _sendInterval)
                {
                    SendTransformToServer(_transform.position, _transform.rotation);
                    _lastSendTime = Time.time;
                }
            }
            else
            {
                // Не владелец: интерполируем
                _transform.position = Vector3.Lerp(
                    _transform.position, 
                    _targetPosition, 
                    Time.deltaTime * _interpolationSpeed
                );
                _transform.rotation = Quaternion.Slerp(
                    _transform.rotation, 
                    _targetRotation, 
                    Time.deltaTime * _interpolationSpeed
                );
            }
        }
        
        [ServerRpc]
        private void SendTransformToServer(Vector3 position, Quaternion rotation)
        {
            // Сервер получает и рассылает всем
            SetTransformObservers(position, rotation);
        }
        
        [ObserversRpc]
        private void SetTransformObservers(Vector3 position, Quaternion rotation)
        {
            if (IsOwner) return; // Владелец не применяет свои же данные
            
            _targetPosition = position;
            _targetRotation = rotation;
        }
    }
}