using FishNet.Object;
using TMPro;
using UnityEngine;

namespace Multi.FishNet
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
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (_playerNetwork == null)
                _playerNetwork = GetComponent<PlayerNetwork>();
            
            if (_playerNetwork != null)
            {
                // Подписка на события UI
                _playerNetwork.OnNicknameChangedUI += UpdateNicknameUI;
                _playerNetwork.OnHealthChangedUI += UpdateHealthUI;
                _playerNetwork.OnAmmoChangedUI += UpdateAmmoUI;
                _playerNetwork.OnScoreChangedUI += UpdateScoreUI;
                
                // Инициализация UI
                UpdateNicknameUI(_playerNetwork.Nickname.Value);
                UpdateHealthUI(_playerNetwork.Health.Value);
                UpdateAmmoUI(_playerNetwork.Ammo.Value);
                UpdateScoreUI(_playerNetwork.Score.Value);
            }
            
            if (_billboardObject != null && _billboardObject.GetComponent<BillboardUI>() == null)
                _billboardObject.AddComponent<BillboardUI>();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            if (_playerNetwork != null)
            {
                _playerNetwork.OnNicknameChangedUI -= UpdateNicknameUI;
                _playerNetwork.OnHealthChangedUI -= UpdateHealthUI;
                _playerNetwork.OnAmmoChangedUI -= UpdateAmmoUI;
                _playerNetwork.OnScoreChangedUI -= UpdateScoreUI;
            }
        }
        
        private void UpdateNicknameUI(string nickname)
        {
            if (_nicknameText != null)
                _nicknameText.text = nickname;
        }
        
        private void UpdateHealthUI(int health)
        {
            if (_hpText != null)
                _hpText.text = $"HP: {health}/100";
        }
        
        private void UpdateAmmoUI(int ammo)
        {
            if (_ammoText != null)
                _ammoText.text = $"Ammo: {ammo}/10";
        }
        
        private void UpdateScoreUI(int score)
        {
            if (_scoreText != null)
                _scoreText.text = $"Score: {score}";
        }
    }
}