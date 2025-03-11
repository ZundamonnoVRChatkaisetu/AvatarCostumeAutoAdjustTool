using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ボーンマッピングタブ
    /// アバターと衣装間のボーン対応関係を設定するUI
    /// </summary>
    public class BoneMappingTab
    {
        // スクロール位置
        private Vector2 scrollPosition;
        
        // フィルタリング
        private string searchQuery = "";
        private bool showOnlyUnmapped = false;
        private bool showOnlyProblematic = false;
        
        // 表示モード
        private enum ViewMode
        {
            ByAvatar,    // アバターボーン基準で表示
            ByCostume,   // 衣装ボーン基準で表示
            ByBodyPart   // 体の部位基準で表示
        }
        private ViewMode currentViewMode = ViewMode.ByAvatar;
        
        // マッピングデータ参照
        private MappingData mappingData;
        
        // ボーンリスト参照
        private List<BoneData> avatarBones = new List<BoneData>();
        private List<BoneData> costumeBones = new List<BoneData>();
        
        // 自動マッピングの信頼度しきい値
        private float mappingConfidenceThreshold = 0.7f;
        
        // エディタスタイル
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle problematicStyle;
        private GUIStyle unmappedStyle;
        private GUIStyle mappedStyle;
        
        /// <summary>
        /// GUIの描画
        /// </summary>
        public void OnGUI(GameObject avatar, GameObject costume)
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();
            
            // 自動マッピングボタン
            DrawAutoMappingSection();
            
            // フィルタとビューモード
            DrawFilteringSection();
            
            // マッピングメイン表示
            DrawMappingSection(avatar, costume);
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// アバターが変更された際の処理
        /// </summary>
        public void OnAvatarChanged(GameObject avatar)
        {
            if (avatar == null) return;
            
            // アバターのボーン情報を解析
            avatarBones = BoneIdentifier.AnalyzeAvatarBones(avatar);
            
            // マッピングデータの更新
            UpdateMappingData();
        }
        
        /// <summary>
        /// 衣装が変更された際の処理
        /// </summary>
        public void OnCostumeChanged(GameObject costume)
        {
            if (costume == null) return;
            
            // 衣装のボーン情報を解析
            costumeBones = BoneIdentifier.AnalyzeCostumeBones(costume);
            
            // マッピングデータの更新
            UpdateMappingData();
        }
        
        /// <summary>
        /// マッピングデータを取得
        /// </summary>
        public MappingData GetMappingData()
        {
            return mappingData;
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
            
            if (problematicStyle == null)
            {
                problematicStyle = new GUIStyle(EditorStyles.label);
                problematicStyle.normal.textColor = Color.red;
            }
            
            if (unmappedStyle == null)
            {
                unmappedStyle = new GUIStyle(EditorStyles.label);
                unmappedStyle.normal.textColor = new Color(1f, 0.5f, 0f); // オレンジ
            }
            
            if (mappedStyle == null)
            {
                mappedStyle = new GUIStyle(EditorStyles.label);
                mappedStyle.normal.textColor = Color.green;
            }
        }
        
        /// <summary>
        /// 自動マッピングセクションの描画
        /// </summary>
        private void DrawAutoMappingSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("自動マッピング", headerStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("信頼度しきい値", GUILayout.Width(100));
            mappingConfidenceThreshold = EditorGUILayout.Slider(mappingConfidenceThreshold, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("名前ベースマッピング"))
            {
                PerformNameBasedMapping();
            }
            
            if (GUILayout.Button("階層ベースマッピング"))
            {
                PerformHierarchyBasedMapping();
            }
            
            if (GUILayout.Button("位置ベースマッピング"))
            {
                PerformPositionBasedMapping();
            }
            
            if (GUILayout.Button("全方式でマッピング"))
            {
                PerformFullAutomaticMapping();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("マッピングをクリア"))
            {
                ClearAllMappings();
            }
            
            if (GUILayout.Button("マッピングを保存"))
            {
                SaveMappingData();
            }
            
            if (GUILayout.Button("マッピングを読込"))
            {
                LoadMappingData();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// フィルタリングセクションの描画
        /// </summary>
        private void DrawFilteringSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("フィルタリング", subHeaderStyle);
            
            // 検索ボックス
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("検索:", GUILayout.Width(40));
            searchQuery = EditorGUILayout.TextField(searchQuery);
            
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            
            // フィルタオプション
            EditorGUILayout.BeginHorizontal();
            showOnlyUnmapped = EditorGUILayout.ToggleLeft("未マッピングのみ表示", showOnlyUnmapped, GUILayout.Width(150));
            showOnlyProblematic = EditorGUILayout.ToggleLeft("問題のあるボーンのみ表示", showOnlyProblematic, GUILayout.Width(170));
            EditorGUILayout.EndHorizontal();
            
            // ビューモード
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("表示モード:", GUILayout.Width(70));
            
            if (GUILayout.Toggle(currentViewMode == ViewMode.ByAvatar, "アバター基準", EditorStyles.toolbarButton))
                currentViewMode = ViewMode.ByAvatar;
                
            if (GUILayout.Toggle(currentViewMode == ViewMode.ByCostume, "衣装基準", EditorStyles.toolbarButton))
                currentViewMode = ViewMode.ByCostume;
                
            if (GUILayout.Toggle(currentViewMode == ViewMode.ByBodyPart, "体の部位基準", EditorStyles.toolbarButton))
                currentViewMode = ViewMode.ByBodyPart;
                
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// マッピングメインセクションの描画
        /// </summary>
        private void DrawMappingSection(GameObject avatar, GameObject costume)
        {
            if (mappingData == null || avatarBones.Count == 0 || costumeBones.Count == 0)
            {
                EditorGUILayout.HelpBox("アバターと衣装を選択すると、ボーンマッピングが表示されます。", MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // マッピングタイトル
            string title = "ボーンマッピング";
            switch (currentViewMode)
            {
                case ViewMode.ByAvatar:
                    title += " (アバター基準)";
                    break;
                case ViewMode.ByCostume:
                    title += " (衣装基準)";
                    break;
                case ViewMode.ByBodyPart:
                    title += " (体の部位基準)";
                    break;
            }
            EditorGUILayout.LabelField(title, headerStyle);
            
            // 統計情報
            int totalBones = (currentViewMode == ViewMode.ByAvatar) ? avatarBones.Count : costumeBones.Count;
            int mappedBones = mappingData.GetMappedBoneCount();
            int unmappedBones = totalBones - mappedBones;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"合計: {totalBones} ボーン", GUILayout.Width(120));
            EditorGUILayout.LabelField($"マッピング済み: {mappedBones} ボーン", mappedStyle, GUILayout.Width(160));
            EditorGUILayout.LabelField($"未マッピング: {unmappedBones} ボーン", unmappedStyle, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
            
            // マッピングテーブル
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // テーブルヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            switch (currentViewMode)
            {
                case ViewMode.ByAvatar:
                    EditorGUILayout.LabelField("アバターボーン", EditorStyles.toolbarButton, GUILayout.Width(200));
                    EditorGUILayout.LabelField("対応する衣装ボーン", EditorStyles.toolbarButton, GUILayout.Width(200));
                    break;
                case ViewMode.ByCostume:
                    EditorGUILayout.LabelField("衣装ボーン", EditorStyles.toolbarButton, GUILayout.Width(200));
                    EditorGUILayout.LabelField("対応するアバターボーン", EditorStyles.toolbarButton, GUILayout.Width(200));
                    break;
                case ViewMode.ByBodyPart:
                    EditorGUILayout.LabelField("体の部位", EditorStyles.toolbarButton, GUILayout.Width(100));
                    EditorGUILayout.LabelField("アバターボーン", EditorStyles.toolbarButton, GUILayout.Width(150));
                    EditorGUILayout.LabelField("衣装ボーン", EditorStyles.toolbarButton, GUILayout.Width(150));
                    break;
            }
            
            EditorGUILayout.LabelField("信頼度", EditorStyles.toolbarButton, GUILayout.Width(60));
            EditorGUILayout.LabelField("マッピング方法", EditorStyles.toolbarButton, GUILayout.Width(120));
            EditorGUILayout.LabelField("操作", EditorStyles.toolbarButton, GUILayout.Width(110));
            
            EditorGUILayout.EndHorizontal();
            
            // マッピングリスト
            DrawMappingList();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// マッピングリストの描画
        /// </summary>
        private void DrawMappingList()
        {
            List<BoneData> primaryBones = (currentViewMode == ViewMode.ByAvatar) ? avatarBones : costumeBones;
            
            // 体の部位ごとの表示の場合
            if (currentViewMode == ViewMode.ByBodyPart)
            {
                // 体の部位ごとにグループ化
                var bodyParts = avatarBones
                    .Select(b => b.bodyPart)
                    .Distinct()
                    .OrderBy(bp => (int)bp);
                    
                foreach (var bodyPart in bodyParts)
                {
                    // 体の部位名
                    string bodyPartName = bodyPart.ToString();
                    EditorGUILayout.LabelField(bodyPartName, subHeaderStyle);
                    
                    // この部位のボーンを取得
                    var bonesInPart = avatarBones.Where(b => b.bodyPart == bodyPart).ToList();
                    
                    foreach (var bone in bonesInPart)
                    {
                        // フィルタリング
                        if (!ShouldDisplayBone(bone, true))
                            continue;
                            
                        DrawBoneMapping(bone, true);
                    }
                    
                    EditorGUILayout.Space();
                }
                
                return;
            }
            
            // アバターまたは衣装ベースの表示
            bool isAvatarBased = (currentViewMode == ViewMode.ByAvatar);
            
            foreach (var bone in primaryBones)
            {
                // フィルタリング
                if (!ShouldDisplayBone(bone, isAvatarBased))
                    continue;
                    
                DrawBoneMapping(bone, isAvatarBased);
            }
        }
        
        /// <summary>
        /// 1つのボーンマッピング行を描画
        /// </summary>
        private void DrawBoneMapping(BoneData bone, bool isAvatarBone)
        {
            EditorGUILayout.BeginHorizontal();
            
            // ボーン名の表示
            string boneName = bone.name;
            BoneData targetBone = null;
            float confidence = 0f;
            MappingMethod method = MappingMethod.NotMapped;
            bool isManuallyMapped = false;
            
            // 対応するボーンを取得
            if (isAvatarBone)
            {
                mappingData.GetCostumeBoneForAvatarBone(bone.id, out targetBone, out confidence, out method, out isManuallyMapped);
            }
            else
            {
                mappingData.GetAvatarBoneForCostumeBone(bone.id, out targetBone, out confidence, out method, out isManuallyMapped);
            }
            
            // スタイルを決定
            GUIStyle style = EditorStyles.label;
            if (targetBone == null)
            {
                style = unmappedStyle;
            }
            else if (confidence < mappingConfidenceThreshold)
            {
                style = problematicStyle;
            }
            
            // アバター/衣装ベースビューでの表示
            if (currentViewMode != ViewMode.ByBodyPart)
            {
                // 元のボーン表示
                EditorGUILayout.LabelField(boneName, style, GUILayout.Width(200));
                
                // 対応するボーン表示/選択
                if (isAvatarBone)
                {
                    DrawMappingTargetField(bone, targetBone, costumeBones);
                }
                else
                {
                    DrawMappingTargetField(bone, targetBone, avatarBones);
                }
            }
            // 体の部位ベースでの表示
            else
            {
                EditorGUILayout.LabelField(bone.bodyPart.ToString(), GUILayout.Width(100));
                
                if (isAvatarBone)
                {
                    EditorGUILayout.LabelField(boneName, style, GUILayout.Width(150));
                    DrawMappingTargetField(bone, targetBone, costumeBones, 150);
                }
            }
            
            // 信頼度表示
            string confidenceStr = targetBone != null ? $"{confidence:P0}" : "-";
            EditorGUILayout.LabelField(confidenceStr, GUILayout.Width(60));
            
            // マッピング方法表示
            string methodStr = targetBone != null ? method.ToString() : "-";
            if (isManuallyMapped)
            {
                methodStr += " (手動)";
            }
            EditorGUILayout.LabelField(methodStr, GUILayout.Width(120));
            
            // 操作ボタン
            EditorGUILayout.BeginHorizontal(GUILayout.Width(110));
            
            if (targetBone != null)
            {
                if (GUILayout.Button("除外", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                {
                    if (isAvatarBone)
                    {
                        mappingData.ExcludeAvatarBone(bone.id);
                    }
                    else
                    {
                        mappingData.ExcludeCostumeBone(bone.id);
                    }
                }
            }
            
            if (GUILayout.Button("強制対応", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            {
                // 強制対応ダイアログを表示
                ShowForceMappingDialog(bone, isAvatarBone);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// マッピング対象選択フィールドの描画
        /// </summary>
        private void DrawMappingTargetField(BoneData sourceBone, BoneData targetBone, List<BoneData> possibleTargets, float width = 200)
        {
            int selectedIndex = -1;
            if (targetBone != null)
            {
                selectedIndex = possibleTargets.FindIndex(b => b.id == targetBone.id);
            }
            
            string[] options = new string[possibleTargets.Count + 1];
            options[0] = "- 未マッピング -";
            
            for (int i = 0; i < possibleTargets.Count; i++)
            {
                options[i + 1] = possibleTargets[i].name;
            }
            
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex + 1, options, GUILayout.Width(width)) - 1;
            
            if (newSelectedIndex != selectedIndex)
            {
                bool isAvatarBone = avatarBones.Contains(sourceBone);
                
                // マッピングの更新
                if (newSelectedIndex >= 0)
                {
                    BoneData newTarget = possibleTargets[newSelectedIndex];
                    
                    if (isAvatarBone)
                    {
                        mappingData.SetManualMapping(sourceBone.id, newTarget.id);
                    }
                    else
                    {
                        mappingData.SetManualMapping(newTarget.id, sourceBone.id);
                    }
                }
                else
                {
                    // マッピング解除
                    if (isAvatarBone)
                    {
                        mappingData.RemoveMapping(sourceBone.id);
                    }
                    else
                    {
                        mappingData.RemoveMappingByCostumeBone(sourceBone.id);
                    }
                }
            }
        }
        
        /// <summary>
        /// 強制マッピングダイアログの表示
        /// </summary>
        private void ShowForceMappingDialog(BoneData bone, bool isAvatarBone)
        {
            // ダイアログ実装
            // 実装予定
        }
        
        /// <summary>
        /// 表示すべきボーンかどうかを判定
        /// </summary>
        private bool ShouldDisplayBone(BoneData bone, bool isAvatarBone)
        {
            // 検索フィルタ
            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (!bone.name.ToLower().Contains(searchQuery.ToLower()))
                {
                    return false;
                }
            }
            
            // マッピング状態によるフィルタ
            if (showOnlyUnmapped || showOnlyProblematic)
            {
                BoneData targetBone = null;
                float confidence = 0f;
                MappingMethod method = MappingMethod.NotMapped;
                bool isManuallyMapped = false;
                
                if (isAvatarBone)
                {
                    mappingData.GetCostumeBoneForAvatarBone(bone.id, out targetBone, out confidence, out method, out isManuallyMapped);
                }
                else
                {
                    mappingData.GetAvatarBoneForCostumeBone(bone.id, out targetBone, out confidence, out method, out isManuallyMapped);
                }
                
                // 未マッピングフィルタ
                if (showOnlyUnmapped && targetBone != null)
                {
                    return false;
                }
                
                // 問題ありフィルタ
                if (showOnlyProblematic && (targetBone == null || confidence >= mappingConfidenceThreshold))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// マッピングデータの更新
        /// </summary>
        private void UpdateMappingData()
        {
            if (avatarBones.Count > 0 && costumeBones.Count > 0)
            {
                if (mappingData == null)
                {
                    mappingData = new MappingData(avatarBones, costumeBones);
                }
                else
                {
                    mappingData.UpdateBoneReferences(avatarBones, costumeBones);
                }
            }
        }
        
        /// <summary>
        /// 名前ベースのマッピングを実行
        /// </summary>
        private void PerformNameBasedMapping()
        {
            if (mappingData == null) return;
            
            int mappedCount = NameBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            
            Debug.Log($"名前ベースマッピング: {mappedCount} ボーンをマッピングしました。");
        }
        
        /// <summary>
        /// 階層ベースのマッピングを実行
        /// </summary>
        private void PerformHierarchyBasedMapping()
        {
            if (mappingData == null) return;
            
            int mappedCount = HierarchyBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            
            Debug.Log($"階層ベースマッピング: {mappedCount} ボーンをマッピングしました。");
        }
        
        /// <summary>
        /// 位置ベースのマッピングを実行
        /// </summary>
        private void PerformPositionBasedMapping()
        {
            if (mappingData == null) return;
            
            int mappedCount = PositionBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            
            Debug.Log($"位置ベースマッピング: {mappedCount} ボーンをマッピングしました。");
        }
        
        /// <summary>
        /// 完全自動マッピングを実行
        /// </summary>
        private void PerformFullAutomaticMapping()
        {
            if (mappingData == null) return;
            
            // マッピングマネージャーを使用して優先順位付きのマッピングを実行
            int mappedCount = MappingManager.PerformFullMapping(
                mappingData, avatarBones, costumeBones, mappingConfidenceThreshold);
                
            Debug.Log($"完全自動マッピング: {mappedCount} ボーンをマッピングしました。");
        }
        
        /// <summary>
        /// 全マッピングをクリア
        /// </summary>
        private void ClearAllMappings()
        {
            if (mappingData == null) return;
            
            if (EditorUtility.DisplayDialog(
                "マッピングクリア確認",
                "すべてのマッピングをクリアしますか？この操作は元に戻せません。",
                "はい",
                "キャンセル"))
            {
                mappingData.ClearAllMappings();
                Debug.Log("すべてのマッピングをクリアしました。");
            }
        }
        
        /// <summary>
        /// マッピングデータを保存
        /// </summary>
        private void SaveMappingData()
        {
            if (mappingData == null) return;
            
            string path = EditorUtility.SaveFilePanel(
                "マッピングデータを保存",
                Application.dataPath,
                "BoneMapping.json",
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    mappingData.SaveToFile(path);
                    Debug.Log($"マッピングデータを保存しました: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"マッピングデータの保存に失敗しました: {ex.Message}");
                    EditorUtility.DisplayDialog("エラー", $"マッピングデータの保存に失敗しました: {ex.Message}", "OK");
                }
            }
        }
        
        /// <summary>
        /// マッピングデータを読み込み
        /// </summary>
        private void LoadMappingData()
        {
            if (mappingData == null) return;
            
            string path = EditorUtility.OpenFilePanel(
                "マッピングデータを読み込み",
                Application.dataPath,
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    mappingData.LoadFromFile(path);
                    Debug.Log($"マッピングデータを読み込みました: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"マッピングデータの読み込みに失敗しました: {ex.Message}");
                    EditorUtility.DisplayDialog("エラー", $"マッピングデータの読み込みに失敗しました: {ex.Message}", "OK");
                }
            }
        }
        
        #endregion
    }
}
