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

    private void Start()
    {
        _gameManager = FindObjectOfType<GameManager>();
        if (_gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            return;
        }
        // Начальное состояние
        UpdateUIForState(_gameManager.CurrentState.Value);
    }

    public void UpdateUIForState(GameManager.GameState state)
    {
        if (_lobbyPanel) _lobbyPanel.SetActive(state == GameManager.GameState.WaitingForPlayers);
        if (_inGameHUDPanel) _inGameHUDPanel.SetActive(state == GameManager.GameState.InProgress);
        if (_resultsPanel) _resultsPanel.SetActive(state == GameManager.GameState.ShowingResults);
    }

    private void Update()
    {
        if (_gameManager == null) return;
        
        // Обновляем счетчик игроков в лобби
        if (_lobbyPanel.activeSelf && _lobbyPlayersCountText != null)
        {
            _lobbyPlayersCountText.text = $"Waiting for players: {_gameManager.ConnectedPlayers.Value}/3";
        }

        // Обновляем таймер матча
        if (_inGameHUDPanel.activeSelf && _matchTimerText != null)
        {
            int seconds = Mathf.CeilToInt(_gameManager.MatchTimer.Value);
            _matchTimerText.text = $"Time left: {seconds}s";
        }
    }

    public void ShowResultsPanel(List<GameManager.PlayerResult> results)
    {
        if (_resultsPanel == null) return;
        
        _resultsPanel.SetActive(true);
        if (_lobbyPanel) _lobbyPanel.SetActive(false);
        if (_inGameHUDPanel) _inGameHUDPanel.SetActive(false);

        // Очищаем старые результаты
        foreach (Transform child in _resultsContainer)
        {
            Destroy(child.gameObject);
        }

        // Заполняем таблицу новыми результатами
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