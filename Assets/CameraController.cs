using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    public float rotationSpeed = 0.3f;
    public bool invertY = true;

    public float distance = 10f;
    public float minDistance = 2f;
    public float maxDistance = 50f;
    public float zoomSpeed = 5f;
    public float zoomSmoothTime = 0.15f;

    public float panSpeed = 25f;

    private Quaternion orbitRotation;
    private float targetDistance;
    private float zoomVelocity;

    private Vector3 pivotPosition;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target not assigned");
            enabled = false;
            return;
        }

        pivotPosition = target.position;

        Vector3 dir = (transform.position - target.position).normalized;
        distance = Vector3.Distance(transform.position, target.position);
        targetDistance = distance;

        orbitRotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void LateUpdate()
    {
        HandleRotation();
        HandleZoom();
        HandlePan();

        distance = Mathf.SmoothDamp(
            distance,
            targetDistance,
            ref zoomVelocity,
            zoomSmoothTime
        );

        Vector3 offset = orbitRotation * Vector3.forward * distance;
        transform.position = pivotPosition + offset;
        transform.rotation = Quaternion.LookRotation(-offset, orbitRotation * Vector3.up);
    }

    void HandleRotation()
    {
        if (!Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * (invertY ? 1f : -1f);

        Quaternion yaw = Quaternion.AngleAxis(mouseX, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(mouseY, transform.right);

        orbitRotation = yaw * pitch * orbitRotation;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }

    void HandlePan()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            return;

        Vector3 move =
            (transform.right * h + transform.forward * v) *
            panSpeed *
            Time.deltaTime;

        pivotPosition += move;
    }
}
