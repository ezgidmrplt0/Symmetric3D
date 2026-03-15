using UnityEngine;

public class ShapeFaceMarker : MonoBehaviour
{
    public enum FaceSurfaceType
    {
        Rectangle,
        Triangle
    }

    [Header("Face Info")]
    public string faceId = "Face";
    public FaceSurfaceType surfaceType = FaceSurfaceType.Rectangle;

    [Header("Grid Size Defaults")]
    public int defaultGridX = 3;
    public int defaultGridY = 3;

    [Header("Placement")]
    [Tooltip("Grid hücrelerinin yüzey üzerinde aralık hesabında kullanılacak yerel ölçek çarpanı")]
    public Vector2 localFaceScale = Vector2.one;

    [Tooltip("Gridler yüzeyin üstüne biraz çıksın diye normal yönünde offset")]
    public float surfaceOffset = 0.01f;

    [Header("Triangle Options")]
    [Tooltip("Triangle yüz için doldurma yönü")]
    public bool triangleFillFromBottomLeft = true;

    public Vector3 Normal => transform.forward;
    public Vector3 Right => transform.right;
    public Vector3 Up => transform.up;
}
