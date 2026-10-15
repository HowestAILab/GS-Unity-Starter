using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

/// <summary>
/// Rondkijken zonder headset: rechtermuisknop ingedrukt houden en de muis bewegen om te
/// kijken, WASD om te lopen, Q en E om te dalen en te stijgen. Met een headset actief doet
/// dit script niets: dan volgt de camera je hoofd.
/// </summary>
public class DesktopCamera : MonoBehaviour
{
    public float speed = 1.5f;
    public float sensitivity = 0.15f;

    float m_Yaw, m_Pitch;

    void Start()
    {
        var euler = transform.eulerAngles;
        m_Yaw = euler.y;
        m_Pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        // Zonder headset heeft de TrackedPoseDriver niets te volgen; laat hem dan de pose niet overschrijven.
        if (!XRSettings.isDeviceActive && TryGetComponent(out TrackedPoseDriver driver))
            driver.enabled = false;
    }

    void Update()
    {
        if (XRSettings.isDeviceActive) return;
        var mouse = Mouse.current;
        var keys = Keyboard.current;
        if (mouse == null || keys == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            m_Yaw += delta.x * sensitivity;
            m_Pitch = Mathf.Clamp(m_Pitch - delta.y * sensitivity, -85f, 85f);
            transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
        }

        Vector3 move = Vector3.zero;
        if (keys.wKey.isPressed) move += Vector3.forward;
        if (keys.sKey.isPressed) move += Vector3.back;
        if (keys.aKey.isPressed) move += Vector3.left;
        if (keys.dKey.isPressed) move += Vector3.right;
        Vector3 flat = Quaternion.Euler(0f, m_Yaw, 0f) * move;
        if (keys.eKey.isPressed) flat += Vector3.up;
        if (keys.qKey.isPressed) flat += Vector3.down;
        transform.position += flat * speed * Time.deltaTime;
    }
}
