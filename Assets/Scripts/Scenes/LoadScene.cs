using UnityEngine.SceneManagement;
using UnityEngine;

namespace Zeke.Scenes
{
    public class LoadScene : MonoBehaviour
    {
        [SerializeField] private SceneReference sceneReference;
        [SerializeField] private LoadSceneMode loadSceneMode;

        public void Load()
        {
            sceneReference.Load(loadSceneMode);
        }
    }
}