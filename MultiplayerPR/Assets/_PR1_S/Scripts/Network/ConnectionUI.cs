using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private GameObject _menuPanel;

        public static string PlayerNickname { get; private set; } = "Player";

        private void Start()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(true);
        }

        public void StartAsHost()
        {
            SaveNickname();
            NetworkManager.Singleton.StartHost();
            HideMenu();
            Debug.Log($"Started as Host with nickname: {PlayerNickname}");
        }

        public void StartAsClient()
        {
            SaveNickname();
            NetworkManager.Singleton.StartClient();
            HideMenu();
            Debug.Log($"Started as Client with nickname: {PlayerNickname}");
        }

        public void StartAsServer()
        {
            SaveNickname();
            NetworkManager.Singleton.StartServer();
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

        /*private void OnGUI()
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 300));

                if (GUILayout.Button("Host"))
                    StartAsHost();

                if (GUILayout.Button("Client"))
                    StartAsClient();

                if (GUILayout.Button("Server"))
                    StartAsServer();

                GUILayout.EndArea();
            }
        }*/
    }
}