using UnityEngine;
using System.Linq;

namespace ZulfarakRPG
{
    // Singleton ScriptableObject that holds all class/subclass references.
    [CreateAssetMenu(fileName = "ClassDatabase", menuName = "ZulfarakRPG/Class Database")]
    public class ClassDatabase : ScriptableObject
    {
        private static ClassDatabase _instance;
        public static ClassDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ClassDatabase>("ClassDatabase");
                return _instance;
            }
        }

        public ClassData[] classes;
        public SubclassData[] subclasses;

        public ClassData GetClass(ClassType type) =>
            classes.FirstOrDefault(c => c.classType == type);

        public SubclassData GetSubclass(SubclassType type) =>
            subclasses.FirstOrDefault(s => s.subclassType == type);

        public SubclassData[] GetSubclassesForClass(ClassType type) =>
            subclasses.Where(s => s.parentClass == type).ToArray();
    }
}
