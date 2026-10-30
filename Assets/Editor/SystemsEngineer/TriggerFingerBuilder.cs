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

        public static void InspectTriggerGeometry()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            try
            {
                string folder = Path.Combine(Path.GetTempPath(),"blackout-TF2"); Directory.CreateDirectory(folder);
                foreach(string name in new[] {"SMG","Rifle"})
                {
                    Transform gun = root.transform.Find(ViewmodelBuilder.HolderPath+"/"+name);
                    var filter = gun.Find("Visual").GetComponentsInChildren<MeshFilter>(true).First(f=>!ViewmodelHands.IsArmPart(f.name) && f.sharedMesh.vertexCount>1000);
                    var vertices = filter.sharedMesh.vertices.Select(v=>gun.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                    string points = string.Join("\n",vertices.Select(v=>$"{v.x:R},{v.y:R},{v.z:R}"));
                    File.WriteAllText(Path.Combine(folder,name+"-vertices.csv"),points);
                    File.WriteAllText(Path.Combine(folder,name+"-triangles.csv"),string.Join(",",filter.sharedMesh.triangles));
                    var rig=gun.Find("WinterReloadVisual");
                    File.WriteAllText(Path.Combine(folder,name+"-joints.txt"),string.Join("\n",rig.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Index")&&t.name.EndsWith("R")).Select(t=>t.name+" "+gun.InverseTransformPoint(t.position).ToString("F6"))));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

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
                    Vector3 contact = FindTrigger(gun);
                    string path = MeshPrefix + label + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    Matrix4x4 meshToGun = gun.worldToLocalMatrix * glove.transform.localToWorldMatrix;
                    Mesh mesh = Bake(label, glove.sharedMesh, contact, meshToGun, saved);
                    if (saved == null) AssetDatabase.CreateAsset(mesh, path);
                    else EditorUtility.SetDirty(mesh);
                    saved = mesh;

                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
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
                BuildSupportThumb(holder.Find("SMG"));
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("[TF1] Installed index-only trigger pulls; original meshes, grips and reload rigs retained.");
        }

        [MenuItem("Blackout/Systems/Wrap SMG Support Thumb")]
        public static void BuildThumbOnly()
        {
            var root=PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                BuildSupportThumb(root.transform.Find(ViewmodelBuilder.HolderPath+"/SMG"));
                PrefabUtility.SaveAsPrefabAsset(root,PlayerPrefabBuilder.PrefabPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }

        public static void BuildThumbAndPreview() {BuildThumbOnly();Preview();}

        private static void BuildSupportThumb(Transform gun)
        {
            const string path = "Assets/Models/Custom/SM_SmgThumb.asset";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WinterArmsBuilder.ModelPrefix+"Smg.fbx");
            Mesh source = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="WinterSmgL").sharedMesh;
            var glove = gun.Find("Visual").GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="GloveL");
            Vector3 contact = FindContact(gun,new Vector3(0,-.055f,.21f),Vector3.forward,"LeftThumbContact");
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh fitted = Bake("Smg",source,contact,gun.worldToLocalMatrix*glove.transform.localToWorldMatrix,saved,true);
            fitted.name = "SmgSupportThumb";
            if(saved==null) AssetDatabase.CreateAsset(fitted,path);
            else EditorUtility.SetDirty(fitted);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            glove.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Vector3 FindTrigger(Transform gun)
        {
            bool smg = gun.name=="SMG";
            return FindContact(gun,new Vector3(0,smg ? -.036f : -.062f,smg ? .213f : -.047f),Vector3.back,"RightIndexContact");
        }

        // These rays begin inside the measured guard or beside the foregrip.
        private static Vector3 FindContact(Transform gun,Vector3 origin,Vector3 direction,string name)
        {
            var filter = gun.Find("Visual").GetComponentsInChildren<MeshFilter>(true)
                .First(f=>!ViewmodelHands.IsArmPart(f.name) && f.sharedMesh.vertexCount>1000);
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            try
            {
                Physics.SyncTransforms();
                if(!collider.Raycast(new Ray(gun.TransformPoint(origin),gun.TransformDirection(direction)),out RaycastHit hit,.3f))
                    throw new InvalidOperationException("Cannot find gun contact: "+name);
                Vector3 contact = gun.InverseTransformPoint(hit.point);
                Transform marker = gun.Find(name);
                if(marker==null) { marker=new GameObject(name).transform;marker.SetParent(gun,false);marker.gameObject.layer=gun.gameObject.layer; }
                marker.localPosition=contact;
                Debug.Log("[TF2] "+name+" "+contact.ToString("F6"));
                return contact;
            }
            finally {Object.DestroyImmediate(collider);}
        }

        // Offline CCD rotates the existing phalanges; it never scales or translates a joint.
        private static Matrix4x4[] FitFinger(Vector3[] chain, Vector3 goal)
        {
            var pose = new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity };
            float[] lengths = Enumerable.Range(0,3).Select(i=>Vector3.Distance(chain[i],chain[i+1])).ToArray();
            for (int iteration = 0; iteration < 32; iteration++)
            {
                if (Vector3.Distance(chain[3], goal) < .0005f) break;
                for (int joint = 2; joint >= 0; joint--)
                {
                    Quaternion desired = Quaternion.FromToRotation(chain[3] - chain[joint], goal - chain[joint]);
                    Quaternion step = Quaternion.RotateTowards(Quaternion.identity, desired, 6f);
                    // Limit deviation from the approved finger pose, avoiding a flipped fingertip.
                    Quaternion previous = pose[joint].rotation;
                    Quaternion limited = Quaternion.RotateTowards(Quaternion.identity, step * previous, joint == 0 ? 40f : 65f);
                    step = limited * Quaternion.Inverse(previous);
                    Matrix4x4 turn = Matrix4x4.Translate(chain[joint]) * Matrix4x4.Rotate(step) * Matrix4x4.Translate(-chain[joint]);
                    for (int child = joint + 1; child < 4; child++) chain[child] = turn.MultiplyPoint3x4(chain[child]);
                    for (int child = joint; child < 3; child++) pose[child] = turn * pose[child];
                }
            }
            for(int i=0;i<3;i++)
                if(Mathf.Abs(lengths[i]-Vector3.Distance(chain[i],chain[i+1]))>1e-5f)
                    throw new InvalidOperationException("Finger fit changed a phalanx length.");
            return pose;
        }

        // A tip-only solve can fold the middle joint upright. Author the wrap arc instead:
        // left back corner -> across the back face -> right back corner.
        private static Matrix4x4[] WrapThumb(Vector3[] chain,Vector3 backContact,Matrix4x4 meshToGun)
        {
            Vector3[] original=(Vector3[])chain.Clone();
            Vector3[] guides={backContact+new Vector3(-.020f,0,.003f),
                backContact+new Vector3(.004f,0,-.006f),backContact+new Vector3(.016f,-.005f,-.001f)};
            var pose=new Matrix4x4[3];
            for(int joint=0;joint<3;joint++)
            {
                Vector3 direction=(meshToGun.inverse.MultiplyPoint3x4(guides[joint])-chain[joint]).normalized;
                float length=Vector3.Distance(original[joint],original[joint+1]);
                chain[joint+1]=chain[joint]+direction*length;
                Quaternion turn=Quaternion.FromToRotation(original[joint+1]-original[joint],chain[joint+1]-chain[joint]);
                pose[joint]=Matrix4x4.Translate(chain[joint])*Matrix4x4.Rotate(turn)*Matrix4x4.Translate(-original[joint]);
                if(Mathf.Abs(Vector3.Distance(chain[joint],chain[joint+1])-length)>1e-5f)
                    throw new InvalidOperationException("Thumb wrap changed a segment length.");
            }
            Vector3[] gunPoints=chain.Select(meshToGun.MultiplyPoint3x4).ToArray();
            string folder=Path.Combine(Path.GetTempPath(),"blackout-TF2-thumb-wrap"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"thumb-chain.txt"),string.Join("\n",gunPoints.Select(v=>v.ToString("F6"))));
            return pose;
        }

        private static Mesh Bake(string label, Mesh holding, Vector3 contact, Matrix4x4 meshToGun, Mesh existing, bool supportThumb = false)
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WinterArmsBuilder.ModelPrefix+(supportThumb ? "Smg" : label)+".fbx"));
            try
            {
                var skin=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Winter"+(supportThumb ? "SmgL" : label+"R"));
                Vector3[] source=holding.vertices, normal=holding.normals;
                BoneWeight[] weights=holding.boneWeights;
                int[] joints=Enumerable.Range(1,3).Select(j=>Array.FindIndex(skin.bones,b=>b.name==(supportThumb ? "Thumb"+j+"L" : "Index"+j+"R"))).ToArray();
                Vector3[] old=joints.Select(i=>holding.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();
                Debug.Log("[TF2] Native "+(supportThumb ? "thumb" : "index")+" pivots "+string.Join(", ",old.Select(v=>v.ToString("F6"))));
                float Weight(BoneWeight w,int joint) => (w.boneIndex0==joint?w.weight0:0)+(w.boneIndex1==joint?w.weight1:0)+(w.boneIndex2==joint?w.weight2:0)+(w.boneIndex3==joint?w.weight3:0);
                float total=0; Vector3 pad=Vector3.zero;
                for(int i=0;i<source.Length;i++) { float w=Weight(weights[i],joints[2]);pad+=source[i]*w;total+=w; }
                pad/=total;
                bool smg=label=="Smg";
                Vector3[] target = { old[0], old[1], old[2], pad };
                Vector3 goal = meshToGun.inverse.MultiplyPoint3x4(contact + new Vector3(-.006f,-.001f,.003f));
                Matrix4x4[] fit = supportThumb ? WrapThumb(target,contact,meshToGun)
                    : smg ? FitFinger(target,goal) : new[] {Matrix4x4.identity,Matrix4x4.identity,Matrix4x4.identity};
                Vector3 rear = meshToGun.inverse.MultiplyVector(Vector3.back).normalized;
                var rest = new Vector3[source.Length];
                var normals = new Vector3[source.Length];
                void ApplyFit()
                {
                    for (int i = 0; i < rest.Length; i++)
                    {
                        rest[i] = source[i]; normals[i] = normal[i];
                        for (int joint = 0; joint < 3; joint++)
                        {
                            float weight = Weight(weights[i], joints[joint]);
                            rest[i] += (fit[joint].MultiplyPoint3x4(source[i]) - source[i]) * weight;
                            normals[i] += (fit[joint].MultiplyVector(normal[i]) - normal[i]) * weight;
                        }
                        normals[i].Normalize();
                    }
                }
                ApplyFit();
                // Refine skin contact by rerunning joint rotations, never by pulling vertices.
                if(smg && !supportThumb)
                {
                    Vector3 touch=meshToGun.inverse.MultiplyPoint3x4(contact);
                    int[] triangles=holding.triangles;
                    for(int pass=0;pass<4;pass++)
                    {
                        float best=float.PositiveInfinity;
                        Vector3 nearest=touch;
                        for(int t=0;t<triangles.Length;t+=3)
                        {
                            int ia=triangles[t],ib=triangles[t+1],ic=triangles[t+2];
                            if(Mathf.Max(Weight(weights[ia],joints[2]),Mathf.Max(Weight(weights[ib],joints[2]),Weight(weights[ic],joints[2])))<.25f) continue;
                            Vector3 va=rest[ia],vb=rest[ib],vc=rest[ic],n=Vector3.Cross(vb-va,vc-va).normalized;
                            if(n.sqrMagnitude<.5f) continue;
                            Vector3 point=touch-n*Vector3.Dot(touch-va,n);
                            if(Vector3.Dot(Vector3.Cross(vb-va,point-va),n)<0 || Vector3.Dot(Vector3.Cross(vc-vb,point-vb),n)<0 || Vector3.Dot(Vector3.Cross(va-vc,point-vc),n)<0) continue;
                            float distance=Vector3.Distance(point,touch);
                            if(distance<best) { best=distance;nearest=point; }
                        }
                        if(best<.00075f || float.IsInfinity(best)) break;
                        goal+=touch-nearest;
                        target=new[]{old[0],old[1],old[2],pad};
                        fit=FitFinger(target,goal);
                        ApplyFit();
                    }
                }
                if(supportThumb) Debug.Log("[TF2] Thumb wrap arc fitted; original segment lengths retained.");
                else if(smg) Debug.Log("[TF2] Rotation-only trigger index, pad-centre error "+Vector3.Distance(target[3],goal).ToString("F6"));
                Matrix4x4 Bend(Vector3 pivot,Vector3 end,float degrees) => Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.AngleAxis(degrees,Vector3.Cross(end-pivot,rear).normalized))*Matrix4x4.Translate(-pivot);
                Matrix4x4 bend2=Bend(target[1],target[3],smg ? 12 : 2),bend3=bend2*Bend(target[2],target[3],smg ? 8 : 1.5f);
                var delta=new Vector3[rest.Length];var normalDelta=new Vector3[rest.Length];
                for(int i=0;i<rest.Length;i++)
                {
                    float w2=Weight(weights[i],joints[1]),w3=Weight(weights[i],joints[2]);
                    delta[i]=(bend2.MultiplyPoint3x4(rest[i])-rest[i])*w2+(bend3.MultiplyPoint3x4(rest[i])-rest[i])*w3;
                    normalDelta[i]=(bend2.MultiplyVector(normals[i])-normals[i])*w2+(bend3.MultiplyVector(normals[i])-normals[i])*w3;
                }
                // Rebuild native buffers directly; serialized field copies can retain stale FBX vertices.
                var mesh=existing != null ? existing : new Mesh();
                mesh.Clear(false); mesh.name="TriggerFinger"+label; mesh.indexFormat=holding.indexFormat;
                mesh.ClearBlendShapes();
                mesh.vertices=rest;mesh.normals=normals;mesh.tangents=holding.tangents;
                mesh.uv=holding.uv;mesh.uv2=holding.uv2;
                mesh.subMeshCount=holding.subMeshCount;
                for(int sub=0;sub<holding.subMeshCount;sub++) mesh.SetTriangles(holding.GetTriangles(sub),sub);
                mesh.bindposes=supportThumb ? holding.bindposes : new[]{Matrix4x4.identity};
                mesh.boneWeights=supportThumb ? holding.boneWeights : Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},mesh.vertexCount).ToArray();
                if(!supportThumb) mesh.AddBlendShapeFrame(TriggerFinger.ShapeName,100,delta,normalDelta,null);
                mesh.RecalculateBounds();
                Debug.Log(supportThumb ? "[TF2] Static support-thumb holding mesh fitted." : $"[TF2] {label} trigger pull {delta.Max(v=>v.magnitude)*1000:F3} mm; rifle rest pose retained.");
                return mesh;
            }
            finally {Object.DestroyImmediate(model);}
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
            string folder = Path.Combine(Path.GetTempPath(),"blackout-TF2-thumb-wrap","preview");
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
