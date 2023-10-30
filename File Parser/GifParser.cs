using System;
using System.IO;
using System.Runtime.InteropServices;

namespace File_Parser
{
    public class GifParser
    {
        private BinaryReader reader;
        private IGifParserCallback callback;

        private Header header;
        private byte[] globalColorTable;

        public GifParser(Stream stream, IGifParserCallback callback)
        {
            reader = new BinaryReader(stream);
            this.callback = callback;
        }

        public bool Read()
        {
            if(header == null)
            { 
                ReadHeader();
                return true;
            }
            else
            {
                char c = reader.ReadChar();
                switch (c)
                {
                    case '!':
                        ReadExtension();
                        return true;
                    case ',':
                        ReadImage();
                        return true;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private void ReadImage()
        {
            UInt16 left = reader.ReadUInt16();
            UInt16 top = reader.ReadUInt16();
            UInt16 width = reader.ReadUInt16();
            UInt16 height = reader.ReadUInt16();

            byte flags = reader.ReadByte();
            bool interlace = (flags & 0x40) != 0;

            byte[] colorTable;

            bool hasLocalColorTable = (flags & 0x80) != 0;
            if (hasLocalColorTable)
            {
                int localColorTableSize = 1 << ((flags & 0x07) + 1);
                byte[] localColorTable = reader.ReadBytes(localColorTableSize * 3);
                colorTable = localColorTable;
            }
            else
            {
                colorTable = globalColorTable;
            }

            byte sub_len, shift, b;
            int key_size = 0;

            int init_key_size, table_is_full;
            int frm_off, frm_size, str_len, i, p, x, y;
            UInt16 clear = 0;
            UInt16 key, stop;
            int ret;
            Table table;
            Entry entry;

            clear = (UInt16)(1 << key_size);
            stop = (UInt16)(clear + 1);
            table = new_table(key_size);
            key_size++;
            init_key_size = key_size;
            sub_len = shift = 0;
            key = get_key(gif, key_size, &sub_len, &shift, &byte); /* clear code */
            frm_off = 0;
            ret = 0;
            frm_size = width * height;
            while (frm_off < frm_size)
            {
                if (key == clear)
                {
                    key_size = init_key_size;
                    table.nentries = (1 << (key_size - 1)) + 2;
                    table_is_full = 0;
                }
                else if (!table_is_full)
                {
                    ret = add_entry(&table, str_len + 1, key, entry.suffix);
                    if (ret == -1)
                    {
                        free(table);
                        return -1;
                    }
                    if (table.nentries == 0x1000)
                    {
                        ret = 0;
                        table_is_full = 1;
                    }
                }
                key = get_key(gif, key_size, &sub_len, &shift, &byte);
                if (key == clear) continue;
                if (key == stop || key == 0x1000) break;
                if (ret == 1) key_size++;
                entry = table.entries[key];
                str_len = entry.length;
                for (i = 0; i < str_len; i++)
                {
                    p = frm_off + entry.length - 1;
                    x = p % width;
                    y = p / width;
                    if (interlace)
                        y = interlaced_line_index((int)height, y);
                    gif->frame[(top + y) * header.LogicalSceenWidth + left + x] = entry.suffix;
                    if (entry.prefix == 0xFFF)
                        break;
                    else
                        entry = table.entries[entry.prefix];
                }
                frm_off += str_len;
                if (key < table.nentries - 1 && !table_is_full)
                    table.entries[table.nentries - 1].suffix = entry.suffix;
            }
            free(table);
            if (key == stop)
                read(gif->fd, &sub_len, 1); /* Must be zero! */
        }

        static Table new_table(int key_size)
        {
            byte key;
            int init_bulk = Math.Max(1 << (key_size + 1), 0x100);
            Table table = new Table
            {
                bulk = init_bulk,
                nentries = (1 << key_size) + 2,
                entries = new Entry[init_bulk]
            };

            for (key = 0; key < (1 << key_size); key++)
            {
                table.entries[key] = new Entry { length = 1, prefix = 0xFFF, suffix = key };
            }
            return table;
        }

        private void ReadHeader()
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(Header)));

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Header header = (Header)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Header));
            handle.Free();

            bool HasGlobalColorTable = (header.LogicalScreenDescriptor & 0x80) != 0;
            int depth = ((header.LogicalScreenDescriptor >> 4) & 7) + 1;
            int globalColorTableSize = 1 << ((header.LogicalScreenDescriptor & 0x07) + 1);

            //3 bytes per table entry
            byte[] globalColorTable = reader.ReadBytes(globalColorTableSize * 3);

            this.header = header;
            callback.ReadHeader(header.LogicalSceenWidth, header.LogicalSceenHeight, header.BackgroundColorIndex, header.AspectRatio, globalColorTable);
        }

        private void ReadExtension()
        {
            int extensionType = reader.ReadByte();
            switch(extensionType)
            {
                //case 0x01:
                //    read_plain_text_ext(gif);
                //    break;
                case 0xF9:
                    ReadGraphicControlExtension();
                    break;
                //case 0xFE:
                //    read_comment_ext(gif);
                //    break;
                case 0xFF:
                    ReadApplicationExtension();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void ReadGraphicControlExtension()
        {
            reader.ReadByte();
            //read block size. should be 4

            byte gce = reader.ReadByte();
            int disposalMethod = (gce >> 2) & 3;
            bool hasUserInput = (gce & 2) != 0;
            bool hasTransparentColor = (gce & 1) != 0;

            UInt16 frameDelay = reader.ReadUInt16();
            byte transparentColorIndex = reader.ReadByte();
            callback.ReadGraphicControlExtension(disposalMethod, hasUserInput, hasTransparentColor, frameDelay, transparentColorIndex);

            //terminating block
            //should be 0
            reader.ReadByte();
        }

        private void ReadApplicationExtension()
        {
            reader.ReadByte();
            //read block size. should be 11

            char[] applicationId = reader.ReadChars(8);
            char[] authCode = reader.ReadChars(3);

            string applicationIdString = new string(applicationId);

            switch(applicationIdString)
            {
                case "NETSCAPE":
                    ReadNetscapeExtension();
                    break;
                default:
                    callback.UnknownApplicationExtension(applicationIdString, new string(authCode));
                    SkipSubBlocks();
                    break;
            }
        }

        private void ReadNetscapeExtension()
        {
            reader.ReadByte();
            //read block size, should be 3

            //skip over unused byte
            reader.ReadByte();

            UInt16 loopCount = reader.ReadUInt16();
            callback.ReadLoopCount(loopCount);

            //terminating block
            //should be 0
            reader.ReadByte();
        }

        /// <summary>
        /// skips over blocks we don't want to read
        /// </summary>
        private void SkipSubBlocks()
        {
            int blockSize = reader.ReadByte();
            reader.ReadBytes(blockSize);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private class Header
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public Char[] VersionHeader;

            public UInt16 LogicalSceenWidth;

            public UInt16 LogicalSceenHeight;

            public Byte LogicalScreenDescriptor;

            public Byte BackgroundColorIndex;

            public Byte AspectRatio;
        }

        struct Entry
        {
            public UInt16 length;
            public UInt16 prefix;
            public byte suffix;
        }

        struct Table
        {
            public int bulk;
            public int nentries;
            public Entry[] entries;
        }
    }
}
