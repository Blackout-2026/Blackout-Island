// Owner: Systems Engineer.
using System.Collections;
using Blackout.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Mission
{
    /// <summary>
    /// Bootstrap.unity's loader: loads the level scenes additively, makes Environment the active scene (fog, skybox
    /// and ambient light come from the active scene), puts the player at the PlayerStart marker and starts the
    /// mission. A scene that is not in Build Settings is skipped with a warning, so the game runs before every
    /// role's scene exists. A scene that is already open (opened by hand for testing) is not loaded twice.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Tooltip("Loaded additively, in this order.")]
        [SerializeField] private string[] scenes = { "Environment", "Interactables", "Agents" };
        [Tooltip("The scene whose lighting settings are used.")]
        [SerializeField] private string activeScene = "Environment";
        [Tooltip("Saved inactive in Bootstrap so it cannot fall before the ground has loaded.")]
        [SerializeField] private GameObject player;
        [SerializeField] private GameObject loadingScreen;
        [SerializeField] private MissionDirector director;

        public bool IsLoaded { get; private set; }

        private IEnumerator Start()
        {
            if (player == null || director == null)
            {
                Debug.LogError("[Blackout] SceneBootstrap needs its player and director assigned.", this);
                yield break;
            }

            SetLoadingScreen(true);
            foreach (string sceneName in scenes)
            {
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    continue;
                }

                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogWarning($"[Blackout] Scene '{sceneName}' is not in Build Settings; skipped. " +
                                     "Run Blackout/Systems/Build Bootstrap Scene once it exists.", this);
                    continue;
                }

                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }

            Scene active = SceneManager.GetSceneByName(activeScene);
            if (active.isLoaded)
            {
                SceneManager.SetActiveScene(active);
            }
            else
            {
                Debug.LogWarning($"[Blackout] '{activeScene}' did not load, so its lighting is not used.", this);
            }

            PlacePlayerAtStart();
            player.SetActive(true);
            SetLoadingScreen(false);
            IsLoaded = true;
            director.Begin(player);
        }

        /// <summary>Moves the (still inactive) player onto the PlayerStart marker, facing its yaw.</summary>
        private void PlacePlayerAtStart()
        {
            foreach (Marker marker in FindObjectsByType<Marker>(FindObjectsSortMode.None))
            {
                if (marker.Type == MarkerType.PlayerStart)
                {
                    Transform start = marker.transform;
                    player.transform.SetPositionAndRotation(start.position,
                        Quaternion.Euler(0f, start.eulerAngles.y, 0f));
                    return;
                }
            }

            Debug.LogWarning("[Blackout] No PlayerStart marker; the player starts where Bootstrap put it.", this);
        }

        private void SetLoadingScreen(bool visible)
        {
            if (loadingScreen != null)
            {
                loadingScreen.SetActive(visible);
            }
        }
    }
}
