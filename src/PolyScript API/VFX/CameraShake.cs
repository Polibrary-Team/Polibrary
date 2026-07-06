using UnityEngine;
public class CameraShake : MonoBehaviour
{
    public CameraShake(IntPtr ptr) : base(ptr) { }

    public float shakeDuration = 0f;
    public float shakeAmount = 0.5f;
    public float decreaseFactor = 1.0f;
    private Vector3 originalPos;
    private bool isShaking = false;

    void LateUpdate()
    {

        if (shakeDuration > 0)
        {
            // If this is the first frame of the shake, capture the position
            if (!isShaking)
            {
                originalPos = transform.localPosition;
                isShaking = true;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, originalPos + UnityEngine.Random.insideUnitSphere * shakeAmount, Time.deltaTime * 3);
            shakeDuration -= Time.deltaTime * decreaseFactor;
        }
        else if (isShaking)
        {
            shakeDuration = 0f;
            transform.localPosition = originalPos;
            isShaking = false;
        }
    }

    public void TriggerShake(float duration, float amount)
    {
        shakeDuration = duration;
        shakeAmount = amount;
    }
}