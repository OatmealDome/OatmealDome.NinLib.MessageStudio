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
                ReadTextSection(reader, sectionSize);
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

    private void ReadTextSection(BinaryDataReader reader, int sectionSize)
    {
        long sectionStartOffset = reader.Position;

        int offsetCount = reader.ReadInt32();

        List<int> offsets = new List<int>();
        for (int i = 0; i < offsetCount; i++)
        {
            offsets.Add(reader.ReadInt32());
        }

        for (int i = 0; i < offsetCount; i++)
        {
            long startOffset = sectionStartOffset + offsets[i];
            
            long endOffset;
            if (i != offsetCount - 1)
            {
                endOffset = sectionStartOffset + offsets[i + 1];
            }
            else
            {
                endOffset = sectionStartOffset + sectionSize;
            }

            int length = (int)(endOffset - startOffset);

            using (reader.TemporarySeek(startOffset, SeekOrigin.Begin))
            {
                StringBuilder builder = new StringBuilder();

                while (reader.Position < endOffset)
                {
                    char c = reader.ReadChar();

                    if (c == 0xe) // control tag start
                    {
                        ushort group = reader.ReadUInt16();
                        ushort type = reader.ReadUInt16();
                        int parametersSize = reader.ReadUInt16();

                        byte[] parameters = reader.ReadBytes(parametersSize);

                        builder.Append($"[custom-tag group={group:x4} type={type:x4} params=");

                        builder.AppendJoin(' ', parameters.Select(x => x.ToString("x2")));

                        builder.Append("]");
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
                
                _strings.Add(builder.ToString());
            }
        }
    }
}
