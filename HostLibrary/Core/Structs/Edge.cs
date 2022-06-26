namespace HostLibrary.Core.Structs
{
    public struct Edge
    {
        public string From { get; set; }
        public string To { get; set; }

        public override int GetHashCode() => From.GetHashCode() ^ To.GetHashCode();

        public override bool Equals(object obj)
        {
            var other = obj as Edge?;
            if (!other.HasValue) return false;

            return From == other.Value.From && To == other.Value.To;
        }
    }
}
