using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Gaussians;
using Gaussians.ThreeD;
using Gaussians.ThreeD.Editor;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

/// <summary>
/// Zet het startproject van de gastles op: projectinstellingen, XR voor de Quest, de URP-feature
/// voor splats, en een scène met een voorbeeldsplat (de Zinogre) en een ballenwerper.
///
/// In de Editor: menu GS Starter. Zonder Editor-venster (batch):
///   Unity.exe -batchmode -projectPath . -executeMethod StarterSetup.BatchSetup -quit -logFile -
/// </summary>
public static class StarterSetup
{
    const string SplatFolder = "Assets/GSStarter/Splats";
    const string ScenePath = "Assets/Scenes/GSStarter.unity";
    const string SampleSplat = "SampleData/zinogre_splat.ply";
    const string SampleCollider = "SampleData/zinogre_collider.obj";

    // Het object staat op een tafel vóór de speler. De splats uit de notebooks kijken naar +Z
    // (straal ~0,5); de camera kijkt ook naar +Z, dus draaien we ze 180° om naar je toe te kijken.
    const float TableHeight = 0.75f;
    static readonly Vector3 TablePosition = new Vector3(0f, 0f, 1.4f);
    static readonly Vector3 SplatPosition = new Vector3(0f, TableHeight + 0.4f, 1.4f);
    static readonly Quaternion SplatRotation = Quaternion.Euler(0f, 180f, 0f);

    static readonly string[] RendererDefaultRefs =
        { "m_ShaderSplats", "m_ShaderComposite", "m_ShaderDebugPoints", "m_ShaderDebugBoxes", "m_CSSplatUtilities" };

    // --- menu -------------------------------------------------------------------------

    [MenuItem("GS Starter/1. Project instellen", priority = 1)]
    public static void SetupProject()
    {
        SetupPlayer();
        AddUrpFeature("Assets/Settings/PC_Renderer.asset");
        AddUrpFeature("Assets/Settings/Mobile_Renderer.asset");
        SetupXR();
        AssetDatabase.SaveAssets();
        Debug.Log("[GS Starter] Project ingesteld. Herstart de Editor als hij nog op DirectX 11 draait.");
    }

    [MenuItem("GS Starter/2. Scène opbouwen (met de Zinogre)", priority = 2)]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Vloer";
        floor.transform.localScale = new Vector3(3f, 1f, 3f); // 30 × 30 m: ballen rollen niet van de wereld

        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Tafel";
        table.transform.localScale = new Vector3(0.9f, TableHeight, 0.9f);
        table.transform.position = TablePosition + Vector3.up * TableHeight / 2f;

        BuildRig();

        var asset = ImportSplat(SampleSplat);
        var collider = ImportCollider(SampleCollider);
        var zinogre = CreateSplatObject("Zinogre (voorbeeld)", asset, collider);
        zinogre.transform.localScale = Vector3.one * 0.8f;

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeGameObject = zinogre;
        Debug.Log($"[GS Starter] Scène bewaard: {ScenePath}");
    }

    [MenuItem("GS Starter/3. Mijn splat importeren…", priority = 20)]
    public static void ImportMySplat()
    {
        string ply = EditorUtility.OpenFilePanel("Kies je splat (*_splat.ply uit notebook 3)", "", "ply");
        if (string.IsNullOrEmpty(ply)) return;
        string obj = EditorUtility.OpenFilePanel("Kies de collider (*_collider.obj), of annuleer", Path.GetDirectoryName(ply), "obj");

        var asset = ImportSplat(ply);
        var collider = string.IsNullOrEmpty(obj) ? null : ImportCollider(obj);
        var go = CreateSplatObject(Path.GetFileNameWithoutExtension(ply), asset, collider);
        Undo.RegisterCreatedObjectUndo(go, "Splat importeren");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("GS Starter/Splat ondersteboven draaien", priority = 40)]
    public static void FlipSelected()
    {
        foreach (var t in Selection.transforms)
        {
            Undo.RecordObject(t, "Splat omdraaien");
            t.rotation = Quaternion.AngleAxis(180f, Vector3.right) * t.rotation;
        }
    }

    /// <summary>Voor de commandolijn: alles in één keer, zonder vensters.</summary>
    public static void BatchSetup()
    {
        SetupProject();
        BuildScene();
        AssetDatabase.SaveAssets();
    }

    // --- projectinstellingen ----------------------------------------------------------

    static void SetupPlayer()
    {
        PlayerSettings.companyName = "Howest AI Lab";
        PlayerSettings.productName = "GS Starter";
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // De splat-plugin werkt niet met DirectX 11 of OpenGL.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });

        // Quest: Vulkan, 64-bit, IL2CPP en minstens Android 12L (API 32).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "be.howest.ailab.gsstarter");
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
    }

    static void AddUrpFeature(string rendererPath)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (data == null)
        {
            Debug.LogWarning($"[GS Starter] Renderer niet gevonden: {rendererPath}");
            return;
        }
        if (data.rendererFeatures.Any(f => f is GaussiansURPFeature)) return;

        var feature = ScriptableObject.CreateInstance<GaussiansURPFeature>();
        feature.name = "Gaussians";
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
        // URP houdt naast de lijst ook de lokale bestands-id's bij; zonder die id verdwijnt
        // de feature bij het herladen van het asset.
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
        {
            var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            (mapField?.GetValue(data) as List<long>)?.Add(localId);
        }
        data.SetDirty();
        EditorUtility.SetDirty(data);
        Debug.Log($"[GS Starter] Gaussians-feature toegevoegd aan {rendererPath}");
    }

    static void SetupXR()
    {
        // XR alleen voor Android (de Quest). Op de laptop blijft Play-modus een gewone camera.
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        var group = BuildTargetGroup.Android;
        if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
        if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
        var general = perTarget.SettingsForBuildTarget(group);
        general.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);

        FeatureHelpers.RefreshFeatures(group);
        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        var wanted = new HashSet<string>
        {
            "MetaQuestFeature",
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
        };
        foreach (var feature in openxr.GetFeatures())
        {
            if (!wanted.Contains(feature.GetType().Name)) continue;
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }
        EditorUtility.SetDirty(openxr);
        EditorUtility.SetDirty(perTarget);
        Debug.Log("[GS Starter] OpenXR ingesteld voor Android (Meta Quest).");
    }

    // --- scène ------------------------------------------------------------------------

    static void BuildRig()
    {
        var originGo = new GameObject("XR Origin");
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originGo.transform, false);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(offset.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f); // ooghoogte zonder headset
        camGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); // licht naar de tafel kijken
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        var head = camGo.AddComponent<TrackedPoseDriver>();
        head.positionInput = new InputActionProperty(new InputAction("Hoofd positie", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        head.rotationInput = new InputActionProperty(new InputAction("Hoofd rotatie", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        camGo.AddComponent<DesktopCamera>();

        var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hand.name = "Rechterhand";
        Object.DestroyImmediate(hand.GetComponent<Collider>());
        hand.transform.SetParent(offset.transform, false);
        hand.transform.localScale = new Vector3(0.04f, 0.04f, 0.12f);
        var handPose = hand.AddComponent<TrackedPoseDriver>();
        handPose.positionInput = new InputActionProperty(new InputAction("Hand positie", binding: "<XRController>{RightHand}/pointerPosition", expectedControlType: "Vector3"));
        handPose.rotationInput = new InputActionProperty(new InputAction("Hand rotatie", binding: "<XRController>{RightHand}/pointerRotation", expectedControlType: "Quaternion"));

        var origin = originGo.AddComponent<XROrigin>();
        origin.Camera = cam;
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        var launcher = originGo.AddComponent<BallLauncher>();
        launcher.head = camGo.transform;
        launcher.rightHand = hand.transform;
    }

    static GameObject CreateSplatObject(string name, GaussianSplat3DAsset asset, Mesh collider)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(SplatPosition, SplatRotation);
        var renderer = root.AddComponent<GaussianSplat3DRenderer>();
        AssignDefaultReferences(renderer, RendererDefaultRefs);
        renderer.m_Asset = asset;

        if (collider != null)
        {
            // Onzichtbaar: alleen voor physics. Zelfde assenstelsel als de splat.
            var child = new GameObject("Collider");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshCollider>().sharedMesh = collider;
        }
        return root;
    }

    /// <summary>De shaders van de splat-renderer zitten in de 'default references' van het script.
    /// Die vult Unity alleen in als je de component via de Inspector toevoegt; hier doen we het zelf.</summary>
    static void AssignDefaultReferences(MonoBehaviour behaviour, IEnumerable<string> fields)
    {
        var script = MonoScript.FromMonoBehaviour(behaviour);
        if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script)) is MonoImporter importer)) return;
        var so = new SerializedObject(behaviour);
        foreach (var field in fields)
        {
            var prop = so.FindProperty(field);
            var value = importer.GetDefaultReference(field);
            if (prop != null && value != null && prop.objectReferenceValue == null) prop.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // --- importeren -------------------------------------------------------------------

    static GaussianSplat3DAsset ImportSplat(string plyPath)
    {
        Directory.CreateDirectory(SplatFolder);
        AssetDatabase.Refresh();
        // Medium: gecomprimeerd (ongeveer 4× kleiner dan lossless), ruim goed genoeg voor de Quest.
        var assets = GaussianSplat3DImporter.Import(Path.GetFullPath(plyPath), SplatFolder,
            GaussianSplat3DImportSettings.Medium, importCameras: false);
        var asset = assets.FirstOrDefault();
        if (asset == null) throw new IOException($"Kon {plyPath} niet importeren");
        Debug.Log($"[GS Starter] Splat geïmporteerd: {AssetDatabase.GetAssetPath(asset)} ({asset.splatCount:N0} Gaussians)");
        return asset;
    }

    /// <summary>
    /// Leest een OBJ in zonder de OBJ-importer van Unity. Die spiegelt de X-as (rechts- naar
    /// linkshandig), terwijl de splat-plugin de coördinaten ongewijzigd overneemt. Met dezelfde
    /// conventie als de splat valt de collider er precies over.
    /// </summary>
    static Mesh ImportCollider(string objPath)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        foreach (var line in File.ReadLines(objPath))
        {
            var parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0] == "v" && parts.Length >= 4)
            {
                vertices.Add(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3])));
            }
            else if (parts[0] == "f" && parts.Length >= 4)
            {
                var idx = parts.Skip(1).Select(p => int.Parse(p.Split('/')[0], CultureInfo.InvariantCulture) - 1).ToArray();
                // Waaiervorm voor veelhoeken. De volgorde blijft: door de spiegeling worden de
                // tegen de klok in gedraaide OBJ-driehoeken precies Unity's voorkant.
                for (int i = 1; i + 1 < idx.Length; i++)
                {
                    triangles.Add(idx[0]);
                    triangles.Add(idx[i]);
                    triangles.Add(idx[i + 1]);
                }
            }
        }

        // Marching cubes levert soms een binnenstebuiten mesh (normalen naar binnen). Dan rolt een
        // bal er gewoon door. Een negatief ondertekend volume verraadt dat: draai alles om.
        double signedVolume = 0;
        for (int t = 0; t < triangles.Count; t += 3)
        {
            Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
            signedVolume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
        }
        if (signedVolume < 0)
        {
            for (int t = 0; t < triangles.Count; t += 3)
                (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            Debug.Log("[GS Starter] Collider stond binnenstebuiten; driehoeken omgedraaid.");
        }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(objPath) };
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Directory.CreateDirectory(SplatFolder);
        string assetPath = $"{SplatFolder}/{mesh.name}.asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
        Debug.Log($"[GS Starter] Collider: {assetPath} ({triangles.Count / 3:N0} driehoeken)");
        return mesh;
    }

    static float Parse(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
