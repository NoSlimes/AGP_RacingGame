using System.IO;
using UnityEngine;

namespace RacingGame
{
    public static class NameUtility
    {
        private static string dataPath = string.Empty;
        private static TextAsset[] textAssets;
        private static string[] nameFilePaths;

        public static void Init()
        {
            dataPath = $"{Application.persistentDataPath}/names";
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            textAssets = Resources.LoadAll<TextAsset>("Names");

            foreach (TextAsset textAsset in textAssets)
            {
                if (!CheckIfFileExists(textAsset.name))
                {
                    CreateFileFromAsset(textAsset);
                }
            }

            nameFilePaths = Directory.GetFiles(dataPath);
        }

        private static bool CheckIfFileExists(string fileName)
        {
            string dataPath = $"{Application.persistentDataPath}/names";
            string filePath = Path.Combine(dataPath, fileName + ".json");

            return File.Exists(filePath);
        }

        private static void CreateFileFromAsset(TextAsset textAsset)
        {
            try
            {
                string filePath = Path.Combine(dataPath, textAsset.name + ".json");

                using (FileStream fs = File.Open(filePath, FileMode.Create))
                {

                    using (StreamWriter writer = new(fs))
                    {
                        writer.Write(textAsset.text);
                    }

                }
            }
            catch (IOException ex)
            {
                Debug.LogError($"Error creating/writing file: {ex.Message}");
            }
        }


        public static string GetRandomNameFromJson()
        {
            string Json = File.ReadAllText(nameFilePaths[Random.Range(0, nameFilePaths.Length)]);

            NameArray nameArray = JsonUtility.FromJson<NameArray>(Json);

            string name = nameArray.names[Random.Range(0, nameArray.names.Length)];

            return name;
        }

        [System.Serializable]
        public class NameArray
        {
            public string[] names;

            public NameArray(string[] names)
            {
                this.names = names;
            }
        }

        public struct NameOptions
        {

        }

    }

}
