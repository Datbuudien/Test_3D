using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

public static class ExplosionPrefabBuilder
{
    private const string MENU_BUILD = "Tools/Test3D/Create Explosion Prefab";
    private const string VFX_FOLDER = "Assets/_Project/VFX";
    private const string PREFAB_PATH = VFX_FOLDER + "/FX_Explosion.prefab";
    private const string ADDITIVE_MATERIAL_PATH = VFX_FOLDER + "/M_FX_Additive.mat";
    private const string SMOKE_MATERIAL_PATH = VFX_FOLDER + "/M_FX_Smoke.mat";
    private const string DEBRIS_MATERIAL_PATH = VFX_FOLDER + "/M_FX_Debris.mat";
    private const string PARTICLE_SHADER = "Universal Render Pipeline/Particles/Unlit";
    private const string LIT_SHADER = "Universal Render Pipeline/Lit";
    private const string DEFAULT_PARTICLE_TEXTURE = "Default-Particle.psd";
    private const string CUBE_MESH = "Cube.fbx";
    private const string LOG_PREFIX = "[ExplosionPrefabBuilder]";

    private const float SURFACE_TRANSPARENT = 1f;
    private const float BLEND_ALPHA = 0f;
    private const float BLEND_ADDITIVE = 2f;
    private const float AUDIO_MAX_DISTANCE = 80f;
    private const float FLASH_RANGE = 14f;
    private const float IMPULSE_DURATION = 0.45f;
    private const float IMPULSE_STRENGTH = 0.6f;
    private const float IMPULSE_DISSIPATION_DISTANCE = 40f;
    private static readonly Vector3 POINT_UP_ROTATION = new Vector3(-90f, 0f, 0f);
    private static readonly Color FLASH_COLOR = new Color(1f, 0.62f, 0.25f);

    [MenuItem(MENU_BUILD)]
    private static void BuildExplosionPrefab()
    {
        Material additive = CreateParticleMaterial(ADDITIVE_MATERIAL_PATH, BLEND_ADDITIVE);
        Material smoke = CreateParticleMaterial(SMOKE_MATERIAL_PATH, BLEND_ALPHA);
        Material debris = CreateDebrisMaterial();

        GameObject root = new GameObject("FX_Explosion");
        ParticleSystem[] particles =
        {
            CreateFireball(root.transform, additive),
            CreateSmoke(root.transform, smoke),
            CreateDebris(root.transform, debris),
            CreateSparks(root.transform, additive)
        };
        Light flashLight = CreateFlashLight(root.transform);
        AudioSource audioSource = CreateAudioSource(root);
        CinemachineImpulseSource impulseSource = CreateImpulseSource(root);

        Explosion explosion = root.AddComponent<Explosion>();
        SerializedObject serializedExplosion = new SerializedObject(explosion);
        SerializedProperty particleArray = serializedExplosion.FindProperty("particleSystems");
        particleArray.arraySize = particles.Length;
        for (int index = 0; index < particles.Length; index++)
        {
            particleArray.GetArrayElementAtIndex(index).objectReferenceValue = particles[index];
        }

        serializedExplosion.FindProperty("audioSource").objectReferenceValue = audioSource;
        serializedExplosion.FindProperty("flashLight").objectReferenceValue = flashLight;
        serializedExplosion.FindProperty("impulseSource").objectReferenceValue = impulseSource;
        serializedExplosion.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"{LOG_PREFIX} Saved {PREFAB_PATH}. Assign an explosion AudioClip on the Explosion component.");
    }

    private static ParticleSystem CreateFireball(Transform parent, Material material)
    {
        ParticleSystem particle = CreateParticleSystem(parent, "Fireball", material, 0.6f);
        ParticleSystem.MainModule main = particle.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(particle, 35);
        SetShape(particle, ParticleSystemShapeType.Sphere, 0.35f);

        Gradient gradient = CreateGradient(
            new[] { new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.1f), new Color(0.7f, 0.15f, 0.02f), new Color(0.15f, 0.08f, 0.05f) },
            new[] { 1f, 0.9f, 0.5f, 0f });
        SetColorOverLifetime(particle, gradient);
        SetSizeOverLifetime(particle, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
        return particle;
    }

    private static ParticleSystem CreateSmoke(Transform parent, Material material)
    {
        ParticleSystem particle = CreateParticleSystem(parent, "Smoke", material, 1f);
        ParticleSystem.MainModule main = particle.main;
        main.startDelay = 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.06f;
        SetBurst(particle, 22);
        SetShape(particle, ParticleSystemShapeType.Hemisphere, 0.6f);

        Gradient gradient = CreateGradient(
            new[] { new Color(0.25f, 0.23f, 0.22f), new Color(0.45f, 0.43f, 0.42f), new Color(0.55f, 0.55f, 0.55f) },
            new[] { 0.85f, 0.5f, 0f });
        SetColorOverLifetime(particle, gradient);
        SetSizeOverLifetime(particle, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));

        ParticleSystem.RotationOverLifetimeModule rotation = particle.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        return particle;
    }

    private static ParticleSystem CreateDebris(Transform parent, Material material)
    {
        ParticleSystem particle = CreateParticleSystem(parent, "Debris", material, 0.3f);
        ParticleSystem.MainModule main = particle.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.startRotation3D = true;
        main.gravityModifier = 1.2f;
        SetBurst(particle, 28);
        SetShape(particle, ParticleSystemShapeType.Hemisphere, 0.2f);

        ParticleSystem.RotationOverLifetimeModule rotation = particle.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotation.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        ParticleSystem.CollisionModule collision = particle.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.3f;
        collision.dampen = 0.4f;

        ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = Resources.GetBuiltinResource<Mesh>(CUBE_MESH);
        return particle;
    }

    private static ParticleSystem CreateSparks(Transform parent, Material material)
    {
        ParticleSystem particle = CreateParticleSystem(parent, "Sparks", material, 0.3f);
        ParticleSystem.MainModule main = particle.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.gravityModifier = 0.6f;
        SetBurst(particle, 45);
        SetShape(particle, ParticleSystemShapeType.Hemisphere, 0.15f);

        Gradient gradient = CreateGradient(
            new[] { new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.15f) },
            new[] { 1f, 0f });
        SetColorOverLifetime(particle, gradient);

        ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.04f;
        particleRenderer.lengthScale = 1f;
        return particle;
    }

    private static ParticleSystem CreateParticleSystem(Transform parent, string objectName, Material material, float duration)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        ParticleSystem particle = child.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particle.main;
        main.duration = duration;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        return particle;
    }

    private static void SetBurst(ParticleSystem particle, short count)
    {
        ParticleSystem.EmissionModule emission = particle.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    private static void SetShape(ParticleSystem particle, ParticleSystemShapeType shapeType, float radius)
    {
        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = radius;
        // Prefab duoc xoay theo phap tuyen mat dat (truc Y) -> ban ra theo +Y
        shape.rotation = POINT_UP_ROTATION;
    }

    private static void SetColorOverLifetime(ParticleSystem particle, Gradient gradient)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SetSizeOverLifetime(ParticleSystem particle, AnimationCurve curve)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particle.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Gradient CreateGradient(Color[] colors, float[] alphas)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
        for (int index = 0; index < colors.Length; index++)
        {
            colorKeys[index] = new GradientColorKey(colors[index], index / (float)(colors.Length - 1));
        }

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[alphas.Length];
        for (int index = 0; index < alphas.Length; index++)
        {
            alphaKeys[index] = new GradientAlphaKey(alphas[index], index / (float)(alphas.Length - 1));
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private static Light CreateFlashLight(Transform parent)
    {
        GameObject child = new GameObject("Flash");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.up * 0.5f;
        Light flashLight = child.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.range = FLASH_RANGE;
        flashLight.color = FLASH_COLOR;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
        return flashLight;
    }

    private static AudioSource CreateAudioSource(GameObject root)
    {
        AudioSource audioSource = root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = AUDIO_MAX_DISTANCE;
        return audioSource;
    }

    private static CinemachineImpulseSource CreateImpulseSource(GameObject root)
    {
        CinemachineImpulseSource impulseSource = root.AddComponent<CinemachineImpulseSource>();
        impulseSource.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
        impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
        impulseSource.ImpulseDefinition.ImpulseDuration = IMPULSE_DURATION;
        impulseSource.ImpulseDefinition.DissipationDistance = IMPULSE_DISSIPATION_DISTANCE;
        impulseSource.DefaultVelocity = Vector3.down * IMPULSE_STRENGTH;
        return impulseSource;
    }

    internal static Material CreateAdditiveMaterial(string path)
    {
        return CreateParticleMaterial(path, BLEND_ADDITIVE);
    }

    // Mau dac, khong texture: mau lay tu vertex color (LineRenderer gradient) -> net, khong bi chim tren nen sang
    internal static Material CreateSolidMaterial(string path)
    {
        Material material = CreateParticleMaterial(path, BLEND_ALPHA);
        material.SetTexture("_BaseMap", null);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateParticleMaterial(string path, float blendMode)
    {
        Material material = LoadOrCreateMaterial(path, PARTICLE_SHADER);
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>(DEFAULT_PARTICLE_TEXTURE));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", SURFACE_TRANSPARENT);
        material.SetFloat("_Blend", blendMode);
        // Dung dung ham URP goi khi chinh material tren Inspector de dat blend state + keyword
        BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateDebrisMaterial()
    {
        Material material = LoadOrCreateMaterial(DEBRIS_MATERIAL_PATH, LIT_SHADER);
        material.SetColor("_BaseColor", new Color(0.18f, 0.16f, 0.14f));
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find(shaderName));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
