using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SaveSystem 
{
    private const string KEY = "Card";

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
    }

    public static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(KEY)) return null;
        string json = PlayerPrefs.GetString(KEY);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool HasSave() => PlayerPrefs.HasKey(KEY);

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY);
    }
}
