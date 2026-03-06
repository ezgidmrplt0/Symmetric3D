using UnityEngine;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    public Color liquidColor = Color.white;
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;
    public float maxAdjacencyDistance = 2.5f;

    [HideInInspector]
    public bool transferring = false;

    void Start()
    {
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
        if (transferring) return;

        LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();

        foreach (LiquidTransfer other in allLiquids)
        {
            if (other == this || other.transferring) continue;

            // Renkler aynı mı kontrolü
            if (other.liquidColor != this.liquidColor) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist < maxAdjacencyDistance && dist > 0.1f)
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

    void StartTransfer(LiquidTransfer target)
    {
        transferring = true;
        target.transferring = true;

        // Hedef dolacak (0.5), Verici boşalacak (-0.5)
        DOTween.To(() => target.fillAmount, x => target.fillAmount = x, 0.5f, transferDuration)
            .OnUpdate(() => { if (target.liquidMat != null) target.liquidMat.SetFloat("_FillAmount", target.fillAmount); });

        DOTween.To(() => fillAmount, x => fillAmount = x, -0.5f, transferDuration)
            .OnUpdate(() => { if (liquidMat != null) liquidMat.SetFloat("_FillAmount", fillAmount); })
            .OnComplete(() =>
            {
                // Animasyon bitince ikisi de silinir
                transform.parent.DOScale(0, 0.2f).OnComplete(() => Destroy(transform.parent.gameObject));
                target.transform.parent.DOScale(0, 0.2f).OnComplete(() => Destroy(target.transform.parent.gameObject));
            });
    }
}