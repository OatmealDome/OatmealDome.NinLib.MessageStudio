using System.Diagnostics;
using System.Text;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.MessageStudio;

public abstract class MessageStudioFile
{
    protected abstract string FileMagic
    {
        get;
    }
    
    protected abstract string FileType
    {
        get;
    }

    protected ByteOrder FileByteOrder;
    protected Encoding FileEncoding;

    protected struct HashTableEntry
    {
        public string Label;
        public int Index;
    }
    
    protected MessageStudioFile(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream(data))
        {
            Read(memoryStream);
        }
    }
    
    protected MessageStudioFile(Stream stream)
    {
        Read(stream);
    }
    
    private void Read(Stream stream)
    {
        ByteOrder byteOrder = ByteOrder.BigEndian;

        using (BinaryDataReader startReader = new BinaryDataReader(stream, true))
        {
            // Set endianness to big by default
            startReader.ByteOrder = ByteOrder.BigEndian;

            // Verify the magic numbers
            if (startReader.ReadString(8) != FileMagic)
            {
                throw new MessageStudioException($"Not a {FileType} file");
            }

            // Read BOM
            ushort bom = startReader.ReadUInt16();
            if (bom == 0xFFFE)
            {
                byteOrder = ByteOrder.LittleEndian;
            }

            startReader.Seek(2); // padding?

            byte encoding = startReader.ReadByte();
            switch (encoding)
            {
                case 0x0:
                    FileEncoding = Encoding.UTF8;
                    break;
                case 0x1:
                    if (byteOrder == ByteOrder.BigEndian)
                    {
                        FileEncoding = Encoding.BigEndianUnicode;
                    }
                    else
                    {
                        FileEncoding = Encoding.Unicode;
                    }

                    break;
                case 0x2:
                    FileEncoding = Encoding.UTF32;
                    break;
                default:
                    throw new MessageStudioException($"Unsupported encoding '{encoding:x}'");
            }
        }

        FileByteOrder = byteOrder;

        using BinaryDataReader reader = new BinaryDataReader(stream, FileEncoding);
        reader.ByteOrder = FileByteOrder;

        byte version = reader.ReadByte();
        if (version != 0x3)
        {
            throw new MessageStudioException($"Unsupported version '{version}'");
        }
        
        ushort sectionCount = reader.ReadUInt16();

        reader.Seek(2); // padding?

        uint fileSize = reader.ReadUInt32();

        reader.Seek(10); // padding?

        for (int i = 0; i < sectionCount; i++)
        {
            Trace.Assert(reader.Position % 0x10 == 0);

            string sectionMagic = reader.ReadString(4, Encoding.UTF8);
            int sectionSize = reader.ReadInt32();

            reader.Seek(8); // padding

            long sectionStart = reader.Position;

            ReadSection(reader, sectionMagic, sectionSize);

            // Seek to next table
            reader.Seek(sectionStart + sectionSize, SeekOrigin.Begin);
            reader.Align(0x10);
        }
        
        FinalizeRead(reader);
    }

    protected List<HashTableEntry> ReadHashTable(BinaryDataReader reader)
    {
        long startOffset = reader.Position;

        int slotCount = reader.ReadInt32();

        List<HashTableEntry> entries = new List<HashTableEntry>();
        for (int i = 0; i < slotCount; i++)
        {
            int labelCount = reader.ReadInt32();
            int labelOffset = reader.ReadInt32();

            using (reader.TemporarySeek(startOffset + labelOffset, SeekOrigin.Begin))
            {
                for (int j = 0; j < labelCount; j++)
                {
                    int stringLength = reader.ReadByte();
                    string label = reader.ReadString(stringLength, Encoding.UTF8);
                    int index = reader.ReadInt32();

                    entries.Add(new HashTableEntry()
                    {
                        Label = label,
                        Index = index
                    });
                }
            }
        }

        return entries;
    }

    protected abstract void ReadSection(BinaryDataReader reader, string sectionMagic, int sectionSize);

    protected abstract void FinalizeRead(BinaryDataReader reader);
}
