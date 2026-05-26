using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace Multi.PR1
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private float _spawnCheckRadius = 2f;
        
        public static PlayerSpawner Instance { get; private set; }
        
        // Отслеживание занятых точек спавна
        private Dictionary<int, ulong> _occupiedSpawnPoints = new Dictionary<int, ulong>();
        
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }
        
        /// <summary>
        /// Получить свободную точку спавна
        /// </summary>
        public Transform GetFreeSpawnPoint(ulong playerId)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return null;
            
            // Сначала пробуем найти свободную точку
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (!_occupiedSpawnPoints.ContainsValue(playerId) && !IsSpawnPointOccupied(i))
                {
                    _occupiedSpawnPoints[i] = playerId;
                    Debug.Log($"[Spawner] Assigned spawn point {i} to player {playerId}");
                    return _spawnPoints[i];
                }
            }
            
            // Если все точки заняты, ищем ту, где игрок уже был (его предыдущая точка)
            foreach (var kvp in _occupiedSpawnPoints)
            {
                if (kvp.Value == playerId)
                {
                    Debug.Log($"[Spawner] Reusing spawn point {kvp.Key} for player {playerId}");
                    return _spawnPoints[kvp.Key];
                }
            }
            
            // Если ничего не подошло - берем первую свободную физически
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (!IsPositionOccupied(_spawnPoints[i].position))
                {
                    _occupiedSpawnPoints[i] = playerId;
                    Debug.Log($"[Spawner] Assigned physically free spawn point {i} to player {playerId}");
                    return _spawnPoints[i];
                }
            }
            
            // Самый крайний случай - первая точка
            Debug.LogWarning($"[Spawner] All spawn points occupied, using first point for player {playerId}");
            _occupiedSpawnPoints[0] = playerId;
            return _spawnPoints[0];
        }
        
        /// <summary>
        /// Освободить точку спавна при смерти игрока
        /// </summary>
        public void ReleaseSpawnPoint(ulong playerId)
        {
            int? pointToRelease = null;
            foreach (var kvp in _occupiedSpawnPoints)
            {
                if (kvp.Value == playerId)
                {
                    pointToRelease = kvp.Key;
                    break;
                }
            }
            
            if (pointToRelease.HasValue)
            {
                _occupiedSpawnPoints.Remove(pointToRelease.Value);
                Debug.Log($"[Spawner] Released spawn point {pointToRelease.Value} from player {playerId}");
            }
        }
        
        /// <summary>
        /// Проверить, занята ли точка спавна по индексу
        /// </summary>
        private bool IsSpawnPointOccupied(int index)
        {
            return _occupiedSpawnPoints.ContainsKey(index);
        }
        
        /// <summary>
        /// Проверить, есть ли другой игрок на позиции
        /// </summary>
        private bool IsPositionOccupied(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, _spawnCheckRadius);
            foreach (var collider in colliders)
            {
                if (collider.GetComponent<PlayerNetwork>() != null)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Старый метод для обратной совместимости
        /// </summary>
        public Transform GetSpawnPoint()
        {
            return GetFreeSpawnPoint(0);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            
            Gizmos.color = Color.green;
            foreach (var point in _spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, _spawnCheckRadius);
                    Gizmos.DrawIcon(point.position, "SpawnPoint", true);
                }
            }
        }
    }
}