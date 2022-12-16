using SoulsFormats;
using System.Text.RegularExpressions;
namespace Partsbnd_Rename_Tool;

public class PartsInfo {
    public string NewName => $"{NewSlot}_{NewGender}_{NewID}";
    public string NewSlot { get; init; }
    public string NewGender { get; init;  }
    public string NewID { get; init; }
}

public class Program {
    private static Regex _fileNameRegex = new Regex("(?<slot>[a-zA-Z]+)_(?<gender>[a-zA-Z])_(?<id>[0-9]+)(.*)\\.(?<ext>[a-zA-Z]+)", RegexOptions.IgnoreCase);
    private static Regex _texNameRegex = new Regex("(?<slot>[a-zA-Z]+)_(?<gender>[a-zA-Z])_(?<id>[0-9]+)*", RegexOptions.IgnoreCase);
    static void Main(string[] args) {

        try {
            BeginPatch(args);
        }
        catch (Exception e) {
            ShowError(e);
            throw;
        }

        ShowUsage("File is not a partsbnd.");
    }
    private static void BeginPatch(string[] args) {

        string bndPath = args[0];
        if (!File.Exists(bndPath))
            ShowUsage("File does not exist.");
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
    }

    private static void PatchBND(string filename, IBinder bnd) {

        Match match = _fileNameRegex.Match(filename);
        string slot = match.Groups["slot"].Value;
        string gender = match.Groups["gender"].Value;
        string newId = match.Groups["id"].Value;

        PartsInfo partsInfo = new() {
            NewSlot = slot,
            NewGender = gender,
            NewID = newId
        };

        Dictionary<string, string> textureReplacements = PatchTPF(bnd, partsInfo);
        PatchBNDFLVER(bnd, textureReplacements);
    }
    private static void PatchBNDFLVER(IBinder bnd, Dictionary<string, string> textureReplacements) {

        foreach (BinderFile file in bnd.Files) {
            if (!FLVER2.IsRead(file.Bytes, out FLVER2 flver)) continue;
            foreach (FLVER2.Material material in flver.Materials) {
                // I don't think this is necessary
                // Match originalMatMatch = _texNameRegex.Match(material.MTD);
                // if (originalMatMatch.Success) {
                //     string originalId = originalMatMatch.Groups["id"].Value;
                //     material.MTD = material.MTD.Replace(originalId, newId);
                // }
                foreach (FLVER2.Texture tex in material.Textures) {
                    if (string.IsNullOrWhiteSpace(tex.Path)) continue;

                    string texName = Path.GetFileNameWithoutExtension(tex.Path);
                    if (!textureReplacements.ContainsKey(texName)) continue;

                    tex.Path = tex.Path.Replace(texName, textureReplacements[texName]);
                }
            }
            file.Bytes = flver.Write();
        }
    }
    private static Dictionary<string, string> PatchTPF(IBinder bnd, PartsInfo partsInfo) {

        Dictionary<string, string> textureReplacements = new();
        foreach (BinderFile file in bnd.Files) {
            Match originalFileMatch = _fileNameRegex.Match(file.Name);
            if (!originalFileMatch.Success) throw new InvalidPartsFileNameException($"No match found in original filename for:\n{file.Name}.\nCannot patch BND");

            string originalId = originalFileMatch.Groups["id"].Value;
            string originalSlot = originalFileMatch.Groups["slot"].Value;
            string originalGender = originalFileMatch.Groups["gender"].Value;
            file.Name = file.Name.Replace(originalId, partsInfo.NewID)
                .Replace($"{originalSlot}_{originalGender}_", $"{partsInfo.NewSlot}_{partsInfo.NewGender}_");

            if (TPF.IsRead(file.Bytes, out TPF tpf)) {
                int count = 0;
                foreach (TPF.Texture tex in tpf.Textures) {
                    string originalTexName = Path.GetFileNameWithoutExtension(tex.Name);
                    Match textureMatch = _texNameRegex.Match(tex.Name);
                    if (textureMatch.Success) {
                        string oldSlot = textureMatch.Groups["slot"].Value;
                        string oldGender = textureMatch.Groups["gender"].Value;
                        tex.Name = tex.Name.Replace(textureMatch.Groups["id"].Value, partsInfo.NewID)
                            .Replace($"{oldSlot}_{oldGender}_", $"{partsInfo.NewSlot}_{partsInfo.NewGender}_");
                    }
                    else {
                        tex.Name = $"{partsInfo.NewName}_{count++}";
                    }
                    textureReplacements[originalTexName] = Path.GetFileNameWithoutExtension(tex.Name);
                }
                file.Bytes = tpf.Write();
            }
        }
        return textureReplacements;
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
    }
    private static void ExitSuccessfully(string message) {
        Console.WriteLine(message);
        Environment.Exit(1);
    }
}

public class InvalidPartsFileNameException : Exception {
    public InvalidPartsFileNameException(string message) : base(message) { }
}
