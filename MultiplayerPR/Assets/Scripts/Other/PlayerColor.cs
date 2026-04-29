using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

public class PlayerColor : NetworkBehaviour
{
    [SerializeField] private MeshRenderer _meshRenderer;
    
    private readonly SyncVar<Color> _playerColor = new SyncVar<Color>();

    public override void OnStartNetwork()
    {
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        if (base.IsServerInitialized)
        {
            _playerColor.Value = new Color(
                Random.Range(0f, 1f),
                Random.Range(0f, 1f),
                Random.Range(0f, 1f)
            );
        }
        
        _playerColor.OnChange += OnColorChanged;
        
        if (_playerColor.Value != default)
        {
            ApplyColor(_playerColor.Value);
        }
    }

    public override void OnStopNetwork()
    {
        _playerColor.OnChange -= OnColorChanged;
    }

    private void OnColorChanged(Color oldValue, Color newValue, bool asServer)
    {
        ApplyColor(newValue);
    }

    private void ApplyColor(Color color)
    {
        if (_meshRenderer != null)
        {
            _meshRenderer.material.color = color;
        }
    }
}