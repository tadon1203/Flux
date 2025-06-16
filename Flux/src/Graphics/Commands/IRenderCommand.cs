namespace Flux.Graphics.Commands;

public interface IRenderCommand
{
    void Execute(D2DRenderer renderer);
}