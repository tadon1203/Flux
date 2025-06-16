using Flux.Graphics;
using UnityEngine;
using Vortice;
using Vortice.Mathematics;
using Vector2 = System.Numerics.Vector2;

namespace Flux;

public class MainMonoBehaviour : MonoBehaviour
{
    private void Update()
    {
        D2DRenderer.DrawText("Hello from D2D!", "Arial", 40f, new Vector2(10, 10), new Color4(255, 255, 255));
        D2DRenderer.FillRectangle(new RawRectF(100, 100, 300, 200), new Color4(255, 0, 0));
        D2DRenderer.DrawRectangle(new RawRectF(100, 100, 300, 200), new Color4(0, 0, 255));
    }
}