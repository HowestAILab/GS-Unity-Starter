using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Gooit ballen om de collider van je splat te testen.
/// In de Editor: linkermuisknop of spatie, vanuit de camera.
/// Op de Quest: de trigger van de rechtercontroller, vanuit je hand.
/// </summary>
public class BallLauncher : MonoBehaviour
{
    public Transform head;
    public Transform rightHand;
    public float speed = 6f;
    public float radius = 0.05f;
    public int maxBalls = 40;
    public Color color = new Color(0.94f, 0.21f, 0.59f); // Howest-roze

    InputAction m_Throw;
    readonly Queue<GameObject> m_Balls = new Queue<GameObject>();

    void OnEnable()
    {
        m_Throw = new InputAction("Gooi", InputActionType.Button);
        m_Throw.AddBinding("<Mouse>/leftButton");
        m_Throw.AddBinding("<Keyboard>/space");
        m_Throw.AddBinding("<XRController>{RightHand}/triggerPressed");
        m_Throw.performed += _ => Throw();
        m_Throw.Enable();
    }

    void OnDisable() => m_Throw?.Disable();

    void Throw()
    {
        // Zonder headset (of zonder getrackte hand) gooien we vanuit de camera.
        bool fromHand = XRSettings.isDeviceActive && rightHand != null && rightHand.localPosition != Vector3.zero;
        Transform from = fromHand ? rightHand : head;
        if (from == null) return;

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Bal";
        ball.transform.SetPositionAndRotation(from.position + from.forward * 0.15f, from.rotation);
        ball.transform.localScale = Vector3.one * radius * 2f;
        ball.GetComponent<Renderer>().material.color = color;

        var body = ball.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = from.forward * speed;

        m_Balls.Enqueue(ball);
        while (m_Balls.Count > maxBalls) Destroy(m_Balls.Dequeue());
    }
}
