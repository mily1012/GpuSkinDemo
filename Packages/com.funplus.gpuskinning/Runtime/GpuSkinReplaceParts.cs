using UnityEngine;

[ExecuteInEditMode]
public class GpuSkinReplaceParts : MonoBehaviour
{
    private GPUSkinAnimator skinAnimator;
    public GameObject partsGO;

    void Start()
    {
        skinAnimator = this.gameObject.GetComponent<GPUSkinAnimator>();
        if (!skinAnimator)
        {
            Debug.LogError($"该物体 {gameObject.name} 没有挂载GPUSkinAnimator脚本");
            return;
        }
        if (partsGO != null)
        {
            skinAnimator.ReplaceParts(partsGO, true);
        }
    }
}
