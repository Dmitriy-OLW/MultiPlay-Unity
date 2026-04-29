using UnityEngine;
using FishNet.Object;

public class PlayerColor : NetworkBehaviour
{
    public override void OnStartNetwork()
    {
        if (base.Owner.IsLocalClient)
        {
            GetComponent<MeshRenderer>().material.color = Color.green; 
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Color.red; 
        }
    }
}