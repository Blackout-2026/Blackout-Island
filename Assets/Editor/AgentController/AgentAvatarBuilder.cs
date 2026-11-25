// Owner: Agent Controller. Explicit rig mappings for soldier animation retargeting (AN1).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    public static class AgentAvatarBuilder
    {
        private static readonly (HumanBodyBones human, string rigid)[] Bones =
        {
            (HumanBodyBones.Hips, "Hips"),
            (HumanBodyBones.Spine, "Spine"),
            (HumanBodyBones.Chest, "Chest"),
            (HumanBodyBones.Neck, "Neck"),
            (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.LeftUpperArm, "UpperArm_L"),
            (HumanBodyBones.LeftLowerArm, "ForeArm_L"),
            (HumanBodyBones.LeftHand, "Hand_L"),
            (HumanBodyBones.RightUpperArm, "UpperArm_R"),
            (HumanBodyBones.RightLowerArm, "ForeArm_R"),
            (HumanBodyBones.RightHand, "Hand_R"),
            (HumanBodyBones.LeftUpperLeg, "Thigh_L"),
            (HumanBodyBones.LeftLowerLeg, "Shin_L"),
            (HumanBodyBones.LeftFoot, "Foot_L"),
            (HumanBodyBones.RightUpperLeg, "Thigh_R"),
            (HumanBodyBones.RightLowerLeg, "Shin_R"),
            (HumanBodyBones.RightFoot, "Foot_R"),
        };

        public static Avatar Build(GameObject model, string agentName)
        {
            bool robot = agentName == "TacticalGuard";
            var mapped = new Dictionary<HumanBodyBones, Transform>();
            var human = new List<HumanBone>();
            foreach (var bone in Bones)
            {
                string name = robot && bone.rigid == "Hips" ? "Pelvis" : bone.rigid;
                Transform found = Find(model.transform, name);
                if (found == null && bone.human == HumanBodyBones.Neck) continue; // robot has no neck
                if (found == null) throw new InvalidOperationException($"{agentName}: missing required bone {name}.");
                mapped.Add(bone.human, found);
                human.Add(new HumanBone { boneName = name, humanName = HumanTrait.BoneName[(int)bone.human], limit = new HumanLimit { useDefaultValues = true } });
            }

            var skeletonTransforms = new HashSet<Transform> { model.transform };
            foreach (Transform bone in mapped.Values)
                for (Transform ancestor = bone; ancestor != null && ancestor != model.transform; ancestor = ancestor.parent)
                    skeletonTransforms.Add(ancestor);
            foreach (Transform bone in skeletonTransforms)
            {
                if (bone == model.transform) continue;
                Transform source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(bone);
                if (source == null) continue;
                bone.localPosition = source.localPosition;
                bone.localRotation = source.localRotation;
                bone.localScale = source.localScale;
            }

            // Build a real T-pose from the imported joints. No FBX or mesh bind poses are regenerated.
            Vector3 right = mapped[HumanBodyBones.RightUpperLeg].position - mapped[HumanBodyBones.LeftUpperLeg].position;
            right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            Align(mapped, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -right);
            Align(mapped, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -right);
            Align(mapped, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, right);
            Align(mapped, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, right);

            var description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = model.GetComponentsInChildren<Transform>(true).Where(skeletonTransforms.Contains).Select(t => new SkeletonBone
                {
                    name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
                }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f,
            };
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(model, description);
            if (!avatar.isValid || !avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                throw new InvalidOperationException($"{agentName}: Unity could not build a valid Humanoid avatar.");
            }
            avatar.name = agentName + "_SoldierAvatar";
            string path = "Assets/Animations/" + avatar.name + ".asset";
            Avatar existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (existing == null) AssetDatabase.CreateAsset(avatar, path);
            else
            {
                EditorUtility.CopySerialized(avatar, existing);
                UnityEngine.Object.DestroyImmediate(avatar);
                EditorUtility.SetDirty(existing);
                avatar = existing;
            }
            return avatar;
        }

        private static void Align(Dictionary<HumanBodyBones, Transform> bones, HumanBodyBones parent, HumanBodyBones child, Vector3 direction)
        {
            Transform joint = bones[parent];
            joint.rotation = Quaternion.FromToRotation(bones[child].position - joint.position, direction) * joint.rotation;
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
