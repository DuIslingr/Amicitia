﻿namespace AtlusLibSharp.Graphics.RenderWare
{
    using System.IO;

    /// <summary>
    /// Represents a RenderWare node holding the Sky Mipmap value. Usage is unknown.
    /// </summary>
    internal class RWSkyMipMapValue : RWNode
    {
        internal const int SKY_MIPMAP_VALUE = 0x00000FC0;

        public RWSkyMipMapValue() : base(RWNodeType.SkyMipMapValue) { }

        internal RWSkyMipMapValue(RWNodeFactory.RWNodeInfo header, BinaryReader reader)
            : base(header)
        {
            int skyMipMapValue = reader.ReadInt32();
        }

        protected internal override void InternalWriteInnerData(BinaryWriter writer)
        {
            writer.Write(SKY_MIPMAP_VALUE);
        }
    }
}
