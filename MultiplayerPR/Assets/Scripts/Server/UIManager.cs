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

    private GameManager _gameManager;
    private bool _isInitialized = false;
    private float _resultsTimer = 0f;
    private bool _isShowingResults = false;

    private void Start()
    {
        InvokeRepeating(nameof(TryFindGameManager), 0.5f, 0.5f);
    }

    private void TryFindGameManager()
    {
        if (_isInitialized) return;

        _gameManager = FindObjectOfType<GameManager>();
        if (_gameManager != null && _gameManager.IsSpawned)
        {
            _isInitialized = true;
            CancelInvoke(nameof(TryFindGameManager));
            UpdateUIForState(_gameManager.CurrentState.Value);
        }
    }

    private void Update()
    {
        if (!_isInitialized || _gameManager == null) return;
        
        // Обновляем лобби
        if (_lobbyPanel != null && _lobbyPanel.activeSelf)
        {
            UpdateLobbyText();
        }

        // Обновляем таймер матча
        if (_inGameHUDPanel != null && _inGameHUDPanel.activeSelf)
        {
            int seconds = Mathf.CeilToInt(_gameManager.MatchTimer.Value);
            _matchTimerText.text = $"Time left: {seconds}s";
        }

        // Обновляем таймер на экране результатов
        if (_isShowingResults)
        {
            _resultsTimer -= Time.deltaTime;
            if (_resultsTimer <= 0)
            {
                _resultsTimer = 0;
                _isShowingResults = false;
            }
        }
    }

    private void UpdateLobbyText()
    {
        if (_lobbyPlayersCountText != null && _gameManager != null)
        {
            string text = $"Waiting for players: {_gameManager.ConnectedPlayers.Value}/3";
            
            // Если показываем результаты и ждём рестарта - дописываем таймер
            if (_isShowingResults)
            {
                int seconds = Mathf.CeilToInt(_resultsTimer);
                text += $"\nNext match in: {seconds}s";
            }
            
            _lobbyPlayersCountText.text = text;
        }
    }

    public void UpdateUIForState(GameManager.GameState state)
    {
        // Выключаем всё
        if (_lobbyPanel != null) _lobbyPanel.SetActive(false);
        if (_inGameHUDPanel != null) _inGameHUDPanel.SetActive(false);
        if (_resultsPanel != null) _resultsPanel.SetActive(false);

        switch (state)
        {
            case GameManager.GameState.WaitingForPlayers:
                // После результатов показываем лобби с таймером
                if (_isShowingResults)
                {
                    if (_lobbyPanel != null) _lobbyPanel.SetActive(true);
                }
                else
                {
                    if (_lobbyPanel != null) _lobbyPanel.SetActive(true);
                }
                break;
                
            case GameManager.GameState.InProgress:
                if (_inGameHUDPanel != null) _inGameHUDPanel.SetActive(true);
                _isShowingResults = false;
                break;
                
            case GameManager.GameState.ShowingResults:
                if (_resultsPanel != null) _resultsPanel.SetActive(true);
                _resultsTimer = 5f;
                _isShowingResults = true;
                break;
        }
    }

    public void ShowResultsPanel(List<GameManager.PlayerResult> results)
    {
        if (_resultsContainer != null && _playerResultPrefab != null)
        {
            // Очищаем старые результаты
            foreach (Transform child in _resultsContainer)
            {
                Destroy(child.gameObject);
            }

            // Заполняем таблицу
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