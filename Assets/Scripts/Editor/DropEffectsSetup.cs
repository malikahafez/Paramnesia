using System;
using UnityEditor;
using UnityEngine;

namespace StealthGame.Editor
{
    /// <summary>
    /// Replaces the collectibles' flat sunray billboards with a real pulsing light, a soft breathing halo,
    /// themed particles (bone dust, toxic bubbles and drips, heart embers) and a pickup burst.
    /// </summary>
    public static class DropEffectsSetup
    {
        const string Folder = "Assets/Collectibles/Prefabs/";
        const string ParticleMaterial = "Assets/Collectibles/Particle_Sunray.mat"; // additive soft dot

        [MenuItem("Tools/Paramnesia/Set Up Drop Effects")]
        public static void Setup()
        {
            var ivory = new Color(1f, 0.86f, 0.62f);
            var toxic = new Color(0.45f, 1f, 0.2f);
            var blood = new Color(1f, 0.12f, 0.16f);
            Build("Collectible_Bone", ivory, 1.4f, 0.5f, false, 0f, (fx, shell) =>
            {
                var dust = Particles(fx, "Bone Dust", ivory, 1f, 14f, 1.6f, 2.6f, 0.03f, 0.06f);
                Sphere(dust, shell); Rise(dust, 0.1f, 0.25f); Noise(dust, 0.15f);
            });
            Build("Collectible_Slime", toxic, 1.8f, 0.8f, false, 0f, (fx, shell) =>
            {
                // Paler than the light so bubbles read against the green pool they sit in.
                var bubbles = Particles(fx, "Toxic Bubbles", new Color(0.75f, 1f, 0.45f), 1f, 16f, 0.8f, 1.5f, 0.04f, 0.09f);
                Sphere(bubbles, shell * 0.8f); Rise(bubbles, 0.3f, 0.55f); Noise(bubbles, 0.06f);
                var swell = bubbles.sizeOverLifetime;
                swell.enabled = true;
                swell.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.3f));
                var drips = Particles(fx, "Toxic Drips", new Color(0.6f, 1f, 0.3f), 1f, 1.5f, 0.7f, 0.9f, 0.035f, 0.05f);
                drips.transform.localPosition = Vector3.down * shell; // under the flask
                Sphere(drips, 0.04f);
                var main = drips.main;
                main.gravityModifier = 0.5f;
            });
            Build("Collectible_Heart", blood, 2.6f, 1.1f, true, 0.12f, (fx, shell) =>
            {
                var embers = Particles(fx, "Blood Embers", new Color(1f, 0.3f, 0.2f), 1f, 18f, 1f, 1.8f, 0.035f, 0.07f);
                Sphere(embers, shell); Rise(embers, 0.3f, 0.6f); Noise(embers, 0.18f);
            });
            AssetDatabase.SaveAssets();
        }

        // theme receives the effects root and the radius of a shell just outside the item, so motes are not hidden inside the mesh.
        static void Build(string name, Color color, float intensity, float pulseSpeed, bool heartbeat, float beatScale, Action<Transform, float> theme)
        {
            string path = Folder + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var previous = root.transform.Find("Drop FX");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                // The sunray billboards live in nested model prefabs, so they are switched off rather than deleted.
                foreach (var old in root.GetComponentsInChildren<ParticleSystem>(true)) old.gameObject.SetActive(false);

                var model = root.GetComponentInChildren<MeshRenderer>();
                var bounds = model.bounds;
                var fx = new GameObject("Drop FX").transform;
                fx.SetParent(root.transform, false);
                fx.position = bounds.center;
                fx.localScale = Vector3.one / root.transform.lossyScale.x; // world-unit effects under the heart's 0.04 root

                // A real light sells the glow: it pools colour on the floor and nearby walls.
                var light = fx.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = 3f;
                light.shadows = LightShadows.None;

                var halo = Particles(fx, "Halo", color, 0.5f, 1.4f, 1.4f, 1.4f, 1.1f, 1.2f);
                var haloMain = halo.main;
                haloMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                haloMain.maxParticles = 4;
                var grow = halo.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f));

                theme(fx, Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.05f);

                var burst = Particles(fx, "Pickup Burst", color, 1f, 0f, 0.35f, 0.7f, 0.03f, 0.08f);
                var burstMain = burst.main;
                burstMain.loop = false;
                burstMain.duration = 0.6f;
                burstMain.playOnAwake = false;
                burstMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                burstMain.gravityModifier = 0.3f;
                burst.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
                Sphere(burst, 0.08f);
                var flash = Particles(burst.transform, "Flash", color, 0.6f, 0f, 0.25f, 0.25f, 1.4f, 1.4f);
                var flashMain = flash.main;
                flashMain.loop = false;
                flashMain.duration = 0.3f;
                flashMain.playOnAwake = false;
                flash.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

                var glow = root.GetComponent<DropGlow>();
                if (glow == null) glow = root.AddComponent<DropGlow>();
                glow.glowLight = light;
                glow.model = model.transform;
                glow.pulseSpeed = pulseSpeed;
                glow.heartbeat = heartbeat;
                glow.beatScale = beatScale;
                var item = new SerializedObject(root.GetComponent<CollectibleItem>());
                item.FindProperty("pickupBurst").objectReferenceValue = burst;
                item.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static ParticleSystem Particles(Transform parent, string name, Color color, float alpha, float rate,
            float minLife, float maxLife, float minSize, float maxSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = 0f;
            main.startColor = new Color(color.r, color.g, color.b, alpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; // otherwise drops never seen yet stay empty
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.enabled = false;
            // Fade in and out instead of popping.
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
            };
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        internal static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f; // emit from the surface
        }

        internal static void Rise(ParticleSystem ps, float min, float max)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes must share a curve mode.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(min, max);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        internal static void Noise(ParticleSystem ps, float strength)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.3f;
        }
    }
}
