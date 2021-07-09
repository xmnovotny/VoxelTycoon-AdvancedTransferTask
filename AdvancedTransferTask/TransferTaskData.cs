namespace AdvancedTransferTask
{
    public record TransferTaskData
    {
        public int? Percent { get; init; }
        public bool FullAny { get; init; }
        
        public bool IsDefault => Percent == null && !FullAny;
    }
}