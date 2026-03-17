using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerView : NetworkBehaviour
    {
        [Header("Ссылки на компоненты")] [SerializeField]
        private PlayerNetwork _playerNetwork;

        [SerializeField] private TMP_Text _nicknameText;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private GameObject _uiPanel;

        [Header("Billboard")] [SerializeField] private GameObject _billboardObject;

        public override void OnNetworkSpawn()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();

            _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
            _playerNetwork.Health.OnValueChanged += OnHealthChanged;

            OnNicknameChanged(default, _playerNetwork.Nickname.Value);
            OnHealthChanged(0, _playerNetwork.Health.Value);

            if (!IsOwner && _uiPanel != null)
            {
            }

            if (_billboardObject != null)
            {
                BillboardUI billboard = _billboardObject.GetComponent<BillboardUI>();
                if (billboard == null)
                    _billboardObject.AddComponent<BillboardUI>();
            }
        }

        public override void OnNetworkDespawn()
        {
            _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
            _playerNetwork.Health.OnValueChanged -= OnHealthChanged;
        }

        private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
        {
            if (_nicknameText != null)
                _nicknameText.text = newValue.ToString();
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            if (_hpText != null)
                _hpText.text = $"HP: {newValue}";
        }
    }
}