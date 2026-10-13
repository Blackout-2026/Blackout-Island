// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// Writes a line to the Console. Wire a GameEventListener's response to <see cref="Log"/> to prove an event
    /// fired without writing code, for example the GeneratorDisabled listener in the Interactables scene.
    /// </summary>
    public class DebugEventLogger : MonoBehaviour
    {
        public void Log(string message)
        {
            Debug.Log("[Blackout] " + message, this);
        }
    }
}
