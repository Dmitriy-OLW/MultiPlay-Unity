using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Multi.PR1
{
    public class CheckpointManager : NetworkBehaviour
    {
        [Header("Checkpoints")]
        [SerializeField] private Checkpoint[] _checkpoints;
        
        [Header("Settings")]
        [SerializeField] private int _totalLaps = 1;        // 0 = бесконечно, 1 = один круг
        [SerializeField] private int _checkpointPoints = 1;  // Очков за чекпоинт
        [SerializeField] private int _lapBonusPoints = 10;   // Бонус за завершение КРУГА (только при прохождении последнего чекпоинта)
        
        [Header("Visual Settings")]
        [SerializeField] private float _stateSyncRate = 0.2f; // Частота синхронизации состояний
        
        // Данные прогресса для каждого игрока
        private Dictionary<ulong, PlayerProgress> _playerProgress = new Dictionary<ulong, PlayerProgress>();
        
        // Для синхронизации состояний чекпоинтов с клиентами
        private float _lastStateSyncTime;
        
        // События
        public System.Action<PlayerNetwork> OnPlayerFinished;
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            // Инициализируем чекпоинты
            for (int i = 0; i < _checkpoints.Length; i++)
            {
                if (_checkpoints[i] != null)
                {
                    _checkpoints[i].Initialize(this, i);
                }
            }
            
            // Находим всех существующих игроков и инициализируем их прогресс
            PlayerNetwork[] allPlayers = FindObjectsOfType<PlayerNetwork>();
            foreach (var player in allPlayers)
            {
                if (player != null && player.IsSpawned)
                {
                    InitializePlayerProgress(player.OwnerClientId);
                }
            }
            
            Debug.Log($"[CheckpointManager] Spawned on server with {_checkpoints.Length} checkpoints, Total laps: {(_totalLaps == 0 ? "INFINITE" : _totalLaps.ToString())}");
        }
        
        private void Update()
        {
            if (!IsServer) return;
            
            // Периодически синхронизируем состояния чекпоинтов с клиентами
            if (Time.time - _lastStateSyncTime > _stateSyncRate)
            {
                SyncAllStatesToClients();
                _lastStateSyncTime = Time.time;
            }
        }
        
        /// <summary>
        /// Инициализировать прогресс игрока
        /// </summary>
        public void InitializePlayerProgress(ulong playerId)
        {
            if (!IsServer) return;
            
            if (!_playerProgress.ContainsKey(playerId))
            {
                PlayerProgress progress = new PlayerProgress();
                progress.expectedCheckpointIndex = 0;
                progress.currentLap = 0;
                progress.completedCheckpoints = new HashSet<int>();
                _playerProgress[playerId] = progress;
                
                // Активируем первый чекпоинт для игрока
                SetCheckpointStateForPlayerClientRpc(playerId, 0, CheckpointState.Active);
                
                Debug.Log($"[CheckpointManager] Initialized progress for player {playerId}. First checkpoint active.");
            }
        }
        
        /// <summary>
        /// Вызывается когда игрок проезжает чекпоинт
        /// </summary>
        public void OnCheckpointPassed(PlayerNetwork player, int checkpointIndex)
        {
            if (!IsServer) return;
            if (player == null) return;
            
            ulong playerId = player.OwnerClientId;
            
            // Получаем или создаём прогресс игрока
            if (!_playerProgress.TryGetValue(playerId, out PlayerProgress progress))
            {
                // Если прогресса нет - инициализируем
                InitializePlayerProgress(playerId);
                progress = _playerProgress[playerId];
            }
            
            // Проверяем, ожидаемый ли это чекпоинт
            if (checkpointIndex != progress.expectedCheckpointIndex)
            {
                Debug.Log($"[CheckpointManager] Player {playerId} passed wrong checkpoint. Expected: {progress.expectedCheckpointIndex}, Got: {checkpointIndex}");
                return;
            }
            
            // Проверяем, не пройден ли уже этот чекпоинт в текущем круге
            if (progress.completedCheckpoints.Contains(checkpointIndex))
            {
                Debug.Log($"[CheckpointManager] Player {playerId} already passed checkpoint {checkpointIndex} in this lap");
                return;
            }
            
            // Начисляем очки за чекпоинт
            player.Score.Value += _checkpointPoints;
            Debug.Log($"[CheckpointManager] Player {playerId} passed checkpoint {checkpointIndex}. +{_checkpointPoints} points. Total: {player.Score.Value}");
            
            // Отмечаем чекпоинт как пройденный
            progress.completedCheckpoints.Add(checkpointIndex);
            
            // Отправляем клиенту команду на визуальное обновление чекпоинта (завершён)
            SetCheckpointStateForPlayerClientRpc(playerId, checkpointIndex, CheckpointState.Completed);
            
            // Проверяем, последний ли это чекпоинт
            bool isLastCheckpoint = (checkpointIndex == _checkpoints.Length - 1);
            
            if (isLastCheckpoint)
            {
                // Завершение круга - НАЧИСЛЯЕМ БОНУС ЗА КРУГ
                progress.currentLap++;
                progress.completedCheckpoints.Clear();
                
                // Начисляем бонус за завершение круга (ТОЛЬКО ЗА ПОСЛЕДНИЙ ЧЕКПОИНТ)
                player.Score.Value += _lapBonusPoints;
                Debug.Log($"[CheckpointManager] Player {playerId} COMPLETED LAP {progress.currentLap}! +{_lapBonusPoints} bonus points! Total: {player.Score.Value}");
                
                // Проверяем, завершена ли гонка (если totalLaps > 0 и достигли лимита)
                if (_totalLaps > 0 && progress.currentLap >= _totalLaps)
                {
                    // Игрок завершил все круги
                    Debug.Log($"[CheckpointManager] Player {playerId} FINISHED the race after {progress.currentLap} laps!");
                    OnPlayerFinished?.Invoke(player);
                    return;
                }
                
                // Сбрасываем индекс на первый чекпоинт для нового круга
                progress.expectedCheckpointIndex = 0;
                
                // Деактивируем все завершённые чекпоинты и активируем первый
                for (int i = 0; i < _checkpoints.Length; i++)
                {
                    if (i == 0)
                    {
                        SetCheckpointStateForPlayerClientRpc(playerId, i, CheckpointState.Active);
                    }
                    else
                    {
                        SetCheckpointStateForPlayerClientRpc(playerId, i, CheckpointState.Inactive);
                    }
                }
                
                Debug.Log($"[CheckpointManager] Player {playerId} starting lap {progress.currentLap + 1}. First checkpoint active.");
            }
            else
            {
                // Переходим к следующему чекпоинту
                progress.expectedCheckpointIndex = checkpointIndex + 1;
                
                // Активируем следующий чекпоинт
                SetCheckpointStateForPlayerClientRpc(playerId, progress.expectedCheckpointIndex, CheckpointState.Active);
                Debug.Log($"[CheckpointManager] Player {playerId} next checkpoint: {progress.expectedCheckpointIndex}");
            }
        }
        
        /// <summary>
        /// Установить состояние чекпоинта для конкретного игрока
        /// </summary>
        [ClientRpc]
        private void SetCheckpointStateForPlayerClientRpc(ulong playerId, int checkpointIndex, CheckpointState state)
        {
            // Только целевой игрок обновляет визуал
            if (NetworkManager.Singleton.LocalClientId != playerId) return;
            
            if (checkpointIndex >= 0 && checkpointIndex < _checkpoints.Length && _checkpoints[checkpointIndex] != null)
            {
                _checkpoints[checkpointIndex].SetStateForPlayer(playerId, state);
                Debug.Log($"[CheckpointManager] Client {playerId}: Checkpoint {checkpointIndex} state = {state}");
            }
        }
        
        /// <summary>
        /// Синхронизировать все состояния для всех игроков
        /// </summary>
        private void SyncAllStatesToClients()
        {
            foreach (var kvp in _playerProgress)
            {
                ulong playerId = kvp.Key;
                PlayerProgress progress = kvp.Value;
                
                // Синхронизируем активный чекпоинт
                if (progress.expectedCheckpointIndex >= 0 && progress.expectedCheckpointIndex < _checkpoints.Length)
                {
                    SetCheckpointStateForPlayerClientRpc(playerId, progress.expectedCheckpointIndex, CheckpointState.Active);
                }
                
                // Синхронизируем завершённые чекпоинты
                foreach (int completedIndex in progress.completedCheckpoints)
                {
                    if (completedIndex >= 0 && completedIndex < _checkpoints.Length)
                    {
                        SetCheckpointStateForPlayerClientRpc(playerId, completedIndex, CheckpointState.Completed);
                    }
                }
            }
        }
        
        /// <summary>
        /// Сбросить прогресс игрока (при респавне или перезапуске)
        /// </summary>
        public void ResetPlayerProgress(ulong playerId)
        {
            if (!IsServer) return;
            
            if (_playerProgress.ContainsKey(playerId))
            {
                _playerProgress.Remove(playerId);
            }
            
            // Сбрасываем визуальные состояния всех чекпоинтов для игрока
            for (int i = 0; i < _checkpoints.Length; i++)
            {
                SetCheckpointStateForPlayerClientRpc(playerId, i, CheckpointState.Inactive);
            }
            
            // Заново инициализируем прогресс
            InitializePlayerProgress(playerId);
            
            Debug.Log($"[CheckpointManager] Reset progress for player {playerId}");
        }
        
        /// <summary>
        /// Получить прогресс игрока
        /// </summary>
        public PlayerProgress GetPlayerProgress(ulong playerId)
        {
            if (_playerProgress.TryGetValue(playerId, out PlayerProgress progress))
            {
                return progress;
            }
            return null;
        }
        
        /// <summary>
        /// Прогресс игрока
        /// </summary>
        public class PlayerProgress
        {
            public int expectedCheckpointIndex;           // Ожидаемый индекс чекпоинта
            public int currentLap;                        // Текущий круг
            public HashSet<int> completedCheckpoints;     // Пройденные чекпоинты в текущем круге
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_checkpoints == null) return;
            
            for (int i = 0; i < _checkpoints.Length; i++)
            {
                if (_checkpoints[i] != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(_checkpoints[i].transform.position, Vector3.one * 2f);
                    
                    // Рисуем линию между последовательными чекпоинтами
                    if (i > 0 && _checkpoints[i - 1] != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(_checkpoints[i - 1].transform.position, _checkpoints[i].transform.position);
                    }
                }
            }
        }
    }
}