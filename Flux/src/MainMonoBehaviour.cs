using System.Collections.Generic;
using Flux.Features;
using Flux.Graphics;
using Flux.Graphics.Commands;
using UnityEngine;

namespace Flux;

public class MainMonoBehaviour : MonoBehaviour
{
    private void LateUpdate()
    {
        if (D2DRenderer.Instance == null)
            return;

        FeatureManager.Update();

        var commands = new List<IRenderCommand>();
        FeatureManager.Render(commands);

        D2DRenderer.Instance.QueueCommands(commands);
    }
}