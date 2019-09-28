using System.Collections.Generic;

namespace Folke.CsTsService.Nodes
{
    public class ActionNode
    {
        public string? Authorization { get; set; }
        public ActionMethod Type { get; set; }
        public string Name { get; set; }
        public string Route { get; set; }
        public string? Version { get; set; }
        public string? Documentation { get; set; }
        public ActionsGroupNode Group { get; set; }
        public List<ParameterNode> Parameters { get; } = new List<ParameterNode>();
        public ReturnNode? Return { get; set; }

        public ActionNode(ActionsGroupNode actionsGroupNode, string name, string route) => (Group, Name, Route) = (actionsGroupNode, name, route);
    }
}
