using System.Diagnostics;
using System.Text;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.MessageStudio;

public sealed class Msbt
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _labelValueIdx = new Dictionary<string, int>();
    private readonly List<string> _strings = new List<string>();

    private Encoding _encoding;

    public IEnumerable<string> Keys => _values.Keys;

    public Msbt(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream(data))
        {
            Read(memoryStream);
        }
    }

    public Msbt(Stream stream)
    {
        Read(stream);
    }

    public bool ContainsKey(string label)
    {
        return _values.ContainsKey(label);
    }

    public string Get(string label)
    {
        return _values[label];
    }

    // Based on MSBT file format documentation on ZeldaMods
    private void Read(Stream stream)
    {
        using BinaryDataReader reader = new BinaryDataReader(stream);

        // Set endianness to big by default
        reader.ByteOrder = ByteOrder.BigEndian;

        // Verify the magic numbers
        if (reader.ReadString(8) != "MsgStdBn")
        {
            throw new MessageStudioException("Not a MSBT file");
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
                _encoding = Encoding.UTF8;
                break;
            case 0x1:
                if (reader.ByteOrder == ByteOrder.BigEndian)
                {
                    _encoding = Encoding.BigEndianUnicode;
                }
                else
                {
                    _encoding = Encoding.Unicode;
                }

                break;
            case 0x2:
                _encoding = Encoding.UTF32;
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

            switch (sectionMagic)
            {
                case "LBL1":
                    ReadLabelsSection(reader);
                    break;
                case "TXT2":
                    ReadTextSection(reader);
                    break;
                default:
                    // ATR1 not implemented
                    break;
            }

            // Seek to next table
            reader.Seek(sectionStart + sectionSize, SeekOrigin.Begin);
            reader.Align(0x10);
        }

        foreach (KeyValuePair<string, int> labelPair in _labelValueIdx)
        {
            _values[labelPair.Key] = _strings[labelPair.Value];
        }
    }

    private void ReadLabelsSection(BinaryDataReader reader)
    {
        long startOffset = reader.Position;

        int offsetCount = reader.ReadInt32();

        for (int i = 0; i < offsetCount; i++)
        {
            int stringCount = reader.ReadInt32();
            int stringOffset = reader.ReadInt32();

            using (reader.TemporarySeek(startOffset + stringOffset, SeekOrigin.Begin))
            {
                for (int j = 0; j < stringCount; j++)
                {
                    int stringLength = reader.ReadByte();
                    string label = reader.ReadString(stringLength);
                    int textTableIndex = reader.ReadInt32();

                    _labelValueIdx[label] = textTableIndex;
                }
            }
        }
    }

    private void ReadTextSection(BinaryDataReader reader)
    {
        long startOffset = reader.Position;

        int offsetCount = reader.ReadInt32();

        for (int i = 0; i < offsetCount; i++)
        {
            int stringOffset = reader.ReadInt32();

            using (reader.TemporarySeek(startOffset + stringOffset, SeekOrigin.Begin))
            {
                string label = reader.ReadString(StringDataFormat.ZeroTerminated, _encoding);

                _strings.Add(label);
            }
        }
    }
}
