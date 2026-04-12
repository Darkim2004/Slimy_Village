using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneEntitySpawnCatalog : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        public EntityDefinition definition;
        public GameObject prefab;
        public Transform parent;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;
}
