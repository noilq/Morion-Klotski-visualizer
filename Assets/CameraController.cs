using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Rotation")]
    public float rotationSpeed = 0.3f;
    public bool invertY = true;

    [Header("Zoom")]
    public float distance = 10f;
    public float minDistance = 2f;
    public float maxDistance = 50f;
    public float zoomSpeed = 5f;
    public float zoomSmoothTime = 0.15f;

    private Quaternion orbitRotation;
    private float targetDistance;
    private float zoomVelocity;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target not assigned");
            enabled = false;
            return;
        }

        Vector3 dir = (transform.position - target.position).normalized;
        distance = Vector3.Distance(transform.position, target.position);
        targetDistance = distance;

        orbitRotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * (invertY ? 1f : -1f);

            Quaternion yaw = Quaternion.AngleAxis(mouseX, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(mouseY, transform.right);

            orbitRotation = yaw * pitch * orbitRotation;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        distance = Mathf.SmoothDamp(
            distance,
            targetDistance,
            ref zoomVelocity,
            zoomSmoothTime
        );

        Vector3 offset = orbitRotation * Vector3.forward * distance;
        transform.position = target.position + offset;

        transform.rotation = Quaternion.LookRotation(-offset, orbitRotation * Vector3.up);
    }
}
