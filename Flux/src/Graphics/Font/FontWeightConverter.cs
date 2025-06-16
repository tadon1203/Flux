namespace Flux.Graphics.Font;

public static class FontWeightConverter
{
    public static Vortice.DirectWrite.FontWeight ToDirectWrite(FontWeight weight)
    {
        return weight switch
        {
            FontWeight.Bold => Vortice.DirectWrite.FontWeight.Bold,
            FontWeight.Regular => Vortice.DirectWrite.FontWeight.Regular,
            _ => Vortice.DirectWrite.FontWeight.Normal
        };
    }
}