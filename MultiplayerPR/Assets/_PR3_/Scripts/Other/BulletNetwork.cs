using FishNet.Object;
using UnityEngine;

namespace Multi.FishNet
{
    public class BulletNetwork : NetworkBehaviour
    {
        [SerializeField] private float _speed = 10f;
        [SerializeField] private int _damage = 25;
        [SerializeField] private float _lifeTime = 3f;
        
        private bool _hasHit = false;
        public int OwnerId;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            Invoke(nameof(DestroyBullet), _lifeTime);
        }
        
        private void Update()
        {
            transform.position += transform.forward * _speed * Time.deltaTime;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServer || _hasHit) return;
            
            if (other.TryGetComponent(out PlayerNetwork hitPlayer))
            {
                if (hitPlayer.Owner.ClientId == OwnerId) return;
                
                hitPlayer.TakeDamage(_damage, OwnerId);
                _hasHit = true;
                DestroyBullet();
            }
            else if (!other.isTrigger)
            {
                _hasHit = true;
                DestroyBullet();
            }
        }
        
        private void DestroyBullet()
        {
            if (IsSpawned)
                base.Despawn();
        }
    }
}