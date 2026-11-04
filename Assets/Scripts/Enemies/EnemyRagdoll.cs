using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Death ragdoll built from the humanoid skeleton at the moment of death, so living enemies carry
    /// no extra physics. Joint axes and limits follow Unity's Ragdoll Wizard. Once the body settles it
    /// is frozen and its colliders switched off, so corpses cost nothing and never snag the player.
    /// </summary>
    public class EnemyRagdoll : MonoBehaviour
    {
        const float MaxSimulationTime = 6f;
        Rigidbody[] bodies;
        float diedAt;

        public static void Activate(Animator animator, Vector3 launchVelocity)
        {
            if (animator == null || !animator.isHuman || animator.GetComponent<EnemyRagdoll>() != null) return;
            animator.gameObject.AddComponent<EnemyRagdoll>().Build(animator, launchVelocity);
        }

        void Build(Animator animator, Vector3 launchVelocity)
        {
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
            var hips = Bone(HumanBodyBones.Hips);
            var spine = Bone(HumanBodyBones.Spine);
            var head = Bone(HumanBodyBones.Head);
            Transform[] arms = { Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.RightUpperArm) };
            Transform[] elbows = { Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.RightLowerArm) };
            Transform[] legs = { Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.RightUpperLeg) };
            Transform[] knees = { Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.RightLowerLeg) };
            Transform[] hands = { Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.RightHand) };
            Transform[] feet = { Bone(HumanBodyBones.LeftFoot), Bone(HumanBodyBones.RightFoot) };
            // Character axes from the pose itself, as the wizard does.
            Vector3 right = (legs[1].position - legs[0].position).normalized;
            Vector3 up = (head.position - hips.position).normalized;
            Vector3 forward = Vector3.Cross(right, up);
            float shoulders = Vector3.Distance(arms[0].position, arms[1].position);
            float hipWidth = Vector3.Distance(legs[0].position, legs[1].position);

            var hipsBody = Body(hips, 12f);
            var pelvis = hips.gameObject.AddComponent<SphereCollider>();
            pelvis.center = hips.InverseTransformPoint(Vector3.Lerp(hips.position, spine.position, 0.5f));
            pelvis.radius = hipWidth * 0.75f / hips.lossyScale.x;

            var torso = Body(spine, 12f);
            Limb(spine, head, shoulders * 0.32f);
            Joint(torso, hipsBody, right, forward, -20f, 20f, 10f);

            var skull = Body(head, 5f);
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.radius = shoulders * 0.28f / head.lossyScale.x;
            headCollider.center = head.InverseTransformPoint(head.position + up * shoulders * 0.2f);
            Joint(skull, torso, right, forward, -40f, 25f, 25f);

            for (int i = 0; i < 2; i++)
            {
                var thigh = Body(legs[i], 7f);
                Limb(legs[i], knees[i], hipWidth * 0.45f);
                Joint(thigh, hipsBody, right, forward, -20f, 70f, 30f);
                var shin = Body(knees[i], 4f);
                Limb(knees[i], feet[i], hipWidth * 0.35f);
                Joint(shin, thigh, right, forward, -80f, 0f, 0f);
                var upperArm = Body(arms[i], 3f);
                Limb(arms[i], elbows[i], shoulders * 0.14f);
                Joint(upperArm, torso, up, forward, -70f, 10f, 50f);
                var forearm = Body(elbows[i], 2f);
                Limb(elbows[i], hands[i], shoulders * 0.12f);
                Joint(forearm, upperArm, forward, up, -90f, 0f, 0f);
            }

            // Limbs overlap in most poses; colliding with each other they would kick the body apart.
            var parts = GetComponentsInChildren<Collider>();
            for (int a = 0; a < parts.Length; a++)
                for (int b = a + 1; b < parts.Length; b++) Physics.IgnoreCollision(parts[a], parts[b]);

            foreach (var mesh in animator.GetComponentsInChildren<SkinnedMeshRenderer>()) mesh.updateWhenOffscreen = true; // bounds follow the falling body
            animator.enabled = false;
            bodies = GetComponentsInChildren<Rigidbody>();
            foreach (var body in bodies) body.linearVelocity = launchVelocity;
            torso.AddForce(launchVelocity * 0.5f, ForceMode.VelocityChange); // the blow lands on the chest, so the body folds
            diedAt = Time.time;
        }

        void FixedUpdate()
        {
            if (bodies == null) return;
            float elapsed = Time.time - diedAt;
            bool still = true;
            foreach (var body in bodies) still &= body.linearVelocity.sqrMagnitude < 0.01f;
            if (elapsed < 1.5f || (!still && elapsed < MaxSimulationTime)) return;
            foreach (var body in bodies)
            {
                body.isKinematic = true;
                foreach (var collider in body.GetComponents<Collider>()) collider.enabled = false;
            }
            bodies = null;
            enabled = false;
        }

        static Rigidbody Body(Transform bone, float mass)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return body;
        }

        // Capsule from a bone to its child, along whichever local axis points at the child.
        static void Limb(Transform bone, Transform child, float worldRadius)
        {
            Vector3 local = bone.InverseTransformPoint(child.position);
            int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = local * 0.5f;
            capsule.height = Mathf.Abs(local[axis]);
            capsule.radius = worldRadius / bone.lossyScale.x;
        }

        static void Joint(Rigidbody body, Rigidbody parent, Vector3 twistAxis, Vector3 swingAxis, float low, float high, float swing)
        {
            var joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.axis = Snap(body.transform.InverseTransformDirection(twistAxis));
            joint.swingAxis = Snap(body.transform.InverseTransformDirection(swingAxis));
            joint.enablePreprocessing = false;
            joint.lowTwistLimit = new SoftJointLimit { limit = low };
            joint.highTwistLimit = new SoftJointLimit { limit = high };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = 0f };
        }

        // Nearest signed local axis, as the wizard snaps joint axes.
        static Vector3 Snap(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }
    }
}
