using UnityEngine;

/// <summary>
/// Sıvı kontrolcüsü — Sıvı yüzeyinin her zaman %100 düz ve yatay kalmasını sağlar.
/// İstenmeyen çalkantı, eğrilik veya dalgalanma animasyonlarını engeller.
/// </summary>
public class LiquidTilt : MonoBehaviour
{
    public Material liquidMat;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        ResetTilt();
    }

    /// <summary>
    /// Şişedeki sıvıyı tamamen düz ve yatay konuma sıfırlar.
    /// </summary>
    public void ResetTilt()
    {
        if (_renderer != null)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_TiltX", 0f);
            _propBlock.SetFloat("_TiltZ", 0f);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }

    // Geriye dönük uyumluluk için boş metotlar (sıvıyı eğmez/dalgalandırmaz)
    public void TriggerPickupWobble(float multiplier = 1f) { }
    public void TriggerTransferAgitation(float multiplier = 1f) { }
}