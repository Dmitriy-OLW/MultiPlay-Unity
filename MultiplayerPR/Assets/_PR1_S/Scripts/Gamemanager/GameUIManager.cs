using TMPro;
using UnityEngine;
using System.Collections.Generic;

namespace Multi.PR1
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("Game Status UI")]
        [SerializeField] private TMP_Text _gameStateText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private GameObject _inputBlockedOverlay;
        
        [Header("Countdown UI")]
        [SerializeField] private TMP_Text _countdownText;
        
        [Header("Game UI (ожидание игроков здесь)")]
        [SerializeField] private GameObject _gameUIPanel;
        [SerializeField] private TMP_Text _waitingForPlayersText;
        
        [Header("Results UI")]
        [SerializeField] private GameObject _resultsPanel;
        [SerializeField] private Transform _resultsContainer;
        [SerializeField] private GameObject _resultEntryPrefab; // Должен содержать TMP_Text компонент!
        
        [Header("Lobby UI (только кнопки подключения)")]
        [SerializeField] private GameObject _lobbyPanel;
        
        [Header("Pause Menu")]
        [SerializeField] private GameObject _pauseMenuPanel;
        
        private void Start()
        {
            // Показываем только лобби при старте
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(true);
                
            if (_gameUIPanel != null)
                _gameUIPanel.SetActive(false);
                
            if (_resultsPanel != null)
                _resultsPanel.SetActive(false);
                
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.SetActive(false);
                
            if (_countdownText != null)
                _countdownText.gameObject.SetActive(false);
                
            // Включаем курсор при старте
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            
            Debug.Log("[GameUIManager] Started - Lobby visible");
        }
        
        private void LateUpdate()
        {
            // Обработка нажатия ESC для открытия меню паузы
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.TogglePauseMenu();
                }
                else
                {
                    TogglePauseMenu();
                }
            }
        }
        
        public void ShowLobbyUI(bool show)
        {
            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(show);
                Debug.Log($"[GameUIManager] Lobby UI: {show}");
            }
        }
        
        public void ShowGameUI(bool show)
        {
            if (_gameUIPanel != null)
            {
                _gameUIPanel.SetActive(show);
                Debug.Log($"[GameUIManager] Game UI Panel: {show}");
            }
        }
        
        public void ShowResultsUI(bool show)
        {
            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(show);
                Debug.Log($"[GameUIManager] Results UI: {show}");
            }
        }
        
        public void UpdateWaitingPlayersText(int currentPlayers, int targetPlayers)
        {
            if (_waitingForPlayersText != null)
            {
                if (currentPlayers < targetPlayers)
                {
                    _waitingForPlayersText.text = $"Waiting for players...\n{currentPlayers}/{targetPlayers}";
                    _waitingForPlayersText.gameObject.SetActive(true);
                }
                else
                {
                    _waitingForPlayersText.gameObject.SetActive(false);
                }
                Debug.Log($"[GameUIManager] Waiting text: {currentPlayers}/{targetPlayers}");
            }
        }
        
        public void UpdateCountdown(float remainingTime)
        {
            if (_countdownText == null) return;
            
            if (remainingTime > 0)
            {
                _countdownText.gameObject.SetActive(true);
                int displayValue = Mathf.CeilToInt(remainingTime);
                _countdownText.text = displayValue.ToString();
                _countdownText.fontSize = 80;
                _countdownText.color = Color.white;
                
                if (_inputBlockedOverlay != null)
                    _inputBlockedOverlay.SetActive(true);
            }
            else if (remainingTime < 0 && remainingTime > -1f)
            {
                _countdownText.text = "GO!";
                _countdownText.fontSize = 100;
                _countdownText.color = Color.green;
            }
            else if (remainingTime == 0f)
            {
                _countdownText.gameObject.SetActive(false);
                
                if (_inputBlockedOverlay != null)
                    _inputBlockedOverlay.SetActive(false);
            }
        }
        
        public void UpdateGameTimer(float timeRemaining)
        {
            if (_timerText == null) return;
            
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";
            
            if (timeRemaining <= 30f)
            {
                _timerText.color = Color.red;
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
        
        public void UpdateGameStateText(string stateText, Color stateColor)
        {
            if (_gameStateText != null)
            {
                _gameStateText.text = stateText;
                _gameStateText.color = stateColor;
            }
        }
        
        public void ShowResults(string[] playerNames, int[] playerScores, ulong winnerId)
        {
            Debug.Log($"[GameUIManager] Showing results for {playerNames.Length} players");
            
            // Сначала скрываем Game UI
            ShowGameUI(false);
            
            // Показываем Results UI
            ShowResultsUI(true);
            
            if (_resultsContainer == null)
            {
                Debug.LogError("[GameUIManager] Results container is null!");
                return;
            }
            
            if (_resultEntryPrefab == null)
            {
                Debug.LogError("[GameUIManager] Result entry prefab is null!");
                return;
            }
            
            // Очищаем контейнер
            foreach (Transform child in _resultsContainer)
            {
                Destroy(child.gameObject);
            }
            
            var results = new List<(string name, int score)>();
            for (int i = 0; i < playerNames.Length; i++)
            {
                results.Add((playerNames[i], playerScores[i]));
            }
            results.Sort((a, b) => b.score.CompareTo(a.score));
            
            for (int i = 0; i < results.Count; i++)
            {
                GameObject entry = Instantiate(_resultEntryPrefab, _resultsContainer);
                
                // Ищем TMP_Text в prefab'е
                TMP_Text entryText = entry.GetComponent<TMP_Text>();
                if (entryText == null)
                {
                    entryText = entry.GetComponentInChildren<TMP_Text>();
                }
                
                if (entryText != null)
                {
                    string prefix = (i == 0) ? "🏆 " : "";
                    entryText.text = $"{prefix}{results[i].name}: {results[i].score} pts";
                    entryText.color = (i == 0) ? Color.yellow : Color.white;
                    entryText.fontSize = (i == 0) ? 36 : 28;
                    Debug.Log($"[GameUIManager] Added result entry: {entryText.text}");
                }
                else
                {
                    Debug.LogError($"[GameUIManager] Result entry prefab has no TMP_Text component!");
                }
            }
        }
        
        public void TogglePauseMenu()
        {
            if (_pauseMenuPanel == null)
            {
                Debug.LogWarning("[GameUIManager] Pause menu panel not assigned!");
                return;
            }
            
            bool isActive = !_pauseMenuPanel.activeSelf;
            _pauseMenuPanel.SetActive(isActive);
            
            if (isActive)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            Debug.Log($"[GameUIManager] Pause menu: {(isActive ? "opened" : "closed")}");
        }
        
        public void SetPauseMenuActive(bool active)
        {
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.SetActive(active);
        }
        
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}