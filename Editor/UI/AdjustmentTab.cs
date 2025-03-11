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
        
        // 高度な設定表示
        private bool showAdvancedBoneSettings = false;
        
        // エディタスタイル
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle sliderLabelStyle;
        private GUIStyle warningBoxStyle;
        
        // エディタ参照
        private SettingsTab settingsTab;
        
        // 現在のアバターと衣装の参照
        private GameObject currentAvatar;
        private GameObject currentCostume;
        
        // 自動更新設定
        private bool autoApply = false;
        private int autoApplyDelay = 500; // ミリ秒
        private double lastChangeTime = 0;
        
        // エラーメッセージ
        private string errorMessage = null;
        private double errorDisplayStartTime = 0;
        private const double ERROR_DISPLAY_DURATION = 5.0; // 秒
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AdjustmentTab()
        {
            InitializeAdjustmentSettings();
            
            // エディタの更新コールバックを登録
            EditorApplication.update += OnEditorUpdate;
        }
        
        /// <summary>
        /// エディタ更新時のコールバック
        /// </summary>
        private void OnEditorUpdate()
        {
            // 自動適用処理
            if (autoApply && EditorApplication.timeSinceStartup - lastChangeTime > autoApplyDelay / 1000.0)
            {
                if (lastChangeTime > 0)
                {
                    ApplyAdjustments();
                    lastChangeTime = 0;
                }
            }
            
            // エラーメッセージの期限切れ処理
            if (errorMessage != null && EditorApplication.timeSinceStartup - errorDisplayStartTime > ERROR_DISPLAY_DURATION)
            {
                errorMessage = null;
                EditorApplication.RepaintAllViews();
            }
        }
        
        /// <summary>
        /// デストラクタ
        /// </summary>
        ~AdjustmentTab()
        {
            // エディタの更新コールバックを解除
            EditorApplication.update -= OnEditorUpdate;
        }
        
        /// <summary>
        /// 設定タブの参照を設定
        /// </summary>
        public void SetSettingsTab(SettingsTab tab)
        {
            this.settingsTab = tab;
        }
        
        /// <summary>
        /// GUIの描画
        /// </summary>
        public void OnGUI(GameObject avatar, GameObject costume)
        {
            InitializeStyles();
            
            // 現在のアバターと衣装を保存
            currentAvatar = avatar;
            currentCostume = costume;
            
            EditorGUILayout.BeginVertical();
            
            // エラーメッセージの表示
            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }
            
            // 衣装インスタンスの確認とメッセージ表示
            GameObject costumeInstance = AdjustmentManager.GetCostumeInstance();
            if (costumeInstance == null || !costumeInstance.activeInHierarchy)
            {
                EditorGUILayout.HelpBox("衣装がアバターに適用されていません。「衣装を着せる」ボタンを押して、衣装を適用してください。", MessageType.Info);
            }
            
            // 衣装インスタンスが存在する場合は調整UIを表示
            if (costumeInstance != null && costumeInstance.activeInHierarchy)
            {
                // オートアップデート設定
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("調整の自動適用", GUILayout.Width(120));
                bool newAutoApply = EditorGUILayout.Toggle(autoApply);
                if (newAutoApply != autoApply)
                {
                    autoApply = newAutoApply;
                }
                
                if (autoApply)
                {
                    EditorGUILayout.LabelField("遅延(ms)", GUILayout.Width(80));
                    autoApplyDelay = EditorGUILayout.IntSlider(autoApplyDelay, 100, 2000);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // 調整方法選択
            DrawAdjustmentMethodSection();
            
            // プレビュー設定
            DrawPreviewSection();
            
            // グローバル調整
            DrawGlobalAdjustmentSection();
            
            // ボーン構造差異対応設定
            DrawBoneStructureSettingsSection();
            
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
            
            // 既存の設定があれば保持
            if (adjustmentSettings == null)
            {
                InitializeAdjustmentSettings();
            }
            
            currentAvatar = avatar;
        }
        
        /// <summary>
        /// 衣装が変更された際の処理
        /// </summary>
        public void OnCostumeChanged(GameObject costume)
        {
            if (costume == null) return;
            
            // 既存の設定があれば保持
            if (adjustmentSettings == null)
            {
                InitializeAdjustmentSettings();
            }
            
            currentCostume = costume;
        }
        
        /// <summary>
        /// 衣装適用後の処理
        /// </summary>
        public void OnCostumeApplied()
        {
            // 衣装適用後の処理（調整タブが前面に来ることが期待される）
            // 前回の調整設定を取得
            AdjustmentSettings lastSettings = AdjustmentManager.GetLastAdjustmentSettings();
            if (lastSettings != null)
            {
                adjustmentSettings = lastSettings.Clone();
            }
            
            // エラーメッセージをクリア
            errorMessage = null;
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
            
            // 調整方法を現在の選択に更新
            adjustmentSettings.method = currentMethod.ToString();
            
            // 設定タブから高度な設定を適用（ある場合のみ）
            if (settingsTab != null)
            {
                settingsTab.ApplySettingsToAdjustment(adjustmentSettings);
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
            }
            
            if (warningBoxStyle == null)
            {
                warningBoxStyle = new GUIStyle(EditorStyles.helpBox);
                warningBoxStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                warningBoxStyle.fontSize = 12;
                warningBoxStyle.wordWrap = true;
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
                rightLegScale = 1.0f,
                
                // ボーン構造差異対応設定
                detectStructuralDifferences = true,
                redistributeWeights = true,
                adjustScale = true,
                adjustRotation = true,
                adjustBindPoses = true
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
        /// ボーン構造差異対応設定セクションの描画
        /// </summary>
        private void DrawBoneStructureSettingsSection()
        {
            if (adjustmentSettings == null || currentMethod != AdjustmentMethod.BoneBased)
            {
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 折りたたみヘッダー
            showAdvancedBoneSettings = EditorGUILayout.Foldout(showAdvancedBoneSettings, "ボーン構造差異対応設定", true);
            
            if (showAdvancedBoneSettings)
            {
                EditorGUILayout.HelpBox("アバターと衣装のボーン構成が異なる場合の調整設定です。より詳細な設定は「設定」タブで行えます。", MessageType.Info);
                
                // 基本設定
                bool newDetectDiff = EditorGUILayout.Toggle("ボーン構造差異の自動検出", adjustmentSettings.detectStructuralDifferences);
                if (newDetectDiff != adjustmentSettings.detectStructuralDifferences)
                {
                    adjustmentSettings.detectStructuralDifferences = newDetectDiff;
                    RegisterSettingChange();
                }
                
                if (adjustmentSettings.detectStructuralDifferences)
                {
                    EditorGUI.indentLevel++;
                    
                    bool newAdjustBindPoses = EditorGUILayout.Toggle("バインドポーズ調整を適用", adjustmentSettings.adjustBindPoses);
                    if (newAdjustBindPoses != adjustmentSettings.adjustBindPoses)
                    {
                        adjustmentSettings.adjustBindPoses = newAdjustBindPoses;
                        RegisterSettingChange();
                    }
                    
                    bool newRedistributeWeights = EditorGUILayout.Toggle("スキンウェイト再分配を適用", adjustmentSettings.redistributeWeights);
                    if (newRedistributeWeights != adjustmentSettings.redistributeWeights)
                    {
                        adjustmentSettings.redistributeWeights = newRedistributeWeights;
                        RegisterSettingChange();
                    }
                    
                    if (adjustmentSettings.redistributeWeights)
                    {
                        EditorGUILayout.HelpBox("スキンウェイトの再分配はメッシュの読み取り権限が必要です。プロジェクト設定で「Read/Write Enabled」が有効になっていることを確認してください。", MessageType.Info);
                    }
                    
                    EditorGUI.indentLevel--;
                }
                
                // 詳細設定へのリンク
                EditorGUILayout.Space();
                
                EditorGUILayout.HelpBox("より詳細な設定は「設定」タブの「ボーン構造差異対応設定」で行えます。", MessageType.Info);
                
                if (GUILayout.Button("詳細設定を開く"))
                {
                    // メインウィンドウの設定タブを開くように要求
                    EditorPrefs.SetInt("AvatarCostumeAdjustTool_CurrentTab", 2); // 設定タブのインデックス
                }
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
            float newGlobalScale = EditorGUILayout.Slider(adjustmentSettings.globalScale, 0.5f, 1.5f);
            if (newGlobalScale != adjustmentSettings.globalScale)
            {
                adjustmentSettings.globalScale = newGlobalScale;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 上半身設定
            EditorGUILayout.LabelField("上半身", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
            float newUpperX = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetX, -0.1f, 0.1f);
            if (newUpperX != adjustmentSettings.upperBodyOffsetX)
            {
                adjustmentSettings.upperBodyOffsetX = newUpperX;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
            float newUpperY = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetY, -0.1f, 0.1f);
            if (newUpperY != adjustmentSettings.upperBodyOffsetY)
            {
                adjustmentSettings.upperBodyOffsetY = newUpperY;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
            float newUpperZ = EditorGUILayout.Slider(adjustmentSettings.upperBodyOffsetZ, -0.1f, 0.1f);
            if (newUpperZ != adjustmentSettings.upperBodyOffsetZ)
            {
                adjustmentSettings.upperBodyOffsetZ = newUpperZ;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 下半身設定
            EditorGUILayout.LabelField("下半身", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
            float newLowerX = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetX, -0.1f, 0.1f);
            if (newLowerX != adjustmentSettings.lowerBodyOffsetX)
            {
                adjustmentSettings.lowerBodyOffsetX = newLowerX;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
            float newLowerY = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetY, -0.1f, 0.1f);
            if (newLowerY != adjustmentSettings.lowerBodyOffsetY)
            {
                adjustmentSettings.lowerBodyOffsetY = newLowerY;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
            float newLowerZ = EditorGUILayout.Slider(adjustmentSettings.lowerBodyOffsetZ, -0.1f, 0.1f);
            if (newLowerZ != adjustmentSettings.lowerBodyOffsetZ)
            {
                adjustmentSettings.lowerBodyOffsetZ = newLowerZ;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 左右の腕設定
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("左腕", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            float newLeftArmScale = EditorGUILayout.Slider(adjustmentSettings.leftArmScale, 0.5f, 1.5f);
            if (newLeftArmScale != adjustmentSettings.leftArmScale)
            {
                adjustmentSettings.leftArmScale = newLeftArmScale;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("右腕", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            float newRightArmScale = EditorGUILayout.Slider(adjustmentSettings.rightArmScale, 0.5f, 1.5f);
            if (newRightArmScale != adjustmentSettings.rightArmScale)
            {
                adjustmentSettings.rightArmScale = newRightArmScale;
                RegisterSettingChange();
            }
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
            float newLeftLegScale = EditorGUILayout.Slider(adjustmentSettings.leftLegScale, 0.5f, 1.5f);
            if (newLeftLegScale != adjustmentSettings.leftLegScale)
            {
                adjustmentSettings.leftLegScale = newLeftLegScale;
                RegisterSettingChange();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("右脚", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("スケール", GUILayout.Width(80));
            float newRightLegScale = EditorGUILayout.Slider(adjustmentSettings.rightLegScale, 0.5f, 1.5f);
            if (newRightLegScale != adjustmentSettings.rightLegScale)
            {
                adjustmentSettings.rightLegScale = newRightLegScale;
                RegisterSettingChange();
            }
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
            BodyPart newSelectedBodyPart = (BodyPart)EditorGUILayout.EnumPopup("調整部位", selectedBodyPart);
            if (newSelectedBodyPart != selectedBodyPart)
            {
                selectedBodyPart = newSelectedBodyPart;
            }
            
            EditorGUILayout.Space();
            
            BodyPartAdjustment partAdjustment = adjustmentSettings.GetBodyPartAdjustment(selectedBodyPart);
            
            // 部位調整有効/無効設定
            bool newIsEnabled = EditorGUILayout.Toggle("調整を有効にする", partAdjustment.isEnabled);
            if (newIsEnabled != partAdjustment.isEnabled)
            {
                partAdjustment.isEnabled = newIsEnabled;
                RegisterSettingChange();
            }
            
            if (!partAdjustment.isEnabled)
            {
                EditorGUILayout.HelpBox("この部位の調整は現在無効になっています。", MessageType.Info);
                
                if (GUILayout.Button("調整を有効にする"))
                {
                    partAdjustment.isEnabled = true;
                    RegisterSettingChange();
                }
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // 適用する調整項目の選択
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("適用する調整", subHeaderStyle);
            
            bool newAdjustScale = EditorGUILayout.Toggle("スケール調整", partAdjustment.adjustScale);
            if (newAdjustScale != partAdjustment.adjustScale)
            {
                partAdjustment.adjustScale = newAdjustScale;
                RegisterSettingChange();
            }
            
            bool newAdjustPosition = EditorGUILayout.Toggle("位置調整", partAdjustment.adjustPosition);
            if (newAdjustPosition != partAdjustment.adjustPosition)
            {
                partAdjustment.adjustPosition = newAdjustPosition;
                RegisterSettingChange();
            }
            
            bool newAdjustRotation = EditorGUILayout.Toggle("回転調整", partAdjustment.adjustRotation);
            if (newAdjustRotation != partAdjustment.adjustRotation)
            {
                partAdjustment.adjustRotation = newAdjustRotation;
                RegisterSettingChange();
            }
            
            EditorGUILayout.Space();
            
            // スケール調整
            if (partAdjustment.adjustScale)
            {
                EditorGUILayout.LabelField("スケール調整", subHeaderStyle);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("X スケール", GUILayout.Width(120));
                float newScaleX = EditorGUILayout.Slider(partAdjustment.scaleX, 0.5f, 1.5f);
                if (newScaleX != partAdjustment.scaleX)
                {
                    partAdjustment.scaleX = newScaleX;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Y スケール", GUILayout.Width(120));
                float newScaleY = EditorGUILayout.Slider(partAdjustment.scaleY, 0.5f, 1.5f);
                if (newScaleY != partAdjustment.scaleY)
                {
                    partAdjustment.scaleY = newScaleY;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Z スケール", GUILayout.Width(120));
                float newScaleZ = EditorGUILayout.Slider(partAdjustment.scaleZ, 0.5f, 1.5f);
                if (newScaleZ != partAdjustment.scaleZ)
                {
                    partAdjustment.scaleZ = newScaleZ;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                if (GUILayout.Button("均一スケール"))
                {
                    float avgScale = (partAdjustment.scaleX + partAdjustment.scaleY + partAdjustment.scaleZ) / 3f;
                    partAdjustment.scaleX = avgScale;
                    partAdjustment.scaleY = avgScale;
                    partAdjustment.scaleZ = avgScale;
                    RegisterSettingChange();
                }
                
                EditorGUILayout.Space();
            }
            
            // 位置調整
            if (partAdjustment.adjustPosition)
            {
                EditorGUILayout.LabelField("位置調整", subHeaderStyle);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("X オフセット", GUILayout.Width(120));
                float newOffsetX = EditorGUILayout.Slider(partAdjustment.offsetX, -0.1f, 0.1f);
                if (newOffsetX != partAdjustment.offsetX)
                {
                    partAdjustment.offsetX = newOffsetX;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Y オフセット", GUILayout.Width(120));
                float newOffsetY = EditorGUILayout.Slider(partAdjustment.offsetY, -0.1f, 0.1f);
                if (newOffsetY != partAdjustment.offsetY)
                {
                    partAdjustment.offsetY = newOffsetY;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Z オフセット", GUILayout.Width(120));
                float newOffsetZ = EditorGUILayout.Slider(partAdjustment.offsetZ, -0.1f, 0.1f);
                if (newOffsetZ != partAdjustment.offsetZ)
                {
                    partAdjustment.offsetZ = newOffsetZ;
                    RegisterSettingChange();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
            }
            
            // 回転調整
            if (partAdjustment.adjustRotation)
            {
                EditorGUILayout.LabelField("回転調整", subHeaderStyle);
                
                Vector3 newRotation = EditorGUILayout.Vector3Field("回転 (度)", partAdjustment.rotation);
                if (newRotation != partAdjustment.rotation)
                {
                    partAdjustment.rotation = newRotation;
                    RegisterSettingChange();
                }
                
                EditorGUILayout.Space();
            }
            
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
            
            // プリセット名と説明
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("プリセット名", GUILayout.Width(120));
            string newPresetName = EditorGUILayout.TextField(adjustmentSettings.presetName);
            if (newPresetName != adjustmentSettings.presetName)
            {
                adjustmentSettings.presetName = newPresetName;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("説明");
            string newPresetDesc = EditorGUILayout.TextArea(
                adjustmentSettings.presetDescription,
                GUILayout.Height(60)
            );
            if (newPresetDesc != adjustmentSettings.presetDescription)
            {
                adjustmentSettings.presetDescription = newPresetDesc;
            }
            
            EditorGUILayout.Space();
            
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
        /// 設定変更を登録（自動更新用）
        /// </summary>
        private void RegisterSettingChange()
        {
            if (autoApply)
            {
                lastChangeTime = EditorApplication.timeSinceStartup;
            }
        }
        
        /// <summary>
        /// プレビュー設定の更新
        /// </summary>
        private void UpdatePreviewSettings()
        {
            // SceneViewでのプレビュー表示設定の更新
            PreviewManager.SetPreviewEnabled(showPreview);
            PreviewManager.SetPreviewOpacity(previewOpacity);
            PreviewManager.SetWireframeMode(wireframeMode);
            
            // SceneViewの再描画を要求
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 調整の適用
        /// </summary>
        private void ApplyAdjustments()
        {
            if (adjustmentSettings == null) return;
            
            // 設定タブからの設定を適用
            if (settingsTab != null)
            {
                settingsTab.ApplySettingsToAdjustment(adjustmentSettings);
            }
            
            GameObject avatarObject = FindCurrentAvatarWithAttachedCostume();
            
            if (avatarObject == null)
            {
                errorMessage = "アバターオブジェクトが見つかりませんでした。先に「衣装を着せる」を実行してください。";
                errorDisplayStartTime = EditorApplication.timeSinceStartup;
                return;
            }
            
            // 衣装インスタンスを取得してから調整を適用
            GameObject costumeInstance = AdjustmentManager.GetCostumeInstance();
            if (costumeInstance == null)
            {
                costumeInstance = FindCostumeInstance(avatarObject);
                if (costumeInstance == null)
                {
                    errorMessage = "衣装インスタンスが見つかりません。先に「衣装を着せる」を実行してください。";
                    errorDisplayStartTime = EditorApplication.timeSinceStartup;
                    return;
                }
            }
            
            try
            {
                // 選択中のアバターに対して調整を適用
                AdjustmentManager.ApplyFineAdjustment(avatarObject, adjustmentSettings);
                Debug.Log("調整を適用しました。");
                
                // エラーメッセージをクリア
                errorMessage = null;
            }
            catch (Exception ex)
            {
                errorMessage = $"調整の適用中にエラーが発生しました: {ex.Message}";
                errorDisplayStartTime = EditorApplication.timeSinceStartup;
                Debug.LogError(errorMessage);
            }
            
            // SceneViewの再描画を要求
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 現在の衣装が適用されたアバターを探す
        /// </summary>
        private GameObject FindCurrentAvatarWithAttachedCostume()
        {
            // 1. 現在のアバター参照を使用
            if (currentAvatar != null)
            {
                if (HasAttachedCostume(currentAvatar))
                {
                    return currentAvatar;
                }
            }
            
            // 2. 最後に調整を適用したアバターを使用
            GameObject lastAvatar = AdjustmentManager.GetLastAvatarObject();
            if (lastAvatar != null && lastAvatar.activeInHierarchy)
            {
                if (HasAttachedCostume(lastAvatar))
                {
                    return lastAvatar;
                }
            }
            
            // 3. 現在選択されているオブジェクトを確認
            if (Selection.activeGameObject != null)
            {
                // 選択オブジェクトがアバターかどうかを確認
                Transform selectedTransform = Selection.activeGameObject.transform;
                if (HasAttachedCostume(selectedTransform.gameObject))
                {
                    return selectedTransform.gameObject;
                }
                
                // 親階層をチェック
                Transform parent = selectedTransform.parent;
                while (parent != null)
                {
                    if (HasAttachedCostume(parent.gameObject))
                    {
                        return parent.gameObject;
                    }
                    parent = parent.parent;
                }
                
                // 子階層をチェック
                Transform[] children = selectedTransform.GetComponentsInChildren<Transform>();
                foreach (Transform child in children)
                {
                    if (child != selectedTransform && HasAttachedCostume(child.gameObject))
                    {
                        return child.gameObject;
                    }
                }
            }
            
            // 4. シーン内の全オブジェクトをチェック
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                if (HasAttachedCostume(root))
                {
                    return root;
                }
                
                // 深さ優先で子オブジェクトをチェック
                Transform[] children = root.GetComponentsInChildren<Transform>();
                foreach (Transform child in children)
                {
                    if (child != root.transform && HasAttachedCostume(child.gameObject))
                    {
                        return child.gameObject;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 衣装インスタンスを探す
        /// </summary>
        private GameObject FindCostumeInstance(GameObject avatarObject)
        {
            if (avatarObject == null) return null;
            
            // アバターの子から "_Instance" が付くオブジェクトを検索
            for (int i = 0; i < avatarObject.transform.childCount; i++)
            {
                Transform child = avatarObject.transform.GetChild(i);
                if (child.name.EndsWith("_Instance"))
                {
                    return child.gameObject;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 衣装が適用されているかを確認するヘルパーメソッド
        /// </summary>
        private bool HasAttachedCostume(GameObject obj)
        {
            if (obj == null) return false;
            
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                Transform child = obj.transform.GetChild(i);
                if (child.name.EndsWith("_Instance"))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 部位別調整の適用
        /// </summary>
        private void ApplyBodyPartAdjustments()
        {
            if (adjustmentSettings == null) return;
            
            GameObject avatarObject = FindCurrentAvatarWithAttachedCostume();
            
            if (avatarObject == null)
            {
                errorMessage = "アバターオブジェクトが見つかりませんでした。先に「衣装を着せる」を実行してください。";
                errorDisplayStartTime = EditorApplication.timeSinceStartup;
                return;
            }
            
            try
            {
                // 選択中の部位に対して調整を適用
                AdjustmentManager.ApplyBodyPartAdjustment(avatarObject, selectedBodyPart, adjustmentSettings);
                Debug.Log($"{selectedBodyPart}の調整を適用しました。");
                
                // エラーメッセージをクリア
                errorMessage = null;
            }
            catch (Exception ex)
            {
                errorMessage = $"部位調整の適用中にエラーが発生しました: {ex.Message}";
                errorDisplayStartTime = EditorApplication.timeSinceStartup;
                Debug.LogError(errorMessage);
            }
            
            // SceneViewの再描画を要求
            SceneView.RepaintAll();
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
            
            Debug.Log($"{part}の調整をリセットしました。");
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
                adjustmentSettings.presetName.Length > 0 ? adjustmentSettings.presetName : "AdjustmentPreset",
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // 設定タブからの設定を適用
                    if (settingsTab != null)
                    {
                        settingsTab.ApplySettingsToAdjustment(adjustmentSettings);
                    }
                    
                    JsonUtils.SaveToJson(path, adjustmentSettings);
                    Debug.Log($"調整プリセットを保存しました: {path}");
                }
                catch (Exception ex)
                {
                    errorMessage = $"調整プリセットの保存に失敗しました: {ex.Message}";
                    errorDisplayStartTime = EditorApplication.timeSinceStartup;
                    Debug.LogError(errorMessage);
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
                    errorMessage = $"調整プリセットの読み込みに失敗しました: {ex.Message}";
                    errorDisplayStartTime = EditorApplication.timeSinceStartup;
                    Debug.LogError(errorMessage);
                }
            }
        }
        
        #endregion
    }
}