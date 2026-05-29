using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Collections;

namespace Multi.PR1
{
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private TMP_InputField _ipAddressInput;
        [SerializeField] private TMP_InputField _playerCountInput;
        [SerializeField] private GameObject _lobbyPanel;

        public static string PlayerNickname { get; private set; } = "Player";
        
        private void Start()
        {
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(true);
                
            if (_ipAddressInput != null && string.IsNullOrEmpty(_ipAddressInput.text))
                _ipAddressInput.text = "127.0.0.1";
                
            if (_playerCountInput != null && string.IsNullOrEmpty(_playerCountInput.text))
                _playerCountInput.text = "2";
                
            // Включаем курсор
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        public void StartAsHost()
        {
            SaveNickname();
            int playerCount = GetPlayerCount();
            
            Debug.Log($"[ConnectionUI] Starting as Host with player count: {playerCount}");
            
            // Сначала запускаем хост
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", 7777);
            }
            
            NetworkManager.Singleton.StartHost();
            
            // Ждём один кадр, чтобы NetworkManager инициализировался, затем отправляем количество игроков
            StartCoroutine(SetPlayerCountAfterStart(playerCount));
            
            // Скрываем лобби сразу при старте
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(false);
            
            Debug.Log($"Started as Host with nickname: {PlayerNickname}, will wait for {playerCount} players");
        }
        
        private IEnumerator SetPlayerCountAfterStart(int playerCount)
        {
            // Ждём, пока NetworkManager полностью инициализируется
            yield return null;
            yield return null;
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetTargetPlayers(playerCount);
                Debug.Log($"[ConnectionUI] Set target players to {playerCount} via direct call");
            }
            else
            {
                Debug.LogError("[ConnectionUI] GameManager.Instance is null!");
            }
        }

        public void StartAsClient()
        {
            SaveNickname();
            string ipAddress = GetIpAddress();
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, 7777);
            }
            
            NetworkManager.Singleton.StartClient();
            
            // Скрываем лобби сразу при старте
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(false);
            
            Debug.Log($"Started as Client with nickname: {PlayerNickname}, connecting to {ipAddress}");
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
                string text = _playerCountInput.text.Trim();
                Debug.Log($"[ConnectionUI] Parsing player count from: '{text}'");
                
                if (int.TryParse(text, out int count))
                {
                    int clamped = Mathf.Max(2, Mathf.Min(10, count));
                    Debug.Log($"[ConnectionUI] Parsed: {count}, Clamped: {clamped}");
                    return clamped;
                }
                else
                {
                    Debug.LogWarning($"[ConnectionUI] Failed to parse '{text}', using default 2");
                }
            }
            return 2;
        }
    }
}