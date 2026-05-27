using Unity.Netcode;
using UnityEngine;
using System.Text;

namespace Multi.PR1
{
    public class NetworkDebugger : NetworkBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool _showGUI = true;
        [SerializeField] private Vector2 _guiPosition = new Vector2(10, 10);
        
        private PlayerNetwork _playerNetwork;
        private NetworkCarController _carController;
        private StringBuilder _debugText = new StringBuilder();

        private void Start()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
            _carController = GetComponent<NetworkCarController>();
            
            Debug.Log($"[DEBUGGER] Started on ClientId: {OwnerClientId}, IsOwner: {IsOwner}, IsServer: {IsServer}");
            InvokeRepeating(nameof(LogNetworkState), 1f, 2f);
        }
        
        private void LogNetworkState()
        {
            if (_playerNetwork == null) return;
            
            //Debug.Log($"[DEBUG] Player {OwnerClientId} - Steering: {_playerNetwork.NetworkedSteeringAngle.Value:F1}, Drifting: {_playerNetwork.NetworkedIsDrifting.Value}, RPM: {_playerNetwork.NetworkedWheelRPM.Value:F0}");
            
            if (_carController != null)
            {
                Debug.Log($"[DEBUG] Player {OwnerClientId} - Local Steering: {_carController.GetCurrentSteeringAngle():F1}, Local Drifting: {_carController.isDrifting}");
            }
        }
        
        private void OnGUI()
        {
            if (!_showGUI) return;
            if (_playerNetwork == null) return;
            
            _debugText.Clear();
            _debugText.AppendLine($"=== NETWORK DEBUG ===");
            _debugText.AppendLine($"ClientId: {OwnerClientId}");
            _debugText.AppendLine($"IsOwner: {IsOwner}");
            _debugText.AppendLine($"IsServer: {IsServer}");
            _debugText.AppendLine($"");
            _debugText.AppendLine($"=== SYNC VALUES ===");
            /*_debugText.AppendLine($"Steering: {_playerNetwork.NetworkedSteeringAngle.Value:F1}");
            _debugText.AppendLine($"Drifting: {_playerNetwork.NetworkedIsDrifting.Value}");
            _debugText.AppendLine($"RPM: {_playerNetwork.NetworkedWheelRPM.Value:F0}");*/
            _debugText.AppendLine($"");
            _debugText.AppendLine($"=== LOCAL VALUES ===");
            
            if (_carController != null)
            {
                _debugText.AppendLine($"L Steering: {_carController.GetCurrentSteeringAngle():F1}");
                _debugText.AppendLine($"L Drifting: {_carController.isDrifting}");
                _debugText.AppendLine($"L Speed: {_carController.carSpeed:F1}");
            }
            
            GUI.color = Color.black;
            GUI.Label(new Rect(_guiPosition.x + 2, _guiPosition.y + 2, 400, 200), _debugText.ToString());
            GUI.color = Color.green;
            GUI.Label(new Rect(_guiPosition.x, _guiPosition.y, 400, 200), _debugText.ToString());
        }
    }
}