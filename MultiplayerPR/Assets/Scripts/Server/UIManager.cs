using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private GameObject _inGameHUDPanel;
    [SerializeField] private GameObject _resultsPanel;
    [SerializeField] private Transform _resultsContainer;
    [SerializeField] private GameObject _playerResultPrefab;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text _lobbyPlayersCountText;
    [SerializeField] private TMP_Text _matchTimerText;

    [Header("References")]
    [SerializeField] private GameManager _gameManager; // Прямая ссылка через инспектор

    private void Start()
    {
        // Если ссылка не установлена в инспекторе, пробуем найти
        if (_gameManager == null)
        {
            _gameManager = FindObjectOfType<GameManager>();
        }

        if (_gameManager == null)
        {
            Debug.LogError("GameManager not found! Please assign it in the inspector.");
            return;
        }

        // Начальное состояние
        UpdateUIForState(_gameManager.CurrentState.Value);
        
        // Подписываемся на изменение состояния
        _gameManager.CurrentState.OnChange += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
        {
            _gameManager.CurrentState.OnChange -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameManager.GameState oldValue, GameManager.GameState newValue, bool asServer)
    {
        if (asServer) return;
        UpdateUIForState(newValue);
    }

    public void UpdateUIForState(GameManager.GameState state)
    {
        Debug.Log($"UIManager: Updating UI for state: {state}");

        // Сначала скрываем все панели
        if (_lobbyPanel != null) _lobbyPanel.SetActive(false);
        if (_inGameHUDPanel != null) _inGameHUDPanel.SetActive(false);
        if (_resultsPanel != null) _resultsPanel.SetActive(false);

        // Показываем нужную панель
        switch (state)
        {
            case GameManager.GameState.WaitingForPlayers:
                if (_lobbyPanel != null) _lobbyPanel.SetActive(true);
                break;
            case GameManager.GameState.InProgress:
                if (_inGameHUDPanel != null) _inGameHUDPanel.SetActive(true);
                break;
            case GameManager.GameState.ShowingResults:
                if (_resultsPanel != null) _resultsPanel.SetActive(true);
                break;
        }
    }

    private void Update()
    {
        if (_gameManager == null) return;
        
        // Обновляем счетчик игроков в лобби
        if (_lobbyPanel != null && _lobbyPanel.activeSelf && _lobbyPlayersCountText != null)
        {
            _lobbyPlayersCountText.text = $"Waiting for players: {_gameManager.ConnectedPlayers.Value}/3";
        }

        // Обновляем таймер матча
        if (_inGameHUDPanel != null && _inGameHUDPanel.activeSelf && _matchTimerText != null)
        {
            int seconds = Mathf.CeilToInt(_gameManager.MatchTimer.Value);
            _matchTimerText.text = $"Time left: {seconds}s";
        }
    }

    public void ShowResultsPanel(List<GameManager.PlayerResult> results)
    {
        Debug.Log("UIManager: Showing results panel");
        
        if (_resultsPanel == null)
        {
            Debug.LogError("Results panel is not assigned!");
            return;
        }
        
        // Скрываем другие панели
        if (_lobbyPanel != null) _lobbyPanel.SetActive(false);
        if (_inGameHUDPanel != null) _inGameHUDPanel.SetActive(false);
        
        // Показываем панель результатов
        _resultsPanel.SetActive(true);

        // Очищаем старые результаты
        if (_resultsContainer != null)
        {
            foreach (Transform child in _resultsContainer)
            {
                Destroy(child.gameObject);
            }

            // Заполняем таблицу новыми результатами
            if (_playerResultPrefab != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    GameObject resultEntry = Instantiate(_playerResultPrefab, _resultsContainer);
                    TMP_Text textComponent = resultEntry.GetComponent<TMP_Text>();
                    if (textComponent != null)
                    {
                        textComponent.text = $"{i + 1}. {results[i].Nickname} — Score: {results[i].Score}";
                    }
                }
            }
        }
    }
}