using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary : Dictionary<string, float>, ISerializationCallbackReceiver
{
    [SerializeField] private List<string> keys = new();
    [SerializeField] private List<float> values = new();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var kv in this)
        {
            keys.Add(kv.Key);
            values.Add(kv.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        for (int i = 0; i < keys.Count; i++)
            this[keys[i]] = values[i];
    }
}
