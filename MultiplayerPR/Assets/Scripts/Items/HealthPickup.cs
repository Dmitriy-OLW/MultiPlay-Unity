using FishNet.Object;
using UnityEngine;

public class HealthPickup : NetworkBehaviour
{
    [SerializeField] private int _healAmount = 40;
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
        if (player.HP.Value >= 100) return;

        player.HP.Value = Mathf.Min(100, player.HP.Value + _healAmount);

        Debug.Log($"[Server] Player {player.OwnerId} picked up health pack");

        if (_manager != null)
            _manager.OnHealthPickupCollected(_spawnPointIndex);

        base.Despawn(gameObject);
    }
}