using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    public Color liquidColor = Color.white;
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;
    public float maxAdjacencyDistance = 1.5f; // Sadece 1 grid (yaklaşık 1.4 birim) mesafeye izin verir

    [Header("Dilim (Slice) Ayarları")]
    public int currentSlices = 1;
    public int maxSlices = 4;

    [HideInInspector]
    public bool transferring = false;

    void Start()
    {
        // Başlangıç doluluğunu -0.5 (boş) ile 0.5 (dolu) arasına oranla
        fillAmount = Mathf.Lerp(-0.5f, 0.5f, (float)currentSlices / maxSlices);

        if (liquidMat != null)
        {
            liquidMat = new Material(liquidMat);
            liquidMat.SetFloat("_FillAmount", fillAmount);
            liquidMat.SetColor("_LiquidColor", liquidColor); // Renk ayarla
            liquidMat.SetColor("_ColorA", liquidColor);      // Shader'daki diğer renk property'si

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material[] sharedMats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] != null && sharedMats[i].shader.name == "Custom/LiquidFullControl")
                    {
                        sharedMats[i] = liquidMat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = sharedMats;
            }

            LiquidTilt tiltCode = GetComponent<LiquidTilt>();
            if (tiltCode != null) tiltCode.liquidMat = liquidMat;
        }
    }

    public void CheckSymmetry()
    {
        if (this == null || transferring || currentSlices >= maxSlices) return;

        LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();

        foreach (LiquidTransfer other in allLiquids)
        {
            if (other == this || other == null || other.transferring || other.currentSlices <= 0) continue;

            // Renkler ve aynı zamanda dilim büyüklükleri aynı mı kontrolü
            // (1/4'lük çeyrek sadece 1/4'lük çeyrekle, 1/2'lik yarım sadece 1/2'lik yarımla birleşir)
            if (other.liquidColor != this.liquidColor || other.currentSlices != this.currentSlices) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            float diffX = Mathf.Abs(transform.position.x - other.transform.position.x);
            float diffY = Mathf.Abs(transform.position.y - other.transform.position.y);
            bool isAligned = diffX < 0.1f || diffY < 0.1f;

            if (dist < maxAdjacencyDistance && dist > 0.1f && isAligned)
            {
                Vector3 posMe = new Vector3(transform.position.x, transform.position.y, 0);
                Vector3 posOther = new Vector3(other.transform.position.x, other.transform.position.y, 0);
                Vector3 dirToOther = (posOther - posMe).normalized;

                Vector3 myFlatFaceDir = new Vector3(transform.up.x, transform.up.y, 0).normalized; 
                Vector3 otherFlatFaceDir = new Vector3(other.transform.up.x, other.transform.up.y, 0).normalized;

                float dotMe = Vector3.Dot(myFlatFaceDir, dirToOther);
                float dotOther = Vector3.Dot(otherFlatFaceDir, -dirToOther);

                if (dotMe > 0.9f && dotOther > 0.9f)
                {
                    StartTransfer(other);
                    break;
                }
            }
        }
    }

    void StartTransfer(LiquidTransfer giver)
    {
        transferring = true;
        giver.transferring = true;

        int needed = maxSlices - this.currentSlices;
        int takeAmount = Mathf.Min(needed, giver.currentSlices);

        this.currentSlices += takeAmount;
        giver.currentSlices -= takeAmount;

        float myTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)this.currentSlices / maxSlices);
        float giverTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)giver.currentSlices / maxSlices);

        Sequence seq = DOTween.Sequence();

        seq.Join(DOTween.To(() => giver.fillAmount, x => giver.fillAmount = x, giverTargetFill, transferDuration)
            .OnUpdate(() => { if (giver != null && giver.liquidMat != null) giver.liquidMat.SetFloat("_FillAmount", giver.fillAmount); }));

        seq.Join(DOTween.To(() => this.fillAmount, x => this.fillAmount = x, myTargetFill, transferDuration)
            .OnUpdate(() => { if (this != null && this.liquidMat != null) this.liquidMat.SetFloat("_FillAmount", this.fillAmount); }));

        seq.OnComplete(() =>
        {
            if (giver != null)
            {
                if (giver.currentSlices <= 0)
                {
                    if (giver.transform.parent != null)
                        giver.transform.parent.DOScale(0, 0.2f).OnComplete(() => Destroy(giver.transform.parent.gameObject));
                }
                else
                {
                    giver.transferring = false;
                }
            }

            if (this != null)
            {
                if (this.currentSlices >= maxSlices)
                {
                    // Tamamlandığında objeyi patlat/yok et
                    if (this.transform.parent != null)
                        this.transform.parent.DOScale(0, 0.2f).OnComplete(() => Destroy(this.transform.parent.gameObject));
                }
                else
                {
                    this.transferring = false;
                }
            }
        });
    }
}