using File_Parser;
using System;
using System.IO;

namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            FileInfo file = new FileInfo("./files/gifs/1.gif");
            if(file.Exists == false)
            {
                throw new Exception();
            }

            GifParser parser = new GifParser(file.OpenRead(), new GifReader());
            while(parser.Read())
            {

            }
        }

        private class GifReader : IGifParserCallback
        {
            public void ReadGraphicControlExtension(int disposalMethod, bool hasUserInput, bool hasTransparentColor, ushort frameDelay, byte transparentColorIndex)
            {
                
            }

            public void ReadHeader(ushort logicalSceenWidth, ushort logicalSceenHeight, byte backgroundColorIndex, byte aspectRatio, byte[] globalColorTable)
            {
                
            }

            public void ReadLoopCount(ushort loopCount)
            {
                
            }

            public void UnknownApplicationExtension(string applicationIdString, string v)
            {
                
            }
        }
    }
}
