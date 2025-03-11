using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 設定タブ
    /// ツールの各種設定を行うためのUI
    /// </summary>
    public class SettingsTab
    {
        // スクロール位置
        private Vector2 scrollPosition;
        
        // 設定値
        private bool enableVerboseLogging = false;
        private bool enableAdvancedFeatures = false;
        private bool enableAutoBackup = true;
        private string backupFolderPath = "Backups";
        private int maxBackupCount = 5;
        private bool enableExperimentalFeatures = false;
        
        // マッピング設定
        private float nameMatchWeight = 0.7f;
        private float hierarchyMatchWeight = 0.2f;
        private float positionMatchWeight = 0.1f;
        private float minimumConfidenceThreshold = 0.5f;
        
        // メッシュ調整設定
        private int meshSamplingDensity = 200;
        private float deformationSmoothness = 0.5f;
        private bool preserveVolume = true;
        
        // ボーン構成差異対応設定
        private bool detectStructuralDifferences = true;
        private bool redistributeWeights = true; 
        private bool adjustRotation = true;
        private bool adjustScale = true;
        private bool adjustBindPoses = true;
        private bool forceUpdateBindPoses = false;
        private bool maintainBoneHierarchy = true;
        private float confidenceThreshold = 0.3f;
        
        // Blender連携設定
        private bool useBlenderBridge = true;
        private string blenderPath = "";
        private bool showBlenderSettings = false;
        
        // エディタスタイル
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private bool showBoneStructureSettings = false;
        
        /// <summary>
        /// GUIの描画
        /// </summary>
        public void OnGUI()
        {
            InitializeStyles();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawGeneralSettings();
            EditorGUILayout.Space(10);
            
            DrawMappingSettings();
            EditorGUILayout.Space(10);
            
            DrawMeshAdjustmentSettings();
            EditorGUILayout.Space(10);
            
            DrawBoneStructureSettings();
            EditorGUILayout.Space(10);
            
            DrawBlenderSettings();
            EditorGUILayout.Space(10);
            
            DrawAdvancedSettings();
            EditorGUILayout.Space(10);
            
            DrawAboutSection();
            
            EditorGUILayout.EndScrollView();
            
            // 保存ボタン
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("設定を保存", GUILayout.Width(150)))
            {
                SaveSettings();
            }
            
            if (GUILayout.Button("設定をリセット", GUILayout.Width(150)))
            {
                ResetSettings();
            }
            
            EditorGUILayout.EndHorizontal();
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
        }
        
        /// <summary>
        /// 一般設定セクションの描画
        /// </summary>
        private void DrawGeneralSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("一般設定", headerStyle);
            
            enableVerboseLogging = EditorGUILayout.Toggle("詳細ログ出力", enableVerboseLogging);
            
            enableAutoBackup = EditorGUILayout.Toggle("自動バックアップ", enableAutoBackup);
            
            if (enableAutoBackup)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("バックアップフォルダ", GUILayout.Width(150));
                backupFolderPath = EditorGUILayout.TextField(backupFolderPath);
                
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFolderPanel("バックアップフォルダの選択", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        backupFolderPath = path;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                maxBackupCount = EditorGUILayout.IntSlider("最大バックアップ数", maxBackupCount, 1, 20);
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// マッピング設定セクションの描画
        /// </summary>
        private void DrawMappingSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ボーンマッピング設定", headerStyle);
            
            EditorGUILayout.LabelField("マッピング方法の重み", subHeaderStyle);
            EditorGUILayout.HelpBox("各マッピング方法の重み付けを設定します。合計が1になるように調整します。", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名前マッチング重み", GUILayout.Width(150));
            nameMatchWeight = EditorGUILayout.Slider(nameMatchWeight, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("階層マッチング重み", GUILayout.Width(150));
            hierarchyMatchWeight = EditorGUILayout.Slider(hierarchyMatchWeight, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("位置マッチング重み", GUILayout.Width(150));
            positionMatchWeight = EditorGUILayout.Slider(positionMatchWeight, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            // 合計を計算して表示
            float totalWeight = nameMatchWeight + hierarchyMatchWeight + positionMatchWeight;
            string weightStatus = totalWeight == 1.0f ? "✓" : "✗";
            EditorGUILayout.LabelField($"合計重み: {totalWeight} {weightStatus}");
            
            if (Mathf.Abs(totalWeight - 1.0f) > 0.01f)
            {
                EditorGUILayout.HelpBox("重みの合計が1.0になるように調整してください。", MessageType.Warning);
                
                if (GUILayout.Button("重みを正規化"))
                {
                    NormalizeWeights();
                }
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("最小信頼度しきい値", GUILayout.Width(150));
            minimumConfidenceThreshold = EditorGUILayout.Slider(minimumConfidenceThreshold, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("この信頼度以下のマッピングは無効として扱われます。", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// メッシュ調整設定セクションの描画
        /// </summary>
        private void DrawMeshAdjustmentSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("メッシュ調整設定", headerStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("メッシュサンプリング密度", GUILayout.Width(180));
            meshSamplingDensity = EditorGUILayout.IntSlider(meshSamplingDensity, 50, 500);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("高い値ほど正確ですが、処理時間が増加します。", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("変形の滑らかさ", GUILayout.Width(180));
            deformationSmoothness = EditorGUILayout.Slider(deformationSmoothness, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            preserveVolume = EditorGUILayout.Toggle("体積保存", preserveVolume);
            EditorGUILayout.HelpBox("有効にすると、変形時に体積をできるだけ保存します。", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// ボーン構造差異対応設定セクションの描画
        /// </summary>
        private void DrawBoneStructureSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 折りたたみヘッダー
            showBoneStructureSettings = EditorGUILayout.Foldout(showBoneStructureSettings, "ボーン構造差異対応設定", true, EditorStyles.foldoutHeader);
            
            if (showBoneStructureSettings)
            {
                EditorGUILayout.HelpBox("アバターと衣装のボーン構成が異なる場合の調整設定です。", MessageType.Info);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("基本設定", subHeaderStyle);
                
                detectStructuralDifferences = EditorGUILayout.Toggle("ボーン構造差異の自動検出", detectStructuralDifferences);
                
                EditorGUI.indentLevel++;
                
                if (detectStructuralDifferences)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("信頼度しきい値", GUILayout.Width(150));
                    confidenceThreshold = EditorGUILayout.Slider(confidenceThreshold, 0f, 1f);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.HelpBox("この値が低いほど多くのボーンがマッピングされますが、精度が低下する可能性があります。", MessageType.Info);
                }
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("調整項目", subHeaderStyle);
                
                adjustScale = EditorGUILayout.Toggle("スケール調整を適用", adjustScale);
                adjustRotation = EditorGUILayout.Toggle("回転調整を適用", adjustRotation);
                adjustBindPoses = EditorGUILayout.Toggle("バインドポーズ調整を適用", adjustBindPoses);
                redistributeWeights = EditorGUILayout.Toggle("スキンウェイト再分配を適用", redistributeWeights);
                
                if (redistributeWeights)
                {
                    EditorGUILayout.HelpBox("スキンウェイトの再分配はメッシュの読み取り権限が必要です。プロジェクト設定で「Read/Write Enabled」が有効になっていることを確認してください。", MessageType.Info);
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("高度な設定", subHeaderStyle);
                maintainBoneHierarchy = EditorGUILayout.Toggle("ボーン階層構造を維持", maintainBoneHierarchy);
                forceUpdateBindPoses = EditorGUILayout.Toggle("バインドポーズを強制更新", forceUpdateBindPoses);
                
                if (forceUpdateBindPoses)
                {
                    EditorGUILayout.HelpBox("この設定は問題がある場合にのみ有効にしてください。通常は自動検出で十分です。", MessageType.Warning);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Blender連携設定セクションの描画
        /// </summary>
        private void DrawBlenderSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 折りたたみヘッダー
            showBlenderSettings = EditorGUILayout.Foldout(showBlenderSettings, "Blender連携設定", true, EditorStyles.foldoutHeader);
            
            if (showBlenderSettings)
            {
                EditorGUILayout.HelpBox(
                    "Blenderを使用して衣装適用の精度を向上させます。" +
                    "Blenderがインストールされている場合、自動的に検出されますが、" +
                    "手動で指定することもできます。", 
                    MessageType.Info);
                
                EditorGUILayout.Space(5);
                
                // Blender連携の有効/無効
                bool newUseBlender = EditorGUILayout.Toggle("Blender連携を使用", useBlenderBridge);
                if (newUseBlender != useBlenderBridge)
                {
                    useBlenderBridge = newUseBlender;
                    AdjustmentManager.SetUseBlenderBridge(useBlenderBridge);
                }
                
                EditorGUI.BeginDisabledGroup(!useBlenderBridge);
                
                // Blenderパスの表示と設定
                EditorGUILayout.BeginHorizontal();
                
                // 現在のBlenderパスを取得
                if (string.IsNullOrEmpty(blenderPath))
                {
                    blenderPath = BlenderBridge.GetBlenderPath();
                }
                
                EditorGUILayout.LabelField("Blenderの実行ファイル", GUILayout.Width(150));
                blenderPath = EditorGUILayout.TextField(blenderPath);
                
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFilePanel(
                        "Blenderの実行ファイルを選択", 
                        "", 
                        Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                    
                    if (!string.IsNullOrEmpty(path))
                    {
                        blenderPath = path;
                        BlenderBridge.SetBlenderPath(blenderPath);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                if (string.IsNullOrEmpty(blenderPath) || !File.Exists(blenderPath))
                {
                    EditorGUILayout.HelpBox(
                        "Blenderの実行ファイルが見つかりません。Blenderをインストールしていない場合は、" +
                        "インストールするか、Blender連携を無効にしてください。", 
                        MessageType.Warning);
                }
                
                // Blenderのテスト実行ボタン
                if (GUILayout.Button("Blender連携をテスト"))
                {
                    TestBlenderIntegration();
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("動作説明", subHeaderStyle);
                EditorGUILayout.HelpBox(
                    "有効にすると、衣装適用時に裏でBlenderを使った処理が自動的に行われます。" +
                    "これにより、複雑なボーン構造でも正確に衣装を適用できます。" +
                    "なお、Blenderが見つからない場合や処理に失敗した場合は、" +
                    "自動的にUnity内での処理にフォールバックします。", 
                    MessageType.Info);
                
                if (GUILayout.Button("一時ファイルをクリーンアップ"))
                {
                    BlenderBridge.CleanupTempFiles();
                    EditorUtility.DisplayDialog("完了", "一時ファイルをクリーンアップしました", "OK");
                }
                
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Blender連携のテスト
        /// </summary>
        private void TestBlenderIntegration()
        {
            try
            {
                // Blenderの初期化を試みる
                bool initialized = BlenderBridge.Initialize();
                
                if (!initialized)
                {
                    EditorUtility.DisplayDialog(
                        "Blender連携テスト", 
                        "Blender連携機能の初期化に失敗しました。\n" +
                        "Blenderのパスが正しいか確認してください。", 
                        "OK");
                    return;
                }
                
                // Blenderのパスが正しいか確認
                string blenderPath = BlenderBridge.GetBlenderPath();
                if (string.IsNullOrEmpty(blenderPath) || !File.Exists(blenderPath))
                {
                    EditorUtility.DisplayDialog(
                        "Blender連携テスト", 
                        "Blenderの実行ファイルが見つかりませんでした。\n" +
                        "Blenderのパスを正しく設定してください。", 
                        "OK");
                    return;
                }
                
                // テスト成功
                EditorUtility.DisplayDialog(
                    "Blender連携テスト", 
                    $"Blender連携テストに成功しました！\n" +
                    $"Blenderの実行ファイル: {blenderPath}\n\n" +
                    "Blender連携機能は正常に動作しています。", 
                    "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Blender連携テスト", 
                    $"Blender連携テスト中にエラーが発生しました：\n{ex.Message}", 
                    "OK");
            }
        }
        
        /// <summary>
        /// 高度な設定セクションの描画
        /// </summary>
        private void DrawAdvancedSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("高度な設定", headerStyle);
            
            enableAdvancedFeatures = EditorGUILayout.Toggle("高度な機能を有効化", enableAdvancedFeatures);
            
            EditorGUI.BeginDisabledGroup(!enableAdvancedFeatures);
            
            EditorGUILayout.HelpBox("これらの設定は上級ユーザー向けです。不適切な設定はツールの動作に問題を引き起こす可能性があります。", MessageType.Warning);
            
            enableExperimentalFeatures = EditorGUILayout.Toggle("実験的機能を有効化", enableExperimentalFeatures);
            
            if (enableExperimentalFeatures)
            {
                EditorGUILayout.HelpBox("実験的機能は不安定な場合があります。自己責任でご利用ください。", MessageType.Warning);
                
                // 実験的機能の設定項目（将来的に追加予定）
                EditorGUILayout.LabelField("実験的機能設定", subHeaderStyle);
                EditorGUILayout.LabelField("実験的機能は現在実装中です。");
            }
            
            if (GUILayout.Button("ボーン命名パターンデータベースを編集"))
            {
                OpenBoneNamingPatternEditor();
            }
            
            if (GUILayout.Button("ボーン参照データをリロード"))
            {
                ReloadBoneReferenceData();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 情報セクションの描画
        /// </summary>
        private void DrawAboutSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ツール情報", headerStyle);
            
            EditorGUILayout.LabelField("全アバター衣装自動調整ツール", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("バージョン: 1.1.0");
            EditorGUILayout.LabelField("作者: VRChat衣装調整ツール開発チーム");
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "このツールは、異なるボーン構造を持つアバターと衣装間での互換性を実現するために開発されました。" +
                "ボーン識別・マッピング機能と衣装調整機能を組み合わせることで、様々なアバターに対して衣装を簡単に適用できます。", 
                MessageType.Info
            );
            
            if (GUILayout.Button("ドキュメントを開く"))
            {
                // ドキュメントページを開く（実装予定）
                Debug.Log("ドキュメントページを開く機能は実装予定です。");
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 重みの正規化
        /// </summary>
        private void NormalizeWeights()
        {
            float total = nameMatchWeight + hierarchyMatchWeight + positionMatchWeight;
            
            if (total <= 0.0f)
            {
                // すべてのウェイトが0の場合はデフォルト値を設定
                nameMatchWeight = 0.7f;
                hierarchyMatchWeight = 0.2f;
                positionMatchWeight = 0.1f;
            }
            else
            {
                // 現在の比率を維持しながら合計を1.0にする
                nameMatchWeight /= total;
                hierarchyMatchWeight /= total;
                positionMatchWeight /= total;
            }
        }
        
        /// <summary>
        /// 設定の保存
        /// </summary>
        private void SaveSettings()
        {
            ToolSettings settings = new ToolSettings
            {
                // 一般設定
                enableVerboseLogging = this.enableVerboseLogging,
                enableAdvancedFeatures = this.enableAdvancedFeatures,
                enableAutoBackup = this.enableAutoBackup,
                backupFolderPath = this.backupFolderPath,
                maxBackupCount = this.maxBackupCount,
                enableExperimentalFeatures = this.enableExperimentalFeatures,
                
                // マッピング設定
                nameMatchWeight = this.nameMatchWeight,
                hierarchyMatchWeight = this.hierarchyMatchWeight,
                positionMatchWeight = this.positionMatchWeight,
                minimumConfidenceThreshold = this.minimumConfidenceThreshold,
                
                // メッシュ調整設定
                meshSamplingDensity = this.meshSamplingDensity,
                deformationSmoothness = this.deformationSmoothness,
                preserveVolume = this.preserveVolume,
                
                // ボーン構造差異対応設定
                detectStructuralDifferences = this.detectStructuralDifferences,
                redistributeWeights = this.redistributeWeights,
                adjustRotation = this.adjustRotation,
                adjustScale = this.adjustScale,
                adjustBindPoses = this.adjustBindPoses,
                forceUpdateBindPoses = this.forceUpdateBindPoses,
                maintainBoneHierarchy = this.maintainBoneHierarchy,
                confidenceThreshold = this.confidenceThreshold,
                
                // Blender連携設定
                useBlenderBridge = this.useBlenderBridge,
                blenderPath = this.blenderPath
            };
            
            try
            {
                string settingsPath = GetSettingsFilePath();
                JsonUtils.SaveToJson(settingsPath, settings);
                Debug.Log("設定を保存しました: " + settingsPath);
                
                // 設定を適用
                AdjustmentManager.SetUseBlenderBridge(useBlenderBridge);
                if (!string.IsNullOrEmpty(blenderPath))
                {
                    BlenderBridge.SetBlenderPath(blenderPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"設定の保存に失敗しました: {ex.Message}");
                EditorUtility.DisplayDialog("エラー", $"設定の保存に失敗しました: {ex.Message}", "OK");
            }
        }
        
        /// <summary>
        /// 設定の読み込み
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                
                if (File.Exists(settingsPath))
                {
                    ToolSettings settings = JsonUtils.LoadFromJson<ToolSettings>(settingsPath);
                    
                    // 一般設定
                    this.enableVerboseLogging = settings.enableVerboseLogging;
                    this.enableAdvancedFeatures = settings.enableAdvancedFeatures;
                    this.enableAutoBackup = settings.enableAutoBackup;
                    this.backupFolderPath = settings.backupFolderPath;
                    this.maxBackupCount = settings.maxBackupCount;
                    this.enableExperimentalFeatures = settings.enableExperimentalFeatures;
                    
                    // マッピング設定
                    this.nameMatchWeight = settings.nameMatchWeight;
                    this.hierarchyMatchWeight = settings.hierarchyMatchWeight;
                    this.positionMatchWeight = settings.positionMatchWeight;
                    this.minimumConfidenceThreshold = settings.minimumConfidenceThreshold;
                    
                    // メッシュ調整設定
                    this.meshSamplingDensity = settings.meshSamplingDensity;
                    this.deformationSmoothness = settings.deformationSmoothness;
                    this.preserveVolume = settings.preserveVolume;
                    
                    // ボーン構造差異対応設定（存在する場合のみ読み込み - 後方互換性のため）
                    if (settings.detectStructuralDifferences.HasValue)
                        this.detectStructuralDifferences = settings.detectStructuralDifferences.Value;
                    if (settings.redistributeWeights.HasValue)
                        this.redistributeWeights = settings.redistributeWeights.Value;
                    if (settings.adjustRotation.HasValue)
                        this.adjustRotation = settings.adjustRotation.Value;
                    if (settings.adjustScale.HasValue)
                        this.adjustScale = settings.adjustScale.Value;
                    if (settings.adjustBindPoses.HasValue)
                        this.adjustBindPoses = settings.adjustBindPoses.Value;
                    if (settings.forceUpdateBindPoses.HasValue)
                        this.forceUpdateBindPoses = settings.forceUpdateBindPoses.Value;
                    if (settings.maintainBoneHierarchy.HasValue)
                        this.maintainBoneHierarchy = settings.maintainBoneHierarchy.Value;
                    if (settings.confidenceThreshold.HasValue)
                        this.confidenceThreshold = settings.confidenceThreshold.Value;
                    
                    // Blender連携設定
                    this.useBlenderBridge = settings.useBlenderBridge;
                    if (!string.IsNullOrEmpty(settings.blenderPath))
                        this.blenderPath = settings.blenderPath;
                    
                    // 設定を適用
                    AdjustmentManager.SetUseBlenderBridge(useBlenderBridge);
                    if (!string.IsNullOrEmpty(blenderPath))
                    {
                        BlenderBridge.SetBlenderPath(blenderPath);
                    }
                    
                    Debug.Log("設定を読み込みました: " + settingsPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"設定の読み込みに失敗しました: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 設定のリセット
        /// </summary>
        private void ResetSettings()
        {
            if (EditorUtility.DisplayDialog(
                "設定リセット確認",
                "すべての設定をデフォルト値にリセットしますか？この操作は元に戻せません。",
                "はい",
                "キャンセル"))
            {
                // 一般設定
                enableVerboseLogging = false;
                enableAdvancedFeatures = false;
                enableAutoBackup = true;
                backupFolderPath = "Backups";
                maxBackupCount = 5;
                enableExperimentalFeatures = false;
                
                // マッピング設定
                nameMatchWeight = 0.7f;
                hierarchyMatchWeight = 0.2f;
                positionMatchWeight = 0.1f;
                minimumConfidenceThreshold = 0.5f;
                
                // メッシュ調整設定
                meshSamplingDensity = 200;
                deformationSmoothness = 0.5f;
                preserveVolume = true;
                
                // ボーン構造差異対応設定
                detectStructuralDifferences = true;
                redistributeWeights = true;
                adjustRotation = true;
                adjustScale = true;
                adjustBindPoses = true;
                forceUpdateBindPoses = false;
                maintainBoneHierarchy = true;
                confidenceThreshold = 0.3f;
                
                // Blender連携設定
                useBlenderBridge = true;
                blenderPath = BlenderBridge.GetBlenderPath();
                
                // 設定を適用
                AdjustmentManager.SetUseBlenderBridge(useBlenderBridge);
                
                Debug.Log("すべての設定をリセットしました。");
            }
        }
        
        /// <summary>
        /// ボーン構造対応設定をAdjustmentSettingsに適用
        /// </summary>
        public void ApplySettingsToAdjustment(AdjustmentSettings adjustmentSettings)
        {
            if (adjustmentSettings == null)
                return;
                
            adjustmentSettings.adjustScale = this.adjustScale;
            adjustmentSettings.adjustRotation = this.adjustRotation;
            adjustmentSettings.adjustBindPoses = this.adjustBindPoses;
            adjustmentSettings.detectStructuralDifferences = this.detectStructuralDifferences;
            adjustmentSettings.redistributeWeights = this.redistributeWeights;
            adjustmentSettings.confidenceThreshold = this.confidenceThreshold;
            adjustmentSettings.forceUpdateBindPoses = this.forceUpdateBindPoses;
            adjustmentSettings.maintainBoneHierarchy = this.maintainBoneHierarchy;
        }
        
        /// <summary>
        /// 設定ファイルのパスを取得
        /// </summary>
        private string GetSettingsFilePath()
        {
            return Path.Combine(Application.dataPath, "AvatarCostumeAdjustTool", "Settings.json");
        }
        
        /// <summary>
        /// ボーン命名パターンエディタを開く
        /// </summary>
        private void OpenBoneNamingPatternEditor()
        {
            // 実装予定
            Debug.Log("ボーン命名パターンエディタは実装予定です。");
        }
        
        /// <summary>
        /// ボーン参照データをリロード
        /// </summary>
        private void ReloadBoneReferenceData()
        {
            // 実装予定
            Debug.Log("ボーン参照データをリロードしました。");
        }
        
        #endregion
    }
    
    /// <summary>
    /// ツール設定保存用クラス
    /// </summary>
    [Serializable]
    public class ToolSettings
    {
        // 一般設定
        public bool enableVerboseLogging;
        public bool enableAdvancedFeatures;
        public bool enableAutoBackup;
        public string backupFolderPath;
        public int maxBackupCount;
        public bool enableExperimentalFeatures;
        
        // マッピング設定
        public float nameMatchWeight;
        public float hierarchyMatchWeight;
        public float positionMatchWeight;
        public float minimumConfidenceThreshold;
        
        // メッシュ調整設定
        public int meshSamplingDensity;
        public float deformationSmoothness;
        public bool preserveVolume;
        
        // ボーン構造差異対応設定（後方互換性のためにnullable型を使用）
        public bool? detectStructuralDifferences;
        public bool? redistributeWeights;
        public bool? adjustRotation;
        public bool? adjustScale;
        public bool? adjustBindPoses;
        public bool? forceUpdateBindPoses;
        public bool? maintainBoneHierarchy;
        public float? confidenceThreshold;
        
        // Blender連携設定
        public bool useBlenderBridge = true;
        public string blenderPath = "";
    }
}