using UnityEditor;
using UnityEngine;

namespace BreathCasino.EditorTools
{
    /// <summary>
    /// Архивный entry-point старого blockout-пайплайна.
    /// Старый генератор сцены больше не участвует в рабочем процессе проекта.
    /// Актуальная рабочая сцена создаётся через MainSceneBuilder.
    /// </summary>
    public static class QuickSetupMenu
    {
        [MenuItem("Breath Casino/Archive/Blockout/Create Legacy Test Scene")]
        public static void CreateLegacyTestScene()
        {
            Debug.Log("Legacy blockout scene generator is archived. Use 'Breath Casino/Main Scene/Rebuild mainScene' instead.");
        }
    }
}
