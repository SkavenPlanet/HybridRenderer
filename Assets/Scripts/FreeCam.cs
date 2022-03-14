using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCam : MonoBehaviour
{

    public float camSpeed;
    public int maxSpeedPow = 10;
    public float speedPow = 0;

    Vector2 smoothMouse;

    public float sensitivity = 4;
    public float smoothing = 3;

    public bool zRotateEnabled = true;

    void Update()
    {

        speedPow += Input.mouseScrollDelta.y;

        speedPow = Mathf.Clamp(speedPow, 0, maxSpeedPow);

		camSpeed = Mathf.Exp(speedPow);
		
		float xMovement = Input.GetAxis("Horizontal");
        float yMovement = 0;// -Input.GetAxis("Rot");
        float zMovement = Input.GetAxis("Vertical");
        Vector3 velocity = new Vector3(xMovement, yMovement, zMovement) * camSpeed * Time.deltaTime;

        transform.Translate(velocity);

        float fov = GetComponent<Camera>().fieldOfView;
        float dampedSensitivity = sensitivity / (Mathf.Log(60) / Mathf.Log(fov));
        Vector3 initialEulerAngles = transform.eulerAngles;

        Vector2 mouseDelta = new Vector2(0, 0);

        if (Input.GetMouseButton(1))
        {

            mouseDelta.x = -Input.GetAxis("Mouse Y") * dampedSensitivity * 5;
            mouseDelta.y = Input.GetAxis("Mouse X") * dampedSensitivity * 5;

        }

        smoothMouse.x = Mathf.Lerp(smoothMouse.x, mouseDelta.x, 1f / smoothing);
        smoothMouse.y = Mathf.Lerp(smoothMouse.y, mouseDelta.y, 1f / smoothing);

        Vector3 rotVector = new Vector3(smoothMouse.x, smoothMouse.y, 0);

        transform.Rotate(rotVector);

        if (Input.GetKey(KeyCode.F) && zRotateEnabled)
        {
            transform.Rotate(0, 0, -Input.GetAxis("Mouse X") * dampedSensitivity * 5);
        }

        if (zRotateEnabled == false)
        {
            transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, 0);
        }

    }
}
