using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class BulletNetwork : NetworkBehaviour
    {
        [SerializeField] private float Speed = 10f;
        [SerializeField] private int Damage = 25;
        [SerializeField] private float LifeTime = 3f;

        public ulong OwnerId;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Invoke(nameof(DestroyBullet), LifeTime);
            }
        }

        private void Update()
        {
            transform.position += transform.forward * Speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            
            if (other.TryGetComponent(out PlayerNetwork hitPlayer))
            {
                if (hitPlayer.OwnerClientId == OwnerId) return;

                hitPlayer.TakeDamage(Damage, OwnerId);
                DestroyBullet();
            }
            else if (!other.isTrigger) 
            {
                DestroyBullet();
            }
        }

        private void DestroyBullet()
        {
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(); 
            }
        }
    }
}