using FishNet.Object;
using UnityEngine;

namespace Multi.FishNet
{
    public class HealthPickup : NetworkBehaviour
    {
        [SerializeField] private int _healAmount = 50;
        [SerializeField] private float _rotationSpeed = 90f;
        
        private PickupManager _manager;
        private int _spawnPointIndex;
        private Vector3 _spawnPosition;
        
        public void Init(PickupManager manager, int spawnPointIndex, Vector3 spawnPosition)
        {
            _manager = manager;
            _spawnPointIndex = spawnPointIndex;
            _spawnPosition = spawnPosition;
        }
        
        private void Update()
        {
            if (!IsSpawned) return;
            transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServer) return;
            
            PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null) return;
            
            // Проверяем через .Value
            if (!player.IsAlive.Value) return;
            if (player.Health.Value >= 100) return;
            
            player.Heal(_healAmount);
            Debug.Log($"[Server] Player {player.Owner.ClientId} picked up health pack");
            
            if (_manager != null)
                _manager.OnHealthPickupCollected(_spawnPointIndex);
            
            base.Despawn();
        }
    }
}