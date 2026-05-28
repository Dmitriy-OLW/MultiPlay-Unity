using UnityEngine;
using Unity.Netcode;

namespace Multi.PR1
{
    public class Checkpoint : NetworkBehaviour
    {
        [Header("Visual Objects")]
        [SerializeField] private GameObject _inactiveObject;    // Неактивное состояние (ещё не доступен)
        [SerializeField] private GameObject _activeObject;      // Активное состояние (нужно проехать)
        [SerializeField] private GameObject _completedObject;   // Завершённое состояние (уже пройден)
        
        [Header("Settings")]
        [SerializeField] private int _checkpointIndex;
        
        private CheckpointManager _manager;
        
        public void Initialize(CheckpointManager manager, int index)
        {
            _manager = manager;
            _checkpointIndex = index;
        }
        
        /// <summary>
        /// Установить состояние чекпоинта для конкретного игрока
        /// </summary>
        public void SetStateForPlayer(ulong playerId, CheckpointState state)
        {
            // Обновляем визуальное состояние для клиента
            UpdateVisualState(state);
        }
        
        private void UpdateVisualState(CheckpointState state)
        {
            if (_inactiveObject != null)
                _inactiveObject.SetActive(state == CheckpointState.Inactive);
            
            if (_activeObject != null)
                _activeObject.SetActive(state == CheckpointState.Active);
            
            if (_completedObject != null)
                _completedObject.SetActive(state == CheckpointState.Completed);
                
            Debug.Log($"[Checkpoint] Checkpoint {_checkpointIndex} state = {state}");
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            
            // Проверяем, что это игрок (машина)
            PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null) return;
            
            if (_manager == null) return;
            
            // Сообщаем менеджеру о проезде чекпоинта
            _manager.OnCheckpointPassed(player, _checkpointIndex);
        }
        
    }
    
    public enum CheckpointState
    {
        Inactive,   // Неактивный - ещё недоступен
        Active,     // Активный - нужно проехать
        Completed   // Завершённый - уже пройден
    }
}