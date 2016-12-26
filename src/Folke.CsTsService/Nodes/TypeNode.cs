using System.Collections.Generic;

namespace Folke.CsTsService.Nodes
{
    public class TypeNode
    {
        public TypeIdentifier Type { get; set; }
        public ClassNode Class { get; set; }
       
        public TypeNode[] Union { get; internal set; }

        public bool IsObservable { get; set; }
        public bool IsCollection { get; set; }

        public bool IsDictionary { get; set; }

        public List<TypeNode> GenericParameters { get; set; }

        public string GenericName { get; set; }
    }
}
