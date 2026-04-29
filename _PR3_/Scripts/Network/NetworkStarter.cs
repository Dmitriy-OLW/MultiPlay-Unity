using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Multi.FishNet
{
    public class NetworkStarter : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        
        private void Start()
        {
            if (_networkManager == null)
                _networkManager = FindObjectOfType<NetworkManager>();
        }
        
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                _networkManager.ServerManager.StartConnection();
                _networkManager.ClientManager.StartConnection();
                Debug.Log("Host started");
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                _networkManager.ClientManager.StartConnection();
                Debug.Log("Client started");
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                _networkManager.ServerManager.StopConnection(true);
                _networkManager.ClientManager.StopConnection();
                Debug.Log("Shutdown");
            }
        }
    }
}