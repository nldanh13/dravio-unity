using UnityEditor;
using UnityEngine;

namespace Dravio.Character.Editor
{
    /// <summary>
    /// Command-line entry for scripts/art/character/run_u5_local.ps1 (U1-CHAR.5):
    ///   Unity.exe -batchmode -projectPath . -executeMethod Dravio.Character.Editor.CharacterBatchCli.RunImport -logFile import.log
    /// Runs Dravio/Character/Import Batch Manifest on Assets/Dravio/Character/Imported/import-manifest.json and exits
    /// with code 1 when an entry failed. Same rules as the menu: approved assets and existing prefabs/controllers are
    /// never overwritten. Does not open or modify any scene.
    /// </summary>
    public static class CharacterBatchCli
    {
        public static void RunImport()
        {
            string report = LivingAssetBatchImporter.Run(ImportManifest.DefaultPath);
            Debug.Log(report);
            bool failed = !report.Contains(" 0 failed") || report.StartsWith("Dravio batch import: ");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
