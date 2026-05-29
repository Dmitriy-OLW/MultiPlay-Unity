using TMPro;
using UnityEngine;
using Unity.Netcode;

namespace Multi.PR1
{
    public class GameUIManager : NetworkBehaviour
    {
        [Header("Game Status UI")]
        [SerializeField] private TMP_Text _gameStateText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private GameObject _inputBlockedOverlay;
        
        [Header("Countdown UI")]
        [SerializeField] private TMP_Text _countdownText;
        
        [Header("Results UI")]
        [SerializeField] private GameObject _resultsPanel;
        [SerializeField] private Transform _resultsContainer;
        [SerializeField] private GameObject _resultEntryPrefab;
        
        [Header("Lobby UI")]
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private TMP_Text _waitingForPlayersText;
        
        [Header("Pause Menu")]
        [SerializeField] private GameObject _pauseMenuPanel;
        
        private float _cachedTimeRemaining;
        
        private void Start()
        {
            // Изначально показываем только лобби
            ShowLobbyUI(true);
            ShowGameUI(false);
            ShowResultsUI(false);
            
            if (_countdownText != null)
                _countdownText.gameObject.SetActive(false);
                
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.SetActive(false);
        }
        
        private void Update()
        {
            // Обновление UI в зависимости от состояния GameManager
            if (GameManager.Instance != null)
            {
                UpdateGameStateDisplay();
                UpdateInputBlockedDisplay();
            }
        }
        
        public void ShowLobbyUI(bool show)
        {
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(show);
        }
        
        public void ShowGameUI(bool show)
        {
            if (_gameStateText != null)
                _gameStateText.transform.parent.gameObject.SetActive(show);
        }
        
        public void ShowResultsUI(bool show)
        {
            if (_resultsPanel != null)
                _resultsPanel.SetActive(show);
        }
        
        public void UpdateWaitingPlayersText(int currentPlayers, int targetPlayers)
        {
            if (_waitingForPlayersText != null)
            {
                _waitingForPlayersText.text = $"Waiting for players...\n{currentPlayers}/{targetPlayers}";
            }
        }
        
        public void UpdateCountdown(float remainingTime)
        {
            if (_countdownText == null) return;
            
            if (remainingTime > 0)
            {
                _countdownText.gameObject.SetActive(true);
                int displayValue = Mathf.CeilToInt(remainingTime);
                
                if (displayValue > 0)
                {
                    _countdownText.text = displayValue.ToString();
                    _countdownText.fontSize = 80;
                    _countdownText.color = Color.white;
                    
                    // Эффект пульсации при каждой секунде
                    StartCoroutine(PulseCountdown());
                }
            }
            else if (remainingTime > -0.5f)
            {
                _countdownText.text = "GO!";
                _countdownText.fontSize = 100;
                _countdownText.color = Color.green;
            }
            else
            {
                _countdownText.gameObject.SetActive(false);
            }
        }
        
        private System.Collections.IEnumerator PulseCountdown()
        {
            Vector3 originalScale = _countdownText.transform.localScale;
            _countdownText.transform.localScale = originalScale * 1.3f;
            yield return new WaitForSeconds(0.1f);
            _countdownText.transform.localScale = originalScale;
        }
        
        public void UpdateGameTimer(float timeRemaining)
        {
            if (_timerText == null) return;
            
            _cachedTimeRemaining = timeRemaining;
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";
            
            // Меняем цвет при остатке менее 30 секунд
            if (timeRemaining <= 30f)
            {
                _timerText.color = Color.red;
                // Мигание при последних 10 секундах
                if (timeRemaining <= 10f)
                {
                    float alpha = Mathf.PingPong(Time.time * 3f, 1f);
                    _timerText.alpha = alpha;
                }
            }
            else
            {
                _timerText.color = Color.white;
                _timerText.alpha = 1f;
            }
        }
        
        private void UpdateGameStateDisplay()
        {
            if (_gameStateText == null) return;
            
            string stateText = "";
            Color stateColor = Color.white;
            
            switch (GameManager.Instance.CurrentState)
            {
                case GameState.Lobby:
                    stateText = "LOBBY";
                    stateColor = Color.yellow;
                    ShowLobbyUI(true);
                    ShowGameUI(false);
                    ShowResultsUI(false);
                    break;
                case GameState.Starting:
                    stateText = "STARTING...";
                    stateColor = Color.cyan;
                    ShowLobbyUI(false);
                    ShowGameUI(true);
                    ShowResultsUI(false);
                    break;
                case GameState.Playing:
                    stateText = "RACING";
                    stateColor = Color.green;
                    ShowLobbyUI(false);
                    ShowGameUI(true);
                    ShowResultsUI(false);
                    break;
                case GameState.Results:
                    stateText = "GAME OVER";
                    stateColor = Color.red;
                    ShowLobbyUI(false);
                    ShowGameUI(false);
                    ShowResultsUI(true);
                    break;
            }
            
            _gameStateText.text = stateText;
            _gameStateText.color = stateColor;
        }
        
        private void UpdateInputBlockedDisplay()
        {
            if (_inputBlockedOverlay == null) return;
            
            bool isBlocked = GameManager.Instance.IsInputBlocked;
            _inputBlockedOverlay.SetActive(isBlocked && GameManager.Instance.CurrentState != GameState.Lobby);
        }
        
        public void ShowResults(string[] playerNames, int[] playerScores, ulong winnerId)
        {
            if (_resultsContainer == null || _resultEntryPrefab == null) return;
            
            // Очищаем контейнер
            foreach (Transform child in _resultsContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Сортируем по очкам (по убыванию)
            var results = new System.Collections.Generic.List<(string name, int score, bool isWinner)>();
            for (int i = 0; i < playerNames.Length; i++)
            {
                results.Add((playerNames[i], playerScores[i], false));
            }
            results.Sort((a, b) => b.score.CompareTo(a.score));
            
            // Отмечаем победителя
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                result.isWinner = (i == 0);
                results[i] = result;
            }
            
            // Создаём записи
            foreach (var result in results)
            {
                GameObject entry = Instantiate(_resultEntryPrefab, _resultsContainer);
                TMP_Text entryText = entry.GetComponent<TMP_Text>();
                if (entryText != null)
                {
                    string prefix = result.isWinner ? "🏆 " : "";
                    entryText.text = $"{prefix}{result.name}: {result.score} pts";
                    
                    if (result.isWinner)
                        entryText.color = Color.yellow;
                    else
                        entryText.color = Color.white;
                }
            }
        }
        
        public void TogglePauseMenu()
        {
            if (_pauseMenuPanel == null) return;
            
            bool isActive = !_pauseMenuPanel.activeSelf;
            _pauseMenuPanel.SetActive(isActive);
        }
        
        public void SetPauseMenuActive(bool active)
        {
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.SetActive(active);
        }
    }
}