using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// メインのエディタウィンドウクラス
    /// 全アバター衣装自動調整ツールのエントリーポイント
    /// </summary>
    public class AvatarCostumeAdjustTool : EditorWindow
    {
        // タブ管理
        private enum TabType
        {
            BoneMapping,
            Adjustment,
            Settings
        }
        private TabType currentTab = TabType.BoneMapping;

        // エディタウィンドウに表示するアバターと衣装
        private GameObject avatarObject;
        private GameObject costumeObject;

        // UIスクロール位置
        private Vector2 scrollPosition;

        // タブコンテンツ
        private BoneMappingTab boneMappingTab;
        private AdjustmentTab adjustmentTab;
        private SettingsTab settingsTab;

        // ウィンドウを開くメニューアイテム
        [MenuItem("Tools/Avatar Costume Auto Adjust Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCostumeAdjustTool>("衣装自動調整ツール");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        // ウィンドウ初期化時に呼ばれる
        private void OnEnable()
        {
            // アイコンの設定
            var icon = EditorGUIUtility.IconContent("d_Animator Icon").image as Texture2D;
            titleContent.image = icon;

            // タブの初期化
            InitializeTabs();
        }

        // タブ初期化
        private void InitializeTabs()
        {
            boneMappingTab = new BoneMappingTab();
            adjustmentTab = new AdjustmentTab();
            settingsTab = new SettingsTab();
        }

        // GUI描画
        private void OnGUI()
        {
            DrawHeader();
            DrawObjectSelectionArea();
            DrawTabButtons();
            DrawTabContent();
        }

        // ヘッダー描画
        private void DrawHeader()
        {
            EditorGUILayout.Space();
            GUILayout.Label("全アバター衣装自動調整ツール", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // オブジェクト選択エリア描画
        private void DrawObjectSelectionArea()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アバター", GUILayout.Width(80));
            GameObject newAvatarObject = (GameObject)EditorGUILayout.ObjectField(
                avatarObject, typeof(GameObject), true);
            
            if (newAvatarObject != avatarObject)
            {
                avatarObject = newAvatarObject;
                OnAvatarChanged();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("衣装", GUILayout.Width(80));
            GameObject newCostumeObject = (GameObject)EditorGUILayout.ObjectField(
                costumeObject, typeof(GameObject), true);
            
            if (newCostumeObject != costumeObject)
            {
                costumeObject = newCostumeObject;
                OnCostumeChanged();
            }
            EditorGUILayout.EndHorizontal();

            bool areObjectsSelected = avatarObject != null && costumeObject != null;
            GUI.enabled = areObjectsSelected;
            
            if (GUILayout.Button("衣装を着せる"))
            {
                ApplyCostume();
            }
            
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        // タブボタン描画
        private void DrawTabButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Toggle(currentTab == TabType.BoneMapping, "ボーンマッピング", EditorStyles.toolbarButton))
                currentTab = TabType.BoneMapping;
            
            if (GUILayout.Toggle(currentTab == TabType.Adjustment, "調整", EditorStyles.toolbarButton))
                currentTab = TabType.Adjustment;
            
            if (GUILayout.Toggle(currentTab == TabType.Settings, "設定", EditorStyles.toolbarButton))
                currentTab = TabType.Settings;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        // タブコンテンツ描画
        private void DrawTabContent()
        {
            bool areObjectsSelected = avatarObject != null && costumeObject != null;
            
            if (!areObjectsSelected)
            {
                EditorGUILayout.HelpBox("アバターと衣装を選択してください。", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            switch (currentTab)
            {
                case TabType.BoneMapping:
                    boneMappingTab.OnGUI(avatarObject, costumeObject);
                    break;
                case TabType.Adjustment:
                    adjustmentTab.OnGUI(avatarObject, costumeObject);
                    break;
                case TabType.Settings:
                    settingsTab.OnGUI();
                    break;
            }
            
            EditorGUILayout.EndScrollView();
        }

        // アバター変更時の処理
        private void OnAvatarChanged()
        {
            if (avatarObject != null)
            {
                // アバターのボーン構造を解析
                boneMappingTab.OnAvatarChanged(avatarObject);
                adjustmentTab.OnAvatarChanged(avatarObject);
            }
        }

        // 衣装変更時の処理
        private void OnCostumeChanged()
        {
            if (costumeObject != null)
            {
                // 衣装のボーン構造を解析
                boneMappingTab.OnCostumeChanged(costumeObject);
                adjustmentTab.OnCostumeChanged(costumeObject);
            }
        }

        // 衣装適用処理
        private void ApplyCostume()
        {
            if (avatarObject == null || costumeObject == null)
                return;

            // 衣装適用の一連の処理を実行
            // 1. マッピングデータの取得
            var mappingData = boneMappingTab.GetMappingData();
            
            // 2. 調整設定の取得
            var adjustmentSettings = adjustmentTab.GetAdjustmentSettings();
            
            // 3. 実際の衣装適用処理
            AdjustmentManager.ApplyCostume(
                avatarObject, 
                costumeObject, 
                mappingData, 
                adjustmentSettings
            );
            
            // 4. 調整タブの更新
            adjustmentTab.OnCostumeApplied();
            
            // 5. エディタの更新を強制
            EditorUtility.SetDirty(avatarObject);
            AssetDatabase.SaveAssets();
            
            Debug.Log("衣装を適用しました！");
        }
    }
}
