using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _gravity = -9.81f;

    private CharacterController _cc;
    private float _verticalVelocity;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!GetComponent<PlayerNetwork>().IsAlive.Value) return;

        if (!base.Owner.IsLocalClient) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0f, v).normalized * _speed;

        _verticalVelocity += _gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        _cc.Move(move * Time.deltaTime);

        if (_cc.isGrounded) _verticalVelocity = 0f;
    }
}
