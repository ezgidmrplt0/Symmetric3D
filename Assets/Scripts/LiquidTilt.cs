using UnityEngine;

public class LiquidTilt : MonoBehaviour
{
    public Material liquidMat;
    public float tiltAmount = 0.5f;
    public float smoothSpeed = 5f;

    private Vector3 lastPos;
    private float currentTiltX;
    private float currentTiltZ;

    void Start()
    {
        lastPos = transform.parent.position;
    }

    void Update()
    {
        Vector3 velocity = (transform.parent.position - lastPos) / Time.deltaTime;
        lastPos = transform.parent.position;

        float targetTiltX = -velocity.x * tiltAmount;
        float targetTiltZ = -velocity.z * tiltAmount;

        currentTiltX = Mathf.Lerp(currentTiltX, targetTiltX, Time.deltaTime * smoothSpeed);
        currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltZ, Time.deltaTime * smoothSpeed);

        liquidMat.SetFloat("_TiltX", currentTiltX);
        liquidMat.SetFloat("_TiltZ", currentTiltZ);
    }
}