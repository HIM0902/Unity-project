using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Implements mouselook. Horizontal mouse movement rotates the body
// around the y-axis, while vertical mouse movement rotates the head
// around the x-axis.
public class MouseLook : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] float turnSpeed = 90f;
    [SerializeField] float headUpperAngleLimit = 85f;
    [SerializeField] float headLowerAngleLimit = -80f;

    [Header("Head Bob")]
    [SerializeField] bool useHeadBob = true;
    [SerializeField] float walkBobSpeed = 10f;
    [SerializeField] float runBobSpeed = 14f;
    [SerializeField] float walkBobAmount = 0.05f;
    [SerializeField] float runBobAmount = 0.08f;
    [SerializeField] float horizontalBobAmount = 0.03f;
    [SerializeField] float bobReturnSpeed = 8f;

    [Header("Jump/Land Bob")]
    [SerializeField] float landingBobAmount = 0.08f;
    [SerializeField] float landingBobSpeed = 10f;

    [Header("Accuracy")]
    [SerializeField] float movingAimPenalty = 1.5f;
    [SerializeField] float runningAimPenalty = 3f;
    [SerializeField] float airborneAimPenalty = 4f;

    float yaw = 0f;
    float pitch = 0f;

    Quaternion bodyStartOrientation;
    Quaternion headStartOrientation;

    Transform head;
    Vector3 headStartLocalPosition;
    Movement movement;

    float bobTimer = 0f;
    float landingOffset = 0f;
    bool wasGrounded = true;

    public float CurrentAimPenalty { get; private set; }

    void Start()
    {
        head = GetComponentInChildren<Camera>().transform;
        movement = GetComponent<Movement>();

        bodyStartOrientation = transform.localRotation;
        headStartOrientation = head.localRotation;
        headStartLocalPosition = head.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (movement != null)
        {
            wasGrounded = movement.IsGrounded;
        }
    }

    void Update()
    {
        UpdateLook();
        UpdateHeadBob();
        UpdateAimPenalty();
    }

    void UpdateLook()
    {
        var horizontal = Input.GetAxis("Mouse X") * Time.deltaTime * turnSpeed;
        var vertical = Input.GetAxis("Mouse Y") * Time.deltaTime * turnSpeed;

        yaw += horizontal;
        pitch -= vertical;
        pitch = Mathf.Clamp(pitch, headLowerAngleLimit, headUpperAngleLimit);

        var bodyRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        var headRotation = Quaternion.AngleAxis(pitch, Vector3.right);

        transform.localRotation = bodyRotation * bodyStartOrientation;
        head.localRotation = headRotation * headStartOrientation;
    }

    void UpdateHeadBob()
    {
        if (!useHeadBob || movement == null)
        {
            return;
        }

        // Start from the default local position...
        Vector3 targetPosition = headStartLocalPosition;

        // ...but use Movement's crouch-adjusted base head Y.
        // This prevents crouch and headbob from fighting each other.
        targetPosition.y = movement.CurrentHeadBaseY;

        if (movement.IsGrounded && movement.IsMoving)
        {
            float bobSpeed = movement.IsRunning ? runBobSpeed : walkBobSpeed;
            float bobAmount = movement.IsRunning ? runBobAmount : walkBobAmount;

            bobTimer += Time.deltaTime * bobSpeed;

            float verticalBob = Mathf.Sin(bobTimer) * bobAmount;
            float horizontalBob = Mathf.Cos(bobTimer * 0.5f) * horizontalBobAmount;

            targetPosition.x += horizontalBob;
            targetPosition.y += verticalBob;
        }
        else
        {
            bobTimer = 0f;
        }

        if (!wasGrounded && movement.IsGrounded)
        {
            landingOffset = landingBobAmount;
        }

        wasGrounded = movement.IsGrounded;

        landingOffset = Mathf.Lerp(landingOffset, 0f, Time.deltaTime * landingBobSpeed);
        targetPosition.y -= landingOffset;

        head.localPosition = Vector3.Lerp(head.localPosition, targetPosition, Time.deltaTime * bobReturnSpeed);
    }

    void UpdateAimPenalty()
    {
        if (movement == null)
        {
            CurrentAimPenalty = 0f;
            return;
        }

        if (!movement.IsGrounded)
        {
            CurrentAimPenalty = airborneAimPenalty;
            return;
        }

        if (movement.IsRunning && movement.IsMoving)
        {
            CurrentAimPenalty = runningAimPenalty;
            return;
        }

        if (movement.IsMoving)
        {
            CurrentAimPenalty = movingAimPenalty;
            return;
        }

        CurrentAimPenalty = 0f;
    }
}