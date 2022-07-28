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
    
    protected Encoding FileEncoding;
    
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
        using BinaryDataReader reader = new BinaryDataReader(stream);

        // Set endianness to big by default
        reader.ByteOrder = ByteOrder.BigEndian;

        // Verify the magic numbers
        if (reader.ReadString(8) != FileMagic)
        {
            throw new MessageStudioException($"Not a {FileType} file");
        }

        // Read BOM
        ushort bom = reader.ReadUInt16();
        if (bom == 0xFFFE)
        {
            reader.ByteOrder = ByteOrder.LittleEndian;
        }

        reader.Seek(2); // padding?

        byte encoding = reader.ReadByte();
        switch (encoding)
        {
            case 0x0:
                FileEncoding = Encoding.UTF8;
                break;
            case 0x1:
                if (reader.ByteOrder == ByteOrder.BigEndian)
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

            string sectionMagic = reader.ReadString(4);
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

    protected abstract void ReadSection(BinaryDataReader reader, string sectionMagic, int sectionSize);

    protected abstract void FinalizeRead(BinaryDataReader reader);
}
