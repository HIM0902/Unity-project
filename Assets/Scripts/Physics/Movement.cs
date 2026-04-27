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

    public Vector3 CurrentVelocity { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float MoveAmount { get; private set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        stepCycle = 0f;
        nextStep = stepInterval * 0.5f;
        previouslyGrounded = controller.isGrounded;
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
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        IsRunning = Input.GetKey(KeyCode.LeftShift);

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

        if (controller.isGrounded)
        {
            moveDirection = input;

            if (jumpPressed)
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
        {
            return;
        }

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
        if (!controller.isGrounded)
        {
            return;
        }

        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            return;
        }

        int soundIndex = Random.Range(0, footstepSounds.Length);
        audioSource.PlayOneShot(footstepSounds[soundIndex]);
    }

    void PlayJumpSound()
    {
        if (jumpSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(jumpSound);
    }

    void PlayLandSound()
    {
        if (landSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(landSound);
    }
}