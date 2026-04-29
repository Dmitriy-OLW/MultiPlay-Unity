using FishNet;
using UnityEngine;
using System.Collections;

public class PickupManager : MonoBehaviour
{
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _respawnDelay = 10f;
    private bool _spawned = false;

    private void Update()
    {
        if (InstanceFinder.IsServer)
        {
            Invoke(nameof(SpawnAll), 0.1f);
        }
    }

    private void SpawnAll()
    {
        if (_spawned) return;

        _spawned = true;
        foreach (var point in _spawnPoints)
            SpawnPickup(point.position);
    }

    public void OnPickedUp(Vector3 position)
    {
        StartCoroutine(RespawnAfterDelay(position));
    }

    private IEnumerator RespawnAfterDelay(Vector3 position)
    {
        yield return new WaitForSeconds(_respawnDelay);
        SpawnPickup(position);
    }

    private void SpawnPickup(Vector3 position)
    {
        GameObject go = Instantiate(_healthPickupPrefab, position, Quaternion.identity);
        go.GetComponent<HealthPickup>().Init(this);
        InstanceFinder.ServerManager.Spawn(go);
        Debug.Log($"Pickup spawned at {position}, active: {go.activeSelf}");
    }
}