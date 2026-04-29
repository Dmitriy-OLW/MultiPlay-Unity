using FishNet.Object;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _cooldown = 0.4f;
    //[SerializeField] private int _maxAmmo = 0;

    private float _lastShotTime;
    private PlayerNetwork _playerNetwork;

    public override void OnStartNetwork()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }
    }

    private void Update()
    {
        if (!base.IsOwner) return;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
    }

    [ServerRpc]
    private void Shoot()
    {
        if (!_playerNetwork.IsAlive.Value) return;
        if (_playerNetwork.Ammo.Value <= 0) return;
        if (Time.time < _lastShotTime + _cooldown) return;

        _lastShotTime = Time.time;
        _playerNetwork.Ammo.Value--;

        Vector3 spawnPos = _firePoint.position + _firePoint.forward * 1.2f;
        Quaternion spawnRot = Quaternion.LookRotation(_firePoint.forward);

        GameObject go = Instantiate(_projectilePrefab, spawnPos, spawnRot);
        
        base.ServerManager.Spawn(go, base.Owner);
    }
}