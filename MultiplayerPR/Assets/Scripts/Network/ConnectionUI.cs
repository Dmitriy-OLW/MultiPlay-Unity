using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat; // Добавляем для Tugboat
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private TMP_InputField _serverAddressInput;
    [SerializeField] private GameObject _menuPanel;

    public static string PlayerNickname { get; private set; } = "Player";

    private readonly string _defaultServerAddress = "172.25.160.1";

    private void Start()
    {
        if (_serverAddressInput != null)
            _serverAddressInput.text = _defaultServerAddress;
    }

    public void StartAsClient()
    {
        SaveNickname();

        string serverAddress = _serverAddressInput != null ? _serverAddressInput.text : _defaultServerAddress;
        if (string.IsNullOrWhiteSpace(serverAddress))
            serverAddress = _defaultServerAddress;

        // Получаем NetworkManager
        NetworkManager networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        // Устанавливаем адрес для клиента
        Transport transport = networkManager.TransportManager.Transport;
        if (transport is Tugboat tugboat)
        {
            tugboat.SetClientAddress(serverAddress);
            Debug.Log($"Client address set to: {serverAddress}");
        }
        else
        {
            Debug.LogError($"Transport is {transport.GetType().Name}, not Tugboat. Cannot set client address.");
            return;
        }

        networkManager.ClientManager.StartConnection();
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