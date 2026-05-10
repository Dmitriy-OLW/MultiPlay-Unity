using FishNet;
using UnityEngine;

public class ServerAutoStart : MonoBehaviour
{
    private void Start()
    {
        // Application.isBatchMode == true, когда Unity запущен без графики (-batchmode).
        if (Application.isBatchMode)
        {
            Debug.Log("[Server] Headless mode detected. Starting server...");
            InstanceFinder.ServerManager.StartConnection();
        }
    }
}