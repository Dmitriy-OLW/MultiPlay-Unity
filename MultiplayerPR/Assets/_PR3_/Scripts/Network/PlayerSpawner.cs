using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Multi.FishNet
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;
        
        private NetworkManager _networkManager;
        
        public static PlayerSpawner Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }
        
        private void Start()
        {
            _networkManager = FindObjectOfType<NetworkManager>();
            
            if (_networkManager != null && IsServer)
            {
                _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            }
        }
        
        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
        }
        
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                SpawnPlayer(conn);
            }
        }
        
        private void SpawnPlayer(NetworkConnection conn)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("Player prefab is not assigned in PlayerSpawner!");
                return;
            }
            
            Transform spawnPoint = GetSpawnPoint();
            
            GameObject playerObj = Instantiate(_playerPrefab, spawnPoint.position, spawnPoint.rotation);
            
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null && _networkManager != null)
            {
                _networkManager.ServerManager.Spawn(netObj, conn);
                Debug.Log($"[Server] Spawned player for connection {conn.ClientId} at {spawnPoint.position}");
            }
            else
            {
                Debug.LogError("Cannot spawn player - NetworkManager or NetworkObject is null!");
            }
        }
        
        public Transform GetSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogWarning("No spawn points assigned, using default position");
                return transform;
            }
            
            return _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        }
    }
}