namespace Folke.CsTsService.Nodes
{
    public class ReturnNode
    {
        public TypeNode Type { get; set; }
        public string? Documentation { get; set; }

        public ReturnNode(TypeNode type)
        {
            Type = type;
        }
    }
}
