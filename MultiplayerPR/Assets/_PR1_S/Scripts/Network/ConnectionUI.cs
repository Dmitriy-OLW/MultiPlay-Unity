using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Multi.PR1
{
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private TMP_InputField _ipAddressInput;
        [SerializeField] private TMP_InputField _playerCountInput;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _connectingPanel;
        [SerializeField] private TMP_Text _connectingStatusText;

        public static string PlayerNickname { get; private set; } = "Player";
        
        private bool _isConnecting = false;

        private void Start()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(true);
                
            if (_connectingPanel != null)
                _connectingPanel.SetActive(false);
                
            // Установка IP по умолчанию (локальный хост)
            if (_ipAddressInput != null)
                _ipAddressInput.text = "127.0.0.1";
                
            // Количество игроков по умолчанию
            if (_playerCountInput != null)
                _playerCountInput.text = "2";
        }

        public void StartAsHost()
        {
            if (_isConnecting) return;
            
            SaveNickname();
            int playerCount = GetPlayerCount();
            
            // Устанавливаем количество игроков в GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetTargetPlayersServerRpc(playerCount);
            }
            
            // Настраиваем транспорт для хоста (слушаем все интерфейсы)
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", 7777);
            }
            
            _isConnecting = true;
            ShowConnectingStatus($"Starting host... Waiting for {playerCount} players");
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnHostReady;
            NetworkManager.Singleton.StartHost();
            
            Debug.Log($"Started as Host with nickname: {PlayerNickname}, waiting for {playerCount} players");
        }
        
        private void OnHostReady(ulong clientId)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnHostReady;
            _isConnecting = false;
            HideMenu();
        }

        public void StartAsClient()
        {
            if (_isConnecting) return;
            
            SaveNickname();
            string ipAddress = GetIpAddress();
            
            // Настраиваем транспорт для клиента
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, 7777);
            }
            
            _isConnecting = true;
            ShowConnectingStatus($"Connecting to {ipAddress}...");
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.StartClient();
            
            Debug.Log($"Started as Client with nickname: {PlayerNickname}, connecting to {ipAddress}");
        }
        
        private void OnClientConnected(ulong clientId)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            _isConnecting = false;
            HideMenu();
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (_isConnecting)
            {
                _isConnecting = false;
                ShowConnectingStatus("Connection failed! Check IP address and try again.", true);
                
                // Возвращаемся в меню через 2 секунды
                Invoke(nameof(ResetToMenu), 2f);
            }
        }
        
        private void ResetToMenu()
        {
            if (_connectingPanel != null)
                _connectingPanel.SetActive(false);
            if (_menuPanel != null)
                _menuPanel.SetActive(true);
        }

        public void StartAsServer()
        {
            if (_isConnecting) return;
            
            SaveNickname();
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", 7777);
            }
            
            _isConnecting = true;
            ShowConnectingStatus("Starting server only...");
            
            NetworkManager.Singleton.StartServer();
            _isConnecting = false;
            HideMenu();
            
            Debug.Log("Started as Server only");
        }

        private void SaveNickname()
        {
            string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
            PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
        }
        
        private string GetIpAddress()
        {
            if (_ipAddressInput != null)
            {
                string ip = _ipAddressInput.text.Trim();
                if (string.IsNullOrEmpty(ip))
                    return "127.0.0.1";
                return ip;
            }
            return "127.0.0.1";
        }
        
        private int GetPlayerCount()
        {
            if (_playerCountInput != null)
            {
                if (int.TryParse(_playerCountInput.text, out int count))
                {
                    return Mathf.Max(2, Mathf.Min(10, count));
                }
            }
            return 2;
        }
        
        private void ShowConnectingStatus(string message, bool isError = false)
        {
            if (_connectingPanel != null)
            {
                _connectingPanel.SetActive(true);
                if (_connectingStatusText != null)
                {
                    _connectingStatusText.text = message;
                    _connectingStatusText.color = isError ? Color.red : Color.white;
                }
            }
            
            if (_menuPanel != null)
                _menuPanel.SetActive(false);
        }

        private void HideMenu()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(false);
            if (_connectingPanel != null)
                _connectingPanel.SetActive(false);
        }
    }
}