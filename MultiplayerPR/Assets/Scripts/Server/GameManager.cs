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
    [SerializeField] private float _resultsScreenDuration = 5f; // 5 секунд после результатов

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
            
            CurrentState.OnChange += OnGameStateChanged;
            
            Debug.Log("[Server] GameManager started. Required players: " + _requiredPlayers);
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

        ConnectedPlayers.Value = base.ServerManager.Clients.Count;
        Debug.Log($"[Server] Players connected: {ConnectedPlayers.Value}/{_requiredPlayers}");

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

    private void StartMatch()
    {
        if (!base.IsServerInitialized) return;

        Debug.Log("[Server] Match started!");
        
        foreach (var conn in base.ServerManager.Clients.Values)
        {
            foreach (var nob in conn.Objects)
            {
                PlayerNetwork pn = nob.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    pn.HP.Value = 100;
                    pn.Score.Value = 0;
                    pn.Ammo.Value = 10;
                    pn.IsAlive.Value = true;
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
        Debug.Log("[Server] Match ended! Showing results...");
        
        CurrentState.Value = GameState.ShowingResults;
        
        RpcShowResults(GetMatchResults());
        
        StartCoroutine(ResetAfterDelay());
    }

    [Server]
    private IEnumerator ResetAfterDelay()
    {
        // Ждём 5 секунд
        yield return new WaitForSeconds(_resultsScreenDuration);
        
        // Возвращаемся в лобби
        CurrentState.Value = GameState.WaitingForPlayers;
        ConnectedPlayers.Value = base.ServerManager.Clients.Count;
        
        Debug.Log($"[Server] Lobby reset. Players: {ConnectedPlayers.Value}/{_requiredPlayers}");
        
        // Если есть 3 игрока, сразу стартуем новый матч
        if (ConnectedPlayers.Value >= _requiredPlayers)
        {
            StartMatch();
        }
    }

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
        return results.OrderByDescending(r => r.Score).ToList();
    }

    [ObserversRpc]
    private void RpcShowResults(List<PlayerResult> results)
    {
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowResultsPanel(results);
        }
    }

    private void OnGameStateChanged(GameState oldValue, GameState newValue, bool asServer)
    {
        if (asServer) return;

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.UpdateUIForState(newValue);
        }
    }
}