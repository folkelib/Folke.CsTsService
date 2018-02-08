using System.Collections.Generic;

namespace Folke.CsTsService.Nodes
{
    public class TypeNode
    {
        public TypeIdentifier Type { get; set; }
        public ClassNode Class { get; set; }
       
        public TypeNode[] Union { get; internal set; }

        public bool IsObservable { get; set; }

        public List<TypeModifier> Modifiers { get; } = new List<TypeModifier>();

        public List<TypeNode> GenericParameters { get; set; }

        public string GenericName { get; set; }
        public bool IsCollection => this.Modifiers.Count == 1 && Modifiers[0] == TypeModifier.Array;
        public bool IsDictionary => Modifiers.Count == 1 && Modifiers[0] == TypeModifier.Dictionary;
    }
}
