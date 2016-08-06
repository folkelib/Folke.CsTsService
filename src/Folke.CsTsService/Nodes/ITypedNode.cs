namespace Folke.CsTsService.Nodes
{
    public interface ITypedNode
    {
        TypeNode Type { get; set; }
        bool IsCollection { get; set; }
        bool IsDictionary { get; set; }
    }
}
