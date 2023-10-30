namespace File_Parser
{
    public interface IGifParserCallback
    {
        void ReadHeader(ushort logicalSceenWidth, ushort logicalSceenHeight, byte backgroundColorIndex, byte aspectRatio, byte[] globalColorTable);
        void ReadLoopCount(ushort loopCount);
        void UnknownApplicationExtension(string applicationIdString, string v);
        void ReadGraphicControlExtension(int disposalMethod, bool hasUserInput, bool hasTransparentColor, ushort frameDelay, byte transparentColorIndex);
    }
}