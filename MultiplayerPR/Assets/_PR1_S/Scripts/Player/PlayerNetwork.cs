using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerNetwork : NetworkBehaviour
    {
       public NetworkVariable<FixedString32Bytes> Nickname = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Health = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<Color> PlayerColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        [SerializeField] private GameObject BulletPrefab;

        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
            }

            PlayerColor.OnValueChanged += OnColorChanged;

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", PlayerColor.Value);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorChanged;
        }

        private void OnColorChanged(Color oldValue, Color newValue)
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", newValue);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitNicknameServerRpc(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{OwnerClientId}" : nickname.Trim();
            Nickname.Value = safeValue;

            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
        }

        [ServerRpc]
        public void RequestRandomColorServerRpc()
        {
            PlayerColor.Value = new Color(Random.value, Random.value, Random.value);
        }

        [ServerRpc]
        public void ShootServerRpc(Vector3 spawnPos, Vector3 direction)
        {
            GameObject bullet = Instantiate(BulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            BulletNetwork bulletScript = bullet.GetComponent<BulletNetwork>();
            bulletScript.OwnerId = OwnerClientId;
            bullet.GetComponent<NetworkObject>().Spawn();
        }

        public void TakeDamage(int damage, ulong shooterId)
        {
            if (!IsServer) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);

            if (Health.Value <= 0)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterId, out var shooterClient))
                {
                    var shooter = shooterClient.PlayerObject.GetComponent<PlayerNetwork>();
                    if (shooter != null) shooter.Score.Value += 1;
                }

                Health.Value = 100; // Респавн
                Vector3 randomSpawnPoint =
                    new Vector3(Random.Range(-5f, 5f), transform.position.y, Random.Range(-5f, 5f));
                TeleportPlayerClientRpc(randomSpawnPoint);
            }
        }

        [ClientRpc]
        private void TeleportPlayerClientRpc(Vector3 spawnPosition)
        {
            if (IsOwner)
            {
                transform.position = spawnPosition;
            }
        }
    }
}