using FishNet.Object;
using UnityEngine;

public class HealthPickup : NetworkBehaviour
{
    [SerializeField] private int _healAmount = 40;
    private PickupManager _manager;
    private Vector3 _spawnPosition;

    public void Init(PickupManager manager)
    {
        _manager = manager;
        _spawnPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;

        var player = other.GetComponent<PlayerNetwork>();
        if (player == null) return;
        if (!player.IsAlive.Value) return;
        if (player.HP.Value >= 100) return;

        player.HP.Value = Mathf.Min(100, player.HP.Value + _healAmount);
        _manager.OnPickedUp(_spawnPosition);
        base.Despawn(gameObject);
    }
}