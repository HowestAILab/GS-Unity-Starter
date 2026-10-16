using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rendert de startscène vanuit de hoofdcamera naar een PNG, om de opbouw te controleren
/// zonder de Editor te openen:
///   Unity.exe -batchmode -projectPath . -executeMethod StarterScreenshot.Batch -quit -screenshot pad.png
/// </summary>
public static class StarterScreenshot
{
    public static void Batch()
    {
        string output = "GSStarter-screenshot.png";
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-screenshot") output = args[i + 1];

        EditorSceneManager.OpenScene("Assets/Scenes/GSStarter.unity");
        var cam = Camera.main;
        if (cam == null) throw new System.Exception("Geen hoofdcamera in de scène");

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        // Een paar keer renderen: de splat-renderer sorteert pas na de eerste frames.
        for (int i = 0; i < 5; i++) cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(output, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Debug.Log($"[GS Starter] Screenshot: {Path.GetFullPath(output)}");
    }

    /// <summary>Laat een bal op de splat vallen en meldt waar hij blijft liggen: een snelle
    /// controle dat de collider over de splat valt (en niet ernaast of gespiegeld).</summary>
    public static void BatchColliderTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GSStarter.unity");
        var splat = GameObject.Find("Zinogre (voorbeeld)");
        var collider = splat.GetComponentInChildren<MeshCollider>();
        var b = collider.bounds;
        Debug.Log($"[GS Starter] Collider wereld-bounds: min {b.min} max {b.max}");

        // Twee ballen: één boven de splat, één boven de lege tafelrand (controle).
        var onSplat = DropBall(new Vector3(b.center.x, b.max.y + 0.5f, b.center.z));
        var onTable = DropBall(new Vector3(0.38f, 1.5f, 1.05f));
        // Physics.simulationMode is a project setting (DynamicsManager.asset): switch it only
        // for this test, and always back. Left on Script, Unity no longer simulates in Play
        // mode and every ball hangs still in the air.
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        float hitHeight = float.NaN;
        try
        {
            Physics.SyncTransforms();
            var startXZ = new Vector2(onSplat.position.x, onSplat.position.z);
            for (int i = 0; i < 200; i++)
            {
                Physics.Simulate(0.01f);
                // Valt de bal vrij, dan blijft hij recht boven zijn startpunt. Wijkt hij opzij af,
                // dan heeft hij iets geraakt: dat is de collider van de splat.
                var xz = new Vector2(onSplat.position.x, onSplat.position.z);
                if (float.IsNaN(hitHeight) && Vector2.Distance(xz, startXZ) > 0.01f) hitHeight = onSplat.position.y;
            }
        }
        finally
        {
            Physics.simulationMode = mode;
        }
        string verdict = !float.IsNaN(hitHeight) && hitHeight > b.min.y
            ? $"OK: geraakt op {hitHeight:F2} m, binnen de collider ({b.min.y:F2}–{b.max.y:F2} m)"
            : "FOUT: de bal viel recht door de splat";
        Debug.Log($"[GS Starter] Collidertest {verdict}; controlebal op de tafel ligt op {onTable.position.y:F2} m (tafelblad 0,75)");
    }

    static Transform DropBall(Vector3 position)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.localScale = Vector3.one * 0.1f;
        ball.transform.position = position;
        ball.AddComponent<Rigidbody>();
        return ball.transform;
    }
}
