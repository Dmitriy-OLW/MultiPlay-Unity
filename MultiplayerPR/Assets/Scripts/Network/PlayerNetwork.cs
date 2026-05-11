using UnityEngine;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerNetwork : NetworkBehaviour
{
    public readonly SyncVar<string> Nickname = new SyncVar<string>("Player");
    public readonly SyncVar<int> HP = new SyncVar<int>(100);
    public readonly SyncVar<bool> IsAlive = new SyncVar<bool>(true);
    public readonly SyncVar<int> Ammo = new SyncVar<int>(10);
    public readonly SyncVar<float> RespawnTime = new SyncVar<float>(0f);
    public readonly SyncVar<int> Score = new SyncVar<int>(0);

    private PlayerMovementPredicted _movement;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovementPredicted>();
    }

    public override void OnStartNetwork()
    {
        HP.OnChange += OnHpChanged;
        IsAlive.OnChange += OnIsAliveChanged;
        RespawnTime.OnChange += OnRespawnTimeChanged;

        if (base.Owner.IsLocalClient)
        {
            StartCoroutine(SendNicknameAfterSpawn());
        }
        
        CharacterController cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = true;
    }

    private IEnumerator SendNicknameAfterSpawn()
    {
        yield return null;
        SetNicknameServer(ConnectionUI.PlayerNickname);
    }

    public override void OnStopNetwork()
    {
        HP.OnChange -= OnHpChanged;
        IsAlive.OnChange -= OnIsAliveChanged;
        RespawnTime.OnChange -= OnRespawnTimeChanged;
    }

    private void OnHpChanged(int prev, int next, bool asServer)
    {
        if (!asServer) return;
        if (next <= 0 && IsAlive.Value)
        {
            IsAlive.Value = false;
            StartCoroutine(RespawnRoutine());
        }
    }

    private void OnIsAliveChanged(bool prev, bool next, bool asServer)
    {
        if (next == false)
            HidePlayer();
        else
            ShowPlayer();
    }

    private void OnRespawnTimeChanged(float oldValue, float newValue, bool asServer)
    {
    }

    private void HidePlayer()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col) col.enabled = false;
        
        if (_movement != null) _movement.enabled = false;
    }

    private void ShowPlayer()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = true;

        Collider col = GetComponent<Collider>();
        if (col) col.enabled = true;
        
        if (_movement != null) _movement.enabled = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetNicknameServer(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{OwnerId}" : nickname.Trim();
        Nickname.Value = safeValue;
    }

    public void AddScore(int amount)
    {
        if (!base.IsServerInitialized) return;
        Score.Value += amount;
    }

    private IEnumerator RespawnRoutine()
    {
        float timer = 3f;
        Transform spawnPoint = PlayerSpawner.Instance?.GetRandomSpawnPoint();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            RespawnTime.Value = Mathf.Max(0, timer);
            yield return null;
        }

        RespawnTime.Value = 0f;
        
        TeleportPlayer(spawnPosition);
        
        if (base.IsServerInitialized)
        {
            HP.Value = 100;
            IsAlive.Value = true;
            Ammo.Value = 10;
        }
    }
    
    [Server]
    private void TeleportPlayer(Vector3 spawnPosition)
    {
        if (!base.IsServerInitialized) return;
        
        PlayerMovementPredicted movement = GetComponent<PlayerMovementPredicted>();
        if (movement != null)
        {
            movement.RequestTeleportServerRpc(spawnPosition, Quaternion.identity);
        }
        else
        {
            transform.position = spawnPosition;
        }
    }
}