using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Multi.PR1
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("Game Settings")]
        [SerializeField] private int _targetPlayers = 2;
        [SerializeField] private float _gameDuration = 180f; // 3 минуты
        [SerializeField] private float _resultsDuration = 10f;
        [SerializeField] private float _countdownDuration = 3f;
        
        [Header("UI Manager")]
        [SerializeField] private GameUIManager _uiManager;
        
        [Header("Fade Effect")]
        [SerializeField] private CanvasGroup _fadeCanvas;
        [SerializeField] private float _fadeDuration = 0.5f;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _gameAudioSource;

        [Header("References")]
        [SerializeField] private CheckpointManager _checkpointManager;
        
        private GameState _currentState = GameState.Lobby;
        private float _gameTimeRemaining;
        private float _resultsTimeRemaining;
        private bool _isGameFinished = false;
        private Dictionary<ulong, PlayerResultData> _playerResults = new Dictionary<ulong, PlayerResultData>();
        
        // Свойства для доступа из других скриптов
        public GameState CurrentState => _currentState;
        public bool IsInputBlocked => _currentState == GameState.Starting || _currentState == GameState.Results || (_currentState == GameState.Lobby && !IsServer);
        public float GameTimeRemaining => _gameTimeRemaining;
        
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
                
            if (_fadeCanvas != null)
                _fadeCanvas.alpha = 0;
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                
                // Начинаем в лобби
                SetState(GameState.Lobby);
                UpdateLobbyUI();
            }
            
            // Включаем аудио позже, при старте гонки
            if (_gameAudioSource != null)
                _gameAudioSource.enabled = false;
                
            // Ищем UIManager если не назначен
            if (_uiManager == null)
                _uiManager = FindObjectOfType<GameUIManager>();
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
        
        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            
            // Инициализируем прогресс игрока в чекпоинтах
            if (_checkpointManager != null)
            {
                _checkpointManager.InitializePlayerProgress(clientId);
            }
            
            UpdateLobbyUI();
            
            // Проверяем, достигнуто ли нужное количество игроков
            if (_currentState == GameState.Lobby && NetworkManager.Singleton.ConnectedClients.Count >= _targetPlayers)
            {
                StartCountdown();
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            // Очищаем прогресс игрока
            if (_checkpointManager != null)
            {
                _checkpointManager.ResetPlayerProgress(clientId);
            }
            
            // Если игрок отключился во время игры - завершаем матч
            if (_currentState == GameState.Playing || _currentState == GameState.Starting)
            {
                StartCoroutine(EndGameWithDelay());
            }
            else if (_currentState == GameState.Lobby)
            {
                UpdateLobbyUI();
            }
        }
        
        private void UpdateLobbyUI()
        {
            if (!IsServer) return;
            
            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            
            // Обновляем UI через UIManager на всех клиентах
            UpdateLobbyUIClientRpc(currentPlayers, _targetPlayers);
        }
        
        [ClientRpc]
        private void UpdateLobbyUIClientRpc(int current, int target)
        {
            if (_uiManager != null)
                _uiManager.UpdateWaitingPlayersText(current, target);
        }
        
        private void StartCountdown()
        {
            if (!IsServer) return;
            
            SetState(GameState.Starting);
            StartCoroutine(CountdownCoroutine());
            
            // Активируем аудио для всех
            EnableAudioClientRpc(true);
        }
        
        [ClientRpc]
        private void EnableAudioClientRpc(bool enable)
        {
            if (_gameAudioSource != null)
                _gameAudioSource.enabled = enable;
        }
        
        private IEnumerator CountdownCoroutine()
        {
            float countdown = _countdownDuration;
            
            while (countdown > 0)
            {
                UpdateCountdownClientRpc(countdown);

                yield return new WaitForSeconds(1f);
                countdown--;
            }
            
            // GO!
            UpdateCountdownClientRpc(-1f);

            yield return new WaitForSeconds(0.5f);
            
            // Скрываем текст
            UpdateCountdownClientRpc(0f);
            
            // Начинаем игру
            StartGame();
        }
        
        [ClientRpc]
        private void UpdateCountdownClientRpc(float remainingTime)
        {
            if (_uiManager != null)
                _uiManager.UpdateCountdown(remainingTime);
        }
        
        private void StartGame()
        {
            if (!IsServer) return;
            
            SetState(GameState.Playing);
            _gameTimeRemaining = _gameDuration;
            _isGameFinished = false;
            
            StartGameClientRpc();
        }
        
        [ClientRpc]
        private void StartGameClientRpc()
        {
            if (_uiManager != null)
            {
                _uiManager.ShowGameUI(true);
                _uiManager.ShowLobbyUI(false);
            }
        }
        
        private void Update()
        {
            // Обновление таймера на сервере
            if (_currentState == GameState.Playing && IsServer)
            {
                _gameTimeRemaining -= Time.deltaTime;
                UpdateGameTimerClientRpc(_gameTimeRemaining);
                
                if (_gameTimeRemaining <= 0 && !_isGameFinished)
                {
                    _isGameFinished = true;
                    StartCoroutine(EndGameWithDelay());
                }
            }
            else if (_currentState == GameState.Results && IsServer)
            {
                _resultsTimeRemaining -= Time.deltaTime;
                if (_resultsTimeRemaining <= 0)
                {
                    StartCoroutine(RestartSceneWithFade());
                }
            }
        }
        
        [ClientRpc]
        private void UpdateGameTimerClientRpc(float timeRemaining)
        {
            if (_uiManager != null)
                _uiManager.UpdateGameTimer(timeRemaining);
        }
        
        public void FinishRace(ulong winnerId)
        {
            if (!IsServer) return;
            if (_isGameFinished) return;
            if (_currentState != GameState.Playing) return;
            
            _isGameFinished = true;
            
            Debug.Log($"[GameManager] Player {winnerId} finished the race!");
            
            // Собираем результаты
            CollectResults();
            
            StartCoroutine(EndGameWithDelay());
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void RequestFinishRaceServerRpc(ulong playerId)
        {
            if (!_isGameFinished && _currentState == GameState.Playing)
            {
                FinishRace(playerId);
            }
        }
        
        private void CollectResults()
        {
            _playerResults.Clear();
            
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                PlayerNetwork player = client.Value.PlayerObject?.GetComponent<PlayerNetwork>();
                if (player != null)
                {
                    PlayerResultData data = new PlayerResultData
                    {
                        PlayerId = client.Key,
                        Nickname = player.Nickname.Value.ToString(),
                        Score = player.Score.Value,
                        IsWinner = false
                    };
                    _playerResults[client.Key] = data;
                }
            }
            
            // Находим победителя (по очкам)
            ulong winnerId = 0;
            int highestScore = -1;
            foreach (var result in _playerResults)
            {
                if (result.Value.Score > highestScore)
                {
                    highestScore = result.Value.Score;
                    winnerId = result.Key;
                }
            }
            
            if (_playerResults.ContainsKey(winnerId))
                _playerResults[winnerId].IsWinner = true;
        }
        
        private IEnumerator EndGameWithDelay()
        {
            yield return new WaitForSeconds(1f);
            EndGame();
        }
        
        private void EndGame()
        {
            if (!IsServer) return;
            
            SetState(GameState.Results);
            _resultsTimeRemaining = _resultsDuration;
            
            // Создаём сериализуемый объект с результатами
            var resultsData = new ResultsData();
            resultsData.WinnerId = 0;
            
            foreach (var result in _playerResults.Values)
            {
                resultsData.PlayerNames.Add(result.Nickname);
                resultsData.PlayerScores.Add(result.Score);
                if (result.IsWinner)
                    resultsData.WinnerId = result.PlayerId;
            }
            
            // Показываем результаты через UIManager
            ShowResultsClientRpc(resultsData);
        }
        
        [ClientRpc]
        private void ShowResultsClientRpc(ResultsData resultsData)
        {
            if (_uiManager != null)
                _uiManager.ShowResults(resultsData.PlayerNames.ToArray(), resultsData.PlayerScores.ToArray(), resultsData.WinnerId);
        }
        
        private IEnumerator RestartSceneWithFade()
        {
            // Затемнение
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_fadeCanvas != null)
                    _fadeCanvas.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            
            if (_fadeCanvas != null)
                _fadeCanvas.alpha = 1;
                
            yield return new WaitForSeconds(0.1f);
            
            // Возвращаем время в нормальное состояние
            Time.timeScale = 1f;
            
            // Отключаем NetworkManager
            NetworkManager.Singleton.Shutdown();
            yield return null;
            
            // Перезапускаем сцену
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        
        public void LeaveSession()
        {
            StartCoroutine(LeaveSessionWithFade());
        }
        
        private IEnumerator LeaveSessionWithFade()
        {
            Time.timeScale = 1f;
            
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_fadeCanvas != null)
                    _fadeCanvas.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            
            if (_fadeCanvas != null)
                _fadeCanvas.alpha = 1;
                
            yield return new WaitForSeconds(0.1f);
            
            NetworkManager.Singleton.Shutdown();
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        
        public void QuitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
        private void SetState(GameState newState)
        {
            _currentState = newState;
            OnStateChangedClientRpc(newState);
        }
        
        [ClientRpc]
        private void OnStateChangedClientRpc(GameState newState)
        {
            _currentState = newState;
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void SetTargetPlayersServerRpc(int count)
        {
            if (IsServer)
                _targetPlayers = Mathf.Max(2, Mathf.Min(10, count));
        }
        
        public int GetTargetPlayers()
        {
            return _targetPlayers;
        }
        
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
        
        [System.Serializable]
        public class PlayerResultData
        {
            public ulong PlayerId;
            public string Nickname;
            public int Score;
            public bool IsWinner;
        }
        
        // Сериализуемый класс для передачи результатов через RPC
        [System.Serializable]
        public class ResultsData : INetworkSerializable
        {
            public List<string> PlayerNames = new List<string>();
            public List<int> PlayerScores = new List<int>();
            public ulong WinnerId;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                // Сериализуем количество игроков
                int count = PlayerNames.Count;
                serializer.SerializeValue(ref count);
                
                if (serializer.IsReader)
                {
                    PlayerNames.Clear();
                    PlayerScores.Clear();
                    
                    for (int i = 0; i < count; i++)
                    {
                        string name = "";
                        int score = 0;
                        
                        serializer.SerializeValue(ref name);
                        serializer.SerializeValue(ref score);
                        
                        PlayerNames.Add(name);
                        PlayerScores.Add(score);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        string name = PlayerNames[i];
                        int score = PlayerScores[i];
                        
                        serializer.SerializeValue(ref name);
                        serializer.SerializeValue(ref score);
                    }
                }
                
                serializer.SerializeValue(ref WinnerId);
            }
        }
    }
}