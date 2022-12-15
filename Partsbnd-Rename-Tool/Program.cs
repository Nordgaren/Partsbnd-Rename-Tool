using SoulsFormats;
using System.Text.RegularExpressions;

namespace Partsbnd_Rename_Tool;

public class Program {
    private static Regex _fileNameRegex = new Regex("(?<slot>[a-zA-Z]+)_(?<gender>[a-zA-Z])_(?<id>[0-9]+)(.*)\\.(?<ext>[a-zA-Z]+)", RegexOptions.IgnoreCase);
    private static Regex _texNameRegex = new Regex("(?<slot>[a-zA-Z]+)_(?<gender>[a-zA-Z])_(?<id>[0-9]+)*", RegexOptions.IgnoreCase);
    static void Main(string[] args) {

        string bndPath = string.Empty;
        try {
            bndPath = args[0];
            if (!File.Exists(bndPath))
                ShowUsage("File does not exist. Please drag and drop a partsbnd file on this exe, to rename the parts files inside.");
        }
        catch (Exception e) {
            ShowUsage("No file given as argument. Please drag and drop a partsbnd file on this exe, to rename the parts files inside.");
        }


        if (BND4.IsRead(bndPath, out BND4 bnd4)) {
            if (!File.Exists($"{bndPath}.bak")) File.Copy(bndPath, $"{bndPath}.bak");

            string filename = Path.GetFileNameWithoutExtension(bndPath);

            Console.WriteLine($"Attempting to patch BND4 {filename}");
            PatchBND(filename, bnd4);
            bnd4.Write(bndPath);
            ExitSuccessfully("Patching Complete!");
        }
        if (BND3.IsRead(bndPath, out BND3 bnd3)) {
            if (!File.Exists($"{bndPath}.bak")) File.Copy(bndPath, $"{bndPath}.bak");

            string filename = Path.GetFileNameWithoutExtension(bndPath);
            Console.WriteLine($"Attempting to patch BND3 {filename}");
            PatchBND(filename, bnd4);
            bnd3.Write(bndPath);
            ExitSuccessfully("Patching Complete!");
        }

        ShowUsage("File does not exist. Please drag and drop a partsbnd file on this exe, to rename the parts files inside.");
    }

    private static void PatchBND(string filename, IBinder bnd) {

        Match match = _fileNameRegex.Match(filename);
        string slot = match.Groups["slot"].Value;
        string gender = match.Groups["gender"].Value;
        string newId = match.Groups["id"].Value;
        string newName = $"{slot}_{gender}_{newId}";
        Dictionary<string, string> textureReplacements = new();

        foreach (BinderFile file in bnd.Files) {
            Match originalFileMatch = _fileNameRegex.Match(file.Name);
            if (!originalFileMatch.Success) throw new Exception("No match found in original filename. Cannot patch BND");

            string originalId = originalFileMatch.Groups["id"].Value;
            file.Name = file.Name.Replace(originalId, newId);

            if (TPF.IsRead(file.Bytes, out TPF tpf)) {
                int count = 0;
                foreach (TPF.Texture tex in tpf.Textures) {
                    Match textureMatch = _texNameRegex.Match(tex.Name);
                    string originalTexName = Path.GetFileName(tex.Name);
                    if (textureMatch.Success) {
                        tex.Name = tex.Name.Replace(textureMatch.Groups["id"].Value, newId);
                    }
                    else {
                        tex.Name = $"{newName}_{count++}.dds";
                    }
                    textureReplacements[originalTexName] = Path.GetFileName(tex.Name);
                }

                file.Bytes = tpf.Write();
            }
        }


        foreach (BinderFile file in bnd.Files) {
            if (!FLVER2.IsRead(file.Bytes, out FLVER2 flver)) continue;
            foreach (FLVER2.Material material in flver.Materials) {
                Match originalMatMatch = _texNameRegex.Match(material.MTD);
                if (originalMatMatch.Success) {
                    string originalId = originalMatMatch.Groups["id"].Value;
                    material.MTD = material.MTD.Replace(originalId, newId);
                }
                foreach (FLVER2.Texture tex in material.Textures) {
                    if (string.IsNullOrWhiteSpace(tex.Path)) continue;

                    string texName = Path.GetFileName(tex.Path);
                    if (!textureReplacements.ContainsKey(texName)) continue;

                    tex.Path = tex.Path.Replace(texName, textureReplacements[texName]);
                }
            }
        }
    }
    private static void ShowUsage(string message) {
        Console.WriteLine(message);
        Console.WriteLine("Drag and Drop a partsbnd file onto this exe to patch the new id number into the bnd");
        Console.ReadKey();
        Environment.Exit(1);
    }
    private static void ShowError(object message) {
        Console.WriteLine(message);
        Console.ReadKey();
        Environment.Exit(1);
    }
    private static void ExitSuccessfully(string message) {
        Console.WriteLine(message);
        Console.ReadKey();
        Environment.Exit(1);
    }
}
