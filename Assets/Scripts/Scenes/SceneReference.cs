using UnityEngine.SceneManagement;
using UnityEngine;

namespace Zeke.Scenes
{
    [CreateAssetMenu(fileName = "Scene", menuName = "Scene Reference", order = 1)]
    public class SceneReference : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }

        public void Load(LoadSceneMode loadSceneMode)
        {
            SceneManager.LoadScene(Name, loadSceneMode);
        }
    }
}