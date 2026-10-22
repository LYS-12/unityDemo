using System;
using System.IO;
using LitJson;
using UnityEngine;

public enum JsonType { JsonUtility, LitJson }

public sealed class JsonDataStore
{
    public static JsonDataStore Instance { get; } = new JsonDataStore();
    private JsonDataStore() { }

    public void SaveData(object data, string fileName, JsonType type = JsonType.LitJson)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        string path = GetPath(Application.persistentDataPath, fileName);
        string json = type == JsonType.JsonUtility ? JsonUtility.ToJson(data) : JsonMapper.ToJson(data);
        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(path, json);
    }

    public T LoadData<T>(string fileName, JsonType type = JsonType.LitJson) where T : new()
    {
        string savedPath = GetPath(Application.persistentDataPath, fileName);
        string defaultPath = GetPath(Application.streamingAssetsPath, fileName);
        if (TryRead(savedPath, type, out T saved)) return saved;
        if (TryRead(defaultPath, type, out T fallback)) return fallback;
        return new T();
    }

    private static string GetPath(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("/") || fileName.Contains("\\") ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName == "." || fileName == "..")
            throw new ArgumentException("请使用不含目录的文件名。", nameof(fileName));
        return Path.Combine(directory, fileName + ".json");
    }

    private static bool TryRead<T>(string path, JsonType type, out T data)
    {
        data = default;
        if (!File.Exists(path)) return false;
        try
        {
            string json = File.ReadAllText(path);
            data = type == JsonType.JsonUtility ? JsonUtility.FromJson<T>(json) : JsonMapper.ToObject<T>(json);
            return !ReferenceEquals(data, null);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                        exception is ArgumentException || exception is JsonException || exception is InvalidOperationException)
        {
            Debug.LogWarning($"无法读取 JSON：{path}。{exception.Message}");
            return false;
        }
    }
}
