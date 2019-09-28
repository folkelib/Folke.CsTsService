namespace Folke.CsTsService.Nodes
{
    public class EnumValueNode
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public string? Documentation { get; set; }

        public EnumValueNode(string name)
        {
            Name = name;
        }
    }
}
