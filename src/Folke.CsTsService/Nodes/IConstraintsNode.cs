namespace Folke.CsTsService.Nodes
{
    interface IConstraintsNode
    {
        bool IsRequired { get; set; }
        int? MinimumLength { get; set; }
        int? MaximumLength { get; set; }
        Format Format { get; set; }
        string CompareTo { get; set; }
        object Minimum { get; set; }
        object Maximum { get; set; }
    }
}
