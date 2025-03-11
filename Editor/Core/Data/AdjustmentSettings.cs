using UnityEngine;
using System;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 部位別調整データを表すクラス
    /// </summary>
    [Serializable]
    public class BodyPartAdjustment
    {
        // 部位名
        public string BodyPart;
        
        // スケール調整
        public float scaleX = 1.0f;
        public float scaleY = 1.0f;
        public float scaleZ = 1.0f;
        
        // 位置調整
        public float offsetX = 0.0f;
        public float offsetY = 0.0f;
        public float offsetZ = 0.0f;
        
        // 回転調整
        public Vector3 rotation = Vector3.zero;
        
        // その他の調整パラメータ
        public bool isEnabled = true;
        public bool useCustomSettings = false;
        
        // スケール・位置・回転の個別調整有効フラグ
        public bool adjustScale = true;
        public bool adjustPosition = true;
        public bool adjustRotation = true;
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public BodyPartAdjustment() { }
        
        /// <summary>
        /// コピー作成用のコンストラクタ
        /// </summary>
        public BodyPartAdjustment(BodyPartAdjustment other)
        {
            if (other == null) return;
            
            this.BodyPart = other.BodyPart;
            this.scaleX = other.scaleX;
            this.scaleY = other.scaleY;
            this.scaleZ = other.scaleZ;
            this.offsetX = other.offsetX;
            this.offsetY = other.offsetY;
            this.offsetZ = other.offsetZ;
            this.rotation = other.rotation;
            this.isEnabled = other.isEnabled;
            this.useCustomSettings = other.useCustomSettings;
            this.adjustScale = other.adjustScale;
            this.adjustPosition = other.adjustPosition;
            this.adjustRotation = other.adjustRotation;
        }
        
        /// <summary>
        /// デフォルト値にリセット
        /// </summary>
        public void Reset()
        {
            scaleX = 1.0f;
            scaleY = 1.0f;
            scaleZ = 1.0f;
            offsetX = 0.0f;
            offsetY = 0.0f;
            offsetZ = 0.0f;
            rotation = Vector3.zero;
            isEnabled = true;
            useCustomSettings = false;
            adjustScale = true;
            adjustPosition = true;
            adjustRotation = true;
        }
        
        /// <summary>
        /// スケールをVector3で取得
        /// </summary>
        public Vector3 GetScaleVector()
        {
            return new Vector3(scaleX, scaleY, scaleZ);
        }
        
        /// <summary>
        /// オフセットをVector3で取得
        /// </summary>
        public Vector3 GetOffsetVector()
        {
            return new Vector3(offsetX, offsetY, offsetZ);
        }
        
        // バインドポーズやスケールを調整するときに使用する最終スケール値
        public Vector3 scaleMultiplier { get { return GetScaleVector(); } }
        
        // 位置調整に使用する最終オフセット値
        public Vector3 positionOffset { get { return GetOffsetVector(); } }
        
        // 回転調整に使用する最終回転値
        public Vector3 rotationOffset { get { return rotation; } }
        
        // PreviewManager用のプロパティ
        public Vector3 Scale { get { return GetScaleVector(); } }
    }

    /// <summary>
    /// 調整設定を表すクラス
    /// </summary>
    [Serializable]
    public class AdjustmentSettings
    {
        // 調整方法
        public string method = "BoneBased";
        
        // グローバル設定
        public float globalScale = 1.0f;
        
        // PreviewManager用のプロパティ
        public float GlobalScale { get { return globalScale; } set { globalScale = value; } }
        
        // 上半身設定
        public float upperBodyOffsetX = 0.0f;
        public float upperBodyOffsetY = 0.0f;
        public float upperBodyOffsetZ = 0.0f;
        
        // 下半身設定
        public float lowerBodyOffsetX = 0.0f;
        public float lowerBodyOffsetY = 0.0f;
        public float lowerBodyOffsetZ = 0.0f;
        
        // 腕設定
        public float leftArmScale = 1.0f;
        public float rightArmScale = 1.0f;
        
        // 脚設定
        public float leftLegScale = 1.0f;
        public float rightLegScale = 1.0f;
        
        // PreviewManager用の位置・回転プロパティ
        public Vector3 PositionOffset = Vector3.zero;
        public Vector3 RotationOffset = Vector3.zero;
        
        // 部位別詳細設定
        public Dictionary<BodyPart, BodyPartAdjustment> bodyPartAdjustments = new Dictionary<BodyPart, BodyPartAdjustment>();
        
        // リスト形式の部位別調整（シリアル化とPreviewManager用）
        private List<BodyPartAdjustment> bodyPartAdjustmentsList = new List<BodyPartAdjustment>();
        
        // PreviewManager用のプロパティ
        public List<BodyPartAdjustment> BodyPartAdjustments { get { return bodyPartAdjustmentsList; } }
        
        // プリセット情報
        public string presetName = "";
        public string presetDescription = "";
        
        // 高度な調整オプション
        public bool adjustScale = true;     // スケールの調整を行うかどうか
        public bool adjustRotation = true;  // 回転の調整を行うかどうか
        public bool adjustBindPoses = true; // バインドポーズの調整を行うかどうか
        
        // ボーン構造差異対応設定
        public bool detectStructuralDifferences = true;  // 構造の違いを自動検出
        public bool redistributeWeights = true;          // ウェイトの再分配を行う
        public float confidenceThreshold = 0.3f;         // マッピング信頼度のしきい値
        
        // アドバンスドオプション
        public bool useAdvancedOptions = false;          // 高度なオプションを使用するかどうか
        public bool forceUpdateBindPoses = false;        // 強制的にバインドポーズを更新
        public bool maintainBoneHierarchy = true;        // ボーン階層構造を維持
        public float positionTolerance = 0.01f;          // 位置の許容誤差
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public AdjustmentSettings()
        {
            InitializeBodyPartAdjustments();
        }
        
        /// <summary>
        /// コピー作成用のコンストラクタ
        /// </summary>
        public AdjustmentSettings(AdjustmentSettings other)
        {
            if (other == null) return;
            
            this.method = other.method;
            this.globalScale = other.globalScale;
            
            this.upperBodyOffsetX = other.upperBodyOffsetX;
            this.upperBodyOffsetY = other.upperBodyOffsetY;
            this.upperBodyOffsetZ = other.upperBodyOffsetZ;
            
            this.lowerBodyOffsetX = other.lowerBodyOffsetX;
            this.lowerBodyOffsetY = other.lowerBodyOffsetY;
            this.lowerBodyOffsetZ = other.lowerBodyOffsetZ;
            
            this.leftArmScale = other.leftArmScale;
            this.rightArmScale = other.rightArmScale;
            
            this.leftLegScale = other.leftLegScale;
            this.rightLegScale = other.rightLegScale;
            
            this.PositionOffset = other.PositionOffset;
            this.RotationOffset = other.RotationOffset;
            
            this.presetName = other.presetName;
            this.presetDescription = other.presetDescription;
            
            // 高度なオプションのコピー
            this.adjustScale = other.adjustScale;
            this.adjustRotation = other.adjustRotation;
            this.adjustBindPoses = other.adjustBindPoses;
            this.detectStructuralDifferences = other.detectStructuralDifferences;
            this.redistributeWeights = other.redistributeWeights;
            this.confidenceThreshold = other.confidenceThreshold;
            this.useAdvancedOptions = other.useAdvancedOptions;
            this.forceUpdateBindPoses = other.forceUpdateBindPoses;
            this.maintainBoneHierarchy = other.maintainBoneHierarchy;
            this.positionTolerance = other.positionTolerance;
            
            // 部位別調整のディープコピー
            this.bodyPartAdjustments = new Dictionary<BodyPart, BodyPartAdjustment>();
            this.bodyPartAdjustmentsList = new List<BodyPartAdjustment>();
            
            foreach (var kvp in other.bodyPartAdjustments)
            {
                var adjustment = new BodyPartAdjustment(kvp.Value);
                this.bodyPartAdjustments[kvp.Key] = adjustment;
                this.bodyPartAdjustmentsList.Add(adjustment);
            }
        }
        
        /// <summary>
        /// 部位別調整を初期化
        /// </summary>
        private void InitializeBodyPartAdjustments()
        {
            bodyPartAdjustments.Clear();
            bodyPartAdjustmentsList.Clear();
            
            // 各体の部位の調整設定を初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                var adjustment = new BodyPartAdjustment();
                adjustment.BodyPart = part.ToString();
                bodyPartAdjustments[part] = adjustment;
                bodyPartAdjustmentsList.Add(adjustment);
            }
        }
        
        /// <summary>
        /// 上半身のオフセットをVector3で取得
        /// </summary>
        public Vector3 GetUpperBodyOffset()
        {
            return new Vector3(upperBodyOffsetX, upperBodyOffsetY, upperBodyOffsetZ);
        }
        
        /// <summary>
        /// 下半身のオフセットをVector3で取得
        /// </summary>
        public Vector3 GetLowerBodyOffset()
        {
            return new Vector3(lowerBodyOffsetX, lowerBodyOffsetY, lowerBodyOffsetZ);
        }
        
        /// <summary>
        /// 指定した体の部位の調整設定を取得
        /// </summary>
        public BodyPartAdjustment GetBodyPartAdjustment(BodyPart part)
        {
            if (!bodyPartAdjustments.ContainsKey(part))
            {
                var adjustment = new BodyPartAdjustment();
                adjustment.BodyPart = part.ToString();
                bodyPartAdjustments[part] = adjustment;
                bodyPartAdjustmentsList.Add(adjustment);
            }
            
            return bodyPartAdjustments[part];
        }
        
        /// <summary>
        /// すべての設定をデフォルト値にリセット
        /// </summary>
        public void ResetAllSettings()
        {
            method = "BoneBased";
            globalScale = 1.0f;
            
            upperBodyOffsetX = 0.0f;
            upperBodyOffsetY = 0.0f;
            upperBodyOffsetZ = 0.0f;
            
            lowerBodyOffsetX = 0.0f;
            lowerBodyOffsetY = 0.0f;
            lowerBodyOffsetZ = 0.0f;
            
            leftArmScale = 1.0f;
            rightArmScale = 1.0f;
            
            leftLegScale = 1.0f;
            rightLegScale = 1.0f;
            
            PositionOffset = Vector3.zero;
            RotationOffset = Vector3.zero;
            
            // 高度なオプションをリセット
            adjustScale = true;
            adjustRotation = true;
            adjustBindPoses = true;
            detectStructuralDifferences = true;
            redistributeWeights = true;
            confidenceThreshold = 0.3f;
            useAdvancedOptions = false;
            forceUpdateBindPoses = false;
            maintainBoneHierarchy = true;
            positionTolerance = 0.01f;
            
            // 部位別調整をリセット
            foreach (var part in bodyPartAdjustments.Keys)
            {
                bodyPartAdjustments[part].Reset();
            }
            
            presetName = "";
            presetDescription = "";
        }
    }
}
