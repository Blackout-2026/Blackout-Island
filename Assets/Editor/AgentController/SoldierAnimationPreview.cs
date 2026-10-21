// Owner: Agent Controller. Unity-rendered review frames, saved outside the project (AN1).
using System.Collections.Generic;
using System.IO;
using Blackout.Agents;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class SoldierAnimationPreview
    {
        // Requires a graphics device: run batch mode without -nographics for this preview only.
        public static void Render()
        {
            string folder = Path.Combine(Path.GetTempPath(), "blackout-AN1-preview");
            Directory.CreateDirectory(folder);
            var objects = new List<GameObject>();
            var camera = new GameObject("Animation review camera").AddComponent<Camera>();
            objects.Add(camera.gameObject);
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.transform.position = new Vector3(4.5f, 1.1f, 12f);
            camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.3f, 0.34f, 0.39f);
            var light = new GameObject("Animation review light").AddComponent<Light>();
            objects.Add(light.gameObject);
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35f, -150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            var target = new RenderTexture(1600, 550, 24);
            var image = new Texture2D(1600, 550, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            string[] names = { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" };
            try
            {
                foreach (string pose in new[] { "Idle", "Run", "Aim", "Death" })
                {
                    var agents = new List<GameObject>();
                    for (int i = 0; i < names.Length; i++)
                    {
                        var agent = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{names[i]}.prefab"));
                        agents.Add(agent);
                        agent.transform.position = new Vector3(i * 3f, 0f, 0f);
                        foreach (SkinnedMeshRenderer skin in agent.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                        AnimatorDriver driver = agent.GetComponentInChildren<AnimatorDriver>();
                        Animator animator = driver.GetComponent<Animator>();
                        animator.Rebind();
                        animator.SetFloat("Speed", pose == "Run" ? 4f : 0f);
                        animator.SetBool("Aiming", pose == "Aim");
                        animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), pose == "Aim" ? 1f : 0f);
                        animator.Play(pose == "Death" ? "Death" : pose == "Aim" ? "Aim" : "Locomotion", 0, 0f);
                        animator.Update(0f);
                        animator.Update(pose == "Death" ? 0.7f : 0.18f);
                        driver.SyncWeaponGrip();
                        if (pose == "Aim")
                        {
                            var settings = new SerializedObject(agent.GetComponent<AgentShooter>());
                            var muzzle = (Transform)settings.FindProperty("muzzle").objectReferenceValue;
                            Transform gun = AgentAvatarBuilder.Find(animator.transform, "Gun");
                            Debug.Log($"[AN1] {names[i]} aim muzzle: forward={muzzle.forward}, dot={Vector3.Dot(muzzle.forward, agent.transform.forward)}, gun={(gun != null ? gun.name : "pod")}; gun renderers={(gun != null ? string.Join(",", System.Array.ConvertAll(gun.GetComponentsInChildren<Renderer>(), r => r.name)) : "pod")}");
                        }
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1600, 550), 0, 0);
                    image.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(folder, pose + ".png"), image.EncodeToPNG());
                    foreach (GameObject agent in agents) Object.DestroyImmediate(agent);
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                foreach (GameObject obj in objects) Object.DestroyImmediate(obj);
            }
            Debug.Log("[AN1] Animation review frames: " + folder);
        }
    }
}
