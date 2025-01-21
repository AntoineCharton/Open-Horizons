using System;
using UnityEngine;

public class PhysicsBody : MonoBehaviour
{
    [SerializeField]
    private Transform gravityTarget;
    [SerializeField]
    private Transform cameraAnchor;
    [SerializeField]
    private float power = 15000f;
    [SerializeField]
    private float torq = 5000f;
    [SerializeField]
    private float gravity = 9.81f;
    private Rigidbody rb;
    [Range(0.1f, 9f)][SerializeField] float sensitivity = 2f;
    Vector2 rotation = Vector2.zero;
    const string xAxis = "Mouse X"; //Strings in direct code generate garbage, storing and re-using them creates no garbage
    const string yAxis = "Mouse Y";
    private float yAxisRotation;
    private float forwardSpeed;
    [SerializeField] private Transform forward;
    [SerializeField] private Transform back;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private LayerMask ground;
    [SerializeField] private bool isForwardGrounded;
    [SerializeField] private bool isLeftGrounded;
    [SerializeField] private bool isBackGrounded;
    [SerializeField] private bool isRightGrounded;
    
    void Start()
    {
        yAxisRotation = 0;
        rb = GetComponent<Rigidbody>();
    }
    

    private void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            forwardSpeed = 1;
        }
        else
        {
            forwardSpeed = 0;
        }
        rotation.x += Input.GetAxis(xAxis) * sensitivity;
        rotation.y += Input.GetAxis(yAxis) * sensitivity;
        cameraAnchor.Rotate(new Vector3(-Input.GetAxis(yAxis) * sensitivity,0,  0), Space.Self);
        yAxisRotation += Input.GetAxis(xAxis);
        transform.LookAt(gravityTarget);
        transform.Rotate(new Vector3(-90, 0, 0),  Space.Self);
        transform.Rotate(new Vector3(0, yAxisRotation, 0));
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        processGravity();
    }

    void processGravity()
    {
        Vector3 diff = transform.position - gravityTarget.position;
        var numberOfContacts = 0;
        isForwardGrounded = Physics.CheckSphere(forward.position, 0.2f, ground);
        isBackGrounded = Physics.CheckSphere(back.position, 0.2f, ground);
        isLeftGrounded = Physics.CheckSphere(left.position, 0.2f, ground);
        isRightGrounded = Physics.CheckSphere(right.position, 0.2f, ground);

        if (isForwardGrounded)
            numberOfContacts++;
        if (isBackGrounded)
            numberOfContacts++;
        if (isLeftGrounded)
            numberOfContacts++;
        if (isRightGrounded)
            numberOfContacts++;

        if (numberOfContacts > 2)
        {
            if(rb.linearVelocity.magnitude > 25 && forwardSpeed > 0)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * 25, Time.deltaTime * 5);
            }
        }
        
        if (isBackGrounded && (isLeftGrounded || isRightGrounded))
        {
            if (forwardSpeed == 0 && numberOfContacts == 4)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity,  Vector3.zero, Time.deltaTime * 10);
            }

            rb.AddRelativeForce(new Vector3(0, -0.2f, 1) * forwardSpeed, ForceMode.Impulse);
        }

        rb.AddForce(- diff.normalized * gravity * (rb.mass));
    }
}
