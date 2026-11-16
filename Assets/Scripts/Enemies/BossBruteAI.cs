using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using StealthGame.Player;
using Weapon = StealthGame.Player.PlayerCharacterController.WeaponType;

namespace StealthGame
{
    /// <summary>
    /// The final boss. It punches and swipes with a slow, floor-marked wind-up, leap-slams onto the player and charges
    /// in a straight line that stuns it against walls and pillars. Each weapon has a job: the axe breaks its armor, the
    /// sword cuts deepest into the flesh that exposes, and in phase 2 the spear outreaches the tremor ring, the safe
    /// way to interrupt the bellow that calls in zombies.
    /// Every move is one coroutine that runs its animation to the end, so nothing is cut off mid-swing; gameplay
    /// moments (release, contact, landing) are read from the animation's own clock, not from timers.
    /// </summary>
    public class BossBruteAI : ZombieBrain
    {
        public const string Locomotion = "Boss Locomotion", PunchState = "Boss Punch", SwipeState = "Boss Swipe", LeapState = "Boss Leap",
            FlexState = "Boss Flex", BellowState = "Boss Bellow", ChargeState = "Boss Charge", StunState = "Boss Stun",
            RecoverState = "Boss Recover", DeathState = "Boss Death";
        public const string PunchClip = "Brute Punch", SwipeClip = "Brute Swipe", LeapClip = "Boss Leap", FlexClip = "Boss Flex",
            RoarClip = "Brute Roar", RunClip = "Brute Run", Pace = "Pace";
        // Seconds into each clip, read off its hand and hip paths.
        // Punch (a one-two): starts with the arm cocked across the chest after the jab, drives forward, lands, settles.
        public const float PunchEnter = 0.38f, PunchRelease = 0.6f, PunchContact = 0.7f, PunchEnd = 1f;
        // Swipe: coils with the striking (left) arm drawn back behind the body, whips it round with a lunge, settles.
        // The coil hides the arm from whoever it is aimed at, so the boss first rears back (the roar's opening) as the tell.
        public const float SwipeEnter = 0.85f, SwipeRelease = 1.15f, SwipeContact = 1.29f, SwipeEnd = 2.4f;
        public const float RearFrom = 0.55f, RearTo = 1.45f;
        // Leap: crouches, leaves the ground, lands on both fists (held until ~2.2 s), stands back up.
        public const float LeapTakeoff = 0.55f, LeapLand = 1.65f, StunFrom = 1.78f, RecoverFrom = 2.3f;
        // Flex: 1.3 s of standing still skipped; leans in and tenses at 2.3 s. Roar: rears back, bellows 1.9-3.9 s, settles by 4.9 s.
        public const float FlexFrom = 1.3f, FlexPeak = 2.3f, FlexEnd = 3.9f, RoarFrom = 0.45f, RoarLoud = 1.9f, RoarOver = 3.9f, RoarEnd = 4.9f;

        [Header("Armor: the axe's job")]
        public int maxArmor = 150;
        [Tooltip("Share of a sword or spear hit that wears the armor; the axe always wears it by its full damage.")]
        [Range(0f, 1f)] public float nonAxeArmorDamage = 0.3f;
        [Tooltip("Share of every hit that reaches health while the armor holds.")]
        [Range(0f, 1f)] public float armoredHealthDamage = 0.5f;
        [Tooltip("Seconds the flesh stays exposed once the armor breaks; the break stagger opens it.")]
        public float exposedDuration = 8f;
        public float breakStagger = 3f;
        [Tooltip("Seconds after the exposed window closes before the armor grows back.")]
        public float armorRegrowDelay = 12f;

        [Header("Exposed flesh: the sword's job (health damage multipliers)")]
        public float swordExposed = 2.5f;
        public float spearExposed = 1.5f;
        public float axeExposed = 1f;

        [Header("Punch and swipe (reach is the combat target's Attack Reach)")]
        public int punchDamage = 25;
        public int swipeDamage = 32;
        [Tooltip("Width in degrees of the floor warning, which is exactly where the hit lands.")]
        public float punchArc = 70f;
        public float swipeArc = 200f;
        [Tooltip("Animation speed while it winds up: lower is a longer, clearer warning. The swipe rears back first, then coils at this speed.")]
        [Range(0.2f, 1f)] public float punchWindupPace = 0.4f;
        [Range(0.2f, 1f)] public float swipeWindupPace = 0.55f;
        [Tooltip("Breather after each punch or swipe has fully played out: the opening to hit back.")]
        public float meleeCooldown = 1f;

        [Header("Weight")]
        [Tooltip("Camera jolt and dust on every footfall. It only ever walks after the player; leap and charge close the distance.")]
        [Range(0f, 1f)] public float walkFootfall = 0.14f;
        [Range(0f, 1f)] public float chargeFootfall = 0.3f;

        [Header("Leap slam")]
        public Vector2 leapRange = new Vector2(6f, 14f);
        public float leapCooldown = 8f;
        public float leapRadius = 3.5f;
        public int leapDamage = 35;

        [Header("Charge (stuns itself on walls and pillars)")]
        public float chargeMinRange = 7f;
        public float chargeCooldown = 10f;
        public float chargeWindup = 1f;
        [Tooltip("The gallop plays at the speed that covers this, so the feet keep their grip: 5.5 m/s is its natural stride at this size. Faster looks frantic, not heavy.")]
        public float chargeSpeed = 6f;
        [Tooltip("Long enough to cross the arena: the charge is meant to end in a wall or pillar.")]
        public float chargeMaxTime = 6f;
        public float wallStun = 3.5f;
        public int chargeDamage = 40;

        [Header("Phase 2: tremor ring (the spear's job) and bellow")]
        [Range(0f, 1f)] public float phase2At = 0.55f;
        [Tooltip("Leap and charge cooldowns are multiplied by this in phase 2.")]
        public float phase2Cooldowns = 0.75f;
        public float tremorInterval = 3f;
        public float tremorWarning = 0.8f;
        public float tremorRadius = 3.8f;
        public int tremorDamage = 12;
        public float bellowInterval = 25f;
        [Tooltip("Tremor pulses come this often while it bellows.")]
        public float bellowTremorInterval = 1f;
        public int bellowHitsToInterrupt = 3;
        public float bellowStagger = 2f;
        [Tooltip("Zombies an uninterrupted bellow calls in, one per spawn point.")]
        public GameObject[] addPrefabs;
        public Transform[] addSpawnPoints;

        [Header("Effects")]
        [Range(0f, 2f)] public float cameraShake = 1f;
        [Tooltip("Set by Tools/Paramnesia/Set Up Boss.")]
        public Material telegraphMaterial;
        public Material dustMaterial;
        public Material ringMaterial;
        public Material glowMaterial;

        [System.NonSerialized] public int armor;
        public bool Phase2 { get; private set; }
        public bool IsExposed => Time.time < exposedUntil;

        static readonly int PaceId = Animator.StringToHash(Pace);
        static Material effectMaterial;
        readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        PlayerCharacterController pcc;
        Animator anim;
        Transform head;
        readonly Transform[] feet = new Transform[2];
        readonly Vector3[] footLast = new Vector3[2];
        readonly bool[] footUp = new bool[2];
        Vector3 lastPosition;
        ParticleSystem fistGlow;
        Light fistLight;
        CinemachineBasicMultiChannelPerlin perlin;
        Coroutine routine;
        GameObject leapRing, chargeLine, tremorRing, punchZone, punchFill, swipeZone, swipeFill, stars;
        GUIStyle barLabel;
        float exposedUntil, regrowAt = float.MaxValue, nextMelee, nextLeap, nextCharge, nextBellow, pendingStagger, stunUntil;
        float chargePace = 1f, shake, calmAmplitude, calmFrequency;
        bool engaged, acting, scripted, charging, bellowing, flexing, stunned, closing, cleanedUp, lastSwipe;
        int bellowHits, sameMelee;

        int PhaseHealth => Mathf.CeilToInt(combat.maxHealth * phase2At);
        float MeleeRadius => combat.attackReach + 0.5f;
        bool Settled => anim.GetCurrentAnimatorStateInfo(0).IsName(Locomotion) && !anim.IsInTransition(0);
        PlayerCharacterController Player =>
            pcc != null ? pcc : (pcc = detection.player != null ? detection.player.GetComponent<PlayerCharacterController>() : null);

        void Start()
        {
            anim = GetComponentInChildren<Animator>();
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            feet[0] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            feet[1] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            lastPosition = transform.position;
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
            {
                clipLengths[clip.name] = clip.length;
                // Root motion is the clip's speed scaled by the body, so this is the playback speed at which the run covers chargeSpeed.
                if (clip.name == RunClip)
                    chargePace = chargeSpeed / Mathf.Max(0.1f, clip.averageSpeed.magnitude * anim.humanScale * anim.transform.lossyScale.z);
            }
            anim.SetFloat(PaceId, 1f);
            armor = maxArmor;
            combat.damageFilter = Filter;
            combat.enableCounterAttack = false; // punch and swipe are moves here, with their own wind-up and floor warning
            leapRing = Marker(PrimitiveType.Cylinder);
            chargeLine = Marker(PrimitiveType.Cube);
            tremorRing = Marker(PrimitiveType.Cylinder);
            punchZone = Marker(PrimitiveType.Quad, Sector(punchArc));
            punchFill = Marker(PrimitiveType.Quad, Sector(punchArc));
            swipeZone = Marker(PrimitiveType.Quad, Sector(swipeArc));
            swipeFill = Marker(PrimitiveType.Quad, Sector(swipeArc));
            stars = Stars();
            fistGlow = Glow("Fist Glow", new Color(1f, 0.25f, 0.1f), 0.9f, 0.35f, 40f);
            fistLight = fistGlow.gameObject.AddComponent<Light>();
            fistLight.type = LightType.Point;
            fistLight.color = new Color(1f, 0.3f, 0.1f);
            fistLight.range = 6f;
            fistLight.shadows = LightShadows.None;
            fistGlow.gameObject.SetActive(false);
        }

        protected override bool StillTracking => true; // an arena boss never wanders off to search

        protected override void Engage(Transform player, bool busy)
        {
            if (armor == 0 && Time.time >= regrowAt) { armor = maxArmor; regrowAt = float.MaxValue; }
            Look(player.position + Vector3.up * 1.5f, scripted ? 0.25f : 1f); // head down and committed mid-leap and mid-charge
            // Straight after its opening roar it closes in on foot before the first leap or charge.
            if (!engaged) { engaged = true; nextLeap = Time.time + 2f; nextCharge = Time.time + 5f; }
            if (acting || busy) return;

            Vector3 toPlayer = Flat(player.position - transform.position);
            float distance = toPlayer.magnitude, offAxis = Vector3.Angle(transform.forward, toPlayer);
            float scale = Phase2 ? phase2Cooldowns : 1f;
            bool ready = Settled; // never cut into an animation that is still finishing
            if (ready && !Phase2 && combat.currentHealth <= PhaseHealth) Run(Flex());
            else if (ready && Phase2 && Time.time >= nextBellow) Run(Bellow(player));
            else if (ready && Time.time >= nextMelee && distance <= combat.attackReach + 0.3f && offAxis <= 100f)
            {
                // Off to the side only the wide swipe covers it; in front it mixes the two, never the same one three times running.
                bool swipe = offAxis > 35f || (sameMelee >= 2 ? !lastSwipe : Random.value < 0.5f);
                sameMelee = swipe == lastSwipe ? sameMelee + 1 : 1;
                lastSwipe = swipe;
                Run(Melee(player, swipe));
            }
            else if (ready && Time.time >= nextLeap && distance >= leapRange.x && distance <= leapRange.y) Run(Leap(player, scale));
            else if (ready && Time.time >= nextCharge && distance >= chargeMinRange &&
                     !NavMesh.Raycast(transform.position, player.position, out _, agent.areaMask)) Run(Charge(player, scale));
            else Chase(player, distance);
        }

        void Chase(Transform player, float distance)
        {
            agent.speed = roamSpeed; // a heavy walk; it never jogs after anyone
            agent.stoppingDistance = combat.attackReach * 0.75f;
            // Holds its ground until the player is clearly out of reach, instead of shuffling after every half step.
            if (distance > combat.attackReach + 0.3f) closing = true;
            else if (distance <= combat.attackReach * 0.8f) closing = false;
            if (!closing)
            {
                if (agent.hasPath) agent.ResetPath();
                TurnTowards(player.position, 3f); // keeps the player from living behind its back
            }
            else if (Time.time >= nextPath) { agent.SetDestination(player.position); nextPath = Time.time + 0.2f; }
        }

        /// <summary>Health damage a hit deals, wearing the armor down as it goes. Touches nothing but `armor`, so Check Boss can assert it.</summary>
        public int Resolve(int damage, Weapon weapon, bool exposed)
        {
            if (exposed)
                return Mathf.RoundToInt(damage * (weapon == Weapon.Sword ? swordExposed : weapon == Weapon.Spear ? spearExposed : axeExposed));
            if (armor <= 0) return damage;
            armor = Mathf.Max(0, armor - Mathf.RoundToInt(damage * (weapon == Weapon.Axe ? 1f : nonAxeArmorDamage)));
            return Mathf.RoundToInt(damage * armoredHealthDamage);
        }

        // EnemyCombatTarget.damageFilter: the weapon in hand is the one that swung (it can't change mid-swing).
        int Filter(int damage)
        {
            if (flexing) return 0;
            if (bellowing && ++bellowHits >= bellowHitsToInterrupt) Stagger(bellowStagger);
            bool armored = armor > 0;
            damage = Resolve(damage, Player != null ? Player.equippedWeapon : Weapon.Sword, IsExposed);
            if (armored && armor == 0)
            {
                exposedUntil = Time.time + exposedDuration;
                regrowAt = exposedUntil + armorRegrowDelay;
                EnemyCombatTarget.SpawnHitSparkBurst(transform.position + Vector3.up * 2.5f, 60);
                Shake(0.5f);
                Stagger(breakStagger);
            }
            // Health can't skip the phase change: the bar holds at the threshold until the boss has flexed.
            if (!Phase2) damage = Mathf.Min(damage, combat.currentHealth - PhaseHealth);
            return damage;
        }

        void Run(IEnumerator move)
        {
            acting = true;
            agent.ResetPath();
            routine = StartCoroutine(Perform(move));
        }

        IEnumerator Perform(IEnumerator move)
        {
            yield return move;
            Finish();
        }

        void Finish()
        {
            acting = scripted = charging = bellowing = flexing = stunned = false;
            anim.SetFloat(PaceId, 1f);
            fistGlow.gameObject.SetActive(false);
            foreach (var marker in new[] { leapRing, chargeLine, punchZone, punchFill, swipeZone, swipeFill }) marker.SetActive(false);
            if (pendingStagger > 0f) { float seconds = pendingStagger; pendingStagger = 0f; Stagger(seconds); }
        }

        /// <summary>Cuts the current move short and drops the boss to its fists; mid-air or mid-charge it waits until the body is back on the ground.</summary>
        void Stagger(float seconds)
        {
            if (scripted) { pendingStagger = Mathf.Max(pendingStagger, seconds); return; }
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            combat.ApplyParryStagger(stunUntil - Time.time);
            if (stunned) return; // already down: it just stays down longer
            if (acting) { StopCoroutine(routine); Finish(); }
            Run(Stun());
        }

        // The leap's landing, both fists planted, held at a crawl; then the same clip's stand-up.
        IEnumerator Stun()
        {
            stunned = true;
            Fade(StunState, 0.45f, LeapClip, StunFrom);
            Dust(transform.position + transform.forward, 12, 0.8f, 2.5f);
            while (Time.time < stunUntil) yield return null;
            stunned = false;
            Fade(RecoverState, 0.4f, LeapClip, RecoverFrom);
            yield return Settle();
        }

        IEnumerator Melee(Transform player, bool swipe)
        {
            string state = swipe ? SwipeState : PunchState, clip = swipe ? SwipeClip : PunchClip;
            float enter = swipe ? SwipeEnter : PunchEnter, release = swipe ? SwipeRelease : PunchRelease;
            float arc = swipe ? swipeArc : punchArc, radius = MeleeRadius;
            GameObject zone = swipe ? swipeZone : punchZone, fill = swipe ? swipeFill : punchFill;
            nextMelee = float.MaxValue;
            var size = new Vector3(radius, 1f, radius);
            // The striking fist burns red from the first moment, and the floor shows where the blow lands:
            // the bright fill reaching the rim is the moment it does.
            var fist = anim.GetBoneTransform(swipe ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            fistGlow.transform.SetParent(fist, false);
            fistGlow.transform.localPosition = Vector3.zero;
            fistGlow.gameObject.SetActive(true);
            float rear = 0f;
            if (swipe)
            {
                // Rears right back first, chest up and arms flared: a full second of unmistakable warning.
                rear = 0.65f;
                Fade(BellowState, 0.3f, RoarClip, RearFrom);
                for (float waited = 0f; waited < 4f; waited += Time.deltaTime)
                {
                    float at = At(BellowState, RoarClip);
                    if (at >= RearTo || (at < 0f && waited > 0.5f)) break;
                    float progress = at < 0f ? 0f : Mathf.InverseLerp(RearFrom, RearTo, at) * rear;
                    TurnTowards(player.position, 2f); // still lining the blow up
                    fistLight.intensity = Mathf.Lerp(1f, 6f, progress);
                    Show(zone, transform.position, size, transform.rotation);
                    Show(fill, transform.position + Vector3.up * 0.02f, size * Mathf.Lerp(0.15f, 1f, progress), transform.rotation);
                    yield return null;
                }
            }
            // Then the clip's own coil, slowed; from here the zone is locked: get out of it.
            anim.SetFloat(PaceId, swipe ? swipeWindupPace : punchWindupPace);
            Fade(state, swipe ? 0.35f : 0.25f, clip, enter);
            for (float waited = 0f; waited < 6f; waited += Time.deltaTime)
            {
                float at = At(state, clip);
                if (at >= release || (at < 0f && waited > 0.5f)) break;
                float progress = Mathf.Lerp(rear, 1f, at < 0f ? 0f : Mathf.InverseLerp(enter, release, at));
                if (!swipe && progress < 0.6f) TurnTowards(player.position, 2f);
                fistLight.intensity = Mathf.Lerp(1f, 6f, progress);
                Show(zone, transform.position, size, transform.rotation);
                Show(fill, transform.position + Vector3.up * 0.02f, size * Mathf.Lerp(0.15f, 1f, progress), transform.rotation);
                yield return null;
            }
            anim.SetFloat(PaceId, 1f);
            yield return PlayTo(state, clip, swipe ? SwipeContact : PunchContact);
            zone.SetActive(false);
            fill.SetActive(false);
            fistGlow.gameObject.SetActive(false);
            Vector3 toPlayer = Flat(player.position - transform.position);
            bool hit = toPlayer.magnitude <= radius && Vector3.Angle(transform.forward, toPlayer) <= arc * 0.5f;
            if (hit && Player != null) Player.OnEnemyAttackStrike(combat, swipe ? swipeDamage : punchDamage);
            if (swipe)
            {
                Dust(transform.position + transform.forward * (radius * 0.6f), 12, 1f, 3f);
                Debris(transform.position + transform.forward * (radius * 0.6f), 8, 4f);
                Shake(hit ? 0.6f : 0.3f);
            }
            else if (hit) Shake(0.4f);
            anim.SetFloat(PaceId, 1f);
            yield return Settle(); // the follow-through plays out before anything else starts
            nextMelee = Time.time + meleeCooldown;
        }

        IEnumerator Leap(Transform player, float scale)
        {
            nextLeap = Time.time + leapCooldown * scale;
            if (!NavMesh.SamplePosition(player.position, out var landing, 2f, agent.areaMask)) yield break;
            Vector3 start = transform.position, target = landing.position;
            Face(target);
            Show(leapRing, target, new Vector3(leapRadius * 2f, 0.01f, leapRadius * 2f), Quaternion.identity);
            Fade(LeapState, 0.2f);
            scripted = true;
            for (float waited = 0f; waited < 5f; waited += Time.deltaTime)
            {
                float at = At(LeapState, LeapClip);
                if (at >= LeapLand || (at < 0f && waited > 0.5f)) break;
                // Crouches where it stands, then flies over anything in the way; the ring marks where it comes down.
                transform.position = Vector3.Lerp(start, target, Mathf.InverseLerp(LeapTakeoff, LeapLand, at));
                yield return null;
            }
            Place(target);
            scripted = false;
            leapRing.SetActive(false);
            Slam(target, leapRadius, 1f);
            Vector3 fromSlam = Flat(player.position - target);
            if (Player != null && fromSlam.magnitude <= leapRadius)
            {
                Player.OnEnemyAttackStrike(combat, leapDamage);
                // Thrown clear, so nobody ends up standing on the boss.
                StartCoroutine(Shove(fromSlam.sqrMagnitude > 0.01f ? fromSlam.normalized : transform.forward, Mathf.Max(1f, 3f - fromSlam.magnitude)));
            }
            yield return Settle(); // fists in the ground, then the clip's own stand-up: the punish window
        }

        IEnumerator Charge(Transform player, float scale)
        {
            nextCharge = Time.time + chargeCooldown * scale;
            float reach = chargeSpeed * chargeMaxTime;
            Fade(BellowState, 0.25f, RoarClip, RoarFrom); // rears back as the wind-up
            for (float t = 0f; t < chargeWindup; t += Time.deltaTime)
            {
                if (t < chargeWindup * 0.7f) TurnTowards(player.position, 8f); // the line locks just before it goes
                float length = NavMesh.Raycast(transform.position, transform.position + transform.forward * reach, out var end, agent.areaMask) ? end.distance : reach;
                Show(chargeLine, transform.position + transform.forward * (length * 0.5f), new Vector3(1.6f, 0.02f, length), transform.rotation);
                yield return null;
            }
            Vector3 direction = transform.forward;
            anim.SetFloat(PaceId, chargePace * 0.5f);
            Fade(ChargeState, 0.2f);
            scripted = charging = true;
            bool struck = false;
            float nextDust = 0f;
            for (float t = 0f; t < chargeMaxTime; t += Time.deltaTime)
            {
                // Builds up over half a second; stride and ground speed rise together so the feet never skate.
                float effort = Mathf.Lerp(0.5f, 1f, t / 0.5f);
                anim.SetFloat(PaceId, chargePace * effort);
                Vector3 next = transform.position + direction * (chargeSpeed * effort * Time.deltaTime);
                if (NavMesh.Raycast(transform.position, next, out var wall, agent.areaMask))
                {
                    Place(wall.position);
                    Vector3 impact = wall.position + direction * 0.8f + Vector3.up * 1.5f;
                    Debris(impact, 24, 7f);
                    Dust(impact, 30, 0.6f, 6f);
                    Ring(impact, Quaternion.FromToRotation(Vector3.up, -direction), 3f);
                    Shake(1f);
                    pendingStagger = wallStun;
                    yield break;
                }
                Place(next);
                if (!struck && Player != null && Flat(player.position - transform.position).magnitude < 1.8f)
                {
                    struck = true;
                    Player.OnEnemyAttackStrike(combat, chargeDamage);
                    Shake(0.8f);
                    // Knocked aside, out of its path.
                    Vector3 aside = Vector3.Cross(Vector3.up, direction);
                    StartCoroutine(Shove(Vector3.Dot(aside, player.position - transform.position) < 0f ? -aside : aside, 2.5f));
                }
                yield return null;
            }
            // Ran out of room to hit anything: all that mass takes a moment to stop.
            chargeLine.SetActive(false);
            charging = false;
            Fade(Locomotion, 0.7f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                Vector3 next = transform.position + direction * (chargeSpeed * (1f - t / 0.9f) * (1f - t / 0.9f) * Time.deltaTime);
                if (NavMesh.Raycast(transform.position, next, out _, agent.areaMask)) break;
                Place(next);
                if (Time.time >= nextDust) { Dust(transform.position, 4, 0.6f, 2f); nextDust = Time.time + 0.08f; }
                yield return null;
            }
            scripted = false;
            yield return Settle();
        }

        IEnumerator Flex()
        {
            Phase2 = true;
            flexing = true; // invulnerable until it is done
            armor = maxArmor;
            exposedUntil = 0f;
            regrowAt = float.MaxValue;
            Fade(FlexState, 0.3f, FlexClip, FlexFrom);
            yield return PlayTo(FlexState, FlexClip, FlexPeak);
            Slam(transform.position, tremorRadius, 0.8f);
            StartCoroutine(Tremors()); // runs alongside every later move, not as one
            yield return Settle();
            nextBellow = Time.time + bellowInterval * 0.4f;
        }

        IEnumerator Bellow(Transform player)
        {
            nextBellow = Time.time + bellowInterval;
            bellowing = true;
            bellowHits = 0;
            Fade(BellowState, 0.3f, RoarClip, RoarFrom);
            float nextWave = 0f;
            for (float waited = 0f; waited < 8f; waited += Time.deltaTime)
            {
                float at = At(BellowState, RoarClip);
                if (at >= RoarOver || (at < 0f && waited > 0.5f)) break;
                if (at >= RoarLoud && Time.time >= nextWave)
                {
                    Ring(head.position + transform.forward, Quaternion.FromToRotation(Vector3.up, transform.forward), 2.5f, 0.4f);
                    Shake(0.25f);
                    nextWave = Time.time + 0.4f;
                }
                yield return null;
            }
            // Not interrupted: reinforcements, and the armor grows straight back.
            bellowing = false;
            armor = maxArmor;
            exposedUntil = 0f;
            regrowAt = float.MaxValue;
            for (int i = 0; addPrefabs != null && addPrefabs.Length > 0 && addSpawnPoints != null && i < addSpawnPoints.Length; i++)
            {
                var prefab = addPrefabs[i % addPrefabs.Length];
                if (prefab == null || addSpawnPoints[i] == null) continue;
                var add = Instantiate(prefab, addSpawnPoints[i].position, addSpawnPoints[i].rotation).GetComponent<ZombieBrain>();
                if (add != null) add.OnDamaged(player.position); // arrives already hunting the player
            }
            yield return Settle(); // the roar winds down on its own
        }

        IEnumerator Tremors()
        {
            while (!combat.IsDead)
            {
                yield return new WaitForSeconds((bellowing ? bellowTremorInterval : tremorInterval) - tremorWarning);
                if (flexing || scripted || stunned) continue;
                // The ring grows out to the danger edge, then pulses.
                for (float t = 0f; t < tremorWarning; t += Time.deltaTime)
                {
                    float size = tremorRadius * 2f * Mathf.Lerp(0.3f, 1f, t / tremorWarning);
                    Show(tremorRing, transform.position, new Vector3(size, 0.01f, size), Quaternion.identity);
                    yield return null;
                }
                tremorRing.SetActive(false);
                if (combat.IsDead || stunned) continue;
                Ring(transform.position + Vector3.up * 0.1f, Quaternion.identity, tremorRadius);
                Dust(transform.position, 14, tremorRadius * 0.5f, 2.5f);
                Shake(0.35f);
                var player = detection.player;
                if (Player != null && player != null && Flat(player.position - transform.position).magnitude <= tremorRadius)
                    Player.OnEnemyAttackStrike(combat, tremorDamage);
            }
        }

        public override void ApplyRootMotion(Vector3 displacement)
        {
            if (scripted) return; // leap and charge place the body themselves
            // The run clip drifts sideways; keep only travel along the facing so it doesn't crab on its way to the player.
            if (!acting) displacement = transform.forward * Mathf.Max(0f, Vector3.Dot(displacement, transform.forward));
            base.ApplyRootMotion(displacement);
        }

        void LateUpdate()
        {
            if (cleanedUp) return;
            if (perlin != null && shake > 0f)
            {
                shake = Mathf.Max(0f, shake - Time.deltaTime * 2.5f);
                // shortcut: restores the gains from when the shake began; toggling the camera view mid-shake keeps the old view's sway until the next toggle.
                perlin.AmplitudeGain = calmAmplitude + shake * 2.5f * cameraShake;
                perlin.FrequencyGain = shake > 0f ? Mathf.Max(calmFrequency, 2.5f) : calmFrequency;
            }
            bool dead = combat.IsDead;
            Footfalls(dead);
            stars.SetActive(stunned && !dead);
            if (stars.activeSelf)
            {
                stars.transform.position = head.position + Vector3.up * 1.1f;
                stars.transform.Rotate(0f, 200f * Time.deltaTime, 0f);
            }
            if (!dead) return;
            cleanedUp = true;
            StopAllCoroutines();
            if (perlin != null && shake > 0f) { perlin.AmplitudeGain = calmAmplitude; perlin.FrequencyGain = calmFrequency; }
            foreach (var made in new[] { leapRing, chargeLine, tremorRing, punchZone, punchFill, swipeZone, swipeFill, stars, fistGlow.gameObject }) Destroy(made);
        }

        // Weight: each foot that comes down while it walks or charges kicks up dust and jolts the camera.
        void Footfalls(bool dead)
        {
            float speed = Time.deltaTime > 0f ? Flat(transform.position - lastPosition).magnitude / Time.deltaTime : 0f;
            lastPosition = transform.position;
            for (int i = 0; i < 2; i++)
            {
                if (feet[i] == null) continue;
                // A planted foot stands still on the ground while the body passes over it; a swinging one outruns the body.
                Vector3 at = feet[i].position;
                float footSpeed = Time.deltaTime > 0f ? Flat(at - footLast[i]).magnitude / Time.deltaTime : 0f;
                footLast[i] = at;
                if (footSpeed > Mathf.Max(1.5f, speed * 1.1f)) footUp[i] = true;
                else if (footUp[i] && footSpeed < Mathf.Max(0.6f, speed * 0.45f))
                {
                    footUp[i] = false; // it has landed
                    if (dead || speed < 1f || (acting && !charging)) continue;
                    var ground = new Vector3(feet[i].position.x, transform.position.y, feet[i].position.z);
                    Dust(ground, charging ? 6 : 3, 0.35f, charging ? 2.5f : 1.2f);
                    Shake(charging ? chargeFootfall : walkFootfall);
                }
            }
        }

        // ---- Animation clock ----

        float Length(string clip) => clipLengths.TryGetValue(clip, out float length) ? length : 1f;

        /// <summary>Cross-fades into a state over `seconds`, starting `from` seconds into its clip.</summary>
        void Fade(string state, float seconds, string clip = null, float from = 0f)
        {
            // CrossFade measures its blend as a fraction of the state being left.
            float leaving = Mathf.Max(0.05f, anim.GetCurrentAnimatorStateInfo(0).length);
            anim.CrossFade(state, Mathf.Clamp(seconds / leaving, 0.01f, 1f), 0, clip != null ? from / Length(clip) : 0f);
        }

        /// <summary>Seconds into `clip` that `state` is showing, or -1 while it isn't the state playing or being blended to.</summary>
        float At(string state, string clip)
        {
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName(state)) info = anim.GetNextAnimatorStateInfo(0);
            return info.IsName(state) ? info.normalizedTime * Length(clip) : -1f;
        }

        IEnumerator PlayTo(string state, string clip, float seconds)
        {
            for (float waited = 0f; waited < 8f; waited += Time.deltaTime)
            {
                float at = At(state, clip);
                if (at >= seconds || (at < 0f && waited > 0.5f)) yield break;
                yield return null;
            }
        }

        /// <summary>Waits for the current animation to finish and blend back into locomotion.</summary>
        IEnumerator Settle()
        {
            yield return null;
            for (float waited = 0f; waited < 8f && !Settled; waited += Time.deltaTime) yield return null;
        }

        // ---- Placement ----

        IEnumerator Shove(Vector3 direction, float distance)
        {
            var body = Player != null ? Player.characterController : null;
            for (float t = 0f; body != null && t < 0.25f; t += Time.deltaTime)
            {
                body.Move(direction * (distance / 0.25f * Time.deltaTime));
                yield return null;
            }
        }

        void Place(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var ground, 1f, agent.areaMask)) point = ground.position;
            transform.position = point;
            if (agent.enabled) agent.nextPosition = point;
        }

        void Face(Vector3 point)
        {
            var to = Flat(point - transform.position);
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---- Floor warnings ----

        static void Show(GameObject marker, Vector3 position, Vector3 size, Quaternion rotation)
        {
            marker.transform.SetPositionAndRotation(position + Vector3.up * 0.04f, rotation);
            marker.transform.localScale = size;
            marker.SetActive(true);
        }

        GameObject Marker(PrimitiveType shape, Mesh mesh = null)
        {
            var marker = GameObject.CreatePrimitive(shape);
            marker.name = name + " Telegraph";
            DestroyImmediate(marker.GetComponent<Collider>());
            if (mesh != null) marker.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = marker.GetComponent<Renderer>();
            if (telegraphMaterial != null) renderer.sharedMaterial = telegraphMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            marker.SetActive(false);
            return marker;
        }

        /// <summary>A flat pie slice of radius 1 lying on the ground, opening along +Z.</summary>
        static Mesh Sector(float degrees)
        {
            const int steps = 32;
            var vertices = new Vector3[steps + 2];
            var triangles = new int[steps * 3];
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-degrees * 0.5f, degrees * 0.5f, i / (float)steps);
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }
            for (int i = 0; i < steps; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            var mesh = new Mesh { name = "Boss Sector", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Effects ----

        static Material EffectMaterial => effectMaterial != null ? effectMaterial : (effectMaterial = new Material(
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")));

        /// <summary>Ground impact: shockwave ring, dust, sparks and a camera jolt.</summary>
        void Slam(Vector3 point, float radius, float strength)
        {
            Ring(point + Vector3.up * 0.1f, Quaternion.identity, radius);
            Ring(point + Vector3.up * 0.1f, Quaternion.identity, radius * 0.6f, 0.6f);
            Dust(point, Mathf.RoundToInt(40 * strength), radius * 0.3f, 7f * strength, true); // rolls outward along the ground
            Dust(point, Mathf.RoundToInt(14 * strength), radius * 0.2f, 3f * strength);
            Debris(point, Mathf.RoundToInt(22 * strength), 8f * strength);
            Shake(strength);
        }

        void Ring(Vector3 position, Quaternion rotation, float radius, float seconds = 0.45f) => StartCoroutine(Expand(position, rotation, radius, seconds));

        // A thin ring that races outward and fades away.
        IEnumerator Expand(Vector3 position, Quaternion rotation, float radius, float seconds)
        {
            var ring = new GameObject("Boss Shockwave");
            Destroy(ring, seconds + 0.1f); // cleaned up even if this coroutine is stopped
            ring.transform.SetPositionAndRotation(position, rotation);
            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            for (int i = 0; i < 48; i++) line.SetPosition(i, new Vector3(Mathf.Cos(i * Mathf.PI / 24f), 0f, Mathf.Sin(i * Mathf.PI / 24f)));
            line.sharedMaterial = ringMaterial != null ? ringMaterial : EffectMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (float t = 0f; t < seconds && ring != null; t += Time.deltaTime)
            {
                float k = t / seconds;
                ring.transform.localScale = Vector3.one * (radius * Mathf.Lerp(0.3f, 1f, 1f - (1f - k) * (1f - k)));
                line.widthMultiplier = Mathf.Lerp(0.3f, 0.08f, k);
                line.startColor = line.endColor = new Color(1f, 0.88f, 0.65f, 0.8f * (1f - k));
                yield return null;
            }
        }

        /// <summary>Soft dust kicked up off the ground: puffs swell, drift and fade.</summary>
        void Dust(Vector3 position, int count, float spread, float speed, bool alongGround = false)
        {
            var puff = new GameObject("Boss Dust");
            puff.transform.position = position + Vector3.up * 0.1f;
            var particles = puff.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.57f, 0.52f, 0.75f), new Color(0.42f, 0.39f, 0.37f, 0.55f));
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = alongGround ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Hemisphere;
            shape.rotation = new Vector3(alongGround ? 90f : -90f, 0f, 0f); // flat ring pushing outward, or a dome going up
            shape.radius = spread;
            if (alongGround) shape.radiusThickness = 0f;
            var swell = particles.sizeOverLifetime;
            swell.enabled = true;
            swell.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
            };
            var drag = particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.12f;
            drag.limit = 0.5f;
            var renderer = puff.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial != null ? dustMaterial : EffectMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
            Destroy(puff, 1.6f);
        }

        /// <summary>Jolts the player's camera, less the farther the boss is.</summary>
        void Shake(float strength)
        {
            if (perlin == null) perlin = FindFirstObjectByType<CinemachineBasicMultiChannelPerlin>();
            if (perlin == null || cameraShake <= 0f) return;
            float near = detection.player != null ? Mathf.Clamp01(1f - Flat(detection.player.position - transform.position).magnitude / 25f) : 1f;
            if (shake <= 0f) { calmAmplitude = perlin.AmplitudeGain; calmFrequency = perlin.FrequencyGain; }
            shake = Mathf.Max(shake, strength * near);
        }

        /// <summary>Chunks of floor thrown up by an impact: tumbling low-poly rubble that falls back and shrinks away.</summary>
        void Debris(Vector3 position, int count, float speed)
        {
            var rubble = new GameObject("Boss Debris");
            rubble.transform.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(-90f, 0f, 0f)); // cone up
            var particles = rubble.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.32f);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.34f, 0.32f, 0.36f), new Color(0.2f, 0.19f, 0.22f));
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.8f;
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            var renderer = rubble.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = EffectMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
            Destroy(rubble, 1.8f);
        }

        /// <summary>A looping emitter of soft glowing motes left behind in the world as it moves.</summary>
        ParticleSystem Glow(string label, Color color, float size, float lifetime, float rate)
        {
            var glow = new GameObject(name + " " + label);
            var particles = glow.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = glow.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial != null ? glowMaterial : EffectMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
            return particles;
        }

        // Glowing motes circling the head while it is stunned; each leaves a short fading tail as the ring turns.
        GameObject Stars()
        {
            var ring = new GameObject(name + " Stun Stars");
            for (int i = 0; i < 4; i++)
            {
                var mote = Glow("Stun Mote", new Color(1f, 0.85f, 0.25f), 0.42f, 0.3f, 45f);
                mote.transform.SetParent(ring.transform, false);
                mote.transform.localPosition = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 0.95f;
            }
            ring.SetActive(false);
            return ring;
        }

        void OnGUI()
        {
            if (!IsAlerted || combat.IsDead) return;
            if (barLabel == null) barLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
            var white = Texture2D.whiteTexture;
            float width = Mathf.Min(Screen.width * 0.6f, 720f), x = (Screen.width - width) * 0.5f, y = 40f;
            string status = stunned ? "STAGGERED"
                : bellowing ? "BELLOWING - hit it " + Mathf.Max(0, bellowHitsToInterrupt - bellowHits) + "x!"
                : flexing ? "ENRAGED"
                : IsExposed ? "EXPOSED - cut it!"
                : armor > 0 ? "ARMORED" : "";
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x - 4f, y - 26f, width + 8f, 52f), white);
            GUI.color = new Color(0.85f, 0.15f, 0.12f);
            GUI.DrawTexture(new Rect(x, y, width * combat.currentHealth / Mathf.Max(1f, combat.maxHealth), 14f), white);
            GUI.color = new Color(0.75f, 0.8f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + 17f, width * armor / Mathf.Max(1f, maxArmor), 5f), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y - 25f, width, 22f), name.Replace("Enemy_", "").Replace("_", " ") + (Phase2 ? " II" : "") + "   " + status, barLabel);
        }
    }
}
