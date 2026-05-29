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
        [SerializeField] private float _fadeDuration = 1f;
        
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
        private bool _resultsShown = false;
        
        private Dictionary<ulong, CachedPlayerData> _cachedPlayerData = new Dictionary<ulong, CachedPlayerData>();
        
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
                
            Debug.Log($"[GameManager] Awake - Instance set");
        }
        
        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GameManager] OnNetworkSpawn START - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");
            
            if (_uiManager == null)
            {
                _uiManager = FindObjectOfType<GameUIManager>();
                Debug.Log($"[GameManager] UIManager found: {_uiManager != null}");
            }
            
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromNetwork;
            
            if (IsServer)
            {
                Debug.Log($"[GameManager] Running as SERVER");
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                
                SetState(GameState.Lobby);
                UpdateLobbyUI();
                
                StartCoroutine(ShowFadeAtStart());
            }
            else
            {
                Debug.Log($"[GameManager] Running as CLIENT");
                if (_uiManager != null)
                {
                    _uiManager.ShowLobbyUI(false);
                    _uiManager.ShowGameUI(true);
                    _uiManager.UpdateWaitingPlayersText(0, _targetPlayers);
                    Debug.Log($"[GameManager] Client UI initialized");
                }
                
                StartCoroutine(ShowFadeAtStart());
            }
            
            Debug.Log($"[GameManager] OnNetworkSpawn END");
        }
        
        private IEnumerator ShowFadeAtStart()
        {
            Debug.Log($"[GameManager] ShowFadeAtStart START");
            if (_fadeCanvas == null) yield break;
            
            _fadeCanvas.alpha = 1;
            yield return new WaitForSeconds(0.5f);
            
            float elapsed = 0;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _fadeCanvas.alpha = 1 - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _fadeCanvas.alpha = 0;
            Debug.Log($"[GameManager] ShowFadeAtStart END");
        }
        
        public void SetTargetPlayers(int count)
        {
            _targetPlayers = Mathf.Max(2, Mathf.Min(10, count));
            Debug.Log($"[GameManager] SetTargetPlayers - target players set to: {_targetPlayers}");
        }
        
        private void OnClientDisconnectedFromNetwork(ulong clientId)
        {
            Debug.Log($"[GameManager] OnClientDisconnectedFromNetwork - clientId: {clientId}, IsServer: {IsServer}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[GameManager] Client disconnected from host! Restarting scene locally...");
                StartCoroutine(RestartSceneWithFade());
            }
        }
        
        public override void OnNetworkDespawn()
        {
            Debug.Log($"[GameManager] OnNetworkDespawn");
            
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
            Debug.Log($"[GameManager] OnClientConnected - clientId: {clientId}, Current players: {currentPlayers}/{_targetPlayers}");
            
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"[GameManager] Connected client: {client.Key}, PlayerObject: {client.Value.PlayerObject != null}");
            }
            
            ShowGameUIClientRpc();
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
            Debug.Log($"[GameManager] ShowGameUIClientRpc called on client {NetworkManager.Singleton.LocalClientId}");
            
            if (_uiManager != null)
            {
                _uiManager.ShowLobbyUI(false);
                _uiManager.ShowGameUI(true);
                Debug.Log($"[GameManager] Game UI shown on client {NetworkManager.Singleton.LocalClientId}");
            }
            else
            {
                Debug.LogError($"[GameManager] UIManager is null in ShowGameUIClientRpc!");
            }
        }
        
        [ClientRpc]
        private void ShowFadeForPlayerClientRpc(ulong clientId)
        {
            Debug.Log($"[GameManager] ShowFadeForPlayerClientRpc - targetClient: {clientId}, localClient: {NetworkManager.Singleton.LocalClientId}");
            
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                StartCoroutine(ShowFadeAtStart());
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            Debug.Log($"[GameManager] OnClientDisconnected - clientId: {clientId}");
            
            if (_checkpointManager != null)
            {
                _checkpointManager.ResetPlayerProgress(clientId);
            }
            
            if (_currentState == GameState.Playing || _currentState == GameState.Starting)
            {
                Debug.Log($"[GameManager] Client disconnected during game, ending game...");
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
            Debug.Log($"[GameManager] UpdateWaitingTextClientRpc on client {NetworkManager.Singleton.LocalClientId}: {current}/{target}");
            
            if (_uiManager != null)
            {
                _uiManager.UpdateWaitingPlayersText(current, target);
            }
        }
        
        private IEnumerator StartCountdownWithFade()
        {
            Debug.Log($"[GameManager] StartCountdownWithFade START");
            
            // Запускаем музыку перед началом отсчёта
            if (_uiManager != null)
            {
                _uiManager.StartBackgroundMusic();
            }
            
            yield return StartCoroutine(FadeIn());
            yield return new WaitForSeconds(0.5f);
            StartCountdown();
            yield return StartCoroutine(FadeOut());
            Debug.Log($"[GameManager] StartCountdownWithFade END");
        }
        
        private IEnumerator FadeIn()
        {
            Debug.Log($"[GameManager] FadeIn START");
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
            Debug.Log($"[GameManager] FadeIn END");
        }
        
        private IEnumerator FadeOut()
        {
            Debug.Log($"[GameManager] FadeOut START");
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
            Debug.Log($"[GameManager] FadeOut END");
        }
        
        private void StartCountdown()
        {
            if (!IsServer) return;
            if (_countdownStarted) return;
            
            _countdownStarted = true;
            SetState(GameState.Starting);
            
            Debug.Log($"[GameManager] StartCountdown - Starting countdown!");
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
            Debug.Log($"[GameManager] UpdateCountdownClientRpc on client {NetworkManager.Singleton.LocalClientId}: {remainingTime}");
            if (_uiManager != null)
                _uiManager.UpdateCountdown(remainingTime);
        }
        
        private void StartGame()
        {
            if (!IsServer) return;
            
            Debug.Log($"[GameManager] StartGame - STARTING GAME!");
            
            SetState(GameState.Playing);
            _gameTimeRemaining = _gameDuration;
            _isGameFinished = false;
            _resultsShown = false;
            
            CacheAllPlayersDataAndSubscribe();
            
            HideWaitingTextClientRpc();
            UpdateGameStateClientRpc("RACING", Color.green);
            
            Debug.Log($"[GameManager] StartGame - Game started, duration: {_gameDuration} seconds");
        }
        
        private void CacheAllPlayersDataAndSubscribe()
        {
            Debug.Log($"[GameManager] CacheAllPlayersDataAndSubscribe START");
            _cachedPlayerData.Clear();
            
            int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"[GameManager] Connected clients count: {connectedCount}");
            
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"[GameManager] Processing client {client.Key}");
                
                PlayerNetwork player = client.Value.PlayerObject?.GetComponent<PlayerNetwork>();
                if (player != null)
                {
                    string nickname = player.Nickname.Value.ToString();
                    int score = player.Score.Value;
                    
                    CachedPlayerData data = new CachedPlayerData
                    {
                        PlayerId = client.Key,
                        Nickname = nickname,
                        Score = score
                    };
                    _cachedPlayerData[client.Key] = data;
                    
                    Debug.Log($"[GameManager] CACHED player {client.Key}: Name='{nickname}', Score={score}");
                    
                    player.Score.OnValueChanged += (oldScore, newScore) => OnPlayerScoreChanged(client.Key, oldScore, newScore);
                }
                else
                {
                    Debug.LogWarning($"[GameManager] PlayerNetwork component is NULL for client {client.Key}!");
                    
                    CachedPlayerData data = new CachedPlayerData
                    {
                        PlayerId = client.Key,
                        Nickname = $"Player_{client.Key}",
                        Score = 0
                    };
                    _cachedPlayerData[client.Key] = data;
                    Debug.Log($"[GameManager] Created TEMP data for player {client.Key}");
                }
            }
            
            Debug.Log($"[GameManager] CacheAllPlayersDataAndSubscribe END - Total cached: {_cachedPlayerData.Count}");
        }
        
        private void OnPlayerScoreChanged(ulong playerId, int oldScore, int newScore)
        {
            Debug.Log($"[GameManager] OnPlayerScoreChanged - Player {playerId}: {oldScore} -> {newScore}");
            
            if (_cachedPlayerData.ContainsKey(playerId))
            {
                var data = _cachedPlayerData[playerId];
                data.Score = newScore;
                _cachedPlayerData[playerId] = data;
                Debug.Log($"[GameManager] Updated cached score for player {playerId}: {newScore}");
            }
            else
            {
                Debug.LogWarning($"[GameManager] Player {playerId} not found in cache!");
            }
        }
        
        [ClientRpc]
        private void HideWaitingTextClientRpc()
        {
            Debug.Log($"[GameManager] HideWaitingTextClientRpc on client {NetworkManager.Singleton.LocalClientId}");
            if (_uiManager != null)
                _uiManager.UpdateWaitingPlayersText(_targetPlayers, _targetPlayers);
        }
        
        [ClientRpc]
        private void UpdateGameStateClientRpc(string stateText, Color stateColor)
        {
            Debug.Log($"[GameManager] UpdateGameStateClientRpc on client {NetworkManager.Singleton.LocalClientId}: {stateText}");
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
                    Debug.Log($"[GameManager] Update - Time's up! Ending game...");
                    StartCoroutine(EndGameWithDelay());
                }
            }
            else if (_currentState == GameState.Results && IsServer)
            {
                _resultsTimeRemaining -= Time.deltaTime;
                if (_resultsTimeRemaining <= 0 && !_isRestarting)
                {
                    Debug.Log($"[GameManager] Update - Results time finished, restarting...");
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
            
            Debug.Log($"[GameManager] FinishRace - Player {winnerId} finished the race!");
            
            CollectResults();
            StartCoroutine(EndGameWithDelay());
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void RequestFinishRaceServerRpc(ulong playerId)
        {
            Debug.Log($"[GameManager] RequestFinishRaceServerRpc from player {playerId}");
            if (!_isGameFinished && _currentState == GameState.Playing)
            {
                FinishRace(playerId);
            }
        }
        
        private void CollectResults()
        {
            Debug.Log($"[GameManager] CollectResults START");
            _playerResults.Clear();
            
            Debug.Log($"[GameManager] Cached data count: {_cachedPlayerData.Count}");
            foreach (var cached in _cachedPlayerData.Values)
            {
                Debug.Log($"[GameManager] Cached entry: PlayerId={cached.PlayerId}, Name={cached.Nickname}, Score={cached.Score}");
            }
            
            foreach (var cached in _cachedPlayerData.Values)
            {
                PlayerResultData data = new PlayerResultData
                {
                    PlayerId = cached.PlayerId,
                    Nickname = cached.Nickname,
                    Score = cached.Score,
                    IsWinner = false
                };
                _playerResults[cached.PlayerId] = data;
                Debug.Log($"[GameManager] Added from CACHE: {data.Nickname} - Score: {data.Score}");
            }
            
            ulong winnerId = 0;
            int highestScore = -1;
            foreach (var result in _playerResults.Values)
            {
                Debug.Log($"[GameManager] Result candidate: {result.Nickname} - Score: {result.Score}");
                if (result.Score > highestScore)
                {
                    highestScore = result.Score;
                    winnerId = result.PlayerId;
                }
            }
            
            if (_playerResults.ContainsKey(winnerId))
            {
                _playerResults[winnerId].IsWinner = true;
                Debug.Log($"[GameManager] WINNER is player {winnerId} with score {highestScore}");
            }
            
            Debug.Log($"[GameManager] CollectResults END - Total results: {_playerResults.Count}");
        }
        
        private IEnumerator EndGameWithDelay()
        {
            Debug.Log($"[GameManager] EndGameWithDelay - Waiting 1 second...");
            yield return new WaitForSeconds(1f);
            EndGame();
        }
        
        private void EndGame()
        {
            if (!IsServer) return;
            if (_resultsShown) return;
            
            Debug.Log($"[GameManager] EndGame - Ending game!");
            
            _resultsShown = true;
            SetState(GameState.Results);
            _resultsTimeRemaining = _resultsDuration;
            
            // Останавливаем музыку
            if (_uiManager != null)
            {
                _uiManager.StopBackgroundMusic();
            }
            
            if (_playerResults.Count == 0 && _cachedPlayerData.Count > 0)
            {
                Debug.Log($"[GameManager] _playerResults is empty, using _cachedPlayerData with {_cachedPlayerData.Count} entries");
                
                foreach (var cached in _cachedPlayerData.Values)
                {
                    PlayerResultData data = new PlayerResultData
                    {
                        PlayerId = cached.PlayerId,
                        Nickname = cached.Nickname,
                        Score = cached.Score,
                        IsWinner = false
                    };
                    _playerResults[cached.PlayerId] = data;
                    Debug.Log($"[GameManager] Added from CACHE: {data.Nickname} - Score: {data.Score}");
                }
                
                ulong winnerId = 0;
                int highestScore = -1;
                foreach (var result in _playerResults.Values)
                {
                    if (result.Score > highestScore)
                    {
                        highestScore = result.Score;
                        winnerId = result.PlayerId;
                    }
                }
                
                if (_playerResults.ContainsKey(winnerId))
                {
                    _playerResults[winnerId].IsWinner = true;
                    Debug.Log($"[GameManager] WINNER is player {winnerId} with score {highestScore}");
                }
            }
            
            var resultsData = new ResultsData();
            resultsData.WinnerId = 0;
            
            Debug.Log($"[GameManager] Building results data from {_playerResults.Count} players");
            
            foreach (var result in _playerResults.Values)
            {
                resultsData.PlayerNames.Add(result.Nickname);
                resultsData.PlayerScores.Add(result.Score);
                if (result.IsWinner)
                    resultsData.WinnerId = result.PlayerId;
                Debug.Log($"[GameManager] Added to resultsData: {result.Nickname} - {result.Score} pts, IsWinner={result.IsWinner}");
            }
            
            Debug.Log($"[GameManager] ResultsData built: PlayerNames.Count={resultsData.PlayerNames.Count}, PlayerScores.Count={resultsData.PlayerScores.Count}, WinnerId={resultsData.WinnerId}");
            
            Debug.Log($"[GameManager] Sending ShowResultsClientRpc to all clients...");
            ShowResultsClientRpc(resultsData);
            
            if (_uiManager != null)
            {
                Debug.Log($"[GameManager] Starting ShowResultsOnHost coroutine...");
                StartCoroutine(ShowResultsOnHost(resultsData));
            }
            else
            {
                Debug.LogError($"[GameManager] UIManager is NULL on host!");
            }
        }
        
        private IEnumerator ShowResultsOnHost(ResultsData resultsData)
        {
            Debug.Log($"[GameManager] ShowResultsOnHost - Waiting 0.2 seconds...");
            yield return new WaitForSeconds(0.2f);
            
            Debug.Log($"[GameManager] ShowResultsOnHost - Showing results on host, player count: {resultsData.PlayerNames.Count}");
            
            if (_uiManager != null)
            {
                _uiManager.ShowResults(resultsData.PlayerNames.ToArray(), resultsData.PlayerScores.ToArray(), resultsData.WinnerId);
            }
            else
            {
                Debug.LogError("[GameManager] UIManager is null on host in ShowResultsOnHost!");
            }
        }
        
        [ClientRpc]
        private void ShowResultsClientRpc(ResultsData resultsData)
        {
            Debug.Log($"[GameManager] ShowResultsClientRpc called on client {NetworkManager.Singleton.LocalClientId} with {resultsData.PlayerNames.Count} players");
            
            for (int i = 0; i < resultsData.PlayerNames.Count; i++)
            {
                Debug.Log($"[GameManager] ShowResultsClientRpc data[{i}]: {resultsData.PlayerNames[i]} - {resultsData.PlayerScores[i]} pts");
            }
            
            if (_uiManager != null)
            {
                Debug.Log($"[GameManager] Starting ShowResultsWithDelay coroutine...");
                StartCoroutine(ShowResultsWithDelay(resultsData));
            }
            else
            {
                Debug.LogError("[GameManager] UIManager is null on client in ShowResultsClientRpc!");
            }
        }
        
        private IEnumerator ShowResultsWithDelay(ResultsData resultsData)
        {
            Debug.Log($"[GameManager] ShowResultsWithDelay - Waiting 0.3 seconds...");
            yield return new WaitForSeconds(0.3f);
            
            Debug.Log($"[GameManager] ShowResultsWithDelay - Actually showing results now with {resultsData.PlayerNames.Count} players");
            _uiManager.ShowResults(resultsData.PlayerNames.ToArray(), resultsData.PlayerScores.ToArray(), resultsData.WinnerId);
        }
        
        private IEnumerator RestartSceneWithDelay()
        {
            Debug.Log($"[GameManager] RestartSceneWithDelay START");
            _isRestarting = true;
            
            bool isHost = NetworkManager.Singleton.IsHost;
            Debug.Log($"[GameManager] IsHost: {isHost}");
            
            if (!isHost)
            {
                Debug.Log($"[GameManager] Client: waiting 1 second before restart...");
                yield return new WaitForSeconds(1f);
                StartCoroutine(RestartSceneWithFade());
            }
            else
            {
                Debug.Log($"[GameManager] Host: waiting 3 seconds before restart...");
                yield return new WaitForSeconds(3f);
                StartCoroutine(RestartSceneWithFade());
            }
        }
        
        private IEnumerator RestartSceneWithFade()
        {
            Debug.Log($"[GameManager] RestartSceneWithFade START");
            if (_fadeStarted) yield break;
            _fadeStarted = true;
            
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
            Debug.Log($"[GameManager] RestartSceneWithFade - Scene reloading...");
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
            Debug.Log($"[GameManager] SetState - New state: {newState}");
            OnStateChangedClientRpc(newState);
        }
        
        [ClientRpc]
        private void OnStateChangedClientRpc(GameState newState)
        {
            Debug.Log($"[GameManager] OnStateChangedClientRpc on client {NetworkManager.Singleton.LocalClientId}: {newState}");
            _currentState = newState;
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void SetTargetPlayersServerRpc(int count)
        {
            if (IsServer)
            {
                _targetPlayers = Mathf.Max(2, Mathf.Min(10, count));
                Debug.Log($"[GameManager] SetTargetPlayersServerRpc - Target players set to: {_targetPlayers}");
            }
        }
        
        public int GetTargetPlayers()
        {
            return _targetPlayers;
        }
        
        private void OnDestroy()
        {
            Debug.Log($"[GameManager] OnDestroy");
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
        public class CachedPlayerData
        {
            public ulong PlayerId;
            public string Nickname;
            public int Score;
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