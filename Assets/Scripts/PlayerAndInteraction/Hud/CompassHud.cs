// Owner: Systems Engineer.
using Blackout.Mission;
using Blackout.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>Scrolling headings and a gold objective pointer, matching prototype/src/ui/hud.js.</summary>
    public sealed class CompassHud : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform strip;
        [SerializeField] private RectTransform objectiveMarker;
        [SerializeField] private Text heading;
        [SerializeField] private Text destination;

        private MissionDirector director;
        private float retryAt;
        private int shownHeading = -1;
        private int shownDistance = -1;
        private string shownLabel;

        private void LateUpdate()
        {
            if (player == null || root == null) return;
            if (director == null && Time.unscaledTime >= retryAt)
            {
                director = FindFirstObjectByType<MissionDirector>();
                retryAt = Time.unscaledTime + 1f;
            }

            bool visible = director != null && director.IsRunning && director.Phase != MissionPhase.Complete
                && (playerHealth == null || !playerHealth.IsDead);
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            float bearing = CompassMath.Bearing(player.forward);
            strip.anchoredPosition = new Vector2(230f - bearing * CompassMath.PixelsPerDegree, 0f);
            int rounded = Mathf.RoundToInt(bearing) % 360;
            if (shownHeading != rounded)
            {
                shownHeading = rounded;
                heading.text = rounded.ToString("000") + "°";
            }

            bool hasTarget = director.TryGetNavigationTarget(out Vector3 target, out string label);
            objectiveMarker.gameObject.SetActive(hasTarget);
            destination.enabled = hasTarget;
            if (!hasTarget) return;
            Vector3 direction = target - player.position;
            objectiveMarker.anchoredPosition = new Vector2(CompassMath.MarkerOffset(bearing, direction), -18f);
            int metres = Mathf.RoundToInt(new Vector2(direction.x, direction.z).magnitude);
            if (metres != shownDistance || label != shownLabel)
            {
                shownDistance = metres;
                shownLabel = label;
                destination.text = label + " · " + metres + " m";
            }
        }
    }
}
