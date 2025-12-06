using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Events;


public class EsinManayer : MonoBehaviour
{
[SerializeField] Animator FadePanelAnimator;
public UnityEvent whenSceneChanged;

public void LoadEnv(int sceneIdx)
{
//whenSceneChanged?.Invoke();
StartCoroutine(Mondongo(sceneIdx));
}

IEnumerator Mondongo(int sceneIdx)
{
AsyncOperation syn = SceneManager.LoadSceneAsync(sceneIdx);
FadePanelAnimator.SetTrigger("fade");
while (!syn.isDone)
	yield return null;
}

}
