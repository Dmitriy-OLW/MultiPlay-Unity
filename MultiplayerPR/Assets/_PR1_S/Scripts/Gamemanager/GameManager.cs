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
        [SerializeField] private float _gameDuration = 180f;
        [SerializeField] private float _resultsDuration = 10f;
        [SerializeField] private float _countdownDuration = 3f;
        
        [Header("UI Manager")]
        [SerializeField] private GameUIManager _uiManager;
        
        [Header("Fade Effect")]
        [SerializeField] private CanvasGroup _fadeCanvas;
        [SerializeField] private float _fadeDuration = 0.5f;
        
        [Header("References")]
        [SerializeField] private CheckpointManager _checkpointManager;
        
        private GameState _currentState = GameState.Lobby;
        private float _gameTimeRemaining;
        private float _resultsTimeRemaining;
        private bool _isGameFinished = false;
        private bool _countdownStarted = false;
        private Dictionary<ulong, PlayerResultData> _playerResults = new Dictionary<ulong, PlayerResultData>();
        
        public GameState CurrentState => _currentState;
        public bool IsInputBlocked => _currentState == GameState.Starting || _currentState == GameState.Results;
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
            Debug.Log($"[GameManager] OnNetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}");
            
            if (_uiManager == null)
                _uiManager = FindObjectOfType<GameUIManager>();
            
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                
                SetState(GameState.Lobby);
                UpdateLobbyUI();
            }
            else
            {
                // Клиент: показываем Game UI с текстом ожидания
                if (_uiManager != null)
                {
                    _uiManager.ShowLobbyUI(false);
                    _uiManager.ShowGameUI(true);
                    _uiManager.UpdateWaitingPlayersText(0, _targetPlayers);
                }
            }
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
            
            Debug.Log($"[GameManager] Client {clientId} connected. Current players: {NetworkManager.Singleton.ConnectedClients.Count}/{_targetPlayers}");
            
            if (_checkpointManager != null)
            {
                _checkpointManager.InitializePlayerProgress(clientId);
            }
            
            UpdateLobbyUI();
            
            if (_currentState == GameState.Lobby && NetworkManager.Singleton.ConnectedClients.Count >= _targetPlayers && !_countdownStarted)
            {
                Debug.Log($"[GameManager] Target players reached! Starting countdown...");
                StartCountdown();
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            Debug.Log($"[GameManager] Client {clientId} disconnected");
            
            if (_checkpointManager != null)
            {
                _checkpointManager.ResetPlayerProgress(clientId);
            }
            
            if (_currentState == GameState.Playing || _currentState == GameState.Starting)
            {
                StartCoroutine(EndGameWithDelay());
            }
            else if (_currentState == GameState.Lobby)
            {
                _countdownStarted = false;
                UpdateLobbyUI();
            }
        }
        
        private void UpdateLobbyUI()
        {
            if (!IsServer) return;
            
            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"[GameManager] UpdateLobbyUI - Players: {currentPlayers}/{_targetPlayers}");
            
            UpdateWaitingTextClientRpc(currentPlayers, _targetPlayers);
        }
        
        [ClientRpc]
        private void UpdateWaitingTextClientRpc(int current, int target)
        {
            if (_uiManager != null)
            {
                // Скрываем лобби и показываем Game UI при первом подключении
                if (current == 1 && !IsServer)
                {
                    _uiManager.ShowLobbyUI(false);
                    _uiManager.ShowGameUI(true);
                }
                _uiManager.UpdateWaitingPlayersText(current, target);
            }
        }
        
        private void StartCountdown()
        {
            if (!IsServer) return;
            if (_countdownStarted) return;
            
            _countdownStarted = true;
            SetState(GameState.Starting);
            
            Debug.Log($"[GameManager] Starting countdown!");
            
            StartCoroutine(CountdownCoroutine());
        }
        
        private IEnumerator CountdownCoroutine()
        {
            float countdown = _countdownDuration;
            
            while (countdown > 0)
            {
                Debug.Log($"[GameManager] Countdown: {countdown}");
                UpdateCountdownClientRpc(countdown);
                yield return new WaitForSeconds(1f);
                countdown--;
            }
            
            UpdateCountdownClientRpc(-1f);
            yield return new WaitForSeconds(0.5f);
            
            UpdateCountdownClientRpc(0f);
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
            
            Debug.Log($"[GameManager] Starting game!");
            
            SetState(GameState.Playing);
            _gameTimeRemaining = _gameDuration;
            _isGameFinished = false;
            
            // Скрываем текст ожидания
            HideWaitingTextClientRpc();
            
            UpdateGameStateClientRpc("RACING", Color.green);
        }
        
        [ClientRpc]
        private void HideWaitingTextClientRpc()
        {
            if (_uiManager != null)
                _uiManager.UpdateWaitingPlayersText(_targetPlayers, _targetPlayers); // Скрывает текст
        }
        
        [ClientRpc]
        private void UpdateGameStateClientRpc(string stateText, Color stateColor)
        {
            if (_uiManager != null)
                _uiManager.UpdateGameStateText(stateText, stateColor);
        }
        
        private void Update()
        {
            if (_currentState == GameState.Playing && IsServer)
            {
                _gameTimeRemaining -= Time.deltaTime;
                UpdateGameTimerClientRpc(_gameTimeRemaining);
                
                if (_gameTimeRemaining <= 0 && !_isGameFinished)
                {
                    _isGameFinished = true;
                    Debug.Log($"[GameManager] Time's up!");
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
            
            Debug.Log($"[GameManager] Ending game!");
            
            SetState(GameState.Results);
            _resultsTimeRemaining = _resultsDuration;
            
            // Скрываем Game UI
            HideGameUIClientRpc();
            
            var resultsData = new ResultsData();
            resultsData.WinnerId = 0;
            
            foreach (var result in _playerResults.Values)
            {
                resultsData.PlayerNames.Add(result.Nickname);
                resultsData.PlayerScores.Add(result.Score);
                if (result.IsWinner)
                    resultsData.WinnerId = result.PlayerId;
            }
            
            ShowResultsClientRpc(resultsData);
        }
        
        [ClientRpc]
        private void HideGameUIClientRpc()
        {
            if (_uiManager != null)
                _uiManager.ShowGameUI(false);
        }
        
        [ClientRpc]
        private void ShowResultsClientRpc(ResultsData resultsData)
        {
            if (_uiManager != null)
                _uiManager.ShowResults(resultsData.PlayerNames.ToArray(), resultsData.PlayerScores.ToArray(), resultsData.WinnerId);
        }
        
        private IEnumerator RestartSceneWithFade()
        {
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
            
            Time.timeScale = 1f;
            
            // Включаем курсор перед перезагрузкой
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            NetworkManager.Singleton.Shutdown();
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ResumeGame()
        {
            if (_uiManager != null)
                _uiManager.SetPauseMenuActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
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
        
        [System.Serializable]
        public class ResultsData : INetworkSerializable
        {
            public List<string> PlayerNames = new List<string>();
            public List<int> PlayerScores = new List<int>();
            public ulong WinnerId;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
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