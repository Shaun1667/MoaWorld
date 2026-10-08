using System.Collections.Generic;
using UnityEngine;

namespace MoaWorld
{
    [CreateAssetMenu(fileName = "MoaDatabase", menuName = "MoaWorld/Moa Database")]
    public class MoaDatabase : ScriptableObject
    {
        private static MoaDatabase instance;
        public static MoaDatabase Instance => instance != null ? instance : instance = Resources.Load<MoaDatabase>("MoaDatabase");

        public List<MoaSpecies> species = new List<MoaSpecies>();

        private Dictionary<string, MoaSpecies> lookup;

        public MoaSpecies Get(string speciesId)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, MoaSpecies>();
                foreach (MoaSpecies s in species)
                {
                    lookup[s.speciesId] = s;
                }
            }
            return lookup.TryGetValue(speciesId, out MoaSpecies result) ? result : null;
        }
    }
}
