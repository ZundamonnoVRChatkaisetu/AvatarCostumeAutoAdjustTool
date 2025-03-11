using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// JSON形式のデータ操作に関するユーティリティクラス
    /// </summary>
    public static class JsonUtils
    {
        /// <summary>
        /// オブジェクトをJSONに変換してファイルに保存
        /// </summary>
        public static void SaveToJson<T>(string filePath, T data)
        {
            string jsonData = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, jsonData, Encoding.UTF8);
        }
        
        /// <summary>
        /// JSONファイルからオブジェクトを読み込み
        /// </summary>
        public static T LoadFromJson<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("指定されたJSONファイルが見つかりません。", filePath);
            }
            
            string jsonData = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonUtility.FromJson<T>(jsonData);
        }
        
        /// <summary>
        /// リソースからJSONファイルを読み込み
        /// </summary>
        public static T LoadFromResources<T>(string resourcePath)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            
            if (textAsset == null)
            {
                throw new FileNotFoundException("指定されたリソースが見つかりません。", resourcePath);
            }
            
            return JsonUtility.FromJson<T>(textAsset.text);
        }
        
        /// <summary>
        /// オブジェクトをディープコピー
        /// </summary>
        public static T DeepCopy<T>(T obj)
        {
            string json = JsonUtility.ToJson(obj);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
