using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Multi.PR1
{
    public class NetworkDebugDisplay : MonoBehaviour
    {
        private List<string> _logs = new List<string>();
        private Vector2 _scrollPosition;
        private bool _showLog = true;

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            string prefix = "[System] ";
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsHost) prefix = "[Host] ";
                else if (NetworkManager.Singleton.IsServer) prefix = "[Server] ";
                else if (NetworkManager.Singleton.IsClient)
                    prefix = $"[Client {NetworkManager.Singleton.LocalClientId}] ";
            }

            _logs.Add(prefix + logString);

            if (_logs.Count > 20)
            {
                _logs.RemoveAt(0);
            }

            _scrollPosition.y = float.MaxValue;
        }

        private void OnGUI()
        {
            if (!_showLog)
            {
                if (GUI.Button(new Rect(Screen.width - 100, 10, 90, 20), "Show Logs")) _showLog = true;
                return;
            }

            float width = 400;
            float height = 300;
            float x = Screen.width - width - 10;
            float y = 10;

            GUILayout.BeginArea(new Rect(x, y, width, height), "Network Logs", GUI.skin.window);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            foreach (var log in _logs)
            {
                GUILayout.Label(log);
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Clear")) _logs.Clear();
            if (GUILayout.Button("Hide")) _showLog = false;

            GUILayout.EndArea();
        }
    }
}