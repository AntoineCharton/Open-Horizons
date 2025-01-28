using UnityEngine;

public class FreeCam : MonoBehaviour
{
    /// <summary>
    /// Normal speed of camera movement.
    /// </summary>
    public float movementSpeed = 10f;

    /// <summary>
    /// Speed of camera movement when shift is held down,
    /// </summary>
    public float fastMovementSpeed = 100f;

    /// <summary>
    /// Sensitivity for free look.
    /// </summary>
    public float freeLookSensitivity = 3f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel.
    /// </summary>
    public float zoomSensitivity = 10f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel (fast mode).
    /// </summary>
    public float fastZoomSensitivity = 50f;
    
    
    public float Sensitivity {
        get { return sensitivity; }
        set { sensitivity = value; }
    }
    [Range(0.1f, 9f)][SerializeField] float sensitivity = 2f;

    Vector2 rotation = Vector2.zero;
    const string xAxis = "Mouse X"; //Strings in direct code generate garbage, storing and re-using them creates no garbage
    const string yAxis = "Mouse Y";
    
    void Update()
    {
        //Rotation
        rotation.x += Input.GetAxis(xAxis) * sensitivity;
        rotation.y += Input.GetAxis(yAxis) * sensitivity;
        var z = 0f;
        if (Input.GetKey(KeyCode.E))
            z = -90 * Time.deltaTime;
        else if(Input.GetKey(KeyCode.Q))
        {
            z = 90 * Time.deltaTime;
        }
        transform.Rotate(new Vector3(-Input.GetAxis(yAxis) * sensitivity,Input.GetAxis(xAxis) * sensitivity,  z), Space.Self);
        
        //Position
        var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            transform.position += (-transform.right * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            transform.position += (transform.right * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            transform.position += (transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            transform.position += (-transform.forward * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.Space))
        {
            transform.position += (transform.up * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            transform.position += (-transform.up * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp))
        {
            transform.position += (Vector3.up * movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown))
        {
            transform.position += (-Vector3.up * movementSpeed * Time.deltaTime);
        }
    }

    void OnDisable()
    {
        StopLooking();
    }

    /// <summary>
    /// Enable free looking.
    /// </summary>
    public void StartLooking()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Disable free looking.
    /// </summary>
    public void StopLooking()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
