using System;
using System.IO;
using System.Text;
using UnityEngine;

// The path is supplied by the caller so editor checks can use an isolated
// temporary directory, never the player's real save.json.
public static class SaveCheckpointStorage
{
    public static SaveData Read(string path)
    {
        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        if (data == null) throw new InvalidDataException("The save contains no game state.");
        data.ValidateAndMigrate();
        return data;
    }

    public static void Write(string path, SaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.ValidateAndMigrate();

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Never truncate the live save. A failed write leaves the old file
        // intact, and replacement retains that old checkpoint as save.json.bak.
        string temporaryPath = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
        using (var stream = new FileStream(temporaryPath, FileMode.Create,
                   FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
        else File.Move(temporaryPath, path);
    }
}
