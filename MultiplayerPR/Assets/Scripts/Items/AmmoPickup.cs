using FishNet.Object;
using UnityEngine;

public class AmmoPickup : NetworkBehaviour
{
    [SerializeField] private int _ammoAmount = 10;
    [SerializeField] private float _rotationSpeed = 90f;

    private PickupManager _manager;
    private int _spawnPointIndex;

    public void Init(PickupManager manager, int spawnPointIndex)
    {
        _manager = manager;
        _spawnPointIndex = spawnPointIndex;
    }

    private void Update()
    {
        if (!base.IsSpawned) return;
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;

        var player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null) return;
        if (!player.IsAlive.Value) return;
        if (player.Ammo.Value >= 10) return;

        player.Ammo.Value = Mathf.Min(10, player.Ammo.Value + _ammoAmount);

        Debug.Log($"[Server] Player {player.OwnerId} picked up ammo pack");

        if (_manager != null)
            _manager.OnAmmoPickupCollected(_spawnPointIndex);

        base.Despawn(gameObject);
    }
}