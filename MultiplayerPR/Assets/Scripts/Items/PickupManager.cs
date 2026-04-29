using FishNet.Object;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PickupManager : NetworkBehaviour
{
    [Header("Pickup Prefabs")]
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private GameObject _ammoPickupPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] _healthSpawnPoints;
    [SerializeField] private Transform[] _ammoSpawnPoints;

    [Header("Spawn Settings")]
    [SerializeField] private float _healthRespawnDelay = 10f;
    [SerializeField] private float _ammoRespawnDelay = 8f;
    [SerializeField] private int _initialHealthPickups = 3;
    [SerializeField] private int _initialAmmoPickups = 4;

    private Queue<int> _availableHealthSpawnPoints = new Queue<int>();
    private Queue<int> _availableAmmoSpawnPoints = new Queue<int>();

    public static PickupManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public override void OnStartNetwork()
    {
        if (!base.IsServerInitialized) return;

        InitializeSpawnPointQueues();
        SpawnInitialPickups();
    }

    private void InitializeSpawnPointQueues()
    {
        for (int i = 0; i < _healthSpawnPoints.Length; i++)
            _availableHealthSpawnPoints.Enqueue(i);

        for (int i = 0; i < _ammoSpawnPoints.Length; i++)
            _availableAmmoSpawnPoints.Enqueue(i);
    }

    private void SpawnInitialPickups()
    {
        for (int i = 0; i < _initialHealthPickups && _availableHealthSpawnPoints.Count > 0; i++)
            SpawnHealthPickup();

        for (int i = 0; i < _initialAmmoPickups && _availableAmmoSpawnPoints.Count > 0; i++)
            SpawnAmmoPickup();
    }

    public void SpawnHealthPickup()
    {
        if (!base.IsServerInitialized) return;
        if (_availableHealthSpawnPoints.Count == 0) return;

        int pointIndex = _availableHealthSpawnPoints.Dequeue();
        Transform spawnPoint = _healthSpawnPoints[pointIndex];

        GameObject pickup = Instantiate(_healthPickupPrefab, spawnPoint.position, Quaternion.identity);
        HealthPickup healthPickup = pickup.GetComponent<HealthPickup>();
        healthPickup.Init(this, pointIndex);

        base.ServerManager.Spawn(pickup);
        Debug.Log($"[Server] Spawned health pickup at point {pointIndex}");
    }

    public void SpawnAmmoPickup()
    {
        if (!base.IsServerInitialized) return;
        if (_availableAmmoSpawnPoints.Count == 0) return;

        int pointIndex = _availableAmmoSpawnPoints.Dequeue();
        Transform spawnPoint = _ammoSpawnPoints[pointIndex];

        GameObject pickup = Instantiate(_ammoPickupPrefab, spawnPoint.position, Quaternion.identity);
        AmmoPickup ammoPickup = pickup.GetComponent<AmmoPickup>();
        ammoPickup.Init(this, pointIndex);

        base.ServerManager.Spawn(pickup);
        Debug.Log($"[Server] Spawned ammo pickup at point {pointIndex}");
    }

    public void OnHealthPickupCollected(int spawnPointIndex)
    {
        if (!base.IsServerInitialized) return;
        StartCoroutine(RespawnHealthPickupCoroutine(spawnPointIndex));
    }

    public void OnAmmoPickupCollected(int spawnPointIndex)
    {
        if (!base.IsServerInitialized) return;
        StartCoroutine(RespawnAmmoPickupCoroutine(spawnPointIndex));
    }

    private IEnumerator RespawnHealthPickupCoroutine(int spawnPointIndex)
    {
        yield return new WaitForSeconds(_healthRespawnDelay);
        _availableHealthSpawnPoints.Enqueue(spawnPointIndex);
        SpawnHealthPickup();
    }

    private IEnumerator RespawnAmmoPickupCoroutine(int spawnPointIndex)
    {
        yield return new WaitForSeconds(_ammoRespawnDelay);
        _availableAmmoSpawnPoints.Enqueue(spawnPointIndex);
        SpawnAmmoPickup();
    }
}