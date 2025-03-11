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
            
            this.scaleX = other.scaleX;
            this.scaleY = other.scaleY;
            this.scaleZ = other.scaleZ;
            this.offsetX = other.offsetX;
            this.offsetY = other.offsetY;
            this.offsetZ = other.offsetZ;
            this.rotation = other.rotation;
            this.isEnabled = other.isEnabled;
            this.useCustomSettings = other.useCustomSettings;
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
        
        // 部位別詳細設定
        public Dictionary<BodyPart, BodyPartAdjustment> bodyPartAdjustments = new Dictionary<BodyPart, BodyPartAdjustment>();
        
        // プリセット情報
        public string presetName = "";
        public string presetDescription = "";
        
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
            
            this.presetName = other.presetName;
            this.presetDescription = other.presetDescription;
            
            // 部位別調整のディープコピー
            this.bodyPartAdjustments = new Dictionary<BodyPart, BodyPartAdjustment>();
            foreach (var kvp in other.bodyPartAdjustments)
            {
                this.bodyPartAdjustments[kvp.Key] = new BodyPartAdjustment(kvp.Value);
            }
        }
        
        /// <summary>
        /// 部位別調整を初期化
        /// </summary>
        private void InitializeBodyPartAdjustments()
        {
            bodyPartAdjustments.Clear();
            
            // 各体の部位の調整設定を初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                bodyPartAdjustments[part] = new BodyPartAdjustment();
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
                bodyPartAdjustments[part] = new BodyPartAdjustment();
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
