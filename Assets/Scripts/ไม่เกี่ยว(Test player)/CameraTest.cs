using UnityEngine;

public class CameraTest : MonoBehaviour  // ← ชื่อนี้ต้องตรงกับชื่อไฟล์
{
    public Transform target;
    public float distance  = 5f;
    public float height    = 2f;
    public float sensitivity = 3f;

    private float yaw   = 0f;
    private float pitch = 20f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void LateUpdate()
    {
        yaw   += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch  = Mathf.Clamp(pitch, -40f, 60f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset      = rotation * new Vector3(0f, height, -distance);
        transform.position  = target.position + offset;
        transform.LookAt(target.position + Vector3.up * height);
    }
}