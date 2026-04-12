using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace Multi.PR1
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private Transform[] _spawnPoints;
        
        public static PlayerSpawner Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }
        
        public Transform GetSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return null;
            return _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        }
    }
}