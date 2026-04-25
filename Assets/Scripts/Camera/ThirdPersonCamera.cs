// ============================================================================
// ThirdPersonCamera.cs
// Smooth third-person follow camera with vertical mouse look and collision.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace HotShipHai.Camera
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The player Transform to follow. Auto-finds by tag 'Player' if empty.")]
        [SerializeField] private Transform target;

        [Tooltip("Offset from the target pivot to the look-at point.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Distance")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 10f;

        [Header("Vertical Look")]
        [Range(0.5f, 10f)]
        [SerializeField] private float verticalSensitivity = 2f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("Smoothing")]
        [Range(1f, 30f)]
        [SerializeField] private float followSmoothing = 10f;
        [Range(1f, 30f)]
        [SerializeField] private float rotationSmoothing = 12f;

        [Header("Collision")]
        [SerializeField] private bool enableCollision = true;
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        private float _currentVerticalAngle = 15f;
        private float _currentDistance;
        private Vector3 _smoothedPosition;

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    Debug.LogError("[ThirdPersonCamera] No target found!");
                    enabled = false;
                    return;
                }
            }
            _currentDistance = distance;
            _smoothedPosition = CalculateDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            HandleVerticalInput();
            UpdateCameraPosition();
        }

        private void HandleVerticalInput()
        {
            if (Mouse.current == null) return;

            float mouseY = Mouse.current.delta.y.ReadValue() * verticalSensitivity * 0.1f;
            _currentVerticalAngle -= mouseY;
            _currentVerticalAngle = Mathf.Clamp(_currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        private void UpdateCameraPosition()
        {
            Vector3 lookAtPoint = target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(_currentVerticalAngle, target.eulerAngles.y, 0f);
            float effectiveDistance = distance;

            if (enableCollision)
            {
                Vector3 direction = -(rotation * Vector3.forward);
                if (Physics.SphereCast(lookAtPoint, collisionRadius, direction, out RaycastHit hit, distance, collisionLayers))
                {
                    effectiveDistance = Mathf.Clamp(hit.distance - collisionRadius, minDistance, distance);
                }
            }

            _currentDistance = Mathf.Lerp(_currentDistance, effectiveDistance, Time.deltaTime * followSmoothing);
            Vector3 desiredPosition = lookAtPoint - (rotation * Vector3.forward * _currentDistance);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, desiredPosition, Time.deltaTime * followSmoothing);
            transform.position = _smoothedPosition;

            Quaternion desiredRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmoothing);
        }

        private Vector3 CalculateDesiredPosition()
        {
            if (target == null) return transform.position;
            Vector3 lookAtPoint = target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(_currentVerticalAngle, target.eulerAngles.y, 0f);
            return lookAtPoint - (rotation * Vector3.forward * distance);
        }
    }
}
