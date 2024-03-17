using System.Diagnostics;
using System.Text;
using OatmealDome.BinaryData;

namespace OatmealDome.NinLib.MessageStudio;

public sealed class Msbt : MessageStudioFile
{
    protected override string FileMagic => "MsgStdBn";

    protected override string FileType => "Msbt";

    private readonly Dictionary<string, byte[]> _values = new Dictionary<string, byte[]>();

    private List<HashTableEntry> _messageLabelEntries = new List<HashTableEntry>();
    private List<byte[]> _stringData = new List<byte[]>();

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
        using MemoryStream stream = new MemoryStream(_values[label]);
        using BinaryDataReader reader = new BinaryDataReader(stream, FileEncoding);
        reader.ByteOrder = FileByteOrder;
        
        StringBuilder builder = new StringBuilder();

        // Tracks where we need to insert the closing ruby tag.
        long? rubyEndOffset = null;
        
        while (reader.Position < reader.Length)
        {
            char c = reader.ReadChar();

            if (c == 0xe) // control tag start
            {
                ushort group = reader.ReadUInt16();
                ushort type = reader.ReadUInt16();
                int parametersSize = reader.ReadUInt16();

                // Group 0 is "System", which is provided by the Message Studio library.
                if (group == 0)
                {
                    switch (type)
                    {
                        case 0: // Ruby (furigana)
                            int rubySize = reader.ReadUInt16();
                            int rubyTextLength = reader.ReadUInt16();
                            
                            byte[] rubyTextRaw = reader.ReadBytes(rubyTextLength);
                            string rubyText = FileEncoding.GetString(rubyTextRaw);

                            rubyEndOffset = reader.Position + rubySize;

                            builder.Append($"[ruby=\"{rubyText}\"]");

                            break;
                        case 2: // Size
                            Trace.Assert(parametersSize == 2, "Parameter size for size tag is not 2 bytes");
                            
                            int percent = reader.ReadUInt16();
                            builder.Append($"[size={percent}%]");

                            break;
                        case 3: // Color
                            Trace.Assert(parametersSize == 2 || parametersSize == 4, "Parameter size for color is not 2 or 4 bytes");
                            
                            uint colorIdx;
                            
                            if (parametersSize == 2)
                            {
                                colorIdx = reader.ReadUInt16();
                            }
                            else
                            {
                                colorIdx = reader.ReadUInt32();
                            }

                            string colorIdxStr = colorIdx.ToString("x" + (parametersSize * 2));

                            builder.Append($"[color={colorIdxStr}]");
                            
                            break;
                        case 4: // Page Break
                            Trace.Assert(parametersSize == 0, "Parameter size for page break is not 0 bytes");

                            builder.Append("[page break]");
                            
                            break;
                        default:
                            // TODO: Font tag
                            throw new MessageStudioException($"Unsupported system tag type '{type:x2}'");
                    }
                }
                else
                {
                    byte[] parameters = reader.ReadBytes(parametersSize);
                    
                    builder.Append($"[group={group:x4} type={type:x4} params=");
                    builder.AppendJoin(' ', parameters.Select(x => x.ToString("x2")));
                    builder.Append("]");
                }
            }
            else if (c == 0xf)
            {
                ushort group = reader.ReadUInt16();
                ushort type = reader.ReadUInt16();
                
                builder.Append($"[/group={group:x4} type={type:x4}]");
            }
            else
            {
                builder.Append(c);
                
                if (rubyEndOffset.HasValue && rubyEndOffset <= reader.Position)
                {
                    builder.Append("[/ruby]");

                    rubyEndOffset = null;
                }
            }
        }

        // Strip the trailing NUL byte.
        builder.Length -= 1;

        return builder.ToString();
    }
    
    public string GetWithoutTags(string label)
    {
        using MemoryStream stream = new MemoryStream(_values[label]);
        using BinaryDataReader reader = new BinaryDataReader(stream, FileEncoding);
        reader.ByteOrder = FileByteOrder;
        
        StringBuilder builder = new StringBuilder();

        while (reader.Position < reader.Length)
        {
            char c = reader.ReadChar();

            if (c == 0xe) // control tag start
            {
                reader.Seek(4); // skip group and type
                
                int parametersSize = reader.ReadUInt16();
                reader.Seek(parametersSize);
            }
            else if (c == 0xf) // control tag end
            {
                reader.Seek(4); // skip group and type
            }
            else
            {
                builder.Append(c);
            }
        }
        
        // Strip the trailing NUL byte.
        builder.Length -= 1;

        return builder.ToString();
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
            _values[entry.Label] = _stringData[entry.Index];
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
                _stringData.Add(reader.ReadBytes(length));
            }
        }
    }
}
