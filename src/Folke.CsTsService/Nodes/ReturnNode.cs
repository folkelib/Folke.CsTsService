namespace Folke.CsTsService.Nodes
{
    public class ReturnNode : ITypedNode
    {
        public TypeNode Type { get; set; }
        public bool IsCollection { get; set; }
        public bool IsDictionary { get; set; }
        public string Documentation { get; set; }
    }
}
