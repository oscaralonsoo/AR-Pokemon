using System;
using UnityEngine;

public enum PokemonType
{
    Colorless, Fire, Water, Grass, Lightning, Psychic, Fighting, Dark, Metal, Dragon, Earth, Normal
}

[CreateAssetMenu(menuName = "Pokemon/Type Button Library")]
public class TypeButtonLibrary : ScriptableObject
{
    [Serializable]
    public class Entry              
    {
        public PokemonType type;
        public Sprite normal;
        public Sprite highlighted;  
        public Sprite pressed;    
        public Sprite disabled;   
    }

    [SerializeField] private Entry[] entries;
    [SerializeField] private Entry fallback;

    public Entry Get(PokemonType type)
    {
        if (entries != null)
        {
            foreach (var e in entries)
                if (e != null && e.type == type) return e;
        }
        return fallback;
    }

    public static PokemonType Parse(string typeName)
    {
        return Enum.TryParse(typeName, true, out PokemonType t) ? t : PokemonType.Colorless;
    }
}