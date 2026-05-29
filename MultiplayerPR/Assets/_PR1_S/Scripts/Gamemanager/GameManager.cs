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
        private bool _isRestarting = false;
        private bool _fadeStarted = false;
        private bool _fadeInProgress = false;
        
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
            
            // Подписываемся на события отключения от сети
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromNetwork;
            
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                
                SetState(GameState.Lobby);
                UpdateLobbyUI();
                
                // Показываем fade для хоста при подключении
                StartCoroutine(ShowFadeAtStart());
            }
            else
            {
                // Клиент: показываем Game UI сразу (убираем лобби)
                if (_uiManager != null)
                {
                    _uiManager.ShowLobbyUI(false);
                    _uiManager.ShowGameUI(true);
                    _uiManager.UpdateWaitingPlayersText(0, _targetPlayers);
                }
                
                // Показываем fade для клиента при подключении
                StartCoroutine(ShowFadeAtStart());
            }
        }
        
        private IEnumerator ShowFadeAtStart()
        {
            if (_fadeCanvas == null) yield break;
            
            // Показываем fade
            _fadeCanvas.alpha = 1;
            yield return new WaitForSeconds(0.5f);
            
            // Плавно убираем fade
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _fadeCanvas.alpha = 1 - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _fadeCanvas.alpha = 0;
        }
        
        // Прямой метод для установки количества игроков (вызывается из ConnectionUI до NetworkSpawn)
        public void SetTargetPlayers(int count)
        {
            _targetPlayers = Mathf.Max(2, Mathf.Min(10, count));
            Debug.Log($"[GameManager] Target players set directly to: {_targetPlayers}");
        }
        
        // Обработка отключения от сети (хост упал)
        private void OnClientDisconnectedFromNetwork(ulong clientId)
        {
            if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[GameManager] Client disconnected from host! Restarting scene locally...");
                StartCoroutine(RestartSceneWithFade());
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromNetwork;
            }
            
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
        
        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            
            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"[GameManager] Client {clientId} connected. Current players: {currentPlayers}/{_targetPlayers}");
            
            // Показываем Game UI для всех подключённых игроков
            ShowGameUIClientRpc();
            
            // Показываем fade эффект для подключающегося игрока
            ShowFadeForPlayerClientRpc(clientId);
            
            if (_checkpointManager != null)
            {
                _checkpointManager.InitializePlayerProgress(clientId);
            }
            
            UpdateLobbyUI();
            
            if (_currentState == GameState.Lobby && currentPlayers >= _targetPlayers && !_countdownStarted)
            {
                Debug.Log($"[GameManager] Target players reached! Starting countdown...");
                StartCoroutine(StartCountdownWithFade());
            }
        }
        
        [ClientRpc]
        private void ShowGameUIClientRpc()
        {
            if (_uiManager != null)
            {
                _uiManager.ShowLobbyUI(false);
                _uiManager.ShowGameUI(true);
                Debug.Log($"[GameManager] Game UI shown on client");
            }
        }
        
        [ClientRpc]
        private void ShowFadeForPlayerClientRpc(ulong clientId)
        {
            // Показываем fade только для подключающегося игрока
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                StartCoroutine(ShowFadeAtStart());
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
                _uiManager.UpdateWaitingPlayersText(current, target);
                Debug.Log($"[GameManager] Updated waiting text on client: {current}/{target}");
            }
        }
        
        private IEnumerator StartCountdownWithFade()
        {
            // Показываем fade экран перед началом игры
            yield return StartCoroutine(FadeIn());
            
            yield return new WaitForSeconds(0.5f);
            
            StartCountdown();
            
            // Скрываем fade после начала отсчёта
            yield return StartCoroutine(FadeOut());
        }
        
        private IEnumerator FadeIn()
        {
            if (_fadeCanvas == null) yield break;
            if (_fadeInProgress) yield break;
            
            _fadeInProgress = true;
            
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _fadeCanvas.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _fadeCanvas.alpha = 1;
            
            _fadeInProgress = false;
        }
        
        private IEnumerator FadeOut()
        {
            if (_fadeCanvas == null) yield break;
            if (_fadeInProgress) yield break;
            
            _fadeInProgress = true;
            
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _fadeCanvas.alpha = 1 - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _fadeCanvas.alpha = 0;
            
            _fadeInProgress = false;
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
                _uiManager.UpdateWaitingPlayersText(_targetPlayers, _targetPlayers);
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
                if (_resultsTimeRemaining <= 0 && !_isRestarting)
                {
                    StartCoroutine(RestartSceneWithDelay());
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
        private void ShowResultsClientRpc(ResultsData resultsData)
        {
            Debug.Log($"[GameManager] ShowResultsClientRpc called");
            if (_uiManager != null)
            {
                _uiManager.ShowResults(resultsData.PlayerNames.ToArray(), resultsData.PlayerScores.ToArray(), resultsData.WinnerId);
            }
        }
        
        // Раздельный рестарт: сначала клиенты, потом через 3 секунды хост
        private IEnumerator RestartSceneWithDelay()
        {
            _isRestarting = true;
            
            // Определяем кто мы
            bool isHost = NetworkManager.Singleton.IsHost;
            
            if (!isHost)
            {
                // Клиент: рестартим через 1 секунду
                yield return new WaitForSeconds(1f);
                StartCoroutine(RestartSceneWithFade());
            }
            else
            {
                // Хост: ждём 3 секунды и рестартим
                yield return new WaitForSeconds(3f);
                StartCoroutine(RestartSceneWithFade());
            }
        }
        
        private IEnumerator RestartSceneWithFade()
        {
            if (_fadeStarted) yield break;
            _fadeStarted = true;
            
            // Показываем fade экран
            yield return StartCoroutine(FadeIn());
            
            yield return new WaitForSeconds(0.5f);
            
            Time.timeScale = 1f;
            
            // Включаем курсор перед перезагрузкой
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        
        public void TogglePauseMenu()
        {
            if (_uiManager != null)
                _uiManager.TogglePauseMenu();
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
            Time.timeScale = 1f;
            StartCoroutine(LeaveSessionWithFade());
        }
        
        private IEnumerator LeaveSessionWithFade()
        {
            yield return StartCoroutine(FadeIn());
            
            yield return new WaitForSeconds(0.5f);
            
            Time.timeScale = 1f;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
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
            {
                _targetPlayers = Mathf.Max(2, Mathf.Min(10, count));
                Debug.Log($"[GameManager] Target players set via RPC to: {_targetPlayers}");
            }
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