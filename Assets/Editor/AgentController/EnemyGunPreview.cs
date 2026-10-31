// Owner: Agent Controller. Review only; images are written outside Assets (EG1).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Agents;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class EnemyGunPreview
    {
        // A graphics device is required: omit -nographics for this method only.
        public static void Render()
        {
            string folder = Path.Combine(Path.GetTempPath(), "blackout-EG1", "preview");
            Directory.CreateDirectory(folder);
            var camera = new GameObject("Enemy gun review camera").AddComponent<Camera>();
            var light = new GameObject("Enemy gun review light").AddComponent<Light>();
            var target = new RenderTexture(1000, 1000, 24);
            var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            camera.orthographic = true;
            camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.26f, .29f, .33f);
            camera.targetTexture = target;
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(35f, -130f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            try
            {
                foreach (string name in EnemyGunBuilder.AgentNames)
                {
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab"));
                    try
                    {
                        var driver = root.GetComponentInChildren<AnimatorDriver>();
                        Animator animator = driver.GetComponent<Animator>();
                        foreach (string pose in new[] { "Aim", "Idle", "Shoot" })
                        {
                            animator.Rebind();
                            animator.SetBool("Aiming", pose != "Idle");
                            int layer = animator.GetLayerIndex("WeaponPose");
                            animator.SetLayerWeight(layer, pose == "Idle" ? 0f : 1f);
                            animator.Play(pose == "Idle" ? "Locomotion" : "Aim", 0, 0f);
                            animator.Update(0f); animator.Update(.2f);
                            if (pose == "Shoot") { animator.Play("Shoot", layer, .25f); animator.Update(0f); }
                            driver.SyncWeaponGrip();
                            foreach (LODGroup lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                            if (pose == "Aim")
                            {
                                Vector3 focus = new Vector3(0f, 1.15f, .2f);
                                camera.transform.position = focus + new Vector3(3f, .6f, 3f);
                                camera.transform.LookAt(focus);
                                camera.Render();
                                RenderTexture.active = target;
                                image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply();
                                RenderTexture.active = null;
                                File.WriteAllBytes(Path.Combine(folder, $"{name}-NativeAim.png"), image.EncodeToPNG());
                            }
                            var baked = new List<(GameObject obj, Mesh mesh, SkinnedMeshRenderer skin)>();
                            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled))
                            {
                                var mesh = new Mesh();
                                skin.BakeMesh(mesh, false);
                                var copy = new GameObject("Review skin", typeof(MeshFilter), typeof(MeshRenderer));
                                copy.transform.SetParent(skin.transform, false);
                                Vector3 scale = skin.transform.lossyScale;
                                copy.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                                copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                                copy.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                                skin.enabled = false;
                                baked.Add((copy, mesh, skin));
                            }
                            foreach (string view in new[] { "ThreeQuarter", "Side" })
                            {
                                Vector3 focus = new Vector3(0f, 1.15f, .2f);
                                camera.transform.position = focus + (view == "Side" ? new Vector3(4f, .15f, 0f) : new Vector3(3f, .6f, 3f));
                                camera.transform.LookAt(focus);
                                camera.Render();
                                RenderTexture.active = target;
                                image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply();
                                RenderTexture.active = null;
                                File.WriteAllBytes(Path.Combine(folder, $"{name}-{pose}-{view}.png"), image.EncodeToPNG());
                            }
                            foreach (var item in baked)
                            {
                                item.skin.enabled = true;
                                Object.DestroyImmediate(item.obj); Object.DestroyImmediate(item.mesh);
                            }
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                RenderTexture.active = null; camera.targetTexture = null;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            }
            Debug.Log("[EG1] Enemy gun review frames: " + folder);
        }
    }
}
