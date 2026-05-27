using Unity.Netcode.Components;
using UnityEngine;

namespace Multi.PR1
{
    /// <summary>
    /// Client-authoritative NetworkTransform для правильной синхронизации позиции
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            // Client authoritative - клиент контролирует свою позицию
            return false;
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[ClientNetworkTransform] Spawned on client {OwnerClientId}, IsOwner: {IsOwner}");
        }
    }
}