# Script to apply costume to avatar in Blender
# 全アバター衣装自動調整ツール - Blender連携スクリプト
import bpy
import sys
import json
import os
import math
import time
from mathutils import Vector, Matrix, Quaternion

# Script version
VERSION = "1.0.0"
print(f"全アバター衣装自動調整ツール - Blender連携スクリプト v{VERSION}")
print(f"実行開始: {time.strftime('%Y-%m-%d %H:%M:%S')}")

# Get arguments
argv = sys.argv
argv = argv[argv.index('--') + 1:]  # Get all args after --

if len(argv) < 5:
    print('エラー: 引数が不足しています。使用法: blender --background --python script.py -- avatarFile costumeFile resultFile mappingFile settingsFile')
    sys.exit(1)

avatar_file = argv[0]
costume_file = argv[1]
result_file = argv[2]
mapping_file = argv[3]
settings_file = argv[4]

print(f'処理するファイル:')
print(f'アバター: {avatar_file}')
print(f'衣装: {costume_file}')
print(f'出力先: {result_file}')
print(f'マッピング: {mapping_file}')
print(f'設定: {settings_file}')

# Clear scene
print("シーンをクリア中...")
bpy.ops.wm.read_factory_settings(use_empty=True)
for obj in bpy.data.objects:
    bpy.data.objects.remove(obj)

# Import avatar
print("アバターをインポート中...")
try:
    bpy.ops.import_scene.fbx(filepath=avatar_file)
    avatar_objects = bpy.context.selected_objects.copy()
    if not avatar_objects:
        print('エラー: アバターオブジェクトがインポートされませんでした')
        sys.exit(1)
except Exception as e:
    print(f'アバターのインポート中にエラーが発生しました: {str(e)}')
    sys.exit(1)

# Find avatar armature
avatar_armature = None
for obj in avatar_objects:
    if obj.type == 'ARMATURE':
        avatar_armature = obj
        break

if not avatar_armature:
    print('エラー: アバターファイル内にアーマチュアが見つかりません')
    sys.exit(1)

print(f"アバターのアーマチュアを検出: {avatar_armature.name}")

# Import costume
print("衣装をインポート中...")
try:
    bpy.ops.import_scene.fbx(filepath=costume_file)
    costume_objects = [obj for obj in bpy.context.selected_objects if obj not in avatar_objects]
    if not costume_objects:
        print('エラー: 衣装オブジェクトがインポートされませんでした')
        sys.exit(1)
except Exception as e:
    print(f'衣装のインポート中にエラーが発生しました: {str(e)}')
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
    print('エラー: 衣装ファイル内にアーマチュアやメッシュが見つかりません')
    sys.exit(1)

print(f"衣装のアーマチュア: {costume_armature.name if costume_armature else 'なし'}")
print(f"衣装のメッシュ数: {len(costume_meshes)}")

# Load mapping and settings
print("マッピングと設定を読み込み中...")
try:
    with open(mapping_file, 'r', encoding='utf-8') as f:
        mapping_data = json.load(f)

    with open(settings_file, 'r', encoding='utf-8') as f:
        settings = json.load(f)
except Exception as e:
    print(f'ファイル読み込み中にエラーが発生しました: {str(e)}')
    sys.exit(1)

# Process bone mapping
bone_mapping = {}
if 'boneMapping' in mapping_data:
    for mapping in mapping_data['boneMapping']:
        if 'avatarBoneId' in mapping and 'costumeBoneId' in mapping:
            bone_mapping[mapping['costumeBoneId']] = mapping['avatarBoneId']

print(f"ボーンマッピング {len(bone_mapping)} 件をロードしました")

# Extract settings
global_scale = settings.get('globalScale', 1.0)
detect_structural_differences = settings.get('detectStructuralDifferences', True)
adjust_scale = settings.get('adjustScale', True)
adjust_rotation = settings.get('adjustRotation', True)
adjust_bind_poses = settings.get('adjustBindPoses', True)
redistribute_weights = settings.get('redistributeWeights', True)

print(f"設定情報:")
print(f" - グローバルスケール: {global_scale}")
print(f" - 構造差異検出: {detect_structural_differences}")
print(f" - スケール調整: {adjust_scale}")
print(f" - 回転調整: {adjust_rotation}")
print(f" - バインドポーズ調整: {adjust_bind_poses}")
print(f" - ウェイト再分配: {redistribute_weights}")

# If we have costume armature, process it
if costume_armature:
    print("衣装のアーマチュアを処理中...")
    
    # Rename costume bones according to mapping
    for bone in costume_armature.data.bones:
        if bone.name in bone_mapping:
            avatar_bone_name = bone_mapping[bone.name]
            print(f"ボーン名をマッピング: {bone.name} -> {avatar_bone_name}")
            bone.name = avatar_bone_name
    
    # Apply global scale to costume armature
    if adjust_scale and global_scale != 1.0:
        costume_armature.scale = (global_scale, global_scale, global_scale)
        print(f"衣装のアーマチュアにグローバルスケール {global_scale} を適用しました")

# Process costume meshes
print("衣装のメッシュを処理中...")
for mesh_obj in costume_meshes:
    # If mesh has armature modifier, update it
    for modifier in mesh_obj.modifiers:
        if modifier.type == 'ARMATURE' and costume_armature:
            # Switch to avatar armature
            print(f"メッシュ {mesh_obj.name} のアーマチュアを変更: {modifier.object.name} -> {avatar_armature.name}")
            modifier.object = avatar_armature
    
    # If mesh has vertex groups, update them
    if costume_armature and avatar_armature:
        updated_groups = 0
        for vgroup in mesh_obj.vertex_groups:
            if vgroup.name in bone_mapping:
                # Rename vertex group to match avatar bone
                avatar_bone_name = bone_mapping[vgroup.name]
                vgroup.name = avatar_bone_name
                updated_groups += 1
        
        print(f"メッシュ {mesh_obj.name} の頂点グループ {updated_groups} 件を更新しました")
    
    # Parent mesh to avatar armature
    mesh_obj.parent = avatar_armature
    
    # Apply global scale to costume mesh
    if adjust_scale and global_scale != 1.0:
        mesh_obj.scale = (global_scale, global_scale, global_scale)
        print(f"メッシュ {mesh_obj.name} にグローバルスケール {global_scale} を適用しました")

# Position costume correctly
for mesh_obj in costume_meshes:
    mesh_obj.location = (0, 0, 0)

# If we have costume armature, remove it since we're using the avatar's armature
if costume_armature:
    print(f"衣装のアーマチュア {costume_armature.name} を削除します")
    bpy.data.objects.remove(costume_armature)

# Ensure all objects are visible
for obj in bpy.data.objects:
    obj.hide_viewport = False
    obj.hide_render = False

# Select objects for export
export_objects = [avatar_armature] + costume_meshes
for obj in bpy.data.objects:
    obj.select_set(obj in export_objects)

# Ensure avatar armature is the active object
if avatar_armature:
    bpy.context.view_layer.objects.active = avatar_armature

# Export result
print(f"結果をエクスポート中: {result_file}")
try:
    bpy.ops.export_scene.fbx(
        filepath=result_file,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward='-Z',
        axis_up='Y'
    )
    print("エクスポートが完了しました")
except Exception as e:
    print(f'エクスポート中にエラーが発生しました: {str(e)}')
    sys.exit(1)

print(f"処理が正常に完了しました")
print(f"実行終了: {time.strftime('%Y-%m-%d %H:%M:%S')}")
