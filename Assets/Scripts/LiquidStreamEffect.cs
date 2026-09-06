using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Şişeden şişeye akan gerçekçi 3D sıvı akıntısı efekti.
/// LineRenderer ve özel LiquidStream.shader ile akıcı, kavisli iksir akışı oluşturur.
/// </summary>
public class LiquidStreamEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Material streamMaterial;
    private static Shader streamShader;

    private Vector3 startPoint;
    private Vector3 endPoint;
    private Color liquidColor;
    private float streamDuration;

    private int pointCount = 18;

    public static LiquidStreamEffect CreateStream(Vector3 fromSpout, Vector3 toInside, Color color, float duration)
    {
        GameObject go = new GameObject("LiquidStreamEffect");
        LiquidStreamEffect stream = go.AddComponent<LiquidStreamEffect>();
        stream.Init(fromSpout, toInside, color, duration);
        return stream;
    }

    private void Init(Vector3 fromSpout, Vector3 toInside, Color color, float duration)
    {
        startPoint = fromSpout;
        endPoint = toInside;
        liquidColor = color;
        streamDuration = duration;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = pointCount;
        lineRenderer.startWidth = 0.10f;
        lineRenderer.endWidth = 0.06f;

        // Akıntı genişlik eğrisi (Ağızda 0.11f, hedefe doğru 0.065f daralma)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0.0f, 0.11f);
        widthCurve.AddKey(0.3f, 0.09f);
        widthCurve.AddKey(1.0f, 0.065f);
        lineRenderer.widthCurve = widthCurve;

        if (streamShader == null) streamShader = Shader.Find("Custom/LiquidStream");
        if (streamShader == null) streamShader = Shader.Find("Sprites/Default");

        streamMaterial = new Material(streamShader);
        streamMaterial.SetColor("_Color", color);
        if (streamMaterial.HasProperty("_InnerGlow")) streamMaterial.SetFloat("_InnerGlow", 1.8f);
        if (streamMaterial.HasProperty("_FlowSpeed")) streamMaterial.SetFloat("_FlowSpeed", 3.5f);

        lineRenderer.material = streamMaterial;

        StartCoroutine(AnimateStreamSequence());
    }

    private IEnumerator AnimateStreamSequence()
    {
        float elapsed = 0f;
        float buildUpDuration = 0.12f;

        // 1. Akıntı başlar — Şişe ağzından hedef şişeye doğru süzülerek uzar
        while (elapsed < buildUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / buildUpDuration);
            Vector3 currentTip = Vector3.Lerp(startPoint, endPoint, t);
            UpdateCurvePositions(startPoint, currentTip);
            yield return null;
        }

        // 2. Akıntı tam boyda sabit akar
        float mainFlowDuration = Mathf.Max(0.05f, streamDuration - buildUpDuration * 1.5f);
        float mainElapsed = 0f;
        while (mainElapsed < mainFlowDuration)
        {
            mainElapsed += Time.deltaTime;
            UpdateCurvePositions(startPoint, endPoint);
            yield return null;
        }

        // 3. Akıntı biter — Şişe ağzından kopup hedefe doğru çekilerek kaybolur
        float fadeDuration = 0.15f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            Vector3 currentStart = Vector3.Lerp(startPoint, endPoint, t);
            UpdateCurvePositions(currentStart, endPoint);
            if (lineRenderer != null) lineRenderer.startWidth = Mathf.Lerp(0.10f, 0f, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateCurvePositions(Vector3 p0, Vector3 p2)
    {
        if (lineRenderer == null) return;

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            Vector3 point = Vector3.Lerp(p0, p2, t);
            lineRenderer.SetPosition(i, point);
        }
    }

    private void OnDestroy()
    {
        if (streamMaterial != null) Destroy(streamMaterial);
    }
}
