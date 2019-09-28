using System.Collections.Generic;

namespace Folke.CsTsService.Nodes
{
    public class ActionsGroupNode
    {
        public AssemblyNode Assembly { get; set; }
        public string Name { get; set; }
        public string? Documentation { get; set; }
        public List<ActionNode> Actions { get; } = new List<ActionNode>();

        public ActionsGroupNode(AssemblyNode assembly, string name) => (Assembly, Name) = (assembly, name);
    }
}
