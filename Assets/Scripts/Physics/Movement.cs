using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Implements character controller movement.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float runSpeed = 6f;
    [SerializeField] float jumpHeight = 2f;
    [SerializeField] float gravity = 20f;
    [Range(0, 10), SerializeField] float airControl = 5f;
    [SerializeField] float minimumAirTimeForLandSound = 0.15f;

    [Header("Crouch")]
    [Tooltip("Hold this key to crouch.")]
    [SerializeField] KeyCode crouchKey = KeyCode.C;

    [Tooltip("Speed while crouching.")]
    [SerializeField] float crouchSpeed = 2f;

    [Tooltip("Standing height of the CharacterController (cached from controller on Start).")]
    [SerializeField] float standingHeight = 2f;

    [Tooltip("Crouching height of the CharacterController.")]
    [SerializeField] float crouchHeight = 1.2f;

    [Tooltip("How quickly the controller changes height/center (bigger = faster).")]
    [SerializeField] float crouchTransitionSpeed = 10f;

    [Header("Head / Camera")]
    [Tooltip("Drag your Head (camera) transform here. If left empty, we try to find a Camera child.")]
    [SerializeField] Transform head;

    [Tooltip("How far the camera should be lower when crouching.")]
    [SerializeField] float crouchHeadOffset = 0.5f;

    [Header("Footsteps")]
    [SerializeField] float stepInterval = 2f;
    [SerializeField] AudioClip[] footstepSounds;
    [SerializeField] AudioClip jumpSound;
    [SerializeField] AudioClip landSound;

    Vector3 moveDirection = Vector3.zero;
    CharacterController controller;
    AudioSource audioSource;

    float stepCycle = 0f;
    float nextStep = 0f;

    bool previouslyGrounded;
    bool jumpPressed;
    float airTime = 0f;

    public bool IsCrouching { get; private set; }
    float standingCenterY;
    float crouchCenterY;

    // IMPORTANT: This is the crouch-adjusted base Y position for the head.
    // MouseLook will read this and add headbob on top.
    public float CurrentHeadBaseY { get; private set; }

    Vector3 headStartLocalPos;

    ZombieAI.SoundEmitter soundEmitter;

    public Vector3 CurrentVelocity { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float MoveAmount { get; private set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        soundEmitter = GetComponent<ZombieAI.SoundEmitter>();

        if (head == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                head = cam.transform;
        }

        if (head != null)
        {
            headStartLocalPos = head.localPosition;
            CurrentHeadBaseY = headStartLocalPos.y;
        }

        stepCycle = 0f;
        nextStep = stepInterval * 0.5f;
        previouslyGrounded = controller.isGrounded;

        standingHeight = controller.height;
        standingCenterY = controller.center.y;

        crouchCenterY = standingCenterY - (standingHeight - crouchHeight) * 0.5f;
    }

    void Update()
    {
        if (!jumpPressed)
        {
            jumpPressed = Input.GetButtonDown("Jump");
        }

        HandleLandingAndAirSounds();
    }

    void FixedUpdate()
    {
        // 1) Crouch + Run logic
        IsCrouching = Input.GetKey(crouchKey);

        IsRunning = Input.GetKey(KeyCode.LeftShift) && !IsCrouching;

        float currentSpeed;
        if (IsCrouching)
            currentSpeed = crouchSpeed;
        else
            currentSpeed = IsRunning ? runSpeed : walkSpeed;

        if (soundEmitter != null)
            soundEmitter.IsCrouching = IsCrouching;

        // Smoothly change the CharacterController height/center
        float targetHeight = IsCrouching ? crouchHeight : standingHeight;
        float targetCenterY = IsCrouching ? crouchCenterY : standingCenterY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        Vector3 center = controller.center;
        center.y = Mathf.Lerp(center.y, targetCenterY, crouchTransitionSpeed * Time.deltaTime);
        controller.center = center;

        // 2) Input
        var input = new Vector3(
            Input.GetAxis("Horizontal"),
            0,
            Input.GetAxis("Vertical")
        );

        input = Vector3.ClampMagnitude(input, 1f);

        MoveAmount = input.magnitude;
        IsMoving = MoveAmount > 0.1f;

        input *= currentSpeed;
        input = transform.TransformDirection(input);

        // 3) Ground / Air
        if (controller.isGrounded)
        {
            moveDirection = input;

            if (jumpPressed && !IsCrouching)
            {
                moveDirection.y = Mathf.Sqrt(2 * gravity * jumpHeight);
                PlayJumpSound();
                jumpPressed = false;
            }
            else
            {
                moveDirection.y = 0;
            }
        }
        else
        {
            input.y = moveDirection.y;
            moveDirection = Vector3.Lerp(moveDirection, input, airControl * Time.deltaTime);
        }

        moveDirection.y -= gravity * Time.deltaTime;

        controller.Move(moveDirection * Time.deltaTime);

        CurrentVelocity = controller.velocity;

        ProgressStepCycle(currentSpeed);
    }

    // Update the crouch-adjusted base head Y once per rendered frame.
    // MouseLook will read this in Update and apply headbob on top.
    void LateUpdate()
    {
        if (head == null) return;

        float baseY = headStartLocalPos.y;
        if (IsCrouching)
            baseY = headStartLocalPos.y - crouchHeadOffset;

        CurrentHeadBaseY = baseY;
    }

    void HandleLandingAndAirSounds()
    {
        if (!controller.isGrounded)
        {
            airTime += Time.deltaTime;
        }

        if (!previouslyGrounded && controller.isGrounded)
        {
            if (airTime >= minimumAirTimeForLandSound)
            {
                PlayLandSound();
            }

            jumpPressed = false;
            airTime = 0f;
        }

        if (controller.isGrounded && previouslyGrounded)
        {
            airTime = 0f;
        }

        previouslyGrounded = controller.isGrounded;
    }

    void ProgressStepCycle(float speed)
    {
        if (!controller.isGrounded)
            return;

        if (controller.velocity.sqrMagnitude > 0.1f && IsMoving)
        {
            stepCycle += (controller.velocity.magnitude + (speed * (IsRunning ? 1.25f : 1f))) * Time.fixedDeltaTime;
        }

        if (stepCycle > nextStep)
        {
            nextStep = stepCycle + stepInterval;
            PlayFootstepAudio();
        }
    }

    void PlayFootstepAudio()
    {
        if (!controller.isGrounded) return;
        if (footstepSounds == null || footstepSounds.Length == 0) return;

        int soundIndex = Random.Range(0, footstepSounds.Length);

        float volume = IsCrouching ? 0.25f : 1f;
        audioSource.PlayOneShot(footstepSounds[soundIndex], volume);
    }

    void PlayJumpSound()
    {
        if (jumpSound == null) return;
        audioSource.PlayOneShot(jumpSound);
    }

    void PlayLandSound()
    {
        if (landSound == null) return;
        audioSource.PlayOneShot(landSound);
    }
}