using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float gravity = 9.81f;
    
    [Header("Field Boundaries")]
    [SerializeField] private float minX = -12f;
    [SerializeField] private float maxX = 12f;
    [SerializeField] private float minZ = -8f;
    [SerializeField] private float maxZ = 8f;
    
    [Header("References")]
    private Animator animator;
    private CharacterController controller;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement();
    }
    
    private void LateUpdate()
    {
        Vector3 currentPos = transform.position;

        currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
        currentPos.z = Mathf.Clamp(currentPos.z, minZ, maxZ);

        transform.position = currentPos;
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            Vector3 moveVelocity = direction * moveSpeed;
            if (!controller.isGrounded)
            {
                moveVelocity.y -= gravity;
            }
            controller.Move(moveVelocity * Time.deltaTime);

            animator.SetFloat(SpeedHash, direction.magnitude);
        }
        else
        {
            if (!controller.isGrounded)
            {
                controller.Move(new Vector3(0f, -gravity * Time.deltaTime, 0f));
            }

            animator.SetFloat(SpeedHash, 0f);
        }
    }
    
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic) return;

        if (hit.moveDirection.y < -0.3f) return;

        Vector3 playerHorizontalVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);

        body.AddForceAtPosition(playerHorizontalVelocity * body.mass, hit.point, ForceMode.Force);
    }
}
