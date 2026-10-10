using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Shared.Tests
{
    public class EventChannelTests
    {
        [Test]
        public void VoidChannel_Raise_CallsEverySubscriber()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            int calls = 0;
            channel.OnRaised += () => calls++;
            channel.OnRaised += () => calls++;

            channel.Raise();

            Assert.AreEqual(2, calls);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void VoidChannel_Unsubscribed_IsNotCalled()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            int calls = 0;
            System.Action handler = () => calls++;
            channel.OnRaised += handler;
            channel.OnRaised -= handler;

            channel.Raise();

            Assert.AreEqual(0, calls);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void VoidChannel_RaiseWithNoSubscribers_DoesNotThrow()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();

            Assert.DoesNotThrow(() => channel.Raise());
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void NoiseChannel_Raise_PassesTheNoiseThrough()
        {
            var channel = ScriptableObject.CreateInstance<NoiseEventChannel>();
            NoiseEvent received = default;
            channel.OnRaised += noise => received = noise;

            channel.Raise(new NoiseEvent(new Vector3(1f, 2f, 3f), 25f, NoiseType.Gunshot, null));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), received.position);
            Assert.AreEqual(25f, received.loudness);
            Assert.AreEqual(NoiseType.Gunshot, received.type);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void StringChannel_Raise_PassesTheTextThrough()
        {
            var channel = ScriptableObject.CreateInstance<StringEventChannel>();
            string received = null;
            channel.OnRaised += text => received = text;

            channel.Raise("Disable the generator");

            Assert.AreEqual("Disable the generator", received);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void GameEventListener_InvokesResponseOnlyWhileEnabled()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            var gameObject = new GameObject("ListenerTest");
            var listener = gameObject.AddComponent<GameEventListener>();
            int calls = 0;
            listener.Response.AddListener(() => calls++);

            // Assign the channel the same way the Inspector does.
            var serialized = new SerializedObject(listener);
            serialized.FindProperty("channel").objectReferenceValue = channel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // OnEnable/OnDisable do not run by themselves in EditMode, so call them as Unity would.
            CallUnityMessage(listener, "OnEnable");
            channel.Raise();
            CallUnityMessage(listener, "OnDisable");
            channel.Raise();

            Assert.AreEqual(1, calls);
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(channel);
        }

        private static void CallUnityMessage(Object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
