using TMPro;           
using Unity.Collections;
using FishNet.Object;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNetwork _playerNetwork;

    [Header("UI Panels")]
    [SerializeField] private GameObject _mainPanel;  
    [SerializeField] private GameObject _respawnPanel;

    [Header("Texts")]
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _respawnTimerText;
    [SerializeField] private Transform _uiCanvas;

    public override void OnStartNetwork()
    {
        _playerNetwork.Nickname.OnChange += OnNicknameChanged;
        _playerNetwork.HP.OnChange += OnHpChanged;
        _playerNetwork.Ammo.OnChange += OnAmmoChanged;

        _playerNetwork.RespawnTime.OnChange += OnRespawnTimeChanged;

        OnNicknameChanged("", _playerNetwork.Nickname.Value, true);
        OnHpChanged(0, _playerNetwork.HP.Value, true);

        AdjustUIToOwner();

        if (!base.Owner.IsLocalClient && _ammoText != null)
        {
            _ammoText.gameObject.SetActive(false);
        }

    }

    public override void OnStopNetwork()
    {

        _playerNetwork.Nickname.OnChange -= OnNicknameChanged;
        _playerNetwork.HP.OnChange -= OnHpChanged;
        _playerNetwork.Ammo.OnChange -= OnAmmoChanged;
        _playerNetwork.RespawnTime.OnChange -= OnRespawnTimeChanged;
    }

    private void OnNicknameChanged(string oldValue, string newValue, bool asServer)
    {
        _nicknameText.text = newValue;
    }

    private void OnHpChanged(int oldValue, int newValue, bool asServer)
    {
        _hpText.text = $"HP: {newValue}";
    }
    private void OnAmmoChanged(int oldValue, int newValue, bool asServer)
    {
        if (base.Owner.IsLocalClient && _ammoText != null)
        {
            _ammoText.text = $"Ammo: {newValue}";
        }
    }

    private void AdjustUIToOwner()
    {
        if (base.Owner.IsLocalClient)
        {
            _uiCanvas.localRotation = Quaternion.Euler(0, 0, 0);
        }
        else
        {
            _uiCanvas.localRotation = Quaternion.identity;
        }
    }

    private void OnRespawnTimeChanged(float oldValue, float newValue, bool asServer)
    {
        if (base.IsOwner)
        {
            if (newValue > 0)
            {
                if (_respawnPanel != null)
                    _respawnPanel.SetActive(true);
                if (_mainPanel != null)
                    _mainPanel.SetActive(false);

                if (_respawnTimerText != null)
                {
                    int seconds = Mathf.CeilToInt(newValue);
                    _respawnTimerText.text = $"Respawn: {seconds}s";
                }
            }
            else
            {
                if (_respawnPanel != null)
                    _respawnPanel.SetActive(false);
                if (_mainPanel != null)
                    _mainPanel.SetActive(true);
            }
        }
        else 
        {
            if (newValue > 0)
            {
                if (_mainPanel != null)
                    _mainPanel.SetActive(false);
                if (_respawnPanel != null)
                    _respawnPanel.SetActive(false);
            }
            else
            {
                if (_mainPanel != null)
                    _mainPanel.SetActive(true);
            }
        }
    }
}