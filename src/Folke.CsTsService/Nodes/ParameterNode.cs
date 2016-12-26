namespace Folke.CsTsService.Nodes
{
    public class ParameterNode : IConstraintsNode
    {
        public string Name { get; set; }
        public string Documentation { get; set; }
        public bool IsRequired { get; set; }
        public int? MinimumLength { get; set; }
        public int? MaximumLength { get; set; }
        public Format Format { get; set; }
        public string CompareTo { get; set; }
        public object Minimum { get; set; }
        public object Maximum { get; set; }
        public TypeNode Type { get; set; }
        public bool IsCollection { get; set; }
        public bool IsDictionary { get; set; }
        public ParameterPosition Position { get; set; }
    }
}
