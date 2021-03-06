﻿namespace AtlusLibSharp.Scripting
{
    using System.IO;
    using System.Collections.Generic;
    using System.Text;

    using AtlusLibSharp.Utilities;
    using IO;
    using Compression;

    public class BMDFile : BinaryFileBase
    {
        internal const short FLAG = 0x0007;
        internal const string TAG = "MSG1";
        internal const byte DATA_START_ADDRESS = 0x20;
        internal const int UNK_CONSTANT = 0x20000;

        private BMDMessage[] _messages;
        private string[] _actorNames;

        #region Properties

        public int DialogCount
        {
            get { return _messages.Length; }
        }

        public BMDMessage[] Messages
        {
            get { return _messages; }
            internal set { _messages = value; }
        }

        public int ActorCount
        {
            get { return _actorNames.Length; }
        }

        public string[] ActorNames
        {
            get { return _actorNames; }
            internal set { _actorNames = value; }
        }

        #endregion

        public BMDFile(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
                InternalRead(reader);
        }

        public BMDFile(Stream stream, bool leaveStreamOpen)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.Default, leaveStreamOpen))
                InternalRead(reader);
        }

        internal BMDFile(BinaryReader reader)
        {
            InternalRead(reader);
        }

        internal BMDFile()
        {
        }

        internal override void InternalWrite(BinaryWriter writer)
        {
            int posFileStart = (int)writer.BaseStream.Position;
            List<int> addressList = new List<int>();

            // Seek past chunk and msg header for writing later
            writer.BaseStream.Seek(DATA_START_ADDRESS, SeekOrigin.Current);

            // Write a dummy message pointer table
            for (int i = 0; i < DialogCount; i++)
            {
                writer.Write(0);
                addressList.Add((int)writer.BaseStream.Position - posFileStart);
                writer.Write(0);
            }

            // Write a dummy offset for writing later
            addressList.Add((int)writer.BaseStream.Position - posFileStart);
            writer.Write(0);
            writer.Write(ActorCount);

            // These are always here for some reason
            writer.Write(0);
            writer.Write(0);

            writer.AlignPosition(4);

            BMDMessageTable[] messagePointerTable = new BMDMessageTable[DialogCount];

            // Write the messages and fill in the message pointer table
            for (int i = 0; i < DialogCount; i++)
            {
                writer.AlignPosition(4);
                messagePointerTable[i].Offset = (int)writer.BaseStream.Position - DATA_START_ADDRESS - posFileStart;
                _messages[i].InternalWrite(writer, ref addressList, posFileStart);
            }

            writer.AlignPosition(4);
            int actorNamePointerTableOffset = (int)writer.BaseStream.Position - DATA_START_ADDRESS - posFileStart;

            // Write dummy actor name pointer table if there are actors present
            if (ActorCount > 0)
            {
                long actorNamePointerTablePosition = writer.BaseStream.Position;
                for (int i = 0; i < ActorCount; i++)
                {
                    addressList.Add((int)writer.BaseStream.Position - posFileStart);
                    writer.Write(0);
                }

                int[] actorNamePointerTable = new int[ActorCount];
                for (int i = 0; i < actorNamePointerTable.Length; i++)
                {
                    actorNamePointerTable[i] = (int)writer.BaseStream.Position - DATA_START_ADDRESS - posFileStart;
                    writer.WriteCString(_actorNames[i]);
                }

                long addresRelocPosition = writer.BaseStream.Position;

                writer.BaseStream.Seek(actorNamePointerTablePosition, SeekOrigin.Begin);
                writer.Write(actorNamePointerTable);

                writer.BaseStream.Seek(addresRelocPosition, SeekOrigin.Begin);
            }

            // Compress and write the address relocationt able
            byte[] addressRelocTable = PointerRelocationTableCompression.Compress(addressList, DATA_START_ADDRESS);
            int addressRelocTableOffset = (int)writer.BaseStream.Position - posFileStart;
            int addressRelocTableSize = addressRelocTable.Length;
            writer.Write(addressRelocTable);

            // Save the end offset for calculating length and seeking later
            long posFileEnd = writer.BaseStream.Position;
            int length = (int)(posFileEnd - posFileStart);

            // Seek back to the start
            writer.BaseStream.Seek(posFileStart, SeekOrigin.Begin);

            // Write Chunk header
            writer.Write(FLAG);
            writer.Write((short)0); // userID
            writer.Write(length);
            writer.WriteCString(TAG, 4);
            writer.Write(0);

            // Write MSG header
            writer.Write(addressRelocTableOffset);
            writer.Write(addressRelocTableSize);
            writer.Write(DialogCount);
            writer.Write(UNK_CONSTANT);

            for (int i = 0; i < DialogCount; i++)
            {
                writer.Write((int)messagePointerTable[i].Type);
                writer.Write(messagePointerTable[i].Offset);
            }

            writer.Write(actorNamePointerTableOffset);

            writer.BaseStream.Seek(posFileEnd, SeekOrigin.Begin);
        }

        private void InternalRead(BinaryReader reader)
        {
            long posFileStart = reader.GetPosition();
            short flag = reader.ReadInt16();
            short userID = reader.ReadInt16();
            int length = reader.ReadInt32();
            string tag = reader.ReadCString(4);
            reader.AlignPosition(16);

            if (tag != TAG)
            {
                throw new InvalidDataException();
            }

            int addressRelocTableOffset = reader.ReadInt32();
            int addressRelocTableSize = reader.ReadInt32();
            int numMessages = reader.ReadInt32();
            short isRelocated = reader.ReadInt16(); // actually a byte but not very important
            short unk0x1E = reader.ReadInt16();

            /*
            if (unk0x1C != UNK_CONSTANT)
            {
                Console.WriteLine("_unk0x1C isn't 0x20000");
            }
            */

            BMDMessageTable[] messagePointerTable = new BMDMessageTable[numMessages];
            for (int i = 0; i < messagePointerTable.Length; i++)
            {
                messagePointerTable[i].Type = (BMDMessageType)reader.ReadInt32();
                messagePointerTable[i].Offset = reader.ReadInt32();
            }

            int actorNamePointerTableOffset = reader.ReadInt32();
            int numActors = reader.ReadInt32();

            reader.BaseStream.Seek(posFileStart + DATA_START_ADDRESS + actorNamePointerTableOffset, SeekOrigin.Begin);
            int[] actorNamePointerTable = reader.ReadInt32Array(numActors);

            _actorNames = new string[numActors];
            for (int i = 0; i < _actorNames.Length; i++)
            {
                reader.BaseStream.Seek(posFileStart + DATA_START_ADDRESS + actorNamePointerTable[i], SeekOrigin.Begin);
                _actorNames[i] = reader.ReadCString();
            }

            _messages = new BMDMessage[numMessages];
            for (int i = 0; i < _messages.Length; i++)
            {
                _messages[i] = BMDMessageFactory.GetMessage(reader, (int)posFileStart, messagePointerTable[i]);
            }
        }
    }

    struct BMDMessageTable
    {
        public BMDMessageType Type;
        public int Offset;
    }

    public enum BMDMessageType : int
    {
        Standard = 0,
        Selection = 1
    }
}
