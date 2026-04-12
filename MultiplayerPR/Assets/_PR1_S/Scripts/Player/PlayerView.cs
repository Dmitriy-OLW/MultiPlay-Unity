using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class PlayerView : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text _nicknameText;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private GameObject _uiPanel;

        [Header("Billboard")]
        [SerializeField] private GameObject _billboardObject;
        
        private PlayerNetwork _playerNetwork;

        public override void OnNetworkSpawn()
        {
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();

            if (_playerNetwork != null)
            {
                _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
                _playerNetwork.Health.OnValueChanged += OnHealthChanged;
                _playerNetwork.Ammo.OnValueChanged += OnAmmoChanged;
                _playerNetwork.Score.OnValueChanged += OnScoreChanged;
                
                OnNicknameChanged(default, _playerNetwork.Nickname.Value);
                OnHealthChanged(0, _playerNetwork.Health.Value);
                OnAmmoChanged(0, _playerNetwork.Ammo.Value);
                OnScoreChanged(0, _playerNetwork.Score.Value);
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
            if (_playerNetwork != null)
            {
                _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
                _playerNetwork.Health.OnValueChanged -= OnHealthChanged;
                _playerNetwork.Ammo.OnValueChanged -= OnAmmoChanged;
                _playerNetwork.Score.OnValueChanged -= OnScoreChanged;
            }
        }

        private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
        {
            if (_nicknameText != null)
                _nicknameText.text = newValue.ToString();
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            if (_hpText != null)
                _hpText.text = $"HP: {newValue}/100";
        }
        
        private void OnAmmoChanged(int oldValue, int newValue)
        {
            if (_ammoText != null)
                _ammoText.text = $"Ammo: {newValue}/10";
        }
        
        private void OnScoreChanged(int oldValue, int newValue)
        {
            if (_scoreText != null)
                _scoreText.text = $"Score: {newValue}";
        }
    }
}