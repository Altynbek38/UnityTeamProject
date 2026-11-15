#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BushPilot.Editor
{
    public static class Milestone1SetupWizard
    {
        [MenuItem("Bush Pilot/Apply Cessna Livery & Save Prefab (Safe)", false, 10)]
        public static void ApplyLiveryAndSaveSafe()
        {
            const string matPath = "Assets/_Project/Art/Models/Materials/Body_MAT.mat";
            const string extMatPath = "Assets/_Project/Art/Materials/M_Cessna_Exterior.mat";
            const string liveryTexPath = "Assets/_Project/Art/Models/textures/Cessna_BushLivery_BaseColor.png";

            Texture2D liveryTex = AssetDatabase.LoadAssetAtPath<Texture2D>(liveryTexPath);
            if (liveryTex != null)
            {
                AssignBaseMap(AssetDatabase.LoadAssetAtPath<Material>(matPath), liveryTex);
                AssignBaseMap(AssetDatabase.LoadAssetAtPath<Material>(extMatPath), liveryTex);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Bush Pilot] Cessna Bush Livery safely applied! No scene objects or transforms were overwritten.");
        }

        private static void AssignBaseMap(Material mat, Texture2D tex)
        {
            if (mat == null || tex == null) return;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
        }
    }
}
#endif
