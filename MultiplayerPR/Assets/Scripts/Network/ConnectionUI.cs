using TMPro;
using FishNet.Managing;     
using FishNet.Transporting;
using UnityEngine;
using FishNet;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private GameObject _menuPanel;

    public static string PlayerNickname { get; private set; } = "Player";

    public void StartAsHost()
    {
        SaveNickname();

        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();

        HideMenu();
    }

    public void StartAsClient()
    {
        SaveNickname();

        InstanceFinder.ClientManager.StartConnection();

        HideMenu();
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
    }

    private void HideMenu()
    {
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}