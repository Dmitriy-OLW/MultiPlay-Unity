using Unity.Netcode.Components;

namespace Multi.PR1
{
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}