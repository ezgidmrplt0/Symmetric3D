using UnityEngine;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    // _FillAmount shader'ında 0 = yarım dolu, 0.5 = tam dolu, -0.5 = tamamen boş
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;

    // Objenin merkezinden merkeze mesafe (grid boyutunuza göre)
    public float maxAdjacencyDistance = 2.5f;

    [HideInInspector]
    public bool transferring = false;

    void Start()
    {
        // Ne olursa olsun oyun başlarken her kürenin sıvı miktarı 0 (tam ortadan yarım) olsun
        fillAmount = 0f;

        if (liquidMat != null)
        {
            // Bütün sıvılar projeden aynı materyal asset'ini kullandığı için
            // birisi dolduğunda diğeri de dolmuş / renk değiştirmiş gibi olabiliyor.
            // Bunu çözmek için materyali kopyalıyoruz (Instance yaratıyoruz).
            liquidMat = new Material(liquidMat);
            liquidMat.SetFloat("_FillAmount", fillAmount);

            // Bu kopyalanan materyali, objenin üzerindeki sıvı meshine giydiriyoruz.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material[] sharedMats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    // "Custom/LiquidFullControl" shader'ını kullanan materyali bul ve yenisiyle değiştir
                    if (sharedMats[i] != null && sharedMats[i].shader.name == "Custom/LiquidFullControl")
                    {
                        sharedMats[i] = liquidMat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = sharedMats;
            }

            // Eğer hareket edince sıvının sallanmasını sağlayan script varsa
            // o da artık herkesin ortak materyalini değil, bu objenin kendi materyalini sallamalı.
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
            if (other == this) continue;
            if (other.transferring) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            // Dip dibeler mi?
            if (dist < maxAdjacencyDistance && dist > 0.1f)
            {
                // Z eksenindeki ufak kaymalar simetriği bozmasın diye Z'yi yoksayıyoruz (2D düzlem)
                Vector3 posMe = new Vector3(transform.position.x, transform.position.y, 0);
                Vector3 posOther = new Vector3(other.transform.position.x, other.transform.position.y, 0);
                
                Vector3 dirToOther = (posOther - posMe).normalized;

                // Shader'da sıvı objenin eksi Y tarafında (-Y) birikiyor. 
                // Bizim bu iki düz yüzeyin öpüşmesini/birleşmesini sağlamamız gerekiyor.
                // Yani düz kesik olan kısımları (+Y yani transform.up) birbirine bakmalı.
                Vector3 myFlatFaceDir = new Vector3(transform.up.x, transform.up.y, 0).normalized; 
                Vector3 otherFlatFaceDir = new Vector3(other.transform.up.x, other.transform.up.y, 0).normalized;

                float dotMe = Vector3.Dot(myFlatFaceDir, dirToOther);
                float dotOther = Vector3.Dot(otherFlatFaceDir, -dirToOther);

                // Düz yüzeyleri birbirine bakıyorsa (0.9'dan büyükse yani yaklaşık 0-25 derece tolerans ile yüz yüzeyse) sıvı aktar
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

        // Hedef sıvının _FillAmount 0'dan (yarım dolu) 0.5f'ye (tam dolu) çıkacak
        DOTween.To(() => target.fillAmount, x => target.fillAmount = x, 0.5f, transferDuration)
            .OnUpdate(() =>
            {
                target.liquidMat.SetFloat("_FillAmount", target.fillAmount);
            });

        // Verici sıvı tamamen boşalacak (-0.5f)
        DOTween.To(() => fillAmount, x => fillAmount = x, -0.5f, transferDuration)
            .OnUpdate(() =>
            {
                if(liquidMat != null) liquidMat.SetFloat("_FillAmount", fillAmount);
            });

        // Verici küre aynı anda küçülüp yok olacak
        transform.DOScale(0, transferDuration)
        .OnComplete(() =>
        {
            Destroy(gameObject);
            
            // Sıvı tamamen aktarıldı, hedef nesnenin transfer kilidini aç
            target.transferring = false;
            
            // Eğer aktarılan obje zincirleme olarak başkasına uyuyorsa patlamaması için kontrol:
            target.CheckSymmetry(); 
        });
    }
}