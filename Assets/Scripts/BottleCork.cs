using UnityEngine;
using DG.Tweening;

/// <summary>
/// Şişenin dekoratif mantar tıpası. Normalde tamamen gizli — sıvı henüz
/// tamamlanmamış bir şişenin ağzı açık kalmalı. Şişe tamamlandığında
/// (LiquidTransfer.IsComplete() true olunca) belirir ve yukarıdan düşüp
/// oturan bir kapanma animasyonu oynatır.
///
/// Tetikleyen yer: LiquidTransfer.PourInto() içindeki tamamlanma bloğu.
/// </summary>
public class BottleCork : MonoBehaviour
{
    [Tooltip("Kapanma animasyonunun süresi")]
    public float closeDuration = 0.35f;

    [Tooltip("Kapanmadan önce tıpanın düşeceği yükseklik (local offset)")]
    public float dropHeight = 0.35f;

    private Vector3 closedLocalPos;
    private bool hasClosed = false;

    void Awake()
    {
        closedLocalPos = transform.localPosition;
        gameObject.SetActive(false);
    }

    /// <summary>Şişe tamamlandığında çağrılır. Birden fazla çağrıya karşı korumalı.</summary>
    public void PlayCloseAnimation()
    {
        if (hasClosed) return;
        hasClosed = true;

        gameObject.SetActive(true);
        transform.localPosition = closedLocalPos + Vector3.up * dropHeight;
        transform.localScale = Vector3.one * 0.6f;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMove(closedLocalPos, closeDuration).SetEase(Ease.OutBounce));
        seq.Join(transform.DOScale(Vector3.one, closeDuration).SetEase(Ease.OutBack));

        AudioManager.PlayPlace();
    }

    /// <summary>Level yeniden spawn edildiğinde tıpayı başlangıç durumuna döndürür.</summary>
    public void ResetCork()
    {
        hasClosed = false;
        transform.DOKill();
        gameObject.SetActive(false);
        transform.localPosition = closedLocalPos;
        transform.localScale = Vector3.one;
    }
}
