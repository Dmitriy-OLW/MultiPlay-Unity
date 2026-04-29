using FishNet.Managing;
using TMPro;
using UnityEngine;

namespace Multi.PR1.FishNet
{
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private NetworkManager _networkManager;
        
        public static string PlayerNickname { get; private set; } = "Player";
        
        private void Start()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(true);
            
            if (_networkManager == null)
                _networkManager = FindObjectOfType<NetworkManager>();
        }
        
        // Убираем async - StartConnection не асинхронный
        public void StartAsHost()
        {
            SaveNickname();
            
            // Важно: сначала сервер, потом клиент
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
            
            HideMenu();
            Debug.Log($"Started as Host with nickname: {PlayerNickname}");
        }
        
        public void StartAsClient()
        {
            SaveNickname();
            _networkManager.ClientManager.StartConnection();
            HideMenu();
            Debug.Log($"Started as Client with nickname: {PlayerNickname}");
        }
        
        public void StartAsServer()
        {
            SaveNickname();
            _networkManager.ServerManager.StartConnection();
            HideMenu();
            Debug.Log("Started as Server only");
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
        }
    }
}