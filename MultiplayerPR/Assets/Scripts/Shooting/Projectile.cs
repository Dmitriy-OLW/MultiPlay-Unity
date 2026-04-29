using FishNet.Object;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float _speed = 18f;
    [SerializeField] private int _damage = 20;

    private void Update()
    {
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;

        var target = other.GetComponentInParent<PlayerNetwork>();
        if (target == null) return;
        if (target.OwnerId == base.OwnerId) return;
        if (!target.IsAlive.Value) return;

        int newHp = Mathf.Max(0, target.HP.Value - _damage);
        target.HP.Value = newHp;
        
        if (newHp <= 0)
        {
            PlayerNetwork shooter = GetShooter();
            if (shooter != null)
            {
                shooter.AddScore(1);
            }
        }

        base.Despawn(gameObject);
    }

    private PlayerNetwork GetShooter()
    {
        if (base.Owner == null) return null;
        return base.Owner.FirstObject?.GetComponent<PlayerNetwork>();
    }
}