// Owner: Systems Engineer.
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blackout.Mission
{
    /// <summary>
    /// The prototype's Mission Complete screen (ui/menus.js): title, subtitle, TIME / KILLS / DEATHS and a RESTART
    /// button. Restart reloads the Bootstrap scene, which loads the whole mission again from the start.
    /// </summary>
    public class MissionCompleteScreen : MonoBehaviour
    {
        [Tooltip("Hidden until the mission is complete.")]
        [SerializeField] private GameObject root;
        [SerializeField] private Text timeValue;
        [SerializeField] private Text killsValue;
        [SerializeField] private Text deathsValue;
        [SerializeField] private Button restartButton;

        private void Awake()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
            }
        }

        public void Show(float seconds, int kills, int deaths)
        {
            SetText(timeValue, MissionStateMachine.FormatTime(seconds));
            SetText(killsValue, kills.ToString());
            SetText(deathsValue, deaths.ToString());
            if (root != null)
            {
                root.SetActive(true);
            }

            if (restartButton != null)
            {
                restartButton.Select();
            }
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
