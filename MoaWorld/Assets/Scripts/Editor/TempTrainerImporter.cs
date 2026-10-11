using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using System.IO;
using System.IO.Compression;

public static class TempTrainerImporter
{
    [MenuItem("MoaWorld/Temp Import Trainers")]
    public static void Run()
    {
        Import("TrainerMale", "https://3d.varco.ai/api/objects/3ef66d9d4c71d113910c565e45509b03.zip");
        Import("TrainerFemale", "https://3d.varco.ai/api/objects/43f4f9533009163ee12b2e0a623ac7d3.zip");
        AssetDatabase.Refresh();
    }

    static void Import(string name, string url)
    {
        string dir = "Assets/Varco3D/" + name;
        if (Directory.Exists(dir)) { Debug.LogWarning("TrainerImport skip, exists: " + dir); return; }
        byte[] data;
        using (var req = UnityWebRequest.Get(url))
        {
            var op = req.SendWebRequest();
            while (!op.isDone) System.Threading.Thread.Sleep(50);
            if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("TrainerImport download failed: " + req.error); return; }
            data = req.downloadHandler.data;
        }
        Directory.CreateDirectory(dir);
        using (var ms = new MemoryStream(data))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            foreach (var e in zip.Entries)
            {
                if (string.IsNullOrEmpty(e.Name)) continue;
                string fn = e.Name;
                if (fn.EndsWith(".obj")) fn = name + ".obj";
                using (var s = e.Open())
                using (var fs = File.Create(Path.Combine(dir, fn))) s.CopyTo(fs);
            }
        }
        foreach (var f in Directory.GetFiles(dir)) Debug.Log("TrainerImport wrote " + f);
    }
}
