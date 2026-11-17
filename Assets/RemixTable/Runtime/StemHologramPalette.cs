using UnityEngine;
namespace RemixTable
{
    // Public targets are sRGB. Decode once at initialization and upload Linear vectors.
    public readonly struct StemHologramPalette
    {
        public readonly Vector4 Primary,Shadow,Highlight,Edge,Lowpass;
        static Vector4 Hex(int rgb) => new Vector4(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
        StemHologramPalette(int primary,int shadow,int highlight,int edge,int lowpass)
        {Primary=Hex(primary);Shadow=Hex(shadow);Highlight=Hex(highlight);Edge=Hex(edge);Lowpass=Hex(lowpass);}
        public static StemHologramPalette For(StemId stem)
        {
            switch(stem)
            {
                case StemId.Drums:return new StemHologramPalette(0xFFB26B,0xA83C08,0xFFE3AB,0xFFECCC,0xDC651C);
                case StemId.Bass:return new StemHologramPalette(0x54DBAA,0x08764C,0xB8FFE3,0xC9FFEB,0x138E57);
                case StemId.Melody:return new StemHologramPalette(0x5DD6EF,0x087B96,0xCCF7FF,0xDCFAFF,0x1689AD);
                default:return new StemHologramPalette(0xB49AFF,0x6441C8,0xE8D6FF,0xF0DFFF,0x7D33F0);
            }
        }
        static Vector4 Decode(Vector4 value) => (Vector4)new Color(value.x,value.y,value.z,1).linear;
        public void Apply(MaterialPropertyBlock block)
        {
            block.SetVector("_PrimaryLinear",Decode(Primary));block.SetVector("_DeepLinear",Decode(Shadow));
            block.SetVector("_HighlightLinear",Decode(Highlight));block.SetVector("_EdgeLinear",Decode(Edge));block.SetVector("_LowpassLinear",Decode(Lowpass));
        }
    }
}
