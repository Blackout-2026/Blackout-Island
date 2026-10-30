// Owner: Systems Engineer. TF1: bake existing index-joint weights into a holding-mesh blend shape.
using System;
using System.IO;
using System.Linq;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class TriggerFingerBuilder
    {
        public const string MeshPrefix = "Assets/Models/Custom/SM_TriggerFinger_";

        [MenuItem("Blackout/Systems/Add Trigger Finger Motion")]
        public static void Build()
        {
            var player = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                var holder = player.transform.Find(ViewmodelBuilder.HolderPath);
                foreach (string label in new[] { "Smg", "Rifle" })
                {
                    Transform gun = holder.Find(label == "Smg" ? "SMG" : "Rifle");
                    var glove = gun.Find("Visual").GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "GloveR");
                    Mesh mesh = Bake(label, glove.sharedMesh);
                    string path = MeshPrefix + label + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }

                    Transform visual = glove.transform.Find("TriggerFingerVisual");
                    if (visual == null)
                    {
                        visual = new GameObject("TriggerFingerVisual").transform;
                        visual.SetParent(glove.transform, false);
                    }
                    visual.gameObject.layer = glove.gameObject.layer;
                    var skin = visual.GetComponent<SkinnedMeshRenderer>();
                    if (skin == null) skin = visual.gameObject.AddComponent<SkinnedMeshRenderer>();
                    var original = glove.GetComponent<MeshRenderer>();
                    skin.sharedMesh = saved;
                    skin.sharedMaterials = original.sharedMaterials;
                    skin.bones = new[] { visual };
                    skin.rootBone = visual;
                    skin.localBounds = saved.bounds;
                    skin.shadowCastingMode = original.shadowCastingMode;
                    skin.receiveShadows = original.receiveShadows;
                    skin.updateWhenOffscreen = true;
                    original.enabled = false; // Preserve the accepted mesh and its references for rollback.
                    var motion = gun.Find("Visual").GetComponent<TriggerFinger>();
                    if (motion == null) motion = gun.Find("Visual").gameObject.AddComponent<TriggerFinger>();
                    PlayerPrefabBuilder.Assign(motion, "weapons", holder.GetComponent<WeaponController>());
                    PlayerPrefabBuilder.Assign(motion, "data", gun.GetComponent<HitscanWeapon>().Data);
                    PlayerPrefabBuilder.Assign(motion, "hands", holder.GetComponent<ViewmodelHands>());
                    PlayerPrefabBuilder.Assign(motion, "input", player.GetComponent<Blackout.Player.PlayerInputReader>());
                    PlayerPrefabBuilder.Assign(motion, "fingerVisual", skin);
                    var settings = new SerializedObject(motion);
                    settings.FindProperty("returnTime").floatValue = .14f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("[TF1] Installed index-only trigger pulls; original meshes, grips and reload rigs retained.");
        }

        private static Mesh Bake(string label, Mesh holding)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WinterArmsBuilder.ModelPrefix + label + ".fbx"));
            var before = new Mesh();
            var after = new Mesh();
            try
            {
                var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Winter" + label + "R");
                if (skin.sharedMesh != holding) throw new InvalidOperationException("Holding arm must match the accepted Winter derivative.");
                skin.BakeMesh(before, true);
                int distalIndex = Array.FindIndex(skin.bones, b => b.name == "Index3R");
                Vector3[] vertices = before.vertices;
                BoneWeight[] weights = holding.boneWeights;
                Vector3 tip = Vector3.zero;
                float total = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float amount = (w.boneIndex0 == distalIndex ? w.weight0 : 0) + (w.boneIndex1 == distalIndex ? w.weight1 : 0)
                        + (w.boneIndex2 == distalIndex ? w.weight2 : 0) + (w.boneIndex3 == distalIndex ? w.weight3 : 0);
                    tip += vertices[i] * amount; total += amount;
                }
                if (total <= 0) throw new InvalidOperationException("Missing weighted index fingertip.");
                tip = skin.transform.TransformPoint(tip / total);
                // Existing phalanx pivots/weights define the bend. Move toward the rear of this gun,
                // away from its muzzle (+Z), rather than guessing an Euler axis on a different rig.
                // The fitted rifle finger is longer: use smaller angles for the same few mm of travel.
                foreach (var joint in new[] { ("Index2R", label == "Smg" ? 8f : 2f), ("Index3R", label == "Smg" ? 5f : 1.5f) })
                {
                    Transform bone = skin.bones.Single(b => b.name == joint.Item1);
                    Vector3 axis = Vector3.Cross(tip - bone.position, -skin.transform.forward).normalized;
                    if (axis.sqrMagnitude < .5f) throw new InvalidOperationException("Cannot determine index bend plane.");
                    Quaternion pull = Quaternion.AngleAxis(joint.Item2, axis);
                    tip = bone.position + pull * (tip - bone.position);
                    bone.rotation = pull * bone.rotation;
                }
                skin.BakeMesh(after, true);
                Vector3[] a = before.vertices, b = after.vertices, n0 = before.normals, n1 = after.normals;
                var delta = new Vector3[a.Length];
                var normals = new Vector3[a.Length];
                float max = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    delta[i] = b[i] - a[i]; normals[i] = n1[i] - n0[i];
                    max = Mathf.Max(max, delta[i].magnitude);
                }
                if (float.IsNaN(max) || max < .0005f || max > .012f) throw new InvalidOperationException("Unexpected trigger-finger travel: " + max);
                var mesh = Object.Instantiate(holding);
                mesh.name = "TriggerFinger" + label;
                mesh.ClearBlendShapes();
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, mesh.vertexCount).ToArray();
                mesh.AddBlendShapeFrame(TriggerFinger.ShapeName, 100, delta, normals, null);
                Debug.Log($"[TF1] {label}: {a.Length} vertices, index maximum travel {max * 1000:F3} mm.");
                return mesh;
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
        }

        // Disposable review scene/cameras only; no gameplay scene is saved.
        public static void BuildAndPreview() { Build(); Preview(); }

        public static void Preview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath);
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f,.2f,.23f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            var light = new GameObject("Finger review light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(30,-35,0);
            var target = new RenderTexture(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var baked = new Mesh();
            string folder = Path.Combine(Path.GetTempPath(),"blackout-TF1","preview");
            Directory.CreateDirectory(folder);
            camera.targetTexture = target;
            Vector3 eye = camera.transform.position;
            Quaternion eyeRotation = camera.transform.rotation;
            try
            {
                foreach (string name in new[] { "SMG", "Rifle" })
                {
                    foreach (string other in new[] { "SMG", "Rifle", "Knife" }) holder.Find(other).gameObject.SetActive(other == name);
                    Transform gun = holder.Find(name);
                    var skin = gun.Find("Visual").GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                    // Explicit CPU bake gives distinct review frames in this single Editor frame.
                    // Runtime uses the SkinnedMeshRenderer directly, without baking or allocating.
                    var frozen = new GameObject("Frozen finger review frame");
                    frozen.transform.SetParent(skin.transform,false);
                    frozen.layer = skin.gameObject.layer;
                    frozen.AddComponent<MeshFilter>().sharedMesh = baked;
                    frozen.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    camera.transform.SetParent(null,true);
                    holder.SetParent(null,true);
                    Vector3 centre = gun.Find("WinterReloadVisual").GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Index3R").position;
                    foreach (string view in new[] { "hip", "right", "left", "front" })
                    {
                        camera.transform.position = view == "hip" ? eye : centre + gun.TransformDirection(view == "front" ? new Vector3(.08f,.03f,.5f) : new Vector3(view == "right" ? .45f : -.45f,.02f,.1f));
                        camera.transform.rotation = view == "hip" ? eyeRotation : Quaternion.LookRotation(centre-camera.transform.position,gun.up);
                        camera.orthographic = view != "hip";
                        camera.orthographicSize = .115f;
                        camera.nearClipPlane = .005f;
                        foreach (int weight in new[] { 0,100 })
                        {
                            skin.SetBlendShapeWeight(0,weight);
                            skin.BakeMesh(baked,true);
                            camera.Render(); RenderTexture.active = target;
                            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                            File.WriteAllBytes(Path.Combine(folder,name+"-"+view+"-"+weight+".png"),image.EncodeToPNG());
                        }
                    }
                    Object.DestroyImmediate(frozen);
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = null; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(holder.gameObject); Object.DestroyImmediate(player); Object.DestroyImmediate(camera.gameObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
