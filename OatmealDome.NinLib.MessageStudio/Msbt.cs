using System.Diagnostics;
using System.Text;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.MessageStudio;

public sealed class Msbt : MessageStudioFile
{
    protected override string FileMagic => "MsgStdBn";

    protected override string FileType => "Msbt";

    private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
    private List<HashTableEntry> _messageLabelEntries = new List<HashTableEntry>();
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
                _messageLabelEntries = ReadHashTable(reader);
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
        foreach (HashTableEntry entry in _messageLabelEntries)
        {
            _values[entry.Label] = _strings[entry.Index];
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
