using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 調整タブ
    /// 衣装のフィット調整や微調整を行うためのUI
    /// </summary>
    public class AdjustmentTab
    {
        // スクロール位置
        private Vector2 scrollPosition;
        
        // 調整方法
        private enum AdjustmentMethod
        {
            BoneBased,   // ボーンベースの調整
            MeshBased    // メッシュベースの調整
        }
        private AdjustmentMethod currentMethod = AdjustmentMethod.BoneBased;
        
        // プレビュー設定
        private bool showPreview = true;
        private float previewOpacity = 0.5f;
        private bool wireframeMode = false;
        
        // 調整設定
        private AdjustmentSettings adjustmentSettings;
        
        // 部位選択
        private BodyPart selectedBodyPart = BodyPart.Chest;
        
        // エディタスタイル
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle sliderLabelStyle;
        
        /// <summary>
        /// GUIの描画
        /// </summary>
        public void OnGUI(GameObject avatar, GameObject costume)
        {
            InitializeStyles();
            
            EditorGUILayout.BeginVertical();
            
            // 調整方法選択
            DrawAdjustmentMethodSection();
            
            // プレビュー設定
            DrawPreviewSection();
            
            // グローバル調整
            DrawGlobalAdjustmentSection();
            
            // 部位別調整
            DrawBodyPartAdjustmentSection();
            
            // プリセット管理
            DrawPresetSection();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// アバターが変更された際の処理
        /// </summary>
        public void OnAvatarChanged(GameObject avatar)
        {
            if (avatar == null) return;
            
            // 新しいアバターに対して調整設定を初期化
            InitializeAdjustmentSettings();
        }
        
        /// <summary>
        /// 衣装が変更された際の処理
        /// </summary>
        public void OnCostumeChanged(GameObject costume)
        {
            if (costume == null) return;
            
            // 新しい衣装に対して調整設定を初期化
            InitializeAdjustmentSettings();
        }
        
        /// <summary>
        /// 衣装適用後の処理
        /// </summary>
        public void OnCostumeApplied()
        {
            // 衣装適用後の処理が必要な場合ここに記述
        }
        
        /// <summary>
        /// 調整設定を取得
        /// </summary>
        public AdjustmentSettings GetAdjustmentSettings()
        {
            if (adjustmentSettings == null)
            {
                InitializeAdjustmentSettings();
            }
            
            return adjustmentSettings;
        }
        
        #region Private Methods
        
        /// <summary>
        /// エディタスタイルの初期化
        /// </summary>
        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.margin = new RectOffset(0, 0, 10, 5);
            }
            
            if (subHeaderStyle == null)
            {
                subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                subHeaderStyle.fontSize = 12;
                subHeaderStyle.margin = new RectOffset(0, 0, 5, 5);
            }
            
            if (sliderLabelStyle == null)
            {
                sliderLabelStyle = new GUIStyle(EditorStyles.label);
                // GUIStyle.width は存在しないため、実際の使用時に GUILayout.Width() を使用します
            }
        }
        
        /// <summary>
        /// 調整設定の初期化
        /// </summary>
        private void InitializeAdjustmentSettings()
        {
            adjustmentSettings = new AdjustmentSettings
            {
                method = (currentMethod == AdjustmentMethod.BoneBased) ? 
                    AdjustmentMethod.BoneBased.ToString() : AdjustmentMethod.MeshBased.ToString(),
                
                // グローバル調整設定
                globalScale = 1.0f,
                
                // 上半身設定
                upperBodyOffsetX = 0.0f,
                upperBodyOffsetY = 0.0f,
                upperBodyOffsetZ = 0.0f,
                
                // 下半身設定
                lowerBodyOffsetX = 0.0f,
                lowerBodyOffsetY = 0.0f,
                lowerBodyOffsetZ = 0.0f,
                
                // 腕設定
                leftArmScale = 1.0f,
                rightArmScale = 1.0f,
                
                // 脚設定
                leftLegScale = 1.0f,
                rightLegScale = 1.0f
            };
            
            // 各体の部位に対して調整設定を初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                adjustmentSettings.GetBodyPartAdjustment(part).Reset();
            }
        }
        
        /// <summary>
        /// 調整方法セクションの描画
        /// </summary>
        private void DrawAdjustmentMethodSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("調整方法", headerStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            AdjustmentMethod newMethod = (AdjustmentMethod)EditorGUILayout.EnumPopup(currentMethod);
            if (newMethod != currentMethod)
            {
                currentMethod = newMethod;
                
                // 調整方法が変更されたら設定を更新
                if (adjustmentSettings != null)
                {
                    adjustmentSettings.method = currentMethod.ToString();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 選択された調整方法に関する説明
            string description = "";
            switch (currentMethod)
            {
                case AdjustmentMethod.BoneBased:
                    description = "ボーンベースの調整: アバターと衣装のボーン構造に基づいて調整を行います。" +
                                "スキニングメッシュの参照ボーンを自動的に変更し、ボーン変換を適用します。";
                    break;
                case AdjustmentMethod.MeshBased:
                    description = "メッシュベースの調整: アバターと衣装のメッシュ形状に基づいて調整を行います。" +
                                "メッシュ自体を変形させ、アバターの形状に合わせます。";
                    break;
            }
            
            EditorGUILayout.HelpBox(description, MessageType.Info);
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// プレビューセクションの描画
        /// </summary>
        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("プレビュー設定", headerStyle);
            
            EditorGUI.BeginChangeCheck();
            
            showPreview = EditorGUILayout.Toggle("プレビュー表示", showPreview);
            
            if (showPreview)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("プレビュー透明度", GUILayout.Width(120));
                previewOpacity = EditorGUILayout.Slider(previewOpacity, 0.1f, 1.0f);
                EditorGUILayout.EndHorizontal();
                
                wireframeMode = EditorGUILayout.Toggle("ワイヤーフレーム表示", wireframeMode);
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                // プレビュー設定が変更されたときの処理
                UpdatePreviewSettings();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// グローバル調整セクションの描画
        /// </summary>
        private void DrawGlobalAdjustmentSection()
        {
            if (adjustmentSettings == null)
            {
                InitializeAdjustmentSettings();
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("グローバル調整", headerStyle);
            
            // 全体スケール
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("全体スケール", GUILayout.Width(120));
            adjustmentSettings.globalScale = EditorGUILayout.Slider(adjustmentSettings.globalScale, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 上半身設定
            EditorGUILayout.LabelField("上半身", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
            adjustmentSettings.upperBodyOffsetX = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetX, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
            adjustmentSettings.upperBodyOffsetY = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetY, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
            adjustmentSettings.upperBodyOffsetZ = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetZ, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 下半身設定
            EditorGUILayout.LabelField("下半身", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
            adjustmentSettings.lowerBodyOffsetX = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetX, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
            adjustmentSettings.lowerBodyOffsetY = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetY, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
            adjustmentSettings.lowerBodyOffsetZ = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetZ, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 左右の腕設定
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("左腕", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            adjustmentSettings.leftArmScale = EditorGUILayout.Slider(adjustmentSettings.leftArmScale, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("右腕", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            adjustmentSettings.rightArmScale = EditorGUILayout.Slider(adjustmentSettings.rightArmScale, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 左右の脚設定
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("左脚", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            adjustmentSettings.leftLegScale = EditorGUILayout.Slider(adjustmentSettings.leftLegScale, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("右脚", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            adjustmentSettings.rightLegScale = EditorGUILayout.Slider(adjustmentSettings.rightLegScale, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // リアルタイム適用ボタン
            if (GUILayout.Button("調整を適用"))
            {
                ApplyAdjustments();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 部位別調整セクションの描画
        /// </summary>
        private void DrawBodyPartAdjustmentSection()
        {
            if (adjustmentSettings == null)
            {
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("部位別詳細調整", headerStyle);
            
            // 部位選択
            selectedBodyPart = (BodyPart)EditorGUILayout.EnumPopup("調整部位", selectedBodyPart);
            
            EditorGUILayout.Space();
            
            BodyPartAdjustment partAdjustment = adjustmentSettings.GetBodyPartAdjustment(selectedBodyPart);
            
            // スケール調整
            EditorGUILayout.LabelField("スケール調整", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X スケール", GUILayout.Width(120));
            partAdjustment.scaleX = EditorGUILayout.Slider(partAdjustment.scaleX, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y スケール", GUILayout.Width(120));
            partAdjustment.scaleY = EditorGUILayout.Slider(partAdjustment.scaleY, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z スケール", GUILayout.Width(120));
            partAdjustment.scaleZ = EditorGUILayout.Slider(partAdjustment.scaleZ, 0.5f, 1.5f);
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("均一スケール"))
            {
                float avgScale = (partAdjustment.scaleX + partAdjustment.scaleY + partAdjustment.scaleZ) / 3f;
                partAdjustment.scaleX = avgScale;
                partAdjustment.scaleY = avgScale;
                partAdjustment.scaleZ = avgScale;
            }
            
            EditorGUILayout.Space();
            
            // 位置調整
            EditorGUILayout.LabelField("位置調整", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
            partAdjustment.offsetX = EditorGUILayout.Slider(partAdjustment.offsetX, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
            partAdjustment.offsetY = EditorGUILayout.Slider(partAdjustment.offsetY, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
            partAdjustment.offsetZ = EditorGUILayout.Slider(partAdjustment.offsetZ, -0.1f, 0.1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 回転調整
            EditorGUILayout.LabelField("回転調整", subHeaderStyle);
            
            partAdjustment.rotation = EditorGUILayout.Vector3Field("回転 (度)", partAdjustment.rotation);
            
            // リアルタイム適用ボタン
            if (GUILayout.Button("部位調整を適用"))
            {
                ApplyBodyPartAdjustments();
            }
            
            if (GUILayout.Button("部位調整をリセット"))
            {
                ResetBodyPartAdjustment(selectedBodyPart);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// プリセットセクションの描画
        /// </summary>
        private void DrawPresetSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("プリセット管理", headerStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("プリセットを保存"))
            {
                SavePreset();
            }
            
            if (GUILayout.Button("プリセットを読込"))
            {
                LoadPreset();
            }
            
            if (GUILayout.Button("すべてリセット"))
            {
                ResetAllAdjustments();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// プレビュー設定の更新
        /// </summary>
        private void UpdatePreviewSettings()
        {
            // SceneViewでのプレビュー表示設定の更新
            // 実装予定
        }
        
        /// <summary>
        /// 調整の適用
        /// </summary>
        private void ApplyAdjustments()
        {
            if (adjustmentSettings == null) return;
            
            // 選択中のアバターと衣装に対して調整を適用
            // 実装予定
        }
        
        /// <summary>
        /// 部位別調整の適用
        /// </summary>
        private void ApplyBodyPartAdjustments()
        {
            if (adjustmentSettings == null) return;
            
            // 選択中の部位に対して調整を適用
            // 実装予定
        }
        
        /// <summary>
        /// 部位別調整のリセット
        /// </summary>
        private void ResetBodyPartAdjustment(BodyPart part)
        {
            if (adjustmentSettings == null) return;
            
            BodyPartAdjustment adjustment = adjustmentSettings.GetBodyPartAdjustment(part);
            adjustment.Reset();
            
            // 変更を適用
            ApplyBodyPartAdjustments();
        }
        
        /// <summary>
        /// すべての調整をリセット
        /// </summary>
        private void ResetAllAdjustments()
        {
            if (EditorUtility.DisplayDialog(
                "調整リセット確認",
                "すべての調整設定をリセットしますか？この操作は元に戻せません。",
                "はい",
                "キャンセル"))
            {
                InitializeAdjustmentSettings();
                ApplyAdjustments();
                Debug.Log("すべての調整設定をリセットしました。");
            }
        }
        
        /// <summary>
        /// プリセットの保存
        /// </summary>
        private void SavePreset()
        {
            if (adjustmentSettings == null) return;
            
            string path = EditorUtility.SaveFilePanel(
                "調整プリセットを保存",
                Application.dataPath,
                "AdjustmentPreset.json",
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    JsonUtils.SaveToJson(path, adjustmentSettings);
                    Debug.Log($"調整プリセットを保存しました: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"調整プリセットの保存に失敗しました: {ex.Message}");
                    EditorUtility.DisplayDialog("エラー", $"調整プリセットの保存に失敗しました: {ex.Message}", "OK");
                }
            }
        }
        
        /// <summary>
        /// プリセットの読み込み
        /// </summary>
        private void LoadPreset()
        {
            string path = EditorUtility.OpenFilePanel(
                "調整プリセットを読み込み",
                Application.dataPath,
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    adjustmentSettings = JsonUtils.LoadFromJson<AdjustmentSettings>(path);
                    Debug.Log($"調整プリセットを読み込みました: {path}");
                    
                    // 読み込んだプリセットを適用
                    ApplyAdjustments();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"調整プリセットの読み込みに失敗しました: {ex.Message}");
                    EditorUtility.DisplayDialog("エラー", $"調整プリセットの読み込みに失敗しました: {ex.Message}", "OK");
                }
            }
        }
        
        #endregion
    }
}
