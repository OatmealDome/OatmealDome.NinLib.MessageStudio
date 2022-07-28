using System.Diagnostics;
using System.Text;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.MessageStudio;

public sealed class Msbt : MessageStudioFile
{
    protected override string FileMagic => "MsgStdBn";

    protected override string FileType => "Msbt";

    private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _labelValueIdx = new Dictionary<string, int>();
    private readonly List<string> _strings = new List<string>();

    public IEnumerable<string> Keys => _values.Keys;

    public Msbt(byte[] data) : base(data)
    {
    }

    public Msbt(Stream stream) : base(stream)
    {
    }
    
    public bool ContainsKey(string label)
    {
        return _values.ContainsKey(label);
    }

    public string Get(string label)
    {
        return _values[label];
    }

    protected override void ReadSection(BinaryDataReader reader, string sectionMagic, int sectionSize)
    {
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
    }

    protected override void FinalizeRead(BinaryDataReader reader)
    {
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
                string label = reader.ReadString(StringDataFormat.ZeroTerminated, FileEncoding);

                _strings.Add(label);
            }
        }
    }
}
