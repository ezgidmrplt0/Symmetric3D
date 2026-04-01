using UnityEngine;

public class LiquidTilt : MonoBehaviour
{
    public Material liquidMat;
    public float tiltAmount = 0.5f;
    public float smoothSpeed = 5f;

    private Vector3 lastPos;
    private float currentTiltX;
    private float currentTiltZ;
    private Renderer _renderer;
    private static MaterialPropertyBlock _propBlock;

    void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        lastPos = transform.parent != null ? transform.parent.position : transform.position;
    }

    void Update()
    {
        Vector3 parentPos = transform.parent != null ? transform.parent.position : transform.position;
        Vector3 velocity = (parentPos - lastPos) / Time.deltaTime;
        lastPos = parentPos;

        float targetTiltX = -velocity.x * tiltAmount;
        float targetTiltZ = -velocity.z * tiltAmount;

        float nextX = Mathf.Lerp(currentTiltX, targetTiltX, Time.deltaTime * smoothSpeed);
        float nextZ = Mathf.Lerp(currentTiltZ, targetTiltZ, Time.deltaTime * smoothSpeed);

        // OPTIMIZATION: Only update property block if there's significant change
        if (Mathf.Abs(nextX - currentTiltX) > 0.001f || Mathf.Abs(nextZ - currentTiltZ) > 0.001f)
        {
            currentTiltX = nextX;
            currentTiltZ = nextZ;

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat("_TiltX", currentTiltX);
                _propBlock.SetFloat("_TiltZ", currentTiltZ);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}