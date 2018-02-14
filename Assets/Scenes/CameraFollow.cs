using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float movementSpeed = 6f;

    Vector2 mouseAbsolute;
    Vector2 smoothMouse;

    public Vector2 clampInDegrees = new Vector2(360, 180);
    public Vector2 sensitivity = new Vector2(2, 2);
    public Vector2 smoothing = new Vector2(3, 3);
    public Vector2 targetDirection;

    void Start()
    {

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;
    }

    void Update()
    {
        /*
         * Moving around.
         */

        transform.position += Input.GetAxis("Horizontal") * transform.right * movementSpeed *
                              Time.deltaTime;
        transform.position += Input.GetAxis("Vertical") * transform.forward * movementSpeed *
                              Time.deltaTime;

        /*
         * Camera direction.
         * Source: https://forum.unity.com/threads/a-free-simple-smooth-mouselook.73117/
         */

        // Allow the script to clamp based on a desired target value.
        var targetOrientation = Quaternion.Euler(targetDirection);

        // Get raw mouse input for a cleaner reading on more sensitive mice.
        var mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        // Scale input against the sensitivity setting and multiply that against the smoothing value.
        mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity.x * smoothing.x,
            sensitivity.y * smoothing.y));

        // Interpolate mouse movement over time to apply smoothing delta.
        smoothMouse.x = Mathf.Lerp(smoothMouse.x, mouseDelta.x, 1f / smoothing.x);
        smoothMouse.y = Mathf.Lerp(smoothMouse.y, mouseDelta.y, 1f / smoothing.y);

        // Find the absolute mouse movement value from point zero.
        mouseAbsolute += smoothMouse;

        // Clamp and apply the local x value first, so as not to be affected by world transforms.
        if (clampInDegrees.x < 360)
            mouseAbsolute.x = Mathf.Clamp(mouseAbsolute.x, -clampInDegrees.x * 0.5f,
                clampInDegrees.x * 0.5f);

        // Then clamp and apply the global y value.
        if (clampInDegrees.y < 360)
            mouseAbsolute.y = Mathf.Clamp(mouseAbsolute.y, -clampInDegrees.y * 0.5f,
                clampInDegrees.y * 0.5f);

        transform.localRotation = Quaternion.AngleAxis(-mouseAbsolute.y,
                                      targetOrientation * Vector3.right) * targetOrientation;

        var yRotation = Quaternion.AngleAxis(mouseAbsolute.x,
            transform.InverseTransformDirection(Vector3.up));
        transform.localRotation *= yRotation;
    }
}
