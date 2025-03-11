using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ヒューマノイドリグの取得と解析を行うクラス
    /// </summary>
    public class HumanoidRigRetriever
    {
        /// <summary>
        /// 指定されたゲームオブジェクトからヒューマノイドボーン情報を取得
        /// </summary>
        public static Dictionary<HumanBodyBones, Transform> RetrieveHumanoidBones(GameObject gameObject)
        {
            Dictionary<HumanBodyBones, Transform> boneMap = new Dictionary<HumanBodyBones, Transform>();
            
            if (gameObject == null)
                return boneMap;
            
            Animator animator = gameObject.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return boneMap;
            
            // すべてのヒューマノイドボーンを取得
            foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                    continue;
                
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null)
                {
                    boneMap[bone] = boneTransform;
                }
            }
            
            return boneMap;
        }

        /// <summary>
        /// ヒューマノイドボーンの階層情報を取得
        /// </summary>
        public static HumanoidBoneHierarchy BuildBoneHierarchy(GameObject gameObject)
        {
            HumanoidBoneHierarchy hierarchy = new HumanoidBoneHierarchy();
            Dictionary<HumanBodyBones, Transform> boneMap = RetrieveHumanoidBones(gameObject);
            
            if (boneMap.Count == 0)
                return hierarchy;
            
            // Hipsをルートとして階層情報を構築
            if (boneMap.TryGetValue(HumanBodyBones.Hips, out Transform hipsTransform))
            {
                hierarchy.Root = new HumanoidBoneNode(HumanBodyBones.Hips, hipsTransform);
                BuildBoneHierarchyRecursive(hierarchy.Root, boneMap);
            }
            
            return hierarchy;
        }

        /// <summary>
        /// 再帰的に階層情報を構築
        /// </summary>
        private static void BuildBoneHierarchyRecursive(HumanoidBoneNode parentNode, Dictionary<HumanBodyBones, Transform> boneMap)
        {
            foreach (var bone in GetChildBones(parentNode.Bone))
            {
                if (boneMap.TryGetValue(bone, out Transform boneTransform))
                {
                    // 実際の階層関係を確認
                    if (IsActualChild(parentNode.Transform, boneTransform))
                    {
                        HumanoidBoneNode childNode = new HumanoidBoneNode(bone, boneTransform);
                        parentNode.Children.Add(childNode);
                        childNode.Parent = parentNode;
                        
                        BuildBoneHierarchyRecursive(childNode, boneMap);
                    }
                }
            }
        }

        /// <summary>
        /// 実際のTransform階層でchildがparentの子孫であるか確認
        /// </summary>
        private static bool IsActualChild(Transform parent, Transform child)
        {
            if (child == null || parent == null)
                return false;
            
            Transform current = child.parent;
            while (current != null)
            {
                if (current == parent)
                    return true;
                
                current = current.parent;
            }
            
            return false;
        }

        /// <summary>
        /// 指定されたボーンの子ボーンを論理的な階層関係に基づいて取得
        /// </summary>
        private static List<HumanBodyBones> GetChildBones(HumanBodyBones bone)
        {
            List<HumanBodyBones> children = new List<HumanBodyBones>();
            
            switch (bone)
            {
                case HumanBodyBones.Hips:
                    children.Add(HumanBodyBones.Spine);
                    children.Add(HumanBodyBones.LeftUpperLeg);
                    children.Add(HumanBodyBones.RightUpperLeg);
                    break;
                
                case HumanBodyBones.Spine:
                    children.Add(HumanBodyBones.Chest);
                    break;
                
                case HumanBodyBones.Chest:
                    children.Add(HumanBodyBones.UpperChest);
                    children.Add(HumanBodyBones.Neck);
                    children.Add(HumanBodyBones.LeftShoulder);
                    children.Add(HumanBodyBones.RightShoulder);
                    break;
                
                case HumanBodyBones.UpperChest:
                    children.Add(HumanBodyBones.Neck);
                    children.Add(HumanBodyBones.LeftShoulder);
                    children.Add(HumanBodyBones.RightShoulder);
                    break;
                
                case HumanBodyBones.Neck:
                    children.Add(HumanBodyBones.Head);
                    break;
                
                case HumanBodyBones.Head:
                    children.Add(HumanBodyBones.LeftEye);
                    children.Add(HumanBodyBones.RightEye);
                    children.Add(HumanBodyBones.Jaw);
                    break;
                
                case HumanBodyBones.LeftShoulder:
                    children.Add(HumanBodyBones.LeftUpperArm);
                    break;
                
                case HumanBodyBones.RightShoulder:
                    children.Add(HumanBodyBones.RightUpperArm);
                    break;
                
                case HumanBodyBones.LeftUpperArm:
                    children.Add(HumanBodyBones.LeftLowerArm);
                    break;
                
                case HumanBodyBones.RightUpperArm:
                    children.Add(HumanBodyBones.RightLowerArm);
                    break;
                
                case HumanBodyBones.LeftLowerArm:
                    children.Add(HumanBodyBones.LeftHand);
                    break;
                
                case HumanBodyBones.RightLowerArm:
                    children.Add(HumanBodyBones.RightHand);
                    break;
                
                case HumanBodyBones.LeftHand:
                    children.Add(HumanBodyBones.LeftThumbProximal);
                    children.Add(HumanBodyBones.LeftIndexProximal);
                    children.Add(HumanBodyBones.LeftMiddleProximal);
                    children.Add(HumanBodyBones.LeftRingProximal);
                    children.Add(HumanBodyBones.LeftLittleProximal);
                    break;
                
                case HumanBodyBones.RightHand:
                    children.Add(HumanBodyBones.RightThumbProximal);
                    children.Add(HumanBodyBones.RightIndexProximal);
                    children.Add(HumanBodyBones.RightMiddleProximal);
                    children.Add(HumanBodyBones.RightRingProximal);
                    children.Add(HumanBodyBones.RightLittleProximal);
                    break;
                
                // 指の階層
                case HumanBodyBones.LeftThumbProximal:
                    children.Add(HumanBodyBones.LeftThumbIntermediate);
                    break;
                case HumanBodyBones.LeftThumbIntermediate:
                    children.Add(HumanBodyBones.LeftThumbDistal);
                    break;
                
                case HumanBodyBones.LeftIndexProximal:
                    children.Add(HumanBodyBones.LeftIndexIntermediate);
                    break;
                case HumanBodyBones.LeftIndexIntermediate:
                    children.Add(HumanBodyBones.LeftIndexDistal);
                    break;
                
                case HumanBodyBones.LeftMiddleProximal:
                    children.Add(HumanBodyBones.LeftMiddleIntermediate);
                    break;
                case HumanBodyBones.LeftMiddleIntermediate:
                    children.Add(HumanBodyBones.LeftMiddleDistal);
                    break;
                
                case HumanBodyBones.LeftRingProximal:
                    children.Add(HumanBodyBones.LeftRingIntermediate);
                    break;
                case HumanBodyBones.LeftRingIntermediate:
                    children.Add(HumanBodyBones.LeftRingDistal);
                    break;
                
                case HumanBodyBones.LeftLittleProximal:
                    children.Add(HumanBodyBones.LeftLittleIntermediate);
                    break;
                case HumanBodyBones.LeftLittleIntermediate:
                    children.Add(HumanBodyBones.LeftLittleDistal);
                    break;
                
                // 右指の階層
                case HumanBodyBones.RightThumbProximal:
                    children.Add(HumanBodyBones.RightThumbIntermediate);
                    break;
                case HumanBodyBones.RightThumbIntermediate:
                    children.Add(HumanBodyBones.RightThumbDistal);
                    break;
                
                case HumanBodyBones.RightIndexProximal:
                    children.Add(HumanBodyBones.RightIndexIntermediate);
                    break;
                case HumanBodyBones.RightIndexIntermediate:
                    children.Add(HumanBodyBones.RightIndexDistal);
                    break;
                
                case HumanBodyBones.RightMiddleProximal:
                    children.Add(HumanBodyBones.RightMiddleIntermediate);
                    break;
                case HumanBodyBones.RightMiddleIntermediate:
                    children.Add(HumanBodyBones.RightMiddleDistal);
                    break;
                
                case HumanBodyBones.RightRingProximal:
                    children.Add(HumanBodyBones.RightRingIntermediate);
                    break;
                case HumanBodyBones.RightRingIntermediate:
                    children.Add(HumanBodyBones.RightRingDistal);
                    break;
                
                case HumanBodyBones.RightLittleProximal:
                    children.Add(HumanBodyBones.RightLittleIntermediate);
                    break;
                case HumanBodyBones.RightLittleIntermediate:
                    children.Add(HumanBodyBones.RightLittleDistal);
                    break;
                
                // 足の階層
                case HumanBodyBones.LeftUpperLeg:
                    children.Add(HumanBodyBones.LeftLowerLeg);
                    break;
                case HumanBodyBones.LeftLowerLeg:
                    children.Add(HumanBodyBones.LeftFoot);
                    break;
                case HumanBodyBones.LeftFoot:
                    children.Add(HumanBodyBones.LeftToes);
                    break;
                
                case HumanBodyBones.RightUpperLeg:
                    children.Add(HumanBodyBones.RightLowerLeg);
                    break;
                case HumanBodyBones.RightLowerLeg:
                    children.Add(HumanBodyBones.RightFoot);
                    break;
                case HumanBodyBones.RightFoot:
                    children.Add(HumanBodyBones.RightToes);
                    break;
            }
            
            return children;
        }

        /// <summary>
        /// ヒューマノイドボーン情報をBoneDataのリストに変換
        /// </summary>
        public static List<BoneData> ConvertToBoneDataList(GameObject gameObject)
        {
            Dictionary<HumanBodyBones, Transform> boneMap = RetrieveHumanoidBones(gameObject);
            List<BoneData> boneDataList = new List<BoneData>();
            
            foreach (var pair in boneMap)
            {
                if (pair.Key != HumanBodyBones.LastBone && pair.Value != null)
                {
                    // ボーンパスの取得
                    string bonePath = GetBonePath(pair.Value, gameObject.transform);
                    
                    // ボーンタイプの取得
                    string boneType = GetBoneType(pair.Key);
                    
                    // 親ボーンの取得
                    string parentBonePath = "";
                    if (pair.Value.parent != null && pair.Value.parent != gameObject.transform)
                    {
                        parentBonePath = GetBonePath(pair.Value.parent, gameObject.transform);
                    }
                    
                    // ボーン情報の作成
                    BoneData boneData = new BoneData
                    {
                        Name = pair.Value.name,
                        Path = bonePath,
                        Type = boneType,
                        Position = pair.Value.position,
                        Rotation = pair.Value.rotation.eulerAngles,
                        Scale = pair.Value.localScale,
                        ParentPath = parentBonePath,
                        Children = GetChildBonePaths(pair.Value, gameObject.transform)
                    };
                    
                    boneDataList.Add(boneData);
                }
            }
            
            return boneDataList;
        }

        /// <summary>
        /// トランスフォームのパスを取得
        /// </summary>
        private static string GetBonePath(Transform bone, Transform root)
        {
            if (bone == root)
                return "";
            
            if (bone.parent == root)
                return bone.name;
            
            return GetBonePath(bone.parent, root) + "/" + bone.name;
        }

        /// <summary>
        /// 子ボーンのパスを取得
        /// </summary>
        private static List<string> GetChildBonePaths(Transform parent, Transform root)
        {
            List<string> childPaths = new List<string>();
            
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                childPaths.Add(GetBonePath(child, root));
            }
            
            return childPaths;
        }

        /// <summary>
        /// HumanBodyBonesからボーンタイプの文字列に変換
        /// </summary>
        private static string GetBoneType(HumanBodyBones bone)
        {
            // ペアボーンの場合は左右を除いた一般的なタイプを返す
            switch (bone)
            {
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.RightUpperArm:
                    return "UpperArm";
                
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                    return "LowerArm";
                
                case HumanBodyBones.LeftHand:
                case HumanBodyBones.RightHand:
                    return "Hand";
                
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    return "UpperLeg";
                
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                    return "LowerLeg";
                
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                    return "Foot";
                
                case HumanBodyBones.LeftToes:
                case HumanBodyBones.RightToes:
                    return "Toes";
                
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder:
                    return "Shoulder";
                
                case HumanBodyBones.LeftEye:
                case HumanBodyBones.RightEye:
                    return "Eye";
                
                // 指のボーン
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.RightThumbProximal:
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.RightThumbIntermediate:
                case HumanBodyBones.LeftThumbDistal:
                case HumanBodyBones.RightThumbDistal:
                    return "Thumb";
                
                case HumanBodyBones.LeftIndexProximal:
                case HumanBodyBones.RightIndexProximal:
                case HumanBodyBones.LeftIndexIntermediate:
                case HumanBodyBones.RightIndexIntermediate:
                case HumanBodyBones.LeftIndexDistal:
                case HumanBodyBones.RightIndexDistal:
                    return "Index";
                
                case HumanBodyBones.LeftMiddleProximal:
                case HumanBodyBones.RightMiddleProximal:
                case HumanBodyBones.LeftMiddleIntermediate:
                case HumanBodyBones.RightMiddleIntermediate:
                case HumanBodyBones.LeftMiddleDistal:
                case HumanBodyBones.RightMiddleDistal:
                    return "Middle";
                
                case HumanBodyBones.LeftRingProximal:
                case HumanBodyBones.RightRingProximal:
                case HumanBodyBones.LeftRingIntermediate:
                case HumanBodyBones.RightRingIntermediate:
                case HumanBodyBones.LeftRingDistal:
                case HumanBodyBones.RightRingDistal:
                    return "Ring";
                
                case HumanBodyBones.LeftLittleProximal:
                case HumanBodyBones.RightLittleProximal:
                case HumanBodyBones.LeftLittleIntermediate:
                case HumanBodyBones.RightLittleIntermediate:
                case HumanBodyBones.LeftLittleDistal:
                case HumanBodyBones.RightLittleDistal:
                    return "Pinky";
                
                // その他のボーン
                default:
                    return bone.ToString();
            }
        }

        /// <summary>
        /// 指定されたゲームオブジェクトから非ヒューマノイドのボーン情報を取得
        /// </summary>
        public static List<BoneData> GetNonHumanoidBones(GameObject gameObject)
        {
            List<BoneData> boneDataList = new List<BoneData>();
            
            if (gameObject == null)
                return boneDataList;
            
            // ヒューマノイドボーンを先に取得
            Dictionary<HumanBodyBones, Transform> humanoidBones = RetrieveHumanoidBones(gameObject);
            HashSet<Transform> humanoidTransforms = new HashSet<Transform>(humanoidBones.Values);
            
            // すべてのトランスフォームを取得
            Transform[] allTransforms = gameObject.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform transform in allTransforms)
            {
                // ルートとヒューマノイドボーンは除外
                if (transform == gameObject.transform || humanoidTransforms.Contains(transform))
                    continue;
                
                // スキンメッシュレンダラーに関連するボーンのみを対象に
                if (IsUsedBySkinnedMesh(transform, gameObject))
                {
                    string bonePath = GetBonePath(transform, gameObject.transform);
                    string parentBonePath = "";
                    
                    if (transform.parent != null && transform.parent != gameObject.transform)
                    {
                        parentBonePath = GetBonePath(transform.parent, gameObject.transform);
                    }
                    
                    BoneData boneData = new BoneData
                    {
                        Name = transform.name,
                        Path = bonePath,
                        Type = GuessNonHumanoidBoneType(transform),
                        Position = transform.position,
                        Rotation = transform.rotation.eulerAngles,
                        Scale = transform.localScale,
                        ParentPath = parentBonePath,
                        Children = GetChildBonePaths(transform, gameObject.transform)
                    };
                    
                    boneDataList.Add(boneData);
                }
            }
            
            return boneDataList;
        }

        /// <summary>
        /// トランスフォームがスキンメッシュレンダラーのボーンとして使用されているか確認
        /// </summary>
        private static bool IsUsedBySkinnedMesh(Transform transform, GameObject root)
        {
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            foreach (var renderer in renderers)
            {
                if (renderer.bones.Contains(transform))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 非ヒューマノイドボーンのタイプを推測
        /// </summary>
        private static string GuessNonHumanoidBoneType(Transform bone)
        {
            string name = bone.name.ToLower();
            
            // 名前に基づいて一般的なタイプを推測
            if (name.Contains("hair") || name.Contains("hat") || name.Contains("cap"))
                return "Hair";
            
            if (name.Contains("ear"))
                return "Ear";
            
            if (name.Contains("tail"))
                return "Tail";
            
            if (name.Contains("wing"))
                return "Wing";
            
            if (name.Contains("skirt") || name.Contains("dress"))
                return "Skirt";
            
            if (name.Contains("breast") || name.Contains("chest") || name.Contains("bust"))
                return "Chest";
            
            // ボーンの位置や階層などから推測できる場合はさらに詳細な推測を追加
            
            // その他のケースではUnknownとする
            return "Unknown";
        }
    }

    /// <summary>
    /// ヒューマノイドボーンの階層情報を表すクラス
    /// </summary>
    public class HumanoidBoneHierarchy
    {
        public HumanoidBoneNode Root { get; set; }
        
        public HumanoidBoneHierarchy()
        {
            Root = null;
        }
        
        /// <summary>
        /// 指定されたボーンタイプのノードを検索
        /// </summary>
        public HumanoidBoneNode FindNode(HumanBodyBones boneType)
        {
            if (Root == null)
                return null;
            
            return FindNodeRecursive(Root, boneType);
        }
        
        private HumanoidBoneNode FindNodeRecursive(HumanoidBoneNode node, HumanBodyBones boneType)
        {
            if (node.Bone == boneType)
                return node;
            
            foreach (var child in node.Children)
            {
                var result = FindNodeRecursive(child, boneType);
                if (result != null)
                    return result;
            }
            
            return null;
        }
    }

    /// <summary>
    /// ヒューマノイドボーンの階層ノードを表すクラス
    /// </summary>
    public class HumanoidBoneNode
    {
        public HumanBodyBones Bone { get; private set; }
        public Transform Transform { get; private set; }
        public HumanoidBoneNode Parent { get; set; }
        public List<HumanoidBoneNode> Children { get; private set; }
        
        public HumanoidBoneNode(HumanBodyBones bone, Transform transform)
        {
            Bone = bone;
            Transform = transform;
            Parent = null;
            Children = new List<HumanoidBoneNode>();
        }
    }
}
