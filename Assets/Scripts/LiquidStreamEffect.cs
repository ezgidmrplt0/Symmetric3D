using UnityEngine;
using System.Collections;

/// <summary>
/// Şişeden şişeye akan son derece gerçekçi, kavisli 3D sıvı akıntısı efekti.
/// Şişe ağzına (spout lip) dinamik olarak kilitlenir; yerçekimi etkisiyle bükülen Bézier
/// akış eğrisi ve silindirik sıvı shader'ı ile akıcı bir şelale akıntısı oluşturur.
/// </summary>
public class LiquidStreamEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Material streamMaterial;
    private static Shader streamShader;

    private Transform sourceTransform;
    private Vector3 localSpoutOffset;
    private Transform targetTransform;
    private Vector3 localTargetInside;

    private Vector3 fallbackStart;
    private Vector3 fallbackEnd;
    private bool useDynamicTransforms = false;

    private Color liquidColor;
    private float streamDuration;

    public System.Func<Vector3> dynamicStartPosition;
    public System.Func<Vector3> dynamicEndPosition;

    private const int POINT_COUNT = 24;
    private Vector3[] bezierPoints = new Vector3[POINT_COUNT];

    public static LiquidStreamEffect CreateStream(
        Transform source, Vector3 spoutOffset,
        Transform target, Vector3 targetInsideOffset,
        Color color, float duration)
    {
        GameObject go = new GameObject("LiquidStreamEffect");
        LiquidStreamEffect stream = go.AddComponent<LiquidStreamEffect>();
        stream.InitDynamic(source, spoutOffset, target, targetInsideOffset, color, duration);
        return stream;
    }

    public static LiquidStreamEffect CreateStream(Vector3 fromSpout, Vector3 toInside, Color color, float duration)
    {
        GameObject go = new GameObject("LiquidStreamEffect");
        LiquidStreamEffect stream = go.AddComponent<LiquidStreamEffect>();
        stream.InitStatic(fromSpout, toInside, color, duration);
        return stream;
    }

    private void InitDynamic(
        Transform source, Vector3 spoutOffset,
        Transform target, Vector3 targetInsideOffset,
        Color color, float duration)
    {
        sourceTransform = source;
        localSpoutOffset = spoutOffset;
        targetTransform = target;
        localTargetInside = targetInsideOffset;
        useDynamicTransforms = true;

        fallbackStart = sourceTransform != null ? sourceTransform.TransformPoint(localSpoutOffset) : Vector3.zero;
        fallbackEnd = targetTransform != null ? targetTransform.TransformPoint(localTargetInside) : Vector3.zero;

        SetupRenderer(color, duration);
    }

    private void InitStatic(Vector3 fromSpout, Vector3 toInside, Color color, float duration)
    {
        fallbackStart = fromSpout;
        fallbackEnd = toInside;
        useDynamicTransforms = false;

        SetupRenderer(color, duration);
    }

    private void SetupRenderer(Color color, float duration)
    {
        liquidColor = color;
        streamDuration = duration;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = POINT_COUNT;
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.textureMode = LineTextureMode.Stretch;

        // Gerçekçi akışkan incelme profili:
        // Ağızda geniş başlar, yerçekimi ivmesiyle incelir, havuza çarpınca hafif yayılır
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0.0f, 0.110f);
        widthCurve.AddKey(0.18f, 0.085f);
        widthCurve.AddKey(0.70f, 0.068f);
        widthCurve.AddKey(1.0f, 0.082f);
        lineRenderer.widthCurve = widthCurve;

        if (streamShader == null) streamShader = Shader.Find("Custom/LiquidStream");
        if (streamShader == null) streamShader = Shader.Find("Sprites/Default");

        streamMaterial = new Material(streamShader);
        streamMaterial.SetColor("_Color", color);
        if (streamMaterial.HasProperty("_InnerGlow")) streamMaterial.SetFloat("_InnerGlow", 1.25f);
        if (streamMaterial.HasProperty("_FlowSpeed")) streamMaterial.SetFloat("_FlowSpeed", 4.2f);
        if (streamMaterial.HasProperty("_SpecularStrength")) streamMaterial.SetFloat("_SpecularStrength", 0.5f);

        lineRenderer.material = streamMaterial;

        StartCoroutine(AnimateStreamSequence());
    }

    private Vector3 GetCurrentStartPoint()
    {
        if (dynamicStartPosition != null)
        {
            return dynamicStartPosition();
        }
        if (useDynamicTransforms && sourceTransform != null)
        {
            fallbackStart = sourceTransform.TransformPoint(localSpoutOffset);
        }
        return fallbackStart;
    }

    private Vector3 GetCurrentEndPoint()
    {
        if (dynamicEndPosition != null)
        {
            return dynamicEndPosition();
        }
        if (useDynamicTransforms && targetTransform != null)
        {
            fallbackEnd = targetTransform.TransformPoint(localTargetInside);
        }
        return fallbackEnd;
    }

    private IEnumerator AnimateStreamSequence()
    {
        float elapsed = 0f;
        float buildUpDuration = 0.09f;

        // 1. Akıntı Başlangıcı: Şişe ağzından fışkırıp yerçekimiyle hedef şişeye doğru uzayan şelale ucu
        while (elapsed < buildUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / buildUpDuration);

            Vector3 p0 = GetCurrentStartPoint();
            Vector3 p3 = GetCurrentEndPoint();
            ComputeBezierArc(p0, p3);

            int visibleCount = Mathf.Max(2, Mathf.RoundToInt(t * POINT_COUNT));
            lineRenderer.positionCount = visibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                lineRenderer.SetPosition(i, bezierPoints[i]);
            }

            yield return null;
        }

        lineRenderer.positionCount = POINT_COUNT;

        // 2. Ana Akış Evresi: Kesintisiz akan dolgun, parlak sıvı şelalesi
        float mainFlowDuration = Mathf.Max(0.05f, streamDuration - 0.18f);
        float mainElapsed = 0f;
        while (mainElapsed < mainFlowDuration)
        {
            mainElapsed += Time.deltaTime;

            Vector3 p0 = GetCurrentStartPoint();
            Vector3 p3 = GetCurrentEndPoint();
            ComputeBezierArc(p0, p3);

            for (int i = 0; i < POINT_COUNT; i++)
            {
                lineRenderer.SetPosition(i, bezierPoints[i]);
            }

            yield return null;
        }

        // 3. Akıntı Bitişi: Akıntı şişe ağzından kopup hedef şişenin içine dökülerek sönümlenir
        float fadeDuration = 0.11f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            Vector3 p0 = GetCurrentStartPoint();
            Vector3 p3 = GetCurrentEndPoint();
            ComputeBezierArc(p0, p3);

            int startIndex = Mathf.Clamp(Mathf.RoundToInt(t * (POINT_COUNT - 1)), 0, POINT_COUNT - 2);
            int remaining = POINT_COUNT - startIndex;
            lineRenderer.positionCount = remaining;

            for (int i = 0; i < remaining; i++)
            {
                lineRenderer.SetPosition(i, bezierPoints[startIndex + i]);
            }

            // Gittikçe incelerek kaybol
            if (lineRenderer != null)
            {
                float baseScale = (1.0f - t);
                AnimationCurve fadeCurve = new AnimationCurve();
                fadeCurve.AddKey(0.0f, 0.115f * baseScale);
                fadeCurve.AddKey(0.70f, 0.070f * baseScale);
                fadeCurve.AddKey(1.0f, 0.085f * baseScale);
                lineRenderer.widthCurve = fadeCurve;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Şişe ağzından dökülen sıvının yerçekimi altındaki doğal parabolik / kavisli Bézier eğrisini hesaplar.
    /// </summary>
    private void ComputeBezierArc(Vector3 p0, Vector3 p3)
    {
        Vector3 delta = p3 - p0;
        float horizontalDist = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);

        // Kontrol Noktası 1: Şişe ağzından çıkış yayı (hafifçe ileri ve yerçekimi başlangıcı)
        Vector3 p1 = p0 + new Vector3(delta.x * 0.38f, -0.04f, delta.z * 0.38f);

        // Kontrol Noktası 2: Hedef şişe ağzına dik giriş yayı
        Vector3 p2 = new Vector3(p3.x, Mathf.Max(p3.y + 0.20f, p0.y - 0.25f), p3.z);

        // 4 noktalı Cubic Bézier hesaplaması
        for (int i = 0; i < POINT_COUNT; i++)
        {
            float t = (float)i / (POINT_COUNT - 1);
            float u = 1.0f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0 + 3.0f * uu * t * p1 + 3.0f * u * tt * p2 + ttt * p3;
            bezierPoints[i] = point;
        }
    }

    private void OnDestroy()
    {
        if (streamMaterial != null) Destroy(streamMaterial);
    }
}
