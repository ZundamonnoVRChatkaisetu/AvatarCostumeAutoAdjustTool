using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// Blenderとの連携を管理するユーティリティクラス
    /// Unityエディタの裏でBlenderを使って処理を行います
    /// </summary>
    public static class BlenderBridge
    {
        // Blenderの実行パス（自動検出または設定から取得）
        private static string blenderPath = null;
        
        // 一時ファイル用のディレクトリ
        private static string tempDir = Path.Combine(Path.GetTempPath(), "AvatarCostumeAdjustTool");
        
        // Pythonスクリプトのパス
        private static string scriptDir = Path.Combine(Application.dataPath, "Toul", "AvatarCostumeAutoAdjustTool", "BlenderScripts");
        
        // 初期化済みフラグ
        private static bool initialized = false;

        /// <summary>
        /// Blender連携機能を初期化
        /// </summary>
        public static bool Initialize()
        {
            if (initialized)
                return true;
            
            try
            {
                // 一時ディレクトリの作成
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                // スクリプトディレクトリの作成
                if (!Directory.Exists(scriptDir))
                {
                    Directory.CreateDirectory(scriptDir);
                    CreateDefaultScripts();
                }
                
                // Blenderのパスを取得
                FindBlenderPath();
                
                initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Blender連携の初期化に失敗しました: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Blenderを使って衣装をアバターに適用
        /// </summary>
        public static bool ApplyCostumeWithBlender(
            GameObject avatarObject, 
            GameObject costumeObject, 
            MappingData mappingData, 
            AdjustmentSettings settings,
            Action<float> progressCallback = null)
        {
            if (!Initialize())
            {
                EditorUtility.DisplayDialog("エラー", 
                    "Blender連携機能の初期化に失敗しました。Unity内での処理を試みます。", "OK");
                return false;
            }
            
            try
            {
                // 進捗報告
                progressCallback?.Invoke(0.1f);
                
                // 一時ファイル名を生成
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string avatarFile = Path.Combine(tempDir, $"avatar_{timestamp}.fbx");
                string costumeFile = Path.Combine(tempDir, $"costume_{timestamp}.fbx");
                string resultFile = Path.Combine(tempDir, $"result_{timestamp}.fbx");
                string mappingFile = Path.Combine(tempDir, $"mapping_{timestamp}.json");
                string settingsFile = Path.Combine(tempDir, $"settings_{timestamp}.json");
                
                // アバターとコスチュームをFBXにエクスポート
                progressCallback?.Invoke(0.2f);
                bool exportSuccess = ExportObjectsToFbx(avatarObject, avatarFile, costumeObject, costumeFile);
                if (!exportSuccess)
                {
                    UnityEngine.Debug.LogError("FBXのエクスポートに失敗しました");
                    EditorUtility.DisplayDialog("エラー", "FBXのエクスポートに失敗しました。Unity内での処理を試みます。", "OK");
                    return false;
                }
                
                // マッピングデータと設定の保存
                progressCallback?.Invoke(0.3f);
                SaveMappingData(mappingData, mappingFile);
                SaveSettings(settings, settingsFile);
                
                // Blenderスクリプトを実行
                progressCallback?.Invoke(0.4f);
                bool blenderSuccess = RunBlenderProcess(avatarFile, costumeFile, resultFile, mappingFile, settingsFile);
                if (!blenderSuccess)
                {
                    UnityEngine.Debug.LogError("Blenderでの処理に失敗しました");
                    EditorUtility.DisplayDialog("エラー", "Blenderでの処理に失敗しました。Unity内での処理を試みます。", "OK");
                    return false;
                }
                
                // 結果のインポート
                progressCallback?.Invoke(0.8f);
                GameObject resultObject = ImportBlenderResult(resultFile, avatarObject);
                if (resultObject == null)
                {
                    UnityEngine.Debug.LogError("Blender処理結果のインポートに失敗しました");
                    EditorUtility.DisplayDialog("エラー", "Blender処理結果のインポートに失敗しました。Unity内での処理を試みます。", "OK");
                    return false;
                }
                
                // 完了
                progressCallback?.Invoke(1.0f);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Blenderを使った衣装適用中にエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("エラー", 
                    $"Blenderを使った衣装適用中にエラーが発生しました。Unity内での処理を試みます。\n{ex.Message}", "OK");
                return false;
            }
        }
        
        /// <summary>
        /// オブジェクトをFBXとしてエクスポート
        /// </summary>
        private static bool ExportObjectsToFbx(
            GameObject avatarObject, 
            string avatarFile, 
            GameObject costumeObject, 
            string costumeFile)
        {
            try
            {
                // アバターのエクスポート
                bool avatarExport = ExportToFbx(avatarObject, avatarFile);
                
                // 衣装のエクスポート
                bool costumeExport = ExportToFbx(costumeObject, costumeFile);
                
                return avatarExport && costumeExport;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"FBXエクスポート中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 単一オブジェクトをFBXとしてエクスポート
        /// </summary>
        private static bool ExportToFbx(GameObject gameObject, string filePath)
        {
            try
            {
                // GameObjectのコピーを作成
                GameObject tempObject = GameObject.Instantiate(gameObject);
                tempObject.name = gameObject.name;
                
                // オブジェクトをシーンのルートに配置
                tempObject.transform.SetParent(null);
                
                // FBXエクスポート設定
                string tempFbxPath = filePath;
                bool success = false;
                
                // Unity FBX Exporter APIを使用
                UnityEditor.EditorUtility.DisplayProgressBar("FBXエクスポート", $"{gameObject.name} をエクスポートしています...", 0.5f);
                
                // Unity バージョンに応じたエクスポート方法を選択
                // リフレクションを使用して存在するメソッドやクラスを検出
                try
                {
                    // GameObject選択を設定
                    Selection.activeGameObject = tempObject;
                    
                    // エクスポートメニューを実行する代わりに、ExportPackageを使用
                    // 対象のパスにFBXを直接書き出す
                    string targetDirectory = Path.GetDirectoryName(tempFbxPath);
                    string assetPath = AssetDatabase.GetAssetPath(tempObject);
                    
                    // プレハブ化して一時的にAssetとして保存
                    string tempPrefabPath = "Assets/Temp_" + tempObject.name + ".prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, tempPrefabPath);
                    
                    if (prefab != null)
                    {
                        // FBXエクスポート用のエディタウィンドウを開くメニュー項目を実行
                        // この方法は理想的ではありませんが、代替APIがない場合の回避策
                        EditorApplication.ExecuteMenuItem("Assets/Export Selection...");
                        
                        // エクスポートダイアログが表示されるので、ユーザーが手動で保存する必要がある
                        EditorUtility.DisplayDialog("FBXエクスポート", 
                            $"表示されるダイアログで、ファイル名を\n{tempFbxPath}\nに設定し、「保存」をクリックしてください。", "OK");
                        
                        // 一時プレハブを削除
                        AssetDatabase.DeleteAsset(tempPrefabPath);
                        
                        // ファイルが作成されたかチェック（ユーザーがキャンセルした可能性もある）
                        if (File.Exists(tempFbxPath))
                        {
                            success = true;
                        }
                        else
                        {
                            // 代替手段として、シーンをFBXとして保存
                            UnityEngine.Debug.LogWarning("FBXエクスポートダイアログでの保存が確認できませんでした。代替手段を試みます。");
                            success = ExportSceneToFbx(tempObject, tempFbxPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"FBXエクスポート中に例外が発生しました: {ex.Message}");
                    success = false;
                }
                
                // 一時オブジェクトを削除
                GameObject.DestroyImmediate(tempObject);
                
                UnityEditor.EditorUtility.ClearProgressBar();
                return success;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"FBXエクスポート中にエラーが発生しました: {ex.Message}");
                UnityEditor.EditorUtility.ClearProgressBar();
                return false;
            }
        }
        
        /// <summary>
        /// シーンをFBXとしてエクスポート（代替手段）
        /// </summary>
        private static bool ExportSceneToFbx(GameObject targetObject, string filePath)
        {
            try
            {
                // 選択を一時的に保存
                GameObject[] currentSelection = Selection.gameObjects;
                
                // エクスポート対象のみを選択
                Selection.activeGameObject = targetObject;
                
                // アセットのエクスポート
                string tempPrefabPath = "Assets/Temp_" + targetObject.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(targetObject, tempPrefabPath);
                
                // FBXエクスポート用メニュー（これは手動での操作が必要）
                EditorApplication.ExecuteMenuItem("Assets/Export Selection...");
                
                // ユーザーにFBXファイルを保存するように指示
                EditorUtility.DisplayDialog("FBXエクスポート", 
                    $"表示されるダイアログで、ファイル名を\n{filePath}\nに設定し、「保存」をクリックしてください。", "OK");
                
                // 一時プレハブを削除
                AssetDatabase.DeleteAsset(tempPrefabPath);
                
                // 元の選択を復元
                Selection.objects = currentSelection;
                
                // ファイルが作成されたかチェック
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"シーンのFBXエクスポート中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// マッピングデータをJSONとして保存
        /// </summary>
        private static void SaveMappingData(MappingData mappingData, string filePath)
        {
            try
            {
                string json = JsonUtility.ToJson(mappingData, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"マッピングデータの保存中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 調整設定をJSONとして保存
        /// </summary>
        private static void SaveSettings(AdjustmentSettings settings, string filePath)
        {
            try
            {
                string json = JsonUtility.ToJson(settings, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"調整設定の保存中にエラーが発生しました: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Blenderプロセスを実行
        /// </summary>
        private static bool RunBlenderProcess(
            string avatarFile, 
            string costumeFile, 
            string resultFile, 
            string mappingFile, 
            string settingsFile)
        {
            try
            {
                // Blenderが見つからない場合
                if (string.IsNullOrEmpty(blenderPath) || !File.Exists(blenderPath))
                {
                    FindBlenderPath();
                    if (string.IsNullOrEmpty(blenderPath) || !File.Exists(blenderPath))
                    {
                        UnityEngine.Debug.LogError("Blenderの実行ファイルが見つかりません");
                        EditorUtility.DisplayDialog("エラー", "Blenderの実行ファイルが見つかりません。設定で指定してください。", "OK");
                        return false;
                    }
                }
                
                // プロセス開始情報の設定
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = blenderPath,
                    Arguments = $"--background --python \"{GetCostumeApplyScriptPath()}\" -- \"{avatarFile}\" \"{costumeFile}\" \"{resultFile}\" \"{mappingFile}\" \"{settingsFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                // Blenderプロセスを開始
                using (Process process = new Process { StartInfo = startInfo })
                {
                    UnityEngine.Debug.Log($"Blenderを起動: {startInfo.FileName} {startInfo.Arguments}");
                    
                    StringBuilder output = new StringBuilder();
                    StringBuilder error = new StringBuilder();
                    
                    process.OutputDataReceived += (sender, args) => 
                    {
                        if (args.Data != null)
                        {
                            output.AppendLine(args.Data);
                            UnityEngine.Debug.Log($"Blender: {args.Data}");
                        }
                    };
                    
                    process.ErrorDataReceived += (sender, args) => 
                    {
                        if (args.Data != null)
                        {
                            error.AppendLine(args.Data);
                            UnityEngine.Debug.LogError($"Blender Error: {args.Data}");
                        }
                    };
                    
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    // プログレスバーを表示しながら待機
                    int timeout = 300; // 5分のタイムアウト
                    int elapsed = 0;
                    
                    while (!process.HasExited && elapsed < timeout)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Blender処理中", 
                            "Blenderでの衣装適用処理を実行中...", 
                            elapsed / (float)timeout))
                        {
                            // キャンセルされた場合
                            process.Kill();
                            EditorUtility.ClearProgressBar();
                            UnityEngine.Debug.Log("Blender処理がキャンセルされました");
                            return false;
                        }
                        
                        System.Threading.Thread.Sleep(1000); // 1秒待機
                        elapsed++;
                    }
                    
                    EditorUtility.ClearProgressBar();
                    
                    // タイムアウトチェック
                    if (!process.HasExited)
                    {
                        process.Kill();
                        UnityEngine.Debug.LogError("Blender処理がタイムアウトしました");
                        return false;
                    }
                    
                    // 終了コードのチェック
                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"Blenderが異常終了しました。終了コード: {process.ExitCode}\nエラー: {error}");
                        return false;
                    }
                    
                    // 成功
                    UnityEngine.Debug.Log($"Blender処理が完了しました\n{output}");
                    return File.Exists(resultFile);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Blenderプロセス実行中にエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.ClearProgressBar();
                return false;
            }
        }
        
        /// <summary>
        /// Blender処理結果をインポート
        /// </summary>
        private static GameObject ImportBlenderResult(string resultFile, GameObject avatarObject)
        {
            try
            {
                // FBXインポート設定
                UnityEditor.AssetDatabase.ImportAsset(resultFile, ImportAssetOptions.ForceSynchronousImport);
                
                // インポートされたオブジェクトを取得
                GameObject importedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(resultFile);
                if (importedPrefab == null)
                {
                    UnityEngine.Debug.LogError($"インポートされたプレハブが見つかりません: {resultFile}");
                    return null;
                }
                
                // アバターの子としてインスタンス化
                GameObject instance = GameObject.Instantiate(importedPrefab);
                instance.name = $"{importedPrefab.name}_Instance";
                
                instance.transform.SetParent(avatarObject.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                
                // 一時ファイルをクリーンアップ
                AssetDatabase.DeleteAsset(resultFile);
                
                return instance;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Blender処理結果のインポート中にエラーが発生しました: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// デフォルトのBlenderスクリプトを作成
        /// </summary>
        private static void CreateDefaultScripts()
        {
            string scriptPath = GetCostumeApplyScriptPath();
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath));
            
            string scriptContent = @"# Script to apply costume to avatar in Blender
import bpy
import sys
import json
import os
import math
from mathutils import Vector, Matrix, Quaternion

# Get arguments
argv = sys.argv
argv = argv[argv.index('--') + 1:]  # Get all args after --

if len(argv) < 5:
    print('Error: Not enough arguments. Usage: blender --background --python script.py -- avatarFile costumeFile resultFile mappingFile settingsFile')
    sys.exit(1)

avatar_file = argv[0]
costume_file = argv[1]
result_file = argv[2]
mapping_file = argv[3]
settings_file = argv[4]

print(f'Processing files:')
print(f'Avatar: {avatar_file}')
print(f'Costume: {costume_file}')
print(f'Result: {result_file}')
print(f'Mapping: {mapping_file}')
print(f'Settings: {settings_file}')

# Clear scene
bpy.ops.wm.read_factory_settings(use_empty=True)
for obj in bpy.data.objects:
    bpy.data.objects.remove(obj)

# Import avatar
bpy.ops.import_scene.fbx(filepath=avatar_file)
avatar_objects = bpy.context.selected_objects
if not avatar_objects:
    print('Error: No avatar objects imported')
    sys.exit(1)

# Find avatar armature
avatar_armature = None
for obj in avatar_objects:
    if obj.type == 'ARMATURE':
        avatar_armature = obj
        break

if not avatar_armature:
    print('Error: No armature found in avatar file')
    sys.exit(1)

# Import costume
bpy.ops.import_scene.fbx(filepath=costume_file)
costume_objects = [obj for obj in bpy.context.selected_objects if obj not in avatar_objects]
if not costume_objects:
    print('Error: No costume objects imported')
    sys.exit(1)

# Find costume armature
costume_armature = None
costume_meshes = []
for obj in costume_objects:
    if obj.type == 'ARMATURE':
        costume_armature = obj
    elif obj.type == 'MESH':
        costume_meshes.append(obj)

if not costume_armature and not costume_meshes:
    print('Error: No armature or meshes found in costume file')
    sys.exit(1)

# Load mapping and settings
with open(mapping_file, 'r') as f:
    mapping_data = json.load(f)

with open(settings_file, 'r') as f:
    settings = json.load(f)

# Process bone mapping
bone_mapping = {}
if 'boneMapping' in mapping_data:
    for mapping in mapping_data['boneMapping']:
        if 'avatarBoneId' in mapping and 'costumeBoneId' in mapping:
            bone_mapping[mapping['costumeBoneId']] = mapping['avatarBoneId']

print(f'Loaded {len(bone_mapping)} bone mappings')

# Process costume meshes
for mesh_obj in costume_meshes:
    # If mesh has armature modifier, update it
    for modifier in mesh_obj.modifiers:
        if modifier.type == 'ARMATURE' and costume_armature:
            # Switch to avatar armature
            modifier.object = avatar_armature
    
    # If mesh has vertex groups, update them
    if costume_armature and avatar_armature:
        for vgroup in mesh_obj.vertex_groups:
            if vgroup.name in bone_mapping:
                # Rename vertex group to match avatar bone
                avatar_bone_name = bone_mapping[vgroup.name]
                vgroup.name = avatar_bone_name
    
    # Parent mesh to avatar
    mesh_obj.parent = avatar_armature

# Apply settings
global_scale = settings.get('globalScale', 1.0)
for mesh_obj in costume_meshes:
    mesh_obj.scale = (global_scale, global_scale, global_scale)

# Position costume correctly
for mesh_obj in costume_meshes:
    mesh_obj.location = (0, 0, 0)

# Export result
export_objects = [avatar_armature] + costume_meshes
for obj in export_objects:
    obj.select_set(True)

bpy.ops.export_scene.fbx(
    filepath=result_file,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    bake_anim=False
)

print('Processing completed successfully')
";

            File.WriteAllText(scriptPath, scriptContent, Encoding.UTF8);
        }
        
        /// <summary>
        /// Blenderの実行パスを検索
        /// </summary>
        private static void FindBlenderPath()
        {
            // 設定から取得
            blenderPath = EditorPrefs.GetString("AvatarCostumeAdjustTool_BlenderPath", "");
            
            // 設定にない場合は自動検出を試みる
            if (string.IsNullOrEmpty(blenderPath) || !File.Exists(blenderPath))
            {
                // Windows環境の場合
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // 一般的なインストールパスをチェック
                    string[] possiblePaths = new string[]
                    {
                        @"C:\Program Files\Blender Foundation\Blender 3.6\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.5\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.4\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.3\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.2\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.1\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 3.0\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender 2.93\blender.exe",
                        @"C:\Program Files\Blender Foundation\Blender\blender.exe"
                    };
                    
                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            blenderPath = path;
                            EditorPrefs.SetString("AvatarCostumeAdjustTool_BlenderPath", blenderPath);
                            break;
                        }
                    }
                }
                // macOS環境の場合
                else if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    string[] possiblePaths = new string[]
                    {
                        "/Applications/Blender.app/Contents/MacOS/Blender",
                        $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}/Applications/Blender.app/Contents/MacOS/Blender"
                    };
                    
                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            blenderPath = path;
                            EditorPrefs.SetString("AvatarCostumeAdjustTool_BlenderPath", blenderPath);
                            break;
                        }
                    }
                }
                // Linux環境の場合
                else if (Application.platform == RuntimePlatform.LinuxEditor)
                {
                    string[] possiblePaths = new string[]
                    {
                        "/usr/bin/blender",
                        "/usr/local/bin/blender"
                    };
                    
                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            blenderPath = path;
                            EditorPrefs.SetString("AvatarCostumeAdjustTool_BlenderPath", blenderPath);
                            break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 衣装適用スクリプトのパスを取得
        /// </summary>
        private static string GetCostumeApplyScriptPath()
        {
            return Path.Combine(scriptDir, "apply_costume.py");
        }
        
        /// <summary>
        /// Blenderのパスを設定
        /// </summary>
        public static void SetBlenderPath(string path)
        {
            if (File.Exists(path))
            {
                blenderPath = path;
                EditorPrefs.SetString("AvatarCostumeAdjustTool_BlenderPath", blenderPath);
                UnityEngine.Debug.Log($"Blenderのパスを設定しました: {blenderPath}");
            }
            else
            {
                UnityEngine.Debug.LogError($"指定されたパスにBlenderが見つかりません: {path}");
            }
        }
        
        /// <summary>
        /// Blenderのパスを取得
        /// </summary>
        public static string GetBlenderPath()
        {
            if (string.IsNullOrEmpty(blenderPath))
            {
                FindBlenderPath();
            }
            
            return blenderPath;
        }
        
        /// <summary>
        /// 一時ファイルをクリーンアップ
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    string[] files = Directory.GetFiles(tempDir);
                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"一時ファイルのクリーンアップ中にエラーが発生しました: {ex.Message}");
            }
        }
    }
}