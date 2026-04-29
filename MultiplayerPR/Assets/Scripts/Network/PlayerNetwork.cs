using Unity.Collections;
using UnityEngine;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

public class PlayerNetwork : NetworkBehaviour
{
    public readonly SyncVar<string> Nickname = new SyncVar<string>("Player");
    public readonly SyncVar<int> HP = new SyncVar<int>(100);
    public readonly SyncVar<bool> IsAlive = new SyncVar<bool>(true);
    public readonly SyncVar<int> Ammo = new SyncVar<int>(20);
    public readonly SyncVar<float> RespawnTime = new SyncVar<float>(0f);

    public override void OnStartNetwork()
    {
        HP.OnChange += OnHpChanged;
        IsAlive.OnChange += OnIsAliveChanged;
        RespawnTime.OnChange += OnRespawnTimeChanged;

        if (base.Owner.IsLocalClient)
        {
            StartCoroutine(SendNicknameAfterSpawn());
            //SetNicknameServer(ConnectionUI.PlayerNickname);
        }
    }

    private IEnumerator SendNicknameAfterSpawn()
    {
        // Ждём один кадр, чтобы сервер успел инициализироваться
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
        // UI обновляется в PlayerView
    }

    private void HidePlayer()
    {
        //MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        //foreach (var r in renderers) r.enabled = false;
        gameObject.GetComponent<MeshRenderer>().enabled = false;

        Collider col = GetComponent<Collider>();
        if (col) col.enabled = false;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
    }

    private void ShowPlayer()
    {
        //MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        //foreach (var r in renderers) r.enabled = true;
        gameObject.GetComponent<MeshRenderer>().enabled = true;

        Collider col = GetComponent<Collider>();
        if (col) col.enabled = true;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetNicknameServer(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{OwnerId}" : nickname.Trim();
        Nickname.Value = safeValue;
    }

    private IEnumerator RespawnRoutine()
    {
        float timer = 3f;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            RespawnTime.Value = timer;
            yield return null;
        }

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        int idx = Random.Range(0, spawnPoints.Length);
        Vector3 newPosition = spawnPoints[idx].transform.position;

        TeleportPlayerObservers(newPosition);
        if (base.IsServerInitialized)
        {
            transform.position = newPosition;
        }

        HP.Value = 100;
        IsAlive.Value = true;
        Ammo.Value = 20;
    }

    [ObserversRpc(BufferLast = true)]
    private void TeleportPlayerObservers(Vector3 spawnPosition)
    {
        if (!base.IsServerInitialized)
        {
            transform.position = spawnPosition;
        }
    }
}