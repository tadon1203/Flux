using Flux.Features;
using Flux.Graphics;
using UnityEngine;
using Vortice.Mathematics;
using Vector2 = System.Numerics.Vector2;

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
        
        context.DrawText("hello d2d", Vector2.Zero, new Color4(Color3.AntiqueWhite, 1f), 40f);

        D2DRenderer.Instance.QueueCommands(context.Commands);
    }
}