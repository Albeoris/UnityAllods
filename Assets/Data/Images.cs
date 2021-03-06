﻿using System;
using UnityEngine;
using System.IO;

public class Images
{
    public enum ImageType
    {
        AllodsBMP,
        Unity
    }

    public static Texture2D LoadImage(string filename, ImageType type)
    {
        return LoadImage(filename, type, 0, false);
    }

    public static Texture2D LoadImage(string filename, uint colormask, ImageType type)
    {
        return LoadImage(filename, type, colormask, true);
    }

    private static Texture2D LoadImage(string filename, ImageType type, uint colormask, bool has_colormask)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        Texture2D texture;
        if (type == ImageType.Unity)
        {
            texture = new Texture2D(0, 0);
            texture.filterMode = FilterMode.Point;
            byte[] imageData = new byte[ms.Length];
            ms.Read(imageData, 0, (int)ms.Length);
            texture.LoadImage(imageData);
            ms.Close();

            if (has_colormask)
            {
                //colormask &= 0xF0F0F0;
                Color32[] colors = texture.GetPixels32();
                for (int i = 0; i < colors.Length; i++)
                {
                    uint pixel = ((uint)colors[i].r << 16) | ((uint)colors[i].g << 8) | colors[i].b | ((uint)colors[i].a << 24);
                    if (/*(pixel & 0xF0F0F0) == colormask*/ (pixel & 0xFFFFFF) == colormask)
                        colors[i].a = 0;
                }
                texture.SetPixels32(colors);
                texture.Apply(false);
            }
        }
        else
        {
            // load bmp manually.
            BinaryReader br = new BinaryReader(ms);
            try
            {
                ushort bfh_signature = br.ReadUInt16();
                if (bfh_signature != 0x4D42) // BM
                {
                    Core.Abort("\"{0}\" is not a valid BMP image", filename);
                    return null;
                }

                ms.Position = 0x0A;
                uint bfh_pixeldata = br.ReadUInt32();

                //Debug.Log(String.Format("pixel data at {0}", bfh_pixeldata));
                uint bi_version = br.ReadUInt32();
                if (bi_version == 12)
                {
                    Core.Abort("\"{0}\": CORE BMP images not supported", filename);
                    return null;
                }

                if (bi_version != 40)
                {
                    Core.Abort("\"{0}\": version {1} is not supported", filename, bi_version);
                    return null;
                }

                int bi_width = br.ReadInt32();
                int bi_height = br.ReadInt32();
                br.BaseStream.Position += 2; // short bi_planes = br.ReadInt16();
                short bi_bitcount = br.ReadInt16();
                br.BaseStream.Position += 4; // uint bi_compression = br.ReadUInt32();
                br.BaseStream.Position += 4; // uint bi_sizeimage = br.ReadUInt32();
                br.BaseStream.Position += 4; // int bi_xpelspermeter = br.ReadInt32();
                br.BaseStream.Position += 4; // int bi_ypelspermeter = br.ReadInt32();
                br.BaseStream.Position += 4; // uint bi_clrused = br.ReadUInt32();
                br.BaseStream.Position += 4; // uint bi_clrimportant = br.ReadUInt32();

                //Debug.Log(String.Format("pixel data at {0}, {1}x{2}, bits: {3}", bfh_pixeldata, bi_width, bi_height, bi_bitcount));
                texture = new Texture2D(bi_width, bi_height, TextureFormat.ARGB32, false);
                texture.filterMode = FilterMode.Point;
                Color32[] colors = new Color32[bi_width * bi_height];
                if (bi_bitcount == 24) // read RGB
                {
                    int i = 0;
                    for (int y = bi_height - 1; y >= 0; y--)
                    {
                        ms.Position = bfh_pixeldata + bi_width * y * 3;
                        for (int x = 0; x < bi_width; x++)
                        {
                            byte b = br.ReadByte();
                            byte g = br.ReadByte();
                            byte r = br.ReadByte();
                            colors[i++] = new Color32(r, g, b, 255);
                        }
                    }
                }
                else if (bi_bitcount == 32) // read RGBA
                {
                    int i = 0;
                    for (int y = bi_height - 1; y >= 0; y--)
                    {
                        ms.Position = bfh_pixeldata + bi_width * y * 4;
                        for (int x = 0; x < bi_width; x++)
                        {
                            byte b = br.ReadByte();
                            byte g = br.ReadByte();
                            byte r = br.ReadByte();
                            br.BaseStream.Position += 1; // byte a = br.ReadByte(); 
                            colors[i++] = new Color32(r, g, b, 255);
                        }
                    }
                }
                else if (bi_bitcount == 8)
                {
                    // also read palette
                    // current offset (after header) = 0x36
                    Color32[] colormap = new Color32[256];
                    for (int i = 0; i < 256; i++)
                    {
                        byte b = br.ReadByte();
                        byte g = br.ReadByte();
                        byte r = br.ReadByte();
                        br.BaseStream.Position += 1; // byte a = br.ReadByte();
                        colormap[i] = new Color32(r, g, b, 255); // ignore alpha value here, it doesn't make sense
                    }

                    int j = 0;
                    for (int y = bi_height - 1; y >= 0; y--)
                    {
                        ms.Position = bfh_pixeldata + bi_width * y;
                        for (int x = 0; x < bi_width; x++)
                            colors[j++] = colormap[br.ReadByte()];
                    }
                }
                else
                {
                    Core.Abort("\"{0}\": bad bit count: {1}", filename, bi_bitcount);
                    return null;
                }
                texture.SetPixels32(colors);

                if (has_colormask)
                {
                    //colormask &= 0xF0F0F0;
                    for (int i = 0; i < colors.Length; i++)
                    {
                        uint pixel = ((uint)colors[i].r << 16) | ((uint)colors[i].g << 8) | colors[i].b | ((uint)colors[i].a << 24);
                        if (/*(pixel & 0xF0F0F0) == colormask*/ (pixel & 0xFFFFFF) == colormask)
                            colors[i].a = 0;
                    }
                    texture.SetPixels32(colors);
                }

                texture.Apply(false);
            }
            finally
            {
                br.Close();
            }
        }

        return texture;
    }

    // load palette
    // this is also used in .256/.16a to retrieve own palette
    private static Texture2D LoadPaletteFromStream(BinaryReader br)
    {
        Color32[] colors = new Color32[256];

        for (int i = 0; i < 256; i++)
        {
            byte b = br.ReadByte();
            byte g = br.ReadByte();
            byte r = br.ReadByte();
            br.BaseStream.Position += 1; // byte a = br.ReadByte();
            colors[i] = new Color32(r, g, b, 255);
        }

        Texture2D texture = new Texture2D(256, 1, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(colors);
        texture.Apply(false);
        return texture;
    }

    // public interface
    public static Texture2D LoadPalette(string filename, uint offset = 0x36)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        BinaryReader br = new BinaryReader(ms);
        ms.Seek(offset, SeekOrigin.Begin);
        Texture2D texture = LoadPaletteFromStream(br);
        br.Close();
        return texture;
    }

    private static void SpriteAddIXIY(ref int ix, ref int iy, uint w, uint add)
    {
        int x = ix;
        int y = iy;
        for (int i = 0; i < add; i++)
        {
            x++;
            if (x >= w)
            {
                y++;
                x = x - (int)w;
            }
        }

        ix = x;
        iy = y;
    }

    public class AllodsSprite
    {
        public Texture2D OwnPalette;
        public Texture2D Atlas;
        public Rect[] AtlasRects;
        public Sprite[] Sprites;
    }

    public class AllodsSpriteSeparate
    {
        public Texture2D OwnPalette;
        public Frame[] Frames;

        public class Frame
        {
            private readonly AllodsSpriteSeparate _Sprite;
            public Frame(AllodsSpriteSeparate sprite) { _Sprite = sprite; }

            public int Width { get; internal set; }
            public int Height { get; internal set; }
            internal Color[] _Colors;
            internal Texture2D _Texture;

            private int GetNearestPOT(int v)
            {
                if (v <= 1) return v;
                int vv = 1;
                for (int i = 0; i < 128; i++)
                {
                    vv *= 2;
                    if (vv >= v)
                        return vv;
                }

                return 0;
            }

            // retrieves texture resized to nearest POT size.
            public Texture2D Texture
            {
                get
                {
                    if (_Texture == null)
                    {
                        // this is inefficient, but this is the ONLY way it works.
                        Texture2D tmpTex = new Texture2D(Width, Height, TextureFormat.RGHalf, false);
                        tmpTex.SetPixels(0, 0, Width, Height, _Colors);
                        _Texture = new Texture2D(0, 0, TextureFormat.RGHalf, false);
                        _Texture.PackTextures(new Texture2D[] { tmpTex }, 0);
                        GameObject.Destroy(tmpTex);
                        _Texture.filterMode = FilterMode.Point;
                        _Colors = null;
                    }

                    return _Texture;
                }
            }

            // retrieves raw texture without resize.
            public Texture2D TextureRaw
            {
                get
                {
                    if (_Texture == null)
                    {
                        // this is inefficient, but this is the ONLY way it works.
                        _Texture = new Texture2D(Width, Height, TextureFormat.RGHalf, false);
                        _Texture.SetPixels(0, 0, Width, Height, _Colors);
                        _Texture.filterMode = FilterMode.Point;
                        _Texture.Apply();
                        _Colors = null;
                    }

                    return _Texture;
                }
            }

            // retrieves truecolor representation of the sprite.
            public Texture2D TextureRawTruecolor
            {
                get
                {
                    if (_Sprite.OwnPalette == null)
                        return null;

                    if (_Texture == null)
                    {
                        // this is inefficient, but this is the ONLY way it works.
                        _Texture = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
                        Color[] paletteColors = _Sprite.OwnPalette.GetPixels();
                        for (int i = 0; i < _Colors.Length; i++)
                        {
                            int paletteIndex = (int)(_Colors[i].r * 255);
                            _Colors[i] = new Color(paletteColors[paletteIndex].r,
                                                   paletteColors[paletteIndex].g,
                                                   paletteColors[paletteIndex].b,
                                                   _Colors[i].g);
                        }
                        _Texture.SetPixels(0, 0, Width, Height, _Colors);
                        _Texture.filterMode = FilterMode.Point;
                        _Texture.Apply();
                        _Colors = null;
                    }

                    return _Texture;
                }
            }
        }
    }

    public static AllodsSpriteSeparate Load256Separate(string filename, bool hasOwnPalette = true)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        BinaryReader br = new BinaryReader(ms);

        ms.Position = ms.Length - 4;
        int count = br.ReadInt32() & 0x7FFFFFFF;

        ms.Position = 0;

        AllodsSpriteSeparate sprite = new AllodsSpriteSeparate();
        // read palette
        if (hasOwnPalette)
            sprite.OwnPalette = LoadPaletteFromStream(br);
        else sprite.OwnPalette = null;
        AllodsSpriteSeparate.Frame[] frames = new AllodsSpriteSeparate.Frame[count];

        int oldCount = count;
        for (int i = 0; i < count; i++)
        {
            uint w = br.ReadUInt32();
            uint h = br.ReadUInt32();
            uint ds = br.ReadUInt32();
            long cpos = ms.Position;

            if (w > 512 || h > 512 || ds > 1000000)
            {
                //Core.Abort("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                Debug.LogFormat("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                i--;
                count--;
                continue;
            }

            Color[] colors = new Color[w * h];
            for (int j = 0; j < colors.Length; j++)
                colors[j].g = 0;

            int ix = 0;
            int iy = 0;
            int ids = (int)ds;
            while (ids > 0)
            {
                ushort ipx = br.ReadByte();
                ipx |= (ushort)(ipx << 8);
                ipx &= 0xC03F;
                ids--;

                if ((ipx & 0xC000) > 0)
                {
                    if ((ipx & 0xC000) == 0x4000)
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx * w);
                    }
                    else
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx);
                    }
                }
                else
                {
                    ipx &= 0x3F;
                    for (int j = 0; j < ipx; j++)
                    {
                        byte ss = br.ReadByte();
                        //uint px = (ss << 16) | (ss << 8) | (ss) | 0xFF000000;
                        colors[iy * w + ix] = new Color((float)ss / 255f, 1, 0, 0);
                        SpriteAddIXIY(ref ix, ref iy, w, 1);
                    }

                    ids -= ipx;
                }
            }

            // add tex here
            //Texture2D texture = new Texture2D((int)w, (int)h, TextureFormat.RGHalf, false); // too large, but meh.
            //texture.filterMode = FilterMode.Point;
            //texture.SetPixels(colors);
            //texture.Apply(false);
            frames[i] = new AllodsSpriteSeparate.Frame(sprite);
            frames[i].Width = (int)w;
            frames[i].Height = (int)h;
            frames[i]._Colors = colors;
            frames[i]._Texture = null;
            ms.Position = cpos + ds;
        }

        br.Close();

        if (oldCount != count)
            Array.Resize(ref frames, count);
        sprite.Frames = frames;

        return sprite;
    }

    // read a .256 sprite to a texture.
    // this is extensively used for rendering human picture.
    public static Texture2D Load256AsTexture(string filename)
    {
        AllodsSpriteSeparate sep = Load256Separate(filename, true);
        return sep.Frames[0].TextureRawTruecolor;
    }

    public static AllodsSprite Load256(string filename, bool hasOwnPalette = true)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        BinaryReader br = new BinaryReader(ms);

        ms.Position = ms.Length - 4;
        int count = br.ReadInt32() & 0x7FFFFFFF;

        ms.Position = 0;

        AllodsSprite sprite = new AllodsSprite();
        // read palette
        if (hasOwnPalette)
            sprite.OwnPalette = LoadPaletteFromStream(br);
        else sprite.OwnPalette = null;
        Texture2D[] frames = new Texture2D[count];

        int oldCount = count;
        for (int i = 0; i < count; i++)
        {
            uint w = br.ReadUInt32();
            uint h = br.ReadUInt32();
            uint ds = br.ReadUInt32();
            long cpos = ms.Position;

            if (w > 512 || h > 512 || ds > 1000000)
            {
                //Core.Abort("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                Debug.LogFormat("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                i--;
                count--;
                continue;
            }

            Color[] colors = new Color[w * h];
            for (int j = 0; j < colors.Length; j++)
                colors[j].g = 0;

            int ix = 0;
            int iy = 0;
            int ids = (int)ds;
            while (ids > 0)
            {
                ushort ipx = br.ReadByte();
                ipx |= (ushort)(ipx << 8);
                ipx &= 0xC03F;
                ids--;

                if ((ipx & 0xC000) > 0)
                {
                    if ((ipx & 0xC000) == 0x4000)
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx * w);
                    }
                    else
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx);
                    }
                }
                else
                {
                    ipx &= 0x3F;
                    for (int j = 0; j < ipx; j++)
                    {
                        byte ss = br.ReadByte();
                        //uint px = (ss << 16) | (ss << 8) | (ss) | 0xFF000000;
                        colors[iy * w + ix] = new Color((float)ss / 255f, 1, 0, 0);
                        SpriteAddIXIY(ref ix, ref iy, w, 1);
                    }

                    ids -= ipx;
                }
            }

            // add tex here
            Texture2D texture = new Texture2D((int)w, (int)h, TextureFormat.RGHalf, false); // too large, but meh.
            texture.filterMode = FilterMode.Point;
            texture.SetPixels(colors);
            //texture.Apply(false);
            frames[i] = texture;
            ms.Position = cpos + ds;
        }

        br.Close();

        if (oldCount != count)
            Array.Resize(ref frames, count);

        sprite.Atlas = new Texture2D(0, 0, TextureFormat.RGHalf, false);
        sprite.AtlasRects = sprite.Atlas.PackTextures(frames, 0);
        sprite.Atlas.filterMode = FilterMode.Point;
        if (sprite.AtlasRects == null)
        {
            Core.Abort("Couldn't pack sprite \"{0}\"", filename);
            return null;
        }

        for (int i = 0; i < frames.Length; i++)
            GameObject.DestroyImmediate(frames[i]);

        sprite.Sprites = new Sprite[sprite.AtlasRects.Length];
        for (int i = 0; i < sprite.AtlasRects.Length; i++)
        {
            sprite.Sprites[i] = Sprite.Create(sprite.Atlas, new Rect(sprite.AtlasRects[i].x * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].y * sprite.Atlas.height,
                                                                     sprite.AtlasRects[i].width * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].height * sprite.Atlas.height), new Vector2(0, 0));
        }

        return sprite;
    }

    public static AllodsSprite Load16A(string filename, bool hasOwnPalette = true)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        BinaryReader br = new BinaryReader(ms);

        ms.Position = ms.Length - 4;
        int count = br.ReadInt32() & 0x7FFFFFFF;

        ms.Position = 0;

        AllodsSprite sprite = new AllodsSprite();
        // read palette
        if (hasOwnPalette)
            sprite.OwnPalette = LoadPaletteFromStream(br);
        else sprite.OwnPalette = null;
        Texture2D[] frames = new Texture2D[count];

        int oldCount = count;
        for (int i = 0; i < count; i++)
        {
            uint w = br.ReadUInt32();
            uint h = br.ReadUInt32();
            uint ds = br.ReadUInt32();
            long cpos = ms.Position;

            if (w > 512 || h > 512 || ds > 1000000)
            {
                Debug.LogFormat("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                i--;
                count--;
                continue;
            }

            Color[] colors = new Color[w * h];
            for (int j = 0; j < colors.Length; j++)
                colors[j].g = 0;

            int ix = 0;
            int iy = 0;
            int ids = (int)ds;
            while (ids > 0)
            {
                ushort ipx = br.ReadUInt16();
                ipx &= 0xC0FF;
                ids -= 2;

                if ((ipx & 0xC000) > 0)
                {
                    if ((ipx & 0xC000) == 0x4000)
                    {
                        ipx &= 0xFF;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx * w);
                    }
                    else
                    {
                        ipx &= 0xFF;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx);
                    }
                }
                else
                {
                    ipx &= 0xFF;
                    for (int j = 0; j < ipx; j++)
                    {
                        uint ss = br.ReadUInt16();
                        uint alpha = (((ss & 0xFF00) >> 9) & 0x0F) + (((ss & 0xFF00) >> 5) & 0xF0);
                        uint idx = ((ss & 0xFF00) >> 1) + ((ss & 0x00FF) >> 1);
                        idx &= 0xFF;
                        alpha &= 0xFF;
                        colors[iy * w + ix] = new Color((float)idx / 255f, (float)alpha / 255, 0, 0);
                        SpriteAddIXIY(ref ix, ref iy, w, 1);
                    }

                    ids -= ipx * 2;
                }
            }

            // add tex here
            Texture2D texture = new Texture2D((int)w, (int)h, TextureFormat.RGHalf, false); // too large, but meh.
            texture.filterMode = FilterMode.Point;
            texture.SetPixels(colors);
            //texture.Apply(false);
            frames[i] = texture;
            ms.Position = cpos + ds;
        }

        br.Close();

        if (oldCount != count)
            Array.Resize(ref frames, count);

        sprite.Atlas = new Texture2D(0, 0, TextureFormat.RGHalf, false);
        sprite.AtlasRects = sprite.Atlas.PackTextures(frames, 0);
        sprite.Atlas.filterMode = FilterMode.Point;
        if (sprite.AtlasRects == null)
        {
            Core.Abort("Couldn't pack sprite \"{0}\"", filename);
            return null;
        }

        for (int i = 0; i < frames.Length; i++)
            GameObject.DestroyImmediate(frames[i]);

        sprite.Sprites = new Sprite[sprite.AtlasRects.Length];
        for (int i = 0; i < sprite.AtlasRects.Length; i++)
        {
            sprite.Sprites[i] = Sprite.Create(sprite.Atlas, new Rect(sprite.AtlasRects[i].x * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].y * sprite.Atlas.height,
                                                                     sprite.AtlasRects[i].width * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].height * sprite.Atlas.height), new Vector2(0, 0));
        }

        return sprite;
    }

    public static AllodsSprite Load16(string filename)
    {
        MemoryStream ms = ResourceManager.OpenRead(filename);
        if (ms == null)
        {
            Core.Abort("Couldn't load \"{0}\"", filename);
            return null;
        }

        BinaryReader br = new BinaryReader(ms);

        ms.Position = ms.Length - 4;
        int count = br.ReadInt32() & 0x7FFFFFFF;

        ms.Position = 0;

        AllodsSprite sprite = new AllodsSprite();
        // read palette
        sprite.OwnPalette = null; // no such thing as the palette in .16 file
        Texture2D[] frames = new Texture2D[count];

        int oldCount = count;
        for (int i = 0; i < count; i++)
        {
            uint w = br.ReadUInt32();
            uint h = br.ReadUInt32();
            uint ds = br.ReadUInt32();
            long cpos = ms.Position;

            if (w > 512 || h > 512 || ds > 1000000)
            {
                Debug.LogFormat("Invalid sprite \"{0}\": NULL frame #{1}", filename, i);
                i--;
                count--;
                continue;
            }

            Color[] colors = new Color[w * h];
            for (int j = 0; j < colors.Length; j++)
                colors[j].g = 0;

            int ix = 0;
            int iy = 0;
            int ids = (int)ds;
            while (ids > 0)
            {
                ushort ipx = br.ReadByte();
                ipx |= (ushort)(ipx << 8);
                ipx &= 0xC03F;
                ids -= 1;

                if ((ipx & 0xC000) > 0)
                {
                    if ((ipx & 0xC000) == 0x4000)
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx * w);
                    }
                    else
                    {
                        ipx &= 0x3F;
                        SpriteAddIXIY(ref ix, ref iy, w, ipx);
                    }
                }
                else
                {
                    ipx &= 0x3F;

                    byte[] bytes = new byte[ipx];
                    for (int j = 0; j < ipx; j++)
                        bytes[j] = br.ReadByte();

                    for (int j = 0; j < ipx; j++)
                    {
                        uint alpha1 = (bytes[j] & 0x0Fu) | ((bytes[j] & 0x0Fu) << 4);
                        colors[iy * w + ix] = new Color(1, (float)alpha1 / 255, 0, 0);
                        SpriteAddIXIY(ref ix, ref iy, w, 1);

                        if (j != ipx - 1 || (bytes[bytes.Length - 1] & 0xF0) > 0)
                        {
                            uint alpha2 = (bytes[j] & 0xF0u) | ((bytes[j] & 0xF0u) >> 4);
                            colors[iy * w + ix] = new Color(1, (float)alpha2 / 255, 0, 0);
                            SpriteAddIXIY(ref ix, ref iy, w, 1);
                        }
                    }

                    ids -= ipx;
                }
            }

            // add tex here
            Texture2D texture = new Texture2D((int)w, (int)h, TextureFormat.RGHalf, false); // too large, but meh.
            texture.filterMode = FilterMode.Point;
            texture.SetPixels(colors);
            //texture.Apply(false);
            frames[i] = texture;
            ms.Position = cpos + ds;
        }

        br.Close();

        if (oldCount != count)
            Array.Resize(ref frames, count);

        sprite.Atlas = new Texture2D(0, 0, TextureFormat.RGHalf, false);
        sprite.AtlasRects = sprite.Atlas.PackTextures(frames, 0);
        sprite.Atlas.filterMode = FilterMode.Point;
        if (sprite.AtlasRects == null)
        {
            Core.Abort("Couldn't pack sprite \"{0}\"", filename);
            return null;
        }

        for (int i = 0; i < frames.Length; i++)
            GameObject.DestroyImmediate(frames[i]);

        sprite.Sprites = new Sprite[sprite.AtlasRects.Length];
        for (int i = 0; i < sprite.AtlasRects.Length; i++)
        {
            sprite.Sprites[i] = Sprite.Create(sprite.Atlas, new Rect(sprite.AtlasRects[i].x * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].y * sprite.Atlas.height,
                                                                     sprite.AtlasRects[i].width * sprite.Atlas.width,
                                                                     sprite.AtlasRects[i].height * sprite.Atlas.height), new Vector2(0, 0));
        }

        return sprite;
    }

    public static AllodsSprite LoadSprite(string filename)
    {
        string[] filename_split = filename.Split(new char[] { '.' });
        string ext = filename_split[filename_split.Length - 1].ToLower();
        if (ext == "16a") return Load16A(filename);
        else if (ext == "256") return Load256(filename);
        else if (ext == "16") return Load16(filename);
        else
        {
            Core.Abort("Couldn't load \"{0}\" (unknown extension)", filename);
            return null;
        }
    }
}
