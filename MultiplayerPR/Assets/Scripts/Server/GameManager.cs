using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private int _requiredPlayers = 3;
    [SerializeField] private float _matchDuration = 60f;
    [SerializeField] private float _resultsScreenDuration = 10f;

    public readonly SyncVar<GameState> CurrentState = new SyncVar<GameState>(GameState.WaitingForPlayers);
    public readonly SyncVar<int> ConnectedPlayers = new SyncVar<int>(0);
    public readonly SyncVar<float> MatchTimer = new SyncVar<float>(0f);

    public enum GameState
    {
        WaitingForPlayers,
        InProgress,
        ShowingResults
    }

    public override void OnStartNetwork()
    {
        if (base.IsServerInitialized)
        {
            base.ServerManager.OnRemoteConnectionState += OnPlayerConnectionChanged;
            base.TimeManager.OnTick += OnServerTick;
            
            // Регистрируем обработчик изменения состояния
            CurrentState.OnChange += OnGameStateChanged;
            
            Debug.Log("[Server] GameManager started and listening for players.");
            
            // Инициализируем счетчик игроков и проверяем запуск матча
            ConnectedPlayers.Value = base.ServerManager.Clients.Count;
            CheckAndStartMatchIfReady();
        }
    }

    public override void OnStopNetwork()
    {
        if (base.ServerManager != null)
            base.ServerManager.OnRemoteConnectionState -= OnPlayerConnectionChanged;
        if (base.TimeManager != null)
            base.TimeManager.OnTick -= OnServerTick;
            
        CurrentState.OnChange -= OnGameStateChanged;
    }

    private void OnPlayerConnectionChanged(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (!base.IsServerInitialized) return;

        // Пересчитываем игроков при каждом подключении/отключении
        ConnectedPlayers.Value = base.ServerManager.Clients.Count;
        Debug.Log($"[Server] Players connected: {ConnectedPlayers.Value}/{_requiredPlayers}");

        // Если мы в лобби, проверяем возможность старта
        if (CurrentState.Value == GameState.WaitingForPlayers)
        {
            CheckAndStartMatchIfReady();
        }
        // Если игрок отключился во время матча и игроков стало меньше нужного
        else if (CurrentState.Value == GameState.InProgress && ConnectedPlayers.Value < _requiredPlayers)
        {
            EndMatch();
        }
    }
    
    [Server]
    private void CheckAndStartMatchIfReady()
    {
        // Если в лобби достаточно игроков, запускаем матч
        if (CurrentState.Value == GameState.WaitingForPlayers && ConnectedPlayers.Value >= _requiredPlayers)
        {
            // Небольшая задержка перед стартом, чтобы игроки успели полностью загрузиться
            StartCoroutine(StartMatchDelayed());
        }
    }
    
    [Server]
    private IEnumerator StartMatchDelayed()
    {
        yield return new WaitForSeconds(1f);
        
        // Повторно проверяем условие перед стартом (за секунду могло что-то измениться)
        if (CurrentState.Value == GameState.WaitingForPlayers && ConnectedPlayers.Value >= _requiredPlayers)
        {
            StartMatch();
        }
    }

    private void OnServerTick()
    {
        if (!base.IsServerInitialized) return;
        if (CurrentState.Value != GameState.InProgress) return;

        MatchTimer.Value -= (float)base.TimeManager.TickDelta;
        if (MatchTimer.Value <= 0f)
        {
            MatchTimer.Value = 0f;
            EndMatch();
        }
    }
    
    [Server]
    private void StartMatch()
    {
        if (!base.IsServerInitialized) return;
        if (CurrentState.Value != GameState.WaitingForPlayers) return;
        
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    if (!pn.IsAlive.Value)
                    {
                        pn.HP.Value = 100;
                        pn.IsAlive.Value = true;
                    }
                    else
                    {
                        pn.HP.Value = 100;
                    }
                    pn.Score.Value = 0;
                    pn.Ammo.Value = 10;
                    
                    Transform spawnPoint = PlayerSpawner.Instance?.GetRandomSpawnPoint();
                    if (spawnPoint != null)
                    {
                        PlayerMovementPredicted movement = pn.GetComponent<PlayerMovementPredicted>();
                        if (movement != null)
                        {
                            movement.RequestTeleportServerRpc(spawnPoint.position, Quaternion.identity);
                        }
                        else
                        {
                            pn.transform.position = spawnPoint.position;
                        }
                    }
                }
            }
        }

        MatchTimer.Value = _matchDuration;
        CurrentState.Value = GameState.InProgress;
    }

    [Server]
    private void EndMatch()
    {
        if (!base.IsServerInitialized) return;
        if (CurrentState.Value != GameState.InProgress) return;
        
        Debug.Log("[Server] Match ended! Showing results...");
        CurrentState.Value = GameState.ShowingResults;
        
        // Отправляем всем клиентам результаты матча
        RpcShowResults(GetMatchResults());
        
        // Запускаем таймер для возврата в лобби
        StartCoroutine(ResetToLobbyCoroutine());
    }

    [Server]
    private IEnumerator ResetToLobbyCoroutine()
    {
        yield return new WaitForSeconds(_resultsScreenDuration);
        ResetToLobby();
    }

    [Server]
    private void ResetToLobby()
    {
        Debug.Log("[Server] Returning to lobby...");
        
        // Сбрасываем состояние игроков для лобби (делаем их видимыми и живыми)
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    if (!pn.IsAlive.Value)
                    {
                        pn.HP.Value = 100;
                        pn.IsAlive.Value = true;
                    }
                    pn.Score.Value = 0;
                    pn.Ammo.Value = 10;
                }
            }
        }
        
        CurrentState.Value = GameState.WaitingForPlayers;
        ConnectedPlayers.Value = base.ServerManager.Clients.Count;
        
        Debug.Log($"[Server] Lobby reset. Players: {ConnectedPlayers.Value}/{_requiredPlayers}");
        
        // Проверяем, не достаточно ли уже игроков для нового матча
        CheckAndStartMatchIfReady();
    }

    // Структура для хранения результатов одного игрока
    public struct PlayerResult
    {
        public string Nickname;
        public int Score;
    }

    [Server]
    private List<PlayerResult> GetMatchResults()
    {
        var results = new List<PlayerResult>();
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    results.Add(new PlayerResult
                    {
                        Nickname = pn.Nickname.Value,
                        Score = pn.Score.Value
                    });
                }
            }
        }
        // Сортировка по очкам (по убыванию)
        return results.OrderByDescending(r => r.Score).ToList();
    }

    // Клиентский RPC для отображения результатов
    [ObserversRpc]
    private void RpcShowResults(List<PlayerResult> results)
    {
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowResultsPanel(results);
        }
        else
        {
            Debug.LogError("UIManager not found!");
        }
    }

    // Обработчик изменения состояния игры на клиенте
    private void OnGameStateChanged(GameState oldValue, GameState newValue, bool asServer)
    {
        if (asServer) return;

        Debug.Log($"Game state changed: {oldValue} -> {newValue}");
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.UpdateUIForState(newValue);
        }
    }
}