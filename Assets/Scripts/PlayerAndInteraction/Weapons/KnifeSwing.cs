// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The knife's swing. When the player attacks, the knife and the hand that holds it sweep across the view: a short wind-up up and to the right,
    /// a fast slash to the left that lands at the weapon's hit delay (the moment <see cref="MeleeWeapon"/> damages whatever is in front), a
    /// follow-through and the return to rest. The other hand stays where it is. It turns the hand's parts about a point near the elbow, so the
    /// whole forearm swings, and puts them back exactly when the swing is over. It only moves meshes: what the knife hits is MeleeWeapon's.
    /// </summary>
    public class KnifeSwing : MonoBehaviour
    {
        public static readonly string[] SwingingParts = { "Knife", "GloveR", "SleeveR" };

        /// <summary>Where the elbow is, in the holder's frame: below and behind the hand, a little to the right.</summary>
        public static readonly Vector3 DefaultPivot = new Vector3(0.24f, -0.28f, 0.22f);

        // The keys, as multiples of the hit delay: wind-up, the blade lands, follow-through, back at rest.
        // Angles are Unity Euler degrees about the elbow point (roll is anticlockwise as the player sees it), shifts are metres in the holder's frame.
        private static readonly Key[] Keys =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.35f, new Vector3(-6f, 10f, -14f), new Vector3(0.04f, 0.04f, -0.04f)),
            new Key(1f, new Vector3(8f, -18f, 18f), new Vector3(-0.12f, -0.02f, 0.07f)),
            new Key(1.5f, new Vector3(10f, -26f, 26f), new Vector3(-0.17f, -0.04f, 0.09f)),
            new Key(3.3f, Vector3.zero, Vector3.zero),
        };

        private readonly struct Key
        {
            public readonly float Time;
            public readonly Vector3 Euler;
            public readonly Vector3 Shift;

            public Key(float time, Vector3 euler, Vector3 shift)
            {
                Time = time;
                Euler = euler;
                Shift = shift;
            }
        }

        /// <summary>How far the swing has turned and moved the hand at one moment.</summary>
        public readonly struct Pose
        {
            public readonly Quaternion Rotation;
            public readonly Vector3 Shift;

            public Pose(Quaternion rotation, Vector3 shift)
            {
                Rotation = rotation;
                Shift = shift;
            }
        }

        [SerializeField] private MeleeWeapon weapon;
        [Tooltip("The knife weapon's Hand: the parent of the knife model.")]
        [SerializeField] private Transform hand;
        [Tooltip("The elbow: the point the swing turns about, in the holder's frame.")]
        [SerializeField] private Vector3 pivot = new Vector3(0.24f, -0.28f, 0.22f);

        private Transform group;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float time = -1f;
        private float hitDelay = 0.11f;

        /// <summary>The moving group (knife, right glove and right sleeve), or null before <see cref="Prepare"/>.</summary>
        public Transform Group => group;

        public bool Swinging => time >= 0f;

        public Vector3 Pivot => pivot;

        /// <summary>Seconds from the start of the swing until the hand is back at rest.</summary>
        public static float Duration(float hitDelay)
        {
            return Keys[Keys.Length - 1].Time * Mathf.Max(hitDelay, 0.05f);
        }

        /// <summary>The swing at a time after the attack began (no turn and no shift before it and after it).</summary>
        public static Pose Evaluate(float time, float hitDelay)
        {
            float scale = Mathf.Max(hitDelay, 0.05f);
            if (time <= 0f || time >= Duration(hitDelay))
            {
                return new Pose(Quaternion.identity, Vector3.zero);
            }

            for (int i = 1; i < Keys.Length; i++)
            {
                float start = Keys[i - 1].Time * scale;
                float end = Keys[i].Time * scale;
                if (time <= end)
                {
                    float t = WeaponMath.Smoothstep(start, end, time);
                    return new Pose(Quaternion.Euler(Vector3.Lerp(Keys[i - 1].Euler, Keys[i].Euler, t)),
                        Vector3.Lerp(Keys[i - 1].Shift, Keys[i].Shift, t));
                }
            }

            return new Pose(Quaternion.identity, Vector3.zero);
        }

        /// <summary>
        /// Gathers the knife, the right glove and the right sleeve into one group at the hand's pose, so they can turn together while the left hand
        /// stays. Safe to call more than once; it does nothing if there is no hand or no model yet.
        /// </summary>
        public bool Prepare()
        {
            if (group != null)
            {
                return true;
            }

            if (hand == null)
            {
                return false;
            }

            var parts = new Transform[SwingingParts.Length];
            int found = 0;
            for (int i = 0; i < SwingingParts.Length; i++)
            {
                parts[i] = FindBelow(hand, SwingingParts[i]);
                if (parts[i] != null)
                {
                    found++;
                }
            }

            if (found == 0)
            {
                return false;
            }

            restPosition = hand.localPosition;
            restRotation = hand.localRotation;
            var made = new GameObject("Swing");
            group = made.transform;
            group.SetParent(transform, false);
            group.SetLocalPositionAndRotation(restPosition, restRotation);
            foreach (Transform part in parts)
            {
                if (part != null)
                {
                    part.SetParent(group, true);
                }
            }

            return true;
        }

        /// <summary>Puts the group where the swing is at this time (0, or the duration, is rest).</summary>
        public void ApplyAt(float seconds)
        {
            if (!Prepare())
            {
                return;
            }

            Pose pose = Evaluate(seconds, hitDelay);
            Vector3 relative = restPosition - pivot;
            group.SetLocalPositionAndRotation(pivot + pose.Rotation * relative + pose.Shift, pose.Rotation * restRotation);
        }

        public void SetHitDelay(float seconds)
        {
            hitDelay = Mathf.Max(seconds, 0.05f);
        }

        private void Awake()
        {
            if (weapon != null && weapon.Data != null)
            {
                hitDelay = Mathf.Max(weapon.Data.meleeHitDelay, 0.05f);
            }

            Prepare();
        }

        private void OnEnable()
        {
            if (weapon != null)
            {
                weapon.Swung += Begin;
            }
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.Swung -= Begin;
            }

            time = -1f;
            if (group != null)
            {
                ApplyAt(0f);
            }
        }

        private void Begin()
        {
            time = 0f;
        }

        private void Update()
        {
            if (time < 0f)
            {
                return;
            }

            time += Time.deltaTime;
            if (time >= Duration(hitDelay))
            {
                time = -1f;
                ApplyAt(0f);
                return;
            }

            ApplyAt(time);
        }

        private static Transform FindBelow(Transform root, string objectName)
        {
            foreach (Transform child in root)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform deeper = FindBelow(child, objectName);
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }
    }
}
