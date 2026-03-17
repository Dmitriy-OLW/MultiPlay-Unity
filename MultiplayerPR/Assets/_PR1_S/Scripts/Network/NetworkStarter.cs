using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class NetworkStarter : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Host started");
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("Client started");
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("Shutdown");
            }
        }
    }
}