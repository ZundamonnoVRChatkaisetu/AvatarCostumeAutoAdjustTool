using UnityEngine;
using System;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 体の部位を表す列挙型
    /// </summary>
    public enum BodyPart
    {
        Head,           // 頭部
        Neck,           // 首
        Chest,          // 胸部
        UpperChest,     // 上胸部
        Spine,          // 脊椎
        Hips,           // 腰
        LeftShoulder,   // 左肩
        LeftUpperArm,   // 左上腕
        LeftLowerArm,   // 左前腕
        LeftHand,       // 左手
        LeftThumb,      // 左親指
        LeftIndex,      // 左人差し指
        LeftMiddle,     // 左中指
        LeftRing,       // 左薬指
        LeftPinky,      // 左小指
        RightShoulder,  // 右肩
        RightUpperArm,  // 右上腕
        RightLowerArm,  // 右前腕
        RightHand,      // 右手
        RightThumb,     // 右親指
        RightIndex,     // 右人差し指
        RightMiddle,    // 右中指
        RightRing,      // 右薬指
        RightPinky,     // 右小指
        LeftUpperLeg,   // 左太もも
        LeftLowerLeg,   // 左すね
        LeftFoot,       // 左足
        LeftToes,       // 左つま先
        RightUpperLeg,  // 右太もも
        RightLowerLeg,  // 右すね
        RightFoot,      // 右足
        RightToes,      // 右つま先
        Other           // その他
    }

    /// <summary>
    /// ボーン情報を保持するクラス
    /// </summary>
    [Serializable]
    public class BoneData
    {
        // 基本情報
        public string id;              // 一意のID
        public string name;            // ボーン名
        public string hierarchyPath;   // 階層パス
        public BodyPart bodyPart;      // 体の部位
        
        // Transform情報
        public Vector3 localPosition;  // ローカル位置
        public Quaternion localRotation; // ローカル回転
        public Vector3 localScale;     // ローカルスケール
        
        // 親子関係
        public string parentId;        // 親ボーンのID
        public List<string> childrenIds; // 子ボーンのIDリスト
        
        // メタデータ
        public bool isRoot;            // ルートボーンかどうか
        public bool isHumanoid;        // ヒューマノイドボーンかどうか
        public string humanoidName;    // Unity標準ヒューマノイド名（該当する場合）
        
        // Unity参照
        [NonSerialized]
        public Transform transform;    // 対応するTransform
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public BoneData()
        {
            id = Guid.NewGuid().ToString();
            childrenIds = new List<string>();
            bodyPart = BodyPart.Other;
        }
        
        /// <summary>
        /// Transformからボーンデータを作成するコンストラクタ
        /// </summary>
        public BoneData(Transform boneTransform, string parentId = null)
        {
            id = Guid.NewGuid().ToString();
            name = boneTransform.name;
            transform = boneTransform;
            this.parentId = parentId;
            childrenIds = new List<string>();
            bodyPart = BodyPart.Other;
            
            // Transformの情報を保存
            localPosition = boneTransform.localPosition;
            localRotation = boneTransform.localRotation;
            localScale = boneTransform.localScale;
            
            // 階層パスの生成
            hierarchyPath = GetHierarchyPath(boneTransform);
            
            // ルートボーンかどうかを判定
            isRoot = boneTransform.parent == null || 
                     boneTransform.parent.GetComponent<Animator>() != null;
        }
        
        /// <summary>
        /// Transformの階層パスを取得
        /// </summary>
        private string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            
            while (parent != null && parent.GetComponent<Animator>() == null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
        
        /// <summary>
        /// ボーン情報の文字列表現を取得
        /// </summary>
        public override string ToString()
        {
            return $"{name} ({bodyPart})";
        }

        /// <summary>
        /// 2つのボーンデータが同じボーンを参照しているかを判定
        /// </summary>
        public bool IsSameBone(BoneData other)
        {
            if (other == null) return false;
            
            // Transformの参照が存在する場合はそれを比較
            if (transform != null && other.transform != null)
            {
                return transform == other.transform;
            }
            
            // ID、名前、階層パスで比較
            return id == other.id || 
                   (name == other.name && hierarchyPath == other.hierarchyPath);
        }
        
        /// <summary>
        /// ボーンの体の部位を推定
        /// </summary>
        public static BodyPart EstimateBodyPart(string boneName)
        {
            boneName = boneName.ToLower();
            
            if (boneName.Contains("head")) return BodyPart.Head;
            if (boneName.Contains("neck")) return BodyPart.Neck;
            if (boneName.Contains("chest") && boneName.Contains("upper")) return BodyPart.UpperChest;
            if (boneName.Contains("chest")) return BodyPart.Chest;
            if (boneName.Contains("spine")) return BodyPart.Spine;
            if (boneName.Contains("hip") || boneName.Contains("pelvis")) return BodyPart.Hips;
            
            // 左側の部位
            if ((boneName.Contains("left") || boneName.StartsWith("l_") || boneName.StartsWith("l.") || boneName.EndsWith("_l") || boneName.EndsWith(".l")))
            {
                if (boneName.Contains("shoulder") || boneName.Contains("clavicle")) return BodyPart.LeftShoulder;
                if (boneName.Contains("arm") && (boneName.Contains("upper") || boneName.Contains("up"))) return BodyPart.LeftUpperArm;
                if (boneName.Contains("arm") && (boneName.Contains("lower") || boneName.Contains("lo") || boneName.Contains("fore"))) return BodyPart.LeftLowerArm;
                if (boneName.Contains("hand") && !boneName.Contains("finger")) return BodyPart.LeftHand;
                
                // 指
                if (boneName.Contains("thumb")) return BodyPart.LeftThumb;
                if (boneName.Contains("index")) return BodyPart.LeftIndex;
                if (boneName.Contains("middle")) return BodyPart.LeftMiddle;
                if (boneName.Contains("ring")) return BodyPart.LeftRing;
                if (boneName.Contains("pinky") || boneName.Contains("little")) return BodyPart.LeftPinky;
                
                // 脚
                if (boneName.Contains("leg") && (boneName.Contains("upper") || boneName.Contains("up") || boneName.Contains("thigh"))) return BodyPart.LeftUpperLeg;
                if (boneName.Contains("leg") && (boneName.Contains("lower") || boneName.Contains("lo") || boneName.Contains("calf"))) return BodyPart.LeftLowerLeg;
                if (boneName.Contains("foot")) return BodyPart.LeftFoot;
                if (boneName.Contains("toe")) return BodyPart.LeftToes;
            }
            
            // 右側の部位
            if ((boneName.Contains("right") || boneName.StartsWith("r_") || boneName.StartsWith("r.") || boneName.EndsWith("_r") || boneName.EndsWith(".r")))
            {
                if (boneName.Contains("shoulder") || boneName.Contains("clavicle")) return BodyPart.RightShoulder;
                if (boneName.Contains("arm") && (boneName.Contains("upper") || boneName.Contains("up"))) return BodyPart.RightUpperArm;
                if (boneName.Contains("arm") && (boneName.Contains("lower") || boneName.Contains("lo") || boneName.Contains("fore"))) return BodyPart.RightLowerArm;
                if (boneName.Contains("hand") && !boneName.Contains("finger")) return BodyPart.RightHand;
                
                // 指
                if (boneName.Contains("thumb")) return BodyPart.RightThumb;
                if (boneName.Contains("index")) return BodyPart.RightIndex;
                if (boneName.Contains("middle")) return BodyPart.RightMiddle;
                if (boneName.Contains("ring")) return BodyPart.RightRing;
                if (boneName.Contains("pinky") || boneName.Contains("little")) return BodyPart.RightPinky;
                
                // 脚
                if (boneName.Contains("leg") && (boneName.Contains("upper") || boneName.Contains("up") || boneName.Contains("thigh"))) return BodyPart.RightUpperLeg;
                if (boneName.Contains("leg") && (boneName.Contains("lower") || boneName.Contains("lo") || boneName.Contains("calf"))) return BodyPart.RightLowerLeg;
                if (boneName.Contains("foot")) return BodyPart.RightFoot;
                if (boneName.Contains("toe")) return BodyPart.RightToes;
            }
            
            return BodyPart.Other;
        }
        
        /// <summary>
        /// ヒューマノイドボーン名からBodyPartを取得
        /// </summary>
        public static BodyPart GetBodyPartFromHumanoidName(string humanoidName)
        {
            switch (humanoidName)
            {
                case "Head": return BodyPart.Head;
                case "Neck": return BodyPart.Neck;
                case "Chest": return BodyPart.Chest;
                case "UpperChest": return BodyPart.UpperChest;
                case "Spine": return BodyPart.Spine;
                case "Hips": return BodyPart.Hips;
                case "LeftShoulder": return BodyPart.LeftShoulder;
                case "LeftUpperArm": return BodyPart.LeftUpperArm;
                case "LeftLowerArm": return BodyPart.LeftLowerArm;
                case "LeftHand": return BodyPart.LeftHand;
                case "LeftThumbProximal":
                case "LeftThumbIntermediate":
                case "LeftThumbDistal": return BodyPart.LeftThumb;
                case "LeftIndexProximal":
                case "LeftIndexIntermediate":
                case "LeftIndexDistal": return BodyPart.LeftIndex;
                case "LeftMiddleProximal":
                case "LeftMiddleIntermediate":
                case "LeftMiddleDistal": return BodyPart.LeftMiddle;
                case "LeftRingProximal":
                case "LeftRingIntermediate":
                case "LeftRingDistal": return BodyPart.LeftRing;
                case "LeftLittleProximal":
                case "LeftLittleIntermediate":
                case "LeftLittleDistal": return BodyPart.LeftPinky;
                case "RightShoulder": return BodyPart.RightShoulder;
                case "RightUpperArm": return BodyPart.RightUpperArm;
                case "RightLowerArm": return BodyPart.RightLowerArm;
                case "RightHand": return BodyPart.RightHand;
                case "RightThumbProximal":
                case "RightThumbIntermediate":
                case "RightThumbDistal": return BodyPart.RightThumb;
                case "RightIndexProximal":
                case "RightIndexIntermediate":
                case "RightIndexDistal": return BodyPart.RightIndex;
                case "RightMiddleProximal":
                case "RightMiddleIntermediate":
                case "RightMiddleDistal": return BodyPart.RightMiddle;
                case "RightRingProximal":
                case "RightRingIntermediate":
                case "RightRingDistal": return BodyPart.RightRing;
                case "RightLittleProximal":
                case "RightLittleIntermediate":
                case "RightLittleDistal": return BodyPart.RightPinky;
                case "LeftUpperLeg": return BodyPart.LeftUpperLeg;
                case "LeftLowerLeg": return BodyPart.LeftLowerLeg;
                case "LeftFoot": return BodyPart.LeftFoot;
                case "LeftToes": return BodyPart.LeftToes;
                case "RightUpperLeg": return BodyPart.RightUpperLeg;
                case "RightLowerLeg": return BodyPart.RightLowerLeg;
                case "RightFoot": return BodyPart.RightFoot;
                case "RightToes": return BodyPart.RightToes;
                default: return BodyPart.Other;
            }
        }
        
        /// <summary>
        /// アバターに設定されたヒューマノイドボーン情報を取得
        /// </summary>
        public static Dictionary<string, BoneData> GetHumanoidBones(Animator animator)
        {
            if (animator == null || !animator.isHuman)
            {
                return new Dictionary<string, BoneData>();
            }
            
            var result = new Dictionary<string, BoneData>();
            var humanDescription = HumanUtility.GetHumanDescription(animator);
            
            if (humanDescription == null)
            {
                return result;
            }
            
            foreach (var humanBone in humanDescription.human)
            {
                Transform boneTransform = animator.GetBoneTransform(HumanUtility.GetHumanBodyBoneFromName(humanBone.humanName));
                
                if (boneTransform != null)
                {
                    BoneData boneData = new BoneData(boneTransform);
                    boneData.isHumanoid = true;
                    boneData.humanoidName = humanBone.humanName;
                    boneData.bodyPart = GetBodyPartFromHumanoidName(humanBone.humanName);
                    
                    result[humanBone.humanName] = boneData;
                }
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// ヒューマノイドユーティリティクラス
    /// </summary>
    public static class HumanUtility
    {
        /// <summary>
        /// アニメーターからHumanDescriptionを取得
        /// </summary>
        public static HumanDescription GetHumanDescription(Animator animator)
        {
            // 実装予定
            // Unity Editorの非公開APIを使用する必要がある可能性あり
            return new HumanDescription();
        }
        
        /// <summary>
        /// ヒューマノイド名からHumanBodyBonesを取得
        /// </summary>
        public static HumanBodyBones GetHumanBodyBoneFromName(string humanName)
        {
            switch (humanName)
            {
                case "Hips": return HumanBodyBones.Hips;
                case "Spine": return HumanBodyBones.Spine;
                case "Chest": return HumanBodyBones.Chest;
                case "UpperChest": return HumanBodyBones.UpperChest;
                case "Neck": return HumanBodyBones.Neck;
                case "Head": return HumanBodyBones.Head;
                
                case "LeftShoulder": return HumanBodyBones.LeftShoulder;
                case "LeftUpperArm": return HumanBodyBones.LeftUpperArm;
                case "LeftLowerArm": return HumanBodyBones.LeftLowerArm;
                case "LeftHand": return HumanBodyBones.LeftHand;
                
                case "RightShoulder": return HumanBodyBones.RightShoulder;
                case "RightUpperArm": return HumanBodyBones.RightUpperArm;
                case "RightLowerArm": return HumanBodyBones.RightLowerArm;
                case "RightHand": return HumanBodyBones.RightHand;
                
                case "LeftUpperLeg": return HumanBodyBones.LeftUpperLeg;
                case "LeftLowerLeg": return HumanBodyBones.LeftLowerLeg;
                case "LeftFoot": return HumanBodyBones.LeftFoot;
                case "LeftToes": return HumanBodyBones.LeftToes;
                
                case "RightUpperLeg": return HumanBodyBones.RightUpperLeg;
                case "RightLowerLeg": return HumanBodyBones.RightLowerLeg;
                case "RightFoot": return HumanBodyBones.RightFoot;
                case "RightToes": return HumanBodyBones.RightToes;
                
                case "LeftThumbProximal": return HumanBodyBones.LeftThumbProximal;
                case "LeftThumbIntermediate": return HumanBodyBones.LeftThumbIntermediate;
                case "LeftThumbDistal": return HumanBodyBones.LeftThumbDistal;
                
                case "LeftIndexProximal": return HumanBodyBones.LeftIndexProximal;
                case "LeftIndexIntermediate": return HumanBodyBones.LeftIndexIntermediate;
                case "LeftIndexDistal": return HumanBodyBones.LeftIndexDistal;
                
                case "LeftMiddleProximal": return HumanBodyBones.LeftMiddleProximal;
                case "LeftMiddleIntermediate": return HumanBodyBones.LeftMiddleIntermediate;
                case "LeftMiddleDistal": return HumanBodyBones.LeftMiddleDistal;
                
                case "LeftRingProximal": return HumanBodyBones.LeftRingProximal;
                case "LeftRingIntermediate": return HumanBodyBones.LeftRingIntermediate;
                case "LeftRingDistal": return HumanBodyBones.LeftRingDistal;
                
                case "LeftLittleProximal": return HumanBodyBones.LeftLittleProximal;
                case "LeftLittleIntermediate": return HumanBodyBones.LeftLittleIntermediate;
                case "LeftLittleDistal": return HumanBodyBones.LeftLittleDistal;
                
                case "RightThumbProximal": return HumanBodyBones.RightThumbProximal;
                case "RightThumbIntermediate": return HumanBodyBones.RightThumbIntermediate;
                case "RightThumbDistal": return HumanBodyBones.RightThumbDistal;
                
                case "RightIndexProximal": return HumanBodyBones.RightIndexProximal;
                case "RightIndexIntermediate": return HumanBodyBones.RightIndexIntermediate;
                case "RightIndexDistal": return HumanBodyBones.RightIndexDistal;
                
                case "RightMiddleProximal": return HumanBodyBones.RightMiddleProximal;
                case "RightMiddleIntermediate": return HumanBodyBones.RightMiddleIntermediate;
                case "RightMiddleDistal": return HumanBodyBones.RightMiddleDistal;
                
                case "RightRingProximal": return HumanBodyBones.RightRingProximal;
                case "RightRingIntermediate": return HumanBodyBones.RightRingIntermediate;
                case "RightRingDistal": return HumanBodyBones.RightRingDistal;
                
                case "RightLittleProximal": return HumanBodyBones.RightLittleProximal;
                case "RightLittleIntermediate": return HumanBodyBones.RightLittleIntermediate;
                case "RightLittleDistal": return HumanBodyBones.RightLittleDistal;
                
                default: return HumanBodyBones.LastBone;
            }
        }
    }
}
