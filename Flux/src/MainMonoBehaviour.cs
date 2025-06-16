using Flux.Features;
using UnityEngine;

namespace Flux;

public class MainMonoBehaviour : MonoBehaviour
{
    private void LateUpdate()
    {
        if (D2DRenderer.Instance == null)
            return;

        FeatureManager.Update();

        var context = new RenderContext();
        FeatureManager.Render(context);

        D2DRenderer.Instance.QueueCommands(context.Commands);
    }
}